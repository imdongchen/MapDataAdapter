using System;
using System.Collections.Generic;
using System.Text;
using MapRendererCL;
using OpenMetaverse;

namespace OpenSim.ApplicationPlugins.MapDataAdapter
{
    class Conversion
    {
        public static LLVector3CL toLLVector3(Vector3 vector)
        {
            return new LLVector3CL(vector.X, vector.Y, vector.Z);
        }
        public static LLQuaternionCL toLLQuaternion(Quaternion qua)
        {
            return new LLQuaternionCL(qua.X, qua.Y, qua.Z, qua.W);
        }
    }
}
