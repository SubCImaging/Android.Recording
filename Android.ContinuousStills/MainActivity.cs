using Android.Annotation;
using Android.App;
using Android.Camera;
using Android.Content;
using Android.Graphics;
using Android.Hardware;
using Android.Hardware.Camera2;
using Android.Hardware.Camera2.Params;
using Android.Hardware.Usb;
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
using Veg.Mediacapture.Sdk;
using static Veg.Mediacapture.Sdk.MediaCaptureConfig;

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

        private bool isRDIing = false;

        //private bool isCancelled;
        private Size[] jpegSizes;
        private MediaCapture capturer;

        private Button picture;
        private AutoFitTextureView preview;
        private bool rdiOnStart = false;
        private CaptureRequest request;
        private CaptureRequest.Builder stillCaptureBuilder;

        private Handler stillHandler;

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

            long frameDurationNs = 1_000_000_000L / framerate;

            stillCaptureBuilder = camera.CreateCaptureRequest(CameraTemplate.StillCapture);
            //stillCaptureBuilder.Set(CaptureRequest.ControlCaptureIntent, (int)ControlCaptureIntent.ZeroShutterLag);
            stillCaptureBuilder.Set(CaptureRequest.JpegQuality, (sbyte)90);
            stillCaptureBuilder.Set(CaptureRequest.FlashMode, (int)FlashMode.Single);
            //stillCaptureBuilder.Set(CaptureRequest.ControlMode, (int)ControlMode.Off);
            stillCaptureBuilder.Set(CaptureRequest.ControlAeMode, (int)ControlAEMode.OnAlwaysFlash);
            //stillCaptureBuilder.Set(CaptureRequest.SensorSensitivity, 400);
            //builder.Set(CaptureRequest.SensorExposureTime, (long)16666667);
            //stillCaptureBuilder.Set(CaptureRequest.SensorExposureTime, (long)33_333_333);
            //stillCaptureBuilder.Set(CaptureRequest.SensorFrameDuration, frameDurationNs);

            //stillCaptureBuilder.Set(CaptureRequest.EdgeMode, (int)EdgeMode.Off);
            //stillCaptureBuilder.Set(CaptureRequest.NoiseReductionMode, (int)NoiseReductionMode.Off);
            //stillCaptureBuilder.Set(CaptureRequest.ColorCorrectionAberrationMode, (int)ColorCorrectionAberrationMode.Off);
            //stillCaptureBuilder.Set(CaptureRequest.ControlAfMode, (int)ControlAFMode.ContinuousPicture);
            stillCaptureBuilder.AddTarget(imageReader.Surface);

            request = stillCaptureBuilder.Build();

            isRDIing = true;

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

            // set up the serial communicator
            //var serial = new AndroidSerial(this, GetSystemService(UsbService) as UsbManager)
            //{
            //    Append = "\n",
            //};

            //await serial.SendAsync($"~aux0 set device:4"); // Device 4 for Aquorea LED.
            //await serial.SendAsync($"~aux0 set driverbaud:115200");

            var cameraManager = (CameraManager)GetSystemService(CameraService);

            while (!preview.IsAvailable)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(100));
            }

            var cameras = cameraManager.GetCameraIdList();

            // select the second camera if it exists because of the dev kit
            var cameraId = cameras.Length > 1 ? cameras[1] : cameras[0];

            // get the camera from the camera manager with the given ID
            camera = await CameraHelpers.OpenCameraAsync(cameraId, cameraManager);
            Characteristics = cameraManager.GetCameraCharacteristics(cameraId);

            var fpsRangesJava = (Java.Lang.Object[])Characteristics.Get(CameraCharacteristics.ControlAeAvailableTargetFpsRanges);

            var availableFpsRanges = fpsRangesJava
                .Cast<Android.Util.Range>()
                .ToArray();

            foreach (var item in availableFpsRanges)
            {
                System.Console.WriteLine($"Warning: {item.Lower} - {item.Upper}");
            }

            System.Console.WriteLine();

            StreamMap = (StreamConfigurationMap)Characteristics.Get(CameraCharacteristics.ScalerStreamConfigurationMap);
            jpegSizes = StreamMap.GetOutputSizes((int)ImageFormatType.Jpeg);

            foreach (var size in jpegSizes)
            {
                System.Console.WriteLine($"Warning Size: {size.Width}x{size.Height}");
            }

            // var jpegSize = jpegSizes.FirstOrDefault(s => s.Width == 1920 && s.Height == 1080) ?? jpegSizes[0];
            var jpegSize = jpegSizes[0];

            imageReader = ImageReader.NewInstance(jpegSize.Width, jpegSize.Height, ImageFormatType.Jpeg, 20);

            baseDirectory = $"{Camera.MainActivity.StorageLocation}/{Camera.MainActivity.GetStoragePoint()}/DCIM";

            imageSaver = new ImageSaver(baseDirectory);//, imageReader);
            // imageSaver.ImageFailed += ImageSaver_ImageFailed;

            var stillThread = new HandlerThread("StillThread");
            stillThread.Start();
            stillHandler = new Android.OS.Handler(stillThread.Looper);

            imageReader.SetOnImageAvailableListener(imageSaver, stillHandler);

            capturer = new MediaCapture(this, null);
            var mConfig = capturer.Config;
            mConfig.CaptureSource = CaptureSources.PpModeExternal.Val();
            MediaCapture.RequestPermission(this, mConfig.CaptureSource);

            // configure RTSP
            mConfig.SetVideoTimestampType(VideoTimestampType.SourceNtp);

            mConfig.Streaming = true;
            mConfig.PreviewScaleType = 0;
            mConfig.Transcoding = true;
            mConfig.CaptureMode = CaptureModes.PpModeVideo.Val();
            mConfig.StreamType = StreamerTypes.StreamTypeRtspServer.Val();
            mConfig.Url = "rtsp://@:" + 5540;
            mConfig.VideoResolution = MediaCaptureConfig.CaptureVideoResolution.VR1920x1080;
            mConfig.CaptureSource = CaptureSources.PpModeExternal.Val();
            mConfig.VideoOrientation = McOrientationLandscape;
            mConfig.VideoBitrate = 10000;
            mConfig.VideoKeyFrameInterval = 1;
            mConfig.VideoFramerate = 30;

            mConfig.SetVideoTimestampType(VideoTimestampType.SourceNtp);

            mConfig.VideoBitrateMode = MediaCaptureConfig.BitrateModeVbr;
            mConfig.VideoAVCProfile = MediaCaptureConfig.AVCProfileMain;
            mConfig.VideoFormat = MediaCaptureConfig.TypeVideoAvc;
            mConfig.VideoMaxLatency = 100;

            // start streaming
            capturer.Open(mConfig, null);
            capturer.StartStreaming();


            await InitializePreviewAsync(framerate, new Surface(preview.SurfaceTexture), imageReader.Surface, capturer.Surface);
        }

        private void C_CaptureComplete(object sender, TotalCaptureResult e)
        {
            var flashState = (int)e.Get(CaptureResult.FlashState);
            if (flashState != null)
            {
                switch (flashState)
                {
                    case (int)FlashState.Ready:
                        System.Diagnostics.Debug.WriteLine("Flash is READY");
                        break;

                    case (int)FlashState.Charging:
                        System.Diagnostics.Debug.WriteLine("Flash is CHARGING");
                        break;

                    case (int)FlashState.Fired:
                        System.Diagnostics.Debug.WriteLine("Flash just FIRED");
                        break;

                    default:
                        System.Diagnostics.Debug.WriteLine($"Flash state: {flashState}");
                        break;
                }
            }
        }

        private async void C_SequenceComplete(object sender, EventArgs e)
        {
            if (!isRDIing)
            {
                return;
            }

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

            // await Task.Delay(100);

            Take();
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

            CaptureRequest.Builder builder;

            if (rdiOnStart)
            {
                builder = camera.CreateCaptureRequest(CameraTemplate.StillCapture);

                builder.AddTarget(imageReader.Surface);

                // builder.Set(CaptureRequest.ControlCaptureIntent, (int)ControlCaptureIntent.ZeroShutterLag);
                builder.Set(CaptureRequest.ControlCaptureIntent, (int)ControlCaptureIntent.StillCapture);
                builder.Set(CaptureRequest.JpegQuality, (sbyte)90);
                builder.Set(CaptureRequest.FlashMode, (int)FlashMode.Single);
            }
            else
            {
                builder = camera.CreateCaptureRequest(CameraTemplate.Preview);
            }

            long frameDurationNs = 1_000_000_000L / framerate;

            var previewSurface = new Surface(preview.SurfaceTexture);

            

            builder.AddTarget(previewSurface);
            builder.AddTarget(capturer.Surface);
            // builder.Set(CaptureRequest.ControlAeTargetFpsRange, new Android.Util.Range(fps, fps));
            builder.Set(CaptureRequest.ControlMode, (int)ControlMode.Off);
            builder.Set(CaptureRequest.ControlAeMode, (int)ControlAEMode.Off);
            builder.Set(CaptureRequest.SensorSensitivity, 400);
            builder.Set(CaptureRequest.SensorExposureTime, (long)8_333_333);
            // builder.Set(CaptureRequest.SensorExposureTime, (long)16_666_667);
            // builder.Set(CaptureRequest.SensorExposureTime, (long)33_333_333);
            builder.Set(CaptureRequest.SensorFrameDuration, frameDurationNs);

            var request = builder.Build();

            var callback = new CaptureCallback();

            var sessionThread = new HandlerThread("SessionThread");
            sessionThread.Start();
            var sessionHandler = new Handler(sessionThread.Looper);

            captureSession.SetRepeatingRequest(request, callback, sessionHandler);
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
                isRDIing = false;

                picture.Text = "Start RDI";
                // await InitializePreviewAsync(framerate, new Surface(preview.SurfaceTexture), imageReader.Surface);
            }
        }

        private void Take()
        {
            var c = new CaptureCallback();
            c.SequenceComplete += C_SequenceComplete;
            c.CaptureComplete += C_CaptureComplete;

            captureSession.Capture(request, c, stillHandler);
        }
    }
}