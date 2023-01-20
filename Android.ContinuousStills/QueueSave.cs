//using Android.Media;
//using Android.OS;
//using Java.IO;
//using System;
//using System.Collections.Concurrent;
//using System.Linq;

//namespace Android.ContinuousStills
//{
//    public class QueueSave : HandlerThread
//    {
//        private readonly ConcurrentQueue<(Image, File)> images;
//        private string baseDirectory;

//        public QueueSave(string name, ConcurrentQueue<(Image, File)> images) : base(name)
//        {
//            this.images = images;
//        }

//        public double GetDiskSpaceRemaining()
//        {
//            baseDirectory = $"{Camera.MainActivity.StorageLocation}/{Camera.MainActivity.GetStoragePoint()}";

//            var fs = new StatFs(baseDirectory);
//            var free = fs.AvailableBlocksLong * fs.BlockSizeLong;
//            return free;
//        }

//        public override void Run()
//        {
//            Android.Util.Log.Info("SubC", "QueueSave::Run");
//            while (true)
//            {
//                var found = images.TryDequeue(out var imageTuple);
//                if (!found)
//                {
//                    // Android.Util.Log.Info("SubC", "not data in queue, sleep 100ms");
//                    Sleep(100);
//                    continue;
//                }

//                var image = imageTuple.Item1;
//                var file = imageTuple.Item2;

//                WriteJpeg(image, file);
//                image.Close();
//            }
//        }

//        private bool SaveImage(File file, byte[] bytes)
//        {
//            if (file == null)
//            {
//                throw new ArgumentNullException("File cannot be null");
//            }

//            using var output = new Java.IO.FileOutputStream(file);
//            try
//            {
//                output.Write(bytes);
//                return true;
//            }
//            catch (System.Exception e)
//            {
//                //System.Console.WriteLine($"Error: Failed to save image {file.AbsolutePath} with exception: {e}");
//                Android.Util.Log.Info("SubC", $"Error: Failed to save image {file} with exception: {e}");

//                return false;
//            }
//            finally
//            {
//                output?.Close();
//            }
//            return false;
//        }

//        private void WriteJpeg(Image image, File file)
//        {
//            var buffer = image.GetPlanes().First().Buffer;
//            var bytes = new byte[buffer.Remaining()];
//            buffer.Get(bytes);

//            Android.Util.Log.Info("SubC", $"Saving: {file}");

//            var freeExternalStorage = GetDiskSpaceRemaining();

//            if (freeExternalStorage < 18759680)
//            {
//                //System.Diagnostics.Debug.WriteLine("Information: Filled drive!!");
//                Android.Util.Log.Info("SubC", "Information: Filled drive!!");
//                throw new FileNotFoundException("filled!!");
//            }
//            if (freeExternalStorage > 18759680)
//            {
//                //System.Diagnostics.Debug.WriteLine("Warning: Freespace: " + freeExternalStorage);
//                Android.Util.Log.Info("SubC", "Warning: Freespace: " + freeExternalStorage);

//                SaveImage(file, bytes);
//            }
//        }
//    }
//}