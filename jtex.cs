/* Lifted from pk3DS
 * https://raw.githubusercontent.com/kwsch/pk3DS/master/pk3DS/3DS/BCLIM.cs
 * Author: Kaphotics
 * License: See pk3DS
 */

using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;

namespace CTR
{
    public class Picross
    {
        internal static void openFile(string path, bool autosave = false, bool crop = true, char format = 'X')
        {
            // Handle file
            if (!File.Exists(path)) throw new Exception("Can only accept files, not folders");
             makeBMP(path, autosave, crop);
        }

        internal static Image makeBMP(string path, bool autosave = false, bool crop = true)
        {
            jtex tex = analyze(path);

            Bitmap img = getIMG(tex);

            if (img == null) return null;
            Rectangle cropRect = new Rectangle(0, 0, (int)tex.Width, (int)tex.Height);
            Bitmap CropBMP = new Bitmap(cropRect.Width, cropRect.Height);
            using (Graphics g = Graphics.FromImage(CropBMP))
            {
                g.DrawImage(img, 
                            new Rectangle(0, 0, CropBMP.Width, CropBMP.Height),
                            cropRect,
                            GraphicsUnit.Pixel);
            }
            if (!autosave) return !crop ? img : CropBMP;

            using (MemoryStream ms = new MemoryStream())
            {
                //error will throw from here
                CropBMP.Save(ms, ImageFormat.Png);
                byte[] data = ms.ToArray();
                File.WriteAllBytes(Path.Combine(Path.GetDirectoryName(path), Path.GetFileNameWithoutExtension(path) + ".png"), data);
            }
            return !crop ? img : CropBMP;
        }
        // Bitmap Data Writing
        internal static Bitmap getIMG(int width, int height, byte[] bytes, int f)
        {
            Bitmap img = new Bitmap(width, height);
            int area = img.Width * img.Height;
            // Tiles Per Width
            int p = gcm(img.Width, 8) / 8;
            if (p == 0) p = 1;
            using (Stream BitmapStream = new MemoryStream(bytes))
            using (BinaryReader br = new BinaryReader(BitmapStream))
            for (uint i = 0; i < area; i++) // for every pixel
            {
                uint x;
                uint y;
                d2xy(i % 64, out x, out y);
                uint tile = i / 64;

                // Shift Tile Coordinate into Tilemap
                x += (uint)(tile % p) * 8;
                y += (uint)(tile / p) * 8;

                // Get Color
                Color c;
                switch (f)
                {
                    case 0x0:  // L8        // 8bit/1 byte
                    case 0x1:  // A8
                    case 0x2:  // LA4
                        c = DecodeColor(br.ReadByte(), f);
                        break;
                    case 0x3:  // LA8       // 16bit/2 byte
                    case 0x4:  // HILO8
                    case 0x5:  // RGB565
                    case 0x8:  // RGBA4444
                    case 0x7:  // RGBA5551
                        c = DecodeColor(br.ReadUInt16(), f);
                        break;
                    case 0x6:  // RGB8:     // 24bit
                        byte[] data = br.ReadBytes(3); Array.Resize(ref data, 4);
                        c = DecodeColor(BitConverter.ToUInt32(data, 0), f);
                        break;
                    case 0x9:  // RGBA8888
                        c = DecodeColor(br.ReadUInt32(), f);
                        break;
                    case 0xC:  // L4
                    case 0xD:  // A4        // 4bit - Do 2 pixels at a time.
                        uint val = br.ReadByte();
                        img.SetPixel((int)x, (int)y, DecodeColor(val & 0xF, f)); // lowest bits for the low pixel
                        i++; x++;
                        c = DecodeColor(val >> 4, f);   // highest bits for the high pixel
                        break;
                    default: throw new Exception("Invalid FileFormat.");
                }
                img.SetPixel((int)x, (int)y, c);
            }
            return img;
        }
        internal static Bitmap getIMG(jtex bclim)
        {
            // New Image
            int w = nlpo2(gcm((int)bclim.Width, 8));
            int h = nlpo2(gcm((int)bclim.Height, 8));
            const int f = 9;
            int area = w * h;
            if (area > bclim.Data.Length / 4)
            {
                w = gcm((int)bclim.Width, 8);
                h = gcm((int)bclim.Height, 8);
            }
            // Build Image
            return getIMG(w, h, bclim.Data, f);
        }

        // BCLIM Data Writing

