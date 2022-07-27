using Android.App;
using Android.Content;
using Android.Media;
using Android.OS;
using Android.Runtime;
using Android.Widget;
using Java.IO;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

namespace Android.ContinuousStills
{
    public class ImageSaver//  : Java.Lang.Object, ImageReader.IOnImageAvailableListener
    {
        private readonly string baseDirectory;
        private readonly ImageReader reader;
        private int folderIndex = 0;

        private int index = 0;

        private int max = 1000;

        public ImageSaver(string baseDirectory, ImageReader reader)
        {
            this.baseDirectory = baseDirectory;
            this.reader = reader;
        }

        public event EventHandler ImageFailed;

        public event EventHandler<string> ImageSaved;

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

        public async void OnImageAvailable(ImageReader reader)
        {
            System.Diagnostics.Debug.WriteLine($"---> Acquiring image");
            Image image = reader.AcquireNextImage();

            var attempts = 0;

            while (image == null)
            {
                image = reader.AcquireNextImage();
                attempts++;

                if (attempts > 20)
                {
                    System.Diagnostics.Debug.WriteLine($"+++> Unable to acquire image");
                    ImageFailed?.Invoke(this, EventArgs.Empty);
                    return;
                }

                await Task.Delay(50);
            }

            System.Diagnostics.Debug.WriteLine($"+++> Image acquired");

            var file = GetStillFile();
            var dir = file.Parent;

            if (!System.IO.Directory.Exists(dir))
            {
                System.IO.Directory.CreateDirectory(dir);
            }

            System.Diagnostics.Debug.WriteLine($"Information: Starting to capture {index}: " + file.Path);

            WriteJpeg(image, file);

            image.Close();

            ImageSaved?.Invoke(this, file.AbsolutePath);
        }

        internal void SaveImage()
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