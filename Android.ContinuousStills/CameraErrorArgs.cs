using Android.Hardware.Camera2;
using System;

namespace Android.ContinuousStills
{
    public class CameraErrorArgs : EventArgs
    {
        public CameraErrorArgs(CameraDevice camera, CameraError error)
        {
            Camera = camera;
            Error = error;
        }

        public CameraDevice Camera { get; }
        public CameraError Error { get; }
    }
}