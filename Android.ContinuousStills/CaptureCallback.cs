using Android.Hardware.Camera2;
using Android.Views;
using System;

namespace Android.ContinuousStills
{
    public class CaptureCallback : CameraCaptureSession.CaptureCallback
    {
        public event EventHandler SequenceComplete;

        public override void OnCaptureBufferLost(CameraCaptureSession session, CaptureRequest request, Surface target, long frameNumber)
        {
            System.Diagnostics.Debug.WriteLine($"Capture buffer lost");
            base.OnCaptureBufferLost(session, request, target, frameNumber);
        }

        public override void OnCaptureCompleted(CameraCaptureSession session, CaptureRequest request, TotalCaptureResult result)
        {
            System.Diagnostics.Debug.WriteLine($"Capture Completed");
            base.OnCaptureCompleted(session, request, result);
        }

        public override void OnCaptureFailed(CameraCaptureSession session, CaptureRequest request, CaptureFailure failure)
        {
            System.Diagnostics.Debug.WriteLine($"Capture failed");
            base.OnCaptureFailed(session, request, failure);
        }

        public override void OnCaptureProgressed(CameraCaptureSession session, CaptureRequest request, CaptureResult partialResult)
        {
            System.Diagnostics.Debug.WriteLine($"Capture progressed");
            base.OnCaptureProgressed(session, request, partialResult);
        }

        public override void OnCaptureSequenceAborted(CameraCaptureSession session, int sequenceId)
        {
            System.Diagnostics.Debug.WriteLine($"Sequence aborted");
            base.OnCaptureSequenceAborted(session, sequenceId);
        }

        public override void OnCaptureSequenceCompleted(CameraCaptureSession session, int sequenceId, long frameNumber)
        {
            System.Diagnostics.Debug.WriteLine($"Sequence completed");
            SequenceComplete?.Invoke(this, EventArgs.Empty);
            base.OnCaptureSequenceCompleted(session, sequenceId, frameNumber);
        }

        public override void OnCaptureStarted(CameraCaptureSession session, CaptureRequest request, long timestamp, long frameNumber)
        {
            System.Diagnostics.Debug.WriteLine($"Capture started");
            base.OnCaptureStarted(session, request, timestamp, frameNumber);
        }
    }
}