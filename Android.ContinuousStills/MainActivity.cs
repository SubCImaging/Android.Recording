﻿using Android.App;
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
using Android.Util;
using Android.Camera;
using System.Timers;
using System.Security.Cryptography.X509Certificates;
using System.IO;
using SubCTools.Droid.Tools;
using Java.Time.Temporal;
using Android.Content.Res;

namespace Android.ContinuousStills
{
    [Activity(Label = "@string/app_name", Theme = "@style/AppTheme", MainLauncher = true)]
    public class MainActivity : AppCompatActivity
    {
        /// <summary>
        /// The location of the SD card in the system.
        /// </summary>
        public const string StorageLocation = "/mnt/expand";

        public Handler handler;

        // private ContinuousStillsManager continuous = new ContinuousStillsManager();
        public ImageReader imageReader;

        public AutoFitTextureView preview;

        private readonly Timer stopTimer = new Timer()
        {
            Interval = TimeSpan.FromHours(6).TotalMilliseconds,
            AutoReset = false,
        };

        //private readonly Timer tempTimer = new Timer(TimeSpan.FromMinutes(1).TotalMilliseconds);
        private string baseDirectory;

        private ContinuousStillsManager continuous;
        private double greenImages;
        private ImageSaver imageSaver;
        private bool isCancelled;
        private Size[] jpegSizes;
        private Button picture;
        private CaptureRequest request;
        private DateTime startTime;
        private CaptureRequest.Builder stillCaptureBuilder;
        private Button stop;
        private string tempDirectory;
        private double totalimages;
        public CameraDevice camera { get; set; }

        public CameraCaptureSession captureSession { get; set; }

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

        /// <summary>
        /// Gets the tempature of a given thermal_zone.
        /// </summary>
        /// <param name="zone">The thermal zone you want to check.</param>
        /// <returns>The tempature of a given thermal_zone.</returns>
        public string GetThermalZoneTemperature(string zone)
        {
            var temperature = ShellSync($"cat sys/class/thermal/thermal_zone{zone}/temp").TrimEnd();
            return temperature;
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

        protected override async void OnCreate(Bundle savedInstanceState)
        {
            Android.Util.Log.Warn("SubC", $"Continuous Stills Started!");

            base.OnCreate(savedInstanceState);
            Xamarin.Essentials.Platform.Init(this, savedInstanceState);
            // Set our view from the "main" layout resource
            SetContentView(Resource.Layout.activity_main);

            stopTimer.Elapsed += StopTimer_Elapsed;

            // set up the camera
            // get the preview to display video
            var result = ShellSync($@"ls").Split('\n');
            result = ShellSync($@"ls /mnt").Split('\n');
            result = ShellSync($@"ls /mnt/expand").Split('\n');

            preview = FindViewById<AutoFitTextureView>(Resource.Id.Preview);
            stop = FindViewById<Button>(Resource.Id.stop);
            stop.Click += Stop_Click;
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
            jpegSizes = StreamMap.GetOutputSizes((int)ImageFormatType.Jpeg);
            imageReader = ImageReader.NewInstance(jpegSizes[0].Width, jpegSizes[0].Height, ImageFormatType.Jpeg, 20);

            baseDirectory = $"{StorageLocation}/{GetStoragePoint()}";

            tempDirectory = $"{baseDirectory}/Temp/";

            ShellSync($"mkdir -p \"{tempDirectory}\"");
            ShellSync($"chmod -R 777 \"{tempDirectory}\"");

            Android.Util.Log.Warn("SubC", $"baseDirectory = {baseDirectory}");
            imageSaver = new ImageSaver(baseDirectory, imageReader);
            imageSaver.ImageFailed += ImageSaver_ImageFailed;
            imageSaver.ImageSaved += ImageSaver_ImageSaved;
            imageSaver.DriveFull += ImageSaver_DriveFull;

            imageReader.SetOnImageAvailableListener(imageSaver, handler);
            var previewSurface = new Surface(preview.SurfaceTexture);
            var imgReader = imageReader.Surface;
            await InitializePreviewAsync(previewSurface, imgReader);
            continuous = new ContinuousStillsManager(previewSurface, camera, imgReader, captureSession, this);
        }

        private void ImageSaver_DriveFull(object sender, EventArgs e)
        {
            continuous.Stop();
        }

        private void ImageSaver_ImageFailed(object sender, EventArgs e)
        {
            isCancelled = true;
        }

        private async void ImageSaver_ImageSaved(object sender, string e)
        {
            Android.Util.Log.Warn("SubC", $"image saved: {e}");

            var isGreen = GreenPictureDetector.ImageIsGreen(new FileInfo(e));

            Android.Util.Log.Warn("SubC", $"image: {e} is green {isGreen}");

            totalimages++;

            if (isGreen)
            {
                greenImages++;
            }

            var temps = ShellSync("cat /sys/class/thermal/thermal_zone*/temp").Replace("\n", ",");

            var per = Math.Round(greenImages / totalimages * 100, 2);

            var log = $"{DateTime.Now},{e},{isGreen},{greenImages},{totalimages},{per},{Math.Round((DateTime.Now - startTime).TotalHours, 2)},{GetDiskSpaceRemaining() - 18759680},{temps}";
            Android.Util.Log.Warn("SubC", log);

            using var t = System.IO.File.AppendText(tempDirectory + "temp.csv");
            t.WriteLine(log);

            if (!isGreen)
            {
                return;
            }

            continuous.Stop();

            using var greenLog = System.IO.File.AppendText(tempDirectory + $"green.log");
            greenLog.WriteLine(log);

            _ = Task.Run(async () =>
            {
                await Task.Delay(TimeSpan.FromSeconds(10));
                continuous.Start();
            });
        }

        private async Task InitializePreviewAsync(params Surface[] surfaces)
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
            var builder = camera.CreateCaptureRequest(CameraTemplate.Preview);
            Surface previewSurface = new Surface(preview.SurfaceTexture);

            builder.AddTarget(previewSurface);

            var request = builder.Build();

            captureSession.SetRepeatingRequest(request, null, null);
        }

        private void Picture_Click(object sender, EventArgs e)
        {
            startTime = DateTime.Now;
            Android.Util.Log.Warn("SubC", "start....");
            continuous.Start();
            stopTimer.Start();
        }

        private void Stop_Click(object sender, EventArgs e)
        {
            stopTimer.Stop();
            continuous.Stop();
        }

        private void StopTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            stopTimer.Stop();
            continuous.Stop();
        }
    }
}