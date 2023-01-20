using Android.App;
using Android.Content;
using Android.Hardware.Camera2;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Android.Media;
using Android.Graphics;
using System.Timers;

namespace Android.ContinuousStills
{
    public class ContinuousStillsManager
    {
        private readonly Activity activity;
        private readonly CaptureCallback c;
        private readonly Timer restartTimer = new Timer(5000) { AutoReset = false };
        private readonly Timer stillTimer = new Timer(500);
        private CameraDevice camera;

        private CameraCaptureSession captureSession;
        private Handler handler;
        private Surface imgReader;
        private Surface previewSurface;
        private CaptureRequest request;
        private CaptureRequest.Builder stillCaptureBuilder;

        public ContinuousStillsManager(
            Surface previewSurface,
            CameraDevice camera,
            Surface imgReader,
            CameraCaptureSession captureSession,
            Activity activity)
        {
            this.previewSurface = previewSurface;
            this.camera = camera;
            this.imgReader = imgReader;
            this.captureSession = captureSession;
            this.activity = activity;
            c = new CaptureCallback();
            c.CaptureFailed += C_CaptureFailed;

            stillTimer.Elapsed += StillTimer_Elapsed;

            restartTimer.Elapsed += (_, __) => stillTimer.Start();
        }

        public void Start()
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
            stillCaptureBuilder.AddTarget(imgReader);
            stillCaptureBuilder.AddTarget(previewSurface);

            request = stillCaptureBuilder.Build();

            stillTimer.Start();
        }

        public void Stop()
        {
            stillTimer.Stop();
        }

        private void C_CaptureFailed(object sender, EventArgs e)
        {
            Android.Util.Log.Error("SubC", "Capture failed");
            stillTimer.Stop();

            restartTimer.Stop();
            restartTimer.Start();
        }

        private void StillTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            Take();
        }

        private void Take()
        {
            Android.Util.Log.Warn("SubC", "take....");

            activity.RunOnUiThread(() =>
            {
                captureSession.Capture(request, c, handler);
            });
        }
    }
}