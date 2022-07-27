using Android.App;
using Android.Hardware.Camera2;
using Android.OS;
using Android.Runtime;
using AndroidX.AppCompat.App;
using System.Threading.Tasks;
using System;
using Android.Views;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Android.Widget;
using Android.Media;
using Android.Content;
using Java.IO;
using Android.Hardware;
using Android.Hardware.Camera2.Params;
using Android.Graphics;
using System.Linq;
using Javax.Xml.Transform;

namespace Android.ContinuousStills
{
    [Activity(Label = "@string/app_name", Theme = "@style/AppTheme", MainLauncher = true)]
    public class MainActivity : AppCompatActivity
    {
        public Handler handler;

        /// <summary>
        /// The location of the SD card in the system.
        /// </summary>
        private const string StorageLocation = "/mnt/expand";

        private string baseDirectory;
        private CameraDevice camera;
        private CameraCaptureSession captureSession;
        private ImageReader imageReader;
        private ImageSaver imageSaver;

        private Button picture;
        private AutoFitTextureView preview;
        private CaptureRequest request;
        private CaptureRequest.Builder stillCaptureBuilder;

        /// <summary>
        /// Gets all characteristics of the camera system.
        /// </summary>
        public CameraCharacteristics Characteristics { get; private set; }

        /// <summary>
        /// Gets stream map for get resolutions for stills
        /// </summary>
        public StreamConfigurationMap StreamMap { get; private set; }

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

            captureSession.Capture(request, new CaptureCallback(), handler);
        }

        protected override async void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            Xamarin.Essentials.Platform.Init(this, savedInstanceState);
            // Set our view from the "main" layout resource
            SetContentView(Resource.Layout.activity_main);
            // set up the camera
            // get the preview to display video
            preview = FindViewById<AutoFitTextureView>(Resource.Id.preview);
            picture = FindViewById<Button>(Resource.Id.picture);
            picture.Click += Picture_Click;

            var cameraManager = (CameraManager)GetSystemService(CameraService);

            while (!preview.IsAvailable)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(100));
            }

            // get the camera from the camera manager with the given ID
            camera = await OpenCameraAsync("0", cameraManager);
            Characteristics = cameraManager.GetCameraCharacteristics("0");
            StreamMap = (StreamConfigurationMap)Characteristics.Get(CameraCharacteristics.ScalerStreamConfigurationMap);
            var jpegSizes = StreamMap.GetOutputSizes((int)ImageFormatType.Jpeg);
            imageReader = ImageReader.NewInstance(jpegSizes[0].Width, jpegSizes[0].Height, ImageFormatType.Jpeg, 2);

            baseDirectory = $"{ StorageLocation}/{ GetStoragePoint()}";

            imageSaver = new ImageSaver(imageReader, this, baseDirectory);

            imageSaver.ImageSaved += ImageSaver_ImageSaved;

            imageReader.SetOnImageAvailableListener(imageSaver, handler);
            await InitializePreviewAsync(new Surface(preview.SurfaceTexture), imageReader.Surface);
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

        private void ImageSaver_ImageSaved(object sender, string e)
        {
            var t = Toast.MakeText(this, $"{e} saved", ToastLength.Short);
            t.Show();

            var freeExternalStorage = GetDiskSpaceRemaining();

            if (freeExternalStorage < 18759680)
            {
                System.Diagnostics.Debug.WriteLine("Information: Filled drive!!");
                return;
            }

            System.Diagnostics.Debug.WriteLine("Warning: Freespace: " + freeExternalStorage);

            captureSession.Capture(request, new CaptureCallback(), handler);
        }

        private async Task InitializePreviewAsync(params Surface[] surfaces)
        {
            var tcs = new TaskCompletionSource<bool>();

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
            var builder = camera.CreateCaptureRequest(CameraTemplate.Preview);
            Surface previewSurface = new Surface(preview.SurfaceTexture);

            builder.AddTarget(previewSurface);

            var request = builder.Build();

            captureSession.SetRepeatingRequest(request, null, null);
        }

        private void Picture_Click(object sender, EventArgs e)
        {
            StartContinuous();
        }
    }
}