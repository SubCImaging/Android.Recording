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
using Android.OS;
using System.Linq;

namespace Android.ContinuousStills
{
    public class ImageSaver : Java.Lang.Object, ImageReader.IOnImageAvailableListener
    {
        public readonly string baseDirectory;
        private readonly ImageReader reader;
        private int failed;
        private int folderIndex = 0;

        private QueueSave handler;
        private int index = 0;
        private int max = 1000;
        private ConcurrentQueue<(Image, File)> queue = new ConcurrentQueue<(Image, File)>();
        private int totalIndex = 0;

        public ImageSaver(string baseDirectory, ImageReader reader)
        {
            this.baseDirectory = baseDirectory;
            this.reader = reader;

            handler = new QueueSave("QueueSave", queue);
            handler.Start();

            var dir = $"{baseDirectory}/Stills/";

            string path = $"{dir}/pic{folderIndex}";
            Camera.MainActivity.ShellSync($"mkdir -p \"{path}\"");
            Camera.MainActivity.ShellSync($"chmod -R 777 \"{path}\"");
        }

        public void OnImageAvailable(ImageReader reader)
        {
            Android.Util.Log.Info("SubC", "Image listener ... acquiring image");
            Image image = reader.AcquireLatestImage();
            if (image == null)
            {
                failed++;

                Android.Util.Log.Info("SubC", $"---> Acquiring image return null, failed {failed} time");
                return;
            }

            if (queue.Count < 10)
            {
                queue.Enqueue((image, GetStillFile()));
                Android.Util.Log.Info("SubC", $"+++> Image is enquened, so far failed {failed} time, acquire {totalIndex}  time");
            }
            else
            {
                Android.Util.Log.Info("SubC", $"+++> queue is full, drop image");
                image.Close();
            }
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
            totalIndex++;

            return new File(path, fileName);
        }
    }
}