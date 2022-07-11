using Android.Runtime;
using Android.Media;

namespace Android.Recording
{
    public class ErrorListener : Java.Lang.Object, MediaRecorder.IOnErrorListener
    {
        public void OnError(MediaRecorder mr, [GeneratedEnum] MediaRecorderError what, int extra)
        {
            System.Diagnostics.Debug.WriteLine($"Error: {what}");
        }
    }
}