using Android.OS;
using Android.Runtime;
using Java.IO;

namespace Android.Recording
{
    public class Observer : FileObserver
    {
        public Observer(string file)
            : base(file)
        {
        }

        public override void OnEvent([GeneratedEnum] FileObserverEvents e, string path)
        {
            System.Diagnostics.Debug.WriteLine($"{e} {path}");
        }
    }
}