using Android.App;
using Android.Camera;
using Android.Content;
using Android.Graphics;
using Android.Hardware;
using Android.Hardware.Camera2;
using Android.Hardware.Camera2.Params;
using Android.Media;
using Android.Net.Wifi.Aware;
using Android.OS;
using Android.Runtime;
using Android.Util;
using Android.Views;
using Android.Widget;
using AndroidX.AppCompat.App;
using Java.IO;
using Javax.Xml.Transform;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Android.ContinuousStills
{
    [Activity(Label = "@string/app_name", Theme = "@style/AppTheme", MainLauncher = true)]
    public class MainActivity : AppCompatActivity
    {
        public Handler handler;

        private string baseDirectory;
        private CameraDevice camera;
        private CameraCaptureSession captureSession;
        private int framerate = 10;
        private ImageReader imageReader;
        private ImageSaver imageSaver;

        //private bool isCancelled;
        private Size[] jpegSizes;

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

            stillCaptureBuilder = camera.CreateCaptureRequest(CameraTemplate.ZeroShutterLag);
            stillCaptureBuilder.Set(CaptureRequest.ControlCaptureIntent, (int)ControlCaptureIntent.ZeroShutterLag);
            stillCaptureBuilder.Set(CaptureRequest.JpegQuality, (sbyte)90);

            //stillCaptureBuilder.Set(CaptureRequest.EdgeMode, (int)EdgeMode.Off);
            //stillCaptureBuilder.Set(CaptureRequest.NoiseReductionMode, (int)NoiseReductionMode.Off);
            //stillCaptureBuilder.Set(CaptureRequest.ColorCorrectionAberrationMode, (int)ColorCorrectionAberrationMode.Off);
            //stillCaptureBuilder.Set(CaptureRequest.ControlAfMode, (int)ControlAFMode.ContinuousPicture);
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

            // get the camera from the camera manager with the given ID
            camera = await CameraHelpers.OpenCameraAsync("0", cameraManager);
            Characteristics = cameraManager.GetCameraCharacteristics("0");

            var fpsRangesJava = (Java.Lang.Object[])Characteristics.Get(CameraCharacteristics.ControlAeAvailableTargetFpsRanges);

            var availableFpsRanges = fpsRangesJava
                .Cast<Android.Util.Range>()
                .ToArray();

            foreach (var item in availableFpsRanges)
            {
                System.Console.WriteLine($"{item.Lower} - {item.Upper}");
            }

            System.Console.WriteLine();

            StreamMap = (StreamConfigurationMap)Characteristics.Get(CameraCharacteristics.ScalerStreamConfigurationMap);
            jpegSizes = StreamMap.GetOutputSizes((int)ImageFormatType.Jpeg);
            imageReader = ImageReader.NewInstance(jpegSizes[0].Width, jpegSizes[0].Height, ImageFormatType.Jpeg, 20);

            baseDirectory = $"{Camera.MainActivity.StorageLocation}/{Camera.MainActivity.GetStoragePoint()}/DCIM";

            imageSaver = new ImageSaver(baseDirectory);//, imageReader);
            // imageSaver.ImageFailed += ImageSaver_ImageFailed;

            imageReader.SetOnImageAvailableListener(imageSaver, handler);

            await InitializePreviewAsync(framerate, new Surface(preview.SurfaceTexture), imageReader.Surface);
        }

        private async void C_SequenceComplete(object sender, EventArgs e)
        {
            var freeExternalStorage = GetDiskSpaceRemaining();

            if (freeExternalStorage < 18759680)
            {
                //System.Diagnostics.Debug.WriteLine("Information: Filled drive!!");
                Android.Util.Log.Info("SubC", "Information: Filled drive!!");
                return;
            }

            //System.Diagnostics.Debug.WriteLine("Warning: Freespace: " + freeExternalStorage);
            Android.Util.Log.Info("SubC", "Warning: Freespace: " + freeExternalStorage);

            // imageSaver.OnImageAvailable(imageReader);

            //if (isCancelled)
            //{
            //    //System.Diagnostics.Debug.WriteLine("Cancelled!!");
            //    Android.Util.Log.Info("SubC", "Cancelled!!");

            //    imageReader = ImageReader.NewInstance(jpegSizes[0].Width, jpegSizes[0].Height, ImageFormatType.Jpeg, 20);

            //    baseDirectory = $"{Camera.MainActivity.StorageLocation}/{Camera.MainActivity.GetStoragePoint()}/DCIM";

            //    imageSaver = new ImageSaver(baseDirectory, imageReader);
            //    imageSaver.ImageFailed += ImageSaver_ImageFailed;

            //    await InitializePreviewAsync(framerate, new Surface(preview.SurfaceTexture), imageReader.Surface);

            //    isCancelled = false;

            //    StartContinuous();
            //    return;
            //}

            //Take();
            //imageSaver.SaveImage();
            // Take();
        }

        //private void ImageSaver_ImageFailed(object sender, EventArgs e)
        //{
        //    isCancelled = true;
        //}

        private async Task InitializePreviewAsync(int fps, params Surface[] surfaces)
        {
            captureSession?.Close();

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
            var builder = camera.CreateCaptureRequest(CameraTemplate.StillCapture);
            Surface previewSurface = new Surface(preview.SurfaceTexture);

            builder.AddTarget(previewSurface);
            builder.AddTarget(imageReader.Surface);
            builder.Set(CaptureRequest.ControlAeTargetFpsRange, new Android.Util.Range(fps, fps));
            builder.Set(CaptureRequest.ControlCaptureIntent, (int)ControlCaptureIntent.ZeroShutterLag);
            builder.Set(CaptureRequest.JpegQuality, (sbyte)90);

            var request = builder.Build();

            var callback = new CaptureCallback();
            // callback.CaptureComplete += C_SequenceComplete;

            captureSession.SetRepeatingRequest(request, callback, null);
        }

        private async void Picture_Click(object sender, EventArgs e)
        {
            if (picture.Text == "Start RDI")
            {
                picture.Text = "Stop RDI";
                StartContinuous();
            }
            else
            {
                picture.Text = "Start RDI";
                await InitializePreviewAsync(framerate, new Surface(preview.SurfaceTexture), imageReader.Surface);
            }
        }

        private void Take()
        {
            var c = new CaptureCallback();
            c.SequenceComplete += C_SequenceComplete;

            captureSession.Capture(request, c, handler);
        }
    }
}