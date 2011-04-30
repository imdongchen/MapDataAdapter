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
    public class MapRegion 
    {
        private UUID m_id;
        private ulong m_handle;
        private uint m_locX;
        private uint m_locY;
        private AgentLayer m_agentLyr;
        private PrimitiveLayer m_primLyr;
        private TerrainLayer m_terrainLyr;
        private Scene m_scene;
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

        public void initialize(string layer)
        {
            switch (layer)
            {
                case "agent":
                    m_agentLyr = new AgentLayer(m_scene);
                    m_agentLyr.initialize();
                    break;
                case "primitive":
                    m_primLyr = new PrimitiveLayer(m_scene);
                    m_primLyr.initialize();
                    break;
                case "terrain":
                    m_terrainLyr = new TerrainLayer(m_scene);
                    m_terrainLyr.initialize();
                    break;
                default:
                    break;
            }
        }

        public Bitmap generateLayerImage(string layer, BBox bbox, int width, int height, int elevation)
        {
            Bitmap layerImg = null;
            try
            {
                switch (layer)
                {
                    case "agent":
                        layerImg = m_agentLyr.render(bbox, width, height, elevation);
                        break;
                    case "primitive":
                        layerImg = m_primLyr.render(bbox, width, height, elevation);
                        break;
                    case "terrain":
                        layerImg = m_terrainLyr.render(bbox, width, height, elevation);
                        break;
                    default:
                        break;
                }
            }
            catch (Exception e)
            {
                m_log.ErrorFormat("[WebMapService]: Generate layer image failed with {0} {1}", e.Message, e.StackTrace);
            }
            return layerImg;
        }
    }
}
