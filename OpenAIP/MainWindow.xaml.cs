using Microsoft.FlightSimulator.SimConnect;
using System;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;

namespace OpenAIP
{
    public enum DEFINITION
    {
        Dummy = 0
    }

    public enum REQUEST
    {
        Dummy = 0
    }

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        SimConnect sim;
        private double longitude = 8.65;
        private double latitude = 49.34;
        private double direction = 0;
        private bool ledIntensity = false;

        /// User-defined win32 event => put basically any number?
        public const int WM_USER_SIMCONNECT = 0x0402;

        protected HwndSource GetHWinSource()
        {
            return PresentationSource.FromVisual(this) as HwndSource;
        }

        public MainWindow()
        {
            InitializeComponent();
            connectionLed.Fill = Brushes.Red;
            this.Topmost = true;

            DispatcherTimer timer = new DispatcherTimer();
            timer.Interval = TimeSpan.FromSeconds(1);
            timer.Tick += Timer_Tick; ;
            timer.Start();

            var assembly = Assembly.GetExecutingAssembly();
            var resourceName = "OpenAIP.map.html";

            using (Stream stream = assembly.GetManifestResourceStream(resourceName))
            using (var reader = new StreamReader(stream))
                mapContainer.NavigateToString(reader.ReadToEnd());
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            ledIntensity = !ledIntensity;
            if (sim == null)
            {
                Connect();
                connectionLed.Fill = (ledIntensity ? Brushes.DarkRed : Brushes.Red);
            }
            else
            {
                try
                {
                    sim.RequestDataOnSimObjectType((REQUEST)1, (DEFINITION)1, 0, SIMCONNECT_SIMOBJECT_TYPE.USER);
                    sim.RequestDataOnSimObjectType((REQUEST)2, (DEFINITION)2, 0, SIMCONNECT_SIMOBJECT_TYPE.USER);
                    sim.RequestDataOnSimObjectType((REQUEST)3, (DEFINITION)3, 0, SIMCONNECT_SIMOBJECT_TYPE.USER);
                    connectionLed.Fill = (ledIntensity ? Brushes.LightGreen : Brushes.Green);
                }
                catch
                {
                    Disconnect();
                }
            }
        }

        private void Sim_OnRecvQuit(SimConnect sender, SIMCONNECT_RECV data)
        {
            connectionLed.Fill = Brushes.Red;
        }

        private void Sim_OnRecvOpen(SimConnect sender, SIMCONNECT_RECV_OPEN data)
        {
            connectionLed.Fill = Brushes.Green;

            /// Define a data structure
            sim.AddToDataDefinition((DEFINITION)1, "PLANE LATITUDE", "degree", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
            /// IMPORTANT: Register it with the simconnect managed wrapper marshaller
            /// If you skip this step, you will only receive a uint in the .dwData field.
            sim.RegisterDataDefineStruct<double>((DEFINITION)1);

            /// Define a data structure
            sim.AddToDataDefinition((DEFINITION)2, "PLANE LONGITUDE", "degree", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
            sim.RegisterDataDefineStruct<double>((DEFINITION)2);

            sim.AddToDataDefinition((DEFINITION)3, "PLANE HEADING DEGREES TRUE", "degree", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
            sim.RegisterDataDefineStruct<double>((DEFINITION)3);
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            var windowsSource = GetHWinSource();
            windowsSource.AddHook(WndProc);

            Connect();
        }

        private void Connect()
        {
            /// The constructor is similar to SimConnect_Open in the native API
            try
            {
                sim = new SimConnect(this.Title, GetHWinSource().Handle, WM_USER_SIMCONNECT, null, 0);
                sim.OnRecvOpen += Sim_OnRecvOpen;
                sim.OnRecvQuit += Sim_OnRecvQuit;
                sim.OnRecvSimobjectDataBytype += Sim_OnRecvSimobjectDataBytype;
            }
            catch
            {
                sim = null;
            }
        }

        private void Sim_OnRecvSimobjectDataBytype(SimConnect sender, SIMCONNECT_RECV_SIMOBJECT_DATA_BYTYPE data)
        {
            uint iRequest = data.dwRequestID;
            double dValue = (double)data.dwData[0];

            try
            {
                switch (iRequest)
                {
                    case 1:
                        latitude = dValue;
                        mapContainer.InvokeScript("setPosition", latitude.ToString(), longitude.ToString());
                        break;
                    case 2:
                        longitude = dValue;
                        mapContainer.InvokeScript("setPosition", latitude.ToString(), longitude.ToString());
                        break;
                    case 3:
                        direction = dValue;
                        mapContainer.InvokeScript("setRotation", direction.ToString());
                        break;
                    default:
                        break;
                }
            }
            catch // most likely a JS issue: for example if the page is not yet loaded
            {
            }
        }

        public void ReceiveSimConnectMessage()
        {
            sim?.ReceiveMessage();
        }

        public void Disconnect()
        {
            if (sim != null)
            {
                sim.Dispose();
                sim = null;
                connectionLed.Fill = Brushes.Red;
            }
        }

        private IntPtr WndProc(IntPtr hWnd, int iMsg, IntPtr hWParam, IntPtr hLParam, ref bool bHandled)
        {
            try
            {
                if (iMsg == WM_USER_SIMCONNECT)
                    ReceiveSimConnectMessage();
            }
            catch
            {
                Disconnect();
            }

            return IntPtr.Zero;
        }

        private void Window_Unloaded(object sender, RoutedEventArgs e)
        {
            Disconnect();
        }
    }
}
