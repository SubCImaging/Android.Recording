using Android.Media;
using Android.OS;
using Java.IO;
using System;
using System.Collections.Concurrent;
using System.Linq;

namespace Android.ContinuousStills
{
    public class QueueSave : HandlerThread
    {
        private readonly ConcurrentQueue<(Image, File)> images;

        public QueueSave(string name, ConcurrentQueue<(Image, File)> images) : base(name)
        {
            this.images = images;
        }

        public static bool SaveImage(File file, byte[] bytes)
        {
            if (file == null)
            {
                throw new ArgumentNullException("File cannot be null");
            }

            var output = new FileOutputStream(file);

            try
            {
                output.Write(bytes);
                return true;
            }
            catch (System.Exception e)
            {
                //System.Console.WriteLine($"Error: Failed to save image {file.AbsolutePath} with exception: {e}");
                Android.Util.Log.Info("SubC", $"Error: Failed to save image {file.AbsolutePath} with exception: {e}");

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

            //System.Diagnostics.Debug.WriteLine($"Saving: {file}");
            Android.Util.Log.Info("SubC", $"Saving: {file}");

            SaveImage(file, bytes);

            image.Close();
        }

        public override void Run()
        {
            while (!images.IsEmpty)
            {
                images.TryDequeue(out var imageTuple);

                var image = imageTuple.Item1;
                var file = imageTuple.Item2;

                WriteJpeg(image, file);
            }
        }
    }
}
