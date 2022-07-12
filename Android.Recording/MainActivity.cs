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

namespace Android.Recording
{
    [Activity(Label = "@string/app_name", Theme = "@style/AppTheme", MainLauncher = true)]
    public class MainActivity : AppCompatActivity
    {
        private CameraDevice camera;
        private CameraCaptureSession captureSession;
        private AutoFitTextureView preview;
        private Button record;

        private MediaRecorder recorder = new MediaRecorder();

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
            base.OnCreate(savedInstanceState);
            Xamarin.Essentials.Platform.Init(this, savedInstanceState);
            // Set our view from the "main" layout resource
            SetContentView(Resource.Layout.activity_main);

            // set up the camera
            // get the preview to display video
            preview = FindViewById<AutoFitTextureView>(Resource.Id.Preview);
            record = FindViewById<Button>(Resource.Id.video);
            record.Click += Record_Click;
            var cameraManager = (CameraManager)GetSystemService(CameraService);

            while (!preview.IsAvailable)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(100));
            }

            // get the camera from the camera manager with the given ID
            camera = await OpenCameraAsync("0", cameraManager);

            await RefreshSessionAsync(CameraTemplate.Preview, new Surface(preview.SurfaceTexture));
        }

        private File GetVideoFile(Context context)
        {
            string fileName = "video-" + DateTime.Now.ToString("yyMMdd-hhmmss") + ".mp4"; //new filenamed based on date time
            var file = new File(context.GetExternalFilesDir(null), fileName);

            System.Diagnostics.Debug.WriteLine($"{file}");

            return file;
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
                await RefreshSessionAsync(CameraTemplate.Preview, new Surface(preview.SurfaceTexture));
            }
        }

        private void Recorder_Error(object sender, MediaRecorder.ErrorEventArgs e)
        {
            System.Console.WriteLine(e.What);
        }

        private void Recorder_Info(object sender, MediaRecorder.InfoEventArgs e)
        {
            System.Console.WriteLine(e.What);
        }

        private async Task RefreshSessionAsync(CameraTemplate template, params Surface[] surfaces)
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

            var builder = camera.CreateCaptureRequest(template);

            foreach (var surface in surfaces)
            {
                builder.AddTarget(surface);
            }

            var request = builder.Build();

            captureSession.SetRepeatingRequest(request, null, null);
        }

        private async Task StartRecordingAsync()
        {
            captureSession.Close();

            recorder.SetVideoSource(VideoSource.Surface);
            recorder.SetOutputFormat(OutputFormat.Mpeg4);

            recorder.SetOnInfoListener(new InfoListener());
            recorder.SetOnErrorListener(new ErrorListener());

            // get the file
            //var file = GetVideoFile(this);

            //if (!file.Exists())
            //{
            //    file.CreateNewFile();
            //}

            recorder.SetOutputFile(GetVideoFile(this).AbsoluteFile.Path);

            recorder.SetVideoEncodingBitRate(25_000);
            recorder.SetVideoFrameRate(30);
            recorder.SetVideoSize(1920, 1080);
            recorder.SetVideoEncoder(VideoEncoder.H264);
            recorder.Prepare();

            await RefreshSessionAsync(CameraTemplate.Record, new Surface(preview.SurfaceTexture), recorder.Surface);

            //var o = new Observer(file.Path);
            //o.StartWatching();

            recorder.Error += Recorder_Error;
            recorder.Info += Recorder_Info;

            recorder.Start();
        }
    }
}