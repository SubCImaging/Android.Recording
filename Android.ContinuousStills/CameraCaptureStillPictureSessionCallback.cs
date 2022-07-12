// <copyright file="CameraCaptureStillPictureSessionCallback.cs" company="SubC Imaging">
// Copyright (c) SubC Imaging. All rights reserved.
// </copyright>

using Android.Hardware.Camera2;
using Android.Util;
using Java.Lang;

namespace Android.ContinuousStills
{
    /// <summary>
    /// Callback to call Takepicture again.
    /// </summary>
    public class CameraCaptureStillPictureSessionCallback : CameraCaptureSession.CaptureCallback
    {
        private readonly MainActivity main;

        /// <summary>
        /// Callback.
        /// </summary>
        /// <param name="owner">for main activity.</param>
        /// <exception cref="System.ArgumentNullException"></exception>
        public CameraCaptureStillPictureSessionCallback(MainActivity mainActivity)
        {
            if (mainActivity == null)
                throw new System.ArgumentNullException("mainActivity");
            this.main = mainActivity;
        }

        /// <summary>
        /// When capture is completed.
        /// </summary>
        /// <param name="session">session.</param>
        /// <param name="request">request.</param>
        /// <param name="result">result.</param>
        public override void OnCaptureCompleted(CameraCaptureSession session, CaptureRequest request, TotalCaptureResult result)
        {
            main.TakePicture();
        }
    }
}