        internal static byte[] getPixelData(Bitmap img, int format, bool rectangle = true)
        {
            int w = img.Width;
            int h = img.Height;

            bool perfect = (w == h && (w != 0) && ((w & (w - 1)) == 0));
            if (!perfect) // Check if square power of two, else resize
            {
                // Square Format Checks
                if (rectangle && Math.Min(img.Width, img.Height) < 32)
                {
                    w = nlpo2(img.Width);
                    h = nlpo2(img.Height);
                }
                else
                {
                    w = h = Math.Max(nlpo2(w), nlpo2(h)); // else resize
                }
            }

            using (MemoryStream mz = new MemoryStream())
            using (BinaryWriter bz = new BinaryWriter(mz))
            {
                int p = gcm(w, 8) / 8;
                if (p == 0) p = 1;
                for (uint i = 0; i < w * h; i++)
                {
                    uint x;
                    uint y;
                    d2xy(i % 64, out x, out y);

                    // Get Shift Tile
                    uint tile = i / 64;

                    // Shift Tile Coordinate into Tilemap
                    x += (uint)(tile % p) * 8;
                    y += (uint)(tile / p) * 8;

                    // Don't write data
                    Color c;
                    if (x >= img.Width || y >= img.Height)
                    { c = Color.FromArgb(0, 0, 0, 0); }
                    else
                    { c = img.GetPixel((int)x, (int)y); if (c.A == 0) c = Color.FromArgb(0, 86, 86, 86); }

                    switch (format)
                    {
                        case 0: bz.Write(GetL8(c)); break;                // L8
                        case 1: bz.Write(GetA8(c)); break;                // A8
                        case 2: bz.Write(GetLA4(c)); break;               // LA4(4)
                        case 3: bz.Write(GetLA8(c)); break;             // LA8(8)
                        case 4: bz.Write(GetHILO8(c)); break;           // HILO8
                        case 5: bz.Write(GetRGB565(c)); break;          // RGB565
                        case 6:
                            {
                                bz.Write(c.B);
                                bz.Write(c.G);
                                bz.Write(c.R); break;
                            }
                        case 7: bz.Write(GetRGBA5551(c)); break;        // RGBA5551
                        case 8: bz.Write(GetRGBA4444(c)); break;        // RGBA4444
                        case 9: bz.Write(GetRGBA8888(c)); break;          // RGBA8
                        case 10: throw new Exception("ETC1 not supported.");
                        case 11: throw new Exception("ETC1A4 not supported.");
                        case 12:
                            {
                                byte val = (byte)(GetL8(c) / 0x11); // First Pix    // L4
                                { c = img.GetPixel((int)x, (int)y); if (c.A == 0) c = Color.FromArgb(0, 0, 0, 0); }
                                val |= (byte)((GetL8(c) / 0x11) << 4); i++;
                                bz.Write(val); break;
                            }
                        case 13:
                            {
                                byte val = (byte)(GetA8(c) / 0x11); // First Pix    // L4
                                { c = img.GetPixel((int)x, (int)y); }
                                val |= (byte)((GetA8(c) / 0x11) << 4); i++;
                                bz.Write(val); break;
                            }
                    }
                }
                if (!perfect)
                    while (mz.Length < nlpo2((int)mz.Length)) // pad
                        bz.Write((byte)0);
                return mz.ToArray();
            }            
        }


        internal static int[] Convert5To8 = { 0x00,0x08,0x10,0x18,0x20,0x29,0x31,0x39,
                                              0x41,0x4A,0x52,0x5A,0x62,0x6A,0x73,0x7B,
                                              0x83,0x8B,0x94,0x9C,0xA4,0xAC,0xB4,0xBD,
                                              0xC5,0xCD,0xD5,0xDE,0xE6,0xEE,0xF6,0xFF };

