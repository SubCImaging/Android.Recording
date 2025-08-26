//-----------------------------------------------------------------------
// <copyright file="AndroidSerial.cs" company="SubC Imaging">
// Copyright (c) SubC Imaging. All rights reserved.
// </copyright>
// <author>Adam Rowe</author>
//-----------------------------------------------------------------------

namespace Android.ContinuousStills
{
    using Android.App;
    using Android.Hardware.Usb;
    using Hoho.Android.UsbSerial.Driver;
    using Hoho.Android.UsbSerial.Util;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    /// <summary>
    /// Responsible for connecting to a USB serial port.
    /// </summary>
    public class AndroidSerial
    {
        /// <summary>
        /// Sync to lock.
        /// </summary>
        private static readonly object Sync = new object();

        /// <summary>
        /// USed for permissions.
        /// </summary>
        private readonly Activity activity;

        /// <summary>
        /// Grab serial device from USBManager.
        /// </summary>
        private readonly UsbManager usbManager;

        /// <summary>
        /// Is the device connected.
        /// </summary>
        private bool isConnected;

        /// <summary>
        /// Serial port connected to.
        /// </summary>
        private IUsbSerialPort port;

        /// <summary>
        /// Sending and receiving serial data on.
        /// </summary>
        private SerialInputOutputManager serialIoManager;

        /// <summary>
        /// Initializes a new instance of the <see cref="AndroidSerial"/> class.
        /// </summary>
        /// <param name="activity">Activity used for permission handling.</param>
        /// <param name="usbManager">Manager to grab device.</param>
        public AndroidSerial(Activity activity, UsbManager usbManager)
        {
            this.activity = activity;
            this.usbManager = usbManager;
        }

        /// <summary>
        /// Fire when data is received from serial port
        /// </summary>
        public event EventHandler<string> DataReceived;

        /// <summary>
        /// Fire when is connected changes
        /// </summary>
        public event EventHandler<bool> IsConnectedChanged;

        /// <summary>
        /// Fire when is sending changes
        /// </summary>
        public event EventHandler<bool> IsSendingChanged;

        /// <summary>
        /// Notify the message router with relevant data
        /// </summary>
        public event EventHandler<Notification> Notify;

        /// <summary>
        /// Gets or sets the string to append to what's being sent.
        /// </summary>
        public string Append { get; set; } = string.Empty;

        /// <summary>
        /// Gets a value indicating whether the port is connected.
        /// </summary>
        public bool IsConnected
        {
            get
            {
                return serialIoManager?.IsOpen ?? false;
            }

            private set
            {
                if (isConnected == value)
                {
                    return;
                }

                isConnected = value;
                IsConnectedChanged?.Invoke(this, isConnected);
            }
        }

        /// <summary>
        /// Gets a value indicating whether the port is sending information.
        /// </summary>
        public bool IsSending
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        /// <summary>
        /// Gets or sets func to process output.
        /// </summary>
        public Func<string, string> Output { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        /// <summary>
        /// Gets or sets string to prepend to data sent.
        /// </summary>
        public string Prepend { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        /// <summary>
        /// Gets connection status.
        /// </summary>
        public string Status => IsConnected ? "Connected" : "Disconnected";

        /// <summary>
        /// Connect to given address.
        /// </summary>
        /// <param name="portName">Port to connect.</param>
        /// <param name="baudRate">Baud rate to use.</param>
        /// <returns>True if connected.</returns>
        public async Task<bool> ConnectAsync(string portName, int baudRate)
        {
            var ports = await GetPortsAsync();

            this.port = (from p in ports
                         where p.ToString() == portName.ToString()
                         select p).FirstOrDefault();

            if (this.port == null)
            {
                return IsConnected = false;
            }

            serialIoManager = new SerialInputOutputManager(this.port)
            {
                BaudRate = baudRate,
                DataBits = 8,
                StopBits = StopBits.One,
                Parity = Parity.None,
            };

            serialIoManager.DataReceived += (s, e) => SerialIoManager_DataReceived(e);

            try
            {
                if (usbManager.HasPermission(port.Driver.Device))
                {
                    serialIoManager.Open(usbManager);
                    return IsConnected = true;
                }

                var permissionGranted = await usbManager.RequestPermissionAsync(port.Driver.Device, activity.BaseContext);
                if (permissionGranted)
                {
                    serialIoManager.Open(usbManager);
                }
                else
                {
                    return IsConnected = false;
                }
            }
            catch (Java.IO.IOException e)
            {
                return IsConnected = false;
            }

            return IsConnected = true;
        }

        /// <summary>
        /// Set is connected to false.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        public async Task DisconnectAsync()
        {
            serialIoManager.Close();
            IsConnected = false;
        }

        /// <summary>
        /// Get all the drivers from the USB manager.
        /// </summary>
        /// <returns>Enumerable of serial drivers.</returns>
        public async Task<IEnumerable<IUsbSerialDriver>> GetDriversAsync() => usbManager.FindAllDriversAsync();

        /// <summary>
        /// Get all the ports from all the drivers.
        /// </summary>
        /// <returns>Enumerable of ports.</returns>
        public async Task<IEnumerable<IUsbSerialPort>> GetPortsAsync() => (await GetDriversAsync()).SelectMany(d => d.Ports);

        /// <summary>
        /// Receive data async.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        public Task ReceiveAsync()
        {
            throw new Exception();
        }

        /// <summary>
        /// Send data to the connect port.
        /// </summary>
        /// <param name="bytes">Data to send through the connected port.</param>
        public void Send(byte[] bytes)
        {
            try
            {
                port?.Write(bytes, 1000);
            }
            catch
            {
                // unable to write, is port disconnected?
            }
        }

        /// <summary>
        /// Send data to the connect port.
        /// </summary>
        /// <param name="data">Data to send.</param>
        public void Send(string data)
        {
            var d = data + Append;

            Console.WriteLine($"Info: {DateTime.Now:HHmmss.fff} Tx: {d}");

            var dataBytes = Encoding.ASCII.GetBytes(d);

            try
            {
                port?.Write(dataBytes, 1000);
            }
            catch
            {
                // unable to write, is port disconnected?
            }
        }

        /// <summary>
        /// Send data to the connect port.
        /// </summary>
        /// <param name="data">Data to send.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        public async Task SendAsync(string data)
        {
            // await Task.Delay(1000);
            Send(data);
        }

        /// <summary>
        /// Serial data received handler.
        /// </summary>
        /// <param name="e">Data that was received.</param>
        private void SerialIoManager_DataReceived(SerialDataReceivedArgs e)
        {
            var data = Encoding.ASCII.GetString(e.Data);

            Console.WriteLine($"Warning: {DateTime.Now:HHmmss.fff} Rx: {data}");
        }
    }
}