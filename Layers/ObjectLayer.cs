using System;
using System.Collections.Generic;
using System.Text;
using OpenSim.Region.Framework.Scenes;
using System.Drawing;
using OpenSim.Framework;
using MapRendererCL;

namespace OpenSim.ApplicationPlugins.MapDataAdapter.Layers
{
    public class ObjectLayer : BaseLayer
    {
        #region members
        private List<LLProfileParamsCL> m_profileParams;
        private List<LLPathParamsCL> m_pathParams;
        private List<LLVolumeParamsCL> m_volumeParams;
        private int m_volumeCount;
        private List<LLVector3CL> m_positions;
        private List<LLQuaternionCL> m_rotations;
        private List<LLVector3CL> m_scales;
        private List<TextureEntryListCL> m_textureEntryLists;       
        #endregion
       
        public ObjectLayer(Scene scene)
            : base(scene)
        {
            m_pathParams = new List<LLPathParamsCL>();
            m_profileParams = new List<LLProfileParamsCL>();
            m_positions = new List<LLVector3CL>();
            m_rotations = new List<LLQuaternionCL>();
            m_scales = new List<LLVector3CL>();
            m_textureEntryLists = new List<TextureEntryListCL>();
            m_volumeParams = new List<LLVolumeParamsCL>();
        }

        public override void initialize()
        { 
            List<EntityBase> objs = m_scene.GetEntities();
            int count = 0;
            int primNum = 0;
            lock (objs)
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
                            m_positions.Add(Conversion.toLLVector3(part.GroupPosition));
                            m_scales.Add(Conversion.toLLVector3(part.Scale));
                            m_rotations.Add(Conversion.toLLQuaternion(part.RotationOffset));

                            PrimitiveBaseShape shape = part.Shape;
                            m_pathParams.Add(new LLPathParamsCL(shape.PathCurve,
                                shape.PathBegin, shape.PathEnd,
                                shape.PathScaleX, shape.PathScaleY,
                                shape.PathShearX, shape.PathShearY,
                                Convert.ToByte(shape.PathTwist), Convert.ToByte(shape.PathTwistBegin),
                                Convert.ToByte(shape.PathRadiusOffset),
                                Convert.ToByte(shape.PathTaperX), Convert.ToByte(shape.PathTaperY),
                                shape.PathRevolutions, Convert.ToByte(shape.PathSkew)));
                            m_profileParams.Add(new LLProfileParamsCL(shape.ProfileCurve,
                                shape.ProfileBegin, shape.ProfileEnd, shape.ProfileHollow));
                            m_volumeParams.Add(new LLVolumeParamsCL(m_profileParams[count], m_pathParams[count]));
                            count++;
                            //get jpg texture files
                            //generateJPGFiles(m_assetService, shape);

                            //set texture information
                            int facenum = part.GetNumberOfSides();
                            primNum += facenum;
                            TextureEntryListCL theTextureEntryListCL = new TextureEntryListCL(facenum);
                            for (uint j = 0; j < facenum; j++)
                            {
                                theTextureEntryListCL.SetTextureEntry(
                                    j,
                                    new LLUUIDCL(shape.Textures.GetFace(j).TextureID.ToString()),
                                    new LLColor4CL(shape.Textures.GetFace(j).RGBA.R, shape.Textures.GetFace(j).RGBA.G, shape.Textures.GetFace(j).RGBA.B, shape.Textures.GetFace(j).RGBA.A),
                                    Convert.ToByte(shape.Textures.GetFace(j).MediaFlags),
                                    shape.Textures.GetFace(j).Glow,
                                    shape.Textures.GetFace(j).RepeatU,
                                    shape.Textures.GetFace(j).RepeatV,
                                    shape.Textures.GetFace(j).OffsetU,
                                    shape.Textures.GetFace(j).OffsetV,
                                    shape.Textures.GetFace(j).Rotation
                                    );
                            }
                            m_textureEntryLists.Add(theTextureEntryListCL);
                        }
                    }
                }
            }
            m_volumeCount = count;
        }

        public override Bitmap render(BBox bbox, int width, int height, int elevation)
        {
            LLVector3CL[] pos = new LLVector3CL[m_volumeCount];
            LLVector3CL[] sca = new LLVector3CL[m_volumeCount];
            LLQuaternionCL[] rot = new LLQuaternionCL[m_volumeCount];
            LLVolumeParamsCL[] vop = new LLVolumeParamsCL[m_volumeCount];
            TextureEntryListCL[] tel = new TextureEntryListCL[m_volumeCount];
            for (int i = 0; i < m_volumeCount; i++)
            {
                pos[i] = m_positions[i];
                sca[i] = m_scales[i];
                rot[i] = m_rotations[i];
                vop[i] = m_volumeParams[i];
                tel[i] = m_textureEntryLists[i];
            }
            MapRenderCL mr = new MapRenderCL();
            string regionID = m_scene.RegionInfo.RegionID.ToString();

            
            mr.mapRender(
                regionID,
                bbox.MinX, bbox.MinY, 0, bbox.MaxX, bbox.MaxY, (float)elevation,
                vop,
                m_volumeCount,
                pos,
                rot,
                sca,
                tel,
                width,
                height,
                "e:\\\\MonoImage\\\\",
                "e:\\\\regionMap\\\\");
            Bitmap bmp = new Bitmap("e:\\\\regionMap\\\\" + regionID + ".bmp");
            bmp.MakeTransparent(Color.FromArgb(0, 0, 0, 0));
            return bmp;
        }
    }
}