        internal static Color DecodeColor(uint val, int format)
        {
            int alpha = 0xFF, red, green, blue;
            switch (format)
            {
                case 0: // L8
                    return Color.FromArgb(alpha, (byte)val, (byte)val, (byte)val);
                case 1: // A8
                    return Color.FromArgb((byte)val, alpha, alpha, alpha);
                case 2: // LA4
                    red = (byte)(val >> 4);
                    alpha = (byte)(val & 0x0F);
                    return Color.FromArgb(alpha, red, red, red);
                case 3: // LA8
                    red = (byte)((val >> 8 & 0xFF));
                    alpha = (byte)(val & 0xFF);
                    return Color.FromArgb(alpha, red, red, red);
                case 4: // HILO8
                    red = (byte)(val >> 8);
                    green = (byte)(val & 0xFF);
                    return Color.FromArgb(alpha, red, green, 0xFF);
                case 5: // RGB565
                    red = Convert5To8[(val >> 11) & 0x1F];
                    green = (byte)(((val >> 5) & 0x3F) * 4);
                    blue = Convert5To8[val & 0x1F];
                    return Color.FromArgb(alpha, red, green, blue);
                case 6: // RGB8
                    red = (byte)((val >> 16) & 0xFF);
                    green = (byte)((val >> 8) & 0xFF);
                    blue = (byte)(val & 0xFF);
                    return Color.FromArgb(alpha, red, green, blue);
                case 7: // RGBA5551
                    red = Convert5To8[(val >> 11) & 0x1F];
                    green = Convert5To8[(val >> 6) & 0x1F];
                    blue = Convert5To8[(val >> 1) & 0x1F];
                    alpha = (val & 0x0001) == 1 ? 0xFF : 0x00;
                    return Color.FromArgb(alpha, red, green, blue);
                case 8: // RGBA4444
                    alpha = (byte)(0x11 * (val & 0xf));
                    red = (byte)(0x11 * ((val >> 12) & 0xf));
                    green = (byte)(0x11 * ((val >> 8) & 0xf));
                    blue = (byte)(0x11 * ((val >> 4) & 0xf));
                    return Color.FromArgb(alpha, red, green, blue);
                case 9: // RGBA8888
                    red = (byte)((val >> 24) & 0xFF);
                    green = (byte)((val >> 16) & 0xFF);
                    blue = (byte)((val >> 8) & 0xFF);
                    alpha = (byte)(val & 0xFF);
                    return Color.FromArgb(alpha, red, green, blue);
                // case 10:
                // case 11:
                case 12: // L4
                    return Color.FromArgb(alpha, (byte)(val * 0x11), (byte)(val * 0x11), (byte)(val * 0x11));
                case 13: // A4
                    return Color.FromArgb((byte)(val * 0x11), alpha, alpha, alpha);
                default:
                    return Color.White;
            }
        }

        // Color Conversion
        internal static byte GetL8(Color c)
        {
            byte red = c.R;
            byte green = c.G;
            byte blue = c.B;
            // Luma (Y’) = 0.299 R’ + 0.587 G’ + 0.114 B’ from wikipedia
            return (byte)(((0x4CB2 * red + 0x9691 * green + 0x1D3E * blue) >> 16) & 0xFF);
        }        // L8
        internal static byte GetA8(Color c)
        {
            return c.A;
        }        // A8
        internal static byte GetLA4(Color c)
        {
            return (byte)((c.A / 0x11) + (c.R / 0x11) << 4);
        }       // LA4
        internal static ushort GetLA8(Color c)
        {
            return (ushort)((c.A) + ((c.R) << 8));
        }     // LA8
        internal static ushort GetHILO8(Color c)
        {
            return (ushort)((c.G) + ((c.R) << 8));
        }   // HILO8
        internal static ushort GetRGB565(Color c)
        {
            int val = 0;
            // val += c.A >> 8; // unused
            val += convert8to5(c.B) >> 3;
            val += (c.G >> 2) << 5;
            val += convert8to5(c.R) << 10;
            return (ushort)val;
        }  // RGB565
        // RGB8
        internal static ushort GetRGBA5551(Color c)
        {
            int val = 0;
            val += (byte)(c.A > 0x80 ? 1 : 0);
            val += convert8to5(c.R) << 11;
            val += convert8to5(c.G) << 6;
            val += convert8to5(c.B) << 1;
            ushort v = (ushort)val;

            return v;
        }// RGBA5551
        internal static ushort GetRGBA4444(Color c)
        {
            int val = 0;
            val += (c.A / 0x11);
            val += ((c.B / 0x11) << 4);
            val += ((c.G / 0x11) << 8);
            val += ((c.R / 0x11) << 12);
            return (ushort)val;
        }// RGBA4444
        internal static uint GetRGBA8888(Color c)     // RGBA8888
        {
            uint val = 0;
            val += c.A;
            val += (uint)(c.B << 8);
            val += (uint)(c.G << 16);
            val += (uint)(c.R << 24);
            return val;
        }

