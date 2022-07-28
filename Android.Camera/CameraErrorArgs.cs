using Android.Hardware.Camera2;
using System;

namespace Android.Camera
{
    public class CameraErrorArgs : EventArgs
    {
        public CameraErrorArgs(CameraDevice camera, Android.Hardware.Camera2.CameraError error)
        {
            Camera = camera;
            Error = error;
        }

        public CameraDevice Camera { get; }
        public Android.Hardware.Camera2.CameraError Error { get; }
    }
}