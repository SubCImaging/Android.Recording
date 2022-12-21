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
        private readonly ImageReader reader;
        private string baseDirectory;
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

            var dir = $"{baseDirectory}/Stills/";

            string path = $"{dir}/pic{folderIndex}";
            Camera.MainActivity.ShellSync($"mkdir -p \"{path}\"");
            Camera.MainActivity.ShellSync($"chmod -R 777 \"{path}\"");
        }

        public event EventHandler DriveFull;

        public event EventHandler ImageFailed;

        public event EventHandler<string> ImageSaved;

        public double GetDiskSpaceRemaining()
        {
            baseDirectory = $"{Camera.MainActivity.StorageLocation}/{Camera.MainActivity.GetStoragePoint()}";

            var fs = new StatFs(baseDirectory);
            var free = fs.AvailableBlocksLong * fs.BlockSizeLong;
            return free;
        }

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

            var stillName = GetStillFile();

            WriteJpeg(image, stillName);

            ImageSaved?.Invoke(this, stillName);

            Android.Util.Log.Info("SubC", $"+++> Image {totalIndex} saved, so far failed {failed} time");
            image.Close();
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

        private bool SaveImage(string file, byte[] bytes)
        {
            if (file == null)
            {
                throw new ArgumentNullException("File cannot be null");
            }

            using var output = new Java.IO.FileOutputStream(file);
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

        private void WriteJpeg(Image image, string file)
        {
            var buffer = image.GetPlanes().First().Buffer;
            var bytes = new byte[buffer.Remaining()];
            buffer.Get(bytes);

            Android.Util.Log.Info("SubC", $"Saving: {file}");
            var freeExternalStorage = GetDiskSpaceRemaining();

            if (freeExternalStorage < 18759680)
            {
                //System.Diagnostics.Debug.WriteLine("Information: Filled drive!!");
                Android.Util.Log.Info("SubC", "Information: Filled drive!!");
                DriveFull?.Invoke(this, EventArgs.Empty);
                // throw new FileNotFoundException("filled!!");
            }
            if (freeExternalStorage > 18759680)
            {
                //System.Diagnostics.Debug.WriteLine("Warning: Freespace: " + freeExternalStorage);
                Android.Util.Log.Info("SubC", "Warning: Freespace: " + freeExternalStorage);

                SaveImage(file, bytes);
            }
        }
    }
}