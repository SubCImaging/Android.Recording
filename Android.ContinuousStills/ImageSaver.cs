using Android.App;
using Android.Content;
using Android.Hardware.Camera2;
using Android.Media;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using Java.IO;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace Android.ContinuousStills
{
    public class ImageSaver : CameraCaptureSession.CaptureCallback, ImageReader.IOnImageAvailableListener
    {
        public event EventHandler<string> ImageSaved;

        private readonly Context activity;
        private readonly string baseDirectory;
        private readonly ImageReader imageReader;

        private int folderIndex = 0;
        private int index = 0;

        private int max = 1000;

        public ImageSaver(ImageReader imageReader, Context activity, string baseDirectory)
        {
            this.imageReader = imageReader;
            this.activity = activity;
            this.baseDirectory = baseDirectory;
        }

        public static bool SaveImage(File file, byte[] bytes)
        {
            if (file == null)
            {
                throw new ArgumentNullException("File cannot be null");
            }

            using var output = new FileOutputStream(file);

            try
            {
                output.Write(bytes);
                return true;
            }
            catch (System.Exception e)
            {
                System.Console.WriteLine($"Error: Failed to save image {file.AbsolutePath} with exception: {e}");
                return false;
            }
            finally
            {
                output?.Close();
            }
        }

        public static void WriteJpeg(Image image, File file)
        {
            var buffer = image.GetPlanes().First().Buffer;
            var bytes = new byte[buffer.Remaining()];
            buffer.Get(bytes);

            SaveImage(file, bytes);

            image.Close();
        }

        public void OnImageAvailable(ImageReader reader)
        {
            var image = imageReader.AcquireNextImage();

            var file = GetStillFile();
            var dir = file.Parent;

            if (!System.IO.Directory.Exists(dir))
            {
                System.IO.Directory.CreateDirectory(dir);
            }

            System.Diagnostics.Debug.WriteLine($"Information: Starting to capture: " + file.Path);

            WriteJpeg(image, file);

            ImageSaved?.Invoke(this, file.AbsolutePath);
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
                MainActivity.ShellSync($"mkdir -p \"{path}\"");
                MainActivity.ShellSync($"chmod -R 777 \"{path}\"");
            }

            index++;

            var file = new File(path, fileName);

            System.Diagnostics.Debug.WriteLine($"{file}");

            return file;
        }
    }
}