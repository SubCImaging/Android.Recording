﻿using Android.Hardware.Camera2;
using Android.Views;
using System;

namespace Android.Camera
{
    public class CaptureCallback : CameraCaptureSession.CaptureCallback
    {
        public event EventHandler SequenceComplete;

        public override void OnCaptureBufferLost(CameraCaptureSession session, CaptureRequest request, Surface target, long frameNumber)
        {
            //System.Diagnostics.Debug.WriteLine($"Capture buffer lost");
            Android.Util.Log.Info("SubC", "Capture buffer lost");
            base.OnCaptureBufferLost(session, request, target, frameNumber);
        }

        public override void OnCaptureCompleted(CameraCaptureSession session, CaptureRequest request, TotalCaptureResult result)
        {
            //System.Diagnostics.Debug.WriteLine($"Capture Completed");
            Android.Util.Log.Info("SubC", "Capture Completed");
            base.OnCaptureCompleted(session, request, result);
        }

        public override void OnCaptureFailed(CameraCaptureSession session, CaptureRequest request, CaptureFailure failure)
        {
            //System.Diagnostics.Debug.WriteLine($"Capture failed");
            Android.Util.Log.Info("SubC", "Capture failed");

            base.OnCaptureFailed(session, request, failure);
        }

        public override void OnCaptureProgressed(CameraCaptureSession session, CaptureRequest request, CaptureResult partialResult)
        {
            //System.Diagnostics.Debug.WriteLine($"Capture progressed");
            Android.Util.Log.Info("SubC", "Capture progressed");
            base.OnCaptureProgressed(session, request, partialResult);
        }

        public override void OnCaptureSequenceAborted(CameraCaptureSession session, int sequenceId)
        {
            //System.Diagnostics.Debug.WriteLine($"Sequence aborted");
            Android.Util.Log.Info("SubC", "Sequence aborted");
            base.OnCaptureSequenceAborted(session, sequenceId);
        }

        public override void OnCaptureSequenceCompleted(CameraCaptureSession session, int sequenceId, long frameNumber)
        {
            //System.Diagnostics.Debug.WriteLine($"Sequence completed");
            Android.Util.Log.Info("SubC", "Sequence Completed");
            SequenceComplete?.Invoke(this, EventArgs.Empty);
            base.OnCaptureSequenceCompleted(session, sequenceId, frameNumber);
        }

        public override void OnCaptureStarted(CameraCaptureSession session, CaptureRequest request, long timestamp, long frameNumber)
        {
            //System.Diagnostics.Debug.WriteLine($"Capture started");
            Android.Util.Log.Info("SubC", "Capture started");
            base.OnCaptureStarted(session, request, timestamp, frameNumber);
        }
    }
}