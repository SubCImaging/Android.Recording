using Android.App;
using Android.Hardware.Camera2;
using Android.OS;
using Android.Runtime;
using AndroidX.AppCompat.App;
using System.Threading.Tasks;
using System;
using System.Timers;
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
using Android.Util;
using Android.Camera;

namespace Android.ContinuousStills
{
    [Activity(Label = "@string/app_name", Theme = "@style/AppTheme", MainLauncher = true)]
    public class MainActivity : AppCompatActivity
    {
        private HandlerThread backgroundThread;
        private string baseDirectory;
        private CameraDevice camera;
        private CameraCaptureSession captureSession;
        private Handler handler;
        private ImageReader imageReader;
        private ImageSaver imageSaver;
        private bool isCancelled;
        private Size[] jpegSizes;
        private Button picture;
        private AutoFitTextureView preview;
        private CaptureRequest request;
        private Handler sessionCallbackhandler;
        private HandlerThread sessionCallbackThread;
        private CaptureRequest.Builder stillCaptureBuilder;
        private System.Timers.Timer timer = new Timer(5000);

        public event EventHandler ImageCapture;

        /// <summary>
        /// Gets all characteristics of the camera system.
        /// </summary>
        public CameraCharacteristics Characteristics { get; private set; }

        /// <summary>
        /// Gets stream map for get resolutions for stills
        /// </summary>
        public StreamConfigurationMap StreamMap { get; private set; }

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

        public Task<CameraDevice> OpenCameraAsync(string cameraId, CameraManager cameraManager)
        {
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
        public async Task<CameraDevice> OpenCameraAsync(string cameraId, CameraManager cameraManager, CameraStateCallback stateCallback, Android.OS.Handler backgroundHandler)
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
            // set up the camera
            // get the preview to display video
            preview = FindViewById<AutoFitTextureView>(Resource.Id.Preview);
            picture = FindViewById<Button>(Resource.Id.picture);
            picture.Click += Picture_Click;

            var cameraManager = (CameraManager)GetSystemService(CameraService);

            while (!preview.IsAvailable)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(100));
            }

            backgroundThread = new HandlerThread("CameraBackground");
            backgroundThread.Start();
            handler = new Android.OS.Handler(backgroundThread.Looper);

            // get the camera from the camera manager with the given ID
            camera = await OpenCameraAsync("0", cameraManager);
            Characteristics = cameraManager.GetCameraCharacteristics("0");
            StreamMap = (StreamConfigurationMap)Characteristics.Get(CameraCharacteristics.ScalerStreamConfigurationMap);
            jpegSizes = StreamMap.GetOutputSizes((int)ImageFormatType.Jpeg);
            imageReader = ImageReader.NewInstance(jpegSizes[0].Width, jpegSizes[0].Height, ImageFormatType.Jpeg, 20);

            baseDirectory = $"{Camera.MainActivity.StorageLocation}/{Camera.MainActivity.GetStoragePoint()}";
            Android.Util.Log.Warn("SubC", $"baseDirectory = {baseDirectory}");

            imageSaver = new ImageSaver(baseDirectory, imageReader);
            imageReader.SetOnImageAvailableListener(imageSaver, handler);

            timer.Elapsed += Timer_Elapsed;
            imageSaver.ImageCaptured += ImageSaver_ImageCaptured;

            CreateCaptureSession(new Surface(preview.SurfaceTexture), imageReader.Surface);
        }

        private void CreateCaptureSession(params Surface[] surfaces)
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

            sessionCallbackThread = new HandlerThread("SessionCallbackThread");
            sessionCallbackThread.Start();
            sessionCallbackhandler = new Android.OS.Handler(sessionCallbackThread.Looper);

            var sessionCallback = new CameraSessionCallback();
            sessionCallback.Configured += configuredHandler;
            sessionCallback.ConfigureFailed += failedHandler;

            try
            {
                camera.CreateCaptureSession(surfaces, sessionCallback, sessionCallbackhandler);
            }
            catch (Exception e)
            {
                throw e;
            }
        }

        private void ImageSaver_ImageCaptured(object sender, EventArgs e)
        {
            timer.Stop();
            timer.Start();
        }

        private async Task InitializePreviewAsync(params Surface[] surfaces)
        {
            captureSession?.Close();

            var builder = camera.CreateCaptureRequest(CameraTemplate.Preview);
            Surface previewSurface = new Surface(preview.SurfaceTexture);

            builder.AddTarget(previewSurface);

            var request = builder.Build();

            captureSession.SetRepeatingRequest(request, null, null);
        }

        private void Picture_Click(object sender, EventArgs e)
        {
            Android.Util.Log.Warn("SubC", "start....");
            StartContinuous();
        }

        private void Take()
        {
            Android.Util.Log.Warn("SubC", "take....");
            captureSession.Capture(request, new CaptureCallback(), handler);

            timer.Start();

            // captureSession.SetRepeatingRequest(request, c, handler);
        }

        private void Timer_Elapsed(object sender, ElapsedEventArgs e)
        {
            Android.Util.Log.Info("SubCTimer", "Picture failed restarting");
            Take();
        }
    }
}