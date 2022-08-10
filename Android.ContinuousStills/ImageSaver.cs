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
        private readonly string baseDirectory;
        private readonly ImageReader reader;
        private int failed;
        private int folderIndex = 0;

        private QueueSave handler;
        private int index = 0;
        private int totalIndex = 0;

        private int max = 1000;
        private ConcurrentQueue<(Image, File)> queue = new ConcurrentQueue<(Image, File)>();

        public ImageSaver(string baseDirectory, ImageReader reader)
        {
            this.baseDirectory = baseDirectory;
            this.reader = reader;

            handler = new QueueSave("QueueSave", queue);

            var dir = $"{baseDirectory}/Stills/";

            string path = $"{dir}/pic{folderIndex}";
            Camera.MainActivity.ShellSync($"mkdir -p \"{path}\"");
            Camera.MainActivity.ShellSync($"chmod -R 777 \"{path}\"");
        }

        public event EventHandler ImageFailed;

        //public event EventHandler<string> ImageSaved;

        public void OnImageAvailable(ImageReader reader)
        {
            Android.Util.Log.Info("SubC", "Image listener ... acquiring image");
            Image image = reader.AcquireLatestImage();
            if (image == null)
            {
                failed++;

                // if (failed > 20)
                // {
                //     failed = 0;
                //     ImageFailed?.Invoke(this, EventArgs.Empty);
                // }
                Android.Util.Log.Info("SubC", $"---> Acquiring image return null, failed {failed} time");
                return;
            }

            Android.Util.Log.Info("SubC", "Image listener ... Image acquired");

            WriteJpeg(image, GetStillFile());
            Android.Util.Log.Info("SubC", $"+++> Image {totalIndex} saved, so far failed {failed} time");
            image.Close();
        }

        private void WriteJpeg(Image image, string file)
        {
            var buffer = image.GetPlanes().First().Buffer;
            var bytes = new byte[buffer.Remaining()];
            buffer.Get(bytes);

            Android.Util.Log.Info("SubC", $"Saving: {file}");

            SaveImage(file, bytes);
        }

        private bool SaveImage(string file, byte[] bytes)
        {
            if (file == null)
            {
                throw new ArgumentNullException("File cannot be null");
            }

            using var output = new Java.IO.FileOutputStream (file);
            try
            {
                output.Write(bytes);
                return true;
            }
            catch (System.Exception e)
            {
                //System.Console.WriteLine($"Error: Failed to save image {file.AbsolutePath} with exception: {e}");
                Android.Util.Log.Info("SubC", $"Error: Failed to save image {file} with exception: {e}");

                return false;
            }
            finally
            {
                output?.Close();
            }
            return false;
        }

        public void SaveImage()
        {
            OnImageAvailable(reader);
        }

        private string GetStillFile()
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

            var file = new File(path, fileName).ToString();

            //System.Diagnostics.Debug.WriteLine($"{file}");
            // Android.Util.Log.Info("SubC", $"GetStillFile returns {file}");

            return file;
        }
    }
}