        // Unit Conversion
        internal static byte convert8to5(int colorval)
        {

            byte[] Convert8to5 = { 0x00,0x08,0x10,0x18,0x20,0x29,0x31,0x39,
                                   0x41,0x4A,0x52,0x5A,0x62,0x6A,0x73,0x7B,
                                   0x83,0x8B,0x94,0x9C,0xA4,0xAC,0xB4,0xBD,
                                   0xC5,0xCD,0xD5,0xDE,0xE6,0xEE,0xF6,0xFF };
            byte i = 0;
            while (colorval > Convert8to5[i]) i++;
            return i;
        }
        internal static UInt32 DM2X(UInt32 code)
        {
            return C11(code >> 0);
        }
        internal static UInt32 DM2Y(UInt32 code)
        {
            return C11(code >> 1);
        }
        internal static UInt32 C11(UInt32 x)
        {
            x &= 0x55555555;                  // x = -f-e -d-c -b-a -9-8 -7-6 -5-4 -3-2 -1-0
            x = (x ^ (x >> 1)) & 0x33333333; // x = --fe --dc --ba --98 --76 --54 --32 --10
            x = (x ^ (x >> 2)) & 0x0f0f0f0f; // x = ---- fedc ---- ba98 ---- 7654 ---- 3210
            x = (x ^ (x >> 4)) & 0x00ff00ff; // x = ---- ---- fedc ba98 ---- ---- 7654 3210
            x = (x ^ (x >> 8)) & 0x0000ffff; // x = ---- ---- ---- ---- fedc ba98 7654 3210
            return x;
        }
        
        /// <summary>
        /// Greatest common multiple (to round up)
        /// </summary>
        /// <param name="n">Number to round-up.</param>
        /// <param name="m">Multiple to round-up to.</param>
        /// <returns>Rounded up number.</returns>
        internal static int gcm(int n, int m)
        {
            return ((n + m - 1) / m) * m;
        }
        /// <summary>
        /// Next Largest Power of 2
        /// </summary>
        /// <param name="x">Input to round up to next 2^n</param>
        /// <returns>2^n > x && x > 2^(n-1) </returns>
        internal static int nlpo2(int x)
        {
            x--; // comment out to always take the next biggest power of two, even if x is already a power of two
            x |= (x >> 1);
            x |= (x >> 2);
            x |= (x >> 4);
            x |= (x >> 8);
            x |= (x >> 16);
            return (x+1);
        }

        // Morton Translation
        /// <summary>
        /// Combines X/Y Coordinates to a decimal ordinate.
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <returns></returns>
        internal static uint xy2d(uint x, uint y)
        {
	        x &= 0x0000ffff;
	        y &= 0x0000ffff;
	        x |= (x << 8);
	        y |= (y << 8);
	        x &= 0x00ff00ff;
	        y &= 0x00ff00ff;
	        x |= (x << 4);
	        y |= (y << 4);
	        x &= 0x0f0f0f0f;
	        y &= 0x0f0f0f0f;
	        x |= (x << 2);
	        y |= (y << 2);
	        x &= 0x33333333;
	        y &= 0x33333333;
	        x |= (x << 1);
	        y |= (y << 1);
	        x &= 0x55555555;
	        y &= 0x55555555;
	        return x | (y << 1);
        }
        /// <summary>
    /// Decimal Ordinate In to X / Y Coordinate Out
    /// </summary>
    /// <param name="d">Loop integer which will be decoded to X/Y</param>
    /// <param name="x">Output X coordinate</param>
    /// <param name="y">Output Y coordinate</param>
        internal static void d2xy(uint d, out uint x, out uint y)
        {
	        x = d;
	        y = (x >> 1);
	        x &= 0x55555555;
	        y &= 0x55555555;
	        x |= (x >> 1);
	        y |= (y >> 1);
	        x &= 0x33333333;
	        y &= 0x33333333;
	        x |= (x >> 2);
	        y |= (y >> 2);
	        x &= 0x0f0f0f0f;
	        y &= 0x0f0f0f0f;
	        x |= (x >> 4);
	        y |= (y >> 4);
	        x &= 0x00ff00ff;
	        y &= 0x00ff00ff;
	        x |= (x >> 8);
	        y |= (y >> 8);
	        x &= 0x0000ffff;
	        y &= 0x0000ffff;
        }

        public static jtex analyze(byte[] data)
        {
            jtex tex = new jtex();
            if (data[0] == 0x11) // compressed
                try
                {
                    MemoryStream oldD = new MemoryStream(data);
                    MemoryStream newD = new MemoryStream();
                    LZSS.Decompress(oldD, data.Length, newD);
                    data = newD.ToArray();
                }
                catch
                {
                    return tex;
                }

            tex.Length = BitConverter.ToUInt32(data, 0x0);
            tex.Width = BitConverter.ToUInt32(data, 0x8);
            tex.Height = BitConverter.ToUInt32(data, 0xC);

            tex.Data = data.Skip((int)tex.Length).ToArray();
            return tex;
        }
        public static jtex analyze(string path)
        {
            return analyze(File.ReadAllBytes(path));
        }
        public struct jtex
        {
            public UInt32 Length;
            public UInt16 Unk;   
            public UInt32 Width; 
            public UInt32 Height;
            
            public byte[] Data;
        }
    }
}