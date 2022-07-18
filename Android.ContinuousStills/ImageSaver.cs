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
    public class ImageSaver : CameraCaptureSession.CaptureCallback
    {
        /// <summary>
        /// The location of the SD card in the system.
        /// </summary>
        private const string StorageLocation = "/mnt/expand";

        private readonly Context activity;
        private readonly ImageReader imageReader;

        public ImageSaver(ImageReader imageReader, Context activity)
        {
            this.imageReader = imageReader;
            this.activity = activity;
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

        /// <summary>
        /// Runs a shell command on the Rayfin.
        /// </summary>
        /// <param name="command">The command you wish to execute.</param>
        /// <param name="timeout">
        /// The maximum time the command is allowed to run before timing out.
        /// </param>
        /// <returns>Anything that comes from stdout.</returns>
        public static string ShellSync(string command, int timeout = 0)
        {
            try
            {
                // Run the command
                var log = new System.Text.StringBuilder();
                var process = Java.Lang.Runtime.GetRuntime().Exec(new[] { "su", "-c", command });
                var bufferedReader = new BufferedReader(
                new InputStreamReader(process.InputStream));

                // Grab the results
                if (timeout > 0)
                {
                    process.Wait(timeout);
                    return string.Empty;
                }

                string line;

                while ((line = bufferedReader.ReadLine()) != null)
                {
                    log.AppendLine(line);
                }
                return log.ToString();
            }
            catch
            {
                return string.Empty;
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

        public override void OnCaptureCompleted(CameraCaptureSession session, CaptureRequest request, TotalCaptureResult result)
        {
            var image = imageReader.AcquireLatestImage();

            var file = GetStillFile();

            WriteJpeg(image, file);

            var toast = Toast.MakeText(activity, file.AbsolutePath + " saved!", ToastLength.Short);
            toast.Show();

            base.OnCaptureCompleted(session, request, result);
        }

        /// <summary>
        /// Gets the <see cref="Guid" /> that represents the SD card in the Rayfin.
        /// </summary>
        /// <returns>The <see cref="Guid" /> that represents the SD card in the Rayfin.</returns>
        private static Guid GetStoragePoint()
        {
            var folders = ShellSync($@"ls {StorageLocation}").Split('\n');

            foreach (string folder in folders)
            {
                if (Guid.TryParse(folder, out Guid result))
                {
                    return result;
                }
            }

            throw new System.IO.IOException("Could not find storage mount");
        }

        private File GetStillFile()
        {
            string fileName = "still-" + DateTime.Now.ToString("yyMMdd-hhmmss.fff") + ".png"; //new filenamed based on date time

            var dir = $"{StorageLocation}/{GetStoragePoint()}/Stills/";

            var file = new File(dir, fileName);

            System.Diagnostics.Debug.WriteLine($"{file}");

            return file;
        }
    }
}