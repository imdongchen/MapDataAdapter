using System;
using System.Collections.Generic;

using System.Text;
using OpenSim.Region.Framework.Scenes;
using System.Drawing;

namespace OpenSim.ApplicationPlugins.MapDataAdapter.Layers
{
    public class AgentLayer : BaseLayer
    {
        public AgentLayer(Scene scene) 
            : base(scene)
        {    
        }

        public override void initialize()
        {
            return;
        }

        public override Bitmap render(BBox bbox, int width, int height, int elevation)
        {
            Bitmap mapImg = new Bitmap(width, height);
            Graphics gfx = Graphics.FromImage(mapImg);

            gfx.Clear(Color.FromArgb(0, 0, 0, 0));
            
            Pen pen = new Pen(Color.Red);
            Brush brush = Brushes.Red;

            // draw agent position on the map
            try
            {
                m_scene.ForEachScenePresence(delegate(ScenePresence agent)
                {
                    if (!agent.IsChildAgent)
                    {
                        PointF agentPos = new PointF(agent.OffsetPosition.X, agent.OffsetPosition.Y);
                        PointF agentImgPos = Conversion.Projection(ref agentPos, ref bbox, width, height);
                        RectangleF rect = new RectangleF(agentImgPos.X, agentImgPos.Y, 20, 20); //point width and height hard coded as 20, should be changed
                        gfx.FillEllipse(brush, rect);
                    }
                }
                );
            }
            catch (Exception)
            {
                throw new Exception("agent layer rendering failed");
            }
            gfx.Dispose();
            pen.Dispose();
            return mapImg;
        }
       
    }
}
