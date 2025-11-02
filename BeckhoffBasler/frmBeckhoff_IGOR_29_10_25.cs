using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Threading;
using System.IO;
using System.Drawing.Imaging;
using System.Drawing.Drawing2D;
using System.Diagnostics; // where the Stopwatch class is defined 
using System.Runtime.InteropServices;
using System.Globalization;
using System.Net;
using System.Net.NetworkInformation;
using System.IO.Ports;
using Basler.Pylon;
using RuntimeMultiGPU2;
using System.Net.Sockets;

using System.Windows.Forms.DataVisualization.Charting; //Add reference to the System.Windows.Forms.DataVisualization in the Solution Explorer panel.

using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;
using Emgu.CV.UI;
using Emgu.CV.Util;
using Emgu.CV.Dnn;

//Feature Detection
using Emgu.CV.Features2D;
using Emgu.CV.XFeatures2D;
using System.Diagnostics;
using System.Diagnostics.Eventing.Reader;
using System.Runtime.InteropServices.ComTypes;
using System.Windows.Media.Imaging;
using System.Security;
using System.Security.Policy;
using System.Reflection;
using System.Windows.Interop;
using System.Windows;
using BeckhoffBasler;
using System.Runtime.CompilerServices;

namespace Inspection
{
    public partial class frmBeckhoff : Form
    {
        public static frmBeckhoff mFormBeckhoffDefInstance;
        private static bool mInitializingDefInstance;
        public string aPath, sourcesPath;
        public static frmMain frmMainInspect;
        public static frmRunning frmRun;
        //public static frmFront frmFrontInspect;

        [StructLayout(LayoutKind.Sequential)]
        struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [DllImport("user32.dll", SetLastError = true)]
        //static extern IntPtr FindWindow(string lpClassName, string lpWindowName);
        //[DllImport("user32.dll", CharSet = CharSet.Auto)]
        static extern IntPtr SendMessage(IntPtr hWnd, UInt32 Msg, IntPtr wParam, IntPtr lParam);
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool GetWindowRect(IntPtr hwnd, out RECT lpRect);
        [DllImport("user32.dll")]
        //static extern IntPtr SetParent(IntPtr hWndChild, IntPtr hWndNewParent);
        //[DllImport("user32.dll")]
        //[return: MarshalAs(UnmanagedType.Bool)]
        static extern bool SetWindowPos(IntPtr hwnd, IntPtr hwndInsertAfter, int x, int y, int cx, int cy, uint uFlags);
        [DllImport("user32.dll")]
        static extern int SetWindowLong(IntPtr hWnd, int nIndex, uint dwNewLong);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool SetForegroundWindow(IntPtr hWnd);

        const UInt32 WM_CLOSE = 0x0010;
        IntPtr WindowToClose = IntPtr.Zero;
        static readonly IntPtr HWND_TOP = new IntPtr(0);
        public const int SWP_NOACTIVATE = 0x0010;
        public const int SWP_SHOWWINDOW = 0x0040;
        //public const UInt32 SWP_NOSIZE = 0x0001;
        public const UInt32 SWP_NOMOVE = 0x0002;

        public const UInt32 TOPMOST_FLAGS = SWP_NOMOVE | SWP_NOSIZE;
        public static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        public static readonly IntPtr HWND_NOTOPMOST = new IntPtr(-2);

        enum WindowLongFlags : int
        {
            GWL_EXSTYLE = -20,
            GWLP_HINSTANCE = -6,
            GWLP_HWNDPARENT = -8,
            GWL_ID = -12,
            GWL_STYLE = -16,
            GWL_USERDATA = -21,
            GWL_WNDPROC = -4,
            DWLP_USER = 0x8,
            DWLP_MSGRESULT = 0x0,
            DWLP_DLGPROC = 0x4
        }



        Stopwatch stopwatch = new Stopwatch();
        //private IniFiles.IniFile oIniFile;

        // ROI (Working)
        private float[,] fRecPointX = new float[6, 4]; // [cam,point] Working Search Area
        private float[,] fRecPointY = new float[6, 4];
        private float[] fWidth = new float[6]; // [cam] Size of ROI of Working Search Area
        private float[] fHeight = new float[6];

        // Current ROI (Working)
        private float[] fROIX = new float[6]; // [cam] Working Search Area X coordinate
        private float[] fROIY = new float[6]; // [cam] Working Search Area Y coordinate

        private int nCurrROIAC = -1; // Set in btnCalibrate_Click and TrayAlignment (for (int i = 0; i < 4; i++) {nCurrROIAC = i;btnInspect_Click(btnInspect1, e);}), and  in btnSearchArea1_Click (nCurrROIAC=-1)
        private bool bMousePressed = false;
        private System.Drawing.Point lastPoint = System.Drawing.Point.Empty;
        private Color selectedClr = Color.FromArgb(255, 0, 255, 0); //green 

        private bool m_bLayoutCalled = false;
        private DateTime m_dt;

        #region Basler live
        // Camera Top
        public string sFriendlyName1 = ""; //"23534683"; //"22750713"
        public Camera camera1 = null;
        //public int nExposure1 = 0;
        private int nExposureInspection = 0;
        private int nExposureDiameter = 0;

        // Camera Side
        public string sFriendlyName2 = "";
        public Camera camera2 = null;
        public int nExposure2 = 0;


        public PixelDataConverter converter1 = new PixelDataConverter();
        public Stopwatch stopWatchLive1 = new Stopwatch();
        public PixelDataConverter converter = new PixelDataConverter();

        public bool bSnapResult = false;
        public bool bSnapProcReady = false;
        #endregion

        public bool bDebugMode = false;

        private bool bfrmBeckhoff_Shown = false;

        private bool bShowMsgBox = false;

        public Task<int> taskcamera1;

        Color bc = Color.FromArgb(154, 206, 244); // 0xF4CE9A;
        Color bc_err = Color.Red;
        float Shift = 10;

        Bitmap image1, processed, display;

        //private Point beginDraw;
        //Pen pen;
        //Graphics g;
        //Bitmap map, map0;

        System.Drawing.Imaging.ImageCodecInfo ici;
        System.Drawing.Imaging.EncoderParameters myEncoderParameters;

        #region ini file
        private string sIniFile;
        private string sCamera;
        private double mmpix = 0.02; //0.02

        private int nROI_left;
        private int nROI_top;
        private int nROI_width;
        private int nROI_height;

        #endregion

        BaslerLive bl = new BaslerLive();

        public struct StrWeldon
        {
            public double Diam;
            public double WeldonTop;
            public double WeldonButtom;
            public double RightEdge;
            public double X0;
            public double Y0;
            public double W;
            public double H;
            public bool bErr;
        }

        public struct StrHist
        {
            public double R_Gr;
            public double G_Gr;
            public double B_Gr;
            public double Gr;
            public bool bErr;
            public bool bNormal;
        }

        public struct TasksParameters_GetLine
        {
            public bool berr;
            public double yc;
            public double Angle;
            public string sException;
            public int x;
            public int y;
            public int width;
            public int height;
        }

        public struct TasksParameters_Snap
        {
            public int x;
            public int y;
            public int width;
            public int height;
            public string sException;
            public int rc;
        }

        public bool bCycle = false;

        public bool bWeldonCycle = false;
        public int nWeldonCycleEmul = 0;

        //public static frmMain mFrmMain;

        Beckhoff Beckhoff_Cam1 = new Beckhoff();
        Beckhoff Beckhoff_Cam2 = new Beckhoff();
        Beckhoff Beckhoff_Gen = new Beckhoff();

        //public static frmMain mFrmMain = new frmMain();

        public string PlcNetID = "5.68.201.84.1.1";
        public int PlcPort = 851;
        public int StartAddressSendCam1 = 10;
        public int StartAddressSendCam2 = 110;
        public int StartAddressSendGen = 210;

        int Axis = 0;
        int Device = 0;
        Single Speed;
        public bool AxisMove = false;


        public frmBeckhoff()
            : base()
        {

            this.DoubleBuffered = true;
            if (mFormBeckhoffDefInstance == null)
            {
                if (mInitializingDefInstance)
                    mFormBeckhoffDefInstance = this;
                else
                    try
                    {
                        // For the start-up form, the first instance created is the default instance
                        //if (System.Reflection.Assembly.GetExecutingAssembly().EntryPoint.DeclaringType == this.GetType()) 
                        mFormBeckhoffDefInstance = this;
                    }
                    catch { }
            }
            InitializeComponent();
            //this.SetStyle(
            //           ControlStyles.AllPaintingInWmPaint |
            //           ControlStyles.UserPaint |
            //           //ControlStyles.OptimizedDoubleBuffer |
            //           ControlStyles.DoubleBuffer,
            //           true);
            //this.SetStyle(ControlStyles.ResizeRedraw | 
            //    ControlStyles.UserPaint | 
            //    ControlStyles.OptimizedDoubleBuffer | 
            //    ControlStyles.AllPaintingInWmPaint | 
            //    ControlStyles.SupportsTransparentBackColor, true);
            //this.UpdateStyles();
            //BufferedPanel ds = new BufferedPanel();
            //panel1 = ds;

            //CreateParams cp = base.CreateParams;
            //cp.ExStyle |= 0x02000000;  // Turn on WS_EX_COMPOSITED

            //frmMainInspect.ListClicked += ListSelected;
            //MainHMI.frmHmi.Controls.Add(this);
        }
        public class BufferedPanel : Panel
        {
            public BufferedPanel()
            {
                SetStyle(ControlStyles.ResizeRedraw |
                ControlStyles.UserPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.SupportsTransparentBackColor, true);
                UpdateStyles();
            }
            protected override void OnPaint(PaintEventArgs e)
            {
                base.OnPaint(e);
            }
        }
        //protected override CreateParams CreateParams
        //{
        //    get
        //    {
        //        CreateParams cp = base.CreateParams;
        //        cp.ExStyle |= 0x02000000;  // Turn on WS_EX_COMPOSITED
        //        return cp;
        //    }
        //}

        public static frmBeckhoff DefInstance
        {
            get {
                frmBeckhoff defInstanceReturn = null;
                if (mFormBeckhoffDefInstance == null || mFormBeckhoffDefInstance.IsDisposed) {
                    mInitializingDefInstance = true;
                    mFormBeckhoffDefInstance = new frmBeckhoff();
                    mInitializingDefInstance = false;
                }
                defInstanceReturn = mFormBeckhoffDefInstance;
                return defInstanceReturn;
            }
            set {
                mFormBeckhoffDefInstance = value;
            }
        }

        public int PictMode = 0;
        public string RejectString = "";
        //bool listSelected = false;

        private void onExposureChangedFromCatalogueNumber(object sender, frmMain.CustomEventArgInt e)
        {
            UpdateTopInpectionExposureTime(e.iint);
        }

        private void onExposureChangedFromBeckofForm(object sender, frmMain.CustomEventArgIntRef e)
        {
            e.Value = nExposureInspection;
        }

        private void ListSelected(object sender, frmMain.MyEventArg e)
        {
            string txt = e.txt;
            //"03 1 Break: Score=0.636717 Area=19 Perimeter=16.5 Ourer=14 X0=2354.1 Y0=748.9 H=5 W=5"
            string[] s = txt.Split(' ');
            int num = 0;
            int num1 = 0;
            bool ok = int.TryParse(s[0], out num);
            if (ok)
            {
                //listSelected = true;
                chkStretchImage2.Checked = true;
                tabControl1.SelectedTab = tabControl1.TabPages[1];
                //NPNP don't strech unless pressing the strech button
                pctSnap.SizeMode = PictureBoxSizeMode.StretchImage;//
                //pctSnap.SizeMode = PictureBoxSizeMode.Zoom;//
                pctSnap.Width = panel1liveTab.Width - 2; //930
                pctSnap.Height = panel1liveTab.Height - 2; //698

                pctSnap.Height = (int)(pctSnap.Width * 3648.0f / 5472.0f);

                //pctSnap.SizeMode = PictureBoxSizeMode.StretchImage;
                pctSnap.Image = null;

                switch (num)
                {
                    case 1:
                        opt1.Checked = true;
                        if (pct1.Image is null) return;
                        pctSnap.Image = pct1.Image;
                        frmBeckhoff.frmMainInspect.pictureBoxInspect.Image = pct1.Image;
                        break;
                    case 2:
                        opt2.Checked = true;
                        if (pct2.Image is null) return;
                        pctSnap.Image = pct2.Image;
                        frmBeckhoff.frmMainInspect.pictureBoxInspect.Image = pct2.Image;
                        break;
                    case 3:
                        opt3.Checked = true;
                        if (pct3.Image is null) return;
                        pctSnap.Image = pct3.Image;
                        frmBeckhoff.frmMainInspect.pictureBoxInspect.Image = pct3.Image;
                        break;
                    case 4:
                        opt4.Checked = true;
                        if (pct4.Image is null) return;
                        pctSnap.Image = pct4.Image;
                        frmBeckhoff.frmMainInspect.pictureBoxInspect.Image = pct4.Image;
                        break;
                    case 5:
                        opt5.Checked = true;
                        if (pct5.Image is null) return;
                        pctSnap.Image = pct5.Image;
                        frmBeckhoff.frmMainInspect.pictureBoxInspect.Image = pct5.Image;
                        break;
                    case 6:
                        opt6.Checked = true;
                        if (pct6.Image is null) return;
                        pctSnap.Image = pct6.Image;
                        frmBeckhoff.frmMainInspect.pictureBoxInspect.Image = pct6.Image;
                        break;
                    case 7:
                        opt7.Checked = true;
                        if (pct7.Image is null) return;
                        pctSnap.Image = pct7.Image;
                        frmBeckhoff.frmMainInspect.pictureBoxInspect.Image = pct7.Image;
                        break;
                    case 8:
                        opt8.Checked = true;
                        if (pct8.Image is null) return;
                        pctSnap.Image = pct8.Image;
                        frmBeckhoff.frmMainInspect.pictureBoxInspect.Image = pct8.Image;
                        break;
                    case 9:
                        opt9.Checked = true;
                        if (pct9.Image is null) return;
                        pctSnap.Image = pct9.Image;
                        frmBeckhoff.frmMainInspect.pictureBoxInspect.Image = pct9.Image;
                        break;
                    case 10:
                        opt10.Checked = true;
                        if (pct10.Image is null) return;
                        pctSnap.Image = pct10.Image;
                        frmBeckhoff.frmMainInspect.pictureBoxInspect.Image = pct10.Image;
                        break;
                    case 11:
                        opt11.Checked = true;
                        if (pct11.Image is null) return;
                        pctSnap.Image = pct11.Image;
                        frmBeckhoff.frmMainInspect.pictureBoxInspect.Image = pct11.Image;
                        break;
                    case 12:
                        opt12.Checked = true;
                        if (pct12.Image is null) return;
                        pctSnap.Image = pct12.Image;
                        frmBeckhoff.frmMainInspect.pictureBoxInspect.Image = pct12.Image;
                        break;
                    case 13:
                        opt13.Checked = true;
                        if (pct13.Image is null) return;
                        pctSnap.Image = pct13.Image;
                        frmBeckhoff.frmMainInspect.pictureBoxInspect.Image = pct13.Image;
                        break;
                    case 14:
                        opt14.Checked = true;
                        if (pct14.Image is null) return;
                        pctSnap.Image = pct14.Image;
                        frmBeckhoff.frmMainInspect.pictureBoxInspect.Image = pct14.Image;
                        break;
                    case 15:
                        opt15.Checked = true;
                        if (pct15.Image is null) return;
                        pctSnap.Image = pct15.Image;
                        frmBeckhoff.frmMainInspect.pictureBoxInspect.Image = pct15.Image;
                        break;
                    case 16:
                        opt16.Checked = true;
                        if (pct16.Image is null) return;
                        pctSnap.Image = pct16.Image;
                        frmBeckhoff.frmMainInspect.pictureBoxInspect.Image = pct16.Image;
                        break;
                    default: return;
                }
                indexnum = num - 1;
                ImageUpdate = true;
                PictMode = 1;

                RejectString = txt;
                frmBeckhoff.frmMainInspect.pictureBoxInspect.Refresh();
                pctSnap.Refresh();

                //if (frmMainInspect.chkROI1.Checked) Reject_Paint(null, null, 1);
                //if (frmMainInspect.chkROI2.Checked) Reject_Paint(null, null, 2);
                //if (frmMainInspect.chkROI3.Checked) Reject_Paint(null, null, 3);
            }
        }

        //NPNP
        public void SetNumBufferSize(int iNumBufferSize)
        {
            inv.set(numBufferSize, "text", iNumBufferSize);
        }

        private async void btnPlus_Click(object sender, EventArgs e)
        {
            try
            {
                if (chkFine.Checked)
                {
                    btn_status(false);
                    if (trackBarSpeedSt.Value > 10) trackBarSpeedSt.Value = 10;
                    Single dist = 0;
                    inv.set(btnPlus, "Enabled", false);
                    string[] s = cmbAxes.Text.Split(':');
                    Axis = int.Parse(s[0]);
                    int axis = Axis;
                    Speed = 1000;
                    Single speed = Speed * Single.Parse(txtSpeedSt.Text) / 100;

                    dist = Single.Parse(cmbFine.Text);

                    var task1 = Task.Run(() => MoveRelSt(axis, dist, speed));
                    await task1;

                    CommReply reply = new CommReply();
                    reply.result = false;
                    reply = task1.Result;
                    inv.settxt(txtCurrPosCams, reply.data[4].ToString("0.000"));

                    if (!(reply.status == "" || reply.status == null)) {
                        MessageBox.Show("ERROR MOVE FINE! " + "\r" + reply.status);
                        btn_status(true);
                        inv.set(btnPlus, "Enabled", true);
                        return;
                    }
                    if (reply.data[1] != 0) { MessageBox.Show("ERROR MOVE"); return; };

                    btn_status(true);
                    inv.set(btnPlus, "Enabled", true);
                }
            }
            catch (Exception ex) {
                MessageBox.Show("ERROR MOVE FINE! " + ex.Message, "ERROR", MessageBoxButtons.OK,
                     MessageBoxIcon.Warning, MessageBoxDefaultButton.Button1, MessageBoxOptions.DefaultDesktopOnly);
                btn_status(true);
                inv.set(btnPlus, "Enabled", true);
            }
        }

        private async void btnPlus_MouseDown(object sender, MouseEventArgs e)
        {
            try {
                if (chkFine.Checked) return;
                AxisMove = true;
                int device = 1;
                int direction = 1;
                string[] s = cmbAxes.Text.Split(':');
                Axis = int.Parse(s[0]);
                int axis = Axis;
                Speed = 100;
                float speed = Speed * Single.Parse(txtSpeedSt.Text) / 100;

                var task1 = Task.Run(() => RunStations_Jog(device, axis, direction, speed));
                await Task.WhenAll(task1);
                CommReply reply = new CommReply();
                reply.result = false;
                reply = task1.Result;
            }
            catch (Exception ex) {
                CommReply reply = new CommReply();
                reply.result = false;
                reply.comment = ex.Message;
            }
        }

        private async void btnPlus_MouseLeave(object sender, EventArgs e)
        {
            try
            {
                //stop
                if (chkFine.Checked) return;
                if (AxisMove) {
                    btn_status(true);
                    AxisMove = false;
                    int device = 0;
                    int axis = 0;

                    var task1 = Task.Run(() => StopStations_Jog(device, axis));
                    await task1;

                    Thread.Sleep(100);
                    //var task2 = Task.Run(() => ReadCurrent());
                    //await task2;
                }
            }
            catch (Exception ex) { }
        }

        private async void btnPlus_MouseUp(object sender, MouseEventArgs e)
        {
            try
            {
                if (chkFine.Checked) return;
                btn_status(true);
                AxisMove = false;
                int device = 0;
                int axis = 0;

                var task1 = Task.Run(() => StopStations_Jog(device, axis));
                await task1;
                Thread.Sleep(100);
                //var task2 = Task.Run(() => ReadCurrent());
                //await task2;
            }
            catch (Exception ex) { }
        }

        delegate void btn_statusInvoked(Boolean status, Boolean falseonly);
        public void btn_status(bool status, bool falseonly = false)
        {
            //enable/disable buttons
            try {
                if (!InvokeRequired) {
                    //btnRotationFromCamera.Enabled = status;
                    btnPlus.Enabled = status;
                    btnMin.Enabled = status;
                    btnCurrPosCams.Enabled = status;
                    btnMoveSt.Enabled = status;
                    btnPwrOnSt.Enabled = status;
                } else {
                    Invoke(new btn_statusInvoked(btn_status), new object[] { status, falseonly });
                }
            }
            catch (Exception ex) { }
        }

        private async Task<CommReply> MoveRelSt(int station, Single dist, Single speed)
        {
            CommReply reply = new CommReply();
            Beckhoff.SendPlcParms ParmsPlc = new Beckhoff.SendPlcParms();
            reply.result = false;
            Array.Resize<Single>(ref ParmsPlc.SendParm, 11);
            for (int i = 0; i < ParmsPlc.SendParm.Length; i++) ParmsPlc.SendParm[i] = 0;

            ParmsPlc.SendParm[0] = MyStatic.CamsCmd.MoveRel;
            ParmsPlc.SendParm[1] = station;
            ParmsPlc.SendParm[2] = dist;
            ParmsPlc.SendParm[3] = speed;
            ParmsPlc.SendParm[10] = 5f;//tmout

            var task1 = Task.Run(() => Beckhoff_Gen.PlcSendCmd(StartAddressSendGen, ParmsPlc));
            await task1;

            ParmsPlc.SendParm = null;
            //wait fini async
            reply = task1.Result;
            return reply;
        }

        private async Task<CommReply> StopStations_Jog(int device, Single ax)//global stop
        {
            try
            {
                CommReply reply = new CommReply();
                Beckhoff.SendPlcParms ParmsPlc = new Beckhoff.SendPlcParms();
                reply.result = false;
                Array.Resize<Single>(ref ParmsPlc.SendParm, 11);
                for (int i = 0; i < ParmsPlc.SendParm.Length; i++) ParmsPlc.SendParm[i] = 0;
                //move jog

                ParmsPlc.SendParm[0] = MyStatic.CamsCmd.Stop;//stop
                //ParmsPlc.SendParm[1] = device;//device
                ParmsPlc.SendParm[1] = ax;//x=1 y=2 z=3 all=0
                ParmsPlc.SendParm[10] = 0.5f;//tmout
                ax = 1;
                var task1 = Task.Run(() => Beckhoff_Gen.PlcSendCmd(StartAddressSendGen, ParmsPlc, true));
                await task1;
                ParmsPlc.SendParm = null;
                //wait fini async
                reply = task1.Result;
                return reply;
            }
            catch (Exception ex) {
                CommReply reply = new CommReply();
                reply.result = false;
                reply.comment = ex.Message;
                return reply;
            }
        }

        private async Task<CommReply> RunStations_Jog(int device, Single ax, Single dir, Single speed)//global power on
        {
            try
            {
                CommReply reply = new CommReply();
                Beckhoff.SendPlcParms ParmsPlc = new Beckhoff.SendPlcParms();
                reply.result = false;
                Array.Resize<Single>(ref ParmsPlc.SendParm, 11);
                for (int i = 0; i < ParmsPlc.SendParm.Length; i++) ParmsPlc.SendParm[i] = 0;
                //move jog

                ParmsPlc.SendParm[0] = MyStatic.CamsCmd.MoveVel;//move vel
                //ParmsPlc.SendParm[1] = device;//move vel
                ParmsPlc.SendParm[1] = ax;//x=1 y=2 z=3
                ParmsPlc.SendParm[2] = dir;//0=negative 1=positive
                ParmsPlc.SendParm[3] = speed;//speed
                ParmsPlc.SendParm[10] = 0.5f;//tmout
                while (AxisMove)
                {
                    var task1 = Task.Run(() => Beckhoff_Gen.PlcSendCmd(StartAddressSendGen, ParmsPlc, false));
                    await task1;

                    reply = task1.Result;
                    if (!AxisMove) { break; }
                    Thread.Sleep(100);
                }
                ParmsPlc.SendParm = null;
                device = 0;
                int axis = 0;
                var task2 = Task.Run(() => StopStations_Jog(device, axis));

                return reply;
            }
            catch (Exception ex) {
                CommReply reply = new CommReply();
                reply.result = false;
                reply.comment = ex.Message;
                return reply;
            }
        }

        private async Task<CommReply> MoveAbs(int device, Single ax, Single Coord, Single speed)//global power on
        {
            try
            {
                CommReply reply = new CommReply();
                Beckhoff.SendPlcParms ParmsPlc = new Beckhoff.SendPlcParms();
                reply.result = false;
                Array.Resize<Single>(ref ParmsPlc.SendParm, 11);
                for (int i = 0; i < ParmsPlc.SendParm.Length; i++) ParmsPlc.SendParm[i] = 0;
                //move jog

                ParmsPlc.SendParm[0] = MyStatic.CamsCmd.MoveAbs;
                //ParmsPlc.SendParm[1] = device;//move vel
                ParmsPlc.SendParm[1] = ax;//x=1 y=2 z=3
                ParmsPlc.SendParm[2] = Coord;//0=negative 1=positive
                ParmsPlc.SendParm[3] = speed;//speed
                ParmsPlc.SendParm[10] = 10.5f;//tmout
                AddList("Beckhoff<= " + "Move Abs:" + MyStatic.CamsCmd.MoveAbs.ToString() + " ax=" + ax.ToString() + " coord=" + Coord.ToString() + " speed=" + speed.ToString() + " //" + DateTime.Now.ToString("HH:mm:ss.fff"));

                var task1 = Task.Run(() => Beckhoff_Gen.PlcSendCmd(StartAddressSendGen, ParmsPlc, true));
                await task1;

                reply = task1.Result;
                AddList("Beckhoff=> " + "res:" + "[0]" + reply.data[0].ToString() + "[1]" + reply.data[1].ToString() + "[2]" + reply.data[2].ToString() + "[3]" + reply.data[3].ToString() +
                   "[4]" + reply.data[4].ToString() + "[5]" + reply.data[5].ToString() + "[6]" + reply.data[6].ToString() + " //" + DateTime.Now.ToString("HH:mm:ss.fff"));
                //Thread.Sleep(10);

                ParmsPlc.SendParm = null;
                device = 0;
                int axis = 0;

                return reply;
            }
            catch (Exception ex) {
                CommReply reply = new CommReply();
                reply.result = false;
                reply.comment = ex.Message;
                return reply;
            }
        }

        private void trackBarSpeedSt_ValueChanged(object sender, EventArgs e)
        {
            txtSpeedSt.Text = trackBarSpeedSt.Value.ToString();
        }

        private async void btnCurrPosCams_Click(object sender, EventArgs e)
        {
            //current position
            try
            {
                btn_status(false);
                var task = Task.Run(() => CurrPos());
                await task;
                bool reply = task.Result;
                btn_status(true);

                if (!reply) {
                    MessageBox.Show("ERROR READ COORDINATES! " + "\r");
                    return;
                }
            }
            catch (Exception ex) {
                CommReply reply = new CommReply();
                reply.result = false;
                reply.comment = ex.Message;
            }
        }
        private async Task<bool> CurrPos()
        {
            //current position
            try
            {
                //btn_status(false);
                Single x = 0;
                Single y = 0;
                Single z = 0;

                //inv.settxt(txtCurrPosCams, "");
                //upDownControlPosSt.UpDownValue = 0.0f;
                inv.set(upDownControlPosSt, "UpDownValue", 0.0f);
                //Axis = int.Parse(cmbAxes.Text);
                int axis = Axis;
                var task1 = Task.Run(() => RunCurrPosCams(axis));

                await task1;
                CommReply reply = new CommReply();
                reply.result = false;
                reply = task1.Result;
                btn_status(true);

                if (!(reply.status == "" || reply.status == null))
                {
                    //MessageBox.Show("ERROR READ COORDINATES! " + "\r" + reply.status);
                    return false;
                }
                if (axis == 1)
                {
                    inv.set(upDownControlPosSt, "UpDownValue", Single.Parse(reply.data[2].ToString("0.000")));
                }
                else if (axis == 2)
                {
                    inv.set(upDownControlPosSt, "UpDownValue", Single.Parse(reply.data[3].ToString("0.000")));
                }
                else if (axis == 3)
                {
                    inv.set(upDownControlPosSt, "UpDownValue", Single.Parse(reply.data[4].ToString("0.000")));
                }
                else if (axis == 4)
                {
                    inv.set(upDownControlPosSt, "UpDownValue", Single.Parse(reply.data[5].ToString("0.000")));
                }
                else if (axis == 5)
                {
                    inv.set(upDownControlPosSt, "UpDownValue", Single.Parse(reply.data[6].ToString("0.000")));
                }
                return true;
            }
            catch (Exception ex)
            {
                CommReply reply = new CommReply();
                reply.result = false;
                reply.comment = ex.Message;
                return false;
            }
        }

        private async void btnPwrOnSt_Click(object sender, EventArgs e)
        {
            try
            {
                Speed = 1000;
                if (((Button)sender).Name == "btnPwrOnSt")
                {
                    btn_status(false);

                    btnPwrOnSt.Enabled = false;
                    int axis = Axis;
                    if (chkAllAxises.Checked) axis = 0;
                    var task1 = Task.Run(() => RunPwrSt(true, axis));
                    await task1;

                    MyStatic.bPower = false;

                    btnPwrOnSt.Enabled = true;
                    CommReply reply = new CommReply();
                    reply.result = false;
                    reply = task1.Result;
                    btn_status(true);

                    if (!(reply.status == "" || reply.status == null)) {
                        MessageBox.Show("ERROR POWER ON! " + "\r" + reply.status);
                        return;
                    }
                    if (reply.data[1] != 0) { MessageBox.Show("ERROR POWER ON"); return; };
                }
                else if (((Button)sender).Name == "btnPwrOffSt")
                {
                    btn_status(false);

                    btnPwrOnSt.Enabled = false;
                    int axis = Axis;
                    if (chkAllAxises.Checked) axis = 0;
                    var task1 = Task.Run(() => RunPwrSt(false, axis));
                    await task1;

                    MyStatic.bPower = false;

                    btnPwrOnSt.Enabled = true;
                    CommReply reply = new CommReply();
                    reply.result = false;
                    reply = task1.Result;
                    btn_status(true);

                    if (!(reply.status == "" || reply.status == null)) {
                        MessageBox.Show("ERROR POWER ON! " + "\r" + reply.status);
                        return;
                    }
                    if (reply.data[1] != 0) { MessageBox.Show("ERROR POWER ON"); return; };
                }
                else if (((Button)sender).Name == "btnStopSt")
                {
                    btn_status(true);
                    AxisMove = false;
                    int device = 0;
                    int axis = Axis;

                    var task1 = Task.Run(() => StopStations_Jog(device, axis));
                    await task1;
                    Thread.Sleep(100);
                    //var task2 = Task.Run(() => ReadCurrent());
                    //await task2;
                }
                else if (((Button)sender).Name == "btnRstSt")
                {
                    btn_status(false);

                    btnPwrOnSt.Enabled = false;
                    FooterStationActAxisInAction = false;
                    btnHome.Enabled = true;
                    btnWork.Enabled = true;
                    int axis = Axis;
                    var task1 = Task.Run(() => RunRstSt(axis));
                    await task1;

                    MyStatic.bPower = false;

                    btnPwrOnSt.Enabled = true;
                    CommReply reply = new CommReply();
                    reply.result = false;
                    reply = task1.Result;
                    //btn_status(true);

                    if (!(reply.status == "" || reply.status == null)) {
                        MessageBox.Show("ERROR RESET! " + "\r" + reply.status);
                        return;
                    }
                    if (reply.data[1] != 0) { MessageBox.Show("ERROR RESET"); return; };
                    Thread.Sleep(500);
                    //power on
                    axis = 0;
                    var task10 = Task.Run(() => RunPwrSt(true, axis));
                    await task10;

                    MyStatic.bPower = false;

                    btnPwrOnSt.Enabled = true;
                    reply = new CommReply();
                    reply.result = false;
                    reply = task10.Result;
                    btn_status(true);

                    if (!(reply.status == "" || reply.status == null)) {
                        MessageBox.Show("ERROR POWER ON! " + "\r" + reply.status);
                        return;
                    }
                    if (reply.data[1] != 0) { MessageBox.Show("ERROR POWER ON"); return; };
                }
                else if (((Button)sender).Name == "btnMoveSt")
                {
                    btn_status(false);
                    Single pos = 0;
                    btnMoveSt.Enabled = false;
                    int axis = Axis;

                    Single speed = Speed * Single.Parse(txtSpeedSt.Text) / 100;

                    pos = Single.Parse(txtCurrPosCams.Text);


                    var task1 = Task.Run(() => MoveAbsSt(axis, pos, speed));
                    await task1;

                    btnMoveSt.Enabled = true;
                    CommReply reply = new CommReply();
                    reply.result = false;
                    reply = task1.Result;
                    btn_status(true);

                    if (!(reply.status == "" || reply.status == null)) {
                        MessageBox.Show("ERRORMOVE MOVE! " + "\r" + reply.status);
                        return;
                    }
                    if (reply.data[1] != 0) { MessageBox.Show("ERROR MOVE"); return; };
                }
            }
            catch (Exception ex) {
                MessageBox.Show("ERROR EXECUTE " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning,
                    MessageBoxDefaultButton.Button1, MessageBoxOptions.DefaultDesktopOnly);
            }
        }
        private async Task<CommReply> MoveAbsSt(int station, Single pos, Single speed)
        {
            CommReply reply = new CommReply();
            Beckhoff.SendPlcParms ParmsPlc = new Beckhoff.SendPlcParms();
            reply.result = false;
            Array.Resize<Single>(ref ParmsPlc.SendParm, 11);
            for (int i = 0; i < ParmsPlc.SendParm.Length; i++) ParmsPlc.SendParm[i] = 0;

            ParmsPlc.SendParm[0] = MyStatic.CamsCmd.MoveAbs;
            ParmsPlc.SendParm[1] = station;
            ParmsPlc.SendParm[2] = pos;
            ParmsPlc.SendParm[3] = speed;
            ParmsPlc.SendParm[10] = 5f;//tmout

            var task1 = Task.Run(() => Beckhoff_Gen.PlcSendCmd(StartAddressSendGen, ParmsPlc));
            await task1;
            ParmsPlc.SendParm = null;
            //wait fini async
            reply = task1.Result;
            return reply;
        }
        private async Task<CommReply> RunPwrSt(bool set, int axis = 0)//stations power on
        {
            CommReply reply = new CommReply();
            Beckhoff.SendPlcParms ParmsPlc = new Beckhoff.SendPlcParms();
            reply.result = false;
            Array.Resize<Single>(ref ParmsPlc.SendParm, 11);
            for (int i = 0; i < ParmsPlc.SendParm.Length; i++) ParmsPlc.SendParm[i] = 0;
            if (set) {
                ParmsPlc.SendParm[0] = MyStatic.CamsCmd.Power;//power on
                ParmsPlc.SendParm[1] = axis;//camSt2
                ParmsPlc.SendParm[2] = 1;//on
                ParmsPlc.SendParm[10] = 12f;//tmout
            } else {
                ParmsPlc.SendParm[0] = MyStatic.CamsCmd.Power;//power on
                ParmsPlc.SendParm[1] = axis;//camSt2
                ParmsPlc.SendParm[2] = 2;//off
                ParmsPlc.SendParm[10] = 12f;//tmout
                ParmsPlc.SendParm[10] = 1f;//tmout
            }

            var task1 = Task.Run(() => Beckhoff_Gen.PlcSendCmd(StartAddressSendGen, ParmsPlc));
            await task1;
            //if (!Beckhoff_Gen.PlcSendCmd(StartAddressSendGen, ParmsPlc, ref Error)) return false;
            ParmsPlc.SendParm = null;
            //wait fini async
            reply = task1.Result;
            return reply;
        }
        private async Task<CommReply> RunRstSt(int axis)//stations power on
        {

            CommReply reply = new CommReply();
            Beckhoff.SendPlcParms ParmsPlc = new Beckhoff.SendPlcParms();
            reply.result = false;
            Array.Resize<Single>(ref ParmsPlc.SendParm, 11);
            for (int i = 0; i < ParmsPlc.SendParm.Length; i++) ParmsPlc.SendParm[i] = 0;

            ParmsPlc.SendParm[0] = MyStatic.CamsCmd.Reset;//power on
            ParmsPlc.SendParm[1] = axis;//camSt2

            ParmsPlc.SendParm[10] = 12f;//tmout


            var task1 = Task.Run(() => Beckhoff_Gen.PlcSendCmd(StartAddressSendGen, ParmsPlc));
            await task1;
            //if (!Beckhoff_Gen.PlcSendCmd(StartAddressSendGen, ParmsPlc, ref Error)) return false;
            ParmsPlc.SendParm = null;
            //wait fini async
            reply = task1.Result;
            return reply;
        }
        private async Task<CommReply> RunCurrPosCams(int axis = 0)//current position
        {
            try
            {
                CommReply reply = new CommReply();
                Beckhoff.SendPlcParms ParmsPlc = new Beckhoff.SendPlcParms();
                reply.result = false;
                Array.Resize<Single>(ref ParmsPlc.SendParm, 11);
                for (int i = 0; i < ParmsPlc.SendParm.Length; i++) ParmsPlc.SendParm[i] = 0;
                //move jog

                ParmsPlc.SendParm[0] = MyStatic.CamsCmd.CurrentPos;//curr pos
                ParmsPlc.SendParm[1] = axis;//cam
                ParmsPlc.SendParm[10] = 0.5f;//tmout

                var task1 = Task.Run(() => Beckhoff_Gen.PlcSendCmd(StartAddressSendGen, ParmsPlc, true));
                await task1;
                reply = task1.Result;

                ParmsPlc.SendParm = null;
                //wait fini async
                return reply;
            }
            catch (Exception ex) {
                CommReply reply = new CommReply();
                reply.result = false;
                reply.comment = ex.Message;
                return reply;
            }
        }

        private async void btnMin_Click(object sender, EventArgs e)
        {
            try
            {
                if (chkFine.Checked)
                {
                    btn_status(false);
                    if (trackBarSpeedSt.Value > 10) trackBarSpeedSt.Value = 10;
                    Single dist = 0;
                    inv.set(btnMin, "Enabled", false);
                    string[] s = cmbAxes.Text.Split(':');
                    Axis = int.Parse(s[0]);
                    int axis = Axis;
                    Speed = 1000;
                    Single speed = Speed * Single.Parse(txtSpeedSt.Text) / 100;

                    dist = Single.Parse(cmbFine.Text);

                    var task1 = Task.Run(() => MoveRelSt(axis, -dist, speed));
                    await task1;

                    CommReply reply = new CommReply();
                    reply.result = false;
                    reply = task1.Result;
                    inv.settxt(txtCurrPosCams, reply.data[4].ToString("0.000"));

                    if (!(reply.status == "" || reply.status == null)) {
                        MessageBox.Show("ERROR MOVE FINE! " + "\r" + reply.status);
                        btn_status(true);
                        inv.set(btnPlus, "Enabled", true);
                        return;
                    }
                    if (reply.data[1] != 0) { MessageBox.Show("ERROR MOVE"); return; };
                    //snap
                    //int cam = 0;
                    //int cam1 = 0, cam2 = 0, cam3 = 0, cam4 = 0, cam5 = 0, campick = 0, camplace = 0;
                    //if (Axis == MyStatic.StationAxis.ZF_ST2) { cam = 1; cam1 = 1; }
                    //else if (Axis == MyStatic.StationAxis.XF_ST3) { cam = 2; cam2 = 1; }
                    //else if (Axis == MyStatic.StationAxis.XF_ST6) { cam = 4; cam4 = 1; }
                    //else if (Axis == MyStatic.StationAxis.ZF_ST7) { cam = 5; cam5 = 1; }
                    //if (cam != 0)
                    //{
                    //    int next = 0;
                    //    //var task2 = Task.Run(() => TstStation(false));
                    //    //await task2;
                    //    //bool b = task2.Result;
                    //    //int vistype = (int)MyStatic.Vision.locator;
                    //
                    //    //var task2 = Task.Run(() => GetCameraCoord(cam, next, 0, 0, 0, 0, 0, 0, 1, (int)MyStatic.VisionCmd.snap, 1, (int)MyStatic.Vision.locator));
                    //    //await task2;
                    //    //CommReply visreply= task2.Result;
                    //    Thread.Sleep(500);
                    //}
                    btn_status(true);
                    inv.set(btnMin, "Enabled", true);
                }
            }
            catch (Exception ex) {
                MessageBox.Show("ERROR MOVE FINE! " + ex.Message, "ERROR", MessageBoxButtons.OK,
                     MessageBoxIcon.Warning, MessageBoxDefaultButton.Button1, MessageBoxOptions.DefaultDesktopOnly);
                btn_status(true);
                inv.set(btnPlus, "Enabled", true);
            }
        }

        private async void btnMin_MouseClick(object sender, MouseEventArgs e)
        {
            try
            {
                if (chkFine.Checked)
                {
                    btn_status(false);
                    if (trackBarSpeedSt.Value > 10) trackBarSpeedSt.Value = 10;
                    Single dist = 0;
                    inv.set(btnMin, "Enabled", false);
                    string[] s = cmbAxes.Text.Split(':');
                    Axis = int.Parse(s[0]);
                    int axis = Axis;
                    Speed = 1000;
                    Single speed = Speed * Single.Parse(txtSpeedSt.Text) / 100;

                    dist = -Single.Parse(cmbFine.Text);


                    var task1 = Task.Run(() => MoveRelSt(axis, dist, speed));
                    await task1;


                    CommReply reply = new CommReply();
                    reply.result = false;
                    reply = task1.Result;
                    inv.settxt(txtCurrPosCams, reply.data[4].ToString("0.000"));


                    if (!(reply.status == "" || reply.status == null))
                    {
                        MessageBox.Show("ERROR MOVE FINE! " + "\r" + reply.status);
                        btn_status(true);
                        btnMin.Enabled = true;
                        return;
                    }
                    if (reply.data[1] != 0) { MessageBox.Show("ERROR MOVE"); return; };
                    //snap
                    int cam = 0;
                    int cam1 = 0, cam2 = 0, cam3 = 0, cam4 = 0, cam5 = 0, campick = 0, camplace = 0;
                    if (Axis == MyStatic.StationAxis.ZF_ST2) { cam = 1; cam1 = 1; }
                    else if (Axis == MyStatic.StationAxis.XF_ST3) { cam = 2; cam2 = 1; }
                    else if (Axis == MyStatic.StationAxis.XF_ST6) { cam = 4; cam4 = 1; }
                    else if (Axis == MyStatic.StationAxis.ZF_ST7) { cam = 5; cam5 = 1; }
                    if (cam != 0)
                    {
                        int next = 0;
                        //var task3 = Task.Run(() => GetCameraCoord(cam, next, 0, 0, 0, 0, 0, 0, 1, (int)MyStatic.VisionCmd.snap, 1,(int)MyStatic.Vision.locator));
                        //await task3;
                        //visresult = task3.Result;
                        //btnTstStation_Click(null, null);
                        //int vistype = (int)MyStatic.Vision.locator;
                        //var task2 = Task.Run(() =>  GetCameraCoord(cam, next, 0, 0, 0, 0, 0, 0, 1, (int)MyStatic.VisionCmd.snap, 1, (int)MyStatic.Vision.locator));
                        //await task2;
                        //CommReply visreply = task2.Result;
                        //var task2 = Task.Run(() => TstStation(false));
                        //await task2;
                        //bool b = task2.Result;
                        Thread.Sleep(500);
                    }
                    btn_status(true);
                    inv.set(btnMin, "Enabled", true);
                }
            }
            catch (Exception ex) {
                MessageBox.Show("ERROR MOVE FINE! " + ex.Message, "ERROR", MessageBoxButtons.OK,
                     MessageBoxIcon.Warning, MessageBoxDefaultButton.Button1, MessageBoxOptions.DefaultDesktopOnly);
                btn_status(true);
                inv.set(btnMin, "Enabled", true);
            }
        }

        private async void btnMin_MouseDown(object sender, MouseEventArgs e)
        {
            try
            {
                if (chkFine.Checked) return;
                AxisMove = true;
                int device = 1;
                int direction = -1;
                string[] s = cmbAxes.Text.Split(':');
                Axis = int.Parse(s[0]);
                int axis = Axis;
                Speed = 100;
                float speed = Speed * Single.Parse(txtSpeedSt.Text) / 100;

                var task1 = Task.Run(() => RunStations_Jog(device, axis, direction, speed));
                await Task.WhenAll(task1);
                CommReply reply = new CommReply();
                reply.result = false;
                reply = task1.Result;
            }
            catch (Exception ex) {
                CommReply reply = new CommReply();
                reply.result = false;
                reply.comment = ex.Message;
            }
        }

        private async void btnMin_MouseUp(object sender, MouseEventArgs e)
        {
            //stop
            if (chkFine.Checked) return;
            btn_status(true);
            AxisMove = false;
            int device = 0;
            int axis = 0;

            var task1 = Task.Run(() => StopStations_Jog(device, axis));
            await task1;
            Thread.Sleep(100);
        }

        private async void btnMin_MouseLeave(object sender, EventArgs e)
        {
            //stop
            if (chkFine.Checked) return;
            if (AxisMove)
            {
                btn_status(true);
                AxisMove = false;
                int device = 0;
                int axis = 0;

                var task1 = Task.Run(() => StopStations_Jog(device, axis));
                await task1;
                Thread.Sleep(100);
            }
            //var task2 = Task.Run(() => ReadCurrent());
            //await task2;
        }
        public void ListAdd(string st, ListBox lst, bool clear = false)
        {
            if (InvokeRequired) {
                this.Invoke(new Action(() => SetLst(st, clear, lst)));
            } else {
                SetLst(st, clear, lst);
            }
        }
        private void SetLst(string text, bool clear, ListBox lst)
        {
            try {
                if (text != "") {
                    lst.Items.Add(text);
                    if (lst.Items.Count > 500) {
                        for (int i = 0; i < 20; i++) lst.Items.RemoveAt(0);
                    }
                    lst.SetSelected(lst.Items.Count - 1, true);
                }
            }
            catch (Exception ex) { MessageBox.Show("LIST EXEPTION " + ex.Message); }
        }
        //string cmdpr =frmMainInspect.FrontPath + @"\Cam2BaslerML.exe";// @"C:\Project\Cam2\Cam2BaslerML\Cam2BaslerML\bin\Debug\Cam2BaslerML.exe";
        int pctSnapH = 0;
        int pctSnapW = 0;
        int panel2H = 0;
        int panel2W = 0;
        int pctSnapHFront = 0;
        int pctSnapWFront = 0;
        int panel4H = 0;
        int panel4W = 0;
        private void frmBeckhoff_Load(object sender, EventArgs e)
        {
            bool bCam2 = false;
            try
            {
                pctSnapH = pctSnap.Height;
                pctSnapW = pctSnap.Width;
                panel2H = panel2.Height;
                panel2W = panel2.Width;

                pctSnapHFront = pctSnapFront.Height;
                pctSnapWFront = pctSnapFront.Width;
                panel4H = panel4.Height;
                panel4W = panel4.Width;

                Beckhoff_Cam1.SetText(1, txtMess, this, true);
                Beckhoff_Cam2.SetText(1, txtMess, this, true);
                Beckhoff_Gen.SetText(1, txtMess, this, true);
                Beckhoff_Cam1.bwName = "Beckhoff_Cam1";
                Beckhoff_Cam2.bwName = "Beckhoff_Cam2";
                Beckhoff_Gen.bwName = "Beckhoff_Gen";

                //this.Cursor = Cursors.WaitCursor;

                System.Windows.Forms.Cursor curs = this.Cursor;
                if (InvokeRequired)
                    this.Invoke(new Action(() => this.Cursor = Cursors.WaitCursor));
                else
                    this.Cursor = Cursors.WaitCursor;


                aPath = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().GetModules()[0].FullyQualifiedName);
                sourcesPath = System.IO.Directory.GetParent(System.IO.Directory.GetParent(aPath).ToString()).ToString();

                LoadIni(1);

                //if (!bDebugMode) {
                try
                {
                    Beckhoff.tcAds.Connect(PlcNetID, PlcPort);
                }
                catch { }
                //}

                if (sFriendlyName1 != "") UpdateDeviceList(1);
                if (nExposureInspection != 0) trkExp1.Value = nExposureInspection;

                SetJpgQuality();

                bl.CameraDestroyed += bl_CameraDestroyed;
                bl.DeviceListUpdated += bl_DeviceListUpdated;

                frmMainInspect = new frmMain();
                frmMainInspect.TopLevel = false;
                frmMainInspect.FormBorderStyle = FormBorderStyle.None;
                frmMainInspect.Visible = true;
                panelInspect.Controls.Add(frmMainInspect);

                frmRun = new frmRunning();

                //frmMainInspect.Show();

                //frmFrontInspect = new frmFront();
                //frmFrontInspect.TopLevel = false;
                //frmFrontInspect.FormBorderStyle = FormBorderStyle.None;
                //frmFrontInspect.Visible = true;
                //panelFrontInspect.Controls.Add(frmFrontInspect);

                //if (chkSaveFile.Checked) frmMainInspect.SaveOnDisk = true;
                string sML = frmMainInspect.FrontPath + @"\ML.bat";// "@"C:\Project\Cam2\Cam2BaslerML\Cam2BaslerML\bin\Debug\ML.bat";
                if (File.Exists(sML))
                {
                    //attach camFront
                    hwndCamFront = FindWindow(null, "Cam2BaslerML");
                    if (hwndCamFront == IntPtr.Zero)
                    {
                        //Detach();
                        Thread.Sleep(100);
                        System.Diagnostics.Process.Start(sML);
                        //Thread.Sleep(1000);
                    }

                    Thread.Sleep(500);
                    //hwndCamFront = (IntPtr)0;
                    //Attach();
                    foreach (Process p in Process.GetProcesses())
                    {
                        if (p.ProcessName == "cmd") p.Kill();
                    }

                    //SetWindowPos(this.Handle, (IntPtr)HWND_TOPMOST, 0, 0, System.Windows.Forms.SystemInformation.PrimaryMonitorSize.Height, System.Windows.Forms.SystemInformation.PrimaryMonitorSize.Width, 0);
                    //Thread.Sleep(1000);
                    //SetWindowPos(this.Handle, (IntPtr)HWND_NOTOPMOST, 0, 0, System.Windows.Forms.SystemInformation.PrimaryMonitorSize.Height, System.Windows.Forms.SystemInformation.PrimaryMonitorSize.Width, 0);
                    AddWindow(true);
                    bStop = false;
                    inv.set(btnSatrt2, "Enabled", false);
                    Thread.Sleep(200);
                    var task = Task.Run(() => RunWebComm());//vision
                    var task1 = Task.Run(() => RunWebComm1());//cognex
                                                              //var task1 = Task.Run(() => RunWebComm1());//cognex
                                                              //await task;
                                                              //await task1;
                    inv.set(btnSatrt2, "Enabled", true);
                    bCam2 = true;
                }

                PopulateFullImagesIndices();

                cmbSaveResults.SelectedIndex = 2;
                cmbSnapShotStrategy.SelectedIndex = (int)eSnapShotStrategy.eSnapShotStrategyFullImagesForColorHistogramHalfImagesForTheRest;
                frmMainInspect.onExposureChangedFromCatalogue += onExposureChangedFromCatalogueNumber;
                frmMainInspect.onExposureChangedFromBeckofForm += onExposureChangedFromBeckofForm;

                this.Invoke(new Action(() => this.Cursor = curs));
                this.Focus();
            }
            catch (System.Exception ex) {
                throw; }

            //if (bCam2) {
            //    Process p1 = new Process();
            //}
            this.Text = this.Text + " Version " + Assembly.GetExecutingAssembly().GetName().Version?.ToString();
        }

        public uint WinStyle = 0;
        public int WinStyle1 = 0;


        [DllImport("user32.dll")]
        static extern IntPtr SetParent(IntPtr hwndChild, IntPtr hwndNewParent);
        [DllImport("user32.dll", SetLastError = true)]
        static extern IntPtr FindWindow(string lpClassName, string lpWindowName);
        [DllImport("user32.dll")]
        static extern IntPtr FindWindowEx(IntPtr hwndParent, IntPtr ChildAfter, string lclasName, string windowTitle);
        [DllImport("user32.dll")]
        static extern int SetWindowLong(IntPtr hwnd, int nIndex, int dwNewLong);
        [DllImport("user32.dll")]
        static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, int uFlags);
        [DllImport("user32.dll", SetLastError = true)]
        static extern int SetWindowLongPtr(IntPtr hwnd, int nIndex, uint dwNewLong);
        [DllImport("user32.dll")]
        static extern int GetWindowLong(IntPtr hwnd, int nIndex);
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        static extern int GetWindowLongPtr(IntPtr hwnd, int nIndex);
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        //static extern IntPtr SendMessage(IntPtr hwnd, UInt32 Msg, IntPtr Wparm, IntPtr Iparm);

        //[DllImport("user32.dll")]
        static extern bool SetWindowpos(IntPtr hwnd, IntPtr hwndInserAfter, int X, int Y, int cx, int cy, uint uFlags);
        [DllImport("user32.dll", SetLastError = true)]
        static extern bool MoveWindow(IntPtr hwnd, int X, int Y, int nWidth, int nHeight, bool bRepaint);
        //const UInt32 WM_CLOSE = 0x0010;

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]


        static extern bool ShowWindow(IntPtr hWnd, int uFlags);

        [DllImport("user32.dll")]
        static extern bool GetWindowRect(IntPtr hwnd, ref Rect rectangle);

        public struct Rect
        {
            public int Left { get; set; }
            public int Top { get; set; }
            public int Right { get; set; }
            public int Bottom { get; set; }
        }

        //public static int GWL_STYLE = -16;
        //public static int GWL_EXSTYLE = -20;
        //public static int WS_BORDER = 8388608;
        const int WS_BORDER = 0x00800000;
        const int WS_DLGFRAME = 0x00400000;
        const int WS_THICKFRAME = 0x00040000;
        const int WS_CAPTION = WS_BORDER | WS_DLGFRAME;
        const int WS_MINIMIZE = 0x20000000;
        const int WS_MAXIMIZE = 0x01000000;
        const int WS_SYSMENU = 0x00080000;
        const int WS_VISIBLE = 0x10000000;
        const int WS_TILED = 0x0000000;
        const int GWL_STYLE = -16;
        const int SWP_NOSIZE = 0x0001;
        const int SWP_NOZORDER = 0x0004;
        //const int SWP_SHOWWINDOW = 0x0040;

        IntPtr hwndCamFront;
        private void Detach()
        {
            try
            {
                IntPtr oldParent = (IntPtr)(0);
                int px = panelCamFront.PointToScreen(System.Drawing.Point.Empty).X;
                int py = panelCamFront.PointToScreen(System.Drawing.Point.Empty).Y;
                SetParent(hwndCamFront, oldParent);
                MoveWindow(hwndCamFront, px, py, panelCamFront.Width, panelCamFront.Height, true);
                SetWindowLong(hwndCamFront, style, WS_VISIBLE + WS_MAXIMIZE + WS_BORDER + WS_CAPTION);
            }
            catch (System.Exception ex) { }
        }
        int style = 0;
        private void Attach()
        {
            try
            {

                int height = System.Windows.Forms.SystemInformation.PrimaryMonitorSize.Height;
                int width = System.Windows.Forms.SystemInformation.PrimaryMonitorSize.Width;
                this.Width = System.Windows.Forms.SystemInformation.PrimaryMonitorSize.Width - 10;
                //panelCamFront.Width = this.Width - txtMess.Width - 50;
                //tabControl1.Width = this.Width - txtMess.Width - 20;
                Stopwatch sw = new Stopwatch();
                sw.Restart();
                hwndCamFront = (IntPtr)(-1);
                while ((long)hwndCamFront <= 0)
                {
                    hwndCamFront = FindWindow(null, "Cam2BaslerML");//Cam2BaslerML
                    Thread.Sleep(200);
                    if (sw.ElapsedMilliseconds > 5000) return;
                }
                style = GetWindowLong(hwndCamFront, GWL_STYLE);

                SetParent(hwndCamFront, panelCamFront.Handle);

                CenterToParent();
                MoveWindow(hwndCamFront, 0, 0, panelCamFront.Width, panelCamFront.Height, true);
                SetWindowLong(hwndCamFront, GWL_STYLE, WS_VISIBLE + WS_MAXIMIZE);

                //this.Cursor.Clip = Rectangle.Empty;
                //this.Cursor.Show();
            }
            catch (Exception ex) { }
        }
        private void AddWindow(bool min = false)
        {
            try
            {


                //int width = (int)(Screen.PrimaryScreen.WorkingArea.Width - 380);
                //int height = (int)(Screen.PrimaryScreen.WorkingArea.Height - 250);

                Stopwatch sw = new Stopwatch();
                sw.Restart();
                //hwndCamFront = (IntPtr)(-1);
                while ((long)hwndCamFront <= 0)
                {
                    hwndCamFront = FindWindow(null, "Cam2BaslerML");//Cam2BaslerML
                    Thread.Sleep(200);
                    if (sw.ElapsedMilliseconds > 5000) return;
                }
                //style = GetWindowLong(hwndCamFront, GWL_STYLE);

                //main window position
                //IntPtr ptr = this.Handle;
                //Rect MyRect = new Rect();
                //GetWindowRect(ptr, ref MyRect);

                SetWindowPos(this.Handle, IntPtr.Zero, 0, 0, this.Width, this.Height, SWP_SHOWWINDOW);

                long lStyle = GetWindowLong(hwndCamFront, GWL_STYLE);
                lStyle &= ~(WS_CAPTION | WS_THICKFRAME | WS_SYSMENU | TOPMOST_FLAGS);
                int rc = SetWindowLong(hwndCamFront, GWL_STYLE, (uint)lStyle);
                if (min)
                    SetWindowPos(hwndCamFront, (IntPtr)HWND_TOPMOST, 0, 0, 0, 0, 0);
                else
                    SetWindowPos(hwndCamFront, (IntPtr)HWND_TOPMOST, panelCamFront.Left + 5 + tabControl1.Left, panelCamFront.Top + tabControl1.Top + 50, tabControl1.Width - 2, panelCamFront.Height, 0);
            }
            catch (Exception ex) { }
        }


        private void txtMess_DoubleClick(object sender, EventArgs e)
        {
            inv.settxt(txtMess, "");
        }

        private void cmbAxes_SelectedIndexChanged(object sender, EventArgs e)
        {
            try
            {
                string[] s = cmbAxes.Text.Split(':');
                Axis = int.Parse(s[0]);
                if (Axis == 1)
                {
                    string pict = Path.GetDirectoryName(Application.ExecutablePath) + "\\ArwDown.ico";
                    btnPlus.Image = new Bitmap(pict);
                    pict = Path.GetDirectoryName(Application.ExecutablePath) + "\\ArwUp.ico";
                    btnMin.Image = new Bitmap(pict);
                }
                else if (Axis == 2)
                {
                    string pict = Path.GetDirectoryName(Application.ExecutablePath) + "\\ArwDown.ico";
                    btnPlus.Image = new Bitmap(pict);
                    pict = Path.GetDirectoryName(Application.ExecutablePath) + "\\ArwUp.ico";
                    btnMin.Image = new Bitmap(pict);

                }
                else if (Axis == 3)
                {
                    string pict = Path.GetDirectoryName(Application.ExecutablePath) + "\\ArwLeft.ico";
                    btnPlus.Image = new Bitmap(pict);
                    pict = Path.GetDirectoryName(Application.ExecutablePath) + "\\ArwRight.ico";
                    btnMin.Image = new Bitmap(pict);

                }
                else if (Axis == 4)
                {
                    string pict = Path.GetDirectoryName(Application.ExecutablePath) + "\\ArwRotLeft.ico";
                    btnPlus.Image = new Bitmap(pict);
                    pict = Path.GetDirectoryName(Application.ExecutablePath) + "\\ArwRotRight.ico";
                    btnMin.Image = new Bitmap(pict);

                }
                else if (Axis == 5)
                {
                    string pict = Path.GetDirectoryName(Application.ExecutablePath) + "\\ArwLeft.ico";
                    btnPlus.Image = new Bitmap(pict);
                    pict = Path.GetDirectoryName(Application.ExecutablePath) + "\\ArwRight.ico";
                    btnMin.Image = new Bitmap(pict);

                }
            }
            catch (Exception ex) { }
        }

        private async void btnMoveSt_Click(object sender, EventArgs e)
        {
            try
            {
                btn_status(false);
                //if (trackBarSpeedSt.Value > 10) trackBarSpeedSt.Value = 10;
                Single dist = 0;
                inv.set(btnPlus, "Enabled", false);
                string[] s = cmbAxes.Text.Split(':');
                Axis = int.Parse(s[0]);
                int axis = Axis;
                if (Axis == 4) Speed = 3600f; else Speed = 1000f;
                Single speed = Speed * Single.Parse(txtSpeedSt.Text) / 100f;

                dist = Single.Parse(txtCurrPosCams.Text);


                var task1 = Task.Run(() => MoveAbs(0, axis, dist, speed));
                await task1;


                CommReply reply = new CommReply();
                reply.result = false;
                reply = task1.Result;
                //inv.settxt(txtCurrPosCams, reply.data[4].ToString("0.000"));
                inv.set(upDownControlPosSt, "UpDownValue", reply.data[4]);


                if (!(reply.status == "" || reply.status == null)) {
                    MessageBox.Show("ERROR MOVE FINE! " + "\r" + reply.status);
                    btn_status(true);
                    inv.set(btnPlus, "Enabled", true);
                    return;
                }
                if (reply.data[1] != 0) { MessageBox.Show("ERROR MOVE"); return; };

                btn_status(true);
                inv.set(btnPlus, "Enabled", true);
            }
            catch (Exception ex) { MessageBox.Show("ERROR MOVE FINE! "); }
        }


        public async Task<bool> MotionInCycle(int nFrameMax, int nFrame)
        {
            try
            {
                Single StartPos = Convert.ToSingle(inv.gettxt(lblStartPosition));
                Single fStep = 360.0f / nFrameMax;
                Single fAbsAngle = nFrame * fStep + StartPos;

                //inv.settxt(txtCurrPosCams, fAbsAngle.ToString());

                //btn_status(false);
                //if (trackBarSpeedSt.Value > 10) trackBarSpeedSt.Value = 10;
                Single dist = 0;
                //inv.set(btnPlus, "Enabled", false);
                Axis = 4; // int.Parse(cmbAxes.Text);
                int axis = Axis;
                Speed = 2000f;
                Single speed = Speed * Single.Parse(txtSpeedSt.Text) / 100f;
                dist = fAbsAngle; // Single.Parse(txtCurrPosCams.Text);

                var task1 = Task.Run(() => MoveAbs(0, axis, dist, speed));
                await task1;

                CommReply reply = new CommReply();
                reply.result = false;
                reply = task1.Result;
                //inv.settxt(txtCurrPosCams, reply.data[4].ToString("0.000"));

                if (!(reply.status == "" || reply.status == null)) {
                    //MessageBox.Show("ERROR MOVE FINE! " + "\r" + reply.status);
                    //btn_status(true);
                    //inv.set(btnPlus, "Enabled", true);
                    return false;
                }
                if (reply.data[1] != 0) { return false; } //MessageBox.Show("ERROR MOVE"); 

                //btn_status(true);
                //inv.set(btnPlus, "Enabled", true);
                return true;
            }
            catch (Exception ex) { return false; } // MessageBox.Show("ERROR MOVE FINE!");
        }


        void bl_CameraDestroyed(object sender, EventArgs e)
        {
            btnOpenLive1.Enabled = true; btnOpenLive1.BackColor = Color.FromArgb(170, 222, 255);
            btnCloseLive1.Enabled = false; btnCloseLive1.BackColor = Color.Gainsboro;
            toolStrip1.Enabled = false;
            trkExp1.Enabled = false;

            btnSnap.Enabled = false;

            Environment.Exit(0);
        }
        void bl_DeviceListUpdated(object sender, EventArgs e)
        {
            //lblBaslerLive1.Text = e.TagSerialNumber;
            btnOpenLive1.Enabled = false; btnOpenLive1.BackColor = Color.Gainsboro;
            btnCloseLive1.Enabled = true; btnCloseLive1.BackColor = Color.FromArgb(170, 222, 255);
            toolStrip1.Enabled = true;

            btnSnap.Enabled = true;

            Environment.Exit(0);
        }

        private void frmBeckhoff_FormClosing(object sender, FormClosingEventArgs e)
        {
            //if (txtItem.Text.Trim() != "") SaveLastItem();
            try
            {
                WC1.Stop();
                WC2.Stop();
                Thread.Sleep(50);
                Detach();
                Thread.Sleep(1000);
                DestroyCamera(1);
                Stopwatch Exit = new Stopwatch();
                Exit.Reset();
                Exit.Start();
                while (Exit.ElapsedMilliseconds < 1000)
                {
                    Application.DoEvents();
                }
                Exit.Stop();
                Exit.Reset();
                //cam2
                //hwndCamFront = FindWindow(null, "Cam2BaslerML")
                foreach (Process proc in Process.GetProcessesByName("Cam2BaslerML"))
                {

                    proc.Kill();

                }

                //if (!(mFrmMain == null))
                //    mFrmMain.Close();

                //Application.Exit();
                stopwatch.Stop();
                frmMainInspect.Close();
            }
            catch { }
        }

        public void CloseLiveWindow()
        {
            if (WindowToClose != IntPtr.Zero) {
                SendMessage(WindowToClose, WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
                WindowToClose = IntPtr.Zero;
            }
        }

        public async void UpdateTopInpectionExposureTime(int nExposureInspection)
        {
            trkExp1.Value = nExposureInspection;

            while (bfrmBeckhoff_Shown == false)
            {
                await Task.Delay(10);
            }

            trkExp1_Scroll(trkExp1, null);
        }

        public async void LoadIni(int nCam) //, bool bLoadExposure)
        {
            IniFiles.IniFile oIniFile = IniFiles.IniFile.FromFile(aPath + "\\BeckhoffBasler.ini");

            if (nCam == 1) //Top
            {
                if (oIniFile["Camera2"]["FriendlyName"] != null && oIniFile["Camera2"]["FriendlyName"].Trim() != "") {
                    sFriendlyName1 = oIniFile["Camera2"]["FriendlyName"];
                }

                if (oIniFile["Camera2"]["ConnectionName"] != null && oIniFile["Camera2"]["ConnectionName"].Trim() != "") {
                    txtConnectionName1.Text = oIniFile["Camera2"]["ConnectionName"];
                }

                if (oIniFile["Camera2"]["ExposureInspection"] != null && oIniFile["Camera2"]["ExposureInspection"].Trim() != "")
                {
                    nExposureInspection = Convert.ToInt32(Convert.ToDouble(oIniFile["Camera2"]["ExposureInspection"]));
                    trkExp1.Value = nExposureInspection;

                    taskcamera1 = Task.Run(() =>
                            TaskProcessCamera1());

                    //trkExp_Scroll(trkExp1, null);
                    //Application.DoEvents();
                }
                if (oIniFile["Camera2"]["ExposureDiameter"] != null && oIniFile["Camera2"]["ExposureDiameter"].Trim() != "")
                {
                    nExposureDiameter = Convert.ToInt32(Convert.ToDouble(oIniFile["Camera2"]["ExposureDiameter"]));
                    //trkExp1.Value = nExposureDiameter;
                    //
                    //taskcamera1 = Task.Run(() =>
                    //        TaskProcessCamera1());
                    //
                    ////trkExp_Scroll(trkExp1, null);
                    ////Application.DoEvents();
                } else {
                    nExposureDiameter = nExposureInspection;
                }

                if (oIniFile["Camera2"]["LightSourceSelector"] != null && oIniFile["Camera2"]["LightSourceSelector"].Trim() != "")
                {
                    int nLight = Convert.ToInt32(oIniFile["Camera2"]["LightSourceSelector"]);
                    if (nLight == 1) {
                        chkLightSource.Checked = true;
                        chkLightSourceOff.Checked = false;
                    } else if (nLight == 2) {
                        chkLightSource.Checked = false;
                        chkLightSourceOff.Checked = true;
                    } else {
                        chkLightSource.Checked = false;
                        chkLightSourceOff.Checked = false;
                    }
                }

                //if (oIniFile["Camera1"]["Exposure1"] != null && oIniFile["Camera1"]["Exposure1"].Trim() != "")
                //    nExposureView1 = Convert.ToInt32(oIniFile["Camera1"]["Exposure1"]);
                //else
                //    nExposureView1 = nExposure1;
                //if (oIniFile["Camera1"]["Exposure2"] != null && oIniFile["Camera1"]["Exposure2"].Trim() != "")
                //    nExposureView2 = Convert.ToInt32(oIniFile["Camera1"]["Exposure2"]);
                //else
                //    nExposureView2 = nExposure1;
                //if (oIniFile["Camera1"]["Exposure3"] != null && oIniFile["Camera1"]["Exposure3"].Trim() != "")
                //    nExposureView3 = Convert.ToInt32(oIniFile["Camera1"]["Exposure3"]);
                //else
                //    nExposureView3 = nExposure1;
                //if (oIniFile["Camera1"]["Exposure4"] != null && oIniFile["Camera1"]["Exposure4"].Trim() != "")
                //    nExposureView4 = Convert.ToInt32(oIniFile["Camera1"]["Exposure4"]);
                //else
                //    nExposureView4 = nExposure1;

                if (oIniFile["SearchArea2"]["RectPointX"] != null && oIniFile["SearchArea2"]["RectPointX"].Trim() != "")
                    txtRectPointX.Text = oIniFile["SearchArea2"]["RectPointX"];
                else
                    txtRectPointX.Text = "";

                if (oIniFile["SearchArea2"]["RectPointY"] != null && oIniFile["SearchArea2"]["RectPointY"].Trim() != "")
                    txtRectPointY.Text = oIniFile["SearchArea2"]["RectPointY"];
                else
                    txtRectPointY.Text = "";

                if (oIniFile["SearchArea2"]["SearchAreaWidth"] != null && oIniFile["SearchArea2"]["SearchAreaWidth"].Trim() != "")
                    txtSearchAreaWidth.Text = oIniFile["SearchArea2"]["SearchAreaWidth"];
                else
                    txtSearchAreaWidth.Text = "";

                if (oIniFile["SearchArea2"]["SearchAreaHeight"] != null && oIniFile["SearchArea2"]["SearchAreaHeight"].Trim() != "")
                    txtSearchAreaHeight.Text = oIniFile["SearchArea2"]["SearchAreaHeight"];
                else
                    txtSearchAreaHeight.Text = "";

                if (oIniFile["SearchArea2"]["UseSearchArea"] != null && oIniFile["SearchArea2"]["UseSearchArea"].Trim() != "")
                {
                    string sBool = oIniFile["SearchArea2"]["UseSearchArea"];
                    if (sBool.Trim().ToLower() == "true")
                        chkUseSearchArea.Checked = true;
                    else
                        chkUseSearchArea.Checked = false;
                }

                if (oIniFile["Options2"]["StretchImage"] != null && oIniFile["Options2"]["StretchImage"].Trim() != "")
                {
                    string sBool = oIniFile["Options2"]["StretchImage"];
                    if (sBool.Trim().ToLower() == "true")
                        chkStretchImage1.Checked = true;
                    else
                        chkStretchImage1.Checked = false;
                }

                if (oIniFile["Options2"]["FillWeldonGreen"] != null && oIniFile["Options2"]["FillWeldonGreen"].Trim() != "")
                {
                    string sBool = oIniFile["Options2"]["FillWeldonGreen"];
                    if (sBool.Trim().ToLower() == "true")
                        chkFillWeldonGreen.Checked = true;
                    else
                        chkFillWeldonGreen.Checked = false;
                }


                if (oIniFile["Weldon"]["DiamNominal"] != null && oIniFile["Weldon"]["DiamNominal"].Trim() != "")
                    txtDiamNominal.Text = oIniFile["Weldon"]["DiamNominal"];
                else
                    txtDiamNominal.Text = "16";

                if (oIniFile["Weldon"]["DiamActual"] != null && oIniFile["Weldon"]["DiamActual"].Trim() != "")
                    txtDiamActual.Text = oIniFile["Weldon"]["DiamActual"];
                else
                    txtDiamActual.Text = "16";

                if (oIniFile["Weldon"]["ROIL"] != null && oIniFile["Weldon"]["ROIL"].Trim() != "")
                    txtROIL.Text = oIniFile["Weldon"]["ROIL"];
                else
                    txtROIL.Text = "110";

                if (oIniFile["Weldon"]["ROIR"] != null && oIniFile["Weldon"]["ROIR"].Trim() != "")
                    txtROIR.Text = oIniFile["Weldon"]["ROIR"];
                else
                    txtROIR.Text = "2000";

                if (oIniFile["Weldon"]["LineWidth"] != null && oIniFile["Weldon"]["LineWidth"].Trim() != "")
                    txtLineWidth.Text = oIniFile["Weldon"]["LineWidth"];
                else
                    txtLineWidth.Text = "7";

                if (oIniFile["Calibration2"]["CalibrationRatio"] != null && oIniFile["Calibration2"]["CalibrationRatio"].Trim() != "")
                    txtCalib.Text = oIniFile["Calibration2"]["CalibrationRatio"];
                else
                    txtCalib.Text = "1";

                // Histogram

                if (oIniFile["Histogram"]["MinBrightnessDifferenceFromGray"] != null && oIniFile["Histogram"]["MinBrightnessDifferenceFromGray"].Trim() != "")
                    txtMinBrDiff.Text = oIniFile["Histogram"]["MinBrightnessDifferenceFromGray"];
                else
                    txtMinBrDiff.Text = "10";

                if (oIniFile["Histogram"]["MaxBrightnessDifferenceFromGray"] != null && oIniFile["Histogram"]["MaxBrightnessDifferenceFromGray"].Trim() != "")
                    txtMaxBrDiff.Text = oIniFile["Histogram"]["MaxBrightnessDifferenceFromGray"];
                else
                    txtMaxBrDiff.Text = "30";

                if (oIniFile["Histogram"]["MinBrightness"] != null && oIniFile["Histogram"]["MinBrightness"].Trim() != "")
                    txtMinBr.Text = oIniFile["Histogram"]["MinBrightness"];
                else
                    txtMinBr.Text = "50";

                if (oIniFile["Histogram"]["MinRelativeRedBrightnessColorImageThreshold"] != null && oIniFile["Histogram"]["MinRelativeRedBrightnessColorImageThreshold"].Trim() != "")
                    txtColorThreshold.Text = oIniFile["Histogram"]["MinRelativeRedBrightnessColorImageThreshold"];
                else
                    txtColorThreshold.Text = "0.02";

                if (oIniFile["Histogram"]["ShowHistogram"] != null && oIniFile["Histogram"]["ShowHistogram"].Trim() != "")
                {
                    string sBool = oIniFile["Histogram"]["ShowHistogram"];
                    if (sBool.Trim().ToLower() == "true")
                        chkShowHist.Checked = true;
                    else
                        chkShowHist.Checked = false;
                }

                if (oIniFile["Histogram"]["ShowOnlyRed"] != null && oIniFile["Histogram"]["ShowOnlyRed"].Trim() != "")
                {
                    string sBool = oIniFile["Histogram"]["ShowOnlyRed"];
                    if (sBool.Trim().ToLower() == "true")
                        chkRed.Checked = true;
                    else
                        chkRed.Checked = false;
                }


            }
            else if (nCam == 2) // Side
            {
                if (oIniFile["Camera1"]["FriendlyName"] != null && oIniFile["Camera1"]["FriendlyName"].Trim() != "") {
                    sFriendlyName2 = oIniFile["Camera1"]["FriendlyName"];
                }

                if (oIniFile["Camera1"]["ConnectionName"] != null && oIniFile["Camera1"]["ConnectionName"].Trim() != "") {
                    //txtConnectionName2.Text = oIniFile["Camera1"]["ConnectionName"];
                }

                if (oIniFile["Camera1"]["Exposure"] != null && oIniFile["Camera1"]["Exposure"].Trim() != "") {
                    nExposure2 = Convert.ToInt32(Convert.ToDouble(oIniFile["Camera1"]["Exposure"]));
                    //trkExp2.Value = nExposure2;
                    //    
                    //taskcamera2 = Task.Run(() =>
                    //        TaskProcessCamera2());
                }

                //if (oIniFile["Options1"]["StretchImage"] != null && oIniFile["Options1"]["StretchImage"].Trim() != "")
                //{
                //    string sBool = oIniFile["Options1"]["StretchImage"];
                //    if (sBool.Trim().ToLower() == "true")
                //        chkStretchImage2.Checked = true;
                //    else
                //        chkStretchImage2.Checked = false;
                //}
            }

            //if (oIniFile["JogDisplacement"]["X"] != null && oIniFile["JogDisplacement"]["X"].Trim() != "")
            //    numericUpDown1.Value = Convert.ToDecimal(oIniFile["JogDisplacement"]["X"]);
            //if (oIniFile["JogDisplacement"]["Z"] != null && oIniFile["JogDisplacement"]["Z"].Trim() != "")
            //    numericUpDown2.Value = Convert.ToDecimal(oIniFile["JogDisplacement"]["Z"]);
            //if (oIniFile["JogDisplacement"]["Rotation"] != null && oIniFile["JogDisplacement"]["Rotation"].Trim() != "")
            //    numericUpDown3.Value = Convert.ToDecimal(oIniFile["JogDisplacement"]["Rotation"]);
            //if (oIniFile["Calibration"]["CalibrationRatio"] != null && oIniFile["Calibration"]["CalibrationRatio"].Trim() != "")
            //    txtScale.Text = oIniFile["Calibration"]["CalibrationRatio"];
            //
            //// Rotate the image 180°
            //if (oIniFile["RotateImage180"]["Rotate1"] != null && oIniFile["RotateImage180"]["Rotate1"].Trim() != "") {
            //    string sBool = oIniFile["RotateImage180"]["Rotate1"];
            //    if (sBool.Trim().ToLower() == "true")
            //        chkRotate1.Checked = true;
            //    else
            //        chkRotate1.Checked = false;
            //}
            //if (oIniFile["RotateImage180"]["Rotate2"] != null && oIniFile["RotateImage180"]["Rotate2"].Trim() != "") {
            //    string sBool = oIniFile["RotateImage180"]["Rotate2"];
            //    if (sBool.Trim().ToLower() == "true")
            //        chkRotate2.Checked = true;
            //    else
            //        chkRotate2.Checked = false;
            //}
            //if (oIniFile["RotateImage180"]["Rotate3"] != null && oIniFile["RotateImage180"]["Rotate3"].Trim() != "") {
            //    string sBool = oIniFile["RotateImage180"]["Rotate3"];
            //    if (sBool.Trim().ToLower() == "true")
            //        chkRotate3.Checked = true;
            //    else
            //        chkRotate3.Checked = false;
            //}
            //if (oIniFile["RotateImage180"]["Rotate4"] != null && oIniFile["RotateImage180"]["Rotate4"].Trim() != "") {
            //    string sBool = oIniFile["RotateImage180"]["Rotate4"];
            //    if (sBool.Trim().ToLower() == "true")
            //        chkRotate4.Checked = true;
            //    else
            //        chkRotate4.Checked = false;
            //}
            //// Rotate the image 90° right
            //if (oIniFile["RotateImage90right"]["Rotate1"] != null && oIniFile["RotateImage90right"]["Rotate1"].Trim() != "") {
            //    string sBool = oIniFile["RotateImage90right"]["Rotate1"];
            //    if (sBool.Trim().ToLower() == "true")
            //        chkRotate1r.Checked = true;
            //    else
            //        chkRotate1r.Checked = false;
            //}
            //if (oIniFile["RotateImage90right"]["Rotate2"] != null && oIniFile["RotateImage90right"]["Rotate2"].Trim() != "") {
            //    string sBool = oIniFile["RotateImage90right"]["Rotate2"];
            //    if (sBool.Trim().ToLower() == "true")
            //        chkRotate2r.Checked = true;
            //    else
            //        chkRotate2r.Checked = false;
            //}
            //if (oIniFile["RotateImage90right"]["Rotate3"] != null && oIniFile["RotateImage90right"]["Rotate3"].Trim() != "") {
            //    string sBool = oIniFile["RotateImage90right"]["Rotate3"];
            //    if (sBool.Trim().ToLower() == "true")
            //        chkRotate3r.Checked = true;
            //    else
            //        chkRotate3r.Checked = false;
            //}
            //if (oIniFile["RotateImage90right"]["Rotate4"] != null && oIniFile["RotateImage90right"]["Rotate4"].Trim() != "") {
            //    string sBool = oIniFile["RotateImage90right"]["Rotate4"];
            //    if (sBool.Trim().ToLower() == "true")
            //        chkRotate4r.Checked = true;
            //    else
            //        chkRotate4r.Checked = false;
            //}
            //// Rotate the image 90° left
            //if (oIniFile["RotateImage90left"]["Rotate1"] != null && oIniFile["RotateImage90left"]["Rotate1"].Trim() != "") {
            //    string sBool = oIniFile["RotateImage90left"]["Rotate1"];
            //    if (sBool.Trim().ToLower() == "true")
            //        chkRotate1l.Checked = true;
            //    else
            //        chkRotate1l.Checked = false;
            //}
            //if (oIniFile["RotateImage90left"]["Rotate2"] != null && oIniFile["RotateImage90left"]["Rotate2"].Trim() != "") {
            //    string sBool = oIniFile["RotateImage90left"]["Rotate2"];
            //    if (sBool.Trim().ToLower() == "true")
            //        chkRotate2l.Checked = true;
            //    else
            //        chkRotate2l.Checked = false;
            //}
            //if (oIniFile["RotateImage90left"]["Rotate3"] != null && oIniFile["RotateImage90left"]["Rotate3"].Trim() != "") {
            //    string sBool = oIniFile["RotateImage90left"]["Rotate3"];
            //    if (sBool.Trim().ToLower() == "true")
            //        chkRotate3l.Checked = true;
            //    else
            //        chkRotate3l.Checked = false;
            //}
            //if (oIniFile["RotateImage90left"]["Rotate4"] != null && oIniFile["RotateImage90left"]["Rotate4"].Trim() != "") {
            //    string sBool = oIniFile["RotateImage90left"]["Rotate4"];
            //    if (sBool.Trim().ToLower() == "true")
            //        chkRotate4l.Checked = true;
            //    else
            //        chkRotate4l.Checked = false;
            //}

            //Debug mode
            if (oIniFile["Debug"]["Debug"] != null && oIniFile["Debug"]["Debug"].Trim() != "")
            {
                string sBool = oIniFile["Debug"]["Debug"];
                if (sBool.Trim().ToLower() == "true") {
                    bDebugMode = true;
                    chkEmul.Checked = true;
                } else {
                    bDebugMode = false;
                    chkEmul.Checked = false;
                }
            }

            //bool bLoadROI = false;
            //if (bLoadExposure == true) {
            //    if (oIniFile["Camera" + nCam.ToString()]["Exposure"] != null && oIniFile["Camera" + nCam.ToString()]["Exposure"].Trim() != "") {
            //        string s = oIniFile["Camera" + nCam.ToString()]["Exposure"];
            //        if (nCam == 1 && camera1 != null) {
            //            try {
            //                if (camera1.Parameters.Contains(PLCamera.ExposureTimeAbs)) {
            //                    try {
            //                        camera1.Parameters[PLCamera.ExposureTimeAbs].TrySetValue(Convert.ToDouble(s), FloatValueCorrection.ClipToRange);
            //                        trkExp1.Value = Convert.ToInt32(camera1.Parameters[PLCamera.ExposureTimeAbs].ToString());
            //                    } catch { }
            //                } else {
            //                    try {
            //                        camera1.Parameters[PLCamera.ExposureTime].TrySetValue(Convert.ToDouble(s), FloatValueCorrection.ClipToRange);
            //                        trkExp1.Value = Convert.ToInt32(camera1.Parameters[PLCamera.ExposureTime].ToString());
            //                    } catch { }
            //                }
            //            }
            //            catch { }
            //        } else if (nCam == 2 && camera2 != null) {
            //            try {
            //                if (camera2.Parameters.Contains(PLCamera.ExposureTimeAbs)) {
            //                    try {
            //                        camera2.Parameters[PLCamera.ExposureTimeAbs].TrySetValue(Convert.ToDouble(s), FloatValueCorrection.ClipToRange);
            //                        trkExp2.Value = Convert.ToInt32(camera2.Parameters[PLCamera.ExposureTimeAbs].ToString());
            //                    } catch { }
            //                } else {
            //                    try {
            //                        camera2.Parameters[PLCamera.ExposureTime].TrySetValue(Convert.ToDouble(s), FloatValueCorrection.ClipToRange);
            //                        trkExp2.Value = Convert.ToInt32(camera2.Parameters[PLCamera.ExposureTime].ToString());
            //                    } catch { }
            //                }
            //            } catch { }
            //        }
            //    }
            //}
            //
            //if (bLoadROI == true) {
            //    if (nCam == 1 || nCam == 2) {
            //        for (int i = 0; i < 4; i++) {
            //            if (oIniFile["SearchArea" + nCam.ToString()]["RectPointX" + i.ToString()] != null && oIniFile["SearchArea" + nCam.ToString()]["RectPointX" + i.ToString()].Trim() != "")
            //                fRecPointX[nCam - 1, i] = Convert.ToSingle(oIniFile["SearchArea" + nCam.ToString()]["RectPointX" + i.ToString()]);
            //            else
            //                fRecPointX[nCam - 1, i] = 0;
            //            if (oIniFile["SearchArea" + nCam.ToString()]["RectPointY" + i.ToString()] != null && oIniFile["SearchArea" + nCam.ToString()]["RectPointY" + i.ToString()].Trim() != "")
            //                fRecPointY[nCam - 1, i] = Convert.ToSingle(oIniFile["SearchArea" + nCam.ToString()]["RectPointY" + i.ToString()]);
            //            else
            //                fRecPointY[nCam - 1, i] = 0;
            //        }
            //        if (oIniFile["SearchArea" + nCam.ToString()]["SearchAreaWidth"] != null && oIniFile["SearchArea" + nCam.ToString()]["SearchAreaWidth"].Trim() != "")
            //            fWidth[nCam - 1] = Convert.ToSingle(oIniFile["SearchArea" + nCam.ToString()]["SearchAreaWidth"]);
            //        else
            //            fWidth[nCam - 1] = 0;
            //        if (oIniFile["SearchArea" + nCam.ToString()]["SearchAreaHeight"] != null && oIniFile["SearchArea" + nCam.ToString()]["SearchAreaHeight"].Trim() != "")
            //            fHeight[nCam - 1] = Convert.ToSingle(oIniFile["SearchArea" + nCam.ToString()]["SearchAreaHeight"]);
            //        else
            //            fHeight[nCam - 1] = 0;
            //    }
            //} else {
            //    for (int i = 0; i < 4; i++) { 
            //        fRecPointX[nCam-1,i] = 0;
            //        fRecPointY[nCam-1,i] = 0;
            //    }
            //    fWidth[nCam-1] = 0;
            //    fHeight[nCam-1] = 0;
            //}
        }

        public void trkExp1_Scroll(object sender, EventArgs e)
        {
            if (sender == trkExp1 && camera1 != null)
            {
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

                        if (Convert.ToBoolean(inv.get(optSnap1, "Checked")))
                            nExposureInspection = val;
                        else if (Convert.ToBoolean(inv.get(optSnap2, "Checked")))
                            nExposureDiameter = val;

                    }
                    catch (Exception ex) {
                        MessageBox.Show("Set exposition error: " + ex.Message, "trkExp_Scrol", MessageBoxButtons.OK, MessageBoxIcon.Error, MessageBoxDefaultButton.Button1);
                    }
                    //exposureTimeSliderControl.Parameter = camera.Parameters[PLCamera.ExposureTimeAbs];
                } else {
                    try {
                        camera1.Parameters[PLCamera.ExposureTime].TrySetValue(val, FloatValueCorrection.ClipToRange);
                        Thread.Sleep(5);

                        if (Convert.ToBoolean(inv.get(optSnap1, "Checked")))
                            nExposureInspection = val;
                        else if (Convert.ToBoolean(inv.get(optSnap2, "Checked")))
                            nExposureDiameter = val;
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
            //    } catch {
            //        MessageBox.Show("Set exposition error", "", MessageBoxButtons.OK, MessageBoxIcon.Error, MessageBoxDefaultButton.Button1); //, MessageBoxOptions.DefaultDesktopOnly);
            //    }
            //}
            //else
            //    MessageBox.Show("Acquisition is nothing !", "", MessageBoxButtons.OK, MessageBoxIcon.Error, MessageBoxDefaultButton.Button1); //, MessageBoxOptions.DefaultDesktopOnly);
            //
            //// To ensure releasing of local references to Hexsight ActiveX objects 
            //lAcquisition = null;
            ////GC.Collect();
            ////GC.WaitForPendingFinalizers();
        }

        public async Task<int> TaskProcessCamera1()
        {
            while (bfrmBeckhoff_Shown == false) {
                await Task.Delay(10);
            }

            trkExp_Scroll(trkExp1, null);

            //taskcamera1.Dispose();
            return (0);
        }

        private void btnSaveSettings_Click(object sender, EventArgs e)
        {
            int nCam = 0;
            if (sender == btnSaveSettings1) nCam = 1;
            //else if (sender == btnSaveSettings2) nCam = 2;
            if (nCam > 0) SaveIni(nCam);
        }

        public void SaveIni(int nCam)
        {
            IniFiles.IniFile oIniFile = IniFiles.IniFile.FromFile(aPath + "\\BeckhoffBasler.ini");

            if (nCam == 1) // && camera1 != null)
            {
                try
                {
                    if (camera1 != null)
                    {
                        string s = "0";
                        if (camera1.Parameters.Contains(PLCamera.ExposureTimeAbs))
                            s = camera1.Parameters[PLCamera.ExposureTimeAbs].ToString();
                        else
                            s = camera1.Parameters[PLCamera.ExposureTime].ToString();

                        //DialogResult dialogResult = MessageBox.Show("Do you want to save the Exposure value for Top Camera ?", "Save BeckhoffBasler.ini",
                        //        MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2); //Sure
                        //if (dialogResult == DialogResult.Yes) {

                        if (Convert.ToBoolean(inv.get(optSnap1, "Checked")))
                            oIniFile["Camera2"]["ExposureInspection"] = s;
                        else if (Convert.ToBoolean(inv.get(optSnap2, "Checked")))
                            oIniFile["Camera2"]["ExposureDiameter"] = s;

                        //if (txtConnectionName1.Text.Trim() != "") {
                        //    oIniFile["Camera2"]["ConnectionName"] = txtConnectionName1.Text.Trim();
                        //}

                        if (chkLightSource.Checked)
                            oIniFile["Camera2"]["LightSourceSelector"] = "1";
                        else if (chkLightSourceOff.Checked)
                            oIniFile["Camera2"]["LightSourceSelector"] = "2";
                        else
                            oIniFile["Camera2"]["LightSourceSelector"] = "0";
                    }

                    if (chkUseSearchArea.Checked)
                        oIniFile["SearchArea2"]["UseSearchArea"] = "true";
                    else
                        oIniFile["SearchArea2"]["UseSearchArea"] = "false";

                    if (IsNumeric(txtRectPointX.Text) && IsNumeric(txtRectPointY.Text) && IsNumeric(txtSearchAreaWidth.Text) && IsNumeric(txtSearchAreaHeight.Text))
                    {
                        oIniFile["SearchArea2"]["RectPointX"] = txtRectPointX.Text;
                        oIniFile["SearchArea2"]["RectPointY"] = txtRectPointY.Text;
                        oIniFile["SearchArea2"]["SearchAreaWidth"] = txtSearchAreaWidth.Text;
                        oIniFile["SearchArea2"]["SearchAreaHeight"] = txtSearchAreaHeight.Text;
                    }
                    else
                        MessageBox.Show("Incorrect coordinate field in Search Area Top Camera", "Save Ini", MessageBoxButtons.OK, MessageBoxIcon.Error);

                    if (chkStretchImage1.Checked)
                        oIniFile["Options2"]["StretchImage"] = "true";
                    else
                        oIniFile["Options2"]["StretchImage"] = "false";

                    if (chkFillWeldonGreen.Checked)
                        oIniFile["Options2"]["FillWeldonGreen"] = "true";
                    else
                        oIniFile["Options2"]["FillWeldonGreen"] = "false";


                    if (IsNumeric(txtDiamNominal.Text) && IsNumeric(txtDiamNominal.Text))
                        oIniFile["Weldon"]["DiamNominal"] = txtDiamNominal.Text;
                    else
                        MessageBox.Show("Incorrect value field in Weldon", "Save Ini", MessageBoxButtons.OK, MessageBoxIcon.Error);

                    if (IsNumeric(txtDiamActual.Text) && IsNumeric(txtDiamActual.Text))
                        oIniFile["Weldon"]["DiamActual"] = txtDiamActual.Text;
                    else
                        MessageBox.Show("Incorrect value field in Weldon", "Save Ini", MessageBoxButtons.OK, MessageBoxIcon.Error);

                    if (IsNumeric(txtROIL.Text) && IsNumeric(txtROIL.Text))
                        oIniFile["Weldon"]["ROIL"] = txtROIL.Text;
                    else
                        MessageBox.Show("Incorrect value field in Weldon", "Save Ini", MessageBoxButtons.OK, MessageBoxIcon.Error);

                    if (IsNumeric(txtROIR.Text) && IsNumeric(txtROIR.Text))
                        oIniFile["Weldon"]["ROIR"] = txtROIR.Text;
                    else
                        MessageBox.Show("Incorrect value field in Weldon", "Save Ini", MessageBoxButtons.OK, MessageBoxIcon.Error);

                    if (IsNumeric(txtLineWidth.Text) && IsNumeric(txtLineWidth.Text))
                        oIniFile["Weldon"]["LineWidth"] = txtLineWidth.Text;
                    else
                        MessageBox.Show("Incorrect value field in Weldon", "Save Ini", MessageBoxButtons.OK, MessageBoxIcon.Error);


                    if (IsNumeric(txtCalib.Text) && IsNumeric(txtCalib.Text))
                        oIniFile["Calibration2"]["CalibrationRatio"] = txtCalib.Text;
                    else
                        MessageBox.Show("Incorrect value field in Weldon", "Save Ini", MessageBoxButtons.OK, MessageBoxIcon.Error);

                    // Histogram

                    if (IsNumeric(txtMinBrDiff.Text) && IsNumeric(txtMaxBrDiff.Text)) {
                        oIniFile["Histogram"]["MinBrightnessDifferenceFromGray"] = txtMinBrDiff.Text;
                        oIniFile["Histogram"]["MaxBrightnessDifferenceFromGray"] = txtMaxBrDiff.Text;
                    } else
                        MessageBox.Show("Incorrect value field in Histogram Min/Max Brightness Difference From Gray", "Save Ini", MessageBoxButtons.OK, MessageBoxIcon.Error);

                    if (IsNumeric(txtMinBr.Text))
                        oIniFile["Histogram"]["MinBrightness"] = txtMinBr.Text;
                    else
                        MessageBox.Show("Incorrect value field in Histogram Min Brightness", "Save Ini", MessageBoxButtons.OK, MessageBoxIcon.Error);

                    if (IsNumeric(txtColorThreshold.Text))
                        oIniFile["Histogram"]["MinRelativeRedBrightnessColorImageThreshold"] = txtColorThreshold.Text;
                    else
                        MessageBox.Show("Incorrect value field in Histogram Min Relative Red Brightness Color Image Threshold", "Save Ini", MessageBoxButtons.OK, MessageBoxIcon.Error);

                    if (chkShowHist.Checked)
                        oIniFile["Histogram"]["ShowHistogram"] = "true";
                    else
                        oIniFile["Histogram"]["ShowHistogram"] = "false";

                    if (chkRed.Checked)
                        oIniFile["Histogram"]["ShowOnlyRed"] = "true";
                    else
                        oIniFile["Histogram"]["ShowOnlyRed"] = "false";


                    //if (chkRotate1.Checked)
                    //    oIniFile["RotateImage180"]["Rotate1"] = "true";
                    //else
                    //    oIniFile["RotateImage180"]["Rotate1"] = "false";
                    //if (chkRotate2.Checked)
                    //    oIniFile["RotateImage180"]["Rotate2"] = "true";
                    //else
                    //    oIniFile["RotateImage180"]["Rotate2"] = "false";
                    //if (chkRotate3.Checked)
                    //    oIniFile["RotateImage180"]["Rotate3"] = "true";
                    //else
                    //    oIniFile["RotateImage180"]["Rotate3"] = "false";
                    //if (chkRotate4.Checked)
                    //    oIniFile["RotateImage180"]["Rotate4"] = "true";
                    //else
                    //    oIniFile["RotateImage180"]["Rotate4"] = "false";
                    //
                    //if (chkRotate1r.Checked)
                    //    oIniFile["RotateImage90right"]["Rotate1"] = "true";
                    //else
                    //    oIniFile["RotateImage90right"]["Rotate1"] = "false";
                    //if (chkRotate2r.Checked)
                    //    oIniFile["RotateImage90right"]["Rotate2"] = "true";
                    //else
                    //    oIniFile["RotateImage90right"]["Rotate2"] = "false";
                    //if (chkRotate3r.Checked)
                    //    oIniFile["RotateImage90right"]["Rotate3"] = "true";
                    //else
                    //    oIniFile["RotateImage90right"]["Rotate3"] = "false";
                    //if (chkRotate4r.Checked)
                    //    oIniFile["RotateImage90right"]["Rotate4"] = "true";
                    //else
                    //    oIniFile["RotateImage90right"]["Rotate4"] = "false";
                    //
                    //if (chkRotate1l.Checked)
                    //    oIniFile["RotateImage90left"]["Rotate1"] = "true";
                    //else
                    //    oIniFile["RotateImage90left"]["Rotate1"] = "false";
                    //if (chkRotate2l.Checked)
                    //    oIniFile["RotateImage90left"]["Rotate2"] = "true";
                    //else
                    //    oIniFile["RotateImage90left"]["Rotate2"] = "false";
                    //if (chkRotate3l.Checked)
                    //    oIniFile["RotateImage90left"]["Rotate3"] = "true";
                    //else
                    //    oIniFile["RotateImage90left"]["Rotate3"] = "false";
                    //if (chkRotate4l.Checked)
                    //    oIniFile["RotateImage90left"]["Rotate4"] = "true";
                    //else
                    //    oIniFile["RotateImage90left"]["Rotate4"] = "false";
                }
                catch (Exception exception) {
                    ShowException("SaveIni Camera Top", exception);
                }

            }
            else if (nCam == 2) // && camera2 != null)
            {
                try
                {
                    if (camera2 != null)
                    {
                        string s = "0";
                        if (camera2.Parameters.Contains(PLCamera.ExposureTimeAbs))
                            s = camera2.Parameters[PLCamera.ExposureTimeAbs].ToString();
                        else
                            s = camera2.Parameters[PLCamera.ExposureTime].ToString();

                        //DialogResult dialogResult = MessageBox.Show("Do you want to save the Exposure value for Side Camera ?", "Save BeckhoffBasler.ini",
                        //        MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2); //Sure
                        //if (dialogResult == DialogResult.Yes) {
                        oIniFile["Camera1"]["Exposure"] = s;

                        //if (txtConnectionName2.Text.Trim() != "") {
                        //    oIniFile["Camera1"]["ConnectionName"] = txtConnectionName2.Text.Trim();
                        //}
                    }
                    //if (chkUseSearchArea2.Checked)
                    //    oIniFile["SearchArea1"]["UseSearchArea"] = "true";
                    //else
                    //    oIniFile["SearchArea1"]["UseSearchArea"] = "false";

                    //if (chkStretchImage2.Checked)
                    //    oIniFile["Options1"]["StretchImage"] = "true";
                    //else
                    //    oIniFile["Options1"]["StretchImage"] = "false";


                }
                catch (Exception exception) {
                    ShowException("SaveIni Camera Side", exception);
                }
            }

            if (chkEmul.Checked)
                oIniFile["Debug"]["Debug"] = "true";
            else
                oIniFile["Debug"]["Debug"] = "false";

            //oIniFile["JogDisplacement"]["X"] = numericUpDown1.Value.ToString();
            //oIniFile["JogDisplacement"]["Z"] = numericUpDown2.Value.ToString();
            //oIniFile["JogDisplacement"]["Rotation"] = numericUpDown3.Value.ToString();

            oIniFile.Save();
        }

        private void frmBeckhoff_Layout(object sender, LayoutEventArgs e)
        {
            //if (m_bLayoutCalled == false)
            //{
            //    m_bLayoutCalled = true;
            //    m_dt = DateTime.Now;
            //    this.Activate();
            //    SplashScreen.CloseForm();
            //}
        }

        private void frmBeckhoff_Shown(object sender, EventArgs e)
        {
            try
            {
                if (!bfrmBeckhoff_Shown)
                {
                    //SplashScreen.CloseSplashScreen1();

                    chkStretchImage1.Checked = true;
                    chkStretchImage2.Checked = true;

                    //chkStretchImage2.Checked = true;
                    //Application.DoEvents();
                    frmMainInspect.ListClicked += ListSelected;
                    SetWindowPos(this.Handle, IntPtr.Zero, 0, 0, this.Width, this.Height, SWP_SHOWWINDOW);
                    this.Focus();

                }


                bfrmBeckhoff_Shown = true;
                SplashScreen.CloseForm();
            }
            catch (Exception ex) { MessageBox.Show("Error Show Form!!!" + ex.Message); }
            //for (int NumOfPickCamera = 6; NumOfPickCamera >= 0; NumOfPickCamera--) {
            //    frmVision.mFormVisionDefInstance.tabControl1.SelectedIndex = NumOfPickCamera;
            //    frmVision.mFormVisionDefInstance.tabControl1.TabPages[NumOfPickCamera].Parent.Focus();
            //    frmVision.mFormVisionDefInstance.tabControl1_Click(sender, e);
            //    if (NumOfPickCamera >= 1 && NumOfPickCamera <= 6) {
            //        int rc = frmVision.mFormVisionDefInstance.Inspection(NumOfPickCamera, null, false);
            //    }
            //    Application.DoEvents();
            //}
        }

        private static bool IsNumeric(System.Object Expression)
        {
            if (Expression == null || Expression is DateTime)
                return false;
            if (Expression is Int16 || Expression is Int32 || Expression is Int64 || Expression is Decimal ||
                    Expression is Single || Expression is Double || Expression is Boolean)
                return true;
            try
            {
                if (Expression is string)
                    Double.Parse(Expression as string);
                else
                    Double.Parse(Expression.ToString());
                return true;
            }
            catch { } // just dismiss errors but return false
            return false;
        }

        #region --------- Basler live ---------
        // Occurs when the single frame acquisition button is clicked.
        private void toolStripButtonOneShot_Click(object sender, EventArgs e)
        {
            if (sender == toolStrip1ButtonOneShot)
                OneShot(1); // Start the grabbing of one image.
            //else if (sender == toolStrip2ButtonOneShot)
            //    OneShot(2);
        }

        // Occurs when the continuous frame acquisition button is clicked.
        private void toolStripButtonContinuousShot_Click(object sender, EventArgs e)
        {
            if (sender == toolStrip1ButtonContinuousShot)
                ContinuousShot(1); // Start the grabbing of images until grabbing is stopped.
            //else if (sender == toolStrip2ButtonContinuousShot)
            //    ContinuousShot(2);
        }

        // Occurs when the stop frame acquisition button is clicked.
        private void toolStripButtonStop_Click(object sender, EventArgs e)
        {
            if (sender == toolStrip1ButtonStop)
                Stop(1); // Stop the grabbing of images.
            //else if (sender == toolStrip2ButtonStop)
            //    Stop(2);
        }

        // Starts the grabbing of a single image and handles exceptions.
        private void OneShot(int nCam)
        {
            try
            {
                IGrabResult grabResult;
                if (nCam == 1)
                {
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

                if (!(grabResult is null))
                {
                    using (grabResult)
                    {
                        if (grabResult.GrabSucceeded)
                        {
                            // Access the image data.
                            //Console.WriteLine("SizeX: {0}", grabResult.Width); Console.WriteLine("SizeY: {0}", grabResult.Height);
                            byte[] buffer = grabResult.PixelData as byte[];

                            MemoryStream ms = new MemoryStream(buffer);
                            Image im = Image.FromStream(ms);
                            im.Save(aPath + "\\Images\\OneSnap" + ".jpg", ImageFormat.Jpeg); // (@"C:\snap.jpg", ImageFormat.Jpeg);
                            for (int i = 0; i < frmMainInspect.SnapFile.Length; i++) frmMainInspect.SnapFile[i] = "";
                            //for (int i = 0; i < frmMainInspect.Mstream.Length; i++) frmMainInspect.Mstream[i] = null;
                            frmMainInspect.SnapFile[0] = aPath + "\\Images\\OneSnap" + ".jpg";
                            //Console.WriteLine("Gray value of first pixel: {0}", buffer[0]); Console.WriteLine("");
                            //ImageWindow.DisplayImage(0, grabResult); // Display the grabbed image.
                        }
                        else
                        {
                            //Console.WriteLine("Error: {0} {1}", grabResult.ErrorCode, grabResult.ErrorDescription);
                        }
                    }
                }
                else
                {
                    MessageBox.Show("StreamGrabber.RetrieveResult camera " + nCam.ToString() + " error.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            catch (Exception exception)
            {
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

        private void btnOpenLive_Click(object sender, EventArgs e)
        {
            if (sender == btnOpenLive1)
                UpdateDeviceList(1);
            //else if (sender == btnOpenLive2)
            //    UpdateDeviceList(2);
        }

        // Starts the continuous grabbing of images and handles exceptions.
        private void ContinuousShot(int nCam)
        {
            try
            {
                // Start the grabbing of images until grabbing is stopped.
                if (nCam == 1)
                {
                    camera1.Parameters[PLCamera.AcquisitionMode].SetValue(PLCamera.AcquisitionMode.Continuous);
                    camera1.StreamGrabber.Start(GrabStrategy.OneByOne, GrabLoop.ProvidedByStreamGrabber);
                }
                //else if (nCam == 2) {
                //    camera2.Parameters[PLCamera.AcquisitionMode].SetValue(PLCamera.AcquisitionMode.Continuous);
                //    camera2.StreamGrabber.Start(GrabStrategy.OneByOne, GrabLoop.ProvidedByStreamGrabber);
                //}
                else return;
            }
            catch (Exception exception)
            {
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
            try
            {
                if (nCam == 1)
                    camera1.StreamGrabber.Stop();
                //else if (nCam == 2)
                //    camera2.StreamGrabber.Stop();
                else return;
            }
            catch (Exception exception)
            {
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
            try
            {
                if (nCam == 1 && camera1 != null)
                {
                    try
                    {
                        if (!(camera1.StreamGrabber == null))
                            camera1.StreamGrabber.Stop();
                    }
                    catch { }

                    camera1.Close();
                    camera1.Dispose();
                    camera1 = null;

                    btnOpenLive1.Enabled = true; btnOpenLive1.BackColor = Color.FromArgb(170, 222, 255);
                    btnCloseLive1.Enabled = false; btnCloseLive1.BackColor = Color.Gainsboro;
                    toolStrip1.Enabled = false;
                    trkExp1.Enabled = false;

                    btnSnap.Enabled = false;
                }
                else if (nCam == 2 && camera2 != null)
                {
                    try
                    {
                        camera2.StreamGrabber.Stop();
                    }
                    catch { }

                    camera2.Close();
                    camera2.Dispose();
                    camera2 = null;
                    Thread.Sleep(1000);

                }
            }
            catch (Exception exception)
            {
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
            try
            {
                bool bFound = false;
                ICameraInfo tag = null;
                List<ICameraInfo> allCameras = CameraFinder.Enumerate();
                foreach (ICameraInfo cameraInfo in allCameras) { // Loop over all cameras found
                    string s = cameraInfo[CameraInfoKey.FriendlyName];
                    if (nCam == 1 && s.Contains(sFriendlyName1)) { //if (nCam == 1 && s.Contains(sFriendlyName1) || nCam == 2 && s.Contains(sFriendlyName2))
                        tag = cameraInfo;
                        bFound = true;
                        //IpConfigurationMethod ip;
                        //tag.
                        //string ipaddr = cameraInfo.  AnnounceRemoteDevice
                        break;
                    }
                }
                if (bFound)
                {
                    if (nCam == 1)
                    {
                        lblBaslerLive1.Text = tag["SerialNumber"].ToString();
                        btnOpenLive1.Enabled = false; btnOpenLive1.BackColor = Color.Gainsboro;
                        btnCloseLive1.Enabled = true; btnCloseLive1.BackColor = Color.FromArgb(170, 222, 255);
                        toolStrip1.Enabled = true;
                        btnSnap.Enabled = true;
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
            catch (Exception exception)
            {
                string source = "";
                if (nCam == 1)
                    source = "UpdateDeviceList camera 1 ";
                //else if (nCam == 2)
                //    source = "UpdateDeviceList camera 2 ";
                ShowException(source, exception);
            }
        }


        const long giMaximalCameraWidth = 5320;
        const long giMaximalCameraHeight = 3032;
        Dictionary<int, int[]> _iSnapFullImage = new Dictionary<int, int[]>();
        bool _bIsInFullImage = false;
        int giPixelTolerance = 100;


        //According to cmbSnapShotStrategy
        //All Images Half Height
        //All Images Full Size
        //All Images ROI Based
        //Images Half Height. Full Size ColorHistogram
        //Images ROI Based. Full Size ColorHistogram

        //CAUTION!!! Do NOT change cmbSnapShotStrategy values, indices or order withhout changing eSnapShotStrategy accordinlgy

        enum eSnapShotStrategy
        {
            eSnapShotStrategyOnlyHalfImages = 0,
            eSnapShotStrategyOnlyFullImages = 1,
            eSnapShotStrategyOnlyGeographicROIBasedImages = 2,
            eSnapShotStrategyFullImagesForColorHistogramHalfImagesForTheRest = 3,
            eSnapShotStrategyFullImagesForColorHistogramGeographicROIBasedImagesForTheRest = 4
        }
        eSnapShotStrategy _eSnapShotStrategy = eSnapShotStrategy.eSnapShotStrategyFullImagesForColorHistogramGeographicROIBasedImagesForTheRest;
        enum eCurrentSnapShotAOI
        {
            eCurrentSnapShotAOI_NOTSETYET = -1,
            eCurrentSnapShotAOIFullImages = 1,
            eCurrentSnapShotAOIHalfImages = 2,
            eCurrentSnapShotAOIGeographicROIBasedImages = 3,
        }
        eCurrentSnapShotAOI _eCurrentSnapShotAOI = eCurrentSnapShotAOI.eCurrentSnapShotAOI_NOTSETYET;

        void PopulateFullImagesIndices()
        {
            _iSnapFullImage[8] = new int[2] { 0, 4 };
            _iSnapFullImage[12] = new int[4] { 0, 3, 6, 9 };
            _iSnapFullImage[16] = new int[4] { 0, 4, 8, 12 };
            //_iSnapFullImage[16] = new int[16] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15 };

            //_iSnapFullImage[8] = new int[1] { 7 };
            //_iSnapFullImage[12] = new int[1] { 11};
            //_iSnapFullImage[16] = new int[1] { 15};

        }

        // Configure native AOI on the camera (stop grabbing before calling).
        bool bCameraAOIDefined = true;

        // Configure native AOI on the camera (stop grabbing before calling).
        void ConfigureWholeImageCameraAOI(Camera camera)
        {
            //full image size is 5320 X 3032
            ConfigureCameraAOI(camera, 0, 0, giMaximalCameraWidth, giMaximalCameraHeight);
        }

        void ConfigureHalfImageCameraAOI(Camera camera)
        {
            //half image size is 5320 X 1516
            ConfigureCameraAOI(camera, 0, 0, giMaximalCameraWidth, giMaximalCameraHeight / 2);
        }

        public (int iAOIOffsetX, int iAOIOffsetY, int iAOIWidth, int iAOIHeight) CalculateRoiBasedCameraAOI(int iPixelTolerance)
        {
            long iROIRightMostRightValue = frmMainInspect.CalcRightmostCroppingRight(iPixelTolerance);
            iROIRightMostRightValue = (iROIRightMostRightValue > giMaximalCameraWidth ? giMaximalCameraWidth : iROIRightMostRightValue);

            int iROIHighestCroppingTop = frmMainInspect.CalcHighestCroppingTop(iPixelTolerance);
            iROIHighestCroppingTop = (iROIHighestCroppingTop <= 0 ? 0 : iROIHighestCroppingTop);

            int iAOIOffsetX=0, iAOIOffsetY=iROIHighestCroppingTop, iAOIWidth=(int)iROIRightMostRightValue, 
                iAOIHeight=(int)giMaximalCameraHeight / 2 - iROIHighestCroppingTop;

            if (iROIHighestCroppingTop >= giMaximalCameraHeight / 2 - iPixelTolerance)
            {
                iROIHighestCroppingTop = (int)giMaximalCameraHeight / 4;
                MessageBox.Show($"Cropping top is below the middle of the image. Setting it to one quarter of the height {giMaximalCameraHeight / 4}");
            }

            return (iAOIOffsetX, iAOIOffsetY, iAOIWidth, iAOIHeight);

        }

        void ConfigureRoiBasedCameraAOI(Camera camera)
        {
            // cropping the top part and the right part of the image:
            //
            // 1. the cropping of the right part of the image:
            //   calculated from 0 to a point the includes the rightmost point of the 3 ROIs + a pixel tolerance
            // 2. the cropping of the top part:
            //   calculated from the middle of the image (height: 3032) a point the includes the top point of the 3 ROIs + a pixel tolerance

            var result = CalculateRoiBasedCameraAOI(giPixelTolerance);

            ConfigureCameraAOI(camera, result.iAOIOffsetX, result.iAOIOffsetY, result.iAOIWidth, result.iAOIHeight);

            //long iROIRightMostRightValue = result.frmMainInspect.CalcRightmostCroppingRight(iPixelTolerance);
            //iROIRightMostRightValue = (iROIRightMostRightValue > giMaximalCameraWidth ? giMaximalCameraWidth : iROIRightMostRightValue);

            //int iROIHighestCroppingTop = frmMainInspect.CalcHighestCroppingTop(iPixelTolerance);
            //iROIHighestCroppingTop = (iROIHighestCroppingTop <= 0 ? 0 : iROIHighestCroppingTop);
            //if (iROIHighestCroppingTop >= giMaximalCameraHeight / 2 - iPixelTolerance)
            //{
            //    iROIHighestCroppingTop = (int)giMaximalCameraHeight / 4;
            //    MessageBox.Show($"Cropping top is below the middle of the image. Setting it to one quarter of the height {giMaximalCameraHeight / 4}");
            //}

            //ConfigureCameraAOI(camera, 0, iROIHighestCroppingTop, iROIRightMostRightValue, giMaximalCameraHeight / 2 - iROIHighestCroppingTop);

        }

        void ConfigureCameraAOI2(Camera camera, int x, int y, int width, int height)
        {
            // Ensure not grabbing while changing AOI
            if (camera.StreamGrabber.IsGrabbing) camera.StreamGrabber.Stop();

            //// Disable centering if available
            //if (camera.Parameters[PLCamera.CenterX].IsWritable) camera.Parameters[PLCamera.CenterX].SetValue(false);
            //if (camera.Parameters[PLCamera.CenterY].IsWritable) camera.Parameters[PLCamera.CenterY].SetValue(false);

            // Reset offsets first (recommended order on many models)
            if (camera.Parameters[PLCamera.OffsetX].IsWritable) camera.Parameters[PLCamera.OffsetX].SetValue(0);
            if (camera.Parameters[PLCamera.OffsetY].IsWritable) camera.Parameters[PLCamera.OffsetY].SetValue(0);

            // Align and set width/height to device constraints
            //long w = Align(width,
            //    camera.Parameters[PLCamera.Width].GetMinimum(),
            //    camera.Parameters[PLCamera.Width].GetMaximum(),
            //    camera.Parameters[PLCamera.Width].GetIncrement());

            //long h = Align(height,
            //    camera.Parameters[PLCamera.Height].GetMinimum(),
            //    camera.Parameters[PLCamera.Height].GetMaximum(),
            //    camera.Parameters[PLCamera.Height].GetIncrement());

            camera.Parameters[PLCamera.Width].SetValue(width);
            camera.Parameters[PLCamera.Height].SetValue(height);

            // After setting width/height, the allowed max offsets may change; read again and align
            //long ox = Align(x,
            //    camera.Parameters[PLCamera.OffsetX].GetMinimum(),
            //    camera.Parameters[PLCamera.OffsetX].GetMaximum(),
            //    camera.Parameters[PLCamera.OffsetX].GetIncrement());

            //long oy = Align(y,
            //    camera.Parameters[PLCamera.OffsetY].GetMinimum(),
            //    camera.Parameters[PLCamera.OffsetY].GetMaximum(),
            //    camera.Parameters[PLCamera.OffsetY].GetIncrement());

            camera.Parameters[PLCamera.OffsetX].SetValue(x);
            camera.Parameters[PLCamera.OffsetY].SetValue(y);

            camera.StreamGrabber.ImageGrabbed -= OnImageGrabbed1;
            camera.StreamGrabber.ImageGrabbed += OnImageGrabbed1;


            if (camera.StreamGrabber.IsGrabbing) camera.StreamGrabber.Start();

        }


        void ConfigureCameraAOI(Camera camera, long x, long y, long width, long height)
        {

            //    //camera1.CameraOpened += Configuration.AcquireContinuous;

            //    //// Register for the events of the image provider needed for proper operation.
            //    //camera1.ConnectionLost += OnConnectionLost1;
            //    //camera1.CameraOpened += OnCameraOpened1;
            //    //camera1.CameraClosed += OnCameraClosed1;

            //    //camera1.StreamGrabber.GrabStarted += OnGrabStarted1;
            //    //camera1.StreamGrabber.ImageGrabbed += OnImageGrabbed1;
            //    //camera1.StreamGrabber.GrabStopped += OnGrabStopped1;

            //    //if (!camera1.IsOpen) camera1.Open();


            try
            {
                // Stop streaming if active
                if (camera.StreamGrabber.IsGrabbing)
                    camera.StreamGrabber.Stop();

                // Reset offsets
                if (camera.Parameters[PLCamera.OffsetX].IsWritable)
                    camera.Parameters[PLCamera.OffsetX].SetValue(0);
                if (camera.Parameters[PLCamera.OffsetY].IsWritable)
                    camera.Parameters[PLCamera.OffsetY].SetValue(0);


                camera1.CameraOpened += Configuration.AcquireContinuous;

                //some parameters have to have a value of multiples of an integer.
                //find that integer and make the values multiples of that integer
                width = (width / camera.Parameters[PLCamera.Width].GetIncrement()) * camera.Parameters[PLCamera.Width].GetIncrement();
                height = (height / camera.Parameters[PLCamera.Height].GetIncrement()) * camera.Parameters[PLCamera.Height].GetIncrement();
                x = (x / camera.Parameters[PLCamera.OffsetX].GetIncrement()) * camera.Parameters[PLCamera.OffsetX].GetIncrement();
                y = (y / camera.Parameters[PLCamera.OffsetY].GetIncrement()) * camera.Parameters[PLCamera.OffsetY].GetIncrement();

                // Set width/height
                long h = camera.Parameters[PLCamera.Height].GetValue();
                Console.WriteLine($"Height before change:{h} trying to change to {height}");
                camera.Parameters[PLCamera.Width].SetValue(width);
                camera.Parameters[PLCamera.Height].SetValue(height);

                // Set offsets again as needed
                camera.Parameters[PLCamera.OffsetX].SetValue(x);
                camera.Parameters[PLCamera.OffsetY].SetValue(y);

                // Manage event handler
                camera.StreamGrabber.ImageGrabbed -= OnImageGrabbed1;
                camera.StreamGrabber.ImageGrabbed += OnImageGrabbed1;

                h = camera.Parameters[PLCamera.Height].GetValue();
                Console.WriteLine($"Height after change:{h}");

                // Start streaming if not already grabbing
                if (!camera.StreamGrabber.IsGrabbing)
                    //camera.StreamGrabber.Start();
                    camera.StreamGrabber.Start(GrabStrategy.OneByOne, GrabLoop.ProvidedByStreamGrabber);

                if (!camera1.IsOpen)
                    camera1.Open();
            }
            catch
            {
                int i = 1;

            }
        }

        // creates a new object for the selected camera device. After that, the connection to the selected camera device is opened.
        private void OpenLiveCamera(int nCam, ICameraInfo tag)
        {
            // Destroy the old camera object.
            if (nCam == 1 && camera1 != null) //if (nCam == 1 && camera1 != null || nCam == 2 && camera2 != null) 
                DestroyCamera(nCam);

            try
            {
                if (nCam == 1)
                {
                    // Create a new camera object.
                    camera1 = new Camera(tag);
                    camera1.CameraOpened += Configuration.AcquireContinuous;
                    // Register for the events of the image provider needed for proper operation.
                    camera1.ConnectionLost += OnConnectionLost1;
                    camera1.CameraOpened += OnCameraOpened1;
                    camera1.CameraClosed += OnCameraClosed1;
                    camera1.StreamGrabber.GrabStarted += OnGrabStarted1;
                    camera1.StreamGrabber.ImageGrabbed += OnImageGrabbed1;
                    camera1.StreamGrabber.GrabStopped += OnGrabStopped1;

                    DeviceAccessibilityInfo di = CameraFinder.GetDeviceAccessibilityInfo(camera1.CameraInfo);

                    while (di == DeviceAccessibilityInfo.NotReachable)
                    {
                        Thread.Sleep(100);
                        di = CameraFinder.GetDeviceAccessibilityInfo(camera1.CameraInfo);
                    }

                    if (di == DeviceAccessibilityInfo.Opened)
                    {
                        camera1.Close();

                        btnReconnectPort_Click(null, null);

                        if (!(camera1 == null))
                            di = CameraFinder.GetDeviceAccessibilityInfo(camera1.CameraInfo);
                    }

                    //if (di == DeviceAccessibilityInfo.Opened) { //new
                    //    DestroyCamera(nCam);

                    //    camera1 = new Camera();
                    //    camera1.CameraOpened += Configuration.AcquireContinuous;
                    //    // Register for the events of the image provider needed for proper operation.
                    //    camera1.ConnectionLost += OnConnectionLost1;
                    //    camera1.CameraOpened += OnCameraOpened1;
                    //    camera1.CameraClosed += OnCameraClosed1;
                    //    camera1.StreamGrabber.GrabStarted += OnGrabStarted1;
                    //    camera1.StreamGrabber.ImageGrabbed += OnImageGrabbed1;
                    //    camera1.StreamGrabber.GrabStopped += OnGrabStopped1;

                    //    camera1.Open(1000, TimeoutHandling.ThrowException);

                    //    di = CameraFinder.GetDeviceAccessibilityInfo(camera1.CameraInfo);
                    //}

                    while (di == DeviceAccessibilityInfo.NotReachable)
                    {
                        Thread.Sleep(100);
                        di = CameraFinder.GetDeviceAccessibilityInfo(camera1.CameraInfo);
                    }

                    if (di == DeviceAccessibilityInfo.Ok)
                    {
                        camera1.Open(); // Open the connection to the camera device.
                        trkExp1.Enabled = true;
                        // Set the parameter for the controls.
                        /*
                                                if (camera1.Parameters.Contains(PLCamera.ExposureTimeAbs))
                                                {
                                                    trkExp1.Value = Convert.ToInt32(camera1.Parameters[PLCamera.ExposureTimeAbs].ToString());
                                                    //exposureTimeSliderControl.Parameter = camera.Parameters[PLCamera.ExposureTimeAbs];
                                                }
                                                else
                                                {
                                                    trkExp1.Value = Convert.ToInt32(camera1.Parameters[PLCamera.ExposureTime].ToString());
                                                    //exposureTimeSliderControl.Parameter = camera.Parameters[PLCamera.ExposureTime];
                                                }
                        */
                        toolStripButtonContinuousShot_Click(toolStrip1ButtonContinuousShot, null);


                        if ((bool)inv.get(chkLightSource, "Checked") == true)
                        {
                            Version sfnc2_0_0 = new Version(2, 0, 0);

                            if (camera1.GetSfncVersion() < sfnc2_0_0) // Handling for older cameras
                            {
                                var rc = camera1.Parameters[PLCamera.LightSourceSelector].TrySetValue(PLCamera.LightSourceSelector.Tungsten); //.Off);
                            }
                            else // Handling for newer cameras (using SFNC 2.0, e.g. USB3 Vision cameras)
                            {
                                var rc = camera1.Parameters[PLCamera.LightSourcePreset].TrySetValue(PLCamera.LightSourcePreset.Tungsten2800K); // Daylight5000K);
                            }
                        }
                        else if ((bool)inv.get(chkLightSourceOff, "Checked") == true)
                        {
                            var rc = camera1.Parameters[PLCamera.LightSourceSelector].TrySetValue(PLCamera.LightSourceSelector.Off);
                        }
                    }
                    else
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
                //
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
            catch (Exception exception)
            {
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
            if (InvokeRequired)
            {
                // If called from a different thread, we must use the Invoke method to marshal the call to the proper thread.
                BeginInvoke(new EventHandler<EventArgs>(OnConnectionLost1), sender, e);
                return;
            }

            DestroyCamera(1); // Close the camera object.

            UpdateDeviceList(1); // Because one device is gone, the list needs to be updated.
        }

        // Occurs when a device with an opened connection is removed.
        private void OnConnectionLost2(Object sender, EventArgs e)
        {
            if (InvokeRequired)
            {
                // If called from a different thread, we must use the Invoke method to marshal the call to the proper thread.
                BeginInvoke(new EventHandler<EventArgs>(OnConnectionLost2), sender, e);
                return;
            }

            DestroyCamera(2); // Close the camera object.

            UpdateDeviceList(2); // Because one device is gone, the list needs to be updated.
        }

        private void btnCloseLive_Click(object sender, EventArgs e)
        {
            if (sender == btnCloseLive1)
                DestroyCamera(1);
            //else if (sender == btnCloseLive2)
            //    DestroyCamera(2);
        }

        public void trkExp_Scroll(object sender, EventArgs e)
        {
            if (sender == trkExp1 && camera1 != null)
            {
                int val = 5000;
                this.Invoke((MethodInvoker)delegate
                {
                    val = trkExp1.Value;
                    toolTip1.SetToolTip(trkExp1, val.ToString());
                });
                //toolTip1.SetToolTip(trkExp1, trkExp1.Value.ToString());
                /*
                                if (camera1.Parameters.Contains(PLCamera.ExposureTimeAbs))
                                {
                                    try
                                    {
                                        camera1.Parameters[PLCamera.ExposureTimeAbs].TrySetValue(val, FloatValueCorrection.ClipToRange);
                                        Thread.Sleep(5);
                                    }
                                    catch (Exception ex)
                                    {
                                        MessageBox.Show("Set exposition error: " + ex.Message, "trkExp_Scrol", MessageBoxButtons.OK, MessageBoxIcon.Error, MessageBoxDefaultButton.Button1);
                                    }
                                    //exposureTimeSliderControl.Parameter = camera.Parameters[PLCamera.ExposureTimeAbs];
                                }
                                else
                                {
                                    try
                                    {
                                        camera1.Parameters[PLCamera.ExposureTime].TrySetValue(val, FloatValueCorrection.ClipToRange);
                                        Thread.Sleep(5);
                                    }
                                    catch (Exception ex)
                                    {
                                        MessageBox.Show("Set exposition error: " + ex.Message, "trkExp_Scrol", MessageBoxButtons.OK, MessageBoxIcon.Error, MessageBoxDefaultButton.Button1);
                                    }
                                    //exposureTimeSliderControl.Parameter = camera.Parameters[PLCamera.ExposureTime];
                                }
                */
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
            //    } catch {
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

        // Occurs when the connection to a camera device is opened.
        private void OnCameraOpened1(Object sender, EventArgs e)
        {
            if (InvokeRequired)
            {
                // If called from a different thread, we must use the Invoke method to marshal the call to the proper thread.
                BeginInvoke(new EventHandler<EventArgs>(OnCameraOpened1), sender, e);
                return;
            }
            // The image provider is ready to grab. Enable the grab buttons.
            //EnableButtons(true, false);
        }

        private void OnCameraOpened2(Object sender, EventArgs e)
        {
            if (InvokeRequired)
            {
                // If called from a different thread, we must use the Invoke method to marshal the call to the proper thread.
                BeginInvoke(new EventHandler<EventArgs>(OnCameraOpened2), sender, e);
                return;
            }
            // The image provider is ready to grab. Enable the grab buttons.
            //EnableButtons(true, false);
        }

        // Occurs when the connection to a camera device is closed.
        private void OnCameraClosed1(Object sender, EventArgs e)
        {
            if (InvokeRequired)
            {
                // If called from a different thread, we must use the Invoke method to marshal the call to the proper thread.
                BeginInvoke(new EventHandler<EventArgs>(OnCameraClosed1), sender, e);
                return;
            }
            // The camera connection is closed. Disable all buttons.
            //EnableButtons(false, false);
        }

        private void OnCameraClosed2(Object sender, EventArgs e)
        {
            if (InvokeRequired)
            {
                // If called from a different thread, we must use the Invoke method to marshal the call to the proper thread.
                BeginInvoke(new EventHandler<EventArgs>(OnCameraClosed2), sender, e);
                return;
            }
            // The camera connection is closed. Disable all buttons.
            //EnableButtons(false, false);
        }

        private void OnGrabStarted1(Object sender, EventArgs e)
        {
            if (InvokeRequired)
            {
                // If called from a different thread, we must use the Invoke method to marshal the call to the proper thread.
                BeginInvoke(new EventHandler<EventArgs>(OnGrabStarted1), sender, e);
                return;
            }

            // Reset the stopwatch used to reduce the amount of displayed images. The camera may acquire images faster than the images can be displayed.
            stopWatchLive1.Reset();

            // Do not update the device list while grabbing to reduce jitter. Jitter may occur because the GUI thread is blocked for a short time when enumerating.
            updateDeviceListTimer.Stop();

            // The camera is grabbing. Disable the grab buttons. Enable the stop button.
            //EnableButtons(false, true);
        }

        // Occurs when a camera starts grabbing.
        //private void OnGrabStarted2(Object sender, EventArgs e)
        //{
        //    if (InvokeRequired) {
        //        // If called from a different thread, we must use the Invoke method to marshal the call to the proper thread.
        //        BeginInvoke(new EventHandler<EventArgs>(OnGrabStarted2), sender, e);
        //        return;
        //    }
        //    // Reset the stopwatch used to reduce the amount of displayed images. The camera may acquire images faster than the images can be displayed.
        //    stopWatchLive2.Reset();
        //    // Do not update the device list while grabbing to reduce jitter. Jitter may occur because the GUI thread is blocked for a short time when enumerating.
        //    updateDeviceListTimer.Stop();
        //    // The camera is grabbing. Disable the grab buttons. Enable the stop button.
        //    //EnableButtons(false, true);
        //}

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
            //Thread.Sleep(50);
            if (InvokeRequired)
            {
                // If called from a different thread, we must use the Invoke method to marshal the call to the proper GUI thread.
                // The grab result will be disposed after the event call. Clone the event arguments for marshaling to the GUI thread.
                BeginInvoke(new EventHandler<ImageGrabbedEventArgs>(OnImageGrabbed1), sender, e.Clone());
                //bGrab = false;
                return;
            }

            try
            {
                // Acquire the image from the camera. Only show the latest image. The camera may acquire images faster than the images can be displayed.

                // Get the grab result.
                IGrabResult grabResult = e.GrabResult;

                // Check if the image can be displayed.
                //NPNP
                if (grabResult.IsValid && grabResult.GrabSucceeded)
                {
                    // Reduce the number of displayed images to a reasonable amount if the camera is acquiring images very fast.

                    //if (stopWatchLive1.IsRunning && stopWatchLive1.ElapsedMilliseconds < 100) 
                    //    return;

                    if (!stopWatchLive1.IsRunning || stopWatchLive1.ElapsedMilliseconds > 100) //33
                    {
                        stopWatchLive1.Restart();

                        if (tabControl1.SelectedTab != tabControl1.TabPages[0]) return;

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
                        //if (true)
                        //{
                            if (tabControl1.SelectedTab == tabControl1.TabPages[0]) { //if (tabControl1.SelectedIndex == 0) 
                            bitmapOld = pct1liveTab.Image as Bitmap;
                            pct1liveTab.Image = bitmap;

                            if (chkShowCross.Checked) // || chkUseSearchArea.Checked)
                            {
                                using (Graphics g = Graphics.FromImage(pct1liveTab.Image))
                                {
                                    g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                                    int PenSize = 4;

                                    if (chkShowCross.Checked)
                                    {
                                        System.Drawing.Point p1 = new System.Drawing.Point(0, Convert.ToInt16(bitmap.Height / 2));
                                        System.Drawing.Point p2 = new System.Drawing.Point(bitmap.Width, Convert.ToInt16(bitmap.Height / 2));
                                        g.DrawLine(new Pen(selectedClr, PenSize), p1, p2);
                                        p1 = new System.Drawing.Point(Convert.ToInt16(bitmap.Width / 2), 0);
                                        p2 = new System.Drawing.Point(Convert.ToInt16(bitmap.Width / 2), bitmap.Height);
                                        g.DrawLine(new Pen(selectedClr, PenSize), p1, p2);
                                    }

                                    int w = 0, h = 0;
                                    if (txtSearchAreaWidth.Text.Trim() != "") w = Convert.ToInt16(txtSearchAreaWidth.Text);
                                    if (txtSearchAreaHeight.Text.Trim() != "") h = Convert.ToInt16(txtSearchAreaHeight.Text);
                                    if (chkUseSearchArea.Checked && w > 0 && h > 0)
                                    {
                                        //float zf = 1;
                                        //double unscaledx = 0, unscaledy = 0;
                                        //zf = (float)ZoomFactorLive(0, 0, out unscaledx, out unscaledy);
                                        //g.DrawRectangle(new Pen(selectedClr, PenSize),
                                        //    zf * (pct1liveTab.Width - w) / 2, zf * (pct1liveTab.Height - h) / 2, w * zf, h * zf);

                                        System.Drawing.Point FirstPoint = new System.Drawing.Point(Convert.ToInt16(txtRectPointX.Text), Convert.ToInt16(txtRectPointY.Text));
                                        System.Drawing.Point SecondPoint = new System.Drawing.Point(Convert.ToInt16(txtRectPointX.Text) + Convert.ToInt16(txtSearchAreaWidth.Text),
                                                                    Convert.ToInt16(txtRectPointY.Text) + Convert.ToInt16(txtSearchAreaHeight.Text));
                                        g.FillEllipse(new SolidBrush(selectedClr), SecondPoint.X - PenSize / 2, SecondPoint.Y - PenSize / 2, PenSize, PenSize);
                                        g.DrawRectangle(new Pen(selectedClr, PenSize), FirstPoint.X, FirstPoint.Y, SecondPoint.X - FirstPoint.X, SecondPoint.Y - FirstPoint.Y);
                                    }
                                    //pct1liveTab.Refresh();
                                }
                            }
                        }

                        if (bitmapOld != null)
                        {
                            // Dispose the bitmap.
                            bitmapOld.Dispose();
                        }

                        bSnapResult = true;
                    }
                }
            }
            catch (Exception exception)
            {
                if (tabControl1.SelectedTab == tabControl1.TabPages[0])
                {
                    ShowException("OnImageGrabbed camera 1 ", exception);
                }
            }
            finally
            {
                // Dispose the grab result if needed for returning it to the grab loop.
                e.DisposeGrabResultIfClone();
            }
            //bGrab = false;
        }

        private void OnImageGrabbed2(Object sender, ImageGrabbedEventArgs e)
        {
            //if (InvokeRequired) {
            //    // If called from a different thread, we must use the Invoke method to marshal the call to the proper GUI thread.
            //    // The grab result will be disposed after the event call. Clone the event arguments for marshaling to the GUI thread.
            //    BeginInvoke(new EventHandler<ImageGrabbedEventArgs>(OnImageGrabbed2), sender, e.Clone());
            //    return;
            //}
            //
            //try {
            //    // Acquire the image from the camera. Only show the latest image. The camera may acquire images faster than the images can be displayed.
            //
            //    // Get the grab result.
            //    IGrabResult grabResult = e.GrabResult;
            //
            //    // Check if the image can be displayed.
            //    if (grabResult.IsValid) {
            //        // Reduce the number of displayed images to a reasonable amount if the camera is acquiring images very fast.
            //        if (!stopWatchLive2.IsRunning || stopWatchLive2.ElapsedMilliseconds > 100) //33
            //        {
            //            stopWatchLive2.Restart();
            //
            //            Bitmap bitmap = new Bitmap(grabResult.Width, grabResult.Height, PixelFormat.Format32bppRgb);
            //            // Lock the bits of the bitmap.
            //            BitmapData bmpData = bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height), ImageLockMode.ReadWrite, bitmap.PixelFormat);
            //            // Place the pointer to the buffer of the bitmap.
            //            converter.OutputPixelFormat = PixelType.BGRA8packed;
            //            IntPtr ptrBmp = bmpData.Scan0;
            //            converter.Convert(ptrBmp, bmpData.Stride * bitmap.Height, grabResult); //Exception handling TODO
            //            bitmap.UnlockBits(bmpData);
            //
            //            // Assign a temporary variable to dispose the bitmap after assigning the new bitmap to the display control.
            //            Bitmap bitmapOld = null; // = pct6liveTab.Image as Bitmap;
            //            //if (tabControl1.SelectedIndex == 0)
            //            //{
            //            //    bitmapOld = pct2live.Image as Bitmap;
            //            //    // Provide the display control with the new bitmap. This action automatically updates the display.
            //            //    pct2live.Image = bitmap;
            //            //}
            //            //else
            //            if (tabControl1.SelectedIndex == 2) {
            //                bitmapOld = pct2liveTab.Image as Bitmap;
            //                pct2liveTab.Image = bitmap;
            //            }
            //
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

        // Occurs when a camera has stopped grabbing.
        private void OnGrabStopped1(Object sender, GrabStopEventArgs e)
        {
            if (InvokeRequired)
            {
                // If called from a different thread, we must use the Invoke method to marshal the call to the proper thread.
                BeginInvoke(new EventHandler<GrabStopEventArgs>(OnGrabStopped1), sender, e);
                return;
            }

            stopWatchLive1.Reset(); // Reset the stopwatch.

            updateDeviceListTimer.Start(); // Re-enable the updating of the device list.

            // The camera stopped grabbing. Enable the grab buttons. Disable the stop button.
            //EnableButtons(true, false);

            // If the grabbed stop due to an error, display the error message.
            if (e.Reason != GrabStopReason.UserRequest)
            {
                MessageBox.Show("A grab error (camera 1) occured:\n" + e.ErrorMessage, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        //private void OnGrabStopped2(Object sender, GrabStopEventArgs e)
        //{
        //    if (InvokeRequired) {
        //        // If called from a different thread, we must use the Invoke method to marshal the call to the proper thread.
        //        BeginInvoke(new EventHandler<GrabStopEventArgs>(OnGrabStopped2), sender, e);
        //        return;
        //    }
        //    stopWatchLive2.Reset(); // Reset the stopwatch.
        //    updateDeviceListTimer.Start(); // Re-enable the updating of the device list.
        //    // The camera stopped grabbing. Enable the grab buttons. Disable the stop button.
        //    //EnableButtons(true, false);
        //    // If the grabbed stop due to an error, display the error message.
        //    if (e.Reason != GrabStopReason.UserRequest) {
        //        MessageBox.Show("A grab error (camera 2) occured:\n" + e.ErrorMessage, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        //    }
        //}
        #endregion --------- Basler live ---------


        private async void btnSnap_Click(object sender, EventArgs e)
        {
            try
            {
                btnSnap.Enabled = false;
                numBufferSize.Value = 2;

                Stopwatch stopwatch = new Stopwatch();
                stopwatch.Restart();
                btn_status(false);
                ButtonsEnabled(false);
                inv.set(btnStopCycle, "Enabled", true);
                RejectB = -1;
                RejectP = -1;
                bCycle = false;
                bWeldonCycle = false;
                frmMainInspect.StopCycle = true;
                Thread.Sleep(200);
                frmMainInspect.StopCycle = false;

                var task = Task.Run(() => CycleVision());
                Thread.Sleep(500);
                var task1 = Task.Run(() => CycleInspect());
                //var task3 = Task.Run(() => frmMainInspect.ImageCycle());//save snaps to images array

                await task;
                await task1;
                //inv.settxt(frmBeckhoff.mFormBeckhoffDefInstance.txtCycleTime, String.Format("0.0", stopwatch.ElapsedMilliseconds / 1000));
                inv.settxt(txtCycleTime, (stopwatch.ElapsedMilliseconds / 1000.0f).ToString("0.000"));
                

                stopwatch.Stop();
                //inv.settxt(frmBeckhoff.mFormBeckhoffDefInstance.txtCycleTime, String.Format("0.0", stopwatch.ElapsedMilliseconds / 1000));
                btn_status(true);
                ButtonsEnabled(true);
                btnSnap.Enabled = true;
            }
            catch (Exception ex) { btnSnap.Enabled = true; }

            return;


            for (int i = 0; i < frmMainInspect.SnapFile.Length; i++) frmMainInspect.SnapFile[i] = "";
            //for (int i = 0; i < frmMainInspect.Mstream.Length; i++) frmMainInspect.Mstream[i] = null;
            bool bExit = false;
            int err = 0;
            //Stopwatch stopwatch = new Stopwatch();
            stopwatch.Restart();
            while (!bExit)
            {
                var task = Task.Run(() => ProcDetect());
                await task;
                stopwatch.Stop();
                if (!task.Result.berr && chkSnapCont.Checked) {

                    bExit = true;
                    Thread.Sleep(2);
                    
                } else {
                    if (task.Result.berr) {
                        err++;
                        Thread.Sleep(200); //5
                        if (err > 2) {
                            MessageBox.Show(task.Result.sException, "Snap+Detect1", MessageBoxButtons.OK, MessageBoxIcon.Error, MessageBoxDefaultButton.Button1);
                            bExit = true;
                        }
                    }
                    else bExit = true;
                }
            }
            btnSnap.Enabled = true;
            return;


            int nCam = 0;
            if (sender == btnSnap)
                nCam = 1;
            //else if (sender == btnSnap2)
            //    nCam = 2;
            try
            {
               
            }
            catch (Exception exception)
            {
                string source = "";
                if (nCam == 1)
                    source = "Snap camera 1 ";
                //else if (nCam == 2)
                //    source = "Snap camera 2 ";
                ShowException(source, exception);
            }
        }

        Bitmap bmpSave;
        //public async Task<TasksParameters_Snap> Snap(bool bSnap = true, bool diam = false, bool save=false)
        public TasksParameters_Snap Snap(bool bSnap = true, bool diam = false, bool save = false)
        // Task<int> Snap(TasksParameters_Snap GetParamsSnap, bool bSnap = true)
        //public int Snap(out int xpar, out int ypar, out int widthpar, out int heightpar, out string sException)
        {
            TasksParameters_Snap GetParamsSnap = new TasksParameters_Snap();
            try
            {
                if ((bool)inv.get(chkUseSearchArea, "Checked") && IsNumeric(inv.gettxt(txtRectPointX)) && IsNumeric(inv.gettxt(txtRectPointY)) &&
                                                IsNumeric(inv.gettxt(txtSearchAreaWidth)) && IsNumeric(inv.gettxt(txtSearchAreaHeight)))
                {
                    GetParamsSnap.x = Convert.ToInt16(inv.gettxt(txtRectPointX)) + 4;
                    GetParamsSnap.y = Convert.ToInt16(inv.gettxt(txtRectPointY)) + 4;
                    GetParamsSnap.width = Convert.ToInt16(inv.gettxt(txtSearchAreaWidth)) - 8;
                    GetParamsSnap.height = Convert.ToInt16(inv.gettxt(txtSearchAreaHeight)) - 8;
                }
                else
                {
                    GetParamsSnap.x = 0;
                    GetParamsSnap.y = 0;
                    GetParamsSnap.width = 0; // pct1liveTab.Image.Width;
                    GetParamsSnap.height = 0; // pct1liveTab.Image.Height;
                }

                if (bSnap && camera1 != null && camera1.IsOpen && !(pct1liveTab.Image is null))
                {
                    tabControl1.Invoke((Action)(() =>
                    {
                        if (tabControl1.SelectedTab != tabControl1.TabPages[0])  tabControl1.SelectTab(0);
                    }));

                    bSnapResult = false;

                    bool bShowCross = Convert.ToBoolean(inv.get(chkShowCross, "Checked"));
                    inv.set(chkShowCross, "Checked", false);

                    //NPNP
                    //disabling the halving because it is done from another function ConfigureAOI
                    //long h = camera1.Parameters[PLCamera.Height].GetValue();
                    //if (h > 2000)
                    //{
                    //    camera1.StreamGrabber.Stop();
                    //    camera1.Parameters[PLCamera.Height].SetValue(1512);
                    //    camera1.StreamGrabber.Start(GrabStrategy.OneByOne, GrabLoop.ProvidedByStreamGrabber);

                    //    //camera1.CameraOpened += Configuration.AcquireContinuous;

                    //    //// Register for the events of the image provider needed for proper operation.
                    //    //camera1.ConnectionLost += OnConnectionLost1;
                    //    //camera1.CameraOpened += OnCameraOpened1;
                    //    //camera1.CameraClosed += OnCameraClosed1;

                    //    //camera1.StreamGrabber.GrabStarted += OnGrabStarted1;
                    //    //camera1.StreamGrabber.ImageGrabbed += OnImageGrabbed1;
                    //    //camera1.StreamGrabber.GrabStopped += OnGrabStopped1;

                    //    //if (!camera1.IsOpen) camera1.Open();

                    //    bSnapResult = false;
                    //    //Thread.Sleep(30);
                    //}

                    while (!bSnapResult)
                    {
                        //Application.DoEvents();
                        Thread.Sleep(2); //20
                    }

                    //camera1.StreamGrabber.Stop();
                    //camera1.Parameters[PLCamera.Height].SetValue(3040); // giMaximalCameraHeight);
                    //camera1.StreamGrabber.Start();

                    inv.set(chkShowCross, "Checked", bShowCross);

                    //camera1.WaitForFrameTriggerReady(100,TimeoutHandling.ThrowException);
                    //camera1.ExecuteSoftwareTrigger();
                    //while (!camera1.CanWaitForFrameTriggerReady);

                    if (true || (bool)inv.get(chkSaveFile, "Checked"))
                    {
                        try
                        {
                            // 1.Save all
                            int opt = 0;
                            pct1liveTab.Invoke((Action)(() =>
                            {
                                if (opt1.Checked) opt = 1;
                                if (opt2.Checked) opt = 2;
                                if (opt3.Checked) opt = 3;
                                if (opt4.Checked) opt = 4;
                                if (opt5.Checked) opt = 5;
                                if (opt6.Checked) opt = 6;
                                if (opt7.Checked) opt = 7;
                                if (opt8.Checked) opt = 8;
                                if (opt9.Checked) opt = 9;
                                if (opt10.Checked) opt = 10;
                                if (opt11.Checked) opt = 11;
                                if (opt12.Checked) opt = 12;
                                if (opt13.Checked) opt = 13;
                                if (opt14.Checked) opt = 14;
                                if (opt15.Checked) opt = 15;
                                if (opt16.Checked) opt = 16;

                                int nFrameMax = 0;
                                if (opt < 1) opt = 1;
                                numBufferSize.Invoke((Action)(() => { nFrameMax = (int)numBufferSize.Value; }));
                                //frmMainInspect.nFrameMax = nFrameMax;
                                numBufferSize.Invoke((Action)(() => { frmMainInspect.nFrameMax = nFrameMax; }));

                                //MemoryStream ms = new System.IO.MemoryStream();

                                if (GetParamsSnap.width == 0 || GetParamsSnap.height == 0)
                                {
                                    //frmMainInspect.AddList("Get Mstream " + (opt).ToString() + " //" + DateTime.Now.ToString("HH:mm:ss.fff"));
                                    Bitmap btm = (Bitmap)pct1liveTab.Image.Clone();
                                    
                                    frmMainInspect.StreamImage[opt - 1] = new System.IO.MemoryStream(); 
                                    btm.Save(frmMainInspect.StreamImage[opt - 1], ici, myEncoderParameters);
                                    if (opt > 0) frmMainInspect.SnapFile[opt - 1] = aPath + "\\Images\\snap" + opt.ToString() + ".jpg";
                                    else frmMainInspect.SnapFile[0] = aPath + "\\Images\\snap" + opt.ToString() + ".jpg";
                                }
                                else
                                {
                                    //frmMainInspect.AddList("Get Mstream " + (opt).ToString() + " //" + DateTime.Now.ToString("HH:mm:ss.fff"));
                                    bmpSave = new Bitmap(GetParamsSnap.width, GetParamsSnap.height);
                                    Graphics g = Graphics.FromImage(bmpSave);
                                    Rectangle section = new Rectangle(GetParamsSnap.x, GetParamsSnap.y, GetParamsSnap.width, GetParamsSnap.height);
                                    //g.DrawImage(pct1liveTab.Image, 0, 0, section, GraphicsUnit.Pixel);
                                    this.Invoke((Action)(() => { g.DrawImage(pct1liveTab.Image, 0, 0, section, GraphicsUnit.Pixel); }));

                                    frmMainInspect.StreamImage[opt - 1] = new System.IO.MemoryStream();
                                    bmpSave.Save(frmMainInspect.StreamImage[opt-1], ici, myEncoderParameters);
                                    if (opt > 0) frmMainInspect.SnapFile[opt - 1] = aPath + "\\Images\\snap" + opt.ToString() + ".jpg";
                                    else frmMainInspect.SnapFile[0] = aPath + "\\Images\\snap" + opt.ToString() + ".jpg";
                                    g.Dispose();
                                }

                                if (diam) opt = 0;
                                if (chkMemory.Checked) frmMainInspect.UseMemory = true; else frmMainInspect.UseMemory = false;
                                //if (!diam)
                                //{
                                //    //var taskImage = Task.Run(() => frmMainInspect.ImageFromFileTask1(opt - 1));
                                //    //await taskImage;
                                //    var task=Task.Run(() => frmMainInspect.ImageFromFileTask1(opt - 1));
                                //    await task;
                                //}
                                //png
                                //NPNP
                                //Save from the camera if:
                                //1. It's a full image (needed for color histogram check)
                                //2. The operator asked to save all the images (from cmbSaveResults)
                                //3. The operator asked to save all the images with defects  (from cmbSaveResults)
                                // pay attention that all the images are saved with a generic name snap1.jpg, snap2.jpg, etc.
                                // they are saved into the rejects folder with a name demarking the item, date and time AFTER they are inspected by congex
                                if (_bIsInFullImage || cmbSaveResults.SelectedItem.ToString()!= "Don't Save Results" || save)
                                {
                                    using (FileStream file = new FileStream(aPath + "\\Images\\snap" + opt.ToString() + ".jpg", FileMode.Create, FileAccess.Write, FileShare.Inheritable))
                                    {
                                        MemoryStream ms = new System.IO.MemoryStream();
                                        int ind = opt - 1;
                                        System.Drawing.Bitmap btm = (Bitmap)pct1liveTab.Image.Clone();
                                        btm.Save(ms, ici, myEncoderParameters); //jgpEncoder
                                        
                                        //StreamImage
                                        //btm.Save(ms,  System.Drawing.Imaging.ImageFormat.Png); //jgpEncoder
                                        byte[] bytes = ms.ToArray();
                                        if (chkMemory.Checked) frmMainInspect.UseMemory = true; else frmMainInspect.UseMemory = false;

                                        //NPNP
                                        file.Write(bytes, 0, bytes.Length);
                                        ms.Close();
                                        file.Close();
                                        ms.Dispose();
                                        file.Dispose();

                                        //    if (opt > 0) frmMainInspect.SnapFile[opt - 1] = aPath + "\\Images\\snap" + opt.ToString() + ".jpg";
                                        //    else frmMainInspect.SnapFile[0] = aPath + "\\Images\\snap" + opt.ToString() + ".jpg";
                                        //    //frmMainInspect.bmpCognex[opt - 1] = (Bitmap)pct1liveTab.Image.Clone();
                                    }
                                }
                            }));

                            //NPNP
                            //disabling the halving because it is done from another function ConfigureAOI
                            //camera1.StreamGrabber.Stop();
                            //camera1.Parameters[PLCamera.Height].SetValue(3040); // 3032);
                            //camera1.StreamGrabber.Start(GrabStrategy.OneByOne, GrabLoop.ProvidedByStreamGrabber);

                            GetParamsSnap.sException = "";
                            GetParamsSnap.rc = 0;
                            frmMainInspect.AddList("Snap Fini" + (opt).ToString() + " //" + DateTime.Now.ToString("HH:mm:ss.fff"));
                        }
                        catch (Exception exception)
                        {
                            //ShowException("Snap camera", exception);
                            GetParamsSnap.sException = exception.Message;
                            GetParamsSnap.rc = -2;
                        }
                    }
                    else
                    {
                        GetParamsSnap.sException = "";
                        GetParamsSnap.rc = 0;
                    }
                }
                else
                {
                    if (bSnap)
                    {
                        if (camera1 == null)
                            GetParamsSnap.sException = "Camera is null";
                        else if (!camera1.IsOpen)
                            GetParamsSnap.sException = "Camera closed";
                        else if (pct1liveTab.Image is null)
                            GetParamsSnap.sException = "Camera live image is null";
                        else
                            GetParamsSnap.sException = "Camera error";
                        GetParamsSnap.rc = -1;
                    }
                    else
                    {
                        GetParamsSnap.sException = "";
                        GetParamsSnap.rc = 0;
                    }
                }
                //if (!bCameraAOIDefined)
                //{
                //    //ConfigureAOI(camera1, 0, 0, giMaximalCameraWidth, giMaximalCameraHeight/2);
                //    ConfigureAOI(camera1, 0, 0, giMaximalCameraWidth, giMaximalCameraHeight);
                //    bCameraAOIDefined = true;
                //}
            }
            catch (Exception ex) {
                GetParamsSnap.sException = ex.Message;
                GetParamsSnap.rc = -2;
            }

            bCycle = false;
            bWeldonCycle = false;
            frmMainInspect.StopCycle = true;
            ButtonsEnabled(true);
            bSnapProcReady = true;
            Thread.Sleep(2);
            return GetParamsSnap;
        }

        public async Task<TasksParameters_GetLine> ProcDetect(bool bSnap = true, bool diam = false, bool save=false) //public bool GetLine(out bool berr, out double yc, out double Angle, bool bSnap = true)
        {
            TasksParameters_GetLine GetParams = new TasksParameters_GetLine();
            GetParams.berr = false;
            GetParams.yc = 0;
            GetParams.Angle = 0;
            GetParams.sException = "";

            try
            {
                Stopwatch stopwatch = new Stopwatch();
                stopwatch.Reset();
                stopwatch.Start();
                
                long t = stopwatch.ElapsedMilliseconds;

                int x = 0, y = 0, width = 0, height = 0;

                if (bSnap) {

                    var task = Task.Run(() => Snap(!bDebugMode, diam, save)); //(bSnap)
                    await task;

                    
                    if (task.Result.rc < 0) { // rc < 0
                        if (task.Result.rc == -1)
                            GetParams.sException = "Snap Error: " + task.Result.sException;
                        
                        else if (task.Result.rc == -2)
                            GetParams.sException = "Snap Exception: " + task.Result.sException;
                        
                        else
                            GetParams.sException = "Snap Error";
                        
                        GetParams.berr = true;
                        return GetParams;
                    }
                    x = task.Result.x; y = task.Result.y; width = task.Result.width; height = task.Result.height;
                }

                long t1 = stopwatch.ElapsedMilliseconds;
                inv.settxt(frmBeckhoff.mFormBeckhoffDefInstance.txtAcqTime, String.Format("{0:f0}", t1 - t));
                

                var task1 = Task.Run(() => ShowImage(x, y, width, height));
                await task1;
                

                if (task1.Result.berr) {
                    
                    GetParams.sException = task1.Result.sException;
                    GetParams.berr = true;
                    return GetParams;
                }


                long t2 = stopwatch.ElapsedMilliseconds;
                inv.settxt(frmBeckhoff.mFormBeckhoffDefInstance.txtInspectTime, String.Format("{0:f0}", t2 - t1));
                
                inv.settxt(frmBeckhoff.mFormBeckhoffDefInstance.txtElapsedTime, String.Format("{0:f0}", t2 - t));
                
                stopwatch.Stop();
                GetParams.sException = "";
                GetParams.berr = false;
            }
            catch (Exception e) {
                GetParams.sException = "Error: " + e.Message;
                
                GetParams.berr = true;
            }
            return GetParams; // !GetParams.berr;
        }
        public async Task<TasksParameters_GetLine> ProcDetectMov(bool bSnap = true, bool diam = false) //public bool GetLine(out bool berr, out double yc, out double Angle, bool bSnap = true)
        {
            TasksParameters_GetLine GetParams=new TasksParameters_GetLine();
            GetParams.berr = false;
            GetParams.yc = 0;
            GetParams.Angle = 0;
            GetParams.sException = "";

            try
            {
                Stopwatch stopwatch = new Stopwatch();
                stopwatch.Reset();
                stopwatch.Start();

                long t = stopwatch.ElapsedMilliseconds;

                int x = 0, y = 0, width = 0, height = 0;

                if (bSnap)
                {

                    var task = Task.Run(() => Snap(!bDebugMode, diam)); //(bSnap)
                    await task;


                    if (task.Result.rc < 0)
                    { // rc < 0
                        if (task.Result.rc == -1)
                            GetParams.sException = "Snap Error: " + task.Result.sException;

                        else if (task.Result.rc == -2)
                            GetParams.sException = "Snap Exception: " + task.Result.sException;

                        else
                            GetParams.sException = "Snap Error";

                        GetParams.berr = true;
                        return GetParams;
                    }
                    x = task.Result.x; y = task.Result.y; width = task.Result.width; height = task.Result.height;
                    GetParams.x = task.Result.x;
                    GetParams.y = task.Result.y;
                    GetParams.width = task.Result.width;
                    GetParams.height = task.Result.height;

                }

                long t1 = stopwatch.ElapsedMilliseconds;
                inv.settxt(frmBeckhoff.mFormBeckhoffDefInstance.txtAcqTime, String.Format("{0:f0}", t1 - t));


               
                long t2 = stopwatch.ElapsedMilliseconds;
                inv.settxt(frmBeckhoff.mFormBeckhoffDefInstance.txtInspectTime, String.Format("{0:f0}", t2 - t1));

                inv.settxt(frmBeckhoff.mFormBeckhoffDefInstance.txtElapsedTime, String.Format("{0:f0}", t2 - t));

                stopwatch.Stop();
                GetParams.sException = "";
                GetParams.berr = false;
            }
            catch (Exception e)
            {
                GetParams.sException = "Error: " + e.Message;

                GetParams.berr = true;
            }
            return GetParams; // !GetParams.berr;
        }
        
        public void SetCameraROI()
        {

        }

        public void ComposeException(Exception ex,[CallerMemberName] string sCallerName="")
        {
            MessageBox.Show($"Error in {sCallerName}: {ex.Message}");
        }

        public bool CheckIsFullImage(int iFrameNumber,int iFrameIndex)
        {
            try
            {
                _bIsInFullImage = false;
                //check if the current image should be snapped in fill size
                //if the frame index appears in _iSnapFullImage
                if (_iSnapFullImage.ContainsKey(iFrameNumber))
                {

                    int[] iFullImages = _iSnapFullImage[iFrameNumber];
                    for (int i = 0; i < iFullImages.Length; i++)
                    {
                        if (iFrameIndex == iFullImages[i])
                            _bIsInFullImage = true;
                    }
                }
                //if the frame index is 0 or in the middle of iFrameNumber
                else
                {
                    if (iFrameIndex == 0 || (iFrameIndex == iFrameNumber / 2))
                        _bIsInFullImage = true;
                }
                return _bIsInFullImage;
            }
            catch (Exception ex)
            {
                ComposeException(ex);
            }
            return false;
        }

        public async void SetCameraAOI(int iFrameIndex,int iFrameNumber)
        {
            //define an AOI according to the strategy

            try
            {
                long h = camera1.Parameters[PLCamera.Height].GetValue();
                Console.WriteLine($"Height just before snap: {h} image:{iFrameIndex}");

                CheckIsFullImage(iFrameNumber, iFrameIndex);


                //if this is the 1st time snapshot was taken
                if (_eCurrentSnapShotAOI == eCurrentSnapShotAOI.eCurrentSnapShotAOI_NOTSETYET)
                {
                    if (_eSnapShotStrategy == eSnapShotStrategy.eSnapShotStrategyOnlyFullImages)
                    {
                        ConfigureWholeImageCameraAOI(camera1);
                        _eCurrentSnapShotAOI = eCurrentSnapShotAOI.eCurrentSnapShotAOIFullImages;
                        await Task.Run(() => Thread.Sleep(20));
                    }
                    else if (_eSnapShotStrategy == eSnapShotStrategy.eSnapShotStrategyOnlyHalfImages)
                    {
                        ConfigureHalfImageCameraAOI(camera1);
                        _eCurrentSnapShotAOI = eCurrentSnapShotAOI.eCurrentSnapShotAOIHalfImages;
                        await Task.Run(() => Thread.Sleep(20));
                    }
                    else if (_eSnapShotStrategy == eSnapShotStrategy.eSnapShotStrategyOnlyGeographicROIBasedImages)
                    {
                        ConfigureRoiBasedCameraAOI(camera1);
                        _eCurrentSnapShotAOI = eCurrentSnapShotAOI.eCurrentSnapShotAOIGeographicROIBasedImages;
                        await Task.Run(() => Thread.Sleep(20));
                    }
                    else if (_eSnapShotStrategy == eSnapShotStrategy.eSnapShotStrategyFullImagesForColorHistogramHalfImagesForTheRest)
                    {
                        if (_bIsInFullImage)
                        {
                            ConfigureWholeImageCameraAOI(camera1);
                            _eCurrentSnapShotAOI = eCurrentSnapShotAOI.eCurrentSnapShotAOIFullImages;
                        }
                        else
                        {
                            ConfigureHalfImageCameraAOI(camera1);
                        }
                        await Task.Run(() => Thread.Sleep(20));
                    }
                    else if (_eSnapShotStrategy == eSnapShotStrategy.eSnapShotStrategyFullImagesForColorHistogramGeographicROIBasedImagesForTheRest)
                    {
                        if (_bIsInFullImage)
                        {
                            ConfigureWholeImageCameraAOI(camera1);
                            _eCurrentSnapShotAOI = eCurrentSnapShotAOI.eCurrentSnapShotAOIFullImages;
                        }
                        else
                        {
                            ConfigureRoiBasedCameraAOI(camera1);
                            _eCurrentSnapShotAOI = eCurrentSnapShotAOI.eCurrentSnapShotAOIGeographicROIBasedImages;
                        }
                    }
                    await Task.Run(() => Thread.Sleep(20));
                }
                //if the strategy is to only take the same kind of image (only full images, only half images, only roi based images)
                //then the AOI was already set at the if (_eCurrentSnapShotAOI == eCurrentSnapShotAOI.eCurrentSnapShotAOI_NOTSETYET)
                //and there is no need to change it
                else if (_eSnapShotStrategy == eSnapShotStrategy.eSnapShotStrategyOnlyFullImages ||
                    _eSnapShotStrategy == eSnapShotStrategy.eSnapShotStrategyOnlyGeographicROIBasedImages ||
                    _eSnapShotStrategy == eSnapShotStrategy.eSnapShotStrategyOnlyHalfImages)
                {
                    if (_eSnapShotStrategy == eSnapShotStrategy.eSnapShotStrategyOnlyFullImages &&
                        _eCurrentSnapShotAOI != eCurrentSnapShotAOI.eCurrentSnapShotAOIFullImages)
                    {
                        ConfigureWholeImageCameraAOI(camera1);
                        await Task.Run(() => Thread.Sleep(20));
                    }
                    else if (_eSnapShotStrategy == eSnapShotStrategy.eSnapShotStrategyOnlyHalfImages &&
                        _eCurrentSnapShotAOI != eCurrentSnapShotAOI.eCurrentSnapShotAOIHalfImages)
                    {
                        ConfigureHalfImageCameraAOI(camera1);
                        await Task.Run(() => Thread.Sleep(20));
                    }
                    else if (_eSnapShotStrategy == eSnapShotStrategy.eSnapShotStrategyOnlyGeographicROIBasedImages &&
                        _eCurrentSnapShotAOI != eCurrentSnapShotAOI.eCurrentSnapShotAOIGeographicROIBasedImages)
                    {
                        ConfigureRoiBasedCameraAOI(camera1);
                        await Task.Run(() => Thread.Sleep(20));
                    }
                }
                else if (_eSnapShotStrategy == eSnapShotStrategy.eSnapShotStrategyFullImagesForColorHistogramGeographicROIBasedImagesForTheRest ||
                    _eSnapShotStrategy == eSnapShotStrategy.eSnapShotStrategyFullImagesForColorHistogramHalfImagesForTheRest)
                {
                    //if the CURRENT image needs to be taken as whole image
                    if (_bIsInFullImage)
                    {
                        ConfigureWholeImageCameraAOI(camera1);
                        _eCurrentSnapShotAOI = eCurrentSnapShotAOI.eCurrentSnapShotAOIFullImages;
                        await Task.Run(() => Thread.Sleep(20));
                    }
                    else
                    {
                        //if the current image needs AOI that is PART of the image
                        //change the AOI ONLY if it wasn't already set the take part of the image
                        if (_eSnapShotStrategy == eSnapShotStrategy.eSnapShotStrategyFullImagesForColorHistogramHalfImagesForTheRest &&
                            _eCurrentSnapShotAOI != eCurrentSnapShotAOI.eCurrentSnapShotAOIHalfImages)
                        {
                            ConfigureHalfImageCameraAOI(camera1);
                            _eCurrentSnapShotAOI = eCurrentSnapShotAOI.eCurrentSnapShotAOIHalfImages;
                            await Task.Run(() => Thread.Sleep(20));
                        }
                        else if (_eSnapShotStrategy == eSnapShotStrategy.eSnapShotStrategyFullImagesForColorHistogramGeographicROIBasedImagesForTheRest &&
                            _eCurrentSnapShotAOI != eCurrentSnapShotAOI.eCurrentSnapShotAOIGeographicROIBasedImages)
                        {
                            ConfigureRoiBasedCameraAOI(camera1);
                            _eCurrentSnapShotAOI = eCurrentSnapShotAOI.eCurrentSnapShotAOIGeographicROIBasedImages;
                            await Task.Run(() => Thread.Sleep(20));
                        }
                    }
                }
                else
                {
                    ComposeException(new Exception("No _eSnapShotStrategy was chosen"));
                }

                Console.WriteLine($"Height just after snap: {h} image:{iFrameIndex}");
            }
            catch (Exception ex)
            {
                ComposeException(ex);
            }
        }

        public async Task<TasksParameters_GetLine> ProcDetectMovSnapFullImage(int iFrameIndex, int iFrameNumber, bool bSnap = true, bool diam = false) //public bool GetLine(out bool berr, out double yc, out double Angle, bool bSnap = true)
        {
            TasksParameters_GetLine GetParams = new TasksParameters_GetLine();
            GetParams.berr = false;
            GetParams.yc = 0;
            GetParams.Angle = 0;
            GetParams.sException = "";

            try
            {
                Stopwatch stopwatch = new Stopwatch();
                stopwatch.Reset();
                stopwatch.Start();

                long t = stopwatch.ElapsedMilliseconds;

                int x = 0, y = 0, width = 0, height = 0;

                if (bSnap)
                {
                    SetCameraAOI(iFrameIndex,iFrameNumber);

                    bSnapProcReady = false;
                    bool bSaveImage = true;
                    var task = Task.Run(() => Snap(!bDebugMode, diam)); //(bSnap)
                    await task;

                    while (!bSnapProcReady) {
                        Thread.Sleep(2);
                    }
                    //Thread.Sleep(100);


                    if (task.Result.rc < 0)
                    { // rc < 0
                        if (task.Result.rc == -1)
                            GetParams.sException = "Snap Error: " + task.Result.sException;

                        else if (task.Result.rc == -2)
                            GetParams.sException = "Snap Exception: " + task.Result.sException;

                        else
                            GetParams.sException = "Snap Error";

                        GetParams.berr = true;
                        return GetParams;
                    }
                    x = task.Result.x; y = task.Result.y; width = task.Result.width; height = task.Result.height;
                    GetParams.x = task.Result.x;
                    GetParams.y = task.Result.y;
                    GetParams.width = task.Result.width;
                    GetParams.height = task.Result.height;

                }

                long t1 = stopwatch.ElapsedMilliseconds;
                inv.settxt(frmBeckhoff.mFormBeckhoffDefInstance.txtAcqTime, String.Format("{0:f0}", t1 - t));



                long t2 = stopwatch.ElapsedMilliseconds;
                inv.settxt(frmBeckhoff.mFormBeckhoffDefInstance.txtInspectTime, String.Format("{0:f0}", t2 - t1));

                inv.settxt(frmBeckhoff.mFormBeckhoffDefInstance.txtElapsedTime, String.Format("{0:f0}", t2 - t));

                stopwatch.Stop();
                GetParams.sException = "";
                GetParams.berr = false;
            }
            catch (Exception e)
            {
                GetParams.sException = "Error: " + e.Message;

                GetParams.berr = true;
            }
            return GetParams; // !GetParams.berr;
        }

        public async Task<TasksParameters_GetLine> ShowImage(int x, int y, int width, int height)
        //public int ShowImage(out bool berr, out double yc, out double Angle)
        {
            //Image img; //Bitmap img = new Bitmap(frmVision.mFormVisionDefInstance.aPath + "\\Images\\snap.jpg");
            //image_path = frmVision.mFormVisionDefInstance.aPath + "\\Images\\snap.jpg";
            //int rc = 0;
                TasksParameters_GetLine GetParams= new TasksParameters_GetLine();
                GetParams.berr = false;
                GetParams.yc = 0;
                GetParams.Angle = 0;
                GetParams.sException = "";
            
                string fname = "";
                int nFrameMax = 0;
            try
            {
                Thread.Sleep(100);
                numBufferSize.Invoke((Action)(() => { nFrameMax = (int)numBufferSize.Value; }));

                int nOpt = 0;
                if (!bWeldonCycle && !bDiamCycle)
                {
                    if ((bool)inv.get(opt1, "Checked")) { nOpt = 1; }
                    else if ((bool)inv.get(opt2, "Checked")) { nOpt = 2; }
                    else if ((bool)inv.get(opt3, "Checked")) { nOpt = 3; }
                    else if ((bool)inv.get(opt4, "Checked")) { nOpt = 4; }
                    else if ((bool)inv.get(opt5, "Checked")) { nOpt = 5; }
                    else if ((bool)inv.get(opt6, "Checked")) { nOpt = 6; }
                    else if ((bool)inv.get(opt7, "Checked")) { nOpt = 7; }
                    else if ((bool)inv.get(opt8, "Checked")) { nOpt = 8; }
                    else if ((bool)inv.get(opt9, "Checked")) { nOpt = 9; }
                    else if ((bool)inv.get(opt10, "Checked")) { nOpt = 10; }
                    else if ((bool)inv.get(opt11, "Checked")) { nOpt = 11; }
                    else if ((bool)inv.get(opt12, "Checked")) { nOpt = 12; }
                    else if ((bool)inv.get(opt13, "Checked")) { nOpt = 13; }
                    else if ((bool)inv.get(opt14, "Checked")) { nOpt = 14; }
                    else if ((bool)inv.get(opt15, "Checked")) { nOpt = 15; }
                    else if ((bool)inv.get(opt16, "Checked")) { nOpt = 16; }
                    else nOpt = 1;
                }

                //rc = HoughLineTransformProc(out berr, out yc, out Angle);
                var task = Task.Run(() => HoughLineTransformProc(GetParams));
                await task;

                if (!bDebugMode)
                {
                    if ((bool)inv.get(chkSaveFile, "Checked"))
                    {
                        fname = frmBeckhoff.mFormBeckhoffDefInstance.aPath + "\\Images\\";
                        if (File.Exists(fname + "snap.bmp"))
                            fname += "snap.bmp";
                        else if (File.Exists(fname + "snap.jpg"))
                            fname += "snap.jpg";
                        else
                        {
                            //MessageBox.Show("Snap file not exists", "ShowImage", MessageBoxButtons.OK, MessageBoxIcon.Error, MessageBoxDefaultButton.Button1);
                            GetParams.sException = "ShowImage: Snap file not exists !";
                            GetParams.berr = true;
                            return GetParams;
                        }
                        int nFrameMax1 = 0;
                        numBufferSize.Invoke((Action)(() => { nFrameMax1 = (int)numBufferSize.Value; }));
                        string fname1 = aPath + "\\Images\\snap" + nOpt.ToString() + ".jpg";
                        //using (var file = new FileStream(fname1, FileMode.Open, FileAccess.Read, FileShare.Inheritable)) {
                        //image = (Bitmap)Bitmap.FromStream(file);
                        //image = new Bitmap(pct1liveTab.Image);// bmpSave;

                        //image = (Bitmap)Bitmap.FromFile(image_path);
                        processed = new Bitmap(pct1liveTab.Image);// image;
                                                                  //img = Image.FromStream(file); //pctResult.Image=
                                                                  //pctResult.Size = pctResult.Image.Size;
                                                                  //file.Close();
                                                                  //        File.Delete(frmVision.mFormVisionDefInstance.aPath + "\\Images\\snap.jpg");
                                                                  //frmVision.mFormVisionDefInstance.panelFrm1.Width = image.Width + 4;
                                                                  //frmVision.mFormVisionDefInstance.panelFrm1.Height = image.Height + 4;
                                                                  //}
                    }
                    else
                    {
                        pct1liveTab.Invoke((Action)(() =>
                        {
                            if (width == 0 || height == 0)
                                processed = new Bitmap(pct1liveTab.Image);
                            else
                            {
                                Bitmap image = (Bitmap)pct1liveTab.Image; //source
                                Rectangle section = new Rectangle(x, y, width, height);
                                processed = new Bitmap(section.Width, section.Height, PixelFormat.Format32bppArgb);
                                using (Graphics g = Graphics.FromImage(processed))
                                {
                                    g.DrawImage(image, 0, 0, section, GraphicsUnit.Pixel);
                                }
                            }

                        }));
                    }
                    

                    if (nOpt != null && nOpt > 0) frmMainInspect.SnapFile[nOpt - 1] = aPath + "\\Images\\snap" + nOpt.ToString() + ".jpg";
                    else frmMainInspect.SnapFile[0] = aPath + "\\Images\\snap" + (1).ToString() + ".jpg";
                    

                    if (bWeldonCycle)
                    {
                        pctSource.Invoke((Action)(() => { pctSource.Image = processed; }));
                    }
                    else
                    {
                        switch (nOpt)
                        {
                            case 1: { pct1.Invoke((Action)(() => { pct1.Image = processed; })); break; } //inv.set(pct1, "Image", processed); break; }
                            case 2: { pct2.Invoke((Action)(() => { pct2.Image = processed; })); break; } //inv.set(pct2, "Image", processed); break; }
                            case 3: { pct3.Invoke((Action)(() => { pct3.Image = processed; })); break; } //inv.set(pct3, "Image", processed); break; }
                            case 4: { pct4.Invoke((Action)(() => { pct4.Image = processed; })); break; } //inv.set(pct4, "Image", processed); break; }
                            case 5: { pct5.Invoke((Action)(() => { pct5.Image = processed; })); break; } //inv.set(pct5, "Image", processed); break; }
                            case 6: { pct6.Invoke((Action)(() => { pct6.Image = processed; })); break; } //inv.set(pct6, "Image", processed); break; }
                            case 7: { pct7.Invoke((Action)(() => { pct7.Image = processed; })); break; } //inv.set(pct7, "Image", processed); break; }
                            case 8: { pct8.Invoke((Action)(() => { pct8.Image = processed; })); break; } //inv.set(pct8, "Image", processed); break; }
                            case 9: { pct9.Invoke((Action)(() => { pct9.Image = processed; })); break; } //inv.set(pct9, "Image", processed); break; }
                            case 10: { pct10.Invoke((Action)(() => { pct10.Image = processed; })); break; } //inv.set(pct10, "Image", processed); break; }
                            case 11: { pct11.Invoke((Action)(() => { pct11.Image = processed; })); break; } //inv.set(pct11, "Image", processed); break; }
                            case 12: { pct12.Invoke((Action)(() => { pct12.Image = processed; })); break; } //inv.set(pct12, "Image", processed); break; }
                            case 13: { pct13.Invoke((Action)(() => { pct13.Image = processed; })); break; } //inv.set(pct13, "Image", processed); break; }
                            case 14: { pct14.Invoke((Action)(() => { pct14.Image = processed; })); break; } //inv.set(pct14, "Image", processed); break; }
                            case 15: { pct15.Invoke((Action)(() => { pct15.Image = processed; })); break; } //inv.set(pct15, "Image", processed); break; }
                            case 16: { pct16.Invoke((Action)(() => { pct16.Image = processed; })); break; } //inv.set(pct16, "Image", processed); break; }
                        }
                    }

                    if (fname != "" && nOpt > 0)
                    {
                        string fnamecopy = fname.Substring(0, fname.Length - 4) + nOpt.ToString() + fname.Substring(fname.Length - 4);
                        

                    }
                }
                else
                {
                    // load "\\Images\\snap1.jpg"...
                    if ((!bWeldonCycle && nOpt > 0) || (bWeldonCycle && nWeldonCycleEmul > 0))
                    {

                        if (bWeldonCycle)
                            fname = frmBeckhoff.mFormBeckhoffDefInstance.aPath + "\\Images\\snap" + nWeldonCycleEmul.ToString() + ".jpg";
                        else
                            fname = frmBeckhoff.mFormBeckhoffDefInstance.aPath + "\\Images\\snap" + nOpt.ToString() + ".jpg";

                        if (!File.Exists(fname))
                        {
                            //MessageBox.Show("Snap file not exists", "ShowImage", MessageBoxButtons.OK, MessageBoxIcon.Error, MessageBoxDefaultButton.Button1);
                            GetParams.sException = "ShowImage: Snap file not exists !";
                            GetParams.berr = true;
                            return GetParams;
                        }
                        else
                        {
                            FileStream fs = new System.IO.FileStream(fname, FileMode.Open, FileAccess.Read);
                            Image pic = Image.FromStream(fs);

                            //frmMainInspect.StreamImage[nOpt] = new MemoryStream();
                            //fs.CopyTo(frmMainInspect.StreamImage[nOpt]);
                            //fs.Close();
                            if (bWeldonCycle)
                            {
                                this.Invoke(new Action(() => pctSource.Image = pic));
                            }
                            else
                            {
                                switch (nOpt)
                                {
                                    case 1: { this.Invoke(new Action(() => pct1.Image = pic)); this.Invoke(new Action(() => pct1.Refresh())); break; }
                                    case 2: { this.Invoke(new Action(() => pct2.Image = pic)); this.Invoke(new Action(() => pct2.Refresh())); break; }
                                    case 3: { this.Invoke(new Action(() => pct3.Image = pic)); this.Invoke(new Action(() => pct3.Refresh())); break; }
                                    case 4: { this.Invoke(new Action(() => pct4.Image = pic)); this.Invoke(new Action(() => pct4.Refresh())); break; }
                                    case 5: { this.Invoke(new Action(() => pct5.Image = pic)); this.Invoke(new Action(() => pct5.Refresh())); break; }
                                    case 6: { this.Invoke(new Action(() => pct6.Image = pic)); this.Invoke(new Action(() => pct6.Refresh())); break; }
                                    case 7: { this.Invoke(new Action(() => pct7.Image = pic)); this.Invoke(new Action(() => pct7.Refresh())); break; }
                                    case 8: { this.Invoke(new Action(() => pct8.Image = pic)); this.Invoke(new Action(() => pct8.Refresh())); break; }
                                    case 9: { this.Invoke(new Action(() => pct9.Image = pic)); this.Invoke(new Action(() => pct9.Refresh())); break; }
                                    case 10: { this.Invoke(new Action(() => pct10.Image = pic)); this.Invoke(new Action(() => pct10.Refresh())); break; }
                                    case 11: { this.Invoke(new Action(() => pct11.Image = pic)); this.Invoke(new Action(() => pct11.Refresh())); break; }
                                    case 12: { this.Invoke(new Action(() => pct12.Image = pic)); this.Invoke(new Action(() => pct12.Refresh())); break; }
                                    case 13: { this.Invoke(new Action(() => pct13.Image = pic)); this.Invoke(new Action(() => pct13.Refresh())); break; }
                                    case 14: { this.Invoke(new Action(() => pct14.Image = pic)); this.Invoke(new Action(() => pct13.Refresh())); break; }
                                    case 15: { this.Invoke(new Action(() => pct15.Image = pic)); this.Invoke(new Action(() => pct14.Refresh())); break; }
                                    case 16: { this.Invoke(new Action(() => pct16.Image = pic)); this.Invoke(new Action(() => pct15.Refresh())); break; }
                                }
                            }
                        }
                    }
                }
                return GetParams;
            }
            catch (Exception ex) 
            { GetParams.berr = true; GetParams.sException = ex.Message; return GetParams; }
        }

        public  int HoughLineTransformProc(TasksParameters_GetLine GetParams)
        //private int HoughLineTransformProc(out bool berr, out double yc, out double Angle)
        {
            GetParams.berr = false;
            GetParams.yc = 0;
            GetParams.Angle = 0;
            return 0;
        }

        private void btnReconnectPort_Click(object sender, EventArgs e)
        {
            string EthDns;
            string EthIP;
            string EthName;
            EthernetInf(txtConnectionName1.Text.Trim(), out EthIP, out EthDns, out EthName);
            if (EthIP != "" && EthName == txtConnectionName1.Text.Trim()) //"Camera")
            {
                btnReconnectPort.Enabled = false;

                string EthDns_temp = "";
                string EthIP_temp;
                int ip1, ip2, ip3, ip4, ip3temp;
                int i1, i2, i3;
                if (EthDns != "")
                {
                    i1 = EthDns.IndexOf(".");
                    ip1 = Convert.ToInt32(EthDns.Substring(0, i1));
                    i2 = EthDns.Substring(i1 + 1).IndexOf(".");
                    ip2 = Convert.ToInt32(EthDns.Substring(i1 + 1, i2));
                    i3 = EthDns.Substring(i1 + i2 + 2).IndexOf(".");
                    ip3 = Convert.ToInt32(EthDns.Substring(i1 + i2 + 2, i3));
                    ip4 = Convert.ToInt32(EthDns.Substring(i1 + i2 + i3 + 3));

                    ip3temp = ip3 + 1;
                    if (ip3temp > 255) ip3temp = 254;
                    EthDns_temp = ip1.ToString() + "." + ip2.ToString() + "." + ip3temp.ToString() + "." + ip4.ToString();
                }
                i1 = EthIP.IndexOf(".");
                ip1 = Convert.ToInt32(EthIP.Substring(0, i1));
                i2 = EthIP.Substring(i1 + 1).IndexOf(".");
                ip2 = Convert.ToInt32(EthIP.Substring(i1 + 1, i2));
                i3 = EthIP.Substring(i1 + i2 + 2).IndexOf(".");
                ip3 = Convert.ToInt32(EthIP.Substring(i1 + i2 + 2, i3));
                ip4 = Convert.ToInt32(EthIP.Substring(i1 + i2 + i3 + 3));

                ip3temp = ip3 + 1;
                if (ip3temp > 255) ip3temp = 254;
                EthIP_temp = ip1.ToString() + "." + ip2.ToString() + "." + ip3temp.ToString() + "." + ip4.ToString();

                SetIP("/c netsh interface ip set address \"" + EthName + "\" static " + EthIP_temp + " " + "255.255.255.0" + " " + EthDns_temp);
                Thread.Sleep(3000);
                Application.DoEvents();

                SetIP("/c netsh interface ip set address \"" + EthName + "\" static " + EthIP + " " + "255.255.255.0" + " " + EthDns +
                " & netsh interface ip set dns \"" + EthName + "\" static " + EthDns);
                //SetIP("/c netsh interface ip set address \"" + EthName + "\" static " + "169.254.217.1" + " " + "255.255.255.0" + " " + "169.254.217.1" +
                //" & netsh interface ip set dns \"" + EthName + "\" static " + "169.254.217.1");
                //SetIP("/c netsh interface ip set address \"" + EthName + "\" static " + "169.254.216.1" + " " + "255.255.255.0" + " " + "169.254.216.1" +
                //" & netsh interface ip set dns \"" + EthName + "\" static " + "169.254.216.1");
                btnReconnectPort.Enabled = true;
            }
        }

        public ImageCodecInfo GetEncoder(ImageFormat format)
        {
            ImageCodecInfo[] codecs = ImageCodecInfo.GetImageDecoders();
            foreach (ImageCodecInfo codec in codecs)
            {
                if (codec.FormatID == format.Guid)
                {
                    return codec;
                }
            }
            return null;
        }

        private void SetJpgQuality()
        {
            try
            {
                // Create an Encoder object based on the GUID for the Quality parameter category.
                System.Drawing.Imaging.Encoder myEncoder = System.Drawing.Imaging.Encoder.Quality;
                // Create an EncoderParameters object. An EncoderParameters object has an array of EncoderParameter objects. 
                // In this case, there is only one EncoderParameter object in the array.
                myEncoderParameters = new System.Drawing.Imaging.EncoderParameters(1);
                System.Drawing.Imaging.EncoderParameter myEncoderParameter = new System.Drawing.Imaging.EncoderParameter(myEncoder, 99L); // quality=95% (long !) 
                myEncoderParameters.Param[0] = myEncoderParameter;
                ici = GetEncoderInfo("image/jpeg");
            }
            catch (Exception ex) { }
        }

        private static System.Drawing.Imaging.ImageCodecInfo GetEncoderInfo(String mimeType)
        {
            
                var encoders = System.Drawing.Imaging.ImageCodecInfo.GetImageEncoders();
                return encoders.FirstOrDefault(t => t.MimeType == mimeType);
            
        }

        private void SetIP(string arg)  //To set IP with elevated cmd prompt
        {
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo("cmd.exe");
                psi.UseShellExecute = true;
                psi.WindowStyle = ProcessWindowStyle.Normal; //.Hidden;
                psi.Verb = "runas";
                psi.Arguments = arg;
                Process.Start(psi);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private void btnLoadSettings_Click(object sender, EventArgs e)
        {
            if (sender == btnLoadSettings1) LoadIni(1);
            //NPNP
            UpdateTopInpectionExposureTime(nExposureInspection);
        }

        private void btnSearchArea_Click(object sender, EventArgs e)
        {
            if (IsNumeric(txtRectPointX.Text) && IsNumeric(txtRectPointY.Text) && IsNumeric(txtSearchAreaWidth.Text) && IsNumeric(txtSearchAreaHeight.Text))
            {
                if (camera1 == null) return;
                bool bOpen = camera1.IsOpen;
                if (bOpen) btnCloseLive_Click(btnCloseLive1, null);

                System.Drawing.Point FirstPoint = new System.Drawing.Point(Convert.ToInt16(txtRectPointX.Text), Convert.ToInt16(txtRectPointY.Text));
                System.Drawing.Point SecondPoint = new System.Drawing.Point(Convert.ToInt16(txtRectPointX.Text) + Convert.ToInt16(txtSearchAreaWidth.Text),
                                            Convert.ToInt16(txtRectPointY.Text) + Convert.ToInt16(txtSearchAreaHeight.Text));
                if (pct1liveTab.Image == null)
                {
                    Bitmap bmp = new Bitmap(pct1liveTab.Width, pct1liveTab.Height);
                    pct1liveTab.Image = bmp; //assign the picturebox.Image property to the bitmap created
                }
                using (Graphics g = Graphics.FromImage(pct1liveTab.Image))
                {
                    g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                    int PenSize = 8;
                    g.FillEllipse(new SolidBrush(selectedClr), SecondPoint.X - PenSize / 2, SecondPoint.Y - PenSize / 2, PenSize, PenSize);
                    g.DrawRectangle(new Pen(selectedClr, PenSize), FirstPoint.X, FirstPoint.Y, SecondPoint.X - FirstPoint.X, SecondPoint.Y - FirstPoint.Y);
                    //g.DrawLine(new Pen(selectedClr, PenSize), lastPoint, temp);
                    pct1liveTab.Refresh();
                }

                if (bOpen)
                {
                    Thread.Sleep(3000);
                    btnOpenLive_Click(btnOpenLive1, null);
                }
            }
            else
                MessageBox.Show("Incorrect coordinate field", "Search Area", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        bool chkStretchImage2_unchecked = false;
        bool chkStretchImageFront_unchecked = false;
        private void chkStretchImage1_CheckedChanged(object sender, EventArgs e)
        {
            if (sender == chkStretchImage1) {
                if (chkStretchImage1.Checked) {
                    pct1liveTab.Dock = DockStyle.Fill;
                    pct1liveTab.Width = panel1liveTab.Width - 2; //930
                    pct1liveTab.Height = panel1liveTab.Height - 2; //698
                    pct1liveTab.SizeMode = PictureBoxSizeMode.StretchImage;

                    panROI.Enabled = true;
                } else {
                    pct1liveTab.Dock = DockStyle.None;
                    pct1liveTab.SizeMode = PictureBoxSizeMode.AutoSize;

                    panROI.Enabled = false;
                }
            } else if (sender == chkStretchImage2) {
                if (chkStretchImage2.Checked) {
                    //pctSnap.Dock = DockStyle.Fill;
                    //pctSnap.Width = panel2.Width - 2; //930
                    //pctSnap.Height = panel2.Height - 2; //698
                    //pctSnap.SizeMode = PictureBoxSizeMode.Zoom; // PictureBoxSizeMode.StretchImage;

                    pctSnap.SizeMode = PictureBoxSizeMode.StretchImage;//.StretchImage
                    pctSnap.Dock = DockStyle.Fill;
                    pctSnap.Width = panel1liveTab.Width - 2; //930
                    //pctSnap.Height = panel1liveTab.Height - 2; //698
                    pctSnap.Height = (int)(pctSnap.Width * 3648.0f / 5472.0f);
                    panel2.AutoScroll = true;

                } else {
                    chkStretchImage2_unchecked = true;
                    pctSnap.Dock = DockStyle.None;
                    pctSnap.SizeMode = PictureBoxSizeMode.AutoSize;
                    pctSnap.Width = 1042;
                    //pctSnap.Height = panel1liveTab.Height - 2; //698
                    pctSnap.Height = 605;
                    panel2.AutoScroll = true;
                    pctSnap.Height = pctSnapH;
                    pctSnap.Width = pctSnapW;
                    panel2.Height = panel2H;
                    panel2.Width = panel2W;


                }
            }
        }

        private void pct1liveTab_MouseDown(object sender, MouseEventArgs e)
        {
            if (optSA1_21.Enabled && optSA1_21.Checked)
            {
                if (camera1 == null) return;

                double zf = 1;
                double unscaledx = 0;
                double unscaledy = 0;
                zf = ZoomFactorLive(0, 0, out unscaledx, out unscaledy);
                bMousePressed = true;
                lastPoint = e.Location;
                lastPoint.X = Convert.ToInt16(lastPoint.X * zf + unscaledx);
                lastPoint.Y = Convert.ToInt16(lastPoint.Y * zf + unscaledy);
                txtRectPointX.Text = lastPoint.X.ToString();
                txtRectPointY.Text = lastPoint.Y.ToString();
            }
            else
                bMousePressed = false;
        }

        private void pct1liveTab_MouseMove(object sender, MouseEventArgs e)
        {
            try
            {
                if (bMousePressed && lastPoint != null)
                {
                    double zf = 1;
                    double unscaledx = 0;
                    double unscaledy = 0;
                    zf = ZoomFactorLive(0, 0, out unscaledx, out unscaledy);
                    System.Drawing.Point temp = e.Location;
                    temp.X = Convert.ToInt16(temp.X * zf + unscaledx);
                    temp.Y = Convert.ToInt16(temp.Y * zf + unscaledy);

                    // Symmetrical ROI - commented
                    //lastPoint.X = Convert.ToInt16(pct1liveTab.Width * zf + unscaledx * 2) - temp.X;
                    //lastPoint.Y = Convert.ToInt16(pct1liveTab.Height * zf + unscaledy * 2) - temp.Y;
                    txtRectPointX.Text = (Math.Min(lastPoint.X, temp.X)).ToString();
                    txtRectPointY.Text = (Math.Min(lastPoint.Y, temp.Y)).ToString();

                    txtSearchAreaWidth.Text = (Math.Abs(temp.X - lastPoint.X)).ToString();
                    txtSearchAreaHeight.Text = (Math.Abs(temp.Y - lastPoint.Y)).ToString();

                    if (pct1liveTab.Image == null)
                    {
                        Bitmap bmp = new Bitmap(pct1liveTab.Width, pct1liveTab.Height);
                        pct1liveTab.Image = bmp; //assign the picturebox.Image property to the bitmap created
                    }
                    using (Graphics g = Graphics.FromImage(pct1liveTab.Image))
                    {
                        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                        int PenSize = 4;
                        g.FillEllipse(new SolidBrush(selectedClr), Math.Min(lastPoint.X, temp.X) - PenSize / 2, Math.Min(lastPoint.Y, temp.Y) - PenSize / 2, PenSize, PenSize);
                        g.DrawRectangle(new Pen(selectedClr, PenSize), Math.Min(lastPoint.X, temp.X), Math.Min(lastPoint.Y, temp.Y),
                            Math.Abs(temp.X - lastPoint.X), Math.Abs(temp.Y - lastPoint.Y));
                        //g.DrawLine(new Pen(selectedClr, PenSize), lastPoint, temp);
                        pct1liveTab.Refresh();
                    }
                }
            }
            catch (Exception ex) { }
        }

        private void pct1liveTab_MouseUp(object sender, MouseEventArgs e)
        {
            if (bMousePressed && lastPoint != null)
            {
                double zf = 1;
                double unscaledx = 0;
                double unscaledy = 0;
                zf = ZoomFactorLive(0, 0, out unscaledx, out unscaledy);
                System.Drawing.Point temp = e.Location;
                temp.X = Convert.ToInt16(temp.X * zf + unscaledx);
                temp.Y = Convert.ToInt16(temp.Y * zf + unscaledy);

                // Symmetrical ROI - commented
                //lastPoint.X = Convert.ToInt16(pct1liveTab.Width * zf + unscaledx * 2) - temp.X;
                //lastPoint.Y = Convert.ToInt16(pct1liveTab.Height * zf + unscaledy * 2) - temp.Y;
                txtRectPointX.Text = (Math.Min(lastPoint.X, temp.X)).ToString();
                txtRectPointY.Text = (Math.Min(lastPoint.Y, temp.Y)).ToString();

                txtSearchAreaWidth.Text = (Math.Abs(temp.X - lastPoint.X)).ToString();
                txtSearchAreaHeight.Text = (Math.Abs(temp.Y - lastPoint.Y)).ToString();

                optSA1_20.Checked = true;
            }
            bMousePressed = false;
            lastPoint = System.Drawing.Point.Empty;
        }

        private double ZoomFactorLive(int x, int y, out double unscaledx, out double unscaledy)
        {
            if (pct1liveTab.Image == null) {
                unscaledx = 1;
                unscaledy = 1;
                return 1;
            }
            int w_i = pct1liveTab.Image.Width;
            int h_i = pct1liveTab.Image.Height;
            int w_c = pct1liveTab.Width;
            int h_c = pct1liveTab.Height;
            float imageRatio = w_i / (float)h_i; // image W:H ratio
            float containerRatio = w_c / (float)h_c; // container W:H ratio
            float scaleFactor = 0;

            if (imageRatio >= containerRatio) { // horizontal image
                scaleFactor = w_c / (float)w_i;
                float scaledHeight = h_i * scaleFactor;
                // calculate gap between top of container and top of image
                float filler = Math.Abs(h_c - scaledHeight) / 2;
                unscaledx = (int)(x / scaleFactor);
                unscaledy = (int)((y - filler) / scaleFactor);
            } else { // vertical image
                scaleFactor = h_c / (float)h_i;
                float scaledWidth = w_i * scaleFactor;
                float filler = Math.Abs(w_c - scaledWidth) / 2;
                unscaledx = (int)((x - filler) / scaleFactor);
                unscaledy = (int)(y / scaleFactor);
            }
            return 1 / scaleFactor;
        }
        int indexnum = 0;
        bool ImageUpdate = false;
        private void pct1_Click(object sender, EventArgs e)
        {
            try
            {

                tabControl1.SelectedTab = tabControl1.TabPages[1];
                //NPNP don't strech unless pressing the strech button
                pctSnap.SizeMode = PictureBoxSizeMode.StretchImage;
                //pctSnap.SizeMode = PictureBoxSizeMode.Zoom;
                pctSnap.Width = panel1liveTab.Width - 2; //930
                //pctSnap.Height = panel1liveTab.Height - 2; //698
                pctSnap.Height = (int)(pctSnap.Width * 3648.0f / 5472.0f);
                //pctSnap.SizeMode = PictureBoxSizeMode.StretchImage;               
                chkStretchImage2.Checked = true;

                pctSnap.Dock = DockStyle.Fill;

                //pctSnap.SizeMode = PictureBoxSizeMode.StretchImage;
                pctSnap.Image = null;
                PictMode = 0;
                RejectString = "";
                int num = 0;
                if (sender == pct1)
                {
                    opt1.Checked = true;
                    if (pct1.Image is null) return;
                    pctSnap.Image = pct1.Image; num = 0;
                    frmBeckhoff.frmMainInspect.pictureBoxInspect.Image = pct1.Image;

                }
                if (sender == pct2)
                {
                    opt2.Checked = true;
                    if (pct2.Image is null) return;
                    pctSnap.Image = pct2.Image; num = 1;
                    frmBeckhoff.frmMainInspect.pictureBoxInspect.Image = pct2.Image;
                }
                if (sender == pct3)
                {
                    opt3.Checked = true;
                    if (pct3.Image is null) return;
                    pctSnap.Image = pct3.Image; num = 2;
                    frmBeckhoff.frmMainInspect.pictureBoxInspect.Image = pct3.Image;
                }
                if (sender == pct4)
                {
                    opt4.Checked = true;
                    if (pct4.Image is null) return;
                    pctSnap.Image = pct4.Image; num = 3;
                    frmBeckhoff.frmMainInspect.pictureBoxInspect.Image = pct4.Image;
                }
                if (sender == pct5)
                {
                    opt5.Checked = true;
                    if (pct5.Image is null) return;
                    pctSnap.Image = pct5.Image; num = 4;
                    frmBeckhoff.frmMainInspect.pictureBoxInspect.Image = pct5.Image;
                }
                if (sender == pct6)
                {
                    opt6.Checked = true;
                    if (pct6.Image is null) return;
                    pctSnap.Image = pct6.Image; num = 5;
                    //frmBeckhoff.frmFrontInspect.pictureBoxInspect.Image = pct6.Image;
                }
                if (sender == pct7)
                {
                    opt7.Checked = true;
                    if (pct7.Image is null) return;
                    pctSnap.Image = pct7.Image; num = 6;
                    frmBeckhoff.frmMainInspect.pictureBoxInspect.Image = pct7.Image;
                }
                if (sender == pct8)
                {
                    opt8.Checked = true;
                    if (pct8.Image is null) return;
                    pctSnap.Image = pct8.Image; num = 7;
                    frmBeckhoff.frmMainInspect.pictureBoxInspect.Image = pct8.Image;
                }
                if (sender == pct9)
                {
                    opt9.Checked = true;
                    if (pct9.Image is null) return;
                    pctSnap.Image = pct9.Image; num = 8;
                    frmBeckhoff.frmMainInspect.pictureBoxInspect.Image = pct9.Image;
                }
                if (sender == pct10)
                {
                    opt10.Checked = true;
                    if (pct10.Image is null) return;
                    pctSnap.Image = pct10.Image; num = 9;
                    frmBeckhoff.frmMainInspect.pictureBoxInspect.Image = pct10.Image;
                }
                if (sender == pct11)
                {
                    opt11.Checked = true;
                    if (pct11.Image is null) return;
                    pctSnap.Image = pct11.Image; num = 10;
                    frmBeckhoff.frmMainInspect.pictureBoxInspect.Image = pct11.Image;
                }
                if (sender == pct12)
                {
                    opt12.Checked = true;
                    if (pct12.Image is null) return;
                    pctSnap.Image = pct12.Image; num = 11;
                    frmBeckhoff.frmMainInspect.pictureBoxInspect.Image = pct12.Image;
                }
                if (sender == pct13)
                {
                    opt13.Checked = true;
                    if (pct13.Image is null) return;
                    pctSnap.Image = pct13.Image; num = 12;
                    frmBeckhoff.frmMainInspect.pictureBoxInspect.Image = pct13.Image;
                }
                if (sender == pct14)
                {
                    opt14.Checked = true;
                    if (pct14.Image is null) return;
                    pctSnap.Image = pct14.Image; num = 13;
                    frmBeckhoff.frmMainInspect.pictureBoxInspect.Image = pct14.Image;
                }
                if (sender == pct15)
                {
                    opt15.Checked = true;
                    if (pct15.Image is null) return;
                    pctSnap.Image = pct15.Image; num = 14;
                    frmBeckhoff.frmMainInspect.pictureBoxInspect.Image = pct15.Image;
                }
                if (sender == pct16)
                {
                    opt16.Checked = true;
                    if (pct16.Image is null) return;
                    
                    pctSnap.Image = pct16.Image; num = 15;
                    frmBeckhoff.frmMainInspect.pictureBoxInspect.Image = pct16.Image;

                }
                //rejected
                indexnum = num;
                ImageUpdate = true;
                frmBeckhoff.frmMainInspect.pictureBoxInspect.Refresh();
                pctSnap.Refresh();
            }
            catch (System.Exception ex) { }
        }

        private async void btnSaveImage_Click(object sender, EventArgs e)
        {
            var task = Task.Run(() => SaveSnapFile());
            await task;

            if (task.Result != "")
            {
                MessageBox.Show(task.Result, "Save Image", MessageBoxButtons.OK, MessageBoxIcon.Error, MessageBoxDefaultButton.Button1);
            }
        }

        private  string SaveSnapFile()
        {
            string sErMes = "";
            try
            {
                int nOpt = 0;
                MemoryStream ms = new System.IO.MemoryStream();

                if (opt1.Checked) {
                    pct1.Invoke((Action)(() =>
                    {
                        if (pct1.Image != null)
                        {
                            nOpt = 1;
                            pct1.Image.Save(ms, ici, myEncoderParameters); //jgpEncoder
                        }
                    }));
                } else if (opt2.Checked) {
                    pct2.Invoke((Action)(() =>
                    {
                        if (pct2.Image != null)
                        {
                            nOpt = 2;
                            pct2.Image.Save(ms, ici, myEncoderParameters); //jgpEncoder
                        }
                    }));
                } else if (opt3.Checked) {
                    pct3.Invoke((Action)(() =>
                    {
                        if (pct3.Image != null)
                        {
                            nOpt = 3;
                            pct3.Image.Save(ms, ici, myEncoderParameters); //jgpEncoder
                        }
                    }));
                } else if (opt4.Checked) {
                    pct4.Invoke((Action)(() =>
                    {
                        if (pct4.Image != null)
                        {
                            nOpt = 4;
                            pct4.Image.Save(ms, ici, myEncoderParameters); //jgpEncoder
                        }
                    }));
                } else if (opt5.Checked) {
                    pct5.Invoke((Action)(() =>
                    {
                        if (pct5.Image != null) {
                            nOpt = 5;
                            pct5.Image.Save(ms, ici, myEncoderParameters); //jgpEncoder
                        }
                    }));
                } else if (opt6.Checked) {
                    pct6.Invoke((Action)(() =>
                    {
                        if (pct6.Image != null)
                        {
                            nOpt = 6;
                            pct6.Image.Save(ms, ici, myEncoderParameters); //jgpEncoder
                        }
                    }));
                } else if (opt7.Checked) {
                    pct7.Invoke((Action)(() =>
                    {
                        if (pct7.Image != null)
                        {
                            nOpt = 7;
                            pct7.Image.Save(ms, ici, myEncoderParameters); //jgpEncoder
                        }
                    }));
                } else if (opt8.Checked) {
                    pct8.Invoke((Action)(() =>
                    {
                        if (pct8.Image != null)
                        {
                            nOpt = 8;
                            pct8.Image.Save(ms, ici, myEncoderParameters); //jgpEncoder
                        }
                    }));
                } else if (opt9.Checked) {
                    pct9.Invoke((Action)(() =>
                    {
                        if (pct9.Image != null)
                        {
                            nOpt = 9;
                            pct9.Image.Save(ms, ici, myEncoderParameters); //jgpEncoder
                        }
                    }));
                } else if (opt10.Checked) {
                    pct10.Invoke((Action)(() =>
                    {
                        if (pct10.Image != null)
                        {
                            nOpt = 10;
                            pct10.Image.Save(ms, ici, myEncoderParameters); //jgpEncoder
                        }
                    }));
                } else if (opt11.Checked) {
                    pct11.Invoke((Action)(() =>
                    {
                        if (pct11.Image != null)
                        {
                            nOpt = 11;
                            pct11.Image.Save(ms, ici, myEncoderParameters); //jgpEncoder
                        }
                    }));
                } else if (opt12.Checked) {
                    pct12.Invoke((Action)(() =>
                    {
                        if (pct12.Image != null)
                        {
                            nOpt = 12;
                            pct12.Image.Save(ms, ici, myEncoderParameters); //jgpEncoder
                        }
                    }));
                } else if (opt13.Checked) {
                    pct13.Invoke((Action)(() =>
                    {
                        if (pct13.Image != null)
                        {
                            nOpt = 13;
                            pct13.Image.Save(ms, ici, myEncoderParameters); //jgpEncoder
                        }
                    }));
                } else if (opt14.Checked) {
                    pct14.Invoke((Action)(() =>
                    {
                        if (pct14.Image != null)
                        {
                            nOpt = 14;
                            pct14.Image.Save(ms, ici, myEncoderParameters); //jgpEncoder
                        }
                    }));
                } else if (opt15.Checked) {
                    pct15.Invoke((Action)(() =>
                    {
                        if (pct15.Image != null)
                        {
                            nOpt = 15;
                            pct15.Image.Save(ms, ici, myEncoderParameters); //jgpEncoder
                        }
                    }));
                } else if (opt16.Checked) {
                    pct16.Invoke((Action)(() =>
                    {
                        if (pct16.Image != null)
                        {
                            nOpt = 16;
                            pct16.Image.Save(ms, ici, myEncoderParameters); //jgpEncoder
                        }
                    }));
                }

                if (nOpt > 0) {
                    using (FileStream file = new FileStream(aPath + "\\Images\\snap" + nOpt.ToString() + ".jpg", FileMode.Create, FileAccess.Write, FileShare.Inheritable))
                    {
                        byte[] bytes = ms.ToArray();
                        file.Write(bytes, 0, bytes.Length);
                        ms.Close();
                    }
                }
            }
            catch (Exception exception) {
                sErMes = exception.Message;
                //MessageBox.Show(exception.Message, "Save Image", MessageBoxButtons.OK, MessageBoxIcon.Error, MessageBoxDefaultButton.Button1);
            }
            return sErMes;
        }

        //private int GetTopInspectExposureTimeFromFrmInspect()
        //{
        //    try
        //    {
        //        int iTopInspectExposureTime;
        //        if (int.TryParse(frmMainInspect.txtNumberOfTopImages.Text, out iTopInspectExposureTime))
        //        {
        //            return iTopInspectExposureTime;
        //        }
        //        return -1;
        //    }
        //    catch (Exception ex) { return -1; }
        //}

        //private void SetTopInspectExposureTimeFromFrmInspect()
        //{
        //    try
        //    {
        //        int iTopInspectExposureTime = GetTopInspectExposureTimeFromFrmInspect();
        //        if (iTopInspectExposureTime != -1)
        //        {
        //            //TopInspectExposureTime.Value = iTopInspectExposureTime;
        //            inv.set(trkExp1, "Value", (System.Decimal)iTopInspectExposureTime);
        //        }
        //        else
        //        {
        //            MessageBox.Show("SetTopInspectExposureTimeFromFrmInspect failed to get or parse frmMainInspect.txtNumberOfTopImages.Text");
        //        }
        //    }
        //    catch (Exception ex) { }
        //}

        //private int GetTopColorExposureTimeFromFrmInspect()
        //{
        //    try
        //    {
        //        int iTopColorExposureTime;
        //        if (int.TryParse(frmMainInspect.txtNumberOfTopImages.Text, out iTopColorExposureTime))
        //        {
        //            return iTopColorExposureTime;
        //        }
        //        return -1;
        //    }
        //    catch (Exception ex) { return -1; }
        //}

        //private void SetTopColorExposureTimeFromFrmInspect()
        //{
        //    try
        //    {
        //        int iTopColorExposureTime = GetTopColorExposureTimeFromFrmInspect();
        //        if (iTopColorExposureTime != -1)
        //        {
        //            //TopColorExposureTime.Value = iTopColorExposureTime;
        //            inv.set(trkExp1, "Value", (System.Decimal)iTopColorExposureTime);
        //        }
        //        else
        //        {
        //            MessageBox.Show("SetTopColorExposureTimeFromFrmInspect failed to get or parse frmMainInspect.txtNumberOfTopImages.Text");
        //        }
        //    }
        //    catch (Exception ex) { }
        //}

        private int GetNumBufferSizeFromFrmInspect()
        {
            try
            {
                int inumBufferSize;
                if (int.TryParse(frmMainInspect.txtNumberOfTopImages.Text, out inumBufferSize))
                {
                    return inumBufferSize;
                }
                return -1;
            }
            catch (Exception ex) { return -1; }
        }

        private void SetNumBufferSizeFromFrmInspect()
        {
            try
            {
                int inumBufferSize = GetNumBufferSizeFromFrmInspect();
                if (inumBufferSize != -1)
                {
                    //numBufferSize.Value = inumBufferSize;
                    inv.set(numBufferSize, "Value", (System.Decimal)inumBufferSize);
                }
                else
                {
                    MessageBox.Show("SetNumBufferSizeFromFrmInspect failed to get or parse frmMainInspect.txtNumberOfTopImages.Text");
                }
            }
            catch(Exception ex){ }
        }
        public bool  CycleInit()
        {
            try
            {
               
                RejectB = -1;
                RejectP = -1;
                RejectBfront = -1;
                RejectPfront = -1;
                //this.Invoke((Action)(() => { frmMainInspect.listBox1.Items.Clear(); }));
                bCycle = false;
                bWeldonCycle = false;
                frmMainInspect.StopCycle = true;
                Thread.Sleep(200);
                frmMainInspect.StopCycle = false;
                
                inv.settxt(frmMainInspect.txtListBox1, "");
                frmMainInspect.lstStr = "";
                frmMainInspect.numBufferSize = (int)numBufferSize.Value;

                frmMainInspect.AddList("numBufferSize:" + numBufferSize.Value.ToString());
                frmMainInspect.AddList("Inspect Catalog:" + frmMainInspect.CmbCatNumText);
                frmMainInspect.AddList("PartNum: " + txtPartNum.Text);
                frmMainInspect.AddList("Order:" + txtOrder.Text);
                frmMainInspect.AddList("Item:" + txtItem.Text);
                frmMainInspect.AddList("Diameter:" + txtD.Text);
                frmMainInspect.AddList("Length:" + txtL.Text);
                frmMainInspect.AddList("LengthU: " + txtLU.Text);
                
                for (int i = 0; i < frmMainInspect.SnapFile.Length; i++) frmMainInspect.SnapFile[i] = "";
                for (int i = 0; i < frmMainInspect.SnapBitmap.Length; i++) frmMainInspect.SnapBitmap[i] = null;
                //frmMainInspect.bmpS = new BitmapSource[(int)numBufferSize.Value];
                frmMainInspect.imageCleRefresh();
                frmMainInspect.RegionFound1BrSave = new string[1];
                frmMainInspect.RegionFound1PlSave = new string[1];
                frmMainInspect.RegionFound2BrSave = new string[1];
                frmMainInspect.RegionFound2PlSave = new string[1];
                frmMainInspect.RegionFound3BrSave = new string[1];
                frmMainInspect.RegionFound3PlSave = new string[1];
                frmMainInspect.RegionFoundFrontBrSave = new string[1];


                frmMainInspect.RegIndex = 0;
                frmMainInspect.RegIndex2 = 0;
                frmMainInspect.RegIndexFront = 0;
                this.Invoke((Action)(() => { frmMainInspect.listBox11.Items.Clear(); }));
                numBufferSize.Invoke((Action)(() => { frmMainInspect.numBufferSize = (int)numBufferSize.Value; }));
                if (frmMainInspect.numBufferSize == 0) frmMainInspect.numBufferSize = 16;
                inCycleInspect = false;
                inCycleVision = false;
                inCycleInspectFront = false;
                frmMainInspect.imageCleRefresh();
                //delete images
                for (int i = 0; i < 16; i++)
                {
                    string fname = aPath + "\\Images\\snap" + (i + 1).ToString() + ".jpg";
                   if(File.Exists(fname)) File.Delete(fname);
                    
                }
                return true;
            }
            catch (Exception ex) { frmMainInspect.AddList("Error cycle init"+ex.Message + "  //" + DateTime.Now.ToString("HH:mm:ss.fff"));return false; }
        }
        private async void btnStartCycle_Click(object sender, EventArgs e)
        {
            try
            {
                Stopwatch stopwatch = new Stopwatch();
                stopwatch.Restart();

                SetNumBufferSizeFromFrmInspect();

                btn_status(false);
                inv.set(btnStopCycle, "Enabled", true);
                ButtonsEnabled(false);
                //panel1liveTab.Visible = true;
                inv.set(panel1liveTab, "Visible", true);
                bCycle = true;

                CycleInit();

                frmMainInspect.txtListBox1Disable = true;


                var task10 = Task.Run(() => frmMainInspect.ImageFromFileTask());
                //var task10 = Task.Run(() => frmMainInspect.ImageFromBmpsTask());
                var task1 = Task.Run(() => CycleInspect());
                var task = Task.Run(() => CycleVision());

                //Thread.Sleep(500);

                //var task3 = Task.Run(() => frmMainInspect.ImageCycle());//save snaps to images array
                //await Task.WhenAll(task,task1);
                //await task;
                await task1;
                //await task10;
                //inv.settxt(frmBeckhoff.mFormBeckhoffDefInstance.txtCycleTime, String.Format("0.0", stopwatch.ElapsedMilliseconds / 1000));
                inv.settxt(txtCycleTime, (stopwatch.ElapsedMilliseconds / 1000.0f).ToString("0.000"));
                //await inspect;
                //var task2 = Task.Run(() => WaitCycleInspectFini());
                //await task2;

                stopwatch.Stop();
                //inv.settxt(frmBeckhoff.mFormBeckhoffDefInstance.txtCycleTime, String.Format("0.0", stopwatch.ElapsedMilliseconds/1000));
                inv.settxt(frmMainInspect.txtListBox1, "");
                inv.settxt(frmMainInspect.txtListBox1, frmMainInspect.lstStr);
                bCycle = false;
                frmMainInspect.txtListBox1Disable = false;
                btn_status(true);
                ButtonsEnabled(true);
                panel1liveTab.Visible = true;
            }
            catch (Exception ex) {
                frmMainInspect.AddList("Error cycle " + ex.Message + "  //" + DateTime.Now.ToString("HH:mm:ss.fff"));
                panel1liveTab.Visible = true; }
        }

        // return "true" if normal end of all cycles
        // return "false" if error occured
        int FrameMax = 0;
        //Task taskInsp = null;
        public bool inCycleVision = false;
        public bool inCycleInspect = false;
        public bool inCycleInspectFront = false;
        public void ShowForms(bool bShow)
        {
            try
            {
                if (chkShowProgram.Checked) return;
                if (bShow)// || chkShowProgram.Checked)
                {
                    frmRun.frames = (int)numBufferSize.Value;
                    this.Invoke(new Action(() => frmRun.Hide()));
                    inv.set(this, "WindowState", FormWindowState.Normal);
                    inv.set(chkStretchImage1, "Checked", true);

                    //CreateParams cp = ba
                    
                    inv.set(panel1, "Visible", false);
                    inv.set(this, "Visible", true);
                   
                    Thread.Sleep(500);
                    
                    inv.set(panel1, "Visible", true);
                   

                }
                else if (!bShow)
                {
                   
                    this.Invoke(new Action(()=> AddWindow(true)));
                    frmRun.frames = (int)numBufferSize.Value;
                    System.Drawing.Point pt = new System.Drawing.Point((Screen.PrimaryScreen.WorkingArea.Width- frmRun.Width) / 2, (Screen.PrimaryScreen.WorkingArea.Height- frmRun.Height) / 2);
                    inv.set(frmRun, "Location", pt);
                    this.Invoke(new Action(() => frmRun.Show()));
                    inv.set(frmRun, "TopMost", true);
                    inv.set(chkStretchImage1, "Checked", false);
                    inv.set(this, "Visible", false);
                    
                }
            }
            catch (Exception ex) { }
        }
        public async Task<WebComm.CommReply> CycleVision()
        {

            //if (bCycle) return false;
            WebComm.CommReply rep = new WebComm.CommReply();
            rep.result = false;
            rep.comment = "";
            RejectB = -1;
            RejectP = -1;
            //frmMainInspect.imageCleRefresh();
            frmMainInspect.numBufferSize = (int)numBufferSize.Value;
            frmRun.bSwowProgram = false;
            _=Task.Run(()=>ShowForms(false));
            //set axis speed

            try
            {
                frmMainInspect.AddList("----------Start Vision Cycle" + " ------------- //" + DateTime.Now.ToString("HH:mm:ss.fff"));
                inCycleVision = true;
                inv.set(trackBarSpeedSt, "Value", trackBarSpeedSt.Maximum);
                inv.settxt(txtSpeedSt, trackBarSpeedSt.Maximum.ToString());
                lstStr = "";
                inv.settxt(txtMess, "---------------------");



                //run inspection

                inv.settxt(frmBeckhoff.mFormBeckhoffDefInstance.txtAcqTime, "");
                inv.settxt(frmBeckhoff.mFormBeckhoffDefInstance.txtInspectTime, "");
                inv.settxt(frmBeckhoff.mFormBeckhoffDefInstance.txtElapsedTime, "");
                inv.settxt(txtMess, "");
                //inv.set(trackBarSpeedSt, "Value", 90);

                Stopwatch stopwatch = new Stopwatch();
                stopwatch.Reset();
                stopwatch.Start();
                //inv.settxt(frmBeckhoff.mFormBeckhoffDefInstance.txtCycleTime, "");
                long t1 = stopwatch.ElapsedMilliseconds;

                ClearPictureArray();

                inv.settxt(lblCount, "0");

                

                int nFrameMax = PreparePictureArray();

                //bCycle = true;

                bWeldonCycle = false;
                nWeldonCycleEmul = 0;
                bool bMotionReady = false;
                btn_status(false);
                //var task10 = Task.Run(() =>frmMainInspect.ImageFromFileTask());
                string s = "";
                inv.settxt(frmRun.lblTime, "0.000");
                for (int nFrame = 1; nFrame <= nFrameMax; nFrame++)
                {
                    Thread.Sleep(1);
                    if (frmRun.bSwowProgram) { _ = Task.Run(() => ShowForms(true)); }
                    inv.settxt(frmRun.lblSnap, nFrame.ToString("00"));

                    //inv.set(pct1liveTab, "Visible", false);
                    //inv.set(chkStretchImage1, "Checked", false);

                    if (frmMainInspect.StopCycle) {frmMainInspect.AddList("Vision stop" + " //" + DateTime.Now.ToString("HH:mm:ss.fff"));  return rep; }
                    OptChecked(nFrame);

                    s = s + "<--" + "start rot" + nFrame.ToString() + " // " + DateTime.Now.ToString("HH:mm:ss.fff") + "\r\n";
                    //inv.settxt(txtMess, txtMess.Text+ "<--" +"start rot"+nFrame.ToString()+ " // " + DateTime.Now.ToString("HH:mm:ss.fff") + "\r\n");
                    if (bDebugMode)
                    {
                        long t2 = stopwatch.ElapsedMilliseconds;
                        while ((stopwatch.ElapsedMilliseconds - t2) < 100) // rotation emulation
                            Thread.Sleep(1);
                    }
                    else
                    {
                        if (!bMotionReady)
                        {
                            var taskM = Task.Run(() => MotionInCycle(nFrameMax, nFrame - 1));
                            await taskM;

                            if (!taskM.Result)
                            {
                                bCycle = false;
                                MessageBox.Show("ERROR MOVE", "MotionInCycle", MessageBoxButtons.OK, MessageBoxIcon.Error, MessageBoxDefaultButton.Button1);
                                rep.result = false;
                                _ = Task.Run(() => ShowForms(true));
                                return rep;
                            }
                            else
                            {

                            }
                        }
                        bMotionReady = false;
                    }
                    //inv.settxt(txtMess, txtMess.Text + "-->" + "fini rot" + nFrame.ToString() + " // " + DateTime.Now.ToString("HH:mm:ss.fff") + "\r\n");
                    s = s + "-->" + "fini rot" + nFrame.ToString() + " // " + DateTime.Now.ToString("HH:mm:ss.fff") + "\r\n";

                    if (!frmMainInspect.StopCycle)
                    {
                        //inv.settxt(txtMess, txtMess.Text + "<--" + "start vis" + nFrame.ToString() + " // " + DateTime.Now.ToString("HH:mm:ss.fff") + "\r\n");
                        s = s + "<--" + "start vis" + nFrame.ToString() + " // " + DateTime.Now.ToString("HH:mm:ss.fff") + "\r\n";
                        int err = 0;

                        inv.set(optSnap1, "Checked", true);

                        while (!frmMainInspect.StopCycle)
                        {
                            Thread.Sleep(1);
                            if (frmMainInspect.StopCycle)
                            {
                                rep.result = false;
                                frmMainInspect.AddList("Stop vision Cycle" + " //" + DateTime.Now.ToString("HH:mm:ss.fff"));
                                _ = Task.Run(() => ShowForms(true));
                                return rep;
                            }

                            //NPNP
                            bool bAllowHalfImages = true;
                            TasksParameters_GetLine GetParamsShow;

                            if (bAllowHalfImages)
                            {
                                var task = Task.Run(() => ProcDetectMovSnapFullImage(nFrame-1, nFrameMax, true));
                                await task;
                                GetParamsShow = task.Result;

                                //await Task.Run(()=>Thread.Sleep(20));
                            }
                            else
                            {
                                var task = Task.Run(() => ProcDetectMov());
                                await task;
                                GetParamsShow = task.Result;
                            }


                            //var task = Task.Run(() => ProcDetectMov());
                            //await task;
                            //TasksParameters_GetLine GetParamsShow = task.Result;
                            if (!GetParamsShow.berr)
                            {
                                inv.settxt(lblCount, nFrame.ToString());
                                //inv.settxt(txtCycleTime, String.Format("0.0", stopwatch.ElapsedMilliseconds / 1000.0));
                                inv.settxt(txtCycleTime, (stopwatch.ElapsedMilliseconds / 1000.0f).ToString("0.000"));
                                inv.settxt(frmRun.lblTime, txtCycleTime.Text);
                                //motion
                                int frame = nFrame;

                                
                                //inv.set(pct1liveTab, "Visible", false);
                                //inv.set(chkStretchImage1,"Checked", false);

                                var taskM1 = Task.Run(() => MotionInCycle(nFrameMax, frame));
                                var task1 = Task.Run(() => ShowImage(GetParamsShow.x, GetParamsShow.y, GetParamsShow.width, GetParamsShow.height));
                                
                                await Task.WhenAll(taskM1, task1);
                                //inv.set(pct1liveTab, "Visible", true);
                                if (taskM1.Result)
                                      bMotionReady = true;
                                else 
                                    bMotionReady = false;
                                
                                Thread.Sleep(1);
                                if (!task1.Result.berr)
                                {
                                    inv.settxt(lblCount, nFrame.ToString());
                                    //inv.settxt(txtCycleTime, String.Format("0.0", stopwatch.ElapsedMilliseconds / 1000.0));
                                    inv.settxt(txtCycleTime, (stopwatch.ElapsedMilliseconds / 1000.0f).ToString("0.000"));
                                    inv.settxt(frmRun.lblTime, txtCycleTime.Text);
                                    rep.result = true;
                                    break;

                                }
                                else
                                {
                                    err++;
                                    s = s + "-->" + "ShowImage+Detect3 Error" + nFrame.ToString() + " // " + DateTime.Now.ToString("HH:mm:ss.fff") + "\r\n";
                                    inv.settxt(txtMess, s);
                                    Thread.Sleep(500);
                                    if (err > 3)
                                    {
                                        bCycle = false;
                                        bWeldonCycle = false;
                                        frmMainInspect.StopCycle = true;
                                        MessageBox.Show(GetParamsShow.sException, "Snap+Detect4", MessageBoxButtons.OK, MessageBoxIcon.Error, MessageBoxDefaultButton.Button1);
                                        rep.result = false;
                                        _ = Task.Run(() => ShowForms(true));
                                        return rep;
                                    }
                                }
                               

                            }
                            else
                            {
                                err++;
                                s = s + "-->" + "Snap+Detect2 Error" + nFrame.ToString() + " // " + DateTime.Now.ToString("HH:mm:ss.fff") + "\r\n";
                                inv.settxt(txtMess, s);
                                Thread.Sleep(500);
                                if (err > 3)
                                {
                                    bCycle = false;
                                    bWeldonCycle = false;
                                    frmMainInspect.StopCycle = true;
                                    MessageBox.Show(GetParamsShow.sException, "Snap+Detect2", MessageBoxButtons.OK, MessageBoxIcon.Error, MessageBoxDefaultButton.Button1);
                                    rep.result = false;
                                    _ = Task.Run(() => ShowForms(true));
                                    return rep;
                                }
                            }



                            Thread.Sleep(1);
                            if (frmMainInspect.StopCycle) { frmMainInspect.AddList("Vision stop" + " //" + DateTime.Now.ToString("HH:mm:ss.fff")); ; return rep; }
                        }
                        //inv.settxt(txtMess, txtMess.Text + "-->" + "fini vis" + nFrame.ToString() + " // " + DateTime.Now.ToString("HH:mm:ss.fff") + "\r\n");
                        s = s + "-->" + "fini vis" + nFrame.ToString() + " // " + DateTime.Now.ToString("HH:mm:ss.fff") + "\r\n";
                        //inv.settxt(txtCycleTime,  (stopwatch.ElapsedMilliseconds / 1000.0f).ToString("0.00"));
                        inCycleVision = false;
                    }
                    Thread.Sleep(1);
                }
                //inv.set(pct1liveTab, "Visible", true);
                //inv.set(chkStretchImage1, "Checked", true);
                inv.settxt(frmRun.lblTime, txtCycleTime.Text);


                if (frmMainInspect.StopCycle)
                {
                    bCycle = false;
                    rep.result = false;
                    rep.status = "0,0";
                    _ = Task.Run(() => ShowForms(true));
                    if (frmMainInspect.StopCycle) { frmMainInspect.AddList("Vision stop" + " //" + DateTime.Now.ToString("HH:mm:ss.fff"));  return rep; }
                    return rep;
                }
                else { rep.result = true;
                    inv.settxt(frmRun.lblTime, txtCycleTime.Text);
                    Thread.Sleep(200);
                    _ = Task.Run(() => ShowForms(true));
                    return rep; }

                inv.settxt(txtMess, s);
                //inv.set(pct1liveTab, "Visible", true);

                _ = Task.Run(() => ShowForms(true));


            }
            catch (System.Exception ex) { inCycleVision = false; frmMainInspect.AddList("Error vision Cycle" +" //" + DateTime.Now.ToString("HH:mm:ss.fff"));  rep.result = false; inv.set(pct1liveTab, "Visible", true);
                ShowForms(true); return rep; }
        }
        int RejectB = -1;
        int RejectP = -1;
        public async Task <WebComm.CommReply> CycleInspect()
        {
            
            //if (bCycle) return false;
            WebComm.CommReply rep = new WebComm.CommReply();
            rep.result = false;
            rep.comment = "";
            bool reply = false;
            frmMainInspect.LoadedBackup = false;

            try
            {
                inCycleInspect = true;
                //this.Invoke(new Action(() => frmMainInspect.InspectionCycle()));
                
                var taskInsp = Task.Run(()=> frmMainInspect.InspectionCycle());
                await taskInsp;
                reply = taskInsp.Result;
                inCycleInspect = false;
                //inv.settxt(frmBeckhoff.mFormBeckhoffDefInstance.txtCycleTime, String.Format("{0:f0}", t3 - t1));
                //save file
                if (!reply)
                {
                    RejectB = -1000;
                    RejectP = -1000;
                    bCycle = false;
                    rep.result = reply;
                    rep.status = RejectB.ToString() + "," + RejectP.ToString();
                    return rep;
                }
                else
                {
                    RejectB = frmMainInspect.RegionFound1BrSave.Length - 1;
                    RejectP = frmMainInspect.RegionFound1PlSave.Length - 1;
                }
                using (FileStream file = new FileStream(aPath + "\\test BREAK" + ".txt", FileMode.Create, FileAccess.Write, FileShare.Inheritable))
                {
                    StreamWriter swr = new StreamWriter(file);
                    for (int i = 0; i < frmMainInspect.RegionFound1BrSave.Length; i++)
                    {
                        string ss = frmMainInspect.RegionFound1BrSave[i];
                        swr.WriteLine(ss);
                    }
                    swr.Close();


                }
                using (FileStream file = new FileStream(aPath + "\\test PEELS" + ".txt", FileMode.Create, FileAccess.Write, FileShare.Inheritable))
                {
                    StreamWriter swr = new StreamWriter(file);
                    for (int i = 0; i < frmMainInspect.RegionFound1PlSave.Length; i++)
                    {
                        string ss = frmMainInspect.RegionFound1PlSave[i];
                        swr.WriteLine(ss);
                    }
                    swr.Close();


                }
                //
                //btn_status(true);

                //ButtonsEnabled(true);
                //this.Invoke((Action)(() => { frmMainInspect.listBox1.Items.Clear(); }));
                //string[] str = frmMainInspect.lstStr.Split('\r');
                //for (int i=0; i<str.Length;i++) this.Invoke((Action)(() => { frmMainInspect.listBox1.Items.Add(str[i]); }));
                //NPNP
                //if (chkSaveResults.Checked)

                //the images are also saved in Snap();
                //but they are saved with a generic name snap1.jpg, snap2.jpg, etc.
                //here they are saved into the rejects folder with a name demarking the item, date and time AFTER they are inspected
                //by congex
                string sSelectedItemText = "";
                cmbSaveResults.Invoke(new Action(()=> sSelectedItemText = cmbSaveResults.SelectedItem.ToString()));
                if (sSelectedItemText != "Don't Save Results")
                {
                   
                    if (sSelectedItemText == "Save All Results" ||
                        (sSelectedItemText == "Save Defects Only" && frmMainInspect.bDefectFoundInTopInspection))
                    {
                        bool res = SaveRejectsTop();
                    }
                    //await task3;
                    
                }

                bCycle = false;
                rep.result = reply;
                rep.status = RejectB.ToString() + "," + RejectP.ToString();
                return rep;

            }

            catch (System.Exception ex) { inCycleInspect = false; frmMainInspect.AddList("inspect error" + " //" + DateTime.Now.ToString("HH:mm:ss.fff"));  rep.result = false; return rep; }
        }
        public  WebComm.CommReply WaitCycleInspectFini()
        {

            //if (bCycle) return false;
            WebComm.CommReply rep = new WebComm.CommReply();
            rep.result = false;
            rep.comment = "";
            bool reply = false;
            Stopwatch sw = new Stopwatch();
            sw.Restart();
            try
            {

                while (sw.ElapsedMilliseconds < 50000)
                {
                    if (RejectB >= 0 && RejectP >= 0)
                    {
                        bCycle = false;
                        rep.result = true;
                        rep.status = RejectB.ToString() + "," + RejectP.ToString();
                        return rep;
                    }
                    if (RejectB == -1000 && RejectP == -1000)
                    {
                        bCycle = false;
                        rep.result = false;
                        rep.status = RejectB.ToString() + "," + RejectP.ToString();
                        return rep;
                    }
                    Thread.Sleep(100);
                    if (frmMainInspect.StopCycle) break;
                }



                bCycle = false;
                rep.result = false;
                rep.status = RejectB.ToString() + "," + RejectP.ToString();
                return rep;

            }

            catch (System.Exception ex) { rep.result = false; return rep; }
        }

        private void ButtonsEnabled(bool bEn)
        {
            inv.set(btnSnap, "Enabled", bEn);
            inv.set(btnStartCycle, "Enabled", bEn);
            //inv.set(btnStopCycle, "Enabled", !bEn);
            inv.set(btnLoadImage, "Enabled", bEn);
            inv.set(btnDetectDiam, "Enabled", bEn);
            inv.set(btnDetectWeldon, "Enabled", bEn);
            inv.set(btnSnapDiam, "Enabled", bEn);
            inv.set(btnSnapWeldon, "Enabled", bEn);
            inv.set(btnStartCycleWeldon, "Enabled", bEn);
            inv.set(btnTestWeldon, "Enabled", bEn);
        }

        private async void btnSetStartPosition_Click(object sender, EventArgs e)
        {
            //current position
            try {
                btn_status(false);
                var task = Task.Run(() => CurrPos());
                await task;
                bool reply = task.Result;
                btn_status(true);
                if (!reply) {
                    MessageBox.Show("READ COORDINATES!", "SetStartPosition", MessageBoxButtons.OK, MessageBoxIcon.Error, MessageBoxDefaultButton.Button1);
                    return; }
            }
            catch (Exception ex) {
                MessageBox.Show(ex.Message, "SetStartPosition", MessageBoxButtons.OK, MessageBoxIcon.Error, MessageBoxDefaultButton.Button1);
                return;
            }
            inv.settxt(lblStartPosition, (inv.get(upDownControlPosSt, "UpDownValue")).ToString());
        }

        private void chkLightSource_CheckedChanged(object sender, EventArgs e)
        {
            if (chkLightSource.Checked) chkLightSourceOff.Checked = false;
        }

        private void chkLightSourceOff_CheckedChanged(object sender, EventArgs e)
        {
            if (chkLightSourceOff.Checked) chkLightSource.Checked = false;
        }

        private async void chkLight1_CheckedChanged(object sender, EventArgs e)
        {
            try
            {
                //int id = 0;
                //if (((CheckBox)sender).Name == "chkLight1") id = 1;
                //if (((CheckBox)sender).Name == "chkLight2") id = 2;
                //if (((CheckBox)sender).Name == "chkLight3") id = 3;
                CommReply reply = new CommReply();
                Beckhoff.SendPlcParms ParmsPlc = new Beckhoff.SendPlcParms();
                reply.result = false;
                Array.Resize<Single>(ref ParmsPlc.SendParm, 11);
                for (int i = 0; i < ParmsPlc.SendParm.Length; i++) ParmsPlc.SendParm[i] = 0;

                ParmsPlc.SendParm[0] = MyStatic.CamsCmd.Lamps;
                if (chkLight1.Checked) ParmsPlc.SendParm[2] = 1; else ParmsPlc.SendParm[2] = 2;
                if (chkLight2.Checked) ParmsPlc.SendParm[3] = 1; else ParmsPlc.SendParm[3] = 2;
                if (chkLight3.Checked) ParmsPlc.SendParm[4] = 1; else ParmsPlc.SendParm[4] = 2;
                ParmsPlc.SendParm[10] = 1f;//tmout


                var task1 = Task.Run(() => Beckhoff_Gen.PlcSendCmd(StartAddressSendGen, ParmsPlc));
                await task1;

                ParmsPlc.SendParm = null;
                reply = task1.Result;
            }
            catch (Exception ex) { }
        }
        bool bStop = false;
        private  void btnSatrt2_Click(object sender, EventArgs e)
        {
            try
            {

                bStop = false;
                inv.set(btnSatrt2, "Enabled", false);
                var task = Task.Run(() => RunWebComm());//vision
                //await task;
                var task1 = Task.Run(() => RunWebComm1());//cognex
                                                          //await task1;

                inv.set(btnSatrt2, "Enabled", true);


            }
            catch (System.Exception)
            {

                throw;
            }
        }
        private WebComm.CommReply SaveROI()
        {
            WebComm.CommReply acommReply = new WebComm.CommReply();
            acommReply.result = false;
            try
            {
                frmMainInspect.SaveROI(null,new EventArgs());
                frmMainInspect.SaveROI1(null, new EventArgs());
                acommReply.result = true;
                return acommReply;
            }
            catch
            {
                return acommReply;
            }
        }

        private async Task<bool> RunWebComm()//vision
        {
            try
            {

                bStop = false;
                //inv.set(btnSatrt2, "Enabled", false);
                WC1.SetControls1(txtServer, this, "Vision", "http://*:5000/");


                WebComm.HostReply reply1 = new WebComm.HostReply();
                if (!WC1.StartServer())
                {
                    inv.settxt(txtServer, txtServer.Text + "Http Server Start Error" + "\r\n");
                    return false;
                }
                string send = "";
                while (!bStop)
                {
                    Thread.Sleep(20);
                    var task = Task.Run(() => WC1.ReadHttp());
                    await task;
                    reply1 = task.Result;
                    if (!reply1.result) { bStop = true; return false; }
                    string[] s = reply1.comment.Split(',');
                    if (s != null && s.Length > 2)
                    {
                        switch (s[0])
                        {


                            case "cmd90"://data
                                if (s.Length >= 7)
                                {
                                    inv.settxt(txtOrder, s[1]);
                                    inv.settxt(txtItem, s[2]);
                                    inv.settxt(txtD, s[3]);
                                    inv.settxt(txtDiamNominal, s[3]);
                                    inv.settxt(txtL, s[4]);
                                    inv.settxt(txtLU, s[5]);
                                    inv.settxt(txtPartNum, s[6]);
                                    send = "cmd90,1,end";
                                }
                                else
                                {
                                    send = "cmd90,0,end";
                                }
                                break;
                            case "cmd91"://start diameter
                                var taskSD = Task.Run(() => TaskSnapDiam());
                                await taskSD;
                                WebComm.CommReply reply = new WebComm.CommReply();
                                reply = taskSD.Result;
                                if (reply.result)
                                {
                                    Single diam = taskSD.Result.data[0];
                                    Single RightEdge = taskSD.Result.data[1];
                                    send = "cmd91,1," + diam.ToString() + "," + RightEdge.ToString() + ",end";
                                }
                                else
                                {
                                    send = "cmd91,0," + "0" + ",end";
                                }

                                break;
                            case "cmd92"://start weldone
                                         // return 0 if no weldon detected
                                         // return 1 if 1 weldon detected and it is correct
                                         // return -1 if 1 weldon detected bigger than weldon max
                                         // return -2, -3 if 2, 3 weldons detected
                                         // return -4 if error occured
                                var taskCW = Task.Run(() => CycleWeldon());
                                await taskCW;
                                int nCode = taskCW.Result;
                                send = "cmd92,1," + nCode.ToString() + ",end";

                                break;
                            case "cmd93"://front inspect
                                RejectBfront = -1;
                                RejectPfront = -1;
                                var task5 = Task.Run(() => InspectFront());
                                await task5;

                                if (task5.Result.result)
                                {
                                    send = "cmd93,1," + task5.Result.status + ",end";

                                }
                                else
                                {
                                    send = "cmd93,0,end";
                                }
                                break;
                            case "cmd94"://start vision top cycle
                                Stopwatch sw = new Stopwatch();
                                sw.Restart();
                                //RejectB = -1;
                                //RejectP = -1;
                                //bCycle = false;
                                //bWeldonCycle = false;
                                //frmMainInspect.StopCycle = true;
                                //Thread.Sleep(200);
                                //frmMainInspect.StopCycle = false;
                                //SetNumBufferSizeFromFrmInspect();
                                var task10 = Task.Run(() => CycleVision());
                                await task10;
                                if (task10.Result.result)
                                {
                                    send = "cmd94,1," + task10.Result.status + ",end";

                                }
                                else
                                {
                                    send = "cmd94,0,end";
                                }
                                break;



                            case "cmd95"://start cycle top

                                SetNumBufferSizeFromFrmInspect();

                                Stopwatch sw1 = new Stopwatch();
                                sw1.Restart();
                                RejectB = -1;
                                RejectP = -1;

                                var task2 = Task.Run(() => CycleVision());
                                Thread.Sleep(500);
                                var task4 = Task.Run(() => CycleInspect());

                                await task2;
                                if (task2.Result.result)
                                {
                                    send = "cmd95,1," + task2.Result.status + ",end";

                                }
                                else
                                {
                                    send = "cmd95,0,end";
                                }
                                break;
                            case "cmd96"://stop cycle
                                bCycle = false;
                                bWeldonCycle = false;
                                frmMainInspect.StopCycle = true;

                                send = "cmd96,1,end";

                                break;

                            case "cmd97"://color
                                string sFilePath = "";
                                bool bres = false;
                                int snaps = (int)(numBufferSize.Value / int.Parse( s[1]));
                                for (int i = 1; i < numBufferSize.Value; i = i + snaps)
                                {

                                    sFilePath = aPath + "\\Images\\snap" + i.ToString() + ".jpg";
                                    //sFilePath = aPath + "\\Images\\snap1 16.jpg"; //"C:\\Users\\DeepL\\Documents\\Visual Studio Projects\\ContourIdentification\\Images";
                                    if (File.Exists(sFilePath))
                                    {
                                        var task6 = Task.Run(() => FileHistogram(sFilePath));
                                        await task6;
                                        StrHist result = task6.Result;
                                        if (!result.bErr)
                                        {

                                        }
                                        else
                                        {

                                            bres = true;
                                            break;
                                        }
                                    }
                                    Thread.Sleep(200);
                                    if (frmMainInspect.StopCycle) { bres = true; break; }
                                }
                                if (!bres) send = "cmd97,1," + snaps.ToString() + ",end";
                                else send = "cmd97,0," + snaps.ToString() + ",end";
                                break;
                            case "cmd98"://save data
                                var task7 = Task.Run(() => SaveROI());
                                await task7;
                                WebComm.CommReply reply7 = /*new WebComm.CommReply();*/
                                    task7.Result;
                                if (reply7.result)
                                {
                                    
                                    send = "cmd98,1,"  + "end";
                                }
                                else
                                {
                                    send = "cmd98,0," + "end";
                                }

                                break;
                            case "cmd99"://check comm
                                send = "cmd99,1,end";
                                break;
                            //wait after cmd83
                            case "cmd83"://wait cognex
                                var task3 = Task.Run(() => WaitCycleInspectFini());
                                await task3;

                                if (task3.Result.result)
                                {
                                    send = "cmd83,1," + task3.Result.status + ",end";

                                }
                                else
                                {
                                    send = "cmd83,0,end";
                                }
                                break;

                            case "cmd80"://enable comm
                                if (s.Length >= 2)
                                {
                                    if (s[1].Trim() == "1")
                                    {
                                        PortEnable("Ethernet");
                                        send = "cmd80,1,end";
                                    }
                                    else if (s[1].Trim() == "0")
                                    {
                                        PortDisable("Ethernet");
                                        send = "cmd80,1,end";
                                    }
                                }
                                else
                                {
                                    send = "cmd80,0,end";
                                }
                                break;
                            case "cmd79"://cicle init
                                //if (s.Length >= 2)
                                //{
                                    //frmMainInspect.PartNumber = s[1].Trim();
                                    //inv.settxt(txtPartNum, s[1].Trim());
                                //}
                                
                                bCycle = true;
                                frmMainInspect.StopCycle = false;
                                SetNumBufferSizeFromFrmInspect();
                                btn_status(false);
                                inv.set(btnStopCycle, "Enabled", true);
                                ButtonsEnabled(false);
                                //inv.set(panel1liveTab,"Visible", true);
                                frmMainInspect.txtListBox1Disable = true;
                                bool b = false;// CycleInit();
                                var task0 = Task.Run(() => CycleInit());
                                await task0;
                                b = task0.Result;
                                Thread.Sleep(100);
                                if (b) _ = Task.Run(() => frmMainInspect.ImageFromFileTask());
                                if (b) send = "cmd79,1,end";
                                else send = "cmd79,0,end";
                                frmMainInspect.StopCycle = false;


                                break;

                        }
                    }


                    var task1 = Task.Run(() => WC1.SendHttp(reply1.contex, send));
                    await task1;
                    Thread.Sleep(1);
                    if (txtServer.Text.Length > 2000) inv.settxt(txtServer, "");


                }
                inv.settxt(txtServer, txtServer.Text + "Http Server Stopped" + "\r\n");
                WC1.Stop();
                return true;


            }
            catch (Exception ex)
            {
                return false;
                //throw;
            }
        }
        private async Task<bool> RunWebComm1()//cognex
        {
            try
            {

                bStop = false;
                //inv.set(btnSatrt2, "Enabled", false);
                WC2.SetControls1(txtServer, this, "Cognex", "http://*:5001/");


                WebComm.HostReply reply1 = new WebComm.HostReply();
                if (!WC2.StartServer())
                {
                    inv.settxt(txtServer, txtServer.Text + "Http Cognex Server Start Error" + "\r\n");
                    return false;
                }
                string send = "";
                while (!bStop)
                {
                    Thread.Sleep(20);
                    var task = Task.Run(() => WC2.ReadHttp());
                    await task;
                    reply1 = task.Result;
                    if (!reply1.result) { bStop = true; return false; }
                    string[] s = reply1.comment.Split(',');
                    if (s != null && s.Length > 2)
                    {
                        switch (s[0])
                        {
                            case "cmd59"://check comm
                                send = "cmd59,1,end";
                                break;

                            case "cmd56"://stop cycle
                                bCycle = false;
                                send = "cmd56,1,end";

                                break;
                            case "cmd53"://start sua
                                Stopwatch sw1 = new Stopwatch();
                                sw1.Restart();
                                if (chkCognex.Checked)
                                {
                                    var task4 = Task.Run(() => CycleInspect());
                                    await task4;
                                    frmMainInspect.txtListBox1Disable = false;
                                    if (task4.Result.result)
                                    {
                                        send = "cmd53,1," + task4.Result.status + ",end";

                                    }
                                    else
                                    {
                                        send = "cmd53,0,end";
                                    }
                                    break;
                                }
                                else
                                    send = "cmd53,1,end";

                                break;
                            case "cmd54"://stop sua

                                send = "cmd54,1,end";

                                break;
                            case "cmd52"://front inspect
                                RejectBfront = -1;
                                RejectPfront = -1;
                                var task5 = Task.Run(() => InspectFront());
                                await task5;

                                if (task5.Result.result)
                                {
                                    send = "cmd52,1," + task5.Result.status + ",end";

                                }
                                else
                                {
                                    send = "cmd52,0,end";
                                }
                                break;

                        }
                    }


                    var task1 = Task.Run(() => WC2.SendHttp(reply1.contex, send));
                    await task1;
                    Thread.Sleep(1);
                    if (txtServer.Text.Length > 2000) inv.settxt(txtServer, "");


                }
                inv.settxt(txtServer, txtServer.Text + "Http Cognex Server Stopped" + "\r\n");
                WC2.Stop();
                return true;


            }
            catch (Exception ex)
            {
                return false;
                //throw;
            }
        }

        private void txtServer_DoubleClick(object sender, EventArgs e)
        {
            try
            {
                txtServer.Text = "";
            }
            catch { }
        }

        WebComm WC1 = new WebComm();//vision
        WebComm WC2 = new WebComm();//cognex

        private void txtClient_DoubleClick(object sender, EventArgs e)
        {
            try
            {
                txtServer.Text = "";
            }
            catch { }
        }

        private void btnStop2_Click_1(object sender, EventArgs e)
        {
            try
            {
                bStop = true;
                WC1.Stop();
                WC2.Stop();
                inv.set(btnSatrt2, "Enabled", true);
            }
            catch (Exception ex) { }
        }

        private void btnLoadImage_Click(object sender, EventArgs e)
        {
            openFileDialog1.InitialDirectory = aPath + "\\Images"; //"C:\\Users\\DeepL\\Documents\\Visual Studio Projects\\ContourIdentification\\Images";
            openFileDialog1.FileName = txtFileName.Text;
            openFileDialog1.Filter = "Image Files (*.jpeg;*.jpg;*.png;*.gif)|(*.jpeg;*.jpg;*.png;*.gif|JPEG Files (*.jpeg)|*.jpeg|PNG Files (*.png)|*.png|JPG Files (*.jpg)|*.jpg|GIF Files (*.gif)|*.gif";
            DialogResult result = openFileDialog1.ShowDialog();
            if (result == DialogResult.OK || result == DialogResult.Yes)
            {
                txtFileName.Text = openFileDialog1.FileName; // System.IO.Path.GetFileName(openFileDialog1.FileName);
                                                             ////ForceUIToUpdate();
                                                             ////using (frmWaitForm frm = new frmWaitForm(System.Action(() => processor.PerformShapeDetection())))
                                                             //using (frmWaitForm frm = new frmWaitForm(Invoke((Action)(() => processor.PerformShapeDetection()) ))
                                                             ////System.Action<> worker => processor.PerformShapeDetection(); 
                                                             ////using (frmWaitForm frm = new frmWaitForm(worker)) {
                                                             //    frm.ShowDialog(this);
                                                             //}
                txtFileName.Refresh();
                FileStream fs = new System.IO.FileStream(openFileDialog1.FileName, FileMode.Open, FileAccess.Read);
                Image pic = Image.FromStream(fs);
                fs.Close();

                //Image pic = new Bitmap(openFileDialog1.FileName);
                //Bitmap img = new Bitmap(openFileDialog1.FileName);
                //for (int i = 0; i < img.Width; i++) {
                //    for (int j = 0; j < img.Height; j++) {
                //        Color pixel = img.GetPixel(i,j);
                //    }
                //}
                pctSource.SizeMode = PictureBoxSizeMode.Zoom;
                pctSource.Image = pic;
                pctGray.SizeMode = PictureBoxSizeMode.Zoom;
            }
        }

        private void btnCalibrate_Click(object sender, EventArgs e)
        {
            if (lblDiam.Text.Trim() != "" && txtDiamActual.Text.Trim() != "")
            {
                double coeff = Convert.ToDouble(txtDiamActual.Text) / Convert.ToDouble(lblDiamUncalib.Text);
                txtCalib.Text = String.Format("{0:f16}", coeff);
            }
        }

        public async void btnDetectDiameter_Click(object sender, EventArgs e)
        {
            var taskD = Task.Run(() => CalcDiamWeldon(false));
            await taskD;
            double diam = taskD.Result.Diam;
            //if (diam == 0) {
            //    MessageBox.Show("Perform Diameter Calculation Error !", "Detect Diameter", MessageBoxButtons.OK, MessageBoxIcon.Error, MessageBoxDefaultButton.Button1);
            //} else {
            //    //inv.set(Form1.mFormDefInstance.tabControl2, "SelectedIndex", 2);
            //}
        }

        // return StrWeldon.Diam = 0 if diameter not detected
        // return StrWeldon.WeldonTop and StrWeldon.WeldonButtom if bWeldon is true
        Rectangle PartRectImage = new Rectangle();
        public  StrWeldon CalcDiamWeldon(bool bWeldon)
        {
            StrWeldon wd = new StrWeldon();
            wd.Diam = 0;
            wd.WeldonTop = 0;
            wd.WeldonButtom = 0;
            wd.RightEdge = 0;

            string sTxtD = (inv.gettxt(mFormBeckhoffDefInstance.txtD)).Trim();
            if (sTxtD != "") inv.settxt(txtDiamNominal, sTxtD);

            if (bWeldon)
            {
                inv.settxt(lblWeldonTop, "");
                inv.settxt(lblWeldonButtom, "");
            }
            else
            {
                inv.settxt(lblDiam, "");
                inv.settxt(lblRightEdge, "");
                inv.settxt(lblDiamUncalib, "");
            }
            inv.settxt(txtWeldonTime, "");

            this.Invoke(new Action(() => lblWeldonTop.Refresh()));
            this.Invoke(new Action(() => lblDiam.Refresh()));
            this.Invoke(new Action(() => lblDiamUncalib.Refresh()));
            this.Invoke(new Action(() => txtWeldonTime.Refresh()));

            int linewidth = Convert.ToInt32(inv.gettxt(txtLineWidth));

            if (pctSource.Image == null) return wd;

            System.Windows.Forms.Cursor curs = this.Cursor;
            if (InvokeRequired)
                this.Invoke(new Action(() => this.Cursor = Cursors.WaitCursor));
            else
                this.Cursor = Cursors.WaitCursor;

            if (bWeldon)
                inv.set(btnDetectWeldon, "Enabled", false);
            else
                inv.set(btnDetectDiam, "Enabled", false);

            Stopwatch watch = Stopwatch.StartNew();
            watch.Reset(); watch.Start();

            Bitmap imSource = null;
            this.Invoke(new Action(() => imSource = new Bitmap(pctSource.Image)));
            //Bitmap imSource = new Bitmap((Image)inv.get(pctSource, "Image"));
            Image<Gray, byte> grayImage = imSource.ToImage<Gray, byte>();
            Mat mt = new Mat(imSource.ToImage<Bgr, byte>().Mat, new Rectangle(0, 0, imSource.Width, imSource.Height));
            Mat matGrayImage = new Mat();
            // Translate into grayscale
            CvInvoke.CvtColor(mt, matGrayImage, Emgu.CV.CvEnum.ColorConversion.Rgb2Gray);
            Mat matBlurImage = new Mat();
            // Gaussian filtering
            CvInvoke.GaussianBlur(matGrayImage, matBlurImage, new System.Drawing.Size(5, 5), 3);

            matBlurImage = 255 - matBlurImage;

            int absOffset = 3;
            Mat element = CvInvoke.GetStructuringElement(Emgu.CV.CvEnum.ElementShape.Rectangle,
                new System.Drawing.Size(absOffset * 2 + 1, absOffset * 2 + 1), new System.Drawing.Point(absOffset, absOffset));
            //CvInvoke.MorphologyEx(matBlurImage, matBlurImage, Emgu.CV.CvEnum.MorphOp.Close, element,
            //    new Point(-1, -1), 1, Emgu.CV.CvEnum.BorderType.Default, new MCvScalar(0, 0, 0)); //(0)
            CvInvoke.MorphologyEx(matBlurImage, matBlurImage, Emgu.CV.CvEnum.MorphOp.Dilate, element,
                new System.Drawing.Point(-1, -1), 2, Emgu.CV.CvEnum.BorderType.Default, new MCvScalar(0, 0, 0)); //(0)
            CvInvoke.MorphologyEx(matBlurImage, matBlurImage, Emgu.CV.CvEnum.MorphOp.Erode, element,
                new System.Drawing.Point(-1, -1), 5, Emgu.CV.CvEnum.BorderType.Default, new MCvScalar(0, 0, 0)); //(0)

            double cannyThreshold = CvInvoke.Threshold(matBlurImage, matBlurImage, 0, 255, Emgu.CV.CvEnum.ThresholdType.Otsu);

            //for (int i = 0; i < matBlurImage.Height; i++)
            //    for (int j = 0; i < matBlurImage.Width; j++) {
            //        if (j > 0 && j < matBlurImage.Width - 50 && matBlurImage[i, j] > 128 )
            //            for (int k = j + 1; k <)
            //    }

            double fVisible = 0;
            if (Convert.ToBoolean(inv.get(chkShowBig, "Checked")))
            {
                if (InvokeRequired)
                {
                    this.Invoke(new Action(() => fVisible = CvInvoke.GetWindowProperty("matBlurImage-Thresholded", WindowPropertyFlags.Visible)));
                    if (fVisible > 0) this.Invoke(new Action(() => CvInvoke.DestroyWindow("matBlurImage-Thresholded")));

                    this.Invoke(new Action(() => CvInvoke.NamedWindow("matBlurImage-Thresholded", WindowFlags.Normal)));
                    this.Invoke(new Action(() => CvInvoke.Imshow("matBlurImage-Thresholded", matBlurImage)));
                }
                else
                {
                    fVisible = CvInvoke.GetWindowProperty("matBlurImage-Thresholded", WindowPropertyFlags.Visible);
                    if (fVisible > 0) CvInvoke.DestroyWindow("matBlurImage-Thresholded");

                    CvInvoke.NamedWindow("matBlurImage-Thresholded", WindowFlags.Normal); // WindowFlags.AutoSize);
                    CvInvoke.Imshow("matBlurImage-Thresholded", matBlurImage);
                }
            }

            //if (bWeldon) {
            //    UMat matCanny = new UMat();
            //    
            //    //cannyThreshold = 112;
            //    double cannyThresholdLinking = cannyThreshold; // 64;
            //
            //    // remove noise and run edge detection
            //    UMat matPyrdown = new UMat();
            //    CvInvoke.PyrDown(matGrayImage, matPyrdown);
            //    CvInvoke.PyrUp(matPyrdown, matGrayImage);
            //    CvInvoke.Canny(matGrayImage, matCanny, cannyThreshold, cannyThresholdLinking);
            //
            //    Image<Bgr, byte> imgCanny = matCanny.ToImage<Bgr, byte>().Copy();
            //
            //    //int HoughLinesPThreshold = (int)Form1.mFormDefInstance.numHoughLinesPThreshold.Value;
            //    //int HoughLinesPMinLineLength = (int)Form1.mFormDefInstance.numHoughLinesPMinLineLength.Value;
            //    //int HoughLinesPMaxGapBetweenLines = (int)Form1.mFormDefInstance.numHoughLinesPMaxGapBetweenLines.Value;
            //
            //    LineSegment2D[] lines = CvInvoke.HoughLinesP(
            //       matCanny,
            //       1, //Distance resolution in pixel-related units
            //       Math.PI / 180.0, //Angle resolution measured in radians. //45.0
            //       50, //10, //HoughLinesPThreshold, //20 //threshold
            //       20, //500, //HoughLinesPMinLineLength, //10, //min Line width //30
            //       10  //3 // HoughLinesPMaxGapBetweenLines); //10 //gap between lines //threshold = 100,minLineLength = 100,maxLineGap = 50)
            //    );
            //    for (int i = 0; i < lines.Length; i++) {
            //        if (Math.Abs(lines[i].P1.Y - lines[i].P2.Y) < 20)
            //            CvInvoke.Line(imgCanny, lines[i].P1, lines[i].P2, new MCvScalar(0, 0, 255), linewidth);
            //    }
            //
            //    if (InvokeRequired) {
            //        this.Invoke(new Action(() => CvInvoke.NamedWindow("matCanny", WindowFlags.Normal)));
            //        this.Invoke(new Action(() => CvInvoke.Imshow("matCanny", matCanny)));
            //    } else {
            //        CvInvoke.NamedWindow("matCanny", WindowFlags.Normal); // WindowFlags.AutoSize);
            //        CvInvoke.Imshow("matCanny", matCanny);
            //    }
            //}

            Image<Bgr, byte> result = matBlurImage.ToImage<Bgr, byte>().Copy();

            VectorOfVectorOfPoint contours = new VectorOfVectorOfPoint();
            CvInvoke.FindContours(matBlurImage, contours, null, Emgu.CV.CvEnum.RetrType.List, Emgu.CV.CvEnum.ChainApproxMethod.ChainApproxSimple);

            double coeff = Convert.ToDouble(inv.gettxt(txtCalib));
            double DiamNominal = Convert.ToDouble(inv.gettxt(txtDiamNominal));

            // p1    p2
            // p3    p4
            System.Drawing.Point p1 = new System.Drawing.Point(), p2 = new System.Drawing.Point(), p3 = new System.Drawing.Point(), p4 = new System.Drawing.Point();

            for (int i = 0; i < contours.Size; i++)
            {
                if (contours[i].Size > 4)
                {
                    Rectangle rect = CvInvoke.BoundingRectangle(contours[i]);
                    if (rect.Width > 200 && rect.Height > 50) //rect.Width > 200 && rect.Height > 2
                    {
                        wd.X0 = rect.X;
                        wd.Y0 = rect.Y;
                        wd.H = rect.Height;
                        wd.W = rect.Width;
                        PartRectImage = rect;
                        PartRectImage.Height = (int)(((Single)PartRectImage.Height / (Single)grayImage.Height) * pctSnap.Height);
                        PartRectImage.Width = (int)(((Single)PartRectImage.Width / (Single)grayImage.Width) * pctSnap.Width);
                        PartRectImage.X = (int)(((Single)PartRectImage.X / (Single)grayImage.Width) * pctSnap.Width);
                        PartRectImage.Y = (int)(((Single)PartRectImage.Y / (Single)grayImage.Height) * pctSnap.Height);

                        PartRectImage.X = PartRectImage.X + 10;
                        PartRectImage.Y = PartRectImage.Y - 5;
                        PartRectImage.Width = PartRectImage.Width - 5;
                        //re.Height = Math.Abs((int)((re.Height / 2.0f) *3.0f* Math.Sin((3.14f / 180.0f) * (90.0f / (Single)numBufferSize.Value))));
                        PartRectImage.Height = 30 + Math.Abs((int)((PartRectImage.Height / 2.0f) - (PartRectImage.Height / 2.0f) * Math.Cos((3.14f / 180.0f) * (360.0f / (Single)numBufferSize.Value))));
                        frmMainInspect.PartRect = PartRectImage;
                        Single scaleWimage = (Single)pctSnap.Width / (Single)frmMainInspect.pictureBoxInspect.Width;//scale in picturebox1 by waight
                        Single scaleHimage = (Single)pctSnap.Height / (Single)frmMainInspect.pictureBoxInspect.Height;//scale in picturebox1 by height

                        Single sW = (Single)frmMainInspect.pictureBoxInspect.Image.Width / (Single)pctSnap.Width;
                        Single sH = (Single)frmMainInspect.pictureBoxInspect.Image.Height / (Single)pctSnap.Height;
                        if (frmMainInspect.chkAutoROI.Checked && PartRectImage.Height != 0 && PartRectImage.Width != 0)
                        {
                            inv.settxt(frmMainInspect.txtPProiPosX, ((int)(PartRectImage.X * sW)).ToString());
                            inv.settxt(frmMainInspect.txtPProiPosY, ((int)(PartRectImage.Y * sH)).ToString());
                            inv.settxt(frmMainInspect.txtPProiHeight, ((int)(PartRectImage.Height * sH)).ToString());
                            inv.settxt(frmMainInspect.txtPProiWidth, ((int)(PartRectImage.Width * sW)).ToString());
                            frmMainInspect.numBufferSize = (int)numBufferSize.Value;
                            frmMainInspect.btnSaveROI_Click(null, null);


                        }

                        result.Draw(rect, new Bgr(Color.Blue), linewidth);

                        var rectangle = CvInvoke.MinAreaRect(contours[i]);
                        System.Drawing.Point[] vertices = Array.ConvertAll(rectangle.GetVertices(), System.Drawing.Point.Round);
                        CvInvoke.Polylines(result, vertices, true, new MCvScalar(0, 0, 255), linewidth);

                        //get 2 ~horizontal lines: p1-p2 and p3-p4 
                        int ind1 = 0, ind2 = 0, ind3 = -1, ind4 = -1;
                        double distmin = Math.Sqrt((Math.Pow(rect.X - vertices[0].X, 2) + Math.Pow(rect.Y - vertices[0].Y, 2)));
                        for (int j = 0; j <= 3; j++)
                        {
                            double dist = Math.Sqrt((Math.Pow(rect.X - vertices[j].X, 2) + Math.Pow(rect.Y - vertices[j].Y, 2)));
                            if (j == 0 || dist < distmin) { distmin = dist; ind1 = j; }
                        }
                        for (int j = 0; j <= 3; j++)
                        {
                            double dist = Math.Sqrt((Math.Pow(rect.X + rect.Width - vertices[j].X, 2) + Math.Pow(rect.Y - vertices[j].Y, 2)));
                            if (j == 0 || dist < distmin) { distmin = dist; ind2 = j; }
                        }
                        for (int j = 0; j <= 3; j++)
                        {
                            if (j == ind1) continue;
                            if (j == ind2) continue;
                            if (ind3 == -1) { ind3 = j; continue; }
                            if (ind4 == -1) { ind4 = j; continue; }
                        }
                        PointF closest = new PointF();
                        distmin = FindDistanceToSegment(new PointF((vertices[ind1].X + vertices[ind2].X) * 0.5f, (vertices[ind1].Y + vertices[ind2].Y) * 0.5f),
                            new PointF(vertices[ind3].X, vertices[ind3].Y), new PointF(vertices[ind4].X, vertices[ind4].Y), out closest);
                        CvInvoke.Line(result, new System.Drawing.Point((int)((vertices[ind1].X + vertices[ind2].X) * 0.5f), (int)((vertices[ind1].Y + vertices[ind2].Y) * 0.5f)),
                            new System.Drawing.Point((int)closest.X, (int)closest.Y), new MCvScalar(0, 0, 255), linewidth);

                        double distmin1 = FindDistanceToSegment(new PointF((vertices[ind3].X + vertices[ind4].X) * 0.5f, (vertices[ind3].Y + vertices[ind4].Y) * 0.5f),
                            new PointF(vertices[ind1].X, vertices[ind1].Y), new PointF(vertices[ind2].X, vertices[ind2].Y), out closest);
                        CvInvoke.Line(result, new System.Drawing.Point((int)((vertices[ind3].X + vertices[ind4].X) * 0.5f), (int)((vertices[ind3].Y + vertices[ind4].Y) * 0.5f)),
                            new System.Drawing.Point((int)closest.X, (int)closest.Y), new MCvScalar(255, 0, 0), linewidth);

                        p1 = vertices[ind1]; p2 = vertices[ind2]; p3 = vertices[ind3]; p4 = vertices[ind4];

                        //if (bWeldon) {
                        //    wd.weldon = Convert.ToDouble(inv.gettxt(txtCalib)) * (distmin + distmin1) / 2f;
                        //    inv.settxt(lblWeldon, String.Format("{0:f3}", wd.weldon));
                        //} else {
                        double DiamUncalib = (distmin + distmin1) / 2f;
                        inv.settxt(lblDiamUncalib, String.Format("{0:f16}", DiamUncalib));

                        if ((bool)inv.get(chkCalibrate, "Checked"))
                        {
                            if (DiamUncalib != 0 && inv.gettxt(txtDiamActual).Trim() != "") //inv.gettxt(lblDiamUncalib).Trim() != ""
                            {
                                coeff = Convert.ToDouble(inv.gettxt(txtDiamActual)) / DiamUncalib;
                                inv.settxt(txtCalib, String.Format("{0:f16}", coeff));
                                inv.set(chkCalibrate, "Checked", false);
                            }
                        }

                        wd.Diam = coeff * (distmin + distmin1) / 2f;
                        inv.settxt(lblDiam, String.Format("{0:f3}", wd.Diam));
                        this.Invoke(new Action(() => lblDiam.Refresh()));
                        wd.RightEdge = coeff * (rect.Left + rect.Width);
                        inv.settxt(lblRightEdge, String.Format("{0:f3}", wd.RightEdge));
                        this.Invoke(new Action(() => lblRightEdge.Refresh()));

                        //}
                        break;
                    }
                }
            }


            if (bWeldon && wd.Diam > 0)
            {
                int ROIL = 0;
                int ROIR = 0;
                this.Invoke(new Action(() => ROIL = Convert.ToInt16(txtROIL.Text)));
                this.Invoke(new Action(() => ROIR = Convert.ToInt16(txtROIR.Text)));

                ROIL = (ROIL + ROIR) / 2;
                ROIR = ROIL + 50;
                ROIL = ROIL - 50;

                //if (!bDebugMode)
                //{
                //    ROIR = ROIR - ROIL - 100;
                //    ROIL = 50;
                //}

                //Emgu.CV.Dnn..BlobDetector bDetect = new Emgu.CV.Cvb.CvBlobDetector();
                //SimpleBlobDetector simpleBlobDetector = new SimpleBlobDetector(new SimpleBlobDetectorParams()
                //{
                //    FilterByCircularity = true,
                //    FilterByArea = true,
                //    MinCircularity = 0.7f,
                //    MaxCircularity = 1.0f,
                //    MinArea = 500,
                //    MaxArea = 10000
                //});

                bool bFillGreen = false;
                if (Convert.ToBoolean(inv.get(chkFillWeldonGreen, "Checked"))) bFillGreen = true;

                int dy = Convert.ToInt16(TableWeldonNominal(DiamNominal) / coeff);

                for (int c = ROIL; c < ROIR; c++) //c = 100; c < result.Width - 100; c++
                {
                    int rmin = p1.Y + (c - p1.X) * (p2.Y - p1.Y) / (p2.X - p1.X);
                    int h = 0;
                    int hmin = rmin;
                    int hmax = hmin;
                    for (int r = rmin; r < rmin + dy; r++)
                    {
                        if (grayImage.Data[r, c, 0] > 190)
                        { //resultresult.Data[r, c, 0] < 172
                            if (h == 0) hmin = r;
                            h++; //result.Data[r, c, 0] = 0; result.Data[r, c, 1] = 255; result.Data[r, c, 2] = 0;
                            hmax = r;
                        }
                        else break;
                    }
                    if (h > 0.2 / coeff) //>0.2mm
                    {
                        if (bFillGreen)
                        {
                            for (int r = rmin; r < rmin + dy; r++)
                            {
                                if (grayImage.Data[r, c, 0] > 190)
                                { //resultresult.Data[r, c, 0] < 172
                                    result.Data[r, c, 0] = 0; result.Data[r, c, 1] = 255; result.Data[r, c, 2] = 0;
                                }
                                else break;
                            }
                        }
                        else
                        {
                            result.Data[hmin, c, 0] = 0; result.Data[hmin, c, 1] = 255; result.Data[hmin, c, 2] = 0;
                            result.Data[hmax, c, 0] = 0; result.Data[hmax, c, 1] = 255; result.Data[hmax, c, 2] = 0;
                        }
                        if (h > wd.WeldonTop) wd.WeldonTop = h;
                    }

                    rmin = p3.Y + (c - p3.X) * (p4.Y - p3.Y) / (p4.X - p3.X);
                    int htop = 0;
                    hmin = rmin;
                    hmax = hmin;
                    for (int r = rmin; r > rmin - dy; r--)
                    {
                        if (grayImage.Data[r, c, 0] > 190)
                        { //resultresult.Data[r, c, 0] < 172
                            if (h == 0) hmin = r;
                            htop++; // result.Data[r, c, 0] = 0; result.Data[r, c, 1] = 255; result.Data[r, c, 2] = 0;
                            hmax = r;
                        }
                        else break;
                    }
                    if (htop > 0.2 / coeff) //>0.2mm
                    {
                        if (bFillGreen)
                        {
                            for (int r = rmin; r > rmin - dy; r--)
                            {
                                if (grayImage.Data[r, c, 0] > 190)
                                { //resultresult.Data[r, c, 0] < 172
                                    result.Data[r, c, 0] = 0; result.Data[r, c, 1] = 255; result.Data[r, c, 2] = 0;
                                }
                                else break;
                            }
                        }
                        else
                        {
                            result.Data[hmin, c, 0] = 0; result.Data[hmin, c, 1] = 255; result.Data[hmin, c, 2] = 0;
                            result.Data[hmax, c, 0] = 0; result.Data[hmax, c, 1] = 255; result.Data[hmax, c, 2] = 0;
                        }
                        if (htop > wd.WeldonButtom) wd.WeldonButtom = htop;
                    }
                }
                wd.WeldonTop = coeff * wd.WeldonTop;
                wd.WeldonButtom = coeff * wd.WeldonButtom;
                inv.settxt(lblWeldonTop, String.Format("{0:f3}", wd.WeldonTop));
                this.Invoke(new Action(() => lblWeldonTop.Refresh()));
                inv.settxt(lblWeldonButtom, String.Format("{0:f3}", wd.WeldonButtom));
                this.Invoke(new Action(() => lblWeldonButtom.Refresh()));

                //this.Invoke(new Action(() => lblWeldon.Refresh()));
                //this.lblWeldon.BeginInvoke((MethodInvoker)delegate () { this.lblWeldon.Text = String.Format("{0:f3}", wd.weldon); });
                //if (InvokeRequired) {
                //    this.Invoke(new Action(() => lblWeldon.Text = String.Format("{0:f3}", wd.weldon)));
                //} else {
                //    lblWeldon.Text = String.Format("{0:f3}", wd.weldon);
                //}
            }

            watch.Stop();
            inv.settxt(txtWeldonTime, watch.ElapsedMilliseconds.ToString());
            this.Invoke(new Action(() => txtWeldonTime.Refresh()));

            if (bWeldon)
                inv.set(btnDetectWeldon, "Enabled", true);
            else
                inv.set(btnDetectDiam, "Enabled", true);

            fVisible = 0;
            if (InvokeRequired)
            {
                if (Convert.ToBoolean(inv.get(chkShowBig, "Checked")))
                {
                    this.Invoke(new Action(() => fVisible = CvInvoke.GetWindowProperty("Rectangle image", WindowPropertyFlags.Visible)));
                    if (fVisible > 0) this.Invoke(new Action(() => CvInvoke.DestroyWindow("Rectangle image")));

                    this.Invoke(new Action(() => CvInvoke.NamedWindow("Rectangle image", WindowFlags.Normal)));
                    this.Invoke(new Action(() => CvInvoke.Imshow("Rectangle image", result)));
                }
                this.Invoke(new Action(() => pctGray.Image = result.ToBitmap()));
                this.Invoke(new Action(() => pctGray.Refresh()));

                this.Invoke(new Action(() => this.Cursor = curs));
            }
            else
            {
                if (Convert.ToBoolean(inv.get(chkShowBig, "Checked")))
                {
                    fVisible = CvInvoke.GetWindowProperty("Rectangle image", WindowPropertyFlags.Visible);
                    if (fVisible > 0) CvInvoke.DestroyWindow("Rectangle image");

                    CvInvoke.NamedWindow("Rectangle image", WindowFlags.Normal); // WindowFlags.AutoSize);
                    CvInvoke.Imshow("Rectangle image", result);
                }
                pctGray.Image = result.ToBitmap();
                pctGray.Refresh();

                this.Cursor = curs;
            }
            return wd;
        }

        public double TableWeldonNominal(double DiamNominal)
        {
            double dy = 0;
            if (DiamNominal <= 6) dy = 0.9 + 0.075;
            else if (DiamNominal <= 8) dy = 1.1 + 0.090;
            else if (DiamNominal <= 10) dy = 1.5 + 0.090;
            else if (DiamNominal <= 12) dy = 1.6 + 0.110;
            else if (DiamNominal <= 14) dy = 1.3 + 0.110;
            else if (DiamNominal <= 16) dy = 1.8 + 0.110;
            else if (DiamNominal <= 18) dy = 1.8 + 0.110;
            else if (DiamNominal <= 20) dy = 1.8 + 0.130;
            else if (DiamNominal <= 25) dy = 2.0 + 0.130;
            else if (DiamNominal <= 32) dy = 2.0 + 0.130;
            return dy;
        }

        // Calculate the distance between point pt and the segment p1 --> p2
        private static double FindDistanceToSegment(PointF pt, PointF p1, PointF p2, out PointF closest)
        {
            double dx = p2.X - p1.X;
            double dy = p2.Y - p1.Y;
            if ((dx == 0) && (dy == 0))
            {
                // It's a point not a line segment.
                closest = p1;
                dx = pt.X - p1.X;
                dy = pt.Y - p1.Y;
                return Math.Sqrt(dx * dx + dy * dy);
            }

            // Calculate the t that minimizes the distance.
            double t = ((pt.X - p1.X) * dx + (pt.Y - p1.Y) * dy) / (dx * dx + dy * dy);

            // See if this represents one of the segment's end points or a point in the middle.
            if (t < 0)
            {
                closest = new PointF(p1.X, p1.Y);
                dx = pt.X - p1.X;
                dy = pt.Y - p1.Y;
            }
            else if (t > 1)
            {
                closest = new PointF(p2.X, p2.Y);
                dx = pt.X - p2.X;
                dy = pt.Y - p2.Y;
            }
            else
            {
                closest = new PointF((float)(p1.X + t * dx), (float)(p1.Y + t * dy));
                dx = pt.X - closest.X;
                dy = pt.Y - closest.Y;
            }
            return Math.Sqrt(dx * dx + dy * dy);
        }

        public async void btnDetectWeldon_Click(object sender, EventArgs e)
        {
            var taskW = Task.Run(() => CalcDiamWeldon(true));
            await taskW;
            double weldontop = taskW.Result.WeldonTop;
            double weldonbuttom = taskW.Result.WeldonButtom;
            //if (weldon == 0) {
            //    MessageBox.Show("Perform Weldon Calculation Error !", "Detect Weldon", MessageBoxButtons.OK, MessageBoxIcon.Error, MessageBoxDefaultButton.Button1);
            //} else {
            //    //inv.set(Form1.mFormDefInstance.tabControl2, "SelectedIndex", 2);
            //}
        }

        public  double CalcWeldon()
        {
            double weldon = 0;
            inv.settxt(lblWeldonTop, "");
            inv.settxt(txtWeldonTime, "");
            int linewidth = Convert.ToInt32(inv.gettxt(txtLineWidth));

            if (pctSource.Image == null) return weldon;

            System.Windows.Forms.Cursor curs = this.Cursor;
            if (InvokeRequired)
                this.Invoke(new Action(() => this.Cursor = Cursors.WaitCursor));
            else
                this.Cursor = Cursors.WaitCursor;

            inv.set(btnDetectWeldon, "Enabled", false);

            Stopwatch watch = Stopwatch.StartNew();
            watch.Reset(); watch.Start();

            Bitmap imSource = new Bitmap(pctSource.Image);
            Image<Gray, byte> grayImage = imSource.ToImage<Gray, byte>();
            Mat mt = new Mat(imSource.ToImage<Bgr, byte>().Mat, new Rectangle(0, 0, imSource.Width, imSource.Height));
            Mat matGrayImage = new Mat();
            // Translate into grayscale
            CvInvoke.CvtColor(mt, matGrayImage, Emgu.CV.CvEnum.ColorConversion.Rgb2Gray);
            Mat matBlurImage = new Mat();
            // Gaussian filtering
            CvInvoke.GaussianBlur(matGrayImage, matBlurImage, new System.Drawing.Size(5, 5), 3);

            matBlurImage = 255 - matBlurImage;

            int absOffset = 3;
            Mat element = CvInvoke.GetStructuringElement(Emgu.CV.CvEnum.ElementShape.Rectangle,
                new System.Drawing.Size(absOffset * 2 + 1, absOffset * 2 + 1), new System.Drawing.Point(absOffset, absOffset));
            CvInvoke.MorphologyEx(matBlurImage, matBlurImage, Emgu.CV.CvEnum.MorphOp.Close, element,
                new System.Drawing.Point(-1, -1), 1, Emgu.CV.CvEnum.BorderType.Default, new MCvScalar(0, 0, 0)); //(0)

            //if (InvokeRequired) {
            //    this.Invoke(new Action(() => CvInvoke.NamedWindow("matBlurImage1", WindowFlags.Normal)));
            //    this.Invoke(new Action(() => CvInvoke.Imshow("matBlurImage1", matBlurImage)));
            //} else {
            //    CvInvoke.NamedWindow("matBlurImage1", WindowFlags.Normal); // WindowFlags.AutoSize);
            //    CvInvoke.Imshow("matBlurImage1", matBlurImage);
            //}

            double cannyThreshold = CvInvoke.Threshold(matBlurImage, matBlurImage, 0, 255, Emgu.CV.CvEnum.ThresholdType.Otsu);

            //if (InvokeRequired) {
            //    this.Invoke(new Action(() => CvInvoke.NamedWindow("matBlurImage2", WindowFlags.Normal)));
            //    this.Invoke(new Action(() => CvInvoke.Imshow("matBlurImage2", matBlurImage)));
            //} else {
            //    CvInvoke.NamedWindow("matBlurImage2", WindowFlags.Normal); // WindowFlags.AutoSize);
            //    CvInvoke.Imshow("matBlurImage2", matBlurImage);
            //}

            UMat matPyrdown = new UMat();
            UMat matCanny = new UMat();
            cannyThreshold = 112;
            double cannyThresholdLinking = 64;

            // remove noise and run edge detection
            CvInvoke.PyrDown(matGrayImage, matPyrdown);
            CvInvoke.PyrUp(matPyrdown, matGrayImage);
            CvInvoke.Canny(matGrayImage, matCanny, cannyThreshold, cannyThresholdLinking);



            Image<Bgr, byte> imgCanny = matCanny.ToImage<Bgr, byte>().Copy();

            //int HoughLinesPThreshold = (int)Form1.mFormDefInstance.numHoughLinesPThreshold.Value;
            //int HoughLinesPMinLineLength = (int)Form1.mFormDefInstance.numHoughLinesPMinLineLength.Value;
            //int HoughLinesPMaxGapBetweenLines = (int)Form1.mFormDefInstance.numHoughLinesPMaxGapBetweenLines.Value;

            LineSegment2D[] lines = CvInvoke.HoughLinesP(
               matCanny,
               1, //Distance resolution in pixel-related units
               Math.PI / 180.0, //Angle resolution measured in radians. //45.0
               20, //HoughLinesPThreshold, //20 //threshold
               200, //HoughLinesPMinLineLength, //10, //min Line width //30
               20); // HoughLinesPMaxGapBetweenLines); //10 //gap between lines //threshold = 100,minLineLength = 100,maxLineGap = 50)

            for (int i = 0; i < lines.Length; i++)
            {
                if (Math.Abs(lines[i].P1.Y - lines[i].P2.Y) < 20)
                    CvInvoke.Line(imgCanny, lines[i].P1, lines[i].P2, new MCvScalar(0, 0, 255), linewidth);
            }

            if (InvokeRequired)
            {
                this.Invoke(new Action(() => CvInvoke.NamedWindow("matCanny", WindowFlags.Normal)));
                this.Invoke(new Action(() => CvInvoke.Imshow("matCanny", matCanny)));
            }
            else
            {
                CvInvoke.NamedWindow("matCanny", WindowFlags.Normal); // WindowFlags.AutoSize);
                CvInvoke.Imshow("matCanny", matCanny);
            }





            Image<Bgr, byte> result = matBlurImage.ToImage<Bgr, byte>().Copy();

            VectorOfVectorOfPoint contours = new VectorOfVectorOfPoint();
            CvInvoke.FindContours(matBlurImage, contours, null, Emgu.CV.CvEnum.RetrType.List, Emgu.CV.CvEnum.ChainApproxMethod.ChainApproxSimple);

            for (int i = 0; i < contours.Size; i++)
            {
                if (contours[i].Size > 4)
                {
                    Rectangle rect = CvInvoke.BoundingRectangle(contours[i]);
                    if (rect.Width > 200 && rect.Height > 50) //rect.Width > 200 && rect.Height > 2
                    {
                        result.Draw(rect, new Bgr(Color.Blue), linewidth);

                        var rectangle = CvInvoke.MinAreaRect(contours[i]);
                        System.Drawing.Point[] vertices = Array.ConvertAll(rectangle.GetVertices(), System.Drawing.Point.Round);
                        CvInvoke.Polylines(result, vertices, true, new MCvScalar(0, 0, 255), linewidth);

                        //get 2 ~horizontal lines: p1-p2 and p3-p4 
                        int ind1 = 0, ind2 = 0, ind3 = -1, ind4 = -1;
                        double distmin = Math.Sqrt((Math.Pow(rect.X - vertices[0].X, 2) + Math.Pow(rect.Y - vertices[0].Y, 2)));
                        for (int j = 0; j <= 3; j++)
                        {
                            double dist = Math.Sqrt((Math.Pow(rect.X - vertices[j].X, 2) + Math.Pow(rect.Y - vertices[j].Y, 2)));
                            if (j == 0 || dist < distmin) { distmin = dist; ind1 = j; }
                        }
                        for (int j = 0; j <= 3; j++)
                        {
                            double dist = Math.Sqrt((Math.Pow(rect.X + rect.Width - vertices[j].X, 2) + Math.Pow(rect.Y - vertices[j].Y, 2)));
                            if (j == 0 || dist < distmin) { distmin = dist; ind2 = j; }
                        }
                        for (int j = 0; j <= 3; j++)
                        {
                            if (j == ind1) continue;
                            if (j == ind2) continue;
                            if (ind3 == -1) { ind3 = j; continue; }
                            if (ind4 == -1) { ind4 = j; continue; }
                        }
                        PointF closest = new PointF();
                        distmin = FindDistanceToSegment(new PointF((vertices[ind1].X + vertices[ind2].X) * 0.5f, (vertices[ind1].Y + vertices[ind2].Y) * 0.5f),
                            new PointF(vertices[ind3].X, vertices[ind3].Y), new PointF(vertices[ind4].X, vertices[ind4].Y), out closest);
                        CvInvoke.Line(result, new System.Drawing.Point((int)((vertices[ind1].X + vertices[ind2].X) * 0.5f), (int)((vertices[ind1].Y + vertices[ind2].Y) * 0.5f)),
                            new System.Drawing.Point((int)closest.X, (int)closest.Y), new MCvScalar(0, 0, 255), linewidth);

                        double distmin1 = FindDistanceToSegment(new PointF((vertices[ind3].X + vertices[ind4].X) * 0.5f, (vertices[ind3].Y + vertices[ind4].Y) * 0.5f),
                            new PointF(vertices[ind1].X, vertices[ind1].Y), new PointF(vertices[ind2].X, vertices[ind2].Y), out closest);
                        CvInvoke.Line(result, new System.Drawing.Point((int)((vertices[ind3].X + vertices[ind4].X) * 0.5f), (int)((vertices[ind3].Y + vertices[ind4].Y) * 0.5f)),
                            new System.Drawing.Point((int)closest.X, (int)closest.Y), new MCvScalar(255, 0, 0), linewidth);

                        weldon = Convert.ToDouble(inv.gettxt(txtCalib)) * (distmin + distmin1) / 2f;
                        inv.settxt(lblWeldonTop, String.Format("{0:f3}", weldon));
                        break;
                    }
                }
            }

            watch.Stop();
            inv.settxt(txtWeldonTime, watch.ElapsedMilliseconds.ToString());

            inv.set(btnDetectWeldon, "Enabled", true);
            if (InvokeRequired)
            {
                this.Invoke(new Action(() => CvInvoke.NamedWindow("Rectangle image", WindowFlags.Normal)));
                this.Invoke(new Action(() => CvInvoke.Imshow("Rectangle image", result)));
                this.Invoke(new Action(() => pctGray.Image = result.ToBitmap()));

                this.Invoke(new Action(() => this.Cursor = curs));
            }
            else
            {
                CvInvoke.NamedWindow("Rectangle image", WindowFlags.Normal); // WindowFlags.AutoSize);
                CvInvoke.Imshow("Rectangle image", result);
                pctGray.Image = result.ToBitmap();

                this.Cursor = curs;
            }
            return weldon;
        }

        public async void btnSnapDiam_Click(object sender, EventArgs e)
        {
            WebComm.CommReply reply = new WebComm.CommReply();

            var taskSD = Task.Run(() => TaskSnapDiam());
            await taskSD;
            reply = taskSD.Result;
            this.Invoke(new Action(() => this.Cursor = Cursors.Default));
        }

        // return 0 if diameter not detected
        public bool bDiamCycle = false;
        public async Task<WebComm.CommReply> TaskSnapDiam()
        {
            WebComm.CommReply reply = new WebComm.CommReply();

            reply.result = false;
            reply.data = new float[10];
            try
            {
                inv.set(btnSnapDiam, "Enabled", false);
                bool bExit = false;
                bool bErr = false;
                bDiamCycle = true;

                inv.set(optSnap2, "Checked", true);

                while (!bExit)
                {
                    var task = Task.Run(() => ProcDetect(true, true, true));
                    await task;

                    if (!task.Result.berr && chkSnapCont.Checked) {
                        Stopwatch stopwatch = new Stopwatch();
                        stopwatch.Reset();
                        stopwatch.Start();
                        while (stopwatch.ElapsedMilliseconds < 10)  { //100
                            Thread.Sleep(1); //5
                            System.Windows.Forms.Application.DoEvents();
                        }
                        stopwatch.Stop();
                    } else {
                        if (task.Result.berr) {
                            bErr = true;
                            if (bShowMsgBox)
                                MessageBox.Show(task.Result.sException, "Snap+Diameter", MessageBoxButtons.OK, MessageBoxIcon.Error, MessageBoxDefaultButton.Button1);
                        }
                        bExit = true;
                    }
                }
                bDiamCycle = false;
                inv.set(optSnap1, "Checked", true);
                if (bErr) { inv.set(btnSnapDiam, "Enabled", true); return reply; }

                string sFile = "";
                try {
                    if (!(inv.get(pct1liveTab, "Image") is null)) {
                        sFile = aPath + "\\Images\\snap " + DateTime.Now.ToString("yy-MM-dd HH-mm-ss") + ".jpg";
                        this.Invoke(new Action(() => pct1liveTab.Image.Save(sFile, ImageFormat.Jpeg)));
                    } else
                        bErr = true;
                }
                catch (Exception exception) {
                    ShowException("Snap camera Top ", exception);
                    bErr = true;
                }
                if (bErr || sFile == "") { inv.set(btnSnapDiam, "Enabled", true); return reply; }


                inv.settxt(txtFileName, sFile);
                this.Invoke(new Action(() => txtFileName.Refresh()));
                FileStream fs = new System.IO.FileStream(sFile, FileMode.Open, FileAccess.Read);
                System.Drawing.Image pic = System.Drawing.Image.FromStream(fs);
                fs.Close();

                inv.set(pctSource, "SizeMode", PictureBoxSizeMode.Zoom);
                inv.set(pctSource, "Image", pic);
                inv.set(pctGray, "SizeMode", PictureBoxSizeMode.Zoom);

                inv.set(btnSnapDiam, "Enabled", true);

                var taskD = Task.Run(() => CalcDiamWeldon(false));
                await taskD;
                double diam = taskD.Result.Diam;
                double RightEdge = taskD.Result.RightEdge;


                //if (diam == 0) {
                //    bErr = true;
                //    MessageBox.Show("Perform Diameter Calculation Error !", "Detect Diameter", MessageBoxButtons.OK, MessageBoxIcon.Error, MessageBoxDefaultButton.Button1);
                //} else {
                //    //inv.set(Form1.mFormDefInstance.tabControl2, "SelectedIndex", 2);
                //}
                
                //inv.set(btnSnapDiam, "Enabled", true);

                reply.result = true;
                reply.data[0] = (Single)diam;
                reply.data[1] = (Single)RightEdge;
                return reply;
            }
            catch (Exception ex) { reply.result = false; return reply; }
        }

        private void chkEmul_CheckedChanged(object sender, EventArgs e)
        {
            if (chkEmul.Checked)
                bDebugMode = true;
            else
                bDebugMode = false;
        }

        private void lblCount_DoubleClick(object sender, EventArgs e)
        {
            lblCount.Text = "0";
        }

        private async void btnTestWeldon_Click(object sender, EventArgs e)
        {
            var taskTestWeldon = Task.Run(() => TestWeldon());
            await taskTestWeldon;
            bool rc = taskTestWeldon.Result;
        }

        public async Task<bool> TestWeldon()
        {
            Bitmap bitmap = new Bitmap(pctDiag.Width, pctDiag.Height, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
            Graphics graphics = Graphics.FromImage(bitmap);
            //Pen pen = new Pen(Color.Blue, 2);
            //graphics.DrawArc(pen, 0, 0, 700, 700, 0, 180);
            this.Invoke(new Action(() => pctDiag.Image = bitmap));

            this.Invoke(new Action(() => pctDiag.Tag = 0));
            this.Invoke(new Action(() => pctDiag.Invalidate()));

            this.Invoke(new Action(() => pctSource.SizeMode = PictureBoxSizeMode.Zoom));
            this.Invoke(new Action(() => pctGray.SizeMode = PictureBoxSizeMode.Zoom));

            this.Invoke(new Action(() => lblW1.Text = "")); this.Invoke(new Action(() => lblW1.Refresh()));
            this.Invoke(new Action(() => lblW2.Text = "")); this.Invoke(new Action(() => lblW2.Refresh()));
            this.Invoke(new Action(() => lblW3.Text = "")); this.Invoke(new Action(() => lblW3.Refresh()));
            this.Invoke(new Action(() => lblWResult.Text = "")); this.Invoke(new Action(() => lblWResult.Refresh()));

            int nFrameMax = 0;
            numBufferSize.Invoke((Action)(() => { nFrameMax = (int)numBufferSize.Value; }));

            bool[] bW = new bool[nFrameMax];

            for (int i = 1; i <= nFrameMax; i++)
            {
                string fName = aPath + "\\images\\snap" + i.ToString() + ".jpg";
                inv.settxt(txtFileName, fName);
                this.Invoke(new Action(() => txtFileName.Refresh()));
                FileStream fs = new System.IO.FileStream(fName, FileMode.Open, FileAccess.Read);
                Image pic = Image.FromStream(fs);
                fs.Close();

                this.Invoke(new Action(() => pctSource.Image = pic));
                this.Invoke(new Action(() => pctSource.Refresh()));

                var taskWD = Task.Run(() => CalcDiamWeldon(true));
                await taskWD;

                if (taskWD.Result.Diam == 0) {
                    if (bShowMsgBox)
                        MessageBox.Show("Perform Weldon Calculation Error !", "Detect Diameter", MessageBoxButtons.OK, MessageBoxIcon.Error, MessageBoxDefaultButton.Button1);
                    return false;
                } else {
                    //inv.set(Form1.mFormDefInstance.tabControl2, "SelectedIndex", 2);
                    //inv.settxt(lblWeldon, String.Format("{0:f3}", taskWD.Result.weldon));
                    this.Invoke(new Action(() => pctDiag.Tag = i));
                    if (taskWD.Result.WeldonTop > 0) bW[i - 1] = true;
                    if (taskWD.Result.WeldonButtom > 0)
                        if (i < nFrameMax / 2) //8
                            bW[i + nFrameMax / 2 - 1] = true; // i+8-1
                        else
                            bW[i - nFrameMax / 2] = true; //i-8
                }
                this.Invoke(new Action(() => pctDiag.Invalidate()));
            }

            int nWeldons = -1;
            bool bWeldonIn = false;
            int[] WeldonStart = new int[nFrameMax];
            int[] WeldonEnd = new int[nFrameMax];
            for (int i = 0; i <= nFrameMax - 1; i++) {
                if (bW[i]) {
                    if (bWeldonIn)
                        WeldonEnd[nWeldons] = i;
                    else {
                        bWeldonIn = true;
                        nWeldons++;
                        WeldonStart[nWeldons] = i;
                        WeldonEnd[nWeldons] = i;
                    }
                } else
                    bWeldonIn = false;
            }

            //this.Invoke(new Action(() => lblW1.Text = WeldonStart[0].ToString() + "-" + WeldonEnd[0].ToString())); 
            //this.Invoke(new Action(() => lblW1.Refresh()));
            //this.Invoke(new Action(() => lblW2.Text = WeldonStart[1].ToString() + "-" + WeldonEnd[1].ToString()));
            //this.Invoke(new Action(() => lblW2.Refresh()));
            //this.Invoke(new Action(() => lblW3.Text = WeldonStart[2].ToString() + "-" + WeldonEnd[2].ToString()));
            //this.Invoke(new Action(() => lblW3.Refresh()));

            int nWeldonIndex15 = -1;
            int k1 = WeldonEnd[0] - WeldonStart[0] + 1;
            if (nWeldons > 0 && WeldonEnd[nWeldons] == nFrameMax - 1) {
                k1 += WeldonEnd[nWeldons] - WeldonStart[nWeldons] + 1;
                nWeldonIndex15 = nWeldons;
            }
            int k2 = 0;
            if (nWeldons > 0 && nWeldonIndex15 > 1) k2 = WeldonEnd[1] - WeldonStart[1] + 1;
            int k3 = 0;
            if (nWeldons > 1 && nWeldonIndex15 > 2) k3 = WeldonEnd[2] - WeldonStart[2] + 1;

            float angle = 360f / nFrameMax;

            if (k1 > 0) {
                this.Invoke(new Action(() => lblW1.Text = ((k1 + 1) * angle).ToString() + "°(" + k1.ToString() + ")"));
                this.Invoke(new Action(() => lblW1.Refresh()));
            }
            if (k2 > 0) {
                this.Invoke(new Action(() => lblW2.Text = ((k2 + 1) * angle).ToString() + "°(" + k2.ToString() + ")"));
                this.Invoke(new Action(() => lblW2.Refresh()));
            }
            if (k3 > 0) {
                this.Invoke(new Action(() => lblW3.Text = ((k3 + 1) * angle).ToString() + "°(" + k3.ToString() + ")"));
                this.Invoke(new Action(() => lblW3.Refresh()));
            }

            double DiamNominal = Convert.ToDouble(inv.gettxt(txtDiamNominal));
            double WeldonNominal = TableWeldonNominal(DiamNominal); // [mm]
            WeldonNominal = (Math.Acos((DiamNominal / 2 - WeldonNominal) / (DiamNominal / 2))) * 2 * 180 / Math.PI; //°
            this.Invoke(new Action(() => lblWeldonMax.Text = String.Format("{0:0.0}", WeldonNominal)));
            this.Invoke(new Action(() => lblWeldonMax.Refresh()));

            if (k1 > 0 && k2 == 0 && k3 == 0 && ((k1 + 1) * angle) <= WeldonNominal) {
                this.Invoke(new Action(() => lblWResult.Text = "Weldon ok"));
                this.Invoke(new Action(() => lblWResult.ForeColor = Color.Green));
                this.Invoke(new Action(() => lblWResult.Refresh()));
                return true;
            } else {
                this.Invoke(new Action(() => lblWResult.Text = "Weldon bad"));
                this.Invoke(new Action(() => lblWResult.ForeColor = Color.Red));
                this.Invoke(new Action(() => lblWResult.Refresh()));
                return false;
            }
        }

        private void pctDiag_Paint(object sender, PaintEventArgs e)
        {
            try
            {
                int nFrameMax = 0;
                numBufferSize.Invoke((Action)(() => { nFrameMax = (int)numBufferSize.Value; }));

                if (pctDiag.Tag is null) return;
                int nTag = (int)pctDiag.Tag;
                if (nTag < 0 || nTag > nFrameMax) return;

                Image bmp = pctDiag.Image;
                if (bmp is null) return;

                using (Graphics g = Graphics.FromImage(bmp))
                {
                    if (nTag == 0)
                    {
                        g.Clear(BackColor);
                        g.DrawEllipse(
                            new Pen(Color.Blue, 1f),
                            0, 0, pctDiag.Size.Width - 4, pctDiag.Size.Height - 4);
                    }
                    else if (nTag >= 1 && nTag <= nFrameMax)
                    {
                        double weldontop = 0;
                        if (inv.gettxt(lblWeldonTop) != "")
                            weldontop = Convert.ToDouble(inv.gettxt(lblWeldonTop));
                        double weldonbuttom = 0;
                        if (inv.gettxt(lblWeldonButtom) != "")
                            weldonbuttom = Convert.ToDouble(inv.gettxt(lblWeldonButtom));
                        double radius = Convert.ToDouble(inv.gettxt(lblDiam)) / 2f;
                        double vecttop = (radius - weldontop) / radius * pctDiag.Size.Width / 2f;
                        double vectbuttom = (radius - weldonbuttom) / radius * pctDiag.Size.Width / 2f;
                        int cx = pctDiag.Size.Width / 2 - 2;
                        int cy = pctDiag.Size.Height / 2 - 2;
                        double angle = (nTag - 1) * Math.PI * 2 / nFrameMax;

                        //top
                        int px = cx + (int)(vecttop * Math.Sin(angle));
                        int py = cy - (int)(vecttop * Math.Cos(angle));

                        g.DrawLine(new Pen(Color.Blue, 1f), new System.Drawing.Point(cx, cy), new System.Drawing.Point(px, py));
                        if (weldontop > 0)
                        {
                            //g.DrawLine(new Pen(Color.Red, 1f), new Point(cx, cy), new Point(px, py));
                            g.DrawLine(new Pen(Color.Red, 1f),
                                new System.Drawing.Point(px + (int)(10 * Math.Cos(angle)), py + (int)(10 * Math.Sin(angle))),
                                new System.Drawing.Point(px - (int)(10 * Math.Cos(angle)), py - (int)(10 * Math.Sin(angle))));
                        }
                        else
                        {
                            //g.DrawLine(new Pen(Color.Blue, 1f), new Point(cx, cy), new Point(px, py));
                        }

                        //buttom
                        px = cx - (int)(vectbuttom * Math.Sin(angle));
                        py = cy + (int)(vectbuttom * Math.Cos(angle));

                        g.DrawLine(new Pen(Color.Blue, 1f), new System.Drawing.Point(cx, cy), new System.Drawing.Point(px, py));
                        if (weldonbuttom > 0)
                        {
                            //g.DrawLine(new Pen(Color.Red, 1f), new Point(cx, cy), new Point(px, py));
                            g.DrawLine(new Pen(Color.Red, 1f),
                                new System.Drawing.Point(px + (int)(10 * Math.Cos(angle)), py + (int)(10 * Math.Sin(angle))),
                                new System.Drawing.Point(px - (int)(10 * Math.Cos(angle)), py - (int)(10 * Math.Sin(angle))));
                        }
                        else
                        {
                            //g.DrawLine(new Pen(Color.Blue, 1f), new Point(cx, cy), new Point(px, py));
                        }
                    }
                    pctDiag.Tag = 100;
                }
                pctDiag.Image = bmp;
            }
            catch (Exception ex) { }
        }

        public async void btnSnapWeldon_Click(object sender, EventArgs e)
        {
            var taskSW = Task.Run(() => TaskSnapWeldon());
            await taskSW;
            bool bError = taskSW.Result.bErr;
            double weldontop = taskSW.Result.WeldonTop;
            double weldonbuttom = taskSW.Result.WeldonButtom;
        }

        // return StrWeldon.bErr = true if error occured
        // else return StrWeldon.WeldonTop and StrWeldon.WeldonButtom if weldon(s) detected

        public async Task<StrWeldon> TaskSnapWeldon()
        {
            inv.set(btnSnapWeldon, "Enabled", false);
            bool bExit = false;
            bool bErr = false;

            inv.set(optSnap2, "Checked", true);

            StrWeldon wd = new StrWeldon();
            wd.Diam = 0;
            wd.WeldonTop = 0;
            wd.WeldonButtom = 0;

            while (!bExit)
            {
                var task = Task.Run(() => ProcDetect(true, true));
                await task;

                if (!task.Result.berr && Convert.ToBoolean(inv.get(chkSnapCont, "Checked")))
                {
                    Stopwatch stopwatch = new Stopwatch();
                    stopwatch.Reset();
                    stopwatch.Start();
                    while (stopwatch.ElapsedMilliseconds < 10) { //100
                        Thread.Sleep(1); //5
                        Application.DoEvents();
                    }
                    stopwatch.Stop();
                }
                else
                {
                    if (task.Result.berr) {
                        bErr = true;
                        if (bShowMsgBox)
                            MessageBox.Show(task.Result.sException, "Snap+Weldon", MessageBoxButtons.OK, MessageBoxIcon.Error, MessageBoxDefaultButton.Button1);
                    }
                    bExit = true;
                }
            }
            if (bErr) { inv.set(btnSnapWeldon, "Enabled", true); wd.bErr = true; return wd; }

            string sFile = "";
            try
            {
                if (!(inv.get(pct1liveTab, "Image") is null))
                {
                    sFile = aPath + "\\Images\\snap " + DateTime.Now.ToString("yy-MM-dd HH-mm-ss") + ".jpg";
                    this.Invoke(new Action(() => pct1liveTab.Image.Save(sFile, ImageFormat.Jpeg)));
                }
                else
                    bErr = true;
            }
            catch (Exception exception)
            {
                ShowException("Snap camera Top ", exception);
                bErr = true;
            }
            if (bErr || sFile == "") { inv.set(btnSnapWeldon, "Enabled", true); wd.bErr = true; return wd; }


            inv.settxt(txtFileName, sFile);
            this.Invoke(new Action(() => txtFileName.Refresh()));
            FileStream fs = new System.IO.FileStream(sFile, FileMode.Open, FileAccess.Read);
            Image pic = Image.FromStream(fs);
            fs.Close();

            inv.set(pctSource, "SizeMode", PictureBoxSizeMode.Zoom);
            inv.set(pctSource, "Image", pic);
            inv.set(pctGray, "SizeMode", PictureBoxSizeMode.Zoom);

            var taskW = Task.Run(() => CalcDiamWeldon(true));
            await taskW;
            double weldontop = taskW.Result.WeldonTop;
            double weldonbuttom = taskW.Result.WeldonButtom;
            //if (diam == 0) {
            //    bErr = true;
            //    MessageBox.Show("Perform Diameter Calculation Error !", "Detect Diameter", MessageBoxButtons.OK, MessageBoxIcon.Error, MessageBoxDefaultButton.Button1);
            //} else {
            //    //inv.set(Form1.mFormDefInstance.tabControl2, "SelectedIndex", 2);
            //}
            inv.set(btnSnapWeldon, "Enabled", true);
            return taskW.Result;
        }

        public async void btnStartCycleWeldon_Click(object sender, EventArgs e)
        {
            try
            {
                var taskCW = Task.Run(() => CycleWeldon());
                await taskCW;
                int nCode = taskCW.Result;
            }
            catch (Exception ex) { }
        }

        // return 0 if no weldon detected
        // return 1 if 1 weldon detected and it is correct
        // return -1 if 1 weldon detected bigger than weldon max
        // return -2, -3 if 2, 3 weldons detected
        // return -4 if error occured

        public async Task<int> CycleWeldon()
        {
            if (bCycle) return -4;

            inv.settxt(lblCount, "0");

            //if (mFrmMain.xContinue)
            //{
            //    //mFrmMain.bContinue = true;
            //    //mFrmMain.xContinue = false;
            //    //mFrmMain.CancelAsync = true;
            //    this.Invoke(new Action(() => mFrmMain.btnBGWstop_Click(mFrmMain.btnBGWstop, null)));
            //    Thread.Sleep(1000);
            //}

            bool bErr = false;
            int nCode = 0;

            ButtonsEnabled(false);

            inv.settxt(frmBeckhoff.mFormBeckhoffDefInstance.txtAcqTime, "");
            inv.settxt(frmBeckhoff.mFormBeckhoffDefInstance.txtInspectTime, "");
            inv.settxt(frmBeckhoff.mFormBeckhoffDefInstance.txtElapsedTime, "");
            inv.settxt(txtMess, "");
            //inv.set(trackBarSpeedSt, "Value", 90);

            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Reset();
            stopwatch.Start();
            //inv.settxt(frmBeckhoff.mFormBeckhoffDefInstance.txtCycleTime, "");
            long t1 = stopwatch.ElapsedMilliseconds;

            //ClearPictureArray();
            pctSource.Invoke((Action)(() => { pctSource.Image = null; }));
            nWeldonCycleEmul = 0;

            //ROI
            bool bUseSearchArea = false;
            this.Invoke(new Action(() => bUseSearchArea = chkUseSearchArea.Checked));
            this.Invoke(new Action(() => chkUseSearchArea.Checked = true));
            int RectPointX = 0;
            int RectPointY = 0;
            int RectPointW = 0;
            int RectPointH = 0;
            this.Invoke(new Action(() => RectPointX = Convert.ToInt16(txtRectPointX.Text)));
            this.Invoke(new Action(() => RectPointY = Convert.ToInt16(txtRectPointY.Text)));
            this.Invoke(new Action(() => RectPointW = Convert.ToInt16(txtSearchAreaWidth.Text)));
            this.Invoke(new Action(() => RectPointH = Convert.ToInt16(txtSearchAreaHeight.Text)));
            this.Invoke(new Action(() => txtRectPointX.Text = txtROIL.Text));
            this.Invoke(new Action(() => txtRectPointY.Text = "0"));
            this.Invoke(new Action(() => txtSearchAreaWidth.Text = (Convert.ToInt16(txtROIR.Text) - Convert.ToInt16(txtROIL.Text)).ToString()));
            if (!(inv.get(pct1liveTab, "Image") is null))
                this.Invoke(new Action(() => txtSearchAreaHeight.Text = pct1liveTab.Image.Height.ToString()));
            else if (!(inv.get(pctSource, "Image") is null))
                this.Invoke(new Action(() => txtSearchAreaHeight.Text = pctSource.Image.Height.ToString())); // pct1liveTab.Image.Height.ToString()));
            else
                this.Invoke(new Action(() => txtSearchAreaHeight.Text = "3032"));

            int nFrameMax = PreparePictureArray();

            Bitmap bitmap = new Bitmap(pctDiag.Width, pctDiag.Height, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
            Graphics graphics = Graphics.FromImage(bitmap);
            this.Invoke(new Action(() => pctDiag.Image = bitmap));

            this.Invoke(new Action(() => pctDiag.Tag = 0));
            this.Invoke(new Action(() => pctDiag.Invalidate()));

            this.Invoke(new Action(() => pctSource.SizeMode = PictureBoxSizeMode.Zoom));
            this.Invoke(new Action(() => pctGray.SizeMode = PictureBoxSizeMode.Zoom));

            this.Invoke(new Action(() => lblW1.Text = "")); this.Invoke(new Action(() => lblW1.Refresh()));
            this.Invoke(new Action(() => lblW2.Text = "")); this.Invoke(new Action(() => lblW2.Refresh()));
            this.Invoke(new Action(() => lblW3.Text = "")); this.Invoke(new Action(() => lblW3.Refresh()));
            this.Invoke(new Action(() => lblWResult.Text = "")); this.Invoke(new Action(() => lblWResult.Refresh()));

            bool[] bW = new bool[nFrameMax];

            //bCycle = true;
            bWeldonCycle = true;

            btn_status(false);
            string s = "";
            for (int nFrame = 1; nFrame <= nFrameMax && bCycle; nFrame++)
            {
                //OptChecked(nFrame);
                nWeldonCycleEmul = nFrame;

                s = s + "<--" + "start rot" + nFrame.ToString() + " // " + DateTime.Now.ToString("HH:mm:ss.fff") + "\r\n";
                //inv.settxt(txtMess, txtMess.Text+ "<--" +"start rot"+nFrame.ToString()+ " // " + DateTime.Now.ToString("HH:mm:ss.fff") + "\r\n");
                if (bDebugMode) {
                    long t2 = stopwatch.ElapsedMilliseconds;
                    while ((stopwatch.ElapsedMilliseconds - t2) < 5) // 100 // rotation emulation
                        Thread.Sleep(1);
                } else {

                    var taskM = Task.Run(() => MotionInCycle(nFrameMax, nFrame - 1));
                    await taskM;

                    if (!taskM.Result) {
                        bCycle = false;
                        bErr = true;
                        if (bShowMsgBox)
                            MessageBox.Show("ERROR MOVE", "MotionInCycle", MessageBoxButtons.OK, MessageBoxIcon.Error, MessageBoxDefaultButton.Button1);
                    }
                }
                //inv.settxt(txtMess, txtMess.Text + "-->" + "fini rot" + nFrame.ToString() + " // " + DateTime.Now.ToString("HH:mm:ss.fff") + "\r\n");
                s = s + "-->" + "fini rot" + nFrame.ToString() + " // " + DateTime.Now.ToString("HH:mm:ss.fff") + "\r\n";

                if (bCycle) {
                    //inv.settxt(txtMess, txtMess.Text + "<--" + "start vis" + nFrame.ToString() + " // " + DateTime.Now.ToString("HH:mm:ss.fff") + "\r\n");
                    s = s + "<--" + "start vis" + nFrame.ToString() + " // " + DateTime.Now.ToString("HH:mm:ss.fff") + "\r\n";

                    var task = Task.Run(() => ProcDetect(true, true));
                    await task;

                    if (!task.Result.berr) {


                        //inv.settxt(lblCount, nFrame.ToString());




                        //if ((bool)inv.get(chkSaveFile, "Checked")) {
                        //    string sErMes = "";
                        //    bool rc = SaveSnapFile(out sErMes);
                        //    if (!rc) {
                        //        bCycle = false;
                        //        MessageBox.Show(sErMes, "Save Image", MessageBoxButtons.OK, MessageBoxIcon.Error, MessageBoxDefaultButton.Button1);
                        //    }
                        //}

                        //switch (nFrame)
                        //{
                        //    case 1: { this.Invoke(new Action(() => pctSource.Image = pct1.Image)); break; }
                        //    case 2: { this.Invoke(new Action(() => pctSource.Image = pct2.Image)); break; }
                        //    case 3: { this.Invoke(new Action(() => pctSource.Image = pct3.Image)); break; }
                        //    case 4: { this.Invoke(new Action(() => pctSource.Image = pct4.Image)); break; }
                        //    case 5: { this.Invoke(new Action(() => pctSource.Image = pct5.Image)); break; }
                        //    case 6: { this.Invoke(new Action(() => pctSource.Image = pct6.Image)); break; }
                        //    case 7: { this.Invoke(new Action(() => pctSource.Image = pct7.Image)); break; }
                        //    case 8: { this.Invoke(new Action(() => pctSource.Image = pct8.Image)); break; }
                        //    case 9: { this.Invoke(new Action(() => pctSource.Image = pct9.Image)); break; }
                        //    case 10: { this.Invoke(new Action(() => pctSource.Image = pct10.Image)); break; }
                        //    case 11: { this.Invoke(new Action(() => pctSource.Image = pct11.Image)); break; }
                        //    case 12: { this.Invoke(new Action(() => pctSource.Image = pct12.Image)); break; }
                        //    case 13: { this.Invoke(new Action(() => pctSource.Image = pct13.Image)); break; }
                        //    case 14: { this.Invoke(new Action(() => pctSource.Image = pct14.Image)); break; }
                        //    case 15: { this.Invoke(new Action(() => pctSource.Image = pct15.Image)); break; }
                        //    case 16: { this.Invoke(new Action(() => pctSource.Image = pct16.Image)); break; }
                        //}
                        this.Invoke(new Action(() => pctSource.Refresh()));

                        var taskWD = Task.Run(() => CalcDiamWeldon(true));
                        await taskWD;

                        if (taskWD.Result.Diam == 0) {
                            bCycle = false;
                            bErr = true;
                            if (bShowMsgBox)
                                MessageBox.Show("Perform Weldon Calculation Error !", "Detect Diameter", MessageBoxButtons.OK, MessageBoxIcon.Error, MessageBoxDefaultButton.Button1);
                            //return false;
                        } else {
                            //inv.set(Form1.mFormDefInstance.tabControl2, "SelectedIndex", 2);
                            //inv.settxt(lblWeldon, String.Format("{0:f3}", taskWD.Result.weldon));
                            this.Invoke(new Action(() => pctDiag.Tag = nFrame));
                            if (taskWD.Result.WeldonTop > 0) bW[nFrame - 1] = true;
                            if (taskWD.Result.WeldonButtom > 0)
                                if (nFrame < nFrameMax / 2) //8
                                    bW[nFrame + nFrameMax / 2 - 1] = true; // i+8-1
                                else
                                    bW[nFrame - nFrameMax / 2] = true; //i-8
                        }
                        this.Invoke(new Action(() => pctDiag.Invalidate()));

                    } else {
                        bCycle = false;
                        bErr = true;
                        if (bShowMsgBox)
                            MessageBox.Show(task.Result.sException, "Snap+Detect3", MessageBoxButtons.OK, MessageBoxIcon.Error, MessageBoxDefaultButton.Button1);
                    }
                    //inv.settxt(txtMess, txtMess.Text + "-->" + "fini vis" + nFrame.ToString() + " // " + DateTime.Now.ToString("HH:mm:ss.fff") + "\r\n");
                    s = s + "-->" + "fini vis" + nFrame.ToString() + " // " + DateTime.Now.ToString("HH:mm:ss.fff") + "\r\n";
                }
                Thread.Sleep(10);
                //Application.DoEvents();
            }

            if (bCycle)
            {
                int nWeldons = -1;
                bool bWeldonIn = false;
                int[] WeldonStart = new int[nFrameMax];
                int[] WeldonEnd = new int[nFrameMax];
                for (int i = 0; i <= nFrameMax - 1; i++)
                {
                    if (bW[i])
                    {
                        if (bWeldonIn)
                            WeldonEnd[nWeldons] = i;
                        else
                        {
                            bWeldonIn = true;
                            nWeldons++;
                            WeldonStart[nWeldons] = i;
                            WeldonEnd[nWeldons] = i;
                        }
                    }
                    else
                        bWeldonIn = false;
                }



                int nWeldonIndex15 = -1;
                int k1 = WeldonEnd[0] - WeldonStart[0] + 1;
                if (nWeldons > 0 && WeldonEnd[nWeldons] == nFrameMax - 1)
                {
                    k1 += WeldonEnd[nWeldons] - WeldonStart[nWeldons] + 1;
                    nWeldonIndex15 = nWeldons;
                }
                int k2 = 0;
                if (nWeldons > 0 && nWeldonIndex15 > 1) k2 = WeldonEnd[1] - WeldonStart[1] + 1;
                int k3 = 0;
                if (nWeldons > 1 && nWeldonIndex15 > 2) k3 = WeldonEnd[2] - WeldonStart[2] + 1;

                float angle = 360f / nFrameMax;

                if (k1 > 0) {
                    this.Invoke(new Action(() => lblW1.Text = ((k1 + 1) * angle).ToString() + "°(" + k1.ToString() + ")"));
                    this.Invoke(new Action(() => lblW1.Refresh()));
                    nCode = 1;
                }
                else
                    nCode = 0; // no weldon found
                if (k2 > 0) {
                    this.Invoke(new Action(() => lblW2.Text = ((k2 + 1) * angle).ToString() + "°(" + k2.ToString() + ")"));
                    this.Invoke(new Action(() => lblW2.Refresh()));
                    nCode = -2;
                }
                if (k3 > 0) {
                    this.Invoke(new Action(() => lblW3.Text = ((k3 + 1) * angle).ToString() + "°(" + k3.ToString() + ")"));
                    this.Invoke(new Action(() => lblW3.Refresh()));
                    nCode = -3;
                }

                double DiamNominal = Convert.ToDouble(inv.gettxt(txtDiamNominal));
                double WeldonNominal = TableWeldonNominal(DiamNominal); // [mm]
                WeldonNominal = (Math.Acos((DiamNominal / 2 - WeldonNominal) / (DiamNominal / 2))) * 2 * 180 / Math.PI; //°
                this.Invoke(new Action(() => lblWeldonMax.Text = String.Format("{0:0.0}", WeldonNominal)));
                this.Invoke(new Action(() => lblWeldonMax.Refresh()));

                if (k1 > 0 && k2 == 0 && k3 == 0 && ((k1 + 1) * angle) <= WeldonNominal)
                {
                    this.Invoke(new Action(() => lblWResult.Text = "Weldon ok"));
                    this.Invoke(new Action(() => lblWResult.ForeColor = Color.Green));
                    this.Invoke(new Action(() => lblWResult.Refresh()));
                    nCode = 1;
                    //return true;
                }
                else
                {
                    this.Invoke(new Action(() => lblWResult.Text = "Weldon bad"));
                    this.Invoke(new Action(() => lblWResult.ForeColor = Color.Red));
                    this.Invoke(new Action(() => lblWResult.Refresh()));
                    bCycle = false;
                    if (nCode == 1) nCode = -1;
                    //return false;
                }
            } else {
                this.Invoke(new Action(() => lblWResult.Text = "Weldon bad"));
                this.Invoke(new Action(() => lblWResult.ForeColor = Color.Red));
                this.Invoke(new Action(() => lblWResult.Refresh()));
                nCode = -4;
            }

            inv.settxt(txtMess, s);

            long t3 = stopwatch.ElapsedMilliseconds;
            stopwatch.Stop();
            inv.settxt(frmBeckhoff.mFormBeckhoffDefInstance.txtCycleTime, String.Format("{0:f0}", t3 - t1));
            btn_status(true);

            //ROI
            this.Invoke(new Action(() => chkUseSearchArea.Checked = bUseSearchArea));
            this.Invoke(new Action(() => txtRectPointX.Text = RectPointX.ToString()));
            this.Invoke(new Action(() => txtRectPointY.Text = RectPointY.ToString()));
            this.Invoke(new Action(() => txtSearchAreaWidth.Text = RectPointW.ToString()));
            this.Invoke(new Action(() => txtSearchAreaHeight.Text = RectPointH.ToString()));

            ButtonsEnabled(true);

            //if (bCycle) { bCycle = false; return true; }
            //else return false;

            if (bCycle)
                bCycle = false;
            else if (nCode == 0)
                bErr = true;

            if (bErr) nCode = -4;

            bWeldonCycle = false;
            nWeldonCycleEmul = 0;

            return nCode;
        }

        private void ClearPictureArray()
        {
            pct1.Invoke((Action)(() => { pct1.Image = null; })); pct2.Invoke((Action)(() => { pct2.Image = null; }));
            pct3.Invoke((Action)(() => { pct3.Image = null; })); pct4.Invoke((Action)(() => { pct4.Image = null; }));
            pct5.Invoke((Action)(() => { pct5.Image = null; })); pct6.Invoke((Action)(() => { pct6.Image = null; }));
            pct7.Invoke((Action)(() => { pct7.Image = null; })); pct8.Invoke((Action)(() => { pct8.Image = null; }));
            pct9.Invoke((Action)(() => { pct9.Image = null; })); pct10.Invoke((Action)(() => { pct10.Image = null; }));
            pct11.Invoke((Action)(() => { pct11.Image = null; })); pct12.Invoke((Action)(() => { pct12.Image = null; }));
            pct13.Invoke((Action)(() => { pct13.Image = null; })); pct14.Invoke((Action)(() => { pct14.Image = null; }));
            pct15.Invoke((Action)(() => { pct15.Image = null; })); pct16.Invoke((Action)(() => { pct16.Image = null; }));
        }

        private int PreparePictureArray()
        {
            int nFrameMax = 0;
            numBufferSize.Invoke((Action)(() => { nFrameMax = (int)numBufferSize.Value; }));
            for (int nFrame = 1; nFrame <= 16; nFrame++) {
                bool bVisible = true;
                if (nFrame > nFrameMax) bVisible = false;
                switch (nFrame) {
                    case 1: { this.Invoke(new Action(() => opt1.Visible = bVisible)); pct1.Invoke((Action)(() => { pct1.Visible = bVisible; })); break; }
                    case 2: { this.Invoke(new Action(() => opt2.Visible = bVisible)); pct2.Invoke((Action)(() => { pct2.Visible = bVisible; })); break; }
                    case 3: { this.Invoke(new Action(() => opt3.Visible = bVisible)); pct3.Invoke((Action)(() => { pct3.Visible = bVisible; })); break; }
                    case 4: { this.Invoke(new Action(() => opt4.Visible = bVisible)); pct4.Invoke((Action)(() => { pct4.Visible = bVisible; })); break; }
                    case 5: { this.Invoke(new Action(() => opt5.Visible = bVisible)); pct5.Invoke((Action)(() => { pct5.Visible = bVisible; })); break; }
                    case 6: { this.Invoke(new Action(() => opt6.Visible = bVisible)); pct6.Invoke((Action)(() => { pct6.Visible = bVisible; })); break; }
                    case 7: { this.Invoke(new Action(() => opt7.Visible = bVisible)); pct7.Invoke((Action)(() => { pct7.Visible = bVisible; })); break; }
                    case 8: { this.Invoke(new Action(() => opt8.Visible = bVisible)); pct8.Invoke((Action)(() => { pct8.Visible = bVisible; })); break; }
                    case 9: { this.Invoke(new Action(() => opt9.Visible = bVisible)); pct9.Invoke((Action)(() => { pct9.Visible = bVisible; })); break; }
                    case 10: { this.Invoke(new Action(() => opt10.Visible = bVisible)); pct10.Invoke((Action)(() => { pct10.Visible = bVisible; })); break; }
                    case 11: { this.Invoke(new Action(() => opt11.Visible = bVisible)); pct11.Invoke((Action)(() => { pct11.Visible = bVisible; })); break; }
                    case 12: { this.Invoke(new Action(() => opt12.Visible = bVisible)); pct12.Invoke((Action)(() => { pct12.Visible = bVisible; })); break; }
                    case 13: { this.Invoke(new Action(() => opt13.Visible = bVisible)); pct13.Invoke((Action)(() => { pct13.Visible = bVisible; })); break; }
                    case 14: { this.Invoke(new Action(() => opt14.Visible = bVisible)); pct14.Invoke((Action)(() => { pct14.Visible = bVisible; })); break; }
                    case 15: { this.Invoke(new Action(() => opt15.Visible = bVisible)); pct15.Invoke((Action)(() => { pct15.Visible = bVisible; })); break; }
                    case 16: { this.Invoke(new Action(() => opt16.Visible = bVisible)); pct16.Invoke((Action)(() => { pct16.Visible = bVisible; })); break; }
                }
            }
            return nFrameMax;
        }

        private void OptChecked(int nFrame)
        {
            switch (nFrame)
            {
                case 1: { this.Invoke(new Action(() => opt1.Checked = true)); break; }
                case 2: { this.Invoke(new Action(() => opt2.Checked = true)); break; }
                case 3: { this.Invoke(new Action(() => opt3.Checked = true)); break; }
                case 4: { this.Invoke(new Action(() => opt4.Checked = true)); break; }
                case 5: { this.Invoke(new Action(() => opt5.Checked = true)); break; }
                case 6: { this.Invoke(new Action(() => opt6.Checked = true)); break; }
                case 7: { this.Invoke(new Action(() => opt7.Checked = true)); break; }
                case 8: { this.Invoke(new Action(() => opt8.Checked = true)); break; }
                case 9: { this.Invoke(new Action(() => opt9.Checked = true)); break; }
                case 10: { this.Invoke(new Action(() => opt10.Checked = true)); break; }
                case 11: { this.Invoke(new Action(() => opt11.Checked = true)); break; }
                case 12: { this.Invoke(new Action(() => opt12.Checked = true)); break; }
                case 13: { this.Invoke(new Action(() => opt13.Checked = true)); break; }
                case 14: { this.Invoke(new Action(() => opt14.Checked = true)); break; }
                case 15: { this.Invoke(new Action(() => opt15.Checked = true)); break; }
                case 16: { this.Invoke(new Action(() => opt16.Checked = true)); break; }
            }
        }

        private void pctSnap_Paint(object sender, PaintEventArgs e)
        {
            try
            {
                
                if (RejectString != "") Reject_Paint(sender, e, 0);
                else
                {
                    if (frmMainInspect.chkROI1.Checked) Reject_Paint(sender, e, 1);
                    if (frmMainInspect.chkROI2.Checked) Reject_Paint(sender, e, 2);
                    if (frmMainInspect.chkROI3.Checked) Reject_Paint(sender, e, 3);
                }
                //frmBeckhoff.frmFrontInspect.pictureBoxInspect.BackgroundImage = pctSnap.Image;
                return;



                Rectangle re = new Rectangle();
                Pen p = new Pen(Color.Red);
                string[] ssbr = new string[1];
                string[] sspel = new string[1];
                int a = 0;
                //break
                for (int i = 0; i < frmMainInspect.RegionFound1BrSave.Length; i++)
                {
                    string s = frmMainInspect.RegionFound1BrSave[i];//break


                    if (s != null && int.Parse(s.Substring(0, 2)) == indexnum + 1)
                    {
                        ssbr[a] = s;
                        a++;
                        Array.Resize<string>(ref ssbr, ssbr.Length + 1);
                    }
                }
                //peels
                a = 0;
                for (int i = 0; i < frmMainInspect.RegionFound1PlSave.Length; i++)
                {
                    string s = frmMainInspect.RegionFound1PlSave[i];//peels

                    if (s != null && int.Parse(s.Substring(0, 2)) == indexnum + 1)
                    {
                        sspel[a] = s;
                        a++;
                        Array.Resize<string>(ref sspel, sspel.Length + 1);
                    }
                }

                Single X0 = 0;
                Single Y0 = 0;
                Single H = 0;
                Single W = 0;

                //Double y010 = (((Single)frmMainInspect.pictureBoxInspect.Height - (Single)frmMainInspect.pictureBoxInspect.Width * ((Single)frmMainInspect.pictureBoxInspect.Image.Height / ((Single)frmMainInspect.pictureBoxInspect.Image.Width))) / 2.0);
                Double x0 = Single.Parse(frmMainInspect.txtPProiPosX.Text) * frmMainInspect.scaleW;
                Double y0 = Single.Parse(frmMainInspect.txtPProiPosY.Text) * frmMainInspect.scaleH;
                Double w = Single.Parse(frmMainInspect.txtPProiWidth.Text) * frmMainInspect.scaleW;
                Double h = Single.Parse(frmMainInspect.txtPProiHeight.Text) * frmMainInspect.scaleH;

                //pctSnap.Width = panel1liveTab.Width - 2; //930
                //pctSnap.Height = panel1liveTab.Height - 2; //698
                Double scalew = ((Single)pctSnap.Width / (Single)frmMainInspect.pictureBoxInspect.Width);//scale in picturebox1 by waight
                Double scaleh = (Single)pctSnap.Height / (Single)frmMainInspect.pictureBoxInspect.Height;//scale in picturebox1 by height
                                                                                                         //y01->yo coordiate for image in picturebox1
                                                                                                         //(y0-y02)*scalew  ->y0 for rectangle in image in picturebox1 
                Double x0roi = ((Single)x0 * scalew);
                Double y0roi = ((Single)y0 * scaleh); //y01 + (y0 - y02) * scaleh;
                Double wroi = scalew * w;
                Double hroi = h * scaleh;
                //re = new Rectangle((int)(x0 * scaleh), (int)(y01 + (y0 - y02) * scalew), (int)((scalew) * w), (int)(h * scaleh));
                re = new Rectangle((int)(x0roi), (int)(y0roi), (int)(wroi), (int)(hroi));

                e.Graphics.DrawRectangle(p, re);
                //frmBeckhoff.frmFrontInspect.pictureBoxInspect.Image = pctSnap.Image;
                //return;
                //
                for (int i = 0; i < 2; i++)
                {
                    string[] ss = new string[1];
                    if (i == 0)
                    {
                        ss = new string[1];
                        Array.Resize<string>(ref ss, ssbr.Length);
                        ss = ssbr;
                        p = new Pen(Color.Red);
                    }
                    else if (i == 1)
                    {
                        ss = new string[1];
                        Array.Resize<string>(ref ss, sspel.Length);
                        ss = sspel;
                        p = new Pen(Color.Yellow);
                    }

                    for (int j = 0; j < ss.Length - 1; j++)
                    {
                        string[] s = ss[j].Split(' ');
                        X0 = -1; Y0 = -1; H = -1; W = -1;
                        for (int jj = 0; jj < s.Length; jj++)
                        {
                            if (s[jj].IndexOf("X0=") >= 0)
                            {
                                X0 = (int)Single.Parse(s[jj].Substring(3, s[jj].Length - 3));
                            }
                            else if (s[jj].IndexOf("Y0=") >= 0)
                            {
                                Y0 = (int)Single.Parse(s[jj].Substring(3, s[jj].Length - 3));
                            }
                            else if (s[jj].IndexOf("H=") >= 0)
                            {
                                H = (int)Single.Parse(s[jj].Substring(2, s[jj].Length - 2));
                            }
                            if (s[jj].IndexOf("W=") >= 0)
                            {
                                W = (int)Single.Parse(s[jj].Substring(2, s[jj].Length - 2));
                            }

                        }

                        if (pctSnap.Image == null) return;
                        Single scaleX = ((Single)pctSnap.Width / (Single)pctSnap.Image.Width);// *((Single)wroi / (Single)pctSnap.Width);
                        Single scaleY = ((Single)pctSnap.Height / (Single)pctSnap.Image.Height);// * ((Single)hroi / (Single)pctSnap.Height);


                        Double x00 = ((Single)X0 * pctSnap.Width / (Single)pctSnap.Image.Width);// * ((Single)pctSnap.Width/(scalew * w));
                        Double y00 = ((Single)Y0 * pctSnap.Height / (Single)pctSnap.Image.Height);// * ((Single)pctSnap.Height / (scalew * h));
                        //w = (Single)W * wroi / (Single)pctSnap.Image.Width;
                        //h = (Single)H * hroi / (Single)pctSnap.Image.Height;

                        w = (Single)W * pctSnap.Width / (Single)pctSnap.Image.Width;
                        h = (Single)H * pctSnap.Height / (Single)pctSnap.Image.Height;

                        //p = new Pen(Color.Red);
                        if (w < 3) { w = 3.0f; }// p = new Pen(Color.Orange); }
                        if (h < 3) { h = 3.0f; }// p = new Pen(Color.Orange); }

                        re = new Rectangle((int)(x00 + x0roi - w / 2), (int)(y00 + y0roi - h / 2), (int)(w), (int)(h));
                        e.Graphics.DrawRectangle(p, re);
                        //frmBeckhoff.frmFrontInspect.pictureBoxInspect.Image = pctSnap.Image;
                    }


                }
            }
            catch (System.Exception ex) { }

        }

        public (int finalX, int finalY) CalculateCoordiantesnForZoomedPictureBox(
            // Original point
            float OriginalPointX0, float OriginalPointY0,
            // Cropping parameters
            float OffsetX, /* crop start X */
            float OffsetY, /* crop start Y */
            float croppedWidth,  /* width2: cropped image width */
            float croppedHeight  /* height2: cropped image height */
            )
        {



            // PictureBox size
            float pbWidth = pctSnap.Width;
            float pbHeight = pctSnap.Height;

            // Step 1: Adjust for cropping
            float croppedX = OriginalPointX0 - OffsetX;
            float croppedY = OriginalPointY0 - OffsetY;

            // Step 2: Calculate scale for PictureBox Zoom mode
            float scale = Math.Min(pbWidth / croppedWidth, pbHeight / croppedHeight);

            // Step 3: Compute displayed image size inside PictureBox
            float displayedWidth = croppedWidth * scale;
            float displayedHeight = croppedHeight * scale;

            // Step 4: Calculate offsets to center the image in the PictureBox
            float offsetX = (pbWidth - displayedWidth) / 2;
            float offsetY = (pbHeight - displayedHeight) / 2;

            // Step 5: Compute the point position in PictureBox coordinates
            int finalX = (int)(offsetX + croppedX * scale);
            int finalY = (int)(offsetY + croppedY * scale);

            return (finalX, finalY);
        }

        // finalX and finalY are the coordinates where the point should be drawn in the PictureBox
        private void Reject_Paint(object sender, PaintEventArgs e, int region)
        {
            try
            {
                if (inCycleInspect || inCycleVision || inCycleInspectFront) return;
                e.Graphics.ResetTransform();
                Pen p = new Pen(Color.Red);
                p.Width = 0.2f;
                string[] RegionFoundBrSave = new string[1];
                string[] RegionFoundPlSave = new string[1];
                Double x0 = 0;
                Double y0 = 0;
                Double w = 0;
                Double h = 0;
                //Single zoom = (Single)pctSnap.Image.Width / (Single)pctSnap.Image.Height; //1.6f;
                //Single pict_zoom = (Single)pctSnap.Width / (Single)pctSnap.Height;
                //zoom = zoom / pict_zoom;
                if (RejectString != "" && RejectString.IndexOf("ROI:") > 0)
                {
                    region = int.Parse(RejectString.Substring(RejectString.IndexOf("ROI:") + 4, 1));
                }

                if (region == 1)
                {
                    p = new Pen(Color.Red);
                    p.Width = 0.2f;
                    RegionFoundBrSave = new string[frmMainInspect.RegionFound1BrSave.Length];
                    RegionFoundBrSave = frmMainInspect.RegionFound1BrSave;
                    RegionFoundPlSave = new string[frmMainInspect.RegionFound1PlSave.Length];
                    RegionFoundPlSave = frmMainInspect.RegionFound1PlSave;

                    x0 = Single.Parse(frmMainInspect.txtPProiPosX.Text) * frmMainInspect.scaleW;
                    y0 = Single.Parse(frmMainInspect.txtPProiPosY.Text) * frmMainInspect.scaleH;
                    w = Single.Parse(frmMainInspect.txtPProiWidth.Text) * frmMainInspect.scaleW;
                    h = Single.Parse(frmMainInspect.txtPProiHeight.Text) * frmMainInspect.scaleH;
                }
                else if (region == 2)
                {
                    p = new Pen(Color.Blue);
                    p.Width = 0.2f;
                    RegionFoundBrSave = new string[frmMainInspect.RegionFound2BrSave.Length];
                    RegionFoundBrSave = frmMainInspect.RegionFound2BrSave;
                    RegionFoundPlSave = new string[frmMainInspect.RegionFound2PlSave.Length];
                    RegionFoundPlSave = frmMainInspect.RegionFound2PlSave;
                    x0 = Single.Parse(frmMainInspect.txtPosX2.Text) * frmMainInspect.scaleW;
                    y0 = Single.Parse(frmMainInspect.txtPosY2.Text) * frmMainInspect.scaleH;
                    w = Single.Parse(frmMainInspect.txtWidth2.Text) * frmMainInspect.scaleW;
                    h = Single.Parse(frmMainInspect.txtHeight2.Text) * frmMainInspect.scaleH;
                }
                else if (region == 3)
                {
                    p = new Pen(Color.Green);
                    p.Width = 0.2f;
                    RegionFoundBrSave = new string[frmMainInspect.RegionFound3BrSave.Length];
                    RegionFoundBrSave = frmMainInspect.RegionFound3BrSave;
                    RegionFoundPlSave = new string[frmMainInspect.RegionFound3PlSave.Length];
                    RegionFoundPlSave = frmMainInspect.RegionFound3PlSave;
                    x0 = Single.Parse(frmMainInspect.txtPosX3.Text) * frmMainInspect.scaleW;
                    y0 = Single.Parse(frmMainInspect.txtPosY3.Text) * frmMainInspect.scaleH;
                    w = Single.Parse(frmMainInspect.txtWidth3.Text) * frmMainInspect.scaleW;
                    h = Single.Parse(frmMainInspect.txtHeight3.Text) * frmMainInspect.scaleH;
                }

                //NPNP
                //use whole image as ROI don't "Geographic ROI" as the coordinate system's origin
                //if (frmMainInspect.bUseWholeImageAsROI)
                if (frmMainInspect.m_eCognexROIType == frmMain.eCognexROIType.eUseWholeImageAsROI)
                {
                    x0 = 0;
                    y0 = 0;
                    w = frmMainInspect.pictureBoxInspect.Image.Width;
                    h = frmMainInspect.pictureBoxInspect.Image.Height;
                }
                else if (frmMainInspect.m_eCognexROIType == frmMain.eCognexROIType.UseAsBeforeGeographicROI)
                {
                    x0 = 0;
                    y0 = 400;
                    w = frmMainInspect.pictureBoxInspect.Image.Width;
                    h = frmMainInspect.pictureBoxInspect.Image.Height - 800;
                }


                Rectangle re = new Rectangle();
                //Pen p = new Pen(Color.Red);
                string[] ssbr = new string[1];
                string[] sspel = new string[1];
                int a = 0;

                //break
                string[] RegionBr = new string[1];
                if (RejectString == "")
                {
                    RegionBr = new string[RegionFoundBrSave.Length];
                    Array.Copy(RegionFoundBrSave, RegionBr, RegionFoundBrSave.Length);
                }
                else
                {
                    if (RejectString.IndexOf("Break") > 0) RegionBr[0] = RejectString;
                }
                //for (int i = 0; i < RegionFoundBrSave.Length; i++)
                for (int i = 0; i < RegionBr.Length; i++)
                {
                    string s = RegionBr[i];//break


                    if (s != null && int.Parse(s.Substring(0, 2)) == indexnum + 1)
                    {
                        ssbr[a] = s;
                        a++;
                        Array.Resize<string>(ref ssbr, ssbr.Length + 1);
                    }
                }
                //peels
                string[] RegionPl = new string[1];
                if (RejectString == "")
                {
                    RegionPl = new string[RegionFoundPlSave.Length];
                    Array.Copy(RegionFoundPlSave, RegionPl, RegionFoundPlSave.Length);
                }
                else
                {
                    if (RejectString.IndexOf("Peels") > 0) RegionPl[0] = RejectString;
                }
                a = 0;
                for (int i = 0; i < RegionPl.Length; i++)
                {
                    string s = RegionPl[i];//peels

                    if (s != null && int.Parse(s.Substring(0, 2)) == indexnum + 1)
                    {
                        sspel[a] = s;
                        a++;
                        Array.Resize<string>(ref sspel, sspel.Length + 1);
                    }
                }

                Single X0 = 0;
                Single Y0 = 0;
                Single H = 0;
                Single W = 0;

                //Double y010 = (((Single)frmMainInspect.pictureBoxInspect.Height - (Single)frmMainInspect.pictureBoxInspect.Width * ((Single)frmMainInspect.pictureBoxInspect.Image.Height / ((Single)frmMainInspect.pictureBoxInspect.Image.Width))) / 2.0);
                //Double x0 = Single.Parse(frmMainInspect.txtPProiPosX.Text) * frmMainInspect.scaleW;
                //Double y0 = Single.Parse(frmMainInspect.txtPProiPosY.Text) * frmMainInspect.scaleH;
                //Double w = Single.Parse(frmMainInspect.txtPProiWidth.Text) * frmMainInspect.scaleW;
                //Double h = Single.Parse(frmMainInspect.txtPProiHeight.Text) * frmMainInspect.scaleH;


                Double scalew = ((Single)pctSnap.Width / (Single)frmMainInspect.pictureBoxInspect.Width);//scale in picturebox1 by waight
                Double scaleh = (Single)pctSnap.Height / (Single)frmMainInspect.pictureBoxInspect.Height;//scale in picturebox1 by height
                frmMainInspect.scaleHimage = scaleh;
                frmMainInspect.scaleWimage = scalew;//y01->yo coordiate for image in picturebox1
                                                    //(y0-y02)*scalew  ->y0 for rectangle in image in picturebox1 
                                                    //Double x0roi = ((Single)x0 * scalew);
                                                    //Double y0roi = ((Single)y0 * scaleh);
                                                    //Double wroi = scalew * w;
                                                    //Double hroi = h * scaleh;
                Double x0roi = ((Single)x0);
                Double y0roi = ((Single)y0);
                Double wroi = w;
                Double hroi = h;
                if (pctSnap.Image == null) return;
                //re = new Rectangle((int)(x0 * scaleh), (int)(y01 + (y0 - y02) * scalew), (int)((scalew) * w), (int)(h * scaleh));
                Single zoom = (Single)pctSnap.Image.Width / (Single)pctSnap.Image.Height; //1.6f;
                                                                                          //Single pict_zoom = (Single)pctSnap.Width / (Single)pctSnap.Height;
                                                                                          //zoom = zoom / pict_zoom;
                                                                                          //double k_hroi = (pctSnap.Height/2.0f - y0roi)/ (pctSnap.Height / 2.0f);
                                                                                          //Bitmap img = (Bitmap)pctSnap.Image;
                                                                                          //float stretch_X = img.Width / (float)pctSnap.Width;
                                                                                          //float stretch_Y = img.Height / (float)pctSnap.Height;

                ////y0roi = y0roi * zoom * k_hroi;
                //Single offset= pctSnap.Height/2.0f-(pctSnap.Height * (Single)pctSnap.Image.Height / (Single)pctSnap.Image.Width)/2.0f;

                re = new Rectangle((int)(x0roi), (int)(y0roi), (int)(wroi), (int)(hroi));
                p.Width = 0.2f;

                //if (frmMainInspect.chkAutoROI.Checked && region == 1 && PartRectImage.Height != 0 && PartRectImage.Width != 0)
                //{

                //    re = PartRectImage;
                //    //re.X = re.X + 5;
                //    //re.Y = re.Y - 15;
                //    //re.Width = re.Width - 5;
                //    ////re.Height = Math.Abs((int)((re.Height / 2.0f) *3.0f* Math.Sin((3.14f / 180.0f) * (90.0f / (Single)numBufferSize.Value))));
                //    //re.Height = 30 + Math.Abs((int)((re.Height / 2.0f)- (re.Height / 2.0f)*Math.Cos((3.14f / 180.0f) * (360.0f / (Single)numBufferSize.Value))));
                //    //frmMainInspect.PartRect = re;
                //    e.Graphics.DrawRectangle(p, re);
                //}
                e.Graphics.ScaleTransform((Single)scalew, (Single)scaleh);
                //if (!frmMainInspect.chkAutoROI.Checked || region!=1 || PartRectImage.Height == 0 || PartRectImage.Width == 0) 
                e.Graphics.DrawRectangle(p, re);

                if (!chkShoeDefects.Checked) return;
                //return;
                //
                for (int i = 0; i < 2; i++)
                {
                    string[] ss = new string[1];
                    if (i == 0)
                    {
                        ss = new string[1];
                        Array.Resize<string>(ref ss, ssbr.Length);
                        ss = ssbr;
                        //p = new Pen(Color.Red);
                        p.Width = 0.2f;
                    }
                    else if (i == 1)
                    {
                        ss = new string[1];
                        Array.Resize<string>(ref ss, sspel.Length);
                        ss = sspel;
                        p = new Pen(Color.Yellow);
                        p.Width = 0.2f;
                    }

                    bool bManagedToParse = true;
                    for (int j = 0; j < ss.Length - 1; j++)
                    {
                        string[] s = ss[j].Split(' ');
                        X0 = -1; Y0 = -1; H = -1; W = -1;
                        for (int jj = 0; jj < s.Length; jj++)
                        {
                            if (s[jj].IndexOf("X0=") >= 0)
                            {
                                X0 = (int)Single.Parse(s[jj].Substring(3, s[jj].Length - 3));
                            }
                            else if (s[jj].IndexOf("Y0=") >= 0)
                            {
                                Y0 = (int)Single.Parse(s[jj].Substring(3, s[jj].Length - 3));
                            }
                            else if (s[jj].IndexOf("H=") >= 0)
                            {
                                H = (int)Single.Parse(s[jj].Substring(2, s[jj].Length - 2));
                            }
                            if (s[jj].IndexOf("W=") >= 0)
                            {
                                W = (int)Single.Parse(s[jj].Substring(2, s[jj].Length - 2));
                            }
                        }

                        if (pctSnap.Image == null) return;
                        Single scaleX = ((Single)pctSnap.Width / (Single)pctSnap.Image.Width);// *((Single)wroi / (Single)pctSnap.Width);
                        Single scaleY = ((Single)pctSnap.Height / (Single)pctSnap.Image.Height);// * ((Single)hroi / (Single)pctSnap.Height);


                        Double x00 = X0 * frmMainInspect.scaleW; ;// ((Single)X0 * pctSnap.Width / (Single)pctSnap.Image.Width);// ;// * ((Single)pctSnap.Width/(scalew * w));
                        Double y00 = Y0 * frmMainInspect.scaleH; ;//  ((Single)Y0 * pctSnap.Height / (Single)pctSnap.Image.Height);// * ((Single)pctSnap.Height / (scalew * h));
                        //w = (Single)W * wroi / (Single)pctSnap.Image.Width;
                        //h = (Single)H * hroi / (Single)pctSnap.Image.Height;

                        w = (Single)W * frmMainInspect.scaleW; ;// * pctSnap.Width / (Single)pctSnap.Image.Width;
                        h = (Single)H * frmMainInspect.scaleH; ;// * pctSnap.Height / (Single)pctSnap.Image.Height;

                        //p = new Pen(Color.Red);
                        if (w < 1) { w = 1.0f; }// p = new Pen(Color.Orange); }
                        if (h < 1) { h = 1.0f; }// p = new Pen(Color.Orange); }
                        w = w * 5.0f;
                        h = h * 5.0f;
                        p.Width = 0.2f;
                        re = new Rectangle((int)(x00 + x0roi - w / 2), (int)((y00 + y0roi - h / 2)), (int)(w), (int)(h));

                        //if (_eSnapShotStrategy == eSnapShotStrategy.eSnapShotStrategyFullImagesForColorHistogramGeographicROIBasedImagesForTheRest)
                        //{
                        //    var result = CalculateRoiBasedCameraAOI(giPixelTolerance);
                        //    var result2 = CalculateCoordiantesnForZoomedPictureBox(X0, Y0, result.iAOIOffsetX, result.iAOIOffsetY, result.iAOIWidth, result.iAOIHeight);
                        //    X0 = (float)(result2.finalX * frmMainInspect.scaleW);
                        //    Y0 = (float)(result2.finalY * frmMainInspect.scaleH);
                        //    re = new Rectangle((int)(X0 - w / 2), (int)((Y0 - h / 2)), (int)(w), (int)(h));
                        //}
                        //else
                        //if (_eSnapShotStrategy == eSnapShotStrategy.eSnapShotStrategyOnlyFullImages)
                        //{
                        //    var result2 = CalculateCoordiantesnForZoomedPictureBox(X0, Y0, 0, 0, giMaximalCameraWidth, giMaximalCameraHeight);
                        //    X0 = (float)(result2.finalX * frmMainInspect.scaleW);
                        //    Y0 = (float)(result2.finalY * frmMainInspect.scaleH);
                        //    re = new Rectangle((int)(X0 - w / 2), (int)((Y0 - h / 2)), (int)(w), (int)(h));
                        //}
                        bool bDebug = false;
                        if (!bDebug)
                            e.Graphics.DrawRectangle(p, re);
                        else
                        {
                            e.Graphics.FillRectangle(new SolidBrush(Color.Green), re);
                        }
                        //(Single)scalew, (Single)scaleh
                        System.Drawing.Point pt = new System.Drawing.Point((int)((x00 + x0roi - w / 2) * (Single)scalew), (int)((y00 + y0roi - h / 2) * (Single)scaleh));


                        pt.X = pt.X - 100;
                        pt.Y = pt.Y - 100;
                        //panel2.AutoScroll = false;
                        if (chkStretchImage2.Checked)
                        {
                            panel2.AutoScroll = true;
                            panel2.AutoScrollMinSize = new System.Drawing.Size(panel2.Width - 5, panel2.Height - 5);

                            //panel2.AutoScrollPosition = pt;
                        }
                        else
                        {
                            if (chkStretchImage2_unchecked)
                            {
                                pctSnap.SizeMode = PictureBoxSizeMode.AutoSize;
                                panel2.AutoScroll = true;
                                panel2.AutoScrollMinSize = new System.Drawing.Size(pctSnap.Width, pctSnap.Height);
                                if (pt.X != 0 && pt.Y != 0)
                                {
                                    panel2.AutoScrollPosition = pt;
                                }
                                chkStretchImage2_unchecked = false;
                            }
                        }


                        //panel1.AutoScrollPosition = new Point((panel1.AutoScrollMinSize.Width -
                        //                                       panel1.ClientSize.Width) / 2, 0);

                    }


                }
            }
            catch (System.Exception ex) { }

        }

        private void btnAttach_Click(object sender, EventArgs e)
        {
            try
            {
                btnAttach.Enabled = false;
                Attach();

                btnAttach.Enabled = true;
            }
            catch (System.Exception ex) { }
        }

        private void btnDetach_Click(object sender, EventArgs e)
        {
            try {
                btnDetach.Enabled = false;
                Detach();


                btnDetach.Enabled = true;
                //chkAttach.Checked = false;
                IntPtr oldParent = (IntPtr)(0);
            }
            catch (System.Exception ex) { }

        }

        private async void btmInspect_Click(object sender, EventArgs e)
        {
            try
            {
                btmInspect.Enabled = false;
                var task = Task.Run(() => InspectFront());
                await task;
                btmInspect.Enabled = true;
            }
            catch (Exception ex) { btmInspect.Enabled = true; }
        }

        private void btnStopCycle_Click(object sender, EventArgs e)
        {
            bCycle = false;
            bWeldonCycle = false;
            frmMainInspect.StopCycle = true;
            frmMainInspect.txtListBox1Disable = false;
            ButtonsEnabled(true);
            panel1liveTab.Visible = true;
            inv.set(frmMainInspect.txtListBox1, "BackColor", Color.White);
        }

        private void btnShowFront_Click(object sender, EventArgs e)
        {
            try
            {
                tabControl1.SelectedTab = tabControl1.TabPages[2];
                //pctSnapFront.Height = (int)(pctSnapFront.Width * 3648.0f / 5472.0f);
                //3648.0f / 5472.0f image for learning from C:\Project\4.2.2025\InspectSolution\setUpApplication\images
                pctSnapFront.Height = (int)(pctSnapFront.Width * 3032.0f / 5320.0f);

                pctSnapFront.SizeMode = PictureBoxSizeMode.StretchImage;
                pctSnapFront.Image = null;
                int num = 0;

                if (frmBeckhoff.frmMainInspect.pictureBoxInspect1 is null) return;
                pctSnapFront.Image = frmBeckhoff.frmMainInspect.pictureBoxInspect1.Image;
                //frmBeckhoff.frmMainInspect.pictureBoxInspect.Image = pct1.Image;



                //rejected
                indexnum = 0;
                //ImageUpdate = true;
                pctSnapFront.Refresh();
                //frmBeckhoff.frmMainInspect.pictureBoxInspect.Refresh();
            }
            catch (System.Exception ex) { }
        }

        private void pctSnapFront_Paint(object sender, PaintEventArgs e)
        {
            try
            {

                if (frmMainInspect.chkROI11.Checked ) RejectFront_Paint(sender, e, 1);

                return;

            }
            catch (System.Exception ex) { }

        }

        private static void EthernetInf(string ConnectionName, out string ip, out string dns, out string nic)  // To get current ethernet config
        {
            ip = ""; dns = ""; nic = "";
            string[] NwDesc = { "TAP", "VMware", "Windows", "Virtual", "Ethernet" };  // Adapter types (Description) to be ommited //{ "Camera" }
            foreach (NetworkInterface ni in NetworkInterface.GetAllNetworkInterfaces()) {
                if (ni.NetworkInterfaceType == NetworkInterfaceType.Ethernet && !NwDesc.Any(ni.Name.Contains)) {
                    if (ni.Name.Contains(ConnectionName)) { //!NwDesc.Any(ni.Description.Contains)  // check for adapter type and its description
                        foreach (IPAddress dnsAdress in ni.GetIPProperties().DnsAddresses) {
                            if (dnsAdress.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork) {
                                dns = dnsAdress.ToString();
                            }
                        }
                        foreach (UnicastIPAddressInformation ips in ni.GetIPProperties().UnicastAddresses) {
                            if (ips.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork) { // && !ips.Address.ToString().StartsWith("169")) { //to exclude automatic ips
                                ip = ips.Address.ToString();
                                nic = ni.Name;
                            }
                        }
                        //if (dns != "") 
                        break;
                    }
                }
            }
        }

        private void chkShoeDefectsFront_Click(object sender, EventArgs e)
        {
            pctSnapFront.Refresh();
        }

        private void chkShoeDefects_Click(object sender, EventArgs e)
        {
            pctSnap.Refresh();
        }

        private static void WifiInf(out string ip, out string dns, out string nic)  // To get current wifi config
        {
            ip = ""; dns = ""; nic = "";
            foreach (NetworkInterface ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (ni.NetworkInterfaceType == NetworkInterfaceType.Wireless80211)
                {
                    foreach (IPAddress dnsAdress in ni.GetIPProperties().DnsAddresses)
                    {
                        if (dnsAdress.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                        {
                            dns = dnsAdress.ToString();
                        }
                    }
                    foreach (UnicastIPAddressInformation ips in ni.GetIPProperties().UnicastAddresses)
                    {
                        if (ips.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork && !ips.Address.ToString().StartsWith("169"))
                        { //to exclude automatic ips
                            ip = ips.Address.ToString();
                            nic = ni.Name;
                        }
                    }
                }
            }
        }



        private void btnRun_Click(object sender, EventArgs e)
        {
            //attach camFront
            try
            {
                hwndCamFront = FindWindow(null, "Cam2BaslerML");
                if (hwndCamFront == IntPtr.Zero)
                {
                    //Detach();

                    string s = frmMainInspect.FrontPath + @"\ML.bat";// @"C:\Project\Cam2\Cam2BaslerML\Cam2BaslerML\bin\Debug\ML.bat";
                    System.Diagnostics.Process.Start(s);

                }


                Thread.Sleep(100);
                hwndCamFront = (IntPtr)0;
                Attach();
                foreach (Process p in Process.GetProcesses())
                {
                    if (p.ProcessName == "cmd") p.Kill();
                }

            }
            catch (Exception ex) { }
        }
        //----------------------------inspect front-------------------------------
        int RejectBfront = -1;

        private async void btnLoadImageHist_Click(object sender, EventArgs e)
        {
            openFileDialog1.InitialDirectory = aPath + "\\Images"; //"C:\\Users\\DeepL\\Documents\\Visual Studio Projects\\ContourIdentification\\Images";
            openFileDialog1.FileName = txtFileName.Text;
            openFileDialog1.Filter = "Image Files (*.jpeg;*.jpg;*.png;*.gif)|(*.jpeg;*.jpg;*.png;*.gif|JPEG Files (*.jpeg)|*.jpeg|PNG Files (*.png)|*.png|JPG Files (*.jpg)|*.jpg|GIF Files (*.gif)|*.gif";
            DialogResult result = openFileDialog1.ShowDialog();
            if (result == DialogResult.OK || result == DialogResult.Yes)
            {
                var taskFH = Task.Run(() => FileHistogram(openFileDialog1.FileName));
                await taskFH;
                double R = taskFH.Result.R_Gr;
                double G = taskFH.Result.G_Gr;
                double B = taskFH.Result.B_Gr;
                double Gr = taskFH.Result.Gr;
                bool bErr = taskFH.Result.bErr;
                bool bNormal = taskFH.Result.bNormal;
            }
        }

        public async Task<StrHist> FileHistogram(string sFilePath)
        {
            inv.settxt(txtFileNameHist, sFilePath); // System.IO.Path.GetFileName(openFileDialog1.FileName);
                                                    ////ForceUIToUpdate();
                                                    ////using (frmWaitForm frm = new frmWaitForm(System.Action(() => processor.PerformShapeDetection())))
                                                    //using (frmWaitForm frm = new frmWaitForm(Invoke((Action)(() => processor.PerformShapeDetection()) ))
                                                    ////System.Action<> worker => processor.PerformShapeDetection(); 
                                                    ////using (frmWaitForm frm = new frmWaitForm(worker)) {
                                                    //    frm.ShowDialog(this);
                                                    //}
            this.Invoke(new Action(() => txtFileNameHist.Refresh()));

            FileStream fs = new System.IO.FileStream(sFilePath, FileMode.Open, FileAccess.Read);
            Image pic = Image.FromStream(fs);
            fs.Close();

            //Image pic = new Bitmap(sFilePath);
            //Bitmap img = new Bitmap(sFilePath);
            //for (int i = 0; i < img.Width; i++) {
            //    for (int j = 0; j < img.Height; j++) {
            //        Color pixel = img.GetPixel(i,j);
            //    }
            //}
            inv.set(pctSource, "SizeMode", PictureBoxSizeMode.Zoom);
            inv.set(pctSource, "Image", pic);
            inv.set(pctHist, "SizeMode", PictureBoxSizeMode.Zoom);
            inv.set(pctHist, "Image", null);
            inv.set(pctHistD, "SizeMode", PictureBoxSizeMode.Zoom);
            inv.set(pctHistD, "Image", null);

            var taskF = Task.Run(() => CalcHistogram());
            await taskF;
            return taskF.Result;
        }

        private async void btnDetectHist_Click(object sender, EventArgs e)
        {
            var taskH = Task.Run(() => CalcHistogram());
            await taskH;
            double RGr = taskH.Result.R_Gr;
            double GGr = taskH.Result.G_Gr;
            double BGr = taskH.Result.B_Gr;
            double Gr = taskH.Result.Gr;
            bool bErr = taskH.Result.bErr;
            bool bNormal = taskH.Result.bNormal;
        }

        public  StrHist CalcHistogram()
        {
            StrHist sh = new StrHist();

            sh.R_Gr = 0; sh.G_Gr = 0; sh.B_Gr = 0; sh.Gr = 0; sh.bErr = false; sh.bNormal = false;
            inv.settxt(lblR, ""); inv.settxt(lblG, ""); inv.settxt(lblB, "");
            inv.settxt(lblRGr, ""); inv.settxt(lblGGr, ""); inv.settxt(lblBGr, "");
            pctHist.Image = null;
            pctHistD.Image = null;
            inv.set(lblColorDefect, "Visible", false);
            inv.set(lblColorNormal, "Visible", false);

            if (pctSource.Image == null)
            {
                sh.bErr = true;
                return sh;
            }

            System.Windows.Forms.Cursor curs = this.Cursor;
            if (InvokeRequired)
                this.Invoke(new Action(() => this.Cursor = Cursors.WaitCursor));
            else
                this.Cursor = Cursors.WaitCursor;

            inv.set(btnDetectHist, "Enabled", false);

            Stopwatch watch = Stopwatch.StartNew();
            watch.Reset(); watch.Start();

            Bitmap imSource = null;
            this.Invoke(new Action(() => imSource = new Bitmap(pctSource.Image)));

            float[] BlueHist = new float[256];
            float[] GreenHist = new float[256];
            float[] RedHist = new float[256];
            float[] GrayHist = new float[256];

            Image<Bgr, Byte> img = imSource.ToImage<Bgr, byte>();

            Image<Gray, Byte> img2Blue = img[0];
            Image<Gray, Byte> img2Green = img[1];
            Image<Gray, Byte> img2Red = img[2];

            bool bRed = Convert.ToBoolean(inv.get(chkRed, "Checked"));

            bool bShowH = Convert.ToBoolean(inv.get(chkShowHist, "Checked"));

            float fColorThreshold = Convert.ToSingle(inv.gettxt(txtColorThreshold));

            int nMin = Convert.ToInt16(inv.gettxt(txtMinBrDiff));
            if (nMin < 0 || nMin > 255) nMin = 0;

            int nMax = Convert.ToInt16(inv.gettxt(txtMaxBrDiff)); //64; //255
            if (nMax < 0 || nMax > 255 || nMax < nMin) nMax = 255;

            float[] x = new float[nMax + 1 - nMin];
            float[] BlueHist1 = new float[nMax + 1 - nMin];
            float[] GreenHist1 = new float[nMax + 1 - nMin];
            float[] RedHist1 = new float[nMax + 1 - nMin];

            int nGrMin = Convert.ToInt16(txtMinBr.Text);
            if (nGrMin < 0 || nGrMin > 255) nGrMin = 0;

            if (bShowH)
            {
                DenseHistogram Histo = new DenseHistogram(255, new RangeF(0, 255));

                Histo.Calculate(new Image<Gray, Byte>[] { img2Blue }, true, null);
                //The data is here
                //Histo.MatND.ManagedArray
                BlueHist = new float[256];
                Histo.CopyTo(BlueHist); //Histo.MatND.ManagedArray.CopyTo(BlueHist, 0);
                Histo.Clear();

                Histo.Calculate(new Image<Gray, Byte>[] { img2Green }, true, null);
                GreenHist = new float[256];
                Histo.CopyTo(GreenHist); //Histo.MatND.ManagedArray.CopyTo(GreenHist, 0);
                Histo.Clear();

                Histo.Calculate(new Image<Gray, Byte>[] { img2Red }, true, null);
                RedHist = new float[256];
                Histo.CopyTo(RedHist); //Histo.MatND.ManagedArray.CopyTo(RedHist, 0);
                Histo.Clear();

                float[] xx = new float[256];

                PlotSplineSolution("Histogram", xx, RedHist, GreenHist, BlueHist, pctHist, 0, 255);
            }

            Image<Gray, Byte> grayImage = img.Convert<Gray, Byte>();
            Image<Gray, Byte> img2DifR = new Image<Gray, Byte>(img[0].Width, img[0].Height);
            Image<Gray, Byte> img2DifG = new Image<Gray, Byte>(img[0].Width, img[0].Height);
            Image<Gray, Byte> img2DifB = new Image<Gray, Byte>(img[0].Width, img[0].Height);

            //img2DifBlueRed = img2Blue.Cmp(img2Red, CmpType.GreaterThan);
            CvInvoke.AbsDiff(img2Red, grayImage, img2DifR);
            CvInvoke.AbsDiff(img2Green, grayImage, img2DifG);
            CvInvoke.AbsDiff(img2Blue, grayImage, img2DifB);

            //grayImage.Save(@"C:\\Users\\igors\\Documents\\Visual Studio Projects\\BeckhoffBasler\\BeckhoffBasler\\BeckhoffBasler\\bin\\Debug\\Images\\gray.jpg");
            //img2DifR.Save(@"C:\\Users\\igors\\Documents\\Visual Studio Projects\\BeckhoffBasler\\BeckhoffBasler\\BeckhoffBasler\\bin\\Debug\\Images\\dR.jpg");
            //img2DifG.Save(@"C:\\Users\\igors\\Documents\\Visual Studio Projects\\BeckhoffBasler\\BeckhoffBasler\\BeckhoffBasler\\bin\\Debug\\Images\\dG.jpg");
            //img2DifB.Save(@"C:\\Users\\igors\\Documents\\Visual Studio Projects\\BeckhoffBasler\\BeckhoffBasler\\BeckhoffBasler\\bin\\Debug\\Images\\dB.jpg");
            //img2Red.Save(@"C:\\Users\\igors\\Documents\\Visual Studio Projects\\BeckhoffBasler\\BeckhoffBasler\\BeckhoffBasler\\bin\\Debug\\Images\\R.jpg");
            //img2Green.Save(@"C:\\Users\\igors\\Documents\\Visual Studio Projects\\BeckhoffBasler\\BeckhoffBasler\\BeckhoffBasler\\bin\\Debug\\Images\\G.jpg");
            //img2Blue.Save(@"C:\\Users\\igors\\Documents\\Visual Studio Projects\\BeckhoffBasler\\BeckhoffBasler\\BeckhoffBasler\\bin\\Debug\\Images\\B.jpg");

            DenseHistogram HistoD = new DenseHistogram(255, new RangeF(0, 255));
            HistoD.Calculate(new Image<Gray, Byte>[] { img2DifR }, true, null);
            HistoD.CopyTo(RedHist);
            HistoD.Clear();
            HistoD.Calculate(new Image<Gray, Byte>[] { img2DifG }, true, null);
            HistoD.CopyTo(GreenHist);
            HistoD.Clear();
            HistoD.Calculate(new Image<Gray, Byte>[] { img2DifB }, true, null);
            HistoD.CopyTo(BlueHist);
            HistoD.Clear();
            HistoD.Calculate(new Image<Gray, Byte>[] { grayImage }, true, null);
            HistoD.CopyTo(GrayHist);
            HistoD.Clear();

            for (int i = nMin; i <= nMax; i++)
            {
                x[i - nMin] = i;
                RedHist1[i - nMin] = RedHist[i];
                if (bRed) { GreenHist1[i - nMin] = RedHist[i]; BlueHist1[i - nMin] = RedHist[i]; }
                else { GreenHist1[i - nMin] = GreenHist[i]; BlueHist1[i - nMin] = BlueHist[i]; }
                sh.R_Gr += RedHist[i];
                sh.G_Gr += GreenHist[i];
                sh.B_Gr += BlueHist[i];
            }
            PlotSplineSolution("Histogram Red-Gray, Green-Gray, Blue-Gray", x, RedHist1, GreenHist1, BlueHist1, pctHistD, 0, nMax - nMin);

            for (int i = nGrMin; i <= 255; i++)
            {
                sh.Gr += GrayHist[i];
            }

            //"Cubic Spline Interpolation - Parametric Fit"
            //for (int ii = 0; ii < pixarr.Length; ii++) {
            //    x[ii] = ii;
            //    y[ii] = dpixarr[ii];
            //}
            //CubicSpline.FitParametric(x, y, SliceWidth, out xs, out ys); // , 1, -1, 1, -1);
            //Form1.mFormDefInstance.pctGrad2.Invoke((Action)(() => {
            //    PlotSplineSolution("Gradient", x, y, xs, ys, Form1.mFormDefInstance.pctGrad2, inddpixmin, inddpixmax);
            //}));

            /*
            using (Mat hsv = new Mat())
            {
                CvInvoke.CvtColor(img, hsv, ColorConversion.Bgr2Hsv);
                Mat[] channels = hsv.Split();

                RangeF H = channels[0].GetValueRange();
                RangeF S = channels[1].GetValueRange();
                RangeF V = channels[2].GetValueRange();

                Console.WriteLine(string.Format("Max H {0} Min H {1}", H.Max, H.Min));
                Console.WriteLine(string.Format("Max S {0} Min S {1}", S.Max, S.Min));
                Console.WriteLine(string.Format("Max V {0} Min V {1}", V.Max, V.Min));

                MCvScalar mean = CvInvoke.Mean(hsv);
                Console.WriteLine(string.Format("Mean H {0} Mean S {1} Mean V {2} ", mean.V0, mean.V1, mean.V2)); ;
            }
            */

            watch.Stop();
            inv.settxt(txtHistTime, watch.ElapsedMilliseconds.ToString());
            this.Invoke(new Action(() => txtHistTime.Refresh()));

            inv.settxt(lblR, sh.R_Gr.ToString());
            inv.settxt(lblG, sh.G_Gr.ToString());
            inv.settxt(lblB, sh.B_Gr.ToString());

            inv.settxt(lblRGr, String.Format("{0:f5}", (sh.R_Gr / sh.Gr)));
            inv.settxt(lblGGr, String.Format("{0:f5}", (sh.G_Gr / sh.Gr)));
            inv.settxt(lblBGr, String.Format("{0:f5}", (sh.B_Gr / sh.Gr)));

            if (sh.R_Gr / sh.Gr > fColorThreshold)
            {
                sh.bNormal = false;
                inv.set(lblColorDefect, "Visible", true);
            }
            else
            {
                sh.bNormal = true;
                inv.set(lblColorNormal, "Visible", true);
            }

            inv.set(btnDetectHist, "Enabled", true);
            this.Invoke(new Action(() => this.Cursor = curs));

            sh.bErr = false;
            return sh;
        }



        private static Series CreateSeries(
            Chart chart, string seriesName, IEnumerable<DataPoint> points, Color color, MarkerStyle markerStyle = MarkerStyle.None)
        {
            var s = new Series()
            {
                XValueType = ChartValueType.Double,
                YValueType = ChartValueType.Double,
                Legend = chart.Legends[0].Name,
                IsVisibleInLegend = true,
                ChartType = SeriesChartType.Line,
                Name = seriesName,
                ChartArea = chart.ChartAreas[0].Name,
                MarkerStyle = markerStyle,
                Color = color,
                MarkerSize = 8
            };

            foreach (var p in points)
            {
                s.Points.Add(p);
            }
            return s;
        }

        private static List<DataPoint> CreateDataPoints(float[] x, float[] y)
        {
            Debug.Assert(x.Length == y.Length);
            List<DataPoint> points = new List<DataPoint>();

            for (int i = 0; i < x.Length; i++)
            {
                points.Add(new DataPoint(x[i], y[i]));
            }
            return points;
        }

        public static void PlotSplineSolution(string title, float[] x, float[] yr, float[] yg, float[] yb,
            //float[] xs, float[] ys,
            System.Windows.Forms.PictureBox pb, int ind1, int ind2, float[] qPrime = null,
            bool bShowSpline = false, bool bDoubleSize = false)
        {
            try
            {
                var chart = new Chart();

                chart.Size = new System.Drawing.Size(704, 400); //600,400
                if (bDoubleSize) chart.Size = new System.Drawing.Size(1200, 800);
                chart.Titles.Add(title);
                chart.Legends.Add(new Legend("Legend"));

                ChartArea ca = new ChartArea("DefaultChartArea");
                ca.AxisX.Title = "X";
                ca.AxisY.Title = "Y";
                ca.AxisX.LabelStyle.Format = "{0}"; //"{0.00}"

                chart.ChartAreas.Add(ca);

                //Series s1 = null;
                //if (bShowSpline) {
                //    s1 = CreateSeries(chart, "Spline", CreateDataPoints(xs, ys), Color.Green, MarkerStyle.None);
                //}
                Series s1 = CreateSeries(chart, "R", CreateDataPoints(x, yr), Color.Red, MarkerStyle.None); //MarkerStyle.Diamond
                Series s2 = CreateSeries(chart, "G", CreateDataPoints(x, yg), Color.Green, MarkerStyle.None);
                Series s3 = CreateSeries(chart, "B", CreateDataPoints(x, yb), Color.Blue, MarkerStyle.None);

                s1.Points[ind1].Color = Color.Red;
                s1.Points[ind2].Color = Color.Red;
                chart.Series.Add(s1);

                s2.Points[ind1].Color = Color.Green;
                s2.Points[ind2].Color = Color.Green;
                chart.Series.Add(s2);

                s3.Points[ind1].Color = Color.Blue;
                s3.Points[ind2].Color = Color.Blue;
                chart.Series.Add(s3);

                for (int i = 0; i <= 2; i++)
                {
                    chart.Series[i].IsVisibleInLegend = false;
                }
                chart.ChartAreas[0].AxisX.Title = "";
                chart.ChartAreas[0].AxisY.Title = "";

                //if (bShowSpline) {
                //    chart.Series.Add(s1);
                //    chart.Series[1].IsVisibleInLegend = false;
                //}

                //if (qPrime != null) {
                //    Series s3 = CreateSeries(chart, "Slope", CreateDataPoints(xs, qPrime), Color.Red, MarkerStyle.None);
                //    chart.Series.Add(s3);
                //}

                ca.RecalculateAxesScale();
                ca.AxisX.Minimum = Math.Floor(ca.AxisX.Minimum);
                ca.AxisX.Maximum = ca.AxisX.Minimum + ind2;// Math.Ceiling(ca.AxisX.Maximum);
                int nIntervals = (x.Length - 1);
                nIntervals = Math.Max(4, nIntervals);
                ca.AxisX.Interval = Math.Max((ca.AxisX.Maximum - ca.AxisX.Minimum) / nIntervals, 10);

                // Save
                //if (File.Exists(path)) {
                //    File.Delete(path);
                //}

                //using (FileStream fs = new FileStream(path, FileMode.CreateNew)) {
                //    chart.SaveImage(fs, ChartImageFormat.Png);
                //}

                using (var chartimage = new MemoryStream())
                {
                    chart.SaveImage(chartimage, ChartImageFormat.Png);
                    pb.Image = System.Drawing.Image.FromStream(chartimage); // chartimage.GetBuffer();
                }
            }
            catch (Exception ex) { }
        }

        private void lblStartPosition_Click(object sender, EventArgs e)
        {

        }

        private void label15_Click(object sender, EventArgs e)
        {

        }
        bool FooterStationActAxisInAction = false;
        private async void btnHome_Click(object sender, EventArgs e)
        {
            try
            {
                if (FooterStationActAxisInAction) return;
                AxisMove = false;
                FooterStationActAxisInAction = true;
                btnHome.Enabled = false;
                btnWork.Enabled = false;
                //SetTraficLights(0, 0, 0, 0);//yellow/green
                int axis = 0;
                Speed = 1000;
                Single speed = Speed * Single.Parse(txtSpeedSt.Text) / 100;

                var task = Task.Run(() => MoveFooterHome(axis, 1000, 1000));
                await task;
                if (!task.Result.result)
                {

                    MessageBox.Show("FOOTER HOME ERROR!", "ERROR", MessageBoxButtons.OK,
                                    MessageBoxIcon.Warning, MessageBoxDefaultButton.Button1, MessageBoxOptions.DefaultDesktopOnly);
                    FooterStationActAxisInAction = false;

                }
                Thread.Sleep(200);

                FooterStationActAxisInAction = false;
                btnHome.Enabled = true;
                btnWork.Enabled = true;
            }
            catch (Exception ex)
            {
                //SetTraficLights(0, 0, 1, 0);//red ight
                MessageBox.Show("ERROR IN PART DATA!", "ERROR", MessageBoxButtons.OK,
                                    MessageBoxIcon.Warning, MessageBoxDefaultButton.Button1, MessageBoxOptions.DefaultDesktopOnly);
                FooterStationActAxisInAction = false;
                btnHome.Enabled = true;
                btnWork.Enabled = true;
            }
        }

        private async void btnWork_Click(object sender, EventArgs e)
        {
            try
            {
                if (FooterStationActAxisInAction) return;
                FooterStationActAxisInAction = true;
                AxisMove = false;
                btnHome.Enabled = false;
                btnWork.Enabled = false;
                //SetTraficLights(0, 0, 0, 0);//
                int axis = 0;
                Speed = 1000;
                Single speed = Speed * Single.Parse(txtSpeedSt.Text) / 100;


                Single Pos5 = 57;
                Single Pos1 = -31.7f;
                Single Pos2 = 0.13f;
                Single Pos4 = 0;
                Single Pos3 = -15;

                var task = Task.Run(() => MoveFooterWork(axis, Pos5, 500, Pos1, Pos2, Pos4, speed, Pos3, 1));
                await task;
                if (!task.Result.result)
                {
                    //SetTraficLights(0, 0, 1, 0);//red ight
                    MessageBox.Show("FOOTER WORK ERROR!", "ERROR", MessageBoxButtons.OK,
                                    MessageBoxIcon.Warning, MessageBoxDefaultButton.Button1, MessageBoxOptions.DefaultDesktopOnly);
                    FooterStationActAxisInAction = false;
                    btnHome.Enabled = true;
                    btnWork.Enabled = true;

                }
                Thread.Sleep(200);
                //SetTraficLights(0, 1, 0, 0);//yellow/green
                FooterStationActAxisInAction = false;
                btnHome.Enabled = true;
                btnWork.Enabled = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show("ERROR IN PART DATA!", "ERROR", MessageBoxButtons.OK,
                                    MessageBoxIcon.Warning, MessageBoxDefaultButton.Button1, MessageBoxOptions.DefaultDesktopOnly);
                FooterStationActAxisInAction = false;
                btnHome.Enabled = true;
                btnWork.Enabled = true;
            }
        }
        private async Task<CommReply> MoveFooterWork(int axis, Single Pos5, Single speed, Single Pos1, Single Pos2, Single Pos4, Single speed1, Single Pos3, int lamps)//current position
        {
            try
            {
                CommReply reply = new CommReply();
                Beckhoff.SendPlcParms ParmsPlc = new Beckhoff.SendPlcParms();
                reply.result = false;

                //if (!LoadPlcData()) return reply;


                Array.Resize<Single>(ref ParmsPlc.SendParm, 11);
                int j = 0;
                ParmsPlc.SendParm[0] = 430;
                ParmsPlc.SendParm[1] = axis;
                ParmsPlc.SendParm[2] = Pos5;
                ParmsPlc.SendParm[3] = speed;
                ParmsPlc.SendParm[4] = Pos1;
                ParmsPlc.SendParm[5] = Pos2;
                ParmsPlc.SendParm[6] = Pos4;
                ParmsPlc.SendParm[7] = speed1;
                ParmsPlc.SendParm[8] = Pos3;
                ParmsPlc.SendParm[9] = lamps;
                ParmsPlc.SendParm[10] = 9.5f;//tmout
                int StartAddressSendGen_1 = 310;

                var task1 = Task.Run(() => Beckhoff_Gen.PlcSendCmd(StartAddressSendGen_1, ParmsPlc, true));
                await task1;
                reply = task1.Result;
                ParmsPlc.SendParm = null;

                if (!reply.result) return reply; ;
                Thread.Sleep(2);

                reply.result = true;
                return reply;

            }
            catch (Exception ex)
            {
                CommReply reply = new CommReply();
                reply.result = false;
                reply.comment = ex.Message;
                return reply;

            }
        }
        private async Task<CommReply> MoveFooterHome(int axis, Single speed5, Single speed4)
        {
            try
            {

                CommReply reply = new CommReply();
                Beckhoff.SendPlcParms ParmsPlc = new Beckhoff.SendPlcParms();
                reply.result = false;

                int StartAddressSendGen_1 = 310;


                Array.Resize<Single>(ref ParmsPlc.SendParm, 11);
                int j = 0;
                ParmsPlc.SendParm[0] = 429;
                ParmsPlc.SendParm[1] = axis;
                ParmsPlc.SendParm[3] = speed5;
                ParmsPlc.SendParm[4] = speed4;

                ParmsPlc.SendParm[10] = 15.0f;//tmout

                var task = Task.Run(() => Beckhoff_Gen.PlcSendCmd(StartAddressSendGen_1, ParmsPlc, true, false));
                await task;
                reply = task.Result;

                ParmsPlc.SendParm = null;

                if (!reply.result)
                {
                    return reply; ;

                }

                reply.result = true;
                //txtMess.Text = txtMess.Text + "fini"+"\r\n";
                return reply;

            }
            catch (Exception ex)
            {
                CommReply reply = new CommReply();
                reply.result = false;
                reply.comment = ex.Message;
                return reply;

            }
        }

        private void btnAddWindow_Click(object sender, EventArgs e)
        {
            AddWindow();
        }

        private void tabControl1_SelectedIndexChanged(object sender, EventArgs e)
        {
            try
            {
                //TabPage current = (sender as TabControl).SelectedTab;
                //if(current.Name=="tabPage10")
                if (tabControl1.SelectedTab == tabControl1.TabPages[3])
                {
                    AddWindow();
                }
                else if (tabControl1.SelectedTab == tabControl1.TabPages[1] || tabControl1.SelectedTab == tabControl1.TabPages[0])
                {
                    SetWindowPos(hwndCamFront, (IntPtr)HWND_NOTOPMOST, 0, 0, 0, 0, (int)TOPMOST_FLAGS);
                    this.Focus();
                }
                else if (tabControl1.SelectedTab == tabControl1.TabPages[2])
                {
                    SetWindowPos(hwndCamFront, (IntPtr)HWND_NOTOPMOST, 0, 0, 0, 0, (int)TOPMOST_FLAGS);
                    this.Focus();
                    chkStretchFront.Checked = true;
                    btnShowFront_Click(sender, e);
                }
                else 
                {
                    SetWindowPos(hwndCamFront, (IntPtr)HWND_NOTOPMOST, 0, 0, 0, 0, (int)TOPMOST_FLAGS);
                    this.Focus();
                    
                }
            }
            catch (Exception ex) { }
        }

        private void btnStop2_Click(object sender, EventArgs e)
        {
            try
            {
                bStop = true;
                WC1.Stop();
                WC2.Stop();
                inv.set(btnSatrt2, "Enabled", true);
            }
            catch (Exception ex) { }
        }

        private void btnSatrt2_Click_1(object sender, EventArgs e)
        {
            try
            {

                bStop = false;
                inv.set(btnSatrt2, "Enabled", false);
                var task = Task.Run(() => RunWebComm());//vision
                //await task;
                var task1 = Task.Run(() => RunWebComm1());//cognex
                                                          //await task1;

                inv.set(btnSatrt2, "Enabled", true);


            }
            catch (System.Exception)
            {

                throw;
            }
        }

        private void txtServer_DoubleClick_1(object sender, EventArgs e)
        {
            inv.settxt(txtServer, "");
        }

        private async void btnStartCycleSnap_Click(object sender, EventArgs e)
        {
            try
            {
                Stopwatch stopwatch = new Stopwatch();
                stopwatch.Restart();
                btn_status(false);
                ButtonsEnabled(false);
                panel1liveTab.Visible = true;
                RejectB = -1;
                RejectP = -1;
                //this.Invoke((Action)(() => { frmMainInspect.listBox1.Items.Clear(); }));
                bCycle = false;
                bWeldonCycle = false;
                frmMainInspect.StopCycle = true;
                Thread.Sleep(200);
                frmMainInspect.StopCycle = false;
                var task = Task.Run(() => CycleVision());
                //var task1 = Task.Run(() => CycleInspect());
                //var task3 = Task.Run(() => frmMainInspect.ImageCycle());//save snaps to images array

                await task;
                //inv.settxt(txtCycleTime, String.Format("0.0", stopwatch.ElapsedMilliseconds / 1000.0f));
                inv.settxt(txtCycleTime, (stopwatch.ElapsedMilliseconds / 1000.0f).ToString("0.000"));
                //await inspect;
                //var task2 = Task.Run(() => WaitCycleInspectFini());
                //await task2;

                stopwatch.Stop();
                //inv.settxt(frmBeckhoff.mFormBeckhoffDefInstance.txtCycleTime, String.Format("0.0", stopwatch.ElapsedMilliseconds/1000));
                btn_status(true);
                ButtonsEnabled(true);
                panel1liveTab.Visible = true;
            }
            catch (Exception ex)
            {
                panel1liveTab.Visible = true;
            }
        }
        //public string lstStr = "";
        private void btnSaveRejects_Click(object sender, EventArgs e)
        {
            btnSaveRejects.Enabled = false;
            SaveRejectsTop();
            SaveRejectsFront();
            btnSaveRejects.Enabled = true;
        }
        string timestep = "";
        private  bool  SaveRejectsTop()
        {
            try
            {
                //top
                string[] ss = new string[1];
                //for (int i = 0; i < frmMainInspect.listBox1.Items.Count; i++)
                //{
                //    string s = frmMainInspect.listBox1.Items[i].ToString();
                //    if (s.IndexOf("Break:") > 0 || s.IndexOf("Peel:") > 0) { ss[ss.Length - 1] = s; Array.Resize<String>(ref ss, ss.Length + 1); }
                //}
                timestep = DateTime.Now.ToString("yy-MM-dd HH-mm-ss");
                string path = "C:\\Rejects\\" + txtOrder.Text.Trim() + "_" + txtItem.Text.Trim() + "\\" + txtPartNum.Text.Trim() + " " + timestep;
                if (!Directory.Exists(path))
                {
                    Directory.CreateDirectory(path);
                }
                using (FileStream file = new FileStream(path + "\\data" + ".txt", FileMode.Create, FileAccess.Write, FileShare.Inheritable))
                {
                    StreamWriter swr = new StreamWriter(file);
                    

                    //for (int i = 0; i < frmMainInspect.listBox1.Items.Count; i++)
                    //{
                    //    string s = frmMainInspect.listBox1.Items[i].ToString();
                        swr.WriteLine(frmMainInspect.txtListBox1.Text);
                    //}
                    swr.Close();
                }
                //copy images
                frmMainInspect.numBufferSize = (int)numBufferSize.Value;
                for (int i = 0; i < frmMainInspect.numBufferSize; i++)
                {
                    string fname = aPath + "\\Images\\snap" + (i + 1).ToString() + ".jpg";
                    string fcopy = path + "\\snap" + (i + 1).ToString() + ".jpg";
                    File.Copy(fname, fcopy, true);
                }
                              

                File.Copy(frmMainInspect.sJassonPath, path + "\\EndmillsData.Jason", true);
                //static public string JassonPath = @"C:\Project\4.2.2025\InspectSolution\setUpApplication\projSampaleViewer\bin\x64\Debug\Data\DataBase\EndmillsData.Jason";
                return true;

            }

            catch (Exception ex) { return false; }

        }
        private  bool  SaveRejectsFront()
        {
            try
            {

                string path = "C:\\Rejects\\" + txtOrder.Text.Trim() + "_" + txtItem.Text.Trim() + "\\" + txtPartNum.Text.Trim() + " " + timestep; //DateTime.Now.ToString("yy-MM-dd HH-mm-ss");

                //front
                string[] ss = new string[1];
                for (int i = 0; i < frmMainInspect.listBox11.Items.Count; i++)
                {
                    string s = frmMainInspect.listBox11.Items[i].ToString();
                    if (s.IndexOf("Break:") > 0 || s.IndexOf("Peel:") > 0) { ss[ss.Length - 1] = s; Array.Resize<String>(ref ss, ss.Length + 1); }
                }
                //path = "C:\\Rejects\\" + txtOrder.Text.Trim() + "_" + txtItem.Text.Trim() + "\\" + txtPartNum.Text.Trim() + " " + DateTime.Now.ToString("yy-MM-dd HH-mm-ss");
               
                if (!Directory.Exists(path))
                {
                    Directory.CreateDirectory(path);
                }
                using (FileStream file = new FileStream(path + "\\dataFront" + ".txt", FileMode.Create, FileAccess.Write, FileShare.Inheritable))
                {
                    StreamWriter swr = new StreamWriter(file);


                    for (int i = 0; i < frmMainInspect.listBox11.Items.Count; i++)
                    {
                        string s = frmMainInspect.listBox11.Items[i].ToString();
                        swr.WriteLine(s);
                    }
                    swr.Close();
                }
                //copy images
                
                string fname1 = frmMainInspect.FrontPath + @"\Images\snap.jpg";// "C:\\Project\\Cam2\\Cam2BaslerML\\Cam2BaslerML\\bin\\Debug\\Images\\snap.jpg";
                string fcopy1 = path + "\\snap-count.jpg";
                File.Copy(fname1, fcopy1, true);
                fname1 = frmMainInspect.FrontPath + @"\Images\snap-inspect.jpg";//"C:\\Project\\Cam2\\Cam2BaslerML\\Cam2BaslerML\\bin\\Debug\\Images\\snap-inspect.jpg";
                fcopy1 = path + "\\snap-inspect.jpg";
                File.Copy(fname1, fcopy1, true);
                return true;

            }

            catch (Exception ex) { return false; }

        }
        private void LoadRejects()
        {
            try
            {
                Invoke((Action)(() =>
                {
                    frmMainInspect.LoadedBackup = true;
                    string path = "";
                    //clear current rejects
                    for (int i = 0; i < frmMainInspect.RegionFound1BrSave.Length; i++) frmMainInspect.RegionFound1BrSave[i] = "";//breaks
                    for (int i = 0; i < frmMainInspect.RegionFound1PlSave.Length; i++) frmMainInspect.RegionFound1PlSave[i] = "";//peels
                                                                                                                                 //
                    if (cmbDir.Text.Trim() != "" && lstSavedParts.SelectedItem.ToString() != "")
                    {
                        path = "C:\\Rejects\\" + cmbDir.Text.Trim() + "\\" + lstSavedParts.SelectedItem.ToString();
                    }
                    else
                    {
                        using (var fbd = new FolderBrowserDialog())
                        {

                            fbd.SelectedPath = "C:\\Rejects";
                            DialogResult result = fbd.ShowDialog();

                            if (result == DialogResult.OK && !string.IsNullOrWhiteSpace(fbd.SelectedPath))
                            {
                                string[] files = Directory.GetFiles(fbd.SelectedPath);
                                path = fbd.SelectedPath;
                                //System.Windows.Forms.MessageBox.Show("Files found: " + files.Length.ToString(), "Message");
                            }
                            else return;
                        }
                    }
                    //top
                    if (File.Exists(path + "\\data.txt"))
                    {
                        File.Copy(path + "\\EndmillsData.Jason", frmMainInspect.sJassonPath, true);
                        //frmMainInspect.listBox1.Items.Clear();
                        inv.settxt(frmMainInspect.txtListBox1, "");
                        frmMainInspect.lstStr = "";
                        foreach (string line in File.ReadLines(path + "\\data.txt"))
                        {
                            // Printing the file contents

                            string[] str = line.Split('\r');
                            for (int i = 0; i < str.Length; i++)
                            {
                                frmMainInspect.lstStr = frmMainInspect.lstStr + line + '\r';
                                frmMainInspect.txtListBox1.AppendText(line);
                                frmMainInspect.txtListBox1.AppendText(Environment.NewLine);
                            }

                            if (line.IndexOf("numBufferSize:") >= 0) numBufferSize.Value = int.Parse(line.Substring(14, line.Length - 14));
                            if (line.IndexOf("Inspect Catalog:") >= 0)
                            {
                                frmMainInspect.CmbCatNum.Text = line.Substring(16, line.Length - 16);
                            }
                        }
                        //frmMainInspect.txtListBox1.Text = frmMainInspect.lstStr;
                        //inv.settxt(frmMainInspect.txtListBox1, frmMainInspect.lstStr);

                    }
                    else
                    {
                        MessageBox.Show("NO DATA FOR TOP INSPECTION! ", "ERROR", MessageBoxButtons.OK,
                                            MessageBoxIcon.Warning, MessageBoxDefaultButton.Button1, MessageBoxOptions.DefaultDesktopOnly);
                        return;
                    }
                    //front
                    if (File.Exists(path + "\\dataFront.txt"))
                    {
                        //File.Copy(path + "\\EndmillsData.Jason", frmMainInspect.sJassonPath, true);
                        frmMainInspect.listBox11.Items.Clear();
                        foreach (string line in File.ReadLines(path + "\\dataFront.txt"))
                        {
                            // Printing the file contents
                            frmMainInspect.listBox11.Items.Add(line);
                            //if (line.IndexOf("numBufferSize:") >= 0) numBufferSize.Value = int.Parse(line.Substring(14, line.Length - 14));
                            if (line.IndexOf("Inspect Catalog:") >= 0) frmMainInspect.CmbCatNum.Text = line.Substring(16, line.Length - 16);
                        }
                    }
                    else
                    {
                        MessageBox.Show("NO FRONT DATA! ", "ERROR", MessageBoxButtons.OK,
                                            MessageBoxIcon.Warning, MessageBoxDefaultButton.Button1, MessageBoxOptions.DefaultDesktopOnly);
                        //return;
                    }
                    //
                    frmMainInspect.CmbCatNum_SelectedIndexChanged(null, null);
                    frmMainInspect.CmbCatNum_SelectedValueChanged(null, null);
                    //copy images top
                    PreparePictureArray();
                    for (int i = 0; i < numBufferSize.Value; i++)
                    {
                        if (File.Exists(path + "\\snap" + (i + 1).ToString() + ".jpg"))
                        {
                            switch (i)
                            {
                                case 0: pct1.Image = Image.FromFile(path + "\\snap" + (i + 1).ToString() + ".jpg");
                                    break;
                                case 1:
                                    pct2.Image = Image.FromFile(path + "\\snap" + (i + 1).ToString() + ".jpg");
                                    break;
                                case 2:
                                    pct3.Image = Image.FromFile(path + "\\snap" + (i + 1).ToString() + ".jpg");
                                    break;
                                case 3:
                                    pct4.Image = Image.FromFile(path + "\\snap" + (i + 1).ToString() + ".jpg");
                                    break;
                                case 4:
                                    pct5.Image = Image.FromFile(path + "\\snap" + (i + 1).ToString() + ".jpg");
                                    break;
                                case 5:
                                    pct6.Image = Image.FromFile(path + "\\snap" + (i + 1).ToString() + ".jpg");
                                    break;
                                case 6:
                                    pct7.Image = Image.FromFile(path + "\\snap" + (i + 1).ToString() + ".jpg");
                                    break;
                                case 7:
                                    pct8.Image = Image.FromFile(path + "\\snap" + (i + 1).ToString() + ".jpg");
                                    break;
                                case 8:
                                    pct9.Image = Image.FromFile(path + "\\snap" + (i + 1).ToString() + ".jpg");
                                    break;
                                case 9:
                                    pct10.Image = Image.FromFile(path + "\\snap" + (i + 1).ToString() + ".jpg");
                                    break;
                                case 10:
                                    pct11.Image = Image.FromFile(path + "\\snap" + (i + 1).ToString() + ".jpg");
                                    break;
                                case 11:
                                    pct12.Image = Image.FromFile(path + "\\snap" + (i + 1).ToString() + ".jpg");
                                    break;
                                case 12:
                                    pct13.Image = Image.FromFile(path + "\\snap" + (i + 1).ToString() + ".jpg");
                                    break;
                                case 13:
                                    pct14.Image = Image.FromFile(path + "\\snap" + (i + 1).ToString() + ".jpg");
                                    break;
                                case 14:
                                    pct15.Image = Image.FromFile(path + "\\snap" + (i + 1).ToString() + ".jpg");
                                    break;
                                case 15:
                                    pct16.Image = Image.FromFile(path + "\\snap" + (i + 1).ToString() + ".jpg");
                                    break;
                            }
                        }

                    }
                    //front
                    if (File.Exists(path + "\\snap-count.jpg"))
                    {
                        string fcopy1 = frmMainInspect.FrontPath + @"\Images\snap.jpg";//"C:\\Project\\Cam2\\Cam2BaslerML\\Cam2BaslerML\\bin\\Debug\\Images\\snap.jpg";
                        string fname1 = path + "\\snap-count.jpg";
                        File.Copy(fname1, fcopy1, true);
                    }
                    if (File.Exists(path + "\\snap-inspect.jpg"))
                    {
                        string fcopy1 = frmMainInspect.FrontPath + @"\Images\snap-inspect.jpg";//"C:\\Project\\Cam2\\Cam2BaslerML\\Cam2BaslerML\\bin\\Debug\\Images\\snap-inspect.jpg";
                        string fname1 = path + "\\snap-inspect.jpg";
                        File.Copy(fname1, fcopy1, true);
                    }



                }
                ));
            }
            catch (Exception ex) { }

        }

        private void btnLoadRejects_Click(object sender, EventArgs e)
        {
            btnLoadRejects.Enabled = false;
            LoadRejects();
            btnLoadRejects.Enabled = true;
        }

        private void cmbDir_Click(object sender, EventArgs e)
        {
            try
            {
                //sort by creation time
                var Dirs = Directory.GetDirectories(@"c:\Rejects", "*", SearchOption.AllDirectories).OrderBy(d => new FileInfo(d).CreationTime);

                cmbDir.Items.Clear();
               
                foreach (string dir in Dirs)
                {
                    string[] ss = dir.Split('\\');
                    if (ss.Length > 3 && !cmbDir.Items.Contains(ss[2]))
                    {
                        cmbDir.Items.Add(ss[2]);
                    }
                    //{
                    //    for (int i = 0; i < ss.Length; i++)
                    //        cmbDir.Items.Add(ss[i]);
                    //}
                   // string st = dir.Substring(("c:\\Rejects\\").Length, dir.Length - ("c:\\Rejects\\").Length);
                    //string[] str = st.Split('\');
                    //cmbDir.Items.Add(st);
                    //cmbDir.Items.Add(dir.Substring(("c:\\Rejects\\").Length, dir.Length - ("c:\\Rejects\\").Length));
                }
            }
            catch (Exception ex)
            {

            }
        }

        private void btnEnablePort_Click(object sender, EventArgs e)
        {
            PortEnable("Ethernet");
        }
        public void PortEnable(string interfaceName)
        {
            try
            {
                System.Diagnostics.Process p = new System.Diagnostics.Process();
                p.StartInfo.FileName = "cmd.exe";
                p.StartInfo.Arguments = "/C netsh interface set interface name= " + Convert.ToChar(34) + interfaceName +
                    Convert.ToChar(34) + " admin=enabled";
                p.StartInfo.Verb = "runas";
                p.Start();
                p.WaitForExit();
                p.Close();

            }
            catch (Exception ex) { }
        }
        public void PortDisable(string interfaceName)
        {
            try
            {
                System.Diagnostics.Process p = new System.Diagnostics.Process();
                p.StartInfo.FileName = "cmd.exe";
                p.StartInfo.Arguments = "/C netsh interface set interface name= " + Convert.ToChar(34) + interfaceName +
                    Convert.ToChar(34) + " admin=disabled";
                p.StartInfo.Verb = "runas";
                p.Start();
                p.WaitForExit();
                p.Close();

            }
            catch (Exception ex) { }
        }

        private void btnDisablePort_Click(object sender, EventArgs e)
        {
            PortDisable("Ethernet");
        }

        private void chkStretchFront_CheckedChanged(object sender, EventArgs e)
        {
           
                if (chkStretchFront.Checked)
                {
                    
                    pctSnapFront.SizeMode = PictureBoxSizeMode.StretchImage;//.StretchImage
                    pctSnapFront.Dock = DockStyle.Fill;
                    //pctSnapFront.Width = panel4.Width - 2; //930
                                                           //pctSnap.Height = panel1liveTab.Height - 2; //698
                pctSnapFront.Height = pctSnapHFront;// (int)(pctSnapFront.Width * 1000.0f / 5472.0f);//3648.0f / 5472.0f);
                pctSnapFront.Width = pctSnapWFront;
                panel4.AutoScroll = true;

                }
                else
                {
                    chkStretchImageFront_unchecked = true;
                    pctSnapFront.Dock = DockStyle.None;
                    pctSnapFront.SizeMode = PictureBoxSizeMode.AutoSize;
                    //pctSnapFront.Width = 1042;

                    //pctSnapFront.Height = 605;
                    panel4.AutoScroll = true;
                    pctSnapFront.Height = pctSnapHFront;
                    pctSnapFront.Width = pctSnapWFront;
                    panel4.Height = panel4H;
                    panel4.Width = panel4W;


                }
            
        }

        private void optSnap1_CheckedChanged(object sender, EventArgs e)
        {
            //return;
            if (!(camera1 is null) && optSnap1.Checked  && nExposureInspection > 0) //&& camera1.CameraNumber > 1
            {
                trkExp1.Value = nExposureInspection;

                //camera1.KeepShot();
                string sv = camera1.Parameters[PLCamera.AcquisitionMode].GetValue();
                if (sv != PLCamera.AcquisitionMode.Continuous)
                {
                    camera1.Parameters[PLCamera.AcquisitionMode].SetValue(PLCamera.AcquisitionMode.Continuous);
                    try
                    {
                        camera1.StreamGrabber.Start(GrabStrategy.OneByOne, GrabLoop.ProvidedByStreamGrabber);
                    }
                    catch (Exception ex) { frmMainInspect.AddList("opt1 camera1.StreamGrabber.Start error"); }
                }
                //if (sv != PLCamera.AcquisitionMode.Continuous)
                //{
                //    camera1.Parameters[PLCamera.AcquisitionMode].SetValue(PLCamera.AcquisitionMode.Continuous);
                //}
                //try
                //{
                //    camera1.StreamGrabber.Start(GrabStrategy.OneByOne, GrabLoop.ProvidedByStreamGrabber);
                //}
                //catch (Exception ex) { frmMainInspect.AddList("opt1 camera1.StreamGrabber.Start error"); }

                trkExp1_Scroll(trkExp1, null);
            }
            else if (!(camera1 is null) && optSnap2.Checked && nExposureDiameter > 0) //&& camera1.CameraNumber > 1
            {
                trkExp1.Value = nExposureDiameter;

                //camera1.KeepShot();
                string sv = camera1.Parameters[PLCamera.AcquisitionMode].GetValue();
                if (sv != PLCamera.AcquisitionMode.Continuous)
                {
                    camera1.Parameters[PLCamera.AcquisitionMode].SetValue(PLCamera.AcquisitionMode.Continuous);
                    try
                    {
                        camera1.StreamGrabber.Start(GrabStrategy.OneByOne, GrabLoop.ProvidedByStreamGrabber);
                    }
                    catch (Exception ex) { frmMainInspect.AddList("opt2 camera1.StreamGrabber.Start error"); }
                }
                //if (sv != PLCamera.AcquisitionMode.Continuous)
                //{
                //    camera1.Parameters[PLCamera.AcquisitionMode].SetValue(PLCamera.AcquisitionMode.Continuous);
                //}
                //try
                //{
                //    camera1.StreamGrabber.Start(GrabStrategy.OneByOne, GrabLoop.ProvidedByStreamGrabber);
                //}
                //catch (Exception ex) { frmMainInspect.AddList("opt2 camera1.StreamGrabber.Start error"); }

                trkExp1_Scroll(trkExp1, null);
            }
        }


        // Align value into [min,max] and to the nearest lower valid increment step
        static long Align(long value, long min, long max, long inc)
        {
            if (value < min) value = min;
            if (value > max) value = max;
            if (inc <= 1) return value;
            long offset = (value - min) % inc;
            return value - offset; // floor to valid step
        }


        private void txtItem_TextChanged(object sender, EventArgs e)
        {
            foreach(var item in frmMainInspect.CmbCatNum.Items)
            {
                if (item.ToString()==txtItem.Text)
                {
                    Invoke((Action)(() => frmMainInspect.CmbCatNum.SelectedItem=item.ToString()));
                    return;
                }
            }

            //if no correspoding item was found, create a new one
            Invoke((Action)(()=> frmMainInspect.NewCatalogItem(txtItem.Text)));
        }

        private void pct1liveTab_Click(object sender, EventArgs e)
        {
            
        }

        private async void btnSnapOnly_Click(object sender, EventArgs e)
        {
            try
            {
                btnSnapOnly.Enabled = false;
                ClearPictureArray();

                inv.settxt(lblCount, "0");
                OptChecked(0);
                var task = Task.Run(() => ProcDetect(true, false, true));
                await task;
                if (task.Result.berr)
                {
                    MessageBox.Show("ERROR SNAP", "VISION", MessageBoxButtons.OK, MessageBoxIcon.Error, MessageBoxDefaultButton.Button1);
                }

                btnSnapOnly.Enabled = true;
            }
            catch (Exception ex) { btnSnapOnly.Enabled = true; }
        }

        private void cmbDir_SelectedIndexChanged(object sender, EventArgs e)
        {
            
                try
                {
                    //sort by creation time
                    var Dirs = Directory.GetDirectories(@"c:\Rejects\"+ cmbDir.Text, "*", SearchOption.AllDirectories).OrderBy(d => new FileInfo(d).CreationTime);

                    lstSavedParts.Items.Clear();

                    foreach (string dir in Dirs)
                    {
                        string[] ss = dir.Split('\\');
                        if (ss.Length > 3 && !cmbDir.Items.Contains(ss[3]))
                        {
                            lstSavedParts.Items.Add(ss[3]);
                        }
                        //{
                        //    for (int i = 0; i < ss.Length; i++)
                        //        cmbDir.Items.Add(ss[i]);
                        //}
                        // string st = dir.Substring(("c:\\Rejects\\").Length, dir.Length - ("c:\\Rejects\\").Length);
                        //string[] str = st.Split('\');
                        //cmbDir.Items.Add(st);
                        //cmbDir.Items.Add(dir.Substring(("c:\\Rejects\\").Length, dir.Length - ("c:\\Rejects\\").Length));
                    }
                }
                catch (Exception ex)
                {

                }
            }

        int RejectPfront = -1;
        public async Task<WebComm.CommReply> InspectFront()
        {
            //if (bCycle) return false;
            WebComm.CommReply rep = new WebComm.CommReply();
            rep.result = false;
            rep.comment = "";
            bool reply = false;
            //
           
            RejectBfront = -1;
            RejectPfront = -1;
            try
            {
                ButtonsEnabled(false);

                //tabControl1.Invoke((Action)(() =>
                //{
                //    tabControl1.SelectTab(3);
                //    AddWindow();
                //}));


                frmMainInspect.RegionFoundFrontBrSave = new string[1];
                //frmMainInspect.RegionFoundFrontPlSave = new string[1];
                
                //frmMainInspect.RegIndex = 0;
                //frmMainInspect.RegIndex2 = 0;
                frmMainInspect.RegIndexFront = 0;

                //run inspection
                //var taskInsp = Task.Run(() => frmMainInspect.InspectionCycle());

                inv.settxt(frmBeckhoff.mFormBeckhoffDefInstance.txtAcqTime, "");
                inv.settxt(frmBeckhoff.mFormBeckhoffDefInstance.txtInspectTime, "");
                inv.settxt(frmBeckhoff.mFormBeckhoffDefInstance.txtElapsedTime, "");
                inv.settxt(txtMess, "");

                inv.settxt(lblCount, "0");

                //bCycle = true;

                bWeldonCycle = false;
                nWeldonCycleEmul = 0;

                btn_status(false);
                inCycleInspectFront = true;
                this.Invoke((Action)(() => { frmMainInspect.listBox11.Items.Clear(); }));
                this.Invoke((Action)(() => { frmMainInspect.listBox11.Items.Add("Inspect front start"); }));
                var taskInsp = Task.Run(() => frmMainInspect.InspectionCycleFront());
                await taskInsp;
                reply = taskInsp.Result;
                inCycleInspectFront = false;
                //inv.settxt(frmBeckhoff.mFormBeckhoffDefInstance.txtCycleTime, String.Format("{0:f0}", t3 - t1));
                //save file
                if (!reply) {
                    RejectBfront = -1000;
                    RejectPfront = -1000;
                    bCycle = false;
                    rep.result = reply;
                    rep.status = RejectBfront.ToString() + "," + RejectPfront.ToString();
                    return rep;
                } else {
                    RejectBfront = frmMainInspect.RegionFoundFrontBrSave.Length - 1;
                    //RejectPfront = frmMainInspect.RegionFoundFrontPlSave.Length - 1;
                }
                //using (FileStream file = new FileStream(aPath + "\\test BREAKfront" + ".txt", FileMode.Create, FileAccess.Write, FileShare.Inheritable))
                //{
                //    StreamWriter swr = new StreamWriter(file);
                //    for (int i = 0; i < frmMainInspect.RegionFoundFrontBrSave.Length; i++) {
                //        string ss = frmMainInspect.RegionFoundFrontBrSave[i];
                //        swr.WriteLine(ss);
                //    }
                //    swr.Close();
                //}
                //using (FileStream file = new FileStream(aPath + "\\test PEELSfront" + ".txt", FileMode.Create, FileAccess.Write, FileShare.Inheritable))
                //{
                //    StreamWriter swr = new StreamWriter(file);
                //    for (int i = 0; i < frmMainInspect.RegionFoundFrontPlSave.Length; i++) {
                //        string ss = frmMainInspect.RegionFoundFrontPlSave[i];
                //        swr.WriteLine(ss);
                //    }
                //    swr.Close();
                //}

                //NPNP
                if (chkSaveResults.Checked)
                //if (cmbSaveResults.SelectedText != "Don't Save Results")
                {
                    var task2 = Task.Run(() => SaveRejectsFront());
                    await task2;

                }
                bCycle = false;
                rep.result = reply;
                rep.status = RejectBfront.ToString() + "," + RejectPfront.ToString();
                return rep;
            }
            catch (System.Exception ex) { inCycleInspectFront = false; rep.result = false; return rep; }
        }
        public string RejectStringFront = "";

        private void cmbSnapShotStrategy_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (cmbSnapShotStrategy.SelectedIndex != -1)
            {
                _eSnapShotStrategy = (eSnapShotStrategy)cmbSnapShotStrategy.SelectedIndex;
            }
        }

        private void RejectFront_Paint(object sender, PaintEventArgs e, int region)
        {
            try
            {
                if (inCycleInspect || inCycleVision || inCycleInspectFront) return;
                if (frmMainInspect.RegionFoundFrontBrSave == null || frmMainInspect.RegionFoundFrontBrSave[0] == null) return;
                e.Graphics.ResetTransform();
                Pen p = new Pen(Color.Red);
                string[] RegionFoundBrSave = new string[1];
                //string[] RegionFoundPlSave = new string[1];
                Double x0 = 0;
                Double y0 = 0;
                Double w = 0;
                Double h = 0;
                //if (region == 1)
                //{
                    p = new Pen(Color.Red);
                    p.Width = 0.2f;
                    RegionFoundBrSave = new string[frmMainInspect.RegionFoundFrontBrSave.Length];
                    RegionFoundBrSave = frmMainInspect.RegionFoundFrontBrSave;
                    //RegionFoundPlSave = new string[frmMainInspect.RegionFoundFrontPlSave.Length];
                    //RegionFoundPlSave = frmMainInspect.RegionFoundFrontPlSave;
                    x0 = Single.Parse(frmMainInspect.txtPProiPosX1.Text) * frmMainInspect.scaleW1;
                    y0 = Single.Parse(frmMainInspect.txtPProiPosY1.Text) * frmMainInspect.scaleH1;
                    w = Single.Parse(frmMainInspect.txtPProiWidth1.Text) * frmMainInspect.scaleW1;
                    h = Single.Parse(frmMainInspect.txtPProiHeight1.Text) * frmMainInspect.scaleH1;
                //}
                if (frmMainInspect.m_eCognexROIType == frmMain.eCognexROIType.eUseWholeImageAsROI)
                {
                    x0 = 0;
                    y0 = 0;
                    w = frmMainInspect.pictureBoxInspect1.Image.Width;
                    h = frmMainInspect.pictureBoxInspect1.Image.Height;
                }
                else if (frmMainInspect.m_eCognexROIType == frmMain.eCognexROIType.UseAsBeforeGeographicROI)
                {
                    x0 = 0;
                    y0 = 400;
                    w = frmMainInspect.pictureBoxInspect1.Image.Width;
                    h = frmMainInspect.pictureBoxInspect1.Image.Height - 800;
                }
                Rectangle re = new Rectangle();
                //Pen p = new Pen(Color.Red);
                string[] ssbr = new string[1];
                //string[] sspel = new string[1];
                int a = 0;
                //break
                string[] RegionBr = new string[1];
                if (RejectStringFront == "")
                {
                    RegionBr = new string[RegionFoundBrSave.Length];
                    Array.Copy(RegionFoundBrSave, RegionBr, RegionFoundBrSave.Length);
                }
                else
                {
                    if (RejectString.IndexOf("Break") > 0) RegionBr[0] = RejectString;
                }

                for (int i = 0; i < RegionFoundBrSave.Length; i++) {
                    string s = RegionFoundBrSave[i];//break
                    if (s != null && int.Parse(s.Substring(0, 2)) == indexnum + 1) {
                        ssbr[a] = s;
                        a++;
                        Array.Resize<string>(ref ssbr, ssbr.Length + 1);
                    }
                }
                //peels
                //a = 0;
                //for (int i = 0; i < RegionFoundPlSave.Length; i++) {
                //    string s = RegionFoundPlSave[i];//peels
                //    if (s != null && int.Parse(s.Substring(0, 2)) == indexnum + 1) {
                //        sspel[a] = s;
                //        a++;
                //        Array.Resize<string>(ref sspel, sspel.Length + 1);
                //    }
                //}

                Single X0 = 0;
                Single Y0 = 0;
                Single H = 0;
                Single W = 0;
                Double scalew = ((Single)pctSnapFront.Width / (Single)frmMainInspect.pictureBoxInspect1.Width);//scale in picturebox1 by waight
                Double scaleh = ((Single)pctSnapFront.Height / (Single)frmMainInspect.pictureBoxInspect1.Height);//scale in picturebox1 by height
                                                                                                                 //y01->yo coordiate for image in picturebox1
                                                                                                                 //(y0-y02)*scalew  ->y0 for rectangle in image in picturebox1 
                Double x0roi = ((Single)x0);// * frmMainInspect.scaleW1);//);
                Double y0roi = ((Single)y0);// * frmMainInspect.scaleH1);//);
                Double wroi = w;// scalew * w;
                Double hroi = h;// * scaleh;
                if (pctSnapFront.Image == null) return;
                //re = new Rectangle((int)(x0 * scaleh), (int)(y01 + (y0 - y02) * scalew), (int)((scalew) * w), (int)(h * scaleh));
                Single zoom = (Single)pctSnapFront.Image.Width / (Single)pctSnapFront.Image.Height;

                re = new Rectangle((int)(x0roi), (int)(y0roi), (int)(wroi), (int)(hroi));
                p.Width = 0.2f;
                e.Graphics.ScaleTransform((Single)scalew, (Single)scaleh);
                e.Graphics.DrawRectangle(p, re);
                if (!chkShoeDefectsFront.Checked) return;
                //return;
                //
                //for (int i = 0; i < 2; i++)
                //{
                    //i = 0;
                    string[] ss = new string[1];
                    //if (i == 0) {
                        ss = new string[1];
                        Array.Resize<string>(ref ss, ssbr.Length);
                        ss = ssbr;
                        //p = new Pen(Color.Red);
                    //} else if (i == 1) {
                    //    ss = new string[1];
                    //    Array.Resize<string>(ref ss, sspel.Length);
                    //    ss = sspel;
                    //    //p = new Pen(Color.Yellow);
                    //}

                    for (int j = 0; j < ss.Length - 1; j++) {
                        string[] s = ss[j].Split(' ');
                        X0 = -1; Y0 = -1; H = -1; W = -1;
                        for (int jj = 0; jj < s.Length; jj++) {
                            if (s[jj].IndexOf("X0=") >= 0) {
                                X0 = (int)Single.Parse(s[jj].Substring(3, s[jj].Length - 3));
                            } else if (s[jj].IndexOf("Y0=") >= 0) {
                                Y0 = (int)Single.Parse(s[jj].Substring(3, s[jj].Length - 3));
                            } else if (s[jj].IndexOf("H=") >= 0) {
                                H = (int)Single.Parse(s[jj].Substring(2, s[jj].Length - 2));
                            } 
                            if (s[jj].IndexOf("W=") >= 0) {
                                W = (int)Single.Parse(s[jj].Substring(2, s[jj].Length - 2));
                            }
                        }

                        if (pctSnapFront.Image == null) return;
                        Single scaleX = ((Single)pctSnapFront.Width / (Single)pctSnapFront.Image.Width);// *((Single)wroi / (Single)pctSnap.Width);
                        Single scaleY = ((Single)pctSnapFront.Height / (Single)pctSnapFront.Image.Height);// * ((Single)hroi / (Single)pctSnap.Height);

                        Double x00 = X0 * frmMainInspect.scaleW1;// * ((Single)pctSnap.Width/(scalew * w));
                        Double y00 = Y0 * frmMainInspect.scaleH1;// * ((Single)pctSnap.Height / (scalew * h));
                        //w = (Single)W * wroi / (Single)pctSnap.Image.Width;
                        //h = (Single)H * hroi / (Single)pctSnap.Image.Height;

                        w = (Single)W * frmMainInspect.scaleW1; ;// scaleX;
                        h = (Single)H * frmMainInspect.scaleH1; //scaleY;

                        //p = new Pen(Color.Red);
                        if (w < 1) { w = 1.0f; }// p = new Pen(Color.Orange); }
                        if (h < 1) { h = 1.0f; }// p = new Pen(Color.Orange); }
                        w = w * 5.0f;
                        h = h * 5.0f;
                        
                        re = new Rectangle((int)(x00 + x0roi - w / 2), (int)(y00 + y0roi - h / 2), (int)(w), (int)(h));
                        //re = new Rectangle((int)(y00 + y0roi - w / 2), (int)(x00 + x0roi - h / 2), (int)(w), (int)(h));
                        //re = new Rectangle((int)(10), (int)(20), (int)(w), (int)(h));
                        p = new Pen(Color.Red);
                        p.Width = 0.2f;
                        e.Graphics.DrawRectangle(p, re);
                    //stretch
                    System.Drawing.Point pt = new System.Drawing.Point((int)((x00 + x0roi - w / 2) * (Single)scalew), (int)((y00 + y0roi - h / 2) * (Single)scaleh));
                        pt.X = pt.X - 100;
                        pt.Y = pt.Y - 100;
                        if (chkStretchFront.Checked)
                        {
                            panel4.AutoScroll = true;
                            panel4.AutoScrollMinSize = new System.Drawing.Size(panel4.Width - 5, panel4.Height - 5);

                            //panel2.AutoScrollPosition = pt;
                        }
                        else
                        {
                            if (chkStretchImageFront_unchecked)
                            {
                                pctSnapFront.SizeMode = PictureBoxSizeMode.AutoSize;
                                panel4.AutoScroll = true;
                                panel4.AutoScrollMinSize = new System.Drawing.Size(pctSnapFront.Width, pctSnapFront.Height);
                                if (pt.X != 0 && pt.Y != 0)
                                {
                                    panel4.AutoScrollPosition = pt;
                                }
                                chkStretchImageFront_unchecked = false;
                            }
                        }
                    }
                //}
            }
            catch (System.Exception ex) { }
        }
        string lstStr = "";
        public  void AddList(string item)
        {
            try
            {

                lstStr = lstStr + item + "\r\n";

                _ = Task.Run(() => this.Invoke((Action)(() => {
                    txtMess.Text = lstStr;
                    if (txtMess.Lines.Length > 0)
                    {
                        txtMess.SelectionStart = txtMess.TextLength;
                        txtMess.ScrollToCaret();
                    }
                })));

            }
            catch (System.Exception ex) { }
        }
    }
}
