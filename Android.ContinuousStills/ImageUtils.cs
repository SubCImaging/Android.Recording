using Android.Graphics;
using Android.OS;
using System;
using System.IO;

namespace Android.ContinuousStills
{
    /// <summary>
    /// Utility class for manipulating YUV and ARGB images.
    /// </summary>
    public static class ImageUtils
    {
        public const int MaxChannelValue = 262143; // 2^18 - 1

        /// <summary>
        /// Converts YUV420SP data to ARGB8888 pixel array.
        /// </summary>
        public static void ConvertYUV420SPToARGB8888(byte[] input, int width, int height, int[] output)
        {
            int frameSize = width * height;
            int yp = 0;

            for (int j = 0; j < height; j++)
            {
                int uvp = frameSize + (j >> 1) * width;
                int u = 0, v = 0;

                for (int i = 0; i < width; i++)
                {
                    int y = input[yp] & 0xFF;

                    if ((i & 1) == 0)
                    {
                        v = input[uvp++] & 0xFF;
                        u = input[uvp++] & 0xFF;
                    }

                    output[yp++] = YUV2RGB(y, u, v);
                }
            }
        }

        //    try
        //    {
        //        using var stream = new FileStream(file.AbsolutePath, FileMode.Create, FileAccess.Write);
        //        bitmap.Compress(Bitmap.CompressFormat.Png, 99, stream);
        //    }
        //    catch
        //    {
        //        // Swallow errors (or add logging if needed)
        //    }
        //}
        /// <summary>
        /// Converts planar YUV420 data to ARGB8888 pixel array.
        /// </summary>
        public static void ConvertYUV420ToARGB8888(
            byte[] yData,
            byte[] uData,
            byte[] vData,
            int width,
            int height,
            int yRowStride,
            int uvRowStride,
            int uvPixelStride,
            int[] output)
        {
            int yp = 0;

            for (int j = 0; j < height; j++)
            {
                int pY = yRowStride * j;
                int pUV = uvRowStride * (j >> 1);

                for (int i = 0; i < width; i++)
                {
                    int uvOffset = pUV + (i >> 1) * uvPixelStride;
                    int y = yData[pY + i] & 0xFF;
                    int u = uData[uvOffset] & 0xFF;
                    int v = vData[uvOffset] & 0xFF;

                    output[yp++] = YUV2RGB(y, u, v);
                }
            }
        }

        /// <summary>
        /// Computes the allocated size in bytes of a YUV420SP image of the given dimensions.
        /// </summary>
        public static int GetYUVByteSize(int width, int height)
        {
            int ySize = width * height;
            int uvSize = ((width + 1) / 2) * ((height + 1) / 2) * 2;
            return ySize + uvSize;
        }

        /// <summary>
        /// Saves a Bitmap to disk for analysis.
        /// </summary>
        //public static void SaveBitmap(Bitmap bitmap, string filename = "preview.png")
        //{
        //    string root = Path.Combine(Environment.ExternalStorageDirectory.AbsolutePath, "tensorflow");
        //    var dir = new Java.IO.File(root);
        //    if (!dir.Exists()) dir.Mkdirs();

        //    var file = new Java.IO.File(dir, filename);
        //    if (file.Exists()) file.Delete();
        /// <summary>
        /// Converts YUV values into ARGB pixel.
        /// </summary>
        private static int YUV2RGB(int y, int u, int v)
        {
            y = Math.Max(y - 16, 0);
            u -= 128;
            v -= 128;

            int y1192 = 1192 * y;
            int r = y1192 + 1634 * v;
            int g = y1192 - 833 * v - 400 * u;
            int b = y1192 + 2066 * u;

            r = Math.Clamp(r, 0, MaxChannelValue);
            g = Math.Clamp(g, 0, MaxChannelValue);
            b = Math.Clamp(b, 0, MaxChannelValue);

            return unchecked((int)0xFF000000) |
                   ((r << 6) & 0x00FF0000) |
                   ((g >> 2) & 0x0000FF00) |
                   ((b >> 10) & 0x000000FF);
        }
    }
}