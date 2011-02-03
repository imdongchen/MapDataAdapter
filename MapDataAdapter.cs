using System;
using System.Collections.Generic;

using System.Text;
using MapRendererCL;
using OpenSim.Region.Framework.Scenes;
using log4net;
using OpenSim.Framework;
using System.Reflection;

namespace OpenSim.ApplicationPlugins.MapDataAdapter
{
    /// <summary>
    /// generate map tiles from scene data
    /// </summary>
    public class MapDataAdapter : IApplicationPlugin
    {
        #region member

        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        #region IApplicationPlugin Members
        // TODO: required by IPlugin, but likely not at all right 
        private string m_name = "MapDataAdapter";
        private string m_version = "0.0";

        public string Version
        {
            get { return m_version; }
        }
        public string Name
        {
            get { return m_name; }
        }

        protected OpenSimBase m_openSim;
        #endregion

        private float _minX;
        private float _minY;
        private float _minZ;
        private float _maxX;
        private float _maxY;
        private float _maxZ;
        private List<LLProfileParamsCL> _profileParams;
        private List<LLPathParamsCL> _pathParams;
        private List<LLVolumeParamsCL> _volumeParams;
        private int _volumeCount;
        private List<LLVector3CL> _positions;
        private List<LLQuaternionCL> _rotations;
        private List<LLVector3CL> _scales;
        private List<TextureEntryListCL> _textureEntryLists;
        private string _cachePath;
        private string _destPath;

        public float minX
        {
            get { return _minX; }
            set { _minX = value; }
        }
        public float minY
        {
            get { return _minY; }
            set { _minY = value; }
        }

        public float minZ
        {
            get { return _minZ; }
            set { _minZ = value; }
        }

        public float maxX
        {
            get { return _maxX; }
            set { _maxX = value; }
        }

        public float maxY
        {
            get { return _maxY; }
            set { _maxY = value; }
        }
        public float maxZ
        {
            get { return _maxZ; }
            set { _maxZ = value; }
        }
        public List<LLProfileParamsCL> profileParams
        {
            get { return _profileParams; }
            set { _profileParams = value; }
        }
        public List<LLPathParamsCL> pathParams
        {
            get { return _pathParams; }
            set { _pathParams = value; }
        }
        public List<LLVolumeParamsCL> volumeParams
        {
            get { return _volumeParams; }
            set { _volumeParams = value; }
        }
        public int volumeCount
        {
            get { return _volumeCount; }
            set { _volumeCount = value; }
        }
        public List<LLVector3CL> positions
        {
            get { return _positions; }
            set { _positions = value; }
        }
        public List<LLQuaternionCL> rotations
        {
            get { return _rotations; }
            set { _rotations = value; }
        }
        public List<LLVector3CL> scales
        {
            get { return _scales; }
            set { _scales = value; }
        }
        public List<TextureEntryListCL> textureEntryLists
        {
            get { return _textureEntryLists; }
            set { _textureEntryLists = value; }
        }
        public string cachePath
        {
            get { return _cachePath; }
            set { _cachePath = value; }
        }
        public string destPath
        {
            get { return _destPath; }
            set { _destPath = value; }
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
            _pathParams = new List<LLPathParamsCL>();
            _profileParams = new List<LLProfileParamsCL>();
            _positions = new List<LLVector3CL>();
            _rotations = new List<LLQuaternionCL>();
            _scales = new List<LLVector3CL>();
            _textureEntryLists = new List<TextureEntryListCL>();
            _volumeParams = new List<LLVolumeParamsCL>();
        }
        public void PostInitialise()
        {
            //在这儿干正事 
            getData(128, 128, 0, 132, 132, 30, "e:\\\\", "e:\\\\"); //hard coded, how to pass params?
            adaptData();
        }
        public void Dispose()
        {

        }

        public MapDataAdapter()
        {
        }
        //public MapDataAdapter(int primNumber)
        //{
        //    _profileParams = new LLProfileParamsCL[primNumber];
        //    _pathParams = new LLPathParamsCL[primNumber];
        //    _volumeParams = new LLVolumeParamsCL[primNumber];
        //    _positions = new LLVector3CL[primNumber];
        //    _rotations = new LLQuaternionCL[primNumber];
        //    _scales = new LLVector3CL[primNumber];
        //    _textureEntryLists = new TextureEntryListCL[primNumber];
        //    volumeCount = primNumber;
        //}

