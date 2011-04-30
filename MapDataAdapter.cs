using System;
using System.Collections.Generic;
using MapRendererCL;
using OpenSim.Region.Framework.Scenes;
using log4net;
using OpenSim.Framework;
using System.Reflection;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using OpenSim.Framework.Servers.HttpServer;
using OpenMetaverse;
using FreeImageAPI;
using OpenSim.Services.Interfaces;
using Nini.Config;
using System.Threading;
using MySql.Data.MySqlClient;
using System.Data.SQLite;
using System.Drawing.Drawing2D;
using OpenSim.ApplicationPlugins.MapDataAdapter.Layers;

//using PrimMesher;


namespace OpenSim.ApplicationPlugins.MapDataAdapter
{
    /// <summary>
    /// generate map tiles from scene data
    /// </summary>
    public class MapDataAdapter : IApplicationPlugin
    {
        #region member

        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        // TODO: required by IPlugin, but likely not at all right 
        private string m_name = "MapDataAdapter";
        private string m_version = "0.0";
        protected OpenSimBase m_openSim;
        private BaseHttpServer m_server;
        private IConfigSource m_config;
        private List<MapRegion> m_regions;        
        private int m_texUpdateInterval;
        private int m_mapUpdateInterval;
        private string m_remoteConnectionString;
        private int m_minMapSize;
        private int m_zoomLevels;
        private int m_maxRenderElevation;
        private int m_minTileSize;
        private HashSet<float> m_scales;
        private Dictionary<float, float> m_sections;

        public string Version
        {
            get { return m_version; }
        }
        public string Name
        {
            get { return m_name; }
        }


        #endregion

        public void Initialise()
        {
            //用于处理opensim 没有被正确初始化的情况 
            m_log.Error("[APPPLUGIN]: " + Name + " cannot be default-initialized!");
            throw new PluginNotInitialisedException(Name);
        }
        public void Initialise(OpenSimBase openSim)
        {
            //将opensim的引用记为内部成员变量 
            m_log.Info("[MapDataAdapter]: initialized!");
            m_openSim = openSim;
            m_server = openSim.HttpServer;
            m_regions = new List<MapRegion>();
            m_scales = new HashSet<float>();
            m_sections = new Dictionary<float, float>();
            m_config = m_config = new IniConfigSource("WebMapService.ini");
            try
            {
                IConfig config              = m_config.Configs["Map"];
                m_remoteConnectionString    = config.GetString("RemoteConnectionString");
                m_texUpdateInterval         = config.GetInt("TexUpdateInterval");
                m_mapUpdateInterval         = config.GetInt("MapUpdateInterval");
                m_minMapSize                = config.GetInt("MinMapSize");
                m_zoomLevels                = config.GetInt("ZoomLevels");
                m_maxRenderElevation        = config.GetInt("MaxRenderElevation");
                m_minTileSize               = config.GetInt("MinTileSize");
            }
            catch (Exception e)
            {
                m_log.Error("Read WebMapService.ini failed with " + e.Message);
            }
            ThreadStart GetTextureDataStart = new ThreadStart(GetTextureData);
            Thread GetTextureDataThread = new Thread(GetTextureDataStart);
            GetTextureDataThread.Start();
        }
        public void PostInitialise()
        {
            List<Scene> scenelist = m_openSim.SceneManager.Scenes;
            foreach (Scene scene in scenelist)
            {
                m_regions.Add(new MapRegion(scene));
            }
            
            ThreadStart generateMapCacheStart = new ThreadStart(generateMapCache);
            Thread generateMapCacheThread = new Thread(generateMapCacheStart);
            generateMapCacheThread.Start();
            // register ows handler
            string httpMethod = "GET";
            string path = "/map";
            OWSStreamHandler h = new OWSStreamHandler(httpMethod, path, owsHandler);
            m_server.AddStreamHandler(h);
        }

