//-----------------------------------------------------------------------
// <copyright file="GreenPictureDetector.cs" company="SubC Imaging">
// Copyright (c) SubC Imaging. All rights reserved.
// </copyright>
// <author>Aaron Watson</author>
//-----------------------------------------------------------------------

namespace SubCTools.Droid.Tools
{
    using Android.Graphics;
    using Android.Media;
    using System.Diagnostics;
    using System.IO;

    /// <summary>
    /// A class for detecting green pictures in the Rayfin.
    /// It does this by checking the corners, if more than 1 is the correct shade
    /// of green it will return true.
    /// </summary>
    public static class GreenPictureDetector
    {
        /// <summary>
        /// The green color we're looking for(YUV0).
        /// </summary>
        private static readonly int Green = -16742656;

        /// <summary>
        /// Checks if the bitmap is green (YUV0).
        /// </summary>
        /// <param name="bitmap">Input <see cref="Bitmap"/>.</param>
        /// <returns>A value indicating whether or not the file has more than 1 green corner.</returns>
        public static bool BitmapIsGreen(Bitmap bitmap)
        {
            var numberOfGreenCorners = 0;

            if (bitmap.GetPixel(0, 0) == Green)
            {
                numberOfGreenCorners++;
            }

            if (bitmap.GetPixel(bitmap.Width - 1, 0) == Green)
            {
                numberOfGreenCorners++;
            }

            if (bitmap.GetPixel(0, bitmap.Height - 1) == Green)
            {
                numberOfGreenCorners++;
            }

            if (bitmap.GetPixel(bitmap.Width - 1, bitmap.Height - 1) == Green)
            {
                numberOfGreenCorners++;
            }

            bitmap.Dispose();

            return numberOfGreenCorners > 1 ? true : false;
        }

        /// <summary>
        /// Checks if the following image is green (YUV0).
        /// </summary>
        /// <param name="file">Input file, must be JPEG.</param>
        /// <returns>A value indicating whether or not the file has more than 1 green corner.</returns>
        public static bool ImageIsGreen(FileInfo file)
        {
            var timer = Stopwatch.StartNew();
            Bitmap bitmap = BitmapFactory.DecodeFile(file.FullName);

            return BitmapIsGreen(bitmap);
        }
    }
}