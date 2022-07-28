using Android.Hardware.Camera2;
using Android.Runtime;
using System;

namespace Android.Camera
{
    public class CameraStateCallback : CameraDevice.StateCallback
    {
        public event EventHandler<CameraDevice> Disconnected;

        public event EventHandler<CameraErrorArgs> Error;

        public event EventHandler<CameraDevice> Opened;

        public override void OnDisconnected(CameraDevice camera)
        {
            Disconnected?.Invoke(this, camera);
        }

        public override void OnError(CameraDevice camera, [GeneratedEnum] Android.Hardware.Camera2.CameraError error)
        {
            Error?.Invoke(this, new CameraErrorArgs(camera, error));
        }

        public override void OnOpened(CameraDevice camera)
        {
            Opened?.Invoke(this, camera);
        }
    }
}