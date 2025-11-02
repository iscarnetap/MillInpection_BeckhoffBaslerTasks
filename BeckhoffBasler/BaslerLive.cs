using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Basler.Pylon;
using System.Threading;
using System.Runtime.InteropServices;
using System.Globalization;
using System.IO;
using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Forms;
using System.Diagnostics;

namespace Inspection
{
    public class DeviceListUpdatedEventArgs : EventArgs
    {
        public string TagSerialNumber { get; set; }
    }

    public class CameraOpennedEventArgs : EventArgs
    {
        public int TrkExpValue { get; set; }
    }



    public class BaslerLive
    {
        public Camera camera1 = null;

        public event EventHandler CameraDestroyed;
        public event EventHandler DeviceListUpdated;
        public event EventHandler CameraOpenned;

        public event EventHandler GrabStarted;


        public string aPath = "";
        public string sFriendlyName1 = "";
        public Stopwatch stopWatchLive1 = new Stopwatch();
        public PixelDataConverter converter = new PixelDataConverter();

        /*
        // Occurs when the single frame acquisition button is clicked.
        private void toolStripButtonOneShot_Click(object sender, EventArgs e) {
            if (sender == toolStrip1ButtonOneShot)
                OneShot(1); // Start the grabbing of one image.
            //else if (sender == toolStrip2ButtonOneShot)
            //    OneShot(2);
        }

        // Occurs when the continuous frame acquisition button is clicked.
        private void toolStripButtonContinuousShot_Click(object sender, EventArgs e) {
            if (sender == toolStrip1ButtonContinuousShot)
                ContinuousShot(1); // Start the grabbing of images until grabbing is stopped.
            //else if (sender == toolStrip2ButtonContinuousShot)
            //    ContinuousShot(2);
        }

        // Occurs when the stop frame acquisition button is clicked.
        private void toolStripButtonStop_Click(object sender, EventArgs e) {
            if (sender == toolStrip1ButtonStop)
                Stop(1); // Stop the grabbing of images.
            //else if (sender == toolStrip2ButtonStop)
            //    Stop(2);
        }
        */

        // Starts the grabbing of a single image and handles exceptions.
        private void OneShot(int nCam)
        {
            try  {
                IGrabResult grabResult;
                if (nCam == 1) {
                    camera1.Parameters[PLCamera.AcquisitionMode].SetValue(PLCamera.AcquisitionMode.SingleFrame);
                    if (!camera1.StreamGrabber.IsGrabbing)
                        camera1.StreamGrabber.Start(1, GrabStrategy.OneByOne, GrabLoop.ProvidedByStreamGrabber);
                    grabResult = camera1.StreamGrabber.RetrieveResult(5000, TimeoutHandling.ThrowException);
                }
                //else if (nCam == 2) {
                //    camera2.Parameters[PLCamera.AcquisitionMode].SetValue(PLCamera.AcquisitionMode.SingleFrame);
                //    camera2.StreamGrabber.Start(1, GrabStrategy.OneByOne, GrabLoop.ProvidedByStreamGrabber);
                //    grabResult = camera2.StreamGrabber.RetrieveResult(5000, TimeoutHandling.ThrowException);
                //} 
                else return;

                if (!(grabResult is null)) {
                    using (grabResult) {
                        if (grabResult.GrabSucceeded) {
                            // Access the image data.
                            //Console.WriteLine("SizeX: {0}", grabResult.Width); Console.WriteLine("SizeY: {0}", grabResult.Height);
                            byte[] buffer = grabResult.PixelData as byte[];

                            MemoryStream ms = new MemoryStream(buffer);
                            Image im = Image.FromStream(ms);
                            im.Save(aPath + "\\Images\\snap.jpg", ImageFormat.Jpeg); // (@"C:\snap.jpg", ImageFormat.Jpeg);

                            //Console.WriteLine("Gray value of first pixel: {0}", buffer[0]); Console.WriteLine("");
                            //ImageWindow.DisplayImage(0, grabResult); // Display the grabbed image.
                        } else {
                            //Console.WriteLine("Error: {0} {1}", grabResult.ErrorCode, grabResult.ErrorDescription);
                        }
                    }
                } else {
                    MessageBox.Show("StreamGrabber.RetrieveResult camera " + nCam.ToString() + " error.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            catch (Exception exception) {
                string source = "";
                if (nCam == 1)
                    source = "OneShot camera 1 ";
                //else if (nCam == 2)
                //    source = "OneShot camera 2 ";
                ShowException(source, exception);
            }
        }

        // Shows exceptions in a message box.
        private void ShowException(string source, Exception exception)
        {
            MessageBox.Show("Exception caught:\n" + source + exception.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        /*
        private void btnOpenLive_Click(object sender, EventArgs e)
        {
            if (sender == btnOpenLive1)
                UpdateDeviceList(1);
            //else if (sender == btnOpenLive2)
            //    UpdateDeviceList(2);
        }
        */

        // Starts the continuous grabbing of images and handles exceptions.
        private void ContinuousShot(int nCam)
        {
            try
            {
                // Start the grabbing of images until grabbing is stopped.
                if (nCam == 1) {
                    camera1.Parameters[PLCamera.AcquisitionMode].SetValue(PLCamera.AcquisitionMode.Continuous);
                    camera1.StreamGrabber.Start(GrabStrategy.OneByOne, GrabLoop.ProvidedByStreamGrabber);
                }
                //else if (nCam == 2) {
                //    camera2.Parameters[PLCamera.AcquisitionMode].SetValue(PLCamera.AcquisitionMode.Continuous);
                //    camera2.StreamGrabber.Start(GrabStrategy.OneByOne, GrabLoop.ProvidedByStreamGrabber);
                //}
                else return;
            }
            catch (Exception exception) {
                string source = "";
                if (nCam == 1)
                    source = "ContinuousShot camera 1 ";
                //else if (nCam == 2)
                //    source = "ContinuousShot camera 2 ";
                ShowException(source, exception);
            }
        }

        // Stops the grabbing of images and handles exceptions.
        private void Stop(int nCam)
        {
            try {
                if (nCam == 1)
                    camera1.StreamGrabber.Stop();
                //else if (nCam == 2)
                //    camera2.StreamGrabber.Stop();
                else return;
            }
            catch (Exception exception) {
                string source = "";
                if (nCam == 1)
                    source = "Stop camera 1 ";
                //else if (nCam == 2)
                //    source = "Stop camera 2 ";
                ShowException(source, exception);
            }
        }

        // Closes the camera object and handles exceptions.
        private void DestroyCamera(int nCam)
        {
            // Disable all parameter controls.
            //try {
            //    if (nCam == 1 && camera1 != null || nCam == 2 && camera2 != null) {
            //        exposureTimeSliderControl.Parameter = null;
            //    }
            //}
            //catch (Exception exception) {
            //    ShowException(exception);
            //}

            // Destroy the camera object.
            try {
                if (nCam == 1 && camera1 != null) {
                    try {
                        camera1.StreamGrabber.Stop();
                    }
                    catch { }

                    camera1.Close();
                    camera1.Dispose();
                    camera1 = null;

                    CameraDestroyed?.Invoke(this, EventArgs.Empty);
                    /*
                    btnOpenLive1.Enabled = true;
                    btnCloseLive1.Enabled = false;
                    toolStrip1.Enabled = false;
                    trkExp1.Enabled = false;

                    btnSnap1.Enabled = false;
                    */
                }
                //else if (nCam == 2 && camera2 != null) {
                //    try {
                //        camera2.StreamGrabber.Stop();
                //    }
                //    catch { }

                //    camera2.Close();
                //    camera2.Dispose();
                //    camera2 = null;

                //    btnOpenLive2.Enabled = true;
                //    btnCloseLive2.Enabled = false;
                //    toolStrip2.Enabled = false;
                //    trkExp2.Enabled = false;
                //}
            }
            catch (Exception exception) {
                string source = "";
                if (nCam == 1)
                    source = "DestroyCamera 1 ";
                //else if (nCam == 2)
                //    source = "DestroyCamera 2 ";
                ShowException(source, exception);
            }
        }

        // Updates the list of available camera devices.
        private void UpdateDeviceList(int nCam)
        {
            try { 
                bool bFound = false;
                ICameraInfo tag = null;
                List<ICameraInfo> allCameras = CameraFinder.Enumerate();
                foreach (ICameraInfo cameraInfo in allCameras) { // Loop over all cameras found
                    string s = cameraInfo[CameraInfoKey.FriendlyName];
                    if (nCam == 1 && s.Contains(sFriendlyName1)) { //nCam == 1 && s.Contains(sFriendlyName1) || nCam == 2 && s.Contains(sFriendlyName2))
                        tag = cameraInfo;
                        bFound = true;
                        break;
                    }
                }
                if (bFound) {
                    if (nCam == 1) {
                        DeviceListUpdatedEventArgs args = new DeviceListUpdatedEventArgs();
                        args.TagSerialNumber = tag["SerialNumber"].ToString();

                        DeviceListUpdated?.Invoke(this, args);
                        /*
                        lblBaslerLive1.Text = tag["SerialNumber"].ToString();
                        btnOpenLive1.Enabled = false;
                        btnCloseLive1.Enabled = true;
                        toolStrip1.Enabled = true;

                        btnSnap1.Enabled = true;
                        */
                    }
                    //else if (nCam == 2) {
                    //    lblBaslerLive2.Text = tag["SerialNumber"].ToString();
                    //    btnOpenLive2.Enabled = false;
                    //    btnCloseLive2.Enabled = true;
                    //    toolStrip2.Enabled = true;
                    //}

                    OpenLiveCamera(nCam, tag);
                }
            }
            catch (Exception exception) {
                string source = "";
                if (nCam == 1)
                    source = "UpdateDeviceList camera 1 ";
                //else if (nCam == 2)
                //    source = "UpdateDeviceList camera 2 ";
                ShowException(source, exception);
            }
        }

        // creates a new object for the selected camera device. After that, the connection to the selected camera device is opened.
        private void OpenLiveCamera(int nCam, ICameraInfo tag)
        {
            // Destroy the old camera object.
            if (nCam == 1 && camera1 != null) { //nCam == 1 && camera1 != null || nCam == 2 && camera2 != null) 
                DestroyCamera(nCam);
            }

            try {
                if (nCam == 1) {
                    // Create a new camera object.
                    camera1 = new Camera(tag);
                    camera1.CameraOpened += Configuration.AcquireContinuous;
                    // Register for the events of the image provider needed for proper operation.
                    camera1.ConnectionLost += OnConnectionLost1;
                    //camera1.CameraOpened += OnCameraOpened1;
                    //camera1.CameraClosed += OnCameraClosed1;
                    camera1.StreamGrabber.GrabStarted += OnGrabStarted1;
                    camera1.StreamGrabber.ImageGrabbed += OnImageGrabbed1;
                    //camera1.StreamGrabber.GrabStopped += OnGrabStopped1;

                    DeviceAccessibilityInfo di = CameraFinder.GetDeviceAccessibilityInfo(camera1.CameraInfo);

                    if (di == DeviceAccessibilityInfo.Opened) {
                        camera1.Close();
                        di = CameraFinder.GetDeviceAccessibilityInfo(camera1.CameraInfo);
                    }

                    if (di == DeviceAccessibilityInfo.Ok) {
                        camera1.Open(); // Open the connection to the camera device.


                        CameraOpennedEventArgs args = new CameraOpennedEventArgs();
                        if (camera1.Parameters.Contains(PLCamera.ExposureTimeAbs)) 
                            args.TrkExpValue = Convert.ToInt32(camera1.Parameters[PLCamera.ExposureTimeAbs].ToString());
                        else
                            args.TrkExpValue = Convert.ToInt32(camera1.Parameters[PLCamera.ExposureTime].ToString());

                        CameraOpenned?.Invoke(this, args);

                        /*
                        trkExp1.Enabled = true;
                        // Set the parameter for the controls.
                        if (camera1.Parameters.Contains(PLCamera.ExposureTimeAbs)) {
                            trkExp1.Value = Convert.ToInt32(camera1.Parameters[PLCamera.ExposureTimeAbs].ToString());
                            //exposureTimeSliderControl.Parameter = camera.Parameters[PLCamera.ExposureTimeAbs];
                        } else {
                            trkExp1.Value = Convert.ToInt32(camera1.Parameters[PLCamera.ExposureTime].ToString());
                            //exposureTimeSliderControl.Parameter = camera.Parameters[PLCamera.ExposureTime];
                        }
                        toolStripButtonContinuousShot_Click(toolStrip1ButtonContinuousShot, null);
                        */
                    } else
                        MessageBox.Show("Camera 1 - DeviceAccessibilityInfo is not ok\n(" + di.ToString() + ")", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                //else if (nCam == 2) {
                //    // Create a new camera object.
                //    camera2 = new Camera(tag);
                //    camera2.CameraOpened += Configuration.AcquireContinuous;
                //    // Register for the events of the image provider needed for proper operation.
                //    camera2.ConnectionLost += OnConnectionLost2;
                //    camera2.CameraOpened += OnCameraOpened2;
                //    camera2.CameraClosed += OnCameraClosed2;
                //    camera2.StreamGrabber.GrabStarted += OnGrabStarted2;
                //    camera2.StreamGrabber.ImageGrabbed += OnImageGrabbed2;
                //    camera2.StreamGrabber.GrabStopped += OnGrabStopped2;

                //    DeviceAccessibilityInfo di = CameraFinder.GetDeviceAccessibilityInfo(camera2.CameraInfo);
                //    if (di == DeviceAccessibilityInfo.Ok) {
                //        camera2.Open(); // Open the connection to the camera device.
                //        trkExp2.Enabled = true;
                //        // Set the parameter for the controls.
                //        if (camera2.Parameters.Contains(PLCamera.ExposureTimeAbs)) {
                //            trkExp2.Value = Convert.ToInt32(camera2.Parameters[PLCamera.ExposureTimeAbs].ToString());
                //            //exposureTimeSliderControl.Parameter = camera.Parameters[PLCamera.ExposureTimeAbs];
                //        } else {
                //            trkExp2.Value = Convert.ToInt32(camera2.Parameters[PLCamera.ExposureTime].ToString());
                //            //exposureTimeSliderControl.Parameter = camera.Parameters[PLCamera.ExposureTime];
                //        }
                //        toolStripButtonContinuousShot_Click(toolStrip2ButtonContinuousShot, null);
                //    }
                //    else
                //        MessageBox.Show("Camera 2 - DeviceAccessibilityInfo is not ok\n(" + di.ToString() + ")", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                //}
            }
            catch (Exception exception) {
                string source = "";
                if (nCam == 1)
                    source = "OpenLiveCamera 1 ";
                //else if (nCam == 2)
                //    source = "OpenLiveCamera 2 ";
                ShowException(source, exception);
            }
        }

        private void OnConnectionLost1(Object sender, EventArgs e)
        {
            /*
            if (InvokeRequired) {
                // If called from a different thread, we must use the Invoke method to marshal the call to the proper thread.
                BeginInvoke(new EventHandler<EventArgs>(OnConnectionLost1), sender, e);
                return;
            }
            */
            DestroyCamera(1); // Close the camera object.

            UpdateDeviceList(1); // Because one device is gone, the list needs to be updated.
        }

        // Occurs when a device with an opened connection is removed.
        /*
        private void OnConnectionLost2(Object sender, EventArgs e)
        {
            if (InvokeRequired) {
                // If called from a different thread, we must use the Invoke method to marshal the call to the proper thread.
                BeginInvoke(new EventHandler<EventArgs>(OnConnectionLost2), sender, e);
                return;
            }

            DestroyCamera(2); // Close the camera object.

            UpdateDeviceList(2); // Because one device is gone, the list needs to be updated.
        }
        */

        /*
        private void btnCloseLive_Click(object sender, EventArgs e)
        {
            if (sender == btnCloseLive1)
                DestroyCamera(1);
            //else if (sender == btnCloseLive2)
            //    DestroyCamera(2);
        }
        */

        /*
        public void trkExp_Scroll(object sender, EventArgs e)
        {
            if (sender == trkExp1 && camera1 != null) {
                int val = 5000;
                this.Invoke((MethodInvoker)delegate
                {
                    val = trkExp1.Value;
                    toolTip1.SetToolTip(trkExp1, val.ToString());
                });
                //toolTip1.SetToolTip(trkExp1, trkExp1.Value.ToString());

                if (camera1.Parameters.Contains(PLCamera.ExposureTimeAbs)) {
                    try {
                        camera1.Parameters[PLCamera.ExposureTimeAbs].TrySetValue(val, FloatValueCorrection.ClipToRange);
                        Thread.Sleep(5);
                    }
                    catch (Exception ex) {
                        MessageBox.Show("Set exposition error: " + ex.Message, "trkExp_Scrol", MessageBoxButtons.OK, MessageBoxIcon.Error, MessageBoxDefaultButton.Button1);
                    }
                    //exposureTimeSliderControl.Parameter = camera.Parameters[PLCamera.ExposureTimeAbs];
                } else {
                    try {
                        camera1.Parameters[PLCamera.ExposureTime].TrySetValue(val, FloatValueCorrection.ClipToRange);
                        Thread.Sleep(5);
                    }
                    catch (Exception ex) {
                        MessageBox.Show("Set exposition error: " + ex.Message, "trkExp_Scrol", MessageBoxButtons.OK, MessageBoxIcon.Error, MessageBoxDefaultButton.Button1);
                    }
                    //exposureTimeSliderControl.Parameter = camera.Parameters[PLCamera.ExposureTime];
                }
            }
            //else if (sender == trkExp2 && camera2 != null) {
            //    toolTip2.SetToolTip(trkExp2, trkExp2.Value.ToString());
            //    if (camera2.Parameters.Contains(PLCamera.ExposureTimeAbs)) {
            //        try {
            //            camera2.Parameters[PLCamera.ExposureTimeAbs].TrySetValue(trkExp2.Value, FloatValueCorrection.ClipToRange);
            //            Thread.Sleep(5);
            //        }
            //        catch { }
            //        //exposureTimeSliderControl.Parameter = camera.Parameters[PLCamera.ExposureTimeAbs];
            //    } else {
            //        try {
            //            camera2.Parameters[PLCamera.ExposureTime].TrySetValue(trkExp2.Value, FloatValueCorrection.ClipToRange);
            //            Thread.Sleep(5);
            //        }
            //        catch { }
            //        //exposureTimeSliderControl.Parameter = camera.Parameters[PLCamera.ExposureTime];
            //    }
            //}
            
            //toolTip1.SetToolTip(trkExp, trkExp.Value.ToString());
            //HSAcquisitionDevice lAcquisition = (HSAcquisitionDevice)mApplicationControl1.ProcessManager.get_Process("Acquisition");
            //if (lAcquisition != null) {
            //    try {
            //        int nCam = 0;
            //        if (radioButton1.Checked == true) nCam = 1;
            //        else if (radioButton2.Checked == true) nCam = 2;
            //        else if (radioButton3.Checked == true) nCam = 3;
            //        else if (radioButton4.Checked == true) nCam = 4;
            //        //else if (radioButton5.Checked == true) nCam = 5;
            //        //else if (radioButton6.Checked == true) nCam = 6;
            //        else return;
            //
            //        lAcquisition.ConfigurationDefault = "Configuration" + nCam.ToString();
            //        //lAcquisition.Execute();
            //        short parindex = 0;
            //        //hsDirectShowParameterIndex dsindex = hsDirectShowParameterIndex.hsDirectShowExposureValue;
            //        //rc = (int)lAcquisition.get_GrabberDirectShowParameter(dsindex);
            //        object o = "ExposureTimeAbs", opar;
            //        parindex = lAcquisition.get_GrabberGigEParameterIndex(o, null, lAcquisition.ConfigurationDefault);
            //        opar = trkExp.Value;
            //        lAcquisition.set_GrabberGigEParameter(parindex, null, lAcquisition.ConfigurationDefault, opar);
            //        //object oret = lAcquisition.get_GrabberGigEParameter(parindex, null, lAcquisition.ConfigurationDefault);
            //        //toolTip1.SetToolTip(trkExp, oret.ToString());
            //        //oret = null; 
            //        Thread.Sleep(5);
            //        o = null;
            //        opar = null;
            //    }
            //    catch {
            //        MessageBox.Show("Set exposition error", "", MessageBoxButtons.OK, MessageBoxIcon.Error, MessageBoxDefaultButton.Button1); //, MessageBoxOptions.DefaultDesktopOnly);
            //    }
            //}
            //else
            //    MessageBox.Show("Acquisition is nothing !", "", MessageBoxButtons.OK, MessageBoxIcon.Error, MessageBoxDefaultButton.Button1); //, MessageBoxOptions.DefaultDesktopOnly);

            //// To ensure releasing of local references to Hexsight ActiveX objects 
            //lAcquisition = null;
            ////GC.Collect();
            ////GC.WaitForPendingFinalizers();
        }
        */

        /*
        // Occurs when the connection to a camera device is opened.
        private void OnCameraOpened1(Object sender, EventArgs e)
        {
            if (InvokeRequired) {
                // If called from a different thread, we must use the Invoke method to marshal the call to the proper thread.
                BeginInvoke(new EventHandler<EventArgs>(OnCameraOpened1), sender, e);
                return;
            }
            // The image provider is ready to grab. Enable the grab buttons.
            //EnableButtons(true, false);
        }
        */

        /*
        private void OnCameraOpened2(Object sender, EventArgs e)
        {
            if (InvokeRequired) {
                // If called from a different thread, we must use the Invoke method to marshal the call to the proper thread.
                BeginInvoke(new EventHandler<EventArgs>(OnCameraOpened2), sender, e);
                return;
            }
            // The image provider is ready to grab. Enable the grab buttons.
            //EnableButtons(true, false);
        }
        */

        /*
        // Occurs when the connection to a camera device is closed.
        private void OnCameraClosed1(Object sender, EventArgs e)
        {
            if (InvokeRequired) {
                // If called from a different thread, we must use the Invoke method to marshal the call to the proper thread.
                BeginInvoke(new EventHandler<EventArgs>(OnCameraClosed1), sender, e);
                return;
            }
            // The camera connection is closed. Disable all buttons.
            //EnableButtons(false, false);
        }
        */

        /*
        private void OnCameraClosed2(Object sender, EventArgs e)
        {
            if (InvokeRequired) {
                // If called from a different thread, we must use the Invoke method to marshal the call to the proper thread.
                BeginInvoke(new EventHandler<EventArgs>(OnCameraClosed2), sender, e);
                return;
            }
            // The camera connection is closed. Disable all buttons.
            //EnableButtons(false, false);
        }
        */
        
        private void OnGrabStarted1(Object sender, EventArgs e)
        {

            GrabStarted?.Invoke(this, EventArgs.Empty);

            //if (InvokeRequired)
            //{
            //    // If called from a different thread, we must use the Invoke method to marshal the call to the proper thread.
            //    BeginInvoke(new EventHandler<EventArgs>(OnGrabStarted1), sender, e);
            //    return;
            //}

            //// Reset the stopwatch used to reduce the amount of displayed images. The camera may acquire images faster than the images can be displayed.
            //stopWatchLive1.Reset();

            //// Do not update the device list while grabbing to reduce jitter. Jitter may occur because the GUI thread is blocked for a short time when enumerating.
            //updateDeviceListTimer.Stop();

            //// The camera is grabbing. Disable the grab buttons. Enable the stop button.
            ////EnableButtons(false, true);
        }
        

        // Occurs when a camera starts grabbing.
        /*
        private void OnGrabStarted2(Object sender, EventArgs e)
        {
            //if (InvokeRequired) {
            //    // If called from a different thread, we must use the Invoke method to marshal the call to the proper thread.
            //    BeginInvoke(new EventHandler<EventArgs>(OnGrabStarted2), sender, e);
            //    return;
            //}

            //// Reset the stopwatch used to reduce the amount of displayed images. The camera may acquire images faster than the images can be displayed.
            //stopWatchLive2.Reset();

            //// Do not update the device list while grabbing to reduce jitter. Jitter may occur because the GUI thread is blocked for a short time when enumerating.
            //updateDeviceListTimer.Stop();

            //// The camera is grabbing. Disable the grab buttons. Enable the stop button.
            ////EnableButtons(false, true);
        }
        */

        //bool bGrab = false;

        // Occurs when an image has been acquired and is ready to be processed.
        
        private void OnImageGrabbed1(Object sender, ImageGrabbedEventArgs e)
        {
            //if (bGrab) return;
            //try {
            //    if (tabControl1.SelectedTab != tabControl1.TabPages[0]) return;
            //}
            //catch { }
            //bGrab = true;

            //if (InvokeRequired) {
            //    // If called from a different thread, we must use the Invoke method to marshal the call to the proper GUI thread.
            //    // The grab result will be disposed after the event call. Clone the event arguments for marshaling to the GUI thread.
            //    BeginInvoke(new EventHandler<ImageGrabbedEventArgs>(OnImageGrabbed1), sender, e.Clone());
            //    //bGrab = false;
            //    return;
            //}

            try {
                // Acquire the image from the camera. Only show the latest image. The camera may acquire images faster than the images can be displayed.

                // Get the grab result.
                IGrabResult grabResult = e.GrabResult;

                // Check if the image can be displayed.
                if (grabResult.IsValid) {
                    // Reduce the number of displayed images to a reasonable amount if the camera is acquiring images very fast.
                    if (!stopWatchLive1.IsRunning || stopWatchLive1.ElapsedMilliseconds > 100) { //33
                        stopWatchLive1.Restart();

 //                       if (tabControl1.SelectedTab != tabControl1.TabPages[0]) return;

                        Bitmap bitmap = new Bitmap(grabResult.Width, grabResult.Height, PixelFormat.Format32bppRgb);
                        // Lock the bits of the bitmap.
                        BitmapData bmpData = bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height), ImageLockMode.ReadWrite, bitmap.PixelFormat);
                        // Place the pointer to the buffer of the bitmap.
                        converter.OutputPixelFormat = PixelType.BGRA8packed;
                        IntPtr ptrBmp = bmpData.Scan0;
                        converter.Convert(ptrBmp, bmpData.Stride * bitmap.Height, grabResult); //Exception handling TODO
                        bitmap.UnlockBits(bmpData);

                        //if (form2.optP2.Checked || form2.optP3.Checked || form2.optP4.Checked) {
                        //    bitmap.RotateFlip(RotateFlipType.Rotate180FlipNone);
                        //}

                        // Assign a temporary variable to dispose the bitmap after assigning the new bitmap to the display control.
                        Bitmap bitmapOld = null;

                        //if (tabControl1.SelectedIndex == 0) {
                        //    bitmapOld = pct1live.Image as Bitmap;
                        //    // Provide the display control with the new bitmap. This action automatically updates the display.
                        //    pct1live.Image = bitmap;
                        //}
                        //else 
                        //if (true) { 
                        if (frmBeckhoff.mFormBeckhoffDefInstance.tabControl1.SelectedTab == frmBeckhoff.mFormBeckhoffDefInstance.tabControl1.TabPages[0]) { //if (tabControl1.SelectedIndex == 0) 
                            bitmapOld = frmBeckhoff.mFormBeckhoffDefInstance.pct1liveTab.Image as Bitmap;
                            frmBeckhoff.mFormBeckhoffDefInstance.pct1liveTab.Image = bitmap;

                            if (frmBeckhoff.mFormBeckhoffDefInstance.chkShowCross.Checked || frmBeckhoff.mFormBeckhoffDefInstance.chkUseSearchArea.Checked) {
                                using (Graphics g = Graphics.FromImage(frmBeckhoff.mFormBeckhoffDefInstance.pct1liveTab.Image)) {
                                    g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                                    int PenSize = 4;
                                    Color selectedClr = Color.Red;

                                    if (frmBeckhoff.mFormBeckhoffDefInstance.chkShowCross.Checked) {
                                        Point p1 = new Point(0, Convert.ToInt16(bitmap.Height / 2));
                                        Point p2 = new Point(bitmap.Width, Convert.ToInt16(bitmap.Height / 2));
                                        g.DrawLine(new Pen(selectedClr, PenSize), p1, p2);
                                        p1 = new Point(Convert.ToInt16(bitmap.Width / 2), 0);
                                        p2 = new Point(Convert.ToInt16(bitmap.Width / 2), bitmap.Height);
                                        g.DrawLine(new Pen(selectedClr, PenSize), p1, p2);
                                    }

                                    int w = 0, h = 0;
                                    if (frmBeckhoff.mFormBeckhoffDefInstance.txtSearchAreaWidth.Text.Trim() != "") w = Convert.ToInt16(frmBeckhoff.mFormBeckhoffDefInstance.txtSearchAreaWidth.Text);
                                    if (frmBeckhoff.mFormBeckhoffDefInstance.txtSearchAreaHeight.Text.Trim() != "") h = Convert.ToInt16(frmBeckhoff.mFormBeckhoffDefInstance.txtSearchAreaHeight.Text);
                                    if (frmBeckhoff.mFormBeckhoffDefInstance.chkUseSearchArea.Checked && w > 0 && h > 0) {
                                        //float zf = 1;
                                        //double unscaledx = 0, unscaledy = 0;
                                        //zf = (float)ZoomFactorLive(0, 0, out unscaledx, out unscaledy);
                                        //g.DrawRectangle(new Pen(selectedClr, PenSize),
                                        //    zf * (pct1liveTab.Width - w) / 2, zf * (pct1liveTab.Height - h) / 2, w * zf, h * zf);

                                        Point FirstPoint = new Point(Convert.ToInt16(frmBeckhoff.mFormBeckhoffDefInstance.txtRectPointX.Text), Convert.ToInt16(frmBeckhoff.mFormBeckhoffDefInstance.txtRectPointY.Text));
                                        Point SecondPoint = new Point(Convert.ToInt16(frmBeckhoff.mFormBeckhoffDefInstance.txtRectPointX.Text) + Convert.ToInt16(frmBeckhoff.mFormBeckhoffDefInstance.txtSearchAreaWidth.Text),
                                                                    Convert.ToInt16(frmBeckhoff.mFormBeckhoffDefInstance.txtRectPointY.Text) + Convert.ToInt16(frmBeckhoff.mFormBeckhoffDefInstance.txtSearchAreaHeight.Text));
                                        g.FillEllipse(new SolidBrush(selectedClr), SecondPoint.X - PenSize / 2, SecondPoint.Y - PenSize / 2, PenSize, PenSize);
                                        g.DrawRectangle(new Pen(selectedClr, PenSize), FirstPoint.X, FirstPoint.Y, SecondPoint.X - FirstPoint.X, SecondPoint.Y - FirstPoint.Y);
                                    }
                                    //pct1liveTab.Refresh();
                                }
                            }
                        }

                        if (bitmapOld != null) {
                            // Dispose the bitmap.
                            bitmapOld.Dispose();
                        }

                        frmBeckhoff.mFormBeckhoffDefInstance.bSnapResult = true;
                    }
                }
            }
            catch (Exception exception) {
                if (frmBeckhoff.mFormBeckhoffDefInstance.tabControl1.SelectedTab == frmBeckhoff.mFormBeckhoffDefInstance.tabControl1.TabPages[0]) {
                    ShowException("OnImageGrabbed camera 1 ", exception);
                }
            }
            finally {
                // Dispose the grab result if needed for returning it to the grab loop.
                e.DisposeGrabResultIfClone();
            }

            //bGrab = false;
        }
        

        /*
        private void OnImageGrabbed2(Object sender, ImageGrabbedEventArgs e)
        {
            //if (InvokeRequired) {
            //    // If called from a different thread, we must use the Invoke method to marshal the call to the proper GUI thread.
            //    // The grab result will be disposed after the event call. Clone the event arguments for marshaling to the GUI thread.
            //    BeginInvoke(new EventHandler<ImageGrabbedEventArgs>(OnImageGrabbed2), sender, e.Clone());
            //    return;
            //}

            //try {
            //    // Acquire the image from the camera. Only show the latest image. The camera may acquire images faster than the images can be displayed.

            //    // Get the grab result.
            //    IGrabResult grabResult = e.GrabResult;

            //    // Check if the image can be displayed.
            //    if (grabResult.IsValid) {
            //        // Reduce the number of displayed images to a reasonable amount if the camera is acquiring images very fast.
            //        if (!stopWatchLive2.IsRunning || stopWatchLive2.ElapsedMilliseconds > 100) //33
            //        {
            //            stopWatchLive2.Restart();

            //            Bitmap bitmap = new Bitmap(grabResult.Width, grabResult.Height, PixelFormat.Format32bppRgb);
            //            // Lock the bits of the bitmap.
            //            BitmapData bmpData = bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height), ImageLockMode.ReadWrite, bitmap.PixelFormat);
            //            // Place the pointer to the buffer of the bitmap.
            //            converter.OutputPixelFormat = PixelType.BGRA8packed;
            //            IntPtr ptrBmp = bmpData.Scan0;
            //            converter.Convert(ptrBmp, bmpData.Stride * bitmap.Height, grabResult); //Exception handling TODO
            //            bitmap.UnlockBits(bmpData);

            //            // Assign a temporary variable to dispose the bitmap after assigning the new bitmap to the display control.
            //            Bitmap bitmapOld = null; // = pct6liveTab.Image as Bitmap;
            //            //if (tabControl1.SelectedIndex == 0) {
            //            //    bitmapOld = pct2live.Image as Bitmap;
            //            //    // Provide the display control with the new bitmap. This action automatically updates the display.
            //            //    pct2live.Image = bitmap;
            //            //}
            //            //else
            //            if (tabControl1.SelectedIndex == 2) {
            //                bitmapOld = pct2liveTab.Image as Bitmap;
            //                pct2liveTab.Image = bitmap;
            //            }

            //            if (bitmapOld != null) {
            //                // Dispose the bitmap.
            //                bitmapOld.Dispose();
            //            }
            //        }
            //    }
            //}
            //catch (Exception exception) {
            //    ShowException("OnImageGrabbed camera 2 ", exception);
            //}
            //finally {
            //    // Dispose the grab result if needed for returning it to the grab loop.
            //    e.DisposeGrabResultIfClone();
            //}
        }
        */

        // Occurs when a camera has stopped grabbing.
        /*
        private void OnGrabStopped1(Object sender, GrabStopEventArgs e)
        {
            if (InvokeRequired) {
                // If called from a different thread, we must use the Invoke method to marshal the call to the proper thread.
                BeginInvoke(new EventHandler<GrabStopEventArgs>(OnGrabStopped1), sender, e);
                return;
            }

            stopWatchLive1.Reset(); // Reset the stopwatch.

            updateDeviceListTimer.Start(); // Re-enable the updating of the device list.

            // The camera stopped grabbing. Enable the grab buttons. Disable the stop button.
            //EnableButtons(true, false);

            // If the grabbed stop due to an error, display the error message.
            if (e.Reason != GrabStopReason.UserRequest) {
                MessageBox.Show("A grab error (camera 1) occured:\n" + e.ErrorMessage, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        */

        /*
        private void OnGrabStopped2(Object sender, GrabStopEventArgs e)
        {
            //if (InvokeRequired) {
            //    // If called from a different thread, we must use the Invoke method to marshal the call to the proper thread.
            //    BeginInvoke(new EventHandler<GrabStopEventArgs>(OnGrabStopped2), sender, e);
            //    return;
            //}

            //stopWatchLive2.Reset(); // Reset the stopwatch.

            //updateDeviceListTimer.Start(); // Re-enable the updating of the device list.

            //// The camera stopped grabbing. Enable the grab buttons. Disable the stop button.
            ////EnableButtons(true, false);

            //// If the grabbed stop due to an error, display the error message.
            //if (e.Reason != GrabStopReason.UserRequest) {
            //    MessageBox.Show("A grab error (camera 2) occured:\n" + e.ErrorMessage, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            //}
        }
        */
    }
}
