using Android.Runtime;
using Android.Media;


namespace Android.Recording
{
    public class InfoListener : Java.Lang.Object, Android.Media.MediaRecorder.IOnInfoListener
    {
        public void OnInfo(MediaRecorder mr, [GeneratedEnum] MediaRecorderInfo what, int extra)
        {
            System.Diagnostics.Debug.WriteLine($"Information: {what}");
        }
    }
}