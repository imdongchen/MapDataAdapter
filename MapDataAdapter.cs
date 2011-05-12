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
        private string m_localConnectionString;
        private int m_minMapSize;
        private int m_zoomLevel;
        private int m_maxRenderElevation;
        private int m_minTileSize;
        private HashSet<float> m_scales;
        private Dictionary<float, int> m_sections;

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
            m_sections = new Dictionary<float, int>();
            m_config = m_config = new IniConfigSource("WebMapService.ini");
            try
            {
                IConfig config              = m_config.Configs["Map"];
                m_remoteConnectionString    = config.GetString("RemoteConnectionString");
                m_texUpdateInterval         = config.GetInt("TexUpdateInterval");
                m_mapUpdateInterval         = config.GetInt("MapUpdateInterval");
                m_minMapSize                = config.GetInt("MinMapSize");
                m_zoomLevel                = config.GetInt("ZoomLevels");
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
            string[] layers = { "primitive", "terrain" };
            if (!Directory.Exists("mapCache"))
                Directory.CreateDirectory("mapCache");
            while (true)
            {
                m_log.Debug("[WebMapService]: Start generating map caches");
                int size = m_minMapSize;
                for (int level = 0; level < m_zoomLevel; level++)
                {
                    float scale = (float)size / 256;
                    m_scales.Add(scale);
                    string mapCachePath = "mapCache//" + scale.ToString();
                    if (!Directory.Exists(mapCachePath))
                        Directory.CreateDirectory(mapCachePath);
                    int tileNum = 1;
                    while (size / tileNum > m_minTileSize)
                        tileNum++;
                    int tileSize = size / tileNum;
                    int section = 256 / tileNum;
                    if (!m_sections.ContainsKey(scale))
                        m_sections.Add(scale, section);
                    foreach (MapRegion region in m_regions)
                    {
                        string regionCachePath = mapCachePath + "//" + region.ID;
                        if (!Directory.Exists(regionCachePath))
                            Directory.CreateDirectory(regionCachePath);
                        m_log.DebugFormat("[WebMapService]: Start rendering region {0}", region.ID);
                        //generate a whole map image 
                        Bitmap mapCache = null;
                        try
                        {
                            region.Elevation = m_maxRenderElevation;
                            region.MapRegionBBox = new BBox(0, 0, 256, 256);
                            region.MapRegionImg = new MapRegionImage(size, size);
                            region.initialize(layers);
                            mapCache = region.generateMapRegionImg();
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
                                        string tileName = regionCachePath + "//" + Utility.IntToLong(i * section, j * section).ToString();
                                        tileCache.Save(tileName);
                                        tileCache.Dispose();
                                    }
                            }
                            catch (Exception e)
                            {
                                m_log.ErrorFormat("[WebMapService]: Divide map into tiles failed with {0} {1}", e.Message, e.StackTrace);
                            }
                        }
                        m_log.DebugFormat("[WebMapService]: Finish rendering region {0}", region.ID);
                    }
                    size *= 2;
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

                        Bitmap objLayer = new Bitmap(width, height);
                        float scale = (float)height / bbox.Width;
                        float tmpScale = scale;
                        string queryPath = "mapCache//";
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
                        queryPath += scale.ToString() + "//" + regionID + "//";
                        int section = 0;
                        try
                        {
                            section = m_sections[scale];
                        }
                        catch (Exception e)
                        {
                            m_log.ErrorFormat("[WebMapService]: No such scale in list");
                        }
                        float tileSize = scale * section;

                        //Get tiles included in the BBOX and merge them together
                        int blockMinX = bbox.MinX / section * section;
                        int blockMinY = bbox.MinY / section * section;
                        int blockMaxX = (bbox.MaxX - 1) / section * section;
                        int blockMaxY = (bbox.MaxY - 1) / section * section;
                        Graphics gfx = Graphics.FromImage(objLayer);
                        gfx.InterpolationMode = InterpolationMode.HighQualityBicubic;
                        for (int y = blockMinY; y <= blockMaxY; y += section)
                            for (int x = blockMinX; x <= blockMaxX; x += section)
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
                                int tileLeftX, tileRightX, tileBottomY, tileUpY;
                                int mapLeftX, mapRightX, mapBottomY, mapUpY;
                                int difLeftX = bbox.MinX - x;
                                int difRightX = bbox.MaxX - (x+section);
                                int difBottomY = bbox.MinY - y;
                                int difUpY = bbox.MaxY - (y+section);
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
                                    RectangleF srcRec = new RectangleF((float)tileLeftX / section * tileSize, (float)tileUpY / section * tileSize, (float)(tileRightX - tileLeftX) / section * tileSize, (float)(tileBottomY - tileUpY) / section * tileSize);
                                    RectangleF destRec = new RectangleF((float)mapLeftX / bbox.Width * width, (float)mapUpY / bbox.Height * height, (float)(mapRightX - mapLeftX) / bbox.Width * width, (float)(mapBottomY - mapUpY) / bbox.Height * height);
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
                     
                        System.IO.MemoryStream stream = new System.IO.MemoryStream();
                        objLayer.Save(stream, System.Drawing.Imaging.ImageFormat.Png);
                        objLayer.Dispose();
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
                case "WFS":
                    if (httpRequest.QueryString["REQUEST"] == "DescribeFeatureType")
                    {
                        if ((httpRequest.QueryString["TYPENAME"] == "agent"))
                        {
                            switch (httpRequest.QueryString["FORMAT"])
                            {
                                case "text":
                                    string textResult = null;
                                    foreach (MapRegion region in m_regions)
                                    {
                                        textResult += region.GetFeaturesByText();
                                    }
                                    return textResult;
                                case "xml":
                                    string xmlResult = null;
                                    foreach (MapRegion region in m_regions)
                                    {
                                        xmlResult += region.GetFeaturesByXml();
                                    }
                                    return xmlResult;
                                default:
                                    return "Feature format not supported";
                            }
                        }
                        else
                            return "Query String is not supported";
                    }
                    break;
            }

            return "Unsupported Service";
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
