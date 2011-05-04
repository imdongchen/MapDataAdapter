﻿using System;
using System.Collections.Generic;
using System.Text;
using OpenSim.Region.Framework.Scenes;
using System.Drawing;
using OpenSim.Framework;
using MapRendererCL;
using OpenSim.Services.Interfaces;
using FreeImageAPI;
using System.IO;
using Nini.Config;
using System.Threading;
using log4net;
using System.Reflection;
using System.Data.SQLite;

namespace OpenSim.ApplicationPlugins.MapDataAdapter.Layers
{
    public class ObjectLayer : BaseLayer
    {
        #region members
        private IAssetService m_assetService;
        private List<PrimitiveCL> m_primitiveList;
        private IConfigSource m_config;
        private string MapPath;
        private string LocalConnectionString;
        private static readonly log4net.ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        #endregion

        public ObjectLayer(Scene scene)
            : base(scene)
        {
            m_primitiveList = new List<PrimitiveCL>();
            try
            {
                m_config = new IniConfigSource("WebMapService.ini");
                IConfig config = m_config.Configs["ObjectLayer"];
                MapPath = config.GetString("MapPath");
            }
            catch (Exception e)
            {
                throw new Exception("Read WebMapService.ini failed");
            }
        }

        public override void initialize()
        {
            List<EntityBase> objs = m_scene.GetEntities();

            lock (objs)
            {
                try
                {
                    foreach (EntityBase obj in objs)
                    {
                        if (obj is SceneObjectGroup)
                        {
                            SceneObjectGroup mapdot = (SceneObjectGroup)obj;
                            foreach (SceneObjectPart part in mapdot.Children.Values)
                            {
                                if (part == null)
                                    continue;
                                LLVector3CL position = Utility.toLLVector3(part.GroupPosition);
                                LLQuaternionCL rotation = Utility.toLLQuaternion(part.RotationOffset);
                                LLVector3CL scale = Utility.toLLVector3(part.Scale);
                                PrimitiveBaseShape shape = part.Shape;
                                LLPathParamsCL pathParams = new LLPathParamsCL(shape.PathCurve,
                                    shape.PathBegin, shape.PathEnd,
                                    shape.PathScaleX, shape.PathScaleY,
                                    shape.PathShearX, shape.PathShearY,
                                    shape.PathTwist, shape.PathTwistBegin,
                                    shape.PathRadiusOffset,
                                    shape.PathTaperX, shape.PathTaperY,
                                    shape.PathRevolutions, shape.PathSkew);
                                LLProfileParamsCL profileParams = new LLProfileParamsCL(shape.ProfileCurve,
                                    shape.ProfileBegin, shape.ProfileEnd, shape.ProfileHollow);
                                LLVolumeParamsCL volumeParams = new LLVolumeParamsCL(profileParams, pathParams);

                                int facenum = part.GetNumberOfSides();
                                List<SimpleColorCL> colors = new List<SimpleColorCL>();
                                for (uint j = 0; j < facenum; j++)
                                {
                                    TextureColorModel data = Utility.GetDataFromFile(shape.Textures.GetFace(j).TextureID.ToString());
                                    colors.Add(new SimpleColorCL(data.A, data.R, data.G, data.B));
                                }

                                m_primitiveList.Add(new PrimitiveCL(volumeParams, position, rotation, scale, colors.ToArray(), facenum));
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    m_log.ErrorFormat("[WebMapService]: Initialize object layer failed with {0} {1}", e.Message, e.StackTrace);
                }
            }
        }

        public override Bitmap render(BBox bbox, int width, int height, int elevation)
        {
            MapRenderCL mr = new MapRenderCL();
            string regionID = m_scene.RegionInfo.RegionID.ToString();
            try
            {
                bool result = mr.mapRender(regionID,
                    bbox.MinX, bbox.MinY, 0, bbox.MaxX, bbox.MaxY, (float)elevation,
                    m_primitiveList.ToArray(), m_primitiveList.Count,
                    width, height,
                    MapPath);

                if (result)
                {
                    Bitmap bmp = new Bitmap(MapPath + regionID + ".bmp");
                    bmp.MakeTransparent(Color.FromArgb(0, 0, 0, 0));
                    return bmp;
                }
                else
                {
                    Bitmap bmp = new Bitmap(width, height);
                    return bmp;
                }
            }
            catch (Exception e)
            {
                m_log.Error(e.Message + e.StackTrace);
                Bitmap bmp = new Bitmap(width, height);
                return bmp;
            }
        }
    }
}
