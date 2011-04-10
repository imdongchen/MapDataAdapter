using System;
using System.Collections.Generic;
using System.Text;
using OpenSim.Region.Framework.Scenes;
using System.Drawing;
using OpenSim.Region.Framework.Interfaces;
using System.Drawing.Drawing2D;

namespace OpenSim.ApplicationPlugins.MapDataAdapter.Layers
{
    public class TerrainLayer : BaseLayer
    {
        public TerrainLayer(Scene scene)
            : base(scene)
        {
        }

        public override void initialize()
        {
            return;
        }

        public override Bitmap render(BBox bbox, int width, int height, int elevation)
        {
            Bitmap gradientmapLd = new Bitmap("defaultstripe.png");
            int pallete = gradientmapLd.Height;            
            ITerrainChannel heightMap = m_scene.Heightmap;
            Bitmap WholeRegionbmp = new Bitmap((int)bbox.Width, (int)bbox.Height);
            Color[] colours = new Color[pallete];
            for (int i = 0; i < pallete; i++)
            {
                colours[i] = gradientmapLd.GetPixel(0, i);
            }

            for (int y = 0; y < (int)bbox.Height; y++)
            {
                for (int x = 0; x < (int)bbox.Width; x++)
                {
                    // 512 is the largest possible height before colours clamp
                    int colorindex;
                    if (elevation >= heightMap[x, y])
                    {
                        colorindex = (int)(Math.Max(Math.Min(1.0, heightMap[x, y] / 512.0), 0.0) * (pallete - 1));
                    }
                    else
                        colorindex = (int)(Math.Max(Math.Min(1.0, elevation / 512.0), 0.0) * (pallete - 1));

                    // Handle error conditions
                    if (colorindex > pallete - 1 || colorindex < 0)
                        WholeRegionbmp.SetPixel(x, (int)bbox.Height - y - 1, Color.Red);
                    else
                        WholeRegionbmp.SetPixel(x, (int)bbox.Height - y - 1, colours[colorindex]);
                }
            }

            Bitmap RegionBBoxBmp = null;
            try
            {
                RegionBBoxBmp = new Bitmap(width, height);
                using (Graphics g = Graphics.FromImage(RegionBBoxBmp))
                {
                    g.SmoothingMode = SmoothingMode.HighQuality;
                    g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    g.CompositingQuality = CompositingQuality.HighQuality;
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.DrawImage(WholeRegionbmp, new Rectangle(0, 0, width, height));
                }
            }
            catch
            {
                if (RegionBBoxBmp != null) RegionBBoxBmp.Dispose();
                throw;
            }
            return RegionBBoxBmp;
        }
    }
}