        public bool getData(float minX, float minY, float minZ, float maxX, float maxY, float maxZ, string cachePath, string destPath)
        {
            this.cachePath = cachePath;
            this.destPath = destPath;
            this.minX = minX;
            this.minY = minY;
            this.minZ = minZ;
            this.maxX = maxX;
            this.maxY = maxY;
            this.maxZ = maxZ;
            List<Scene> scenelist = m_openSim.SceneManager.Scenes;
            int i = 0;
            lock (scenelist)
            {
                foreach (Scene scene in scenelist)
                {
                    List<EntityBase> objs = scene.GetEntities();
                    lock( objs )
                    {
                        foreach (EntityBase obj in objs)
                        {
                            if (obj is SceneObjectGroup)
                            {
                                SceneObjectGroup sog = (SceneObjectGroup)obj;
                                foreach (SceneObjectPart part in sog.Children.Values)
                                {
                                    if (part == null)
                                        continue;
                                    _positions.Add(Conversion.toLLVector3(part.GroupPosition));
                                    _scales.Add(Conversion.toLLVector3(part.Scale));
                                    _rotations.Add(Conversion.toLLQuaternion(part.RotationOffset));
                                    PrimitiveBaseShape shape = part.Shape;
                                    _pathParams.Add(new LLPathParamsCL(shape.PathCurve,
                                        shape.PathBegin, shape.PathEnd,
                                        shape.PathScaleX, shape.PathScaleY, 
                                        shape.PathShearX, shape.PathShearY,
                                        Convert.ToByte(shape.PathTwist), Convert.ToByte(shape.PathTwistBegin),
                                        Convert.ToByte(shape.PathRadiusOffset), 
                                        Convert.ToByte(shape.PathTaperX), Convert.ToByte(shape.PathTaperY),
                                        shape.PathRevolutions, Convert.ToByte(shape.PathSkew)));
                                    _profileParams.Add(new LLProfileParamsCL(shape.ProfileCurve,
                                        shape.ProfileBegin, shape.ProfileEnd, shape.ProfileHollow));
                                    _volumeParams.Add(new LLVolumeParamsCL(_profileParams[i], _pathParams[i]));
                                    i++;
                                }
                            }
                        }
                    }
                }
            }
            return true;
        }

        public bool adaptData()
        {
            volumeCount = volumeParams.Count;   
            LLVector3CL[] pos = new LLVector3CL[volumeCount];
            LLVector3CL[] sca = new LLVector3CL[volumeCount];
            LLQuaternionCL[] rot = new LLQuaternionCL[volumeCount];
            LLVolumeParamsCL[] vop = new LLVolumeParamsCL[volumeCount];
            TextureEntryListCL[] tel = new TextureEntryListCL[volumeCount];
            for (int i = 0; i < volumeCount; i++)
            {
                pos[i] = positions[i];
                sca[i] = scales[i];
                rot[i] = rotations[i];
                vop[i] = volumeParams[i];
                //tel[i] = textureEntryLists[i];
            }
            MapRenderCL mr = new MapRenderCL();
            return mr.mapRender(minX, minY, minZ, maxX, maxY, maxZ, vop, volumeCount, pos, rot, sca, tel, cachePath, destPath);
        }



        //public static void  Main()
        //{

        //    OpenSimDataSetTableAdapters.primshapesTableAdapter pshapeta = new mapDataAdapter.OpenSimDataSetTableAdapters.primshapesTableAdapter();
        //    OpenSimDataSet.primshapesDataTable ds = new OpenSimDataSet.primshapesDataTable();
        //    pshapeta.Fill(ds);
        //    //foreach (DataRow row in ds[2].ProfileHollow)
        //    //{
        //    //    Console.WriteLine("{0}", row[0]);
        //    //}
        //    Console.WriteLine("{0}", ds[2].ProfileHollow);
        //    Console.ReadLine();

        //    //MapDataAdapter mda = new MapDataAdapter();
        //    //mda.adaptData(125,	//minX
        //    //    125,  //minY
        //    //    0, //minZ
        //    //    132.5f,	//maxX
        //    //    132.5f,	//maxY
        //    //    30,	//maxZ
        //    //    0,    //volumeParams
        //    //    0,	//volume个数
        //    //    0,	//region position
        //    //    0,
        //    //    0,
        //    //    0,
        //    //    "e:\\\\",		//纹理虚拟目录
        //    //    "e:\\\\");
        //}
    }

        
}