        public void Dispose()
        {

        }

        public MapDataAdapter()
        {
        }

        private void generateMapCache()
        {
            if (!Directory.Exists("mapCache"))
                Directory.CreateDirectory("mapCache");
            while (true)
            {
                m_log.Debug("[WebMapService]: Start generating map caches");
                foreach (MapRegion region in m_regions)
                {
                    m_log.Debug("[WebMapService]: Start generating cache of region" + region.ID);
                    int size = m_minMapSize;
                    string mapCachePath = "mapCache//" + region.ID + "//";
                    if (!Directory.Exists(mapCachePath))
                        Directory.CreateDirectory(mapCachePath);
                    for (int level = 1; level < m_zoomLevels; level++)
                    {
                        float scale = (float)size / 256;
                        m_scales.Add(scale);
                        string tileCachePath = mapCachePath + scale.ToString();
                        if (!Directory.Exists(tileCachePath))
                            Directory.CreateDirectory(tileCachePath);
                        int tileNum = 1;
                        while (size / tileNum > m_minTileSize)
                            tileNum++;
                        int tileSize = size / tileNum;
                        float section = 256 / tileNum;
                        if (!m_sections.ContainsKey(scale))
                            m_sections.Add(scale, section);
                        Bitmap mapCache = null;
                        try
                        {
                            BBox bbox = new BBox(0, 0, 256, 256);
                            region.initialize("primitive");
                            mapCache = region.generateLayerImage("primitive", bbox, size, size, m_maxRenderElevation);
                        }
                        catch (Exception e)
                        {
                            m_log.ErrorFormat("[WebMapService]: Generate map cache failed with {0} {1}", e.Message, e.StackTrace);
                        }
                        //divide the map into tiles
                        lock (mapCache)
                        {
                            try
                            {
                                for (int i = 0; i < tileNum; i++)
                                    for (int j = 0; j < tileNum; j++)
                                    {
                                        Bitmap tileCache = new Bitmap(tileSize, tileSize);
                                        Graphics gfx = Graphics.FromImage((Image)tileCache);
                                        gfx.InterpolationMode = InterpolationMode.HighQualityBicubic;
                                        Rectangle srcRec = new Rectangle(i * tileSize, size - (j + 1) * tileSize, tileSize, tileSize);
                                        Rectangle destRec = new Rectangle(0, 0, tileSize, tileSize);
                                        gfx.DrawImage(mapCache, destRec, srcRec, GraphicsUnit.Pixel);
                                        gfx.Dispose();
                                        string tileName = tileCachePath + "//" + Utility.IntToLong(i * (int)section, j * (int)section).ToString();
                                        tileCache.Save(tileName);
                                        tileCache.Dispose();
                                        tileCache = null;
                                    }
                            }
                            catch (Exception e)
                            {
                                m_log.ErrorFormat("[WebMapService]: Divide map into tiles failed with {0} {1}", e.Message, e.StackTrace);
                            }
                        }
                        mapCache.Dispose();
                        mapCache = null;
                        size = size * 2;
                    }
                    m_log.DebugFormat("[WebMapService]: Finish rendering region {0}", region.ID);
                }
                m_log.Debug("[WebMapService]: Map Caches generated");
                Thread.Sleep(m_mapUpdateInterval);
            }
        }

