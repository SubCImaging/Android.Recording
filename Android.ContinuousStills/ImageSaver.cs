using Android.App;
using Android.Content;
using Android.Media;
using Android.Runtime;
using Android.Widget;
using Java.IO;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

namespace Android.ContinuousStills
{
    public class ImageSaver
    {
        private readonly string baseDirectory;
        private readonly ImageReader reader;
        private int failed;
        private int folderIndex = 0;

        private QueueSave handler;
        private int index = 0;

        private int max = 1000;
        private ConcurrentQueue<(Image, File)> queue = new ConcurrentQueue<(Image, File)>();

        public ImageSaver(string baseDirectory, ImageReader reader)
        {
            this.baseDirectory = baseDirectory;
            this.reader = reader;

            handler = new QueueSave("QueueSave", queue);
        }

        public event EventHandler ImageFailed;

        //public event EventHandler<string> ImageSaved;

        public void OnImageAvailable(ImageReader reader)
        {
            Android.Util.Log.Info("SubC", "Image listener ... acquiring image");
            //Image image = reader.AcquireNextImage();

            //var attempts = 0;

            //while (image == null)
            //{
            //    attempts++;

            //    if (attempts > 20)
            //    {
            //        Android.Util.Log.Info("SubC", "Image listener ... Unable to acquire image");

            //        if (queue.Count > 0)
            //        {
            //            Android.Util.Log.Info("SubC", "Image listener ... Running");
            //            handler.Run();
            //        }

            //        break;
            //    }

            //    await Task.Delay(50);

            //    image = reader.AcquireNextImage();
            //}
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
                return;
            }

            Android.Util.Log.Info("SubC", "Image listener ... Image acquired");

            //var file = GetStillFile();
            //var dir = file.Parent;

            //if (!System.IO.Directory.Exists(dir))
            //{
            //    System.IO.Directory.CreateDirectory(dir);
            //}

            ////            System.Diagnostics.Debug.WriteLine($"Information: Starting to capture {index}: " + file.Path);
            //Android.Util.Log.Info("SubC", $"Information: Starting to capture { index}: " + file.Path);

            //queue.Enqueue((image, file));

            //if (queue.Count >= 10)
            //{
            //    //System.Diagnostics.Debug.WriteLine("---> Running");
            //    Android.Util.Log.Info("SubC", "Image listener ... Running in queue");
            //    handler.Run();
            //}
            index++;
            //ImageSaved?.Invoke(this, file.AbsolutePath);
            Android.Util.Log.Info("SubC", $"+++> Image {index} acquired");
            QueueSave.WriteJpeg(image, GetStillFile());
            Android.Util.Log.Info("SubC", $"+++> Image {index} saved");
            image.Close();
        }

        public void SaveImage()
        {
            OnImageAvailable(reader);
        }

        private File GetStillFile()
        {
            string fileName = "still-" + DateTime.Now.ToString("yyMMdd-hhmmss.fff") + ".jpg"; //new filenamed based on date time

            var dir = $"{baseDirectory}/Stills/";

            string path = $"{dir}/pic{folderIndex}";

            if (index >= max)
            {
                index = 0;
                folderIndex++;
                path = $"{dir}/pic{folderIndex}";
                Camera.MainActivity.ShellSync($"mkdir -p \"{path}\"");
                Camera.MainActivity.ShellSync($"chmod -R 777 \"{path}\"");
            }

            index++;

            var file = new File(path, fileName);

            //System.Diagnostics.Debug.WriteLine($"{file}");
            Android.Util.Log.Info("SubC", $"{file}");

            return file;
        }
    }
}