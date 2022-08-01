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

        public event EventHandler<string> ImageSaved;

        public async void OnImageAvailable(ImageReader reader)
        {
            System.Diagnostics.Debug.WriteLine($"---> Acquiring image");
            Image image = reader.AcquireNextImage();

            var attempts = 0;

            while (image == null)
            {
                attempts++;

                if (attempts > 20)
                {
                    System.Diagnostics.Debug.WriteLine($"+++> Unable to acquire image");

                    if (queue.Count > 0)
                    {
                        System.Diagnostics.Debug.WriteLine("---> Running");
                        handler.Run();
                    }

                    break;
                }

                await Task.Delay(50);

                image = reader.AcquireNextImage();
            }

            if (image == null)
            {
                failed++;

                if (failed > 20)
                {
                    failed = 0;
                    ImageFailed?.Invoke(this, EventArgs.Empty);
                }

                return;
            }

            System.Diagnostics.Debug.WriteLine($"+++> Image acquired");

            var file = GetStillFile();
            var dir = file.Parent;

            if (!System.IO.Directory.Exists(dir))
            {
                System.IO.Directory.CreateDirectory(dir);
            }

            System.Diagnostics.Debug.WriteLine($"Information: Starting to capture {index}: " + file.Path);

            queue.Enqueue((image, file));

            if (queue.Count >= 10)
            {
                System.Diagnostics.Debug.WriteLine("---> Running");
                handler.Run();
            }

            ImageSaved?.Invoke(this, file.AbsolutePath);
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

            System.Diagnostics.Debug.WriteLine($"{file}");

            return file;
        }
    }
}