        public string owsHandler(string request, string path, string param,
                                      OSHttpRequest httpRequest, OSHttpResponse httpResponse)
        {
            switch (httpRequest.QueryString["SERVICE"])
            {
                case "WMS":
                    if (httpRequest.QueryString["REQUEST"] == "GetMap")
                    {                        
                        //parse query string
                        string[] layers = httpRequest.QueryString["LAYERS"].Split(',');
                        BBox bbox = new BBox(httpRequest.QueryString["BBOX"]);
                        int height = Int32.Parse(httpRequest.QueryString["HEIGHT"]);
                        int width = Int32.Parse(httpRequest.QueryString["WIDTH"]);                        
                        int elevation = Int32.Parse(httpRequest.QueryString["ELEVATION"]);
                        string regionID = httpRequest.QueryString["REGIONID"];

                        //Get tiles included in the BBOX and merge them together
                        List<Bitmap> layerImgs = new List<Bitmap>();
                        foreach (MapRegion region in m_regions)
                        {
                            if (region.ID == regionID)
                            {
                                string queryPath = "mapCache//" + regionID + "//";

                                for (int i = 0, len = layers.Length; i < len; i++)
                                {
                                    if (layers[i] == "terrain")
                                    {
                                        region.initialize("terrain");
                                        layerImgs.Add(region.generateLayerImage("terrain", bbox, width, height, elevation));
                                    }
                                    if (layers[i] == "primitive")
                                    {
                                        float scale = (float)height / bbox.Width;
                                        float tmpScale = scale;
                                        float diff = 10000.0f;
                                        foreach (float s in m_scales)
                                        {
                                            float tmp = Math.Abs(scale - s);
                                            if (diff > tmp)
                                            {
                                                diff = tmp;
                                                tmpScale = s;
                                            }
                                        }
                                        scale = tmpScale;
                                        queryPath = queryPath + scale + "//";
                                        float section = 0.0f;
                                        try
                                        {
                                            section = m_sections[scale];
                                        }
                                        catch (Exception e)
                                        {
                                            m_log.ErrorFormat("[WebMapService]: No such scale in list");
                                        }
                                        float tileSize = scale * section;
                                        Bitmap primLayer = new Bitmap(width, height);
                                        int blockMinX = bbox.MinX / (int)section * (int)section;
                                        int blockMinY = bbox.MinY / (int)section * (int)section;
                                        int blockMaxX = (bbox.MaxX - 1) / (int)section * (int)section;
                                        int blockMaxY = (bbox.MaxY - 1) / (int)section * (int)section;
                                        Graphics gfx = Graphics.FromImage(primLayer);
                                        gfx.InterpolationMode = InterpolationMode.HighQualityBicubic;
                                        for (int y = blockMinY; y <= blockMaxY; y += (int)section)
                                            for (int x = blockMinX; x <= blockMaxX; x += (int)section)
                                            {
                                                string tileName = queryPath + Utility.IntToLong(x, y);
                                                Bitmap tile = null;
                                                try
                                                {
                                                    tile = new Bitmap(tileName);
                                                }
                                                catch (Exception e)
                                                {
                                                    m_log.ErrorFormat("[WebMapService]: Tile file not found with {0} {1}", e.Message, e.StackTrace);
                                                    tile = new Bitmap((int)tileSize, (int)tileSize);
                                                    Graphics g = Graphics.FromImage(tile);
                                                    g.Clear(Color.Blue);
                                                    g.Dispose();
                                                }
                                                //Calculate boundary of tiles and map
                                                float tileLeftX, tileRightX, tileBottomY, tileUpY;
                                                float mapLeftX, mapRightX, mapBottomY, mapUpY;
                                                float difLeftX = (float)bbox.MinX - (float)x;
                                                float difRightX = (float)bbox.MaxX - ((float)x + section);
                                                float difBottomY = (float)bbox.MinY - (float)y;
                                                float difUpY = (float)bbox.MaxY - ((float)y + section);
                                                if (difLeftX > 0) { tileLeftX = difLeftX; mapLeftX = 0; }
                                                else { tileLeftX = 0; mapLeftX = -difLeftX; }
                                                if (difRightX > 0) { tileRightX = section; mapRightX = bbox.Width - difRightX; }
                                                else { tileRightX = section + difRightX; mapRightX = bbox.Width; }
                                                if (difBottomY > 0) { tileBottomY = section - difBottomY; mapBottomY = bbox.Height; }
                                                else { tileBottomY = section; mapBottomY = bbox.Height + difBottomY; }
                                                if (difUpY > 0) { tileUpY = 0; mapUpY = difUpY; }
                                                else { tileUpY = -difUpY; mapUpY = 0; }
                                                try
                                                {
                                                    RectangleF srcRec = new RectangleF(tileLeftX / section * tileSize, tileUpY / section * tileSize, (tileRightX - tileLeftX) / section * tileSize, (tileBottomY - tileUpY) / section * tileSize);
                                                    RectangleF destRec = new RectangleF(mapLeftX / bbox.Width * width, mapUpY / bbox.Height * height, (mapRightX - mapLeftX) / bbox.Width * width, (mapBottomY - mapUpY) / bbox.Height * height);
                                                    gfx.DrawImage(tile, destRec, srcRec, GraphicsUnit.Pixel);
                                                    tile.Dispose();
                                                }
                                                catch (Exception e)
                                                {
                                                    tile.Dispose();
                                                    m_log.ErrorFormat("[WebMapService]: Failed to cut and merge tiles with {0} {1}", e.Message, e.StackTrace);
                                                }
                                            }
                                        gfx.Dispose();
                                        layerImgs.Add(primLayer);
                                    }
                                }
                            }
                        }
                        
                        Bitmap queryImg = null;
                        try
                        {
                            queryImg = overlayImages(width, height, layerImgs);
                        }
                        catch (Exception e)
                        {
                            m_log.ErrorFormat("[WebMapService]: Failed to overlay layers with {0} {1}", e.Message, e.StackTrace);
                        }
                        for (int i = 0, len = layerImgs.Count; i < len; i++)
                        {
                            layerImgs[i].Dispose();
                            layerImgs[i] = null;
                        }
                        System.IO.MemoryStream stream = new System.IO.MemoryStream();
                        queryImg.Save(stream, System.Drawing.Imaging.ImageFormat.Png);
                        queryImg.Dispose();
                        queryImg = null;
                        byte[] byteImage = stream.ToArray();
                        stream.Close();
                        stream.Dispose();
                        httpResponse.ContentType = "image/png";
                        return Convert.ToBase64String(byteImage);                                                               
                    }
                    else if (httpRequest.QueryString["REQUEST"] == "GetCapabilities")
                    {
                        httpResponse.ContentType = "text/xml";
                        string capDes = "";
                        TextReader textReader = new StreamReader("Capability.xml");
                        capDes = textReader.ReadToEnd();
                        textReader.Close();
                        return capDes;
                    }
                    else
                    {
                        return "Sorry, the request method is not supported by this service.";
                    }
            }
            return "Unsupported Service";
        }

        private Bitmap overlayImages(int width, int height, List<Bitmap> layerImgs)
        {
            Bitmap image = new Bitmap(width, height);
            Graphics gfx = Graphics.FromImage(image);
            gfx.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.GammaCorrected;
            gfx.CompositingMode = System.Drawing.Drawing2D.CompositingMode.SourceOver;
            foreach (Bitmap img in layerImgs)
            {
                gfx.DrawImage(img, new Rectangle(0, 0, width, height));
            }
            gfx.Dispose();
            return image;
        }

        private void GetTextureData()
        {
            while (true)
            {
                m_log.Debug("[WebMapService]: Start getting texture from remote database");
                try
                {
                    Utility.ConnectMysql(m_remoteConnectionString);
                    List<TextureColorModel> data = Utility.GetDataFromMysql();
                    Utility.DisconnectMysql();
                    Utility.StoreDataIntoFiles(data);
                }
                catch (Exception e)
                {
                    m_log.ErrorFormat("[WebMapService]: Get texture data failed with {0} {1}", e.Message, e.StackTrace);
                }
                m_log.Debug("[WebMapService]: Successfully got all remote texture data");
                Thread.Sleep(m_texUpdateInterval);
            }
        }

        
    }
}
