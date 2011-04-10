using System;
using System.Collections.Generic;
using System.Text;
using MapRendererCL;
using OpenMetaverse;
using System.Drawing;
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

        /// <summary>
        /// project inworld coordinates to image coordinates
        /// </summary>
        /// <param name="agentPos">inworld coordinate of avatar in a region</param>
        /// <param name="bbox">requested region bounding box</param>
        /// <param name="width">image width</param>
        /// <param name="height">image height</param>
        /// <returns></returns>
        internal static PointF Projection(ref PointF agentPos, ref BBox bbox, int width, int height)
        {
            PointF result = new PointF();        
            result.X = (agentPos.X - bbox.MinX) * width / bbox.Width;
            result.Y = height - (agentPos.Y - bbox.MinY) * height / bbox.Height;
            return result;
        }
    }
}
