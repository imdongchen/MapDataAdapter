using System;
using System.Collections.Generic;

using System.Text;
using OpenSim.Region.Framework.Scenes;
using System.Drawing;

namespace OpenSim.ApplicationPlugins.MapDataAdapter.Layers
{
    public abstract class BaseLayer : IDisposable
    {
        protected Scene m_scene;

        public BaseLayer(Scene scene)
        {
            m_scene = scene;
        }

        /// <summary>
        /// get necessary data to generate the projection map
        /// </summary>
        public abstract void initialize();

        /// <summary>
        /// generate the projection Bitmap of a layer
        /// </summary>
        /// <param name="bbox">the boundary of the layer to be drawn</param>
        /// <param name="height">height of the output bitmap</param>
        /// <param name="weight">weight of the output bitmap</param>
        public abstract Bitmap render(BBox bbox, int height, int weight, int elevation);

        #region IDisposable Members

        public void Dispose()
        {
            throw new NotImplementedException();
        }

        #endregion
    }
}
