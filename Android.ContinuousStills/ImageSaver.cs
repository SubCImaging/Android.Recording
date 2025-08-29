using Android.App;
using Android.Content;
using Android.Graphics;
using Android.Media;
using Android.Runtime;
using Android.SE.Omapi;
using Android.Util;
using Android.Widget;
using Java.IO;
using Java.Nio;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using static Android.ContinuousStills.CameraHelpers;
using static Android.Media.ImageReader;

namespace Android.ContinuousStills
{
    public class ImageSaver : Java.Lang.Object, IOnImageAvailableListener
    {
        private readonly string baseDirectory;
        private readonly QueueSave handler;
        private readonly Size jpegSize;
        private readonly bool saveAsBmp;
        private readonly byte[][] yuvBytes = new byte[3][];

        //private readonly ImageReader reader;
        private int failed;

        private int folderIndex = 0;
        private int index = 0;

        private bool isSaving = false;
        private int max = 1000;
        private ConcurrentQueue<(Image, File)> queue = new ConcurrentQueue<(Image, File)>();

        private RunningAverage runningAverage = new RunningAverage(10);
        private DateTime timeSinceLastSave = DateTime.Now;

        private int yRowStride = 0;

        public ImageSaver(string baseDirectory, bool saveAsBmp, Size jpegSize)
        {
            this.baseDirectory = baseDirectory;
            this.saveAsBmp = saveAsBmp;
            this.jpegSize = jpegSize;
            handler = new QueueSave("QueueSave", queue);
        }

        public event EventHandler ImageFailed;

        public void OnImageAvailable(ImageReader reader)
        {
            if (saveAsBmp)
            {
                SaveAsBMP(reader);
                return;
            }

            if (isSaving)
            {
                Android.Util.Log.Info("SubC", "Error: Already saving");
                return;
            }

            isSaving = true;

            // Android.Util.Log.Info("SubC", "Image listener ... acquiring image");
            Image image = reader.AcquireLatestImage();
            if (image == null)
            {
                failed++;

                if (failed > 20)
                {
                    failed = 0;
                    ImageFailed?.Invoke(this, EventArgs.Empty);
                }
                Android.Util.Log.Info("SubC", $"---> Acquiring image return null, failed {failed} time");

                isSaving = false;

                return;
            }

            // Android.Util.Log.Info("SubC", "Image listener ... Image acquired");

            var file = GetStillFile();
            var dir = file.Parent;

            if (!System.IO.Directory.Exists(dir))
            {
                System.IO.Directory.CreateDirectory(dir);

                Camera.MainActivity.ShellSync($"mkdir -p \"{dir}\"");
            }

            Camera.MainActivity.ShellSync($"chmod -R 777 \"{dir}\"");

            index++;
            //ImageSaved?.Invoke(this, file.AbsolutePath);
            // Android.Util.Log.Info("SubC", $"+++> Image {index} acquired");
            QueueSave.WriteJpeg(image, GetStillFile());
            // Android.Util.Log.Info("SubC", $"+++> Image {index} saved");

            isSaving = false;

            var t = (DateTime.Now - timeSinceLastSave).TotalMilliseconds;

            timeSinceLastSave = DateTime.Now;

            Android.Util.Log.Info("SubC", $"Info: Still FPS: {runningAverage.Add(1000 / t)}");

            // image.Close();
        }

        protected void FillBytes(Image.Plane[] planes, byte[][] yuvBytes)
        {
            for (int i = 0; i < planes.Length; ++i)
            {
                ByteBuffer buffer = planes[i].Buffer;
                if (yuvBytes[i] == null || yuvBytes[i].Length < buffer.Capacity())
                {
                    yuvBytes[i] = new byte[buffer.Capacity()];
                }
                buffer.Get(yuvBytes[i]);
            }
        }

        private File GetStillFile()
        {
            string fileName = "still-" + DateTime.Now.ToString("yyMMdd-hhmmss.fff") + ".jpg"; //new filenamed based on date time

            var dir = $"{baseDirectory}/Stills";

            var path = $"{dir}/pic{folderIndex}";

            if (index >= max)
            {
                index = 0;
                folderIndex++;
                path = $"{dir}/pic{folderIndex}";
                Camera.MainActivity.ShellSync($"mkdir -p \"{path}\"");
                Camera.MainActivity.ShellSync($"chmod -R 777 \"{path}\"");
            }

            // index++;

            var file = new File(path, fileName);

            // Android.Util.Log.Info("SubC", $"{file}");

            return file;
        }

        private void SaveAsBMP(ImageReader reader)
        {
            var rgbArray = new int[jpegSize.Width * jpegSize.Height];

            // Android.Util.Log.Info("SubC", "Image listener ... acquiring image");
            Image image = reader.AcquireLatestImage();
            if (image == null)
            {
                failed++;

                if (failed > 20)
                {
                    failed = 0;
                    ImageFailed?.Invoke(this, EventArgs.Empty);
                }
                Android.Util.Log.Info("SubC", $"---> Acquiring image return null, failed {failed} time");

                isSaving = false;

                return;
            }

            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            var planes = image.GetPlanes();
            FillBytes(planes, yuvBytes);

            yRowStride = planes[0].RowStride;
            int uvRowStride = planes[1].RowStride;
            int uvPixelStride = planes[1].PixelStride;

            ImageUtils.ConvertYUV420ToARGB8888(
                        yuvBytes[0],
                        yuvBytes[1],
                        yuvBytes[2],
                        jpegSize.Width,
                        jpegSize.Height,
                        yRowStride,
                        uvRowStride,
                        uvPixelStride,
                        rgbArray
                    );

            image.Close();

            var rgbFrameBitmap = Graphics.Bitmap.CreateBitmap(jpegSize.Width, jpegSize.Height, Bitmap.Config.Argb8888);
            rgbFrameBitmap.SetPixels(rgbArray, 0, jpegSize.Width, 0, 0, jpegSize.Width, jpegSize.Height);

            var file = GetStillFile();

            var dir = file.Parent;

            if (!System.IO.Directory.Exists(dir))
            {
                System.IO.Directory.CreateDirectory(dir);

                Camera.MainActivity.ShellSync($"mkdir -p \"{dir}\"");
            }

            Camera.MainActivity.ShellSync($"chmod -R 777 \"{dir}\"");

            var sFile = file.AbsolutePath.Replace(".jpg", ".png");

            using var stream = new System.IO.FileStream(sFile, System.IO.FileMode.Create);
            rgbFrameBitmap.Compress(Bitmap.CompressFormat.Png, 99, stream);

            System.Console.WriteLine($"+++> Image {index} saved to {sFile} took {stopwatch.ElapsedMilliseconds} ms");
        }

        //public void SaveImage()
        //{
        //    OnImageAvailable(reader);
        //}
    }
}