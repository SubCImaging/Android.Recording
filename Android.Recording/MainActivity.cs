using Android.App;
using Android.Camera;
using Android.Content;
using Android.Hardware;
using Android.Hardware.Camera2;
using Android.Media;
using Android.OS;
using Android.Provider;
using Android.Runtime;
using Android.Util;
using Android.Views;
using Android.Widget;
using AndroidX.AppCompat.App;
using AndroidX.Core.App;
using Java.IO;
using Java.Nio;
using SubC.Rayfin.Helpers;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography.X509Certificates;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Timers;

namespace Android.Recording
{
    [Activity(Label = "@string/app_name", Theme = "@style/AppTheme", MainLauncher = true)]
    public class MainActivity : AppCompatActivity, MediaRecorder.IOnInfoListener
    {
        private CameraDevice camera;

        private CameraCaptureSession captureSession;

        private AutoFitTextureView preview;

        private Button record;

        private MediaRecorder recorder = new MediaRecorder();
        private Timer recordingTimer;
        private Shell shell;
        private bool isRecording;
        private int videosRecorded = 0;
        private Stopwatch recorderTimer = new Stopwatch();
        private FileInfo recordingLogger;
        private string dir = $"sdcard/Android/data/com.companyname.android.recording/sdcard/DCIM";
        //private string dir = "/mnt/user/0/emulated/";
        private Stopwatch restartTimer = new Stopwatch();

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

        public async void OnInfo(MediaRecorder mr, [GeneratedEnum] MediaRecorderInfo what, int extra)
        {
            System.Diagnostics.Debug.WriteLine($"Warning: " + what);

            if (what == MediaRecorderInfo.MaxDurationReached)
            {
                videosRecorded += 1;

                restartTimer.Start();
                recorder.Stop();
                Toast.MakeText(this, $"Videos Recorded: {videosRecorded}", ToastLength.Short).Show();

                await StartRecordingAsync(this);
                restartTimer.Stop();

                var restartTime = restartTimer.Elapsed;
                //Android.Util.Log.Debug("LogPath", $"{recordingLogger.FullName}");

                restartTimer.Reset();

                using (System.IO.StreamWriter writer = new System.IO.StreamWriter(recordingLogger.FullName, true))
                {
                    writer.WriteLine($"Restart Recording Time ==> {restartTime}");
                }
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


        protected override async void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            Xamarin.Essentials.Platform.Init(this, savedInstanceState);
            // Set our view from the "main" layout resource
            SetContentView(Resource.Layout.activity_main);
            ActivityCompat.RequestPermissions(this, new string[]
            {
                Manifest.Permission.ReadExternalStorage
            }, 1001);

            //Intent intent = new Intent(Settings.ActionManageAllFilesAccessPermission);
            //intent.SetData(Android.Net.Uri.Parse("package:" + Application.Context.PackageName));
            //StartActivity(intent);
            //Intent intent = new Intent(Intent.ActionOpenDocument);
            //intent.SetType("*video/*"); // Or "video/*", "image/*", etc.
            //intent.AddCategory(Intent.CategoryOpenable);

            //StartActivityForResult(intent, 1234);



            // set up the camera
            // get the preview to display video
            preview = FindViewById<AutoFitTextureView>(Resource.Id.Preview);
            record = FindViewById<Button>(Resource.Id.video);

            recordingTimer = new Timer();
            //var logDir = (string)Android.OS.Environment.DirectoryPictures;
            shell = new Shell();
            recordingLogger = new FileInfo($"{dir}/prepareLogs.log");

            record.Click += Record_Click;

            var cameraManager = (CameraManager)GetSystemService(CameraService);

            while (!preview.IsAvailable)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(100));
            }

            // get the camera from the camera manager with the given ID
            camera = await OpenCameraAsync("0", cameraManager);

            //await GetStoragePoint(shell);

            await RefreshSessionAsync(CameraTemplate.Preview, new Surface(preview.SurfaceTexture));
        }

        private Java.IO.File GetVideoFile()
        {


            var baseDirectory = $"mnt/expand/67331e5c-9099-4f57-8602-f8e17fddbe2e/DCIM";
            shell.Execute($"mkdir -p \"{baseDirectory}\"");
            shell.Execute($"chmod -R 777 \"{baseDirectory}\"");
            string fileName = "video-" + DateTime.Now.ToString("yyMMdd-hhmmss") + ".mp4"; //new filenamed based on date time

            //string incindex = $"{dir}/vid{index}";
            //if (index >= max)
            //{
            //    ShellSync($"mkdir -p \"{incindex}\"");
            //    ShellSync($"chmod -R 777 \"{dir}/vid{incindex}\"");
            //}
            //index++;
            var file = new Java.IO.File(/*dir*/ baseDirectory, fileName); // change "dir" into "incindex" to generate a folder each time

            System.Diagnostics.Debug.WriteLine($"{file}");
            return file;
        }

        private async void Record_Click(object sender, EventArgs e)
        {
            if (record.Text == "record")
            {
                record.Text = "stop";
                await StartRecordingAsync(this);
                //recordingTimer.Start();
            }
            else
            {
                record.Text = "record";
                //recordingTimer.Stop();
                recorder.Stop();

                await RefreshSessionAsync(CameraTemplate.Preview, new Surface(preview.SurfaceTexture));
            }
        }

        private void Recorder_Error(object sender, MediaRecorder.ErrorEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine("===> " + e.What);
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

        /// <summary>
        /// Gets the <see cref="Guid" /> that represents the SD card in the Rayfin.
        /// </summary>
        /// <returns>The <see cref="Guid" /> that represents the SD card in the Rayfin.</returns>
        private static async Task<Guid> GetStoragePoint(Shell shell)
        {
            var retries = 0;
            while (retries < 3)
            {
                var folders = shell.Execute($@"ls /mnt/expand").Split('\n');

                foreach (string folder in folders)
                {
                    if (Guid.TryParse(folder, out Guid result))
                    {
                        return result;
                    }
                }

                retries++;
                await Task.Delay(2000);
            }

            throw new System.IO.IOException("Could not find storage mount");
        }

        //public Android.Net.Uri CreateMediaOutputUri(Context context)
        //{
        //    var values = new ContentValues();
        //    values.Put(MediaStore.MediaColumns.DisplayName, "recording_" + Guid.NewGuid() + ".mp4");
        //    values.Put(MediaStore.MediaColumns.MimeType, "video/mp4");
        //    values.Put(MediaStore.MediaColumns.RelativePath, "Movies/MyApp"); // Automatically goes to SD card if mounted and used

        //    var externalUri = MediaStore.Video.Media.ExternalContentUri;

        //    var resolver = context.ContentResolver;
        //    return resolver.Insert(externalUri, values); // This is the URI to give to MediaRecorder
        //}


        private async Task StartRecordingAsync(Context context)
        {
            //captureSession.Close();
            isRecording = true;
            recorder.SetVideoSource(VideoSource.Surface);
            recorder.SetOutputFormat(OutputFormat.Mpeg4);

            //var uri = CreateMediaOutputUri(context);

            //var fileDescriptor = context.ContentResolver.OpenFileDescriptor(uri, "w").FileDescriptor;

            // get the file
            //var storagePoint = await GetStoragePoint(shell);
            var file = GetVideoFile().AbsoluteFile;

            var dir = file.Parent;

            if (!System.IO.Directory.Exists(dir))
            {
                System.IO.Directory.CreateDirectory(dir);
            }

            System.Diagnostics.Debug.WriteLine($"Error: Starting to record : " + file.Path /*fileDescriptor*/);

            recorder.SetOutputFile(file.Path /*fileDescriptor*/);
            recorder.SetMaxDuration((int)TimeSpan.FromMinutes(10).TotalMilliseconds);
            recorder.SetOnInfoListener(this);
            recorder.SetVideoEncodingBitRate(25_000_000);
            recorder.SetVideoFrameRate(30);
            recorder.SetVideoSize(3840, 2160);
            recorder.SetMaxFileSize(21_474_836_480);
            recorder.SetVideoEncoder(VideoEncoder.H264);


            recorderTimer.Start();
            recorder.Prepare();
            recorderTimer.Stop();
            var prepareTime = recorderTimer.Elapsed;
            //Android.Util.Log.Debug("LogPath", $"{recordingLogger.FullName}");

            recorderTimer.Reset();
            //using (var fileStream = new FileStream(recordingLogger.AbsolutePath, FileMode.Create, FileAccess.Write))
            //{
            //    fileStream.Write($"Preparing Time ==> {prepareTime}");
            //}
            //using (System.IO.StreamWriter writer = new System.IO.StreamWriter(recordingLogger.FullName, true))
            //{
            //    writer.WriteLine($"Preparing Time ==> {prepareTime}");
            //}

            await RefreshSessionAsync(CameraTemplate.Record, new Surface(preview.SurfaceTexture), recorder.Surface);

            recorder.Error += Recorder_Error;
            //recorder.Info += Recorder_Info;

            recorder.Start();
        }
    }
}