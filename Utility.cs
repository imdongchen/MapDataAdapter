using System;
using System.Collections.Generic;
using System.Text;
using MapRendererCL;
using OpenMetaverse;
using System.Drawing;
using System.Data.SQLite;
using MySql.Data.MySqlClient;
using FreeImageAPI;
using System.IO;

namespace OpenSim.ApplicationPlugins.MapDataAdapter
{
    class Utility
    {
        private static SQLiteConnection m_sqliteConnect;
        private static MySqlConnection m_mysqlConnect;

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

        public static long IntToLong(int a, int b)
        {
            return ((long)a << 32 | (long)b);
        }

        public static void LongToInt(long a, out int b, out int c)
        {
            b = (int)(a >> 32);
            c = (int)(a & 0x00000000FFFFFFFF);
        }

        public static void StoreDataIntoFiles(List<TextureColorModel> data)
        {
            if (!Directory.Exists("textureColorCache"))
                Directory.CreateDirectory("textureColorCache");
            foreach (TextureColorModel model in data)
            {
                string file = "textureColorCache//" + model.ID;
                if (File.Exists(file))
                    return;
                TextWriter tw = new StreamWriter(file, false);
                tw.WriteLine(model.A);
                tw.WriteLine(model.R);
                tw.WriteLine(model.G);
                tw.WriteLine(model.B);
                tw.Close();
            }
        }

        public static TextureColorModel GetDataFromFile(string id)
        {
            TextureColorModel model;
            string file = "textureColorCache//" + id;
            if (!File.Exists(file))
                model = new TextureColorModel(null, 255, 255, 0, 0);
            else
            {
                TextReader tr = new StreamReader(file);
                byte a = Convert.ToByte(tr.ReadLine());
                byte r = Convert.ToByte(tr.ReadLine());
                byte g = Convert.ToByte(tr.ReadLine());
                byte b = Convert.ToByte(tr.ReadLine());
                model = new TextureColorModel(id, a, r, g, b);
                tr.Close();
            }
            return model;
        }

        public static void ConnectSqlite(string connectionString)
        {
            m_sqliteConnect = new SQLiteConnection(connectionString);
            m_sqliteConnect.Open();
        }

        public static void DisconnectSqlite()
        {
            if (m_sqliteConnect != null)
            {
                m_sqliteConnect.Close();
                m_sqliteConnect = null;
            }
        }

        public static void InitializeSqlite()
        {
            lock (m_sqliteConnect)
            {
                using (var cmd = new SQLiteCommand("CREATE TABLE IF NOT EXISTS assets(id char(50), a byte, r byte, g byte, b byte)", m_sqliteConnect))
                {
                    cmd.ExecuteNonQuery();
                }
            }
        }

        public static void StoreDataIntoSqlite(List<TextureColorModel> models)
        {
            lock (m_sqliteConnect)
            {
                foreach (TextureColorModel model in models)
                {
                    if (ExistData(model.ID))
                    {
                        using (SQLiteCommand cmd = new SQLiteCommand("UPDATE assets set a=:a, r=:r, g=:g, b=:b WHERE id=:id", m_sqliteConnect))
                        {
                            cmd.Parameters.Add(new SQLiteParameter(":id", model.ID));
                            cmd.Parameters.Add(new SQLiteParameter(":a", model.A));
                            cmd.Parameters.Add(new SQLiteParameter(":r", model.R));
                            cmd.Parameters.Add(new SQLiteParameter(":g", model.G));
                            cmd.Parameters.Add(new SQLiteParameter(":b", model.B));

                            cmd.ExecuteNonQuery();
                        }
                    }
                    else
                    {
                        using (SQLiteCommand cmd = new SQLiteCommand("INSERT INTO assets(id, a, r, g, b) values(:id, :a, :r, :g, :b)", m_sqliteConnect))
                        {
                            cmd.Parameters.Add(new SQLiteParameter(":id", model.ID));
                            cmd.Parameters.Add(new SQLiteParameter(":a", model.A));
                            cmd.Parameters.Add(new SQLiteParameter(":r", model.R));
                            cmd.Parameters.Add(new SQLiteParameter(":g", model.G));
                            cmd.Parameters.Add(new SQLiteParameter(":b", model.B));

                            cmd.ExecuteNonQuery();
                        }
                    }
                }
            }
        }

        public static TextureColorModel GetDataFromSqlite(string id)
        {
            TextureColorModel model;
            lock (m_sqliteConnect)
            {
                using (SQLiteCommand cmd = new SQLiteCommand("SELECT * FROM assets WHERE id=:id", m_sqliteConnect))
                {
                    cmd.Parameters.Add(new SQLiteParameter(":id", id));
                    using (SQLiteDataReader reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            model = new TextureColorModel((string)reader["id"], Convert.ToByte(reader["a"]), Convert.ToByte(reader["r"]), Convert.ToByte(reader["g"]), Convert.ToByte(reader["b"]));
                            reader.Close();
                        }
                        else
                        {
                            model = new TextureColorModel(null, 255, 255, 0, 0);
                            reader.Close();
                        }
                    }
                }
            }
            return model;
        }

        public static bool ExistData(string id)
        {
            lock (m_sqliteConnect)
            {
                using (SQLiteCommand cmd = new SQLiteCommand("SELECT id FROM assets where id=:id", m_sqliteConnect))
                {
                    cmd.Parameters.Add(new SQLiteParameter(":id", id));
                    using (SQLiteDataReader reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            reader.Close();
                            return true;
                        }
                        else
                        {
                            reader.Close();
                            return false;
                        }
                    }
                }
            }
        }

        public static void ConnectMysql(string connectionString)
        {
            m_mysqlConnect = new MySqlConnection(connectionString);
            m_mysqlConnect.Open();
        }

        public static void DisconnectMysql()
        {
            if (m_mysqlConnect != null)
            {
                m_mysqlConnect.Close();
                m_mysqlConnect = null;
            }
        }

        public static List<TextureColorModel> GetDataFromMysql()
        {
            List<TextureColorModel> models = new List<TextureColorModel>();
            lock (m_mysqlConnect)
            {
                using (MySqlCommand cmd = new MySqlCommand("SELECT id, data FROM assets", m_mysqlConnect))
                {
                    using (MySqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            string id = (string)reader["id"];
                            byte[] data = (byte[])reader["data"];
                            SimpleColorCL color = GetColorFromTexture(data);
                            if (color != null)
                            {
                                models.Add(new TextureColorModel(id, color.GetA(), color.GetR(), color.GetG(), color.GetB()));
                            }
                        }
                    }
                }
            }
            return models;
        }

        private static SimpleColorCL GetColorFromTexture(byte[] data)
        {
            using (MemoryStream ms = new MemoryStream(data))
            {
                FIBITMAP dib = FreeImage.LoadFromStream(ms);
                if (dib.IsNull)
                {
                    return null;
                }
                uint width = FreeImage.GetWidth(dib);
                uint height = FreeImage.GetHeight(dib);
                int sum = (int)(width * height);
                int r = 0, g = 0, b = 0, a = 0;
                for (uint x = 0; x < width; x++)
                    for (uint y = 0; y < height; y++)
                    {
                        RGBQUAD color;
                        FreeImage.GetPixelColor(dib, x, y, out color);
                        r += color.rgbRed;
                        g += color.rgbGreen;
                        b += color.rgbBlue;
                        a += color.rgbReserved;
                    }
                r = r / sum;
                g = g / sum;
                b = b / sum;
                a = a / sum;

                FreeImage.Unload(dib);
                return new SimpleColorCL((byte)a, (byte)r, (byte)g, (byte)b);
            }
        }
    }

    public class TextureColorModel
    {
        public string ID;
        public byte A;
        public byte R;
        public byte G;
        public byte B;
        public TextureColorModel(string id, byte a, byte r, byte g, byte b)
        {
            ID = id;
            A = a;
            R = r;
            G = g;
            B = b;
        }
        public TextureColorModel() { }
    }
}
