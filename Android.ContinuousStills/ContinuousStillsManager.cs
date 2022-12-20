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
using Android.Camera;
using Android.Media;
using Android.Graphics;

namespace Android.ContinuousStills
{
    public class ContinuousStillsManager
    {
        private CameraDevice camera;

        private CameraCaptureSession captureSession;
        private Handler handler;
        private Surface imgReader;
        private Surface previewSurface;
        private CaptureRequest request;
        private CaptureRequest.Builder stillCaptureBuilder;

        public ContinuousStillsManager(Surface previewSurface, CameraDevice camera, Surface imgReader, CameraCaptureSession captureSession)
        {
            this.previewSurface = previewSurface;
            this.camera = camera;
            this.imgReader = imgReader;
            this.captureSession = captureSession;
        }

        public void StartContinousStills()
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

            Take();
        }

        public void StopContinuousStills()
        {
            captureSession.StopRepeating();
        }

        public void Take()
        {
            Android.Util.Log.Warn("SubC", "take....");
            var c = new CaptureCallback();
            captureSession.Capture(request, c, handler);
        }
    }
}