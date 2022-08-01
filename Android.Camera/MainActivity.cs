using Android.App;
using Android.Graphics;
using Android.Hardware.Camera2;
using Android.Hardware.Camera2.Params;
using Android.Media;
using Android.OS;
using Android.Runtime;
using Android.Util;
using Android.Views;
using Android.Widget;
using AndroidX.AppCompat.App;
using Java.IO;
using System;
using System.Threading.Tasks;

namespace Android.Camera
{
    [Activity(Label = "@string/app_name", Theme = "@style/AppTheme", MainLauncher = true)]
    public class MainActivity : AppCompatActivity, MediaRecorder.IOnInfoListener
    {
        /// <summary>
        /// The location of the SD card in the system.
        /// </summary>
        public const string StorageLocation = "/mnt/expand";

        public CameraDevice camera;
        public CameraCaptureSession captureSession;
        public Handler handler;
        public ImageSaver imageSaver;
        public AutoFitTextureView preview;
        public CaptureRequest request;
        private string baseDirectory;
        private Button capture;
        private ImageReader imageReader;
        private bool isCancelled;
        private Size[] jpegSizes;
        private Button record;
        private MediaRecorder recorder = new MediaRecorder();
        private CaptureRequest.Builder stillCaptureBuilder;

        /// <summary>
        /// Gets all characteristics of the camera system.
        /// </summary>
        public CameraCharacteristics Characteristics { get; private set; }

        /// <summary>
        /// Gets stream map for get resolutions for stills
        /// </summary>
        public StreamConfigurationMap StreamMap { get; private set; }

        /// <summary>
        /// Gets the <see cref="Guid" /> that represents the SD card in the Rayfin.
        /// </summary>
        /// <returns>The <see cref="Guid" /> that represents the SD card in the Rayfin.</returns>
        public static Guid GetStoragePoint()
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

        public static Task<CameraDevice> OpenCameraAsync(string cameraId, CameraManager cameraManager)
        {
            // create a new background thread to open the camera on
            var backgroundThread = new HandlerThread("CameraBackground");
            backgroundThread.Start();
            var handler = new Android.OS.Handler(backgroundThread.Looper);

            return OpenCameraAsync(cameraId, cameraManager, new CameraStateCallback(), handler);
        }

        /// <summary>
        /// Gets a <see cref="CameraDevice"/> from a <see cref="Context"/> and a cameraId.
        /// </summary>
        /// <param name="cameraId">Camera Id.</param>
        /// <param name="cameraManager">A <see cref="CameraManager"/>.</param>
        /// <param name="stateCallback">A <see cref="CameraStateCallback"/>.</param>
        /// <param name="backgroundHandler">A background handler.</param>
        /// <returns>A task of CameraDevice.</returns>
        public static async Task<CameraDevice> OpenCameraAsync(string cameraId, CameraManager cameraManager, CameraStateCallback stateCallback, Android.OS.Handler backgroundHandler)
        {
            var tcs = new TaskCompletionSource<CameraDevice>();
            var handler = new EventHandler<CameraDevice>((s, e) =>
            {
                tcs.SetResult(e);
            });

            stateCallback.Opened += handler;

            try
            {
                cameraManager.OpenCamera(cameraId, stateCallback, backgroundHandler);
            }
            catch (Java.IO.IOException e)
            {
                throw new System.Exception(e.ToString());
            }

            var c = await tcs.Task;

            stateCallback.Opened -= handler;

            return c;
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

        public double GetDiskSpaceRemaining()
        {
            var fs = new StatFs(baseDirectory);
            var free = fs.AvailableBlocksLong * fs.BlockSizeLong;
            return free;
        }

        public async void OnInfo(MediaRecorder mr, [GeneratedEnum] MediaRecorderInfo what, int extra)
        {
            System.Diagnostics.Debug.WriteLine($"Warning: " + what);

            if (what == MediaRecorderInfo.MaxDurationReached)
            {
                recorder.Stop();

                await StartRecordingAsync();
            }
            if (what == MediaRecorderInfo.MaxFilesizeReached)
            {
                var toast = Toast.MakeText(this, "Cannot record more than 4 GB!!", ToastLength.Long);
                toast.Show();
                System.Console.WriteLine($"Error: Cannot record more than 20 GB!!");
            }
        }

        public override void OnRequestPermissionsResult(int requestCode, string[] permissions, [GeneratedEnum] Android.Content.PM.Permission[] grantResults)
        {
            Xamarin.Essentials.Platform.OnRequestPermissionsResult(requestCode, permissions, grantResults);
            if (requestCode == 1)
            {
                // show premission window
            }
            base.OnRequestPermissionsResult(requestCode, permissions, grantResults);
        }

        public void StartContinuous()
        {
            if (camera == null)
            {
                return;
            }

            stillCaptureBuilder = camera.CreateCaptureRequest(CameraTemplate.StillCapture);
            stillCaptureBuilder.Set(CaptureRequest.ControlCaptureIntent, (int)ControlCaptureIntent.ZeroShutterLag);
            stillCaptureBuilder.Set(CaptureRequest.EdgeMode, (int)EdgeMode.Off);
            stillCaptureBuilder.Set(CaptureRequest.NoiseReductionMode, (int)NoiseReductionMode.Off);
            stillCaptureBuilder.Set(CaptureRequest.ColorCorrectionAberrationMode, (int)ColorCorrectionAberrationMode.Off);
            stillCaptureBuilder.Set(CaptureRequest.ControlAfMode, (int)ControlAFMode.ContinuousPicture);
            stillCaptureBuilder.AddTarget(imageReader.Surface);

            request = stillCaptureBuilder.Build();

            Take();
        }

        protected override async void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            Xamarin.Essentials.Platform.Init(this, savedInstanceState);
            // Set our view from the "main" layout resource

            SetContentView(Resource.Layout.activity_main);
            preview = FindViewById<AutoFitTextureView>(Resource.Id.Preview);
            record = FindViewById<Button>(Resource.Id.video);
            capture = FindViewById<Button>(Resource.Id.picture);

            capture.Click += Capture_Click;
            record.Click += Record_Click;
            var cameraManager = (CameraManager)GetSystemService(CameraService);

            while (!preview.IsAvailable)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(100));
            }

            // get the camera from the camera manager with the given ID
            camera = await OpenCameraAsync("0", cameraManager);

            Characteristics = cameraManager.GetCameraCharacteristics("0");
            StreamMap = (StreamConfigurationMap)Characteristics.Get(CameraCharacteristics.ScalerStreamConfigurationMap);
            jpegSizes = StreamMap.GetOutputSizes((int)ImageFormatType.Jpeg);
            imageReader = ImageReader.NewInstance(jpegSizes[0].Width, jpegSizes[0].Height, ImageFormatType.Jpeg, 20);

            baseDirectory = $"{ StorageLocation}/{GetStoragePoint()}";

            imageSaver = new ImageSaver(baseDirectory, imageReader);
            imageSaver.ImageFailed += ImageSaver_ImageFailed;

            await InitializePreviewAsync(CameraTemplate.Preview, new Surface(preview.SurfaceTexture));
        }

        private async void C_SequenceComplete(object sender, EventArgs e)
        {
            var freeExternalStorage = GetDiskSpaceRemaining();

            if (freeExternalStorage < 18759680)
            {
                System.Diagnostics.Debug.WriteLine("Information: Filled drive!!");
                return;
            }

            System.Diagnostics.Debug.WriteLine("Warning: Freespace: " + freeExternalStorage);

            if (isCancelled)
            {
                System.Diagnostics.Debug.WriteLine("Cancelled!!");
                imageReader = ImageReader.NewInstance(jpegSizes[0].Width, jpegSizes[0].Height, ImageFormatType.Jpeg, 20);

                baseDirectory = $"{ StorageLocation}/{GetStoragePoint()}";

                imageSaver = new ImageSaver(baseDirectory, imageReader);
                imageSaver.ImageFailed += ImageSaver_ImageFailed;

                await InitializePreviewAsync(CameraTemplate.Preview, new Surface(preview.SurfaceTexture), imageReader.Surface);

                isCancelled = false;

                StartContinuous();
                return;
            }

            Take();
            imageSaver.SaveImage();
        }

        private async void Capture_Click(object sender, EventArgs e)
        {
            await InitializePreviewAsync(CameraTemplate.Preview, new Surface(preview.SurfaceTexture), imageReader.Surface);
            StartContinuous();
        }

        private File GetVideoFile()
        {
            string fileName = "video-" + DateTime.Now.ToString("yyMMdd-hhmmss") + ".mp4"; //new filenamed based on date time

            var dir = $"{StorageLocation}/{GetStoragePoint()}/Videos/";
            //string incindex = $"{dir}/vid{index}";
            //if (index >= max)
            //{
            //    ShellSync($"mkdir -p \"{incindex}\"");
            //    ShellSync($"chmod -R 777 \"{dir}/vid{incindex}\"");
            //}
            //index++;
            var file = new File(dir, fileName); // change "dir" into "incindex" to generate a folder each time

            System.Diagnostics.Debug.WriteLine($"{file}");
            return file;
        }

        private void ImageSaver_ImageFailed(object sender, EventArgs e)
        {
            isCancelled = true;
        }

        private async Task InitializePreviewAsync(CameraTemplate template, params Surface[] surfaces)
        {
            //captureSession?.Close();

            var tcs = new TaskCompletionSource<bool>();
            captureSession = null;
            var failedHandler = new EventHandler<CameraCaptureSession>((s, e) =>
            {
                captureSession = e;
                tcs.TrySetResult(false);
            });

            var configuredHandler = new EventHandler<CameraCaptureSession>((s, e) =>
            {
                captureSession = e;
                tcs.TrySetResult(true);
            });

            var sessionCallbackThread = new HandlerThread("SessionCallbackThread");
            sessionCallbackThread.Start();
            var handler = new Android.OS.Handler(sessionCallbackThread.Looper);

            var sessionCallback = new CameraSessionCallback();
            sessionCallback.Configured += configuredHandler;
            sessionCallback.ConfigureFailed += failedHandler;

            try
            {
                camera.CreateCaptureSession(surfaces, sessionCallback, handler);
            }
            catch (Exception e)
            {
                tcs.TrySetResult(false);
                throw e;
            }

            await tcs.Task;
            var builder = camera.CreateCaptureRequest(template);
            foreach (var surface in surfaces)
            {
                builder.AddTarget(surface);
            }
            request = builder.Build();

            captureSession.SetRepeatingRequest(request, null, null);
        }

        private async void Record_Click(object sender, EventArgs e)
        {
            if (record.Text == "record")
            {
                record.Text = "stop";
                await StartRecordingAsync();
            }
            else
            {
                record.Text = "record";
                recorder.Stop();
                await InitializePreviewAsync(CameraTemplate.Preview, new Surface(preview.SurfaceTexture));
            }
        }

        private void Recorder_Error(object sender, MediaRecorder.ErrorEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine("===> " + e.What);
        }

        private async Task StartRecordingAsync()
        {
            //captureSession.Close();

            recorder.SetVideoSource(VideoSource.Surface);
            recorder.SetOutputFormat(OutputFormat.Mpeg4);

            // get the file
            var file = GetVideoFile().AbsoluteFile;

            var dir = file.Parent;

            if (!System.IO.Directory.Exists(dir))
            {
                System.IO.Directory.CreateDirectory(dir);
            }

            System.Diagnostics.Debug.WriteLine($"Error: Starting to record : " + file.Path);

            recorder.SetOutputFile(file.Path);
            recorder.SetMaxDuration((int)TimeSpan.FromMinutes(10).TotalMilliseconds);
            recorder.SetOnInfoListener(this);
            recorder.SetVideoEncodingBitRate(100_000_000);
            recorder.SetVideoFrameRate(30);
            recorder.SetVideoSize(3840, 2160);
            recorder.SetMaxFileSize(21_474_836_480);
            recorder.SetVideoEncoder(VideoEncoder.H264);
            recorder.Prepare();

            await InitializePreviewAsync(CameraTemplate.Record, new Surface(preview.SurfaceTexture), recorder.Surface);
            recorder.Error += Recorder_Error;
            recorder.Start();
        }

        private void Take()
        {
            var c = new CaptureCallback();
            c.SequenceComplete += C_SequenceComplete;

            captureSession.Capture(request, c, handler);
        }
    }
}