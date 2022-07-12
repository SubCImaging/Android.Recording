using Android;
using Android.App;
using Android.Content;
using Android.OS;
using AndroidX.Core.App;

namespace Android.Recording
{
    public class ConfirmationDialog : DialogFragment
    {
        private static Fragment mParent;

        public override Dialog OnCreateDialog(Bundle savedInstanceState)
        {
            mParent = ParentFragment;
            return new AlertDialog.Builder(Activity)
                .SetMessage(Resource.String.request_permission)
                .SetPositiveButton(Android.Resource.String.Ok, new PositiveListener())
                .SetNegativeButton(Android.Resource.String.Cancel, new NegativeListener())
                .Create();
        }

        private class NegativeListener : Java.Lang.Object, IDialogInterfaceOnClickListener
        {
            public void OnClick(IDialogInterface dialog, int which)
            {
                Activity activity = mParent.Activity;
                if (activity != null)
                {
                    activity.Finish();
                }
            }
        }

        private class PositiveListener : Java.Lang.Object, IDialogInterfaceOnClickListener
        {
            public void OnClick(IDialogInterface dialog, int which)
            {
                //ActivityCompat.RequestPermissions(mParent,
                //              new string[] { Manifest.Permission.Camera }, MainActivity.REQUEST_CAMERA_PERMISSION);
            }
        }
    }
}