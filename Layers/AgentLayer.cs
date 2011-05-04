using System;
using System.Collections.Generic;

using System.Text;
using OpenSim.Region.Framework.Scenes;
using System.Drawing;
using System.IO;
using System.Xml;

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
                        PointF agentImgPos = Utility.Projection(ref agentPos, ref bbox, width, height);
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


        internal string GetFeaturesByText()
        {
            string res = "";
            try
            {
                m_scene.ForEachScenePresence(delegate(ScenePresence agent)
                {
                    if (!agent.IsChildAgent)
                    {
                        res += agent.Name + "," + agent.OffsetPosition.X + "," + agent.OffsetPosition.Y + "," + agent.OffsetPosition.Z + "\n";
                    }
                }
                );
            }
            catch (Exception)
            {
                throw new Exception("agent layer rendering failed");
            }
            
            return res;
        }

        internal string GetFeaturesByXml()
        {
            Stream st = new MemoryStream();
            XmlTextWriter featureWriter = new XmlTextWriter(st, Encoding.UTF8);
            featureWriter.Formatting = Formatting.Indented;
            featureWriter.WriteStartDocument();
            // start write element FeatureCollection
            featureWriter.WriteStartElement("wfs", "FeatureCollection");
            featureWriter.WriteAttributeString("xmlns", "wfs", null, "http://www.opengis.net/wfs");
            featureWriter.WriteAttributeString("xmlns", "gml", null, "http://www.opengis.net/gml");
            featureWriter.WriteAttributeString("xmlns", "xsi", null, "http://www.w3.org/2001/XMLSchema-instance");
            featureWriter.WriteAttributeString("xsi", "schemaLocation", null, "http://www.opengis.net/wfs ../wfs/1.1.0/WFS.xsd");

            //start write element gml:boundedBy
            featureWriter.WriteStartElement("gml", "boundedBy");
            featureWriter.WriteStartElement("gml", "Envelope");
            featureWriter.WriteAttributeString("srs", "http://www.opengis.net/gml/srs/epsg.xml#63266405");
            string lowerCorner = "";
            string upperCorner = "";
            featureWriter.WriteElementString("gml", "lowerCorner", lowerCorner);
            featureWriter.WriteElementString("gml", "upperCorner", upperCorner);
            featureWriter.WriteEndElement();//end of element gml:Envelope
            featureWriter.WriteEndElement(); //end of element gml:boundedBy

            m_scene.ForEachScenePresence(delegate(ScenePresence agent)
            {
                featureWriter.WriteStartElement("gml", "featureMember");
                /*
                 * <gml:Point gml:id="p21" srsName="urn:ogc:def:crs:EPSG:6.6:4326">
                 * <gml:coordinates>45.67, 88.56</gml:coordinates>
                 * </gml:Point>         
                */
                featureWriter.WriteElementString("name", agent.Name);
                featureWriter.WriteStartElement("gml", "Point");
                string posString = agent.OffsetPosition.X + "," + agent.OffsetPosition.Y + "," + agent.OffsetPosition.Z;
                featureWriter.WriteElementString("gml", "coordinates", null, posString);
                featureWriter.WriteEndElement();
                featureWriter.WriteEndElement();
            }
            );
            featureWriter.WriteEndElement();// end write element FeatureCollection            
            featureWriter.WriteEndDocument();
            featureWriter.Flush();

            byte[] buffer = new byte[st.Length];
            st.Seek(0, SeekOrigin.Begin);
            st.Read(buffer, 0, (int)st.Length);
            featureWriter.Close();

            return Encoding.UTF8.GetString(buffer);
        }
    }
}
