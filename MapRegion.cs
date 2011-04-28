using System;
using System.Collections.Generic;
using System.Text;
using OpenSim.Framework;
using OpenMetaverse;
using OpenSim.Region.Framework.Scenes;
using OpenSim.ApplicationPlugins.MapDataAdapter.Layers;
using System.Drawing;
using Nini.Config;
using log4net;
using System.Reflection;

namespace OpenSim.ApplicationPlugins.MapDataAdapter
{
    public class MapRegionImage
    {
        public Bitmap MapRegionBmp;
        public int Width;
        public int Height;
        public int X;
        public int Y;
        public MapRegionImage() { }
        public MapRegionImage(int width, int height)
        {
            Width = width;
            Height = height;
            MapRegionBmp = new Bitmap(width, height);
        }
    }

    public class MapRegion 
    {
        private UUID m_id;
        private ulong m_handle;
        private uint m_locX;
        private uint m_locY;
        private AgentLayer m_agentLyr;
        private ObjectLayer m_objLyr;
        private TerrainLayer m_terrainLyr;
        private Scene m_scene;
        private bool m_hasAgentLyr = false;
        private bool m_hasObjLyr = false;
        private bool m_hasTerrainLyr = false;
        public MapRegionImage MapRegionImg;
        public BBox MapRegionBBox;
        public int Elevation;
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        public uint LocX
        {
            get { return m_locX; }
            set
            {
                if (m_handle == 0)
                    m_locX = value;
                else
                    throw new Exception("can't set value to an inworld region!");
            }
        }
        public uint LocY
        {
            get { return m_locY; }
            set
            {
                if (m_handle == 0)
                    m_locY = value;
                else
                    throw new Exception("can't set value to an inworld region!");
            }

        }
        public string ID
        {
            get { return m_id.ToString(); }
        }
        public ulong Handle
        {
            get { return m_handle; }
        }
        public MapRegion(Scene scene)
        {
            m_scene = scene;
            m_id = scene.RegionInfo.RegionID;
            m_handle = scene.RegionInfo.RegionHandle;
            m_locX = scene.RegionInfo.RegionLocX;
            m_locY = scene.RegionInfo.RegionLocY;
        }

        /// <summary>
        /// get necessary data for layers in a region
        /// </summary>
        /// <param name="layers">requested layers, namely agent, primitive and terrain</param>
        public void initialize(string[] layers)
        {
            m_hasAgentLyr = false;
            m_hasObjLyr = false;
            m_hasTerrainLyr = false;
            for (int i = 0; i < layers.Length; i++)
            {
                switch (layers[i])
                {
                    case "agent":
                        m_agentLyr = new AgentLayer(m_scene);
                        m_agentLyr.initialize();
                        m_hasAgentLyr = true;
                        break;
                    case "primitive":                       
                        m_objLyr = new ObjectLayer(m_scene);
                        m_objLyr.initialize();
                        m_log.Debug("[WebMapService]: Primitive Layer initialized");
                        m_hasObjLyr = true;
                        break;
                    case "terrain":
                        m_terrainLyr = new TerrainLayer(m_scene);
                        m_terrainLyr.initialize();
                        m_hasTerrainLyr = true;
                        break;
                    default:
                        break;
                }
            }
        }

        /// <summary>
        /// generate the bitmap of a region
        /// </summary>
        /// <param name="bbox">requested rendering boundary</param>
        /// <param name="height">height of the bitmap</param>
        /// <param name="width">width of the bitmap</param>
        /// <returns></returns>
        public Bitmap generateMapRegionImg()
        {
            Bitmap mapRegionImg = new Bitmap(MapRegionImg.Width, MapRegionImg.Height);

            try
            {
                int width = MapRegionImg.Width;
                int height = MapRegionImg.Height;

                Graphics gf = Graphics.FromImage((Image)mapRegionImg);

                List<Bitmap> layerBatches = new List<Bitmap>();

                if (m_hasTerrainLyr)
                    layerBatches.Add(m_terrainLyr.render(MapRegionBBox, width, height, Elevation));
                if (m_hasObjLyr)
                    layerBatches.Add(m_objLyr.render(MapRegionBBox, width, height, Elevation));
                if (m_hasAgentLyr)
                    layerBatches.Add(m_agentLyr.render(MapRegionBBox, width, height, Elevation));

                for (int i = 0; i < layerBatches.Count; i++)
                {
                    gf.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.GammaCorrected;
                    //determine the overwrite mode
                    gf.CompositingMode = System.Drawing.Drawing2D.CompositingMode.SourceOver;
                    gf.DrawImage(layerBatches[i], new Rectangle(0, 0, width, height));
                    layerBatches[i].Dispose();
                }
                gf.Dispose();
            }
            catch (Exception e)
            {
                throw new Exception("generate map region image failed, " + e.Message);
            }
            
            MapRegionImg.MapRegionBmp = mapRegionImg;
            return mapRegionImg;            
        }
    }
}
