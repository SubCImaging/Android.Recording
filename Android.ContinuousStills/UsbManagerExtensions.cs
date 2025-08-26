// <copyright file="UsbManagerExtensions.cs" company="SubC Imaging">
// Copyright (c) SubC Imaging. All rights reserved.
// </copyright>

namespace Android.ContinuousStills
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Android.App;
    using Android.Content;
    using Android.Hardware.Usb;
    using Hoho.Android.UsbSerial.Driver;

    /// <summary>
    /// Responsible for extending the UsbManager class.
    /// </summary>
    public static class UsbManagerExtensions
    {
        private const string ACTION_USB_PERMISSION = "com.Hoho.Android.UsbSerial.Util.USB_PERMISSION";

        private static readonly Dictionary<Tuple<Context, UsbDevice>, TaskCompletionSource<bool>> taskCompletionSources =
            new Dictionary<Tuple<Context, UsbDevice>, TaskCompletionSource<bool>>();

        /// <summary>
        /// Find all available drivers from current UsbManager and probe all available drivers.
        /// </summary>
        /// <param name="usbManager">Usb Manager.</param>
        /// <returns>List of usb drivers.</returns>
        public static IList<IUsbSerialDriver> FindAllDriversAsync(this UsbManager usbManager)
        {
            // adding a custom driver to the default probe table
            var table = UsbSerialProber.DefaultProbeTable;
            table.AddProduct(0x1b4f, 0x0008, Java.Lang.Class.FromType(typeof(CdcAcmSerialDriver))); // IOIO OTG

            table.AddProduct(0x0483, 0x5740, Java.Lang.Class.FromType(typeof(CdcAcmSerialDriver))); // L086
            var prober = new UsbSerialProber(table);

            return prober.FindAllDrivers(usbManager);
        }

        /// <summary>
        /// Request permission to access the device.
        /// </summary>
        /// <param name="manager">Usb Manager.</param>
        /// <param name="device">Device to request permission for.</param>
        /// <param name="context">Context to request on.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        public static async Task<bool> RequestPermissionAsync(this UsbManager manager, UsbDevice device, Context context)
        {
            var completionSource = new TaskCompletionSource<bool>();

            Console.WriteLine($"Device has permission: {manager.HasPermission(device)}");

            var usbPermissionReceiver = new UsbPermissionReceiver(completionSource);
            context.RegisterReceiver(usbPermissionReceiver, new IntentFilter(ACTION_USB_PERMISSION));

            var intent = PendingIntent.GetBroadcast(context, 0, new Intent(ACTION_USB_PERMISSION), 0);
            manager.RequestPermission(device, intent);

            return await completionSource.Task;
        }

        private class UsbPermissionReceiver
            : BroadcastReceiver
        {
            private readonly TaskCompletionSource<bool> completionSource;

            public UsbPermissionReceiver(TaskCompletionSource<bool> completionSource)
            {
                this.completionSource = completionSource;
            }

            public override void OnReceive(Context context, Intent intent)
            {
                var device = intent.GetParcelableExtra(UsbManager.ExtraDevice) as UsbDevice;
                var permissionGranted = intent.GetBooleanExtra(UsbManager.ExtraPermissionGranted, false);
                context.UnregisterReceiver(this);
                completionSource.TrySetResult(permissionGranted);
            }
        }
    }
}