using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.ApplicationModel.DataTransfer;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Devices.Bluetooth;
using Windows.Devices.Enumeration;
using Windows.Devices.Input;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Graphics.Printing.PrintSupport;
using Windows.Media.Devices;
using Windows.System;
using Windows.UI;
using Windows.UI.Core;
using Windows.UI.Input;
using Windows.UI.Text;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Documents;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using Windows.UI.Xaml.Shapes;
using Point = Windows.Foundation.Point;
using Rectangle = Windows.UI.Xaml.Shapes.Rectangle;
using System.Threading.Tasks;
using System.Text;
using Windows.Security.Cryptography;
using Windows.Storage.Streams;
using Windows.UI.Popups;
using Windows.Devices.Bluetooth.Advertisement;
using Windows.UI.Xaml.Media.Imaging;
using System.Security.Cryptography;
using System.Data;
using Windows.Data.Json;
using Windows.Storage;
using System.Xml;
using static System.Net.Mime.MediaTypeNames;
using System.IO.Ports;


// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace PropMagic
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        public string SelectedBleDeviceId;
        public string SelectedBleDeviceName = "No device selected";

        public string currentVersion = "80124";

        private ObservableCollection<BluetoothLEDeviceDisplay> KnownDevices = new ObservableCollection<BluetoothLEDeviceDisplay>();
        private List<DeviceInformation> UnknownDevices = new List<DeviceInformation>();

        private DeviceWatcher deviceWatcher;

        private BluetoothLEDevice bluetoothLeDevice = null;
        private GattCharacteristic writeCharacteristic;

        // Only one registered characteristic at a time.
        private GattCharacteristic readCharacteristic;
        private GattPresentationFormat presentationFormat;

        readonly int E_BLUETOOTH_ATT_WRITE_NOT_PERMITTED = unchecked((int)0x80650003);
        readonly int E_BLUETOOTH_ATT_INVALID_PDU = unchecked((int)0x80650004);
        readonly int E_ACCESSDENIED = unchecked((int)0x80070005);
        readonly int E_DEVICE_NOT_AVAILABLE = unchecked((int)0x800710df); // HRESULT_FROM_WIN32(ERROR_DEVICE_NOT_AVAILABLE)

        const uint SYSTEM_START = 0;
        const uint SYSTEM_IDLE = 1;
        const uint SYSTEM_BLE_SCAN = 2;
        const uint SYSTEM_BLE_CONNECTING = 3;
        const uint SYSTEM_BLE_CONNECTED = 4;
        const uint SYSTEM_BLE_DISCONNECTING = 5;
        const uint SYSTEM_BLE_DISCONNECTED = 6;
        const uint SYSTEM_BLE_REQUEST_DEVICE_DATA = 7;
        const uint SYSTEM_BLE_DROPPED = 8;

        const uint D0 = 0; //  - Trigger
        const uint D1 = 1; //  - VCC-OUT-1
        const uint D2 = 2; //  - VCC-OUT-2
        const uint D3 = 21; // - Neopixel
        const uint D4 = 22; // - SDA
        const uint D5 = 23; // - SCL
        const uint D6 = 16;
        const uint D7 = 17;
        const uint D8 = 19; // - Neopixel
        const uint D9 = 20;
        const uint D10 = 18;

        const bool OPEN = true;
        const bool CLOSE = false;

        private uint SYSTEM_STATE = 0;

        private bool SCANNER_INIT_COMPLETE = false;
        private bool DEVICE_WATCHER_READY_TO_START = false;

        SolidColorBrush green = new SolidColorBrush(Colors.Green);
        SolidColorBrush red = new SolidColorBrush(Colors.Red);
        SolidColorBrush blue = new SolidColorBrush(Colors.Blue);
        SolidColorBrush black = new SolidColorBrush(Colors.Black);
        SolidColorBrush yellow = new SolidColorBrush(Colors.Yellow);
        SolidColorBrush gray = new SolidColorBrush(Colors.Gray);
        SolidColorBrush light_gray = new SolidColorBrush(Colors.LightGray);
        SolidColorBrush white = new SolidColorBrush(Colors.White);

        Thickness highlight_thickness = new Thickness(1.5, .5, 1.5, .5);
        Thickness normal_thickness = new Thickness(0);
        Thickness row_thickness = new Thickness(.2);

        bool isDraggingLeft = false;
        bool isDraggingRight = false;
        bool isHeld = false;
        bool isPanning = false;
        bool isScaling = false;
        bool markerPan_start = false;
        bool markerPan_end = false;
        bool realtimeScrubber = false;

        bool endtimeChanged = false;

        Border selectedBlock = null;

        PointerPoint prevPointer = null;

        double prev_zoom_scale = 1;

        double zoom_scale = 1;
        double XPanTracker = 0;
        double YPanTracker = 0;

        double blockEdgeSize = 10;
        double blockEdgeSize_const = 6;
        double blockHeight = 7;

        double xPanSpeed = 1.1;
        double yPanSpeed = 1.1;

        double currentMarkerPosition = 0;
        double endMarkerPosition = 100;
        double markerPosition_corrected = 0;

        double timelineWidth;
        double timelineHeight;

        bool timelineLoaded = false;
        bool timelineRequest_Sent = false;
        bool timelineRequest_Complete = false;

        int transferLength = 0;
        double transferIterator = 0;

        DispatcherTimer mouseHoldTimer = new DispatcherTimer();
        DispatcherTimer pinchDebounceTimer = new DispatcherTimer();
        DispatcherTimer timeMarkerTimer = new DispatcherTimer();
        DispatcherTimer bleScanTimer = new DispatcherTimer();
        DispatcherTimer bleConnectionCheckTimer = new DispatcherTimer();

        DispatcherTimer systemTimer = new DispatcherTimer();

        HashSet<uint> pointertracker = new HashSet<uint>();
        
        TextBlock timeMarker;
        Line timeMarker_line;
        Line timeMarker_line_current;
        Line timeMarker_line_end;

        bool BLE_Ready = false;
        bool BLE_Scanning = false;
        bool BLE_Connection_Sent = false;
        bool BLE_Connection_Accepted = false;
        bool BLE_Connection_Rejection = false;

        BluetoothLEDeviceDisplay bleDeviceDisplay = null;

        ObservableCollection<timelineBlock> timelineBlockList = new ObservableCollection<timelineBlock>();
        ObservableCollection<rowController> rowControllers = new ObservableCollection<rowController>();

        HashSet<int> activeBlockIDs = new HashSet<int>();
        HashSet<int> activeControllerIDs = new HashSet<int>();

        Dictionary<string, uint> outputMapping = new Dictionary<string, uint>();

        Stopwatch stopwatch = new Stopwatch();
        Stopwatch realtimeLockout = new Stopwatch();

        long prevMillis = 0;
        long prevRealtimeLockout = 0;
        long alternateRealtimeLockout = 0;

        bool bleConnectionTask_Running = false;

        Windows.UI.Xaml.Controls.Image scrubber_arrow_start;
        Windows.UI.Xaml.Controls.Image scrubber_arrow_end;

        MenuFlyout blockMenu = new MenuFlyout();
        MenuFlyout rowTypeMenu = new MenuFlyout();
        MenuFlyout rowPinMenu = new MenuFlyout();

        MenuFlyout ambientMenu = new MenuFlyout();
        MenuFlyout scareMenu = new MenuFlyout();
        MenuFlyout playbackMenu = new MenuFlyout();

        MenuFlyoutItem rPin_1 = new MenuFlyoutItem();
        MenuFlyoutItem rPin_2 = new MenuFlyoutItem();
        MenuFlyoutItem rPin_3 = new MenuFlyoutItem();
        MenuFlyoutItem rPin_4 = new MenuFlyoutItem();
        MenuFlyoutItem rPin_5 = new MenuFlyoutItem();
        MenuFlyoutItem rPin_6 = new MenuFlyoutItem();
        MenuFlyoutItem rPin_7 = new MenuFlyoutItem();
        MenuFlyoutItem rPin_none = new MenuFlyoutItem();

        rowController selectedRow = null;

        Border add_output_border;

        //int activeRows = 4;

        public MainPage()
        {
            this.InitializeComponent();

            add_output_border = new Border();
            add_output_border.Name = "add_output_border";
            add_output_border.DoubleTapped += add_output_border_DoubleTapped;
            add_output_border.Width = 43;
            add_output_border.Height = 10;
            add_output_border.HorizontalAlignment = HorizontalAlignment.Center;
            add_output_border.VerticalAlignment = VerticalAlignment.Center;
            //add_output_border.BorderBrush = blue;
            //add_output_border.BorderThickness = normal_thickness;
            add_output_border.CornerRadius = new CornerRadius(2);

            TextBlock t = new TextBlock();
            t.Width = 35;
            t.Height = 7;
            t.HorizontalAlignment = HorizontalAlignment.Center;
            t.SetValue(Canvas.LeftProperty, 3);
            t.VerticalAlignment = VerticalAlignment.Top;
            t.Text = "+ Add Output";
            t.FontSize = 5;

            block_id_box.Children.Add(add_output_border);

            add_output_border.Child = t;

            main_viewbox.Stretch = Stretch.Fill;
            scrubber_arrow_start = scrubber_arrow;

            mouseHoldTimer.Interval = TimeSpan.FromMilliseconds(20);
            mouseHoldTimer.Tick += mouseHoldTimer_Tick;

            pinchDebounceTimer.Interval = TimeSpan.FromMilliseconds(400);
            pinchDebounceTimer.Tick += PinchDebounceTimer_Tick;

            timeMarkerTimer.Interval = TimeSpan.FromMilliseconds(5);
            timeMarkerTimer.Tick += TimeMarkerTimer_Tick;

            bleScanTimer.Interval = TimeSpan.FromSeconds(2);
            bleScanTimer.Tick += BleScanTimer_Tick;

            bleConnectionCheckTimer.Interval = TimeSpan.FromSeconds(10);
            bleConnectionCheckTimer.Tick += BleConnectionCheckTimer_Tick;

            systemTimer.Interval = TimeSpan.FromMilliseconds(250);
            systemTimer.Tick += SystemTimer_Tick;
            systemTimer.Start();

            timelineWidth = (double)timeline_canvas.Width * 19;
            timelineHeight = (double)timeline_canvas.Height * 2;

            MenuFlyoutItem playback_mode_trigger = new MenuFlyoutItem();
            playback_mode_trigger.Text = "Trigger Mode";
            playback_mode_trigger.Click += Playback_mode_trigger_Click;

            MenuFlyoutItem playback_mode_loop = new MenuFlyoutItem();
            playback_mode_loop.Text = "Loop Mode";
            playback_mode_loop.Click += Playback_mode_loop_Click;

            playbackMenu.Items.Add(playback_mode_trigger);
            playbackMenu.Items.Add(playback_mode_loop);

            MenuFlyoutItem split = new MenuFlyoutItem();
            split.Text = "Split Block";
            split.Click += Split_Click;

            MenuFlyoutItem delete = new MenuFlyoutItem();
            delete.Text = "Delete";
            delete.Click += Delete_Click;

            MenuFlyoutItem type = new MenuFlyoutItem();
            type.Text = "Change PWM";
            type.Click += Type_Click;

            blockMenu.Items.Add(split);
            blockMenu.Items.Add(delete);
            blockMenu.Items.Add(type);

            rPin_1.Text = "Relay 1";
            rPin_1.Click += RPin_Click;

            rPin_2.Text = "Relay 2";
            rPin_2.Click += RPin_Click;

            rPin_3.Text = "Relay 3";
            rPin_3.Click += RPin_Click;

            rPin_4.Text = "Relay 4";
            rPin_4.Click += RPin_Click;

            rPin_5.Text = "Relay 5";
            rPin_5.Click += RPin_Click;

            rPin_6.Text = "Relay 6";
            rPin_6.Click += RPin_Click;

            rPin_7.Text = "Relay 7";
            rPin_7.Click += RPin_Click;

            rPin_none.Text = "Unassign";
            rPin_none.Click += RPin_Click;

            rowPinMenu.Items.Add(rPin_1);
            rowPinMenu.Items.Add(rPin_2);
            rowPinMenu.Items.Add(rPin_3);
            rowPinMenu.Items.Add(rPin_4);
            rowPinMenu.Items.Add(rPin_5);
            rowPinMenu.Items.Add(rPin_6);
            rowPinMenu.Items.Add(rPin_7);
            rowPinMenu.Items.Add(rPin_none);

            outputMapping.Add("Unassign", 99);
            outputMapping.Add("Relay 1", D1);
            outputMapping.Add("Relay 2", D2);
            outputMapping.Add("Relay 3", D3);
            outputMapping.Add("Relay 4", D4);
            outputMapping.Add("Relay 5", D5);
            outputMapping.Add("Relay 6", D6);
            outputMapping.Add("Relay 7", D8);


            MenuFlyoutItem ambientTrack_1 = new MenuFlyoutItem();
            ambientTrack_1.Text = "1";
            ambientTrack_1.Click += ambientTrack_Click;

            MenuFlyoutItem ambientTrack_2 = new MenuFlyoutItem();
            ambientTrack_2.Text = "2";
            ambientTrack_2.Click += ambientTrack_Click;

            MenuFlyoutItem ambientTrack_3 = new MenuFlyoutItem();
            ambientTrack_3.Text = "3";
            ambientTrack_3.Click += ambientTrack_Click;

            MenuFlyoutItem ambientTrack_4 = new MenuFlyoutItem();
            ambientTrack_4.Text = "0";
            ambientTrack_4.Click += ambientTrack_Click;

            ambientMenu.Items.Add(ambientTrack_1);
            ambientMenu.Items.Add(ambientTrack_2);
            ambientMenu.Items.Add(ambientTrack_3);
            ambientMenu.Items.Add(ambientTrack_4);

            MenuFlyoutItem scareTrack_1 = new MenuFlyoutItem();
            scareTrack_1.Text = "1";
            scareTrack_1.Click += scareTrack_Click;

            MenuFlyoutItem scareTrack_2 = new MenuFlyoutItem();
            scareTrack_2.Text = "2";
            scareTrack_2.Click += scareTrack_Click;

            MenuFlyoutItem scareTrack_3 = new MenuFlyoutItem();
            scareTrack_3.Text = "3";
            scareTrack_3.Click += scareTrack_Click;

            MenuFlyoutItem scareTrack_4 = new MenuFlyoutItem();
            scareTrack_4.Text = "0";
            scareTrack_4.Click += scareTrack_Click;

            scareMenu.Items.Add(scareTrack_1);
            scareMenu.Items.Add(scareTrack_2);
            scareMenu.Items.Add(scareTrack_3);
            scareMenu.Items.Add(scareTrack_4);

            // Grid Spacing
            // -- 100px = 1000ms
            // -- 10px  = 100ms

            timeMarker = new TextBlock();
            timeMarker.FontSize = 5;
            timeMarker.Text = "0";
            timeMarker.SetValue(Canvas.LeftProperty, 0);
            timeMarker.SetValue(Canvas.TopProperty, -2);

            timeMarker_line_current = new Line();
            timeMarker_line_current.X1 = 0;
            timeMarker_line_current.Y1 = 0;
            timeMarker_line_current.X2 = 0;
            timeMarker_line_current.Y2 = timelineHeight;
            timeMarker_line_current.Stroke = green;
            timeMarker_line_current.StrokeThickness = 5;
            timeMarker_line_current.Opacity = .4f;

            timeMarker_line_end = new Line();
            timeMarker_line_end.X1 = 0;
            timeMarker_line_end.Y1 = 0;
            timeMarker_line_end.X2 = 0;
            timeMarker_line_end.Y2 = timelineHeight;
            timeMarker_line_end.Stroke = red;
            timeMarker_line_end.StrokeThickness = 5;
            timeMarker_line_end.Opacity = .4f;

            scrubber_arrow_start = new Windows.UI.Xaml.Controls.Image();
            scrubber_arrow_start.Source = new BitmapImage(new Uri("ms-appx:///Assets/arrow1.png"));
            scrubber_arrow_start.Height = 8;
            scrubber_arrow_start.Width = 8;
            scrubber_arrow_start.SetValue(Canvas.TopProperty, 11);
            scrubber_arrow_start.SetValue(Canvas.LeftProperty, currentMarkerPosition - 4);
            scrubber_arrow_start.SetValue(Canvas.ZIndexProperty, 10);

            scrubber_arrow_end = new Windows.UI.Xaml.Controls.Image();
            scrubber_arrow_end.Source = new BitmapImage(new Uri("ms-appx:///Assets/arrow2.png"));
            scrubber_arrow_end.Height = 8;
            scrubber_arrow_end.Width = 8;
            scrubber_arrow_end.SetValue(Canvas.TopProperty, 11);
            scrubber_arrow_end.SetValue(Canvas.LeftProperty, endMarkerPosition - 4);
            scrubber_arrow_end.SetValue(Canvas.ZIndexProperty, 10);

            scrubber_arrow_start.PointerPressed += timeMarker_line_current_PointerPressed;
            scrubber_arrow_end.PointerPressed += timeMarker_line_end_PointerPressed;


            //string jsonString = "Test";//await FileIO.ReadTextAsync(await StorageFile.GetFileFromApplicationUriAsync(new Uri("ms-appx:///Assets/MyData.json")));
            //var rootObject = JsonObject.Parse(jsonString);
            //ystem.Diagnostics.Debug.WriteLine(rootObject["myJsonProperty"]);
        }

        private void Playback_mode_loop_Click(object sender, RoutedEventArgs e)
        {
            play_mode_combo_box.Text = "Loop";
            JsonObject jsonObject = new JsonObject();
            jsonObject["edit"] = JsonValue.CreateStringValue("option");
            jsonObject["loop"] = JsonValue.CreateBooleanValue(true);
            sendMessage(jsonObject);
        }
        private void Playback_mode_trigger_Click(object sender, RoutedEventArgs e)
        {
            play_mode_combo_box.Text = "Trigger";

            JsonObject jsonObject = new JsonObject();
            jsonObject["edit"] = JsonValue.CreateStringValue("option");
            jsonObject["loop"] = JsonValue.CreateBooleanValue(false);
            sendMessage(jsonObject);
        }
        private void ambientTrack_Click(object sender, RoutedEventArgs e)
        {
            MenuFlyoutItem mf = sender as MenuFlyoutItem;
            ambient_combo_box.Text = mf.Text;
            JsonObject jsonObject = new JsonObject();

            jsonObject["edit"] = JsonValue.CreateStringValue("option");
            jsonObject["ambient"] = JsonValue.CreateNumberValue(Convert.ToDouble(mf.Text));
            sendMessage(jsonObject);
        }
        private void scareTrack_Click(object sender, RoutedEventArgs e)
        {
            MenuFlyoutItem mf = sender as MenuFlyoutItem;
            scare_combo_box.Text = mf.Text;

            JsonObject jsonObject = new JsonObject();
            jsonObject["edit"] = JsonValue.CreateStringValue("option");
            jsonObject["scare"] = JsonValue.CreateNumberValue(Convert.ToDouble(mf.Text));
            sendMessage(jsonObject);
        }
        private void RPin_Click(object sender, RoutedEventArgs e)
        {
            MenuFlyoutItem mf = sender as MenuFlyoutItem;
            pin_selection_box.Text = mf.Text;
        }
        private void Type_Click(object sender, RoutedEventArgs e)
        {
            //-- Open Block Sub Editor -- 
            toggleBlockConfigurationWindow(true);
        }
        private void Delete_Click(object sender, RoutedEventArgs e)
        {
            if (selectedBlock != null)
            {
                timelineBlock tb = timelineBlockList.Single(q => q.block == selectedBlock);
                JsonObject jsonObject = new JsonObject();
                jsonObject["remove"] = JsonValue.CreateStringValue("block");
                jsonObject["uniqueID"] = JsonValue.CreateNumberValue(tb.uID);

                sendMessage(jsonObject);
                activeBlockIDs.Remove(tb.uID);
                timelineBlockList.Remove(tb);
                timeline_canvas.Children.Remove(selectedBlock);
                selectedBlock = null;
            }
        }
        private void Split_Click(object sender, RoutedEventArgs e)
        {
            if (selectedBlock != null)
            {
                timelineBlock tb = timelineBlockList.Single(q => q.block == selectedBlock);
                tb.split();
                double x = (double)selectedBlock.GetValue(Canvas.LeftProperty);
                double y = (double)selectedBlock.GetValue(Canvas.TopProperty);
                double width = tb.endTime - tb.startTime;
                createBasicBlock(x + width, y, width, -1, tb.rowID, 100, 100);
                triggerBlockUpdates();
                clearHighlights();
                selectedBlock = null;
            }
        }
        private void createBasicBlock(double x, double y, double width, int uid, int rid, int pwmStart, int pwmEnd)
        {
            Border timeBlock = new Border();
            timeBlock.Background = gray;
            timeBlock.Width = width;
            timeBlock.Height = blockHeight;
            timeBlock.PointerPressed += TimeBlock_PointerPressed;
            //timeBlock.PointerReleased += TimeBlock_PointerReleased;
            timeBlock.PointerEntered += TimeBlock_PointerEntered;
            timeBlock.PointerExited += TimeBlock_PointerExited;
            timeBlock.DoubleTapped += TimeBlock_DoubleTapped;
            timeBlock.CornerRadius = new CornerRadius(3, 3, 3, 3);
            timeBlock.SetValue(Canvas.LeftProperty, x);
            timeBlock.SetValue(Canvas.TopProperty, y);
            TextBlock textBlock = new TextBlock();
            textBlock.Text = (timeBlock.Width * 10).ToString();
            double newFont = 8 / (zoom_scale * .8);
            if (newFont > 4)
            {
                newFont = 4;
            }
            textBlock.FontSize = newFont;
            textBlock.HorizontalAlignment = HorizontalAlignment.Center;
            textBlock.VerticalAlignment = VerticalAlignment.Center;
            textBlock.FontWeight = FontWeights.Bold;
            timeBlock.Child = textBlock;
            timeline_canvas.Children.Add(timeBlock);
            snapBlock(timeBlock);

            if (uid > -1)
            {
                activeBlockIDs.Add(uid);
                timelineBlockList.Add(new timelineBlock(timeBlock, uid, rid, (int)x, (int)(x + width), pwmStart, pwmEnd));  
            }
            else
            {

                //-- New Block Creation --
                bool uniqueID_Found = false;
                int uID = 0;
                while (!uniqueID_Found)
                {
                    uID += 1;
                    uniqueID_Found = activeBlockIDs.Add(uID);
                }

                if (rid > -1)
                {
                    timelineBlockList.Add(new timelineBlock(timeBlock, XPanTracker, YPanTracker, uID, rid, true, pwmStart, pwmEnd));
                }
               
            }
            updateBlockText(timeBlock);
        }
        private async void SystemTimer_Tick(object sender, object e)
        {
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
            {
                lock (this)
                {
                    switch (SYSTEM_STATE)
                    {

                        case SYSTEM_START:
                            initializeTimelineGrid();
                            initializeTimelineScrubber();

                            SYSTEM_STATE = SYSTEM_IDLE;
                            break;

                        case SYSTEM_IDLE:
                            scanForDevice();

                            break;

                        case SYSTEM_BLE_CONNECTING:

                            if (!bleConnectionCheckTimer.IsEnabled)
                            {
                                bleConnectionCheckTimer.Start();
                            }
                            if (!BLE_Connection_Sent)
                            {
                                BLE_Connection_Sent = true;
                                scannerDisable();
                                attemptConnection();
                            }

                            if (BLE_Connection_Rejection)
                            {
                                BLE_Connection_Sent = false;
                                BLE_Connection_Rejection = false;
                                BLE_Connection_Accepted = false;
                                SYSTEM_STATE = SYSTEM_BLE_DISCONNECTED;
                            }

                            if (BLE_Connection_Accepted)
                            {
                                BLE_Connection_Sent = false;
                                BLE_Connection_Rejection = false;
                                BLE_Connection_Accepted = false;
                                SYSTEM_STATE = SYSTEM_BLE_CONNECTED;
                            }
                            break;

                        case SYSTEM_BLE_CONNECTED:
                            if (!timelineLoaded)
                            {
                                SYSTEM_STATE = SYSTEM_BLE_REQUEST_DEVICE_DATA;
                                timelineRequest_Sent = false;
                                timelineRequest_Complete = false;
                            }
                            break;

                        case SYSTEM_BLE_DISCONNECTING:
                            requestDisconnection();
                            SYSTEM_STATE = SYSTEM_BLE_DISCONNECTED;

                            break;

                        case SYSTEM_BLE_DISCONNECTED:
                            SYSTEM_STATE = SYSTEM_IDLE;
                            break;

                        case SYSTEM_BLE_REQUEST_DEVICE_DATA:
                            if (!timelineRequest_Complete)
                            {
                                if (!timelineRequest_Sent)
                                {
                                    timelineRequest_Sent = true;
                                    JsonObject jsonObject = new JsonObject();
                                    jsonObject["request"] = JsonValue.CreateStringValue("device information");
                                    sendMessage(jsonObject);
                                    load_bar.Value = 0;
                                    load_bar.Maximum = 100;
                                    toggleLoadWindow(OPEN);
                                }

                            }
                            else
                            {
                                SYSTEM_STATE = SYSTEM_BLE_CONNECTED;
                                timelineLoaded = true;
                                toggleTimelineWindow(OPEN);
                            }
                            break;

                        case SYSTEM_BLE_DROPPED:
                            forceDisconnection();
                            SYSTEM_STATE = SYSTEM_BLE_DISCONNECTED;
                            break;
                    }

                    system_state_label.Text = getSystemState();
                }
            });
        }
        private string getSystemState()
        {
            switch (SYSTEM_STATE)
            {

                case SYSTEM_START:
                    return "SYSTEM_START";

                case SYSTEM_IDLE:
                    return "SYSTEM_IDLE";

                case SYSTEM_BLE_CONNECTING:
                    return "SYSTEM_BLE_CONNECTING";

                case SYSTEM_BLE_CONNECTED:
                    return "SYSTEM_BLE_CONNECTED";

                case SYSTEM_BLE_DISCONNECTING:
                    return "SYSTEM_BLE_DISCONNECTING";

                case SYSTEM_BLE_DISCONNECTED:
                    return "SYSTEM_DISCONNECTED";

                case SYSTEM_BLE_REQUEST_DEVICE_DATA:
                    return "SYSTEM_BLE_REQUEST_DEVICE_DATA";

                case SYSTEM_BLE_DROPPED:
                    return "SYSTEM_BLE_DROPPED";

            }
            return "UNKNOWN";
        }
        private void UIElement_OnLostFocus(object sender, RoutedEventArgs e)
        {
            
        }

        //--------------------------------------------------------------------------------------
        private void initializeTimelineScrubber()
        {
            resetMarker();
            //timeline_canvas.Children.Add(timeMarker);
            timeline_canvas.Children.Add(timeMarker_line_current);
            timeline_canvas.Children.Add(timeMarker_line_end);
        }
        private void initializeTimelineGrid()
        {
            XPanTracker = 0;
            YPanTracker = 0;
            timeline_canvas.Children.Clear();
            timeline_ticks_box.Children.Clear();
            block_id_box.Children.Clear();
            rowControllers.Clear();

            block_id_box.Children.Add(add_output_border);
            add_output_border.SetValue(Canvas.TopProperty, 0);
            timeline_ticks_box.Children.Add(scrubber_arrow_start);
            timeline_ticks_box.Children.Add(scrubber_arrow_end);

            for (double i = 0; i < timelineWidth; i++)
            {
                if (i % 10 == 0)
                {
                    Line line = new Line();
                    line.X1 = i;
                    line.Y1 = 0;
                    line.X2 = i;
                    line.Y2 = timelineHeight;
                    line.StrokeThickness = .5;
                    line.Stroke = light_gray;
                    if (i % 50 == 0)
                    {
                        line.Stroke = yellow;
                        TextBlock textBlock = new TextBlock();
                        textBlock.FontSize = 6;
                        double tick_value = i / 100;

                        textBlock.Text = tick_value + "s";
                        textBlock.SetValue(Canvas.LeftProperty, i - 3);
                        if (tick_value > 0)
                            timeline_ticks_box.Children.Add(textBlock);
                    }
                    line.Opacity = .3f;
                    timeline_canvas.Children.Add(line);


                }
            }

            for (int i = 0; i < timelineHeight; i++)
            {
                if (i % 15 == 0)
                {
                    Line line = new Line();
                    line.X1 = 0;
                    line.Y1 = i;
                    line.X2 = timelineWidth;
                    line.Y2 = i;
                    line.StrokeThickness = .5;
                    line.Stroke = light_gray;
                    line.Opacity = .3f;
                    timeline_canvas.Children.Add(line);

                    Rectangle blockZone = new Rectangle();
                    blockZone.Name = i.ToString();
                    blockZone.Fill = null;
                    blockZone.Opacity = .4f;
                    blockZone.Width = timelineWidth;
                    blockZone.Height = 11;
                    blockZone.SetValue(Canvas.LeftProperty, 0);
                    blockZone.SetValue(Canvas.TopProperty, i + 2);
                    timeline_canvas.Children.Add(blockZone);
                }
            }
        }
        private void resetMarker()
        {
            currentMarkerPosition = 5 - XPanTracker;
            markerPosition_corrected = 5;
            prevMillis = 0;
            stopwatch.Reset();
            scrubber_arrow_start.SetValue(Canvas.LeftProperty, currentMarkerPosition - 3);
            scrubber_arrow_end.SetValue(Canvas.LeftProperty, endMarkerPosition - 3 - XPanTracker);
            timeMarker_line_current.SetValue(Canvas.LeftProperty, currentMarkerPosition);
            timeMarker_line_end.SetValue(Canvas.LeftProperty, endMarkerPosition - XPanTracker);
        }
        private void toggleLoadWindow(bool open)
        {
            if (open)
            {
                load_border.Visibility = Visibility.Visible;

            }
            else
            {
                load_border.Visibility = Visibility.Collapsed;
            }
        }
        private void toggleTimelineWindow(bool open)
        {
            if (open)
            {
                main_timeline_border.Visibility = Visibility.Visible;
                load_border.Visibility = Visibility.Collapsed;
                XPanTracker = 0;
            }
            else
            {
                main_timeline_border.Visibility = Visibility.Collapsed;
            }
        }
        private void toggleScanWindow(bool open)
        {
            if (open)
            {
                ble_border.Visibility = Visibility.Visible;
                ble_connection_border.Visibility = Visibility.Collapsed;
                active_controller_border.Visibility = Visibility.Collapsed;
            }
            else
            {
                ble_border.Visibility = Visibility.Collapsed;
            }
        }
        private void toggleConnectingWindow(bool open)
        {
            if (open)
            {
                ble_connection_border.Visibility = Visibility.Visible;
                ble_border.Visibility = Visibility.Collapsed;
                active_controller_border.Visibility = Visibility.Collapsed;
            }
            else
            {
                ble_connection_border.Visibility = Visibility.Collapsed;
            }
        }
        private void toggleDeviceWindow(bool open)
        {
            if (open)
            {
                active_controller_border.Visibility = Visibility.Visible;
                ble_connection_border.Visibility = Visibility.Collapsed;
                ble_border.Visibility = Visibility.Collapsed;

            }
            else
            {
                active_controller_border.Visibility = Visibility.Collapsed;
            }
        }
        private void toggleOutputConfigurationWindow(bool open)
        {
            if(open)
            {
                row_controller_menu.Visibility = Visibility.Visible;
                exit_canvas.Visibility = Visibility.Visible;
            }
            else
            {
                row_controller_menu.Visibility = Visibility.Collapsed;
                exit_canvas.Visibility = Visibility.Collapsed;
                if(selectedRow != null)
                {
                    uint _pin = pinLookup(pin_selection_box.Text);
                    unassignOutputToPin(selectedRow.assignedPin);
                    selectedRow.assignedPin = _pin;
                    assignOutputToPin(selectedRow.assignedPin);
                    selectedRow.assignedType = 1;
                    if (row_description_edit.Text.Length > 1)
                    {
                        selectedRow.description = row_description_edit.Text;
                    }
                    else
                    {
                        outputDiscriptionShortDialog();
                    }
                    TextBlock tb = selectedRow.block.Child as TextBlock;
                    tb.Text = selectedRow.description;

                    sendMessage(selectedRow.getBlockData());
                }
            }
        }
        private void toggleBlockConfigurationWindow(bool open)
        {
            if (open)
            {
                block_control_menu.Visibility = Visibility.Visible;
                exit_canvas.Visibility = Visibility.Visible;

                if (selectedBlock != null)
                {
                    timelineBlock tb = timelineBlockList.Single(t => t.block == selectedBlock);
                    pwm_start_slider.Value = tb.pwmStart;
                    pwm_end_slider.Value = tb.pwmEnd;
                }
            }
            else
            {
                block_control_menu.Visibility = Visibility.Collapsed;
                exit_canvas.Visibility = Visibility.Collapsed;
                if (selectedBlock != null)
                {
                    timelineBlock tb = timelineBlockList.Single(t => t.block == selectedBlock);
                    tb.pwmStart = (int)pwm_start_slider.Value;
                    tb.pwmEnd = (int)pwm_end_slider.Value;                    
                    sendMessage(tb.getBlockData());
                }
            }
        }
        private uint pinLookup(string key)
        {
            uint pin = 99;
            bool found = outputMapping.TryGetValue(key, out pin);
            return pin;
        }
        private void timeMarker_line_current_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            markerPan_start = true;
        }
        private void timeMarker_line_end_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            markerPan_end = true;
        }
        private void TimeMarkerTimer_Tick(object sender, object e)
        {
            double mark_time = currentMarkerPosition - XPanTracker;
            long currentMillis = stopwatch.ElapsedMilliseconds / 10;

            long tDelta = currentMillis - prevMillis;
            prevMillis = currentMillis;

            if (currentMarkerPosition > endMarkerPosition)
            {
                resetMarker();
                timeMarkerTimer.Stop();
                stopwatch.Reset();

                JsonObject jsonObject = new JsonObject();
                jsonObject["request"] = JsonValue.CreateStringValue("stop");
                //sonObject["uniqueID"] = JsonValue.CreateNumberValue(34);
                //Debug.WriteLine(jsonObject.Stringify());

                sendMessage(jsonObject);
                clearHighlights();
                return;
            }

            if (mark_time < 200)
            {
                currentMarkerPosition += tDelta;
                timeMarker.SetValue(Canvas.LeftProperty, mark_time);
                timeMarker_line_current.SetValue(Canvas.LeftProperty, mark_time);
                scrubber_arrow_start.SetValue(Canvas.LeftProperty, mark_time - 3);
            }
            else
            {


                XPanTracker += tDelta;

                foreach (var border in timeline_canvas.Children)
                {
                    border.SetValue(Canvas.LeftProperty, (double)border.GetValue(Canvas.LeftProperty) - tDelta);
                }
                foreach (var border in timeline_ticks_box.Children)
                {
                    border.SetValue(Canvas.LeftProperty, (double)border.GetValue(Canvas.LeftProperty) - tDelta);
                }
                currentMarkerPosition += tDelta;

                timeMarker.SetValue(Canvas.LeftProperty, 200);
                timeMarker_line_current.SetValue(Canvas.LeftProperty, 200);

                scrubber_arrow_start.SetValue(Canvas.LeftProperty, 200 - 3);

            }
            int CMP = (int)currentMarkerPosition;
            foreach (timelineBlock tb in timelineBlockList)
            {
                if (CMP > tb.startTime && CMP < tb.endTime)
                {
                    highlightBlock(tb.block, true);
                }
                else
                {
                    highlightBlock(tb.block, false);
                }
            }

            timeMarker.Text = CMP.ToString();
            markerPosition_corrected = CMP;

        }
        private void PinchDebounceTimer_Tick(object sender, object e)
        {
            pinchDebounceTimer.Stop();
            isScaling = false;
        }
        private void mouseHoldTimer_Tick(object sender, object e)
        {
            isPanning = true;
            mouseHoldTimer.Stop();
        }
        private void TimeBlock_PointerEntered(object sender, PointerRoutedEventArgs e)
        {
            if (isScaling) { e.Handled = true; return; }
            if (isHeld) { return; }
            Border rect = (Border)sender;
            highlightBlock(rect, true);
            selectedBlock = rect;
            markerPan_end = false;
            markerPan_start = false;
        }
        private void TimeBlock_PointerExited(object sender, PointerRoutedEventArgs e)
        {
            if (isScaling) { e.Handled = true; return; }
            Border rect = (Border)sender;
            if (selectedBlock != rect) { return; }

            if (!isDraggingLeft && !isDraggingRight && !isHeld)
            {
                highlightBlock(rect, false);
                selectedBlock = null;
            }
            markerPan_end = false;
            markerPan_start = false;
        }
        private void TimeBlock_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            if (isScaling) { e.Handled = true; return; }
            Border rect = (Border)sender;
            selectedBlock = rect;
            markerPan_end = false;
            markerPan_start = false;
        }
        private void highlightBlock(Border parentBlock, bool isSelected)
        {
            if (isSelected)
            {
                parentBlock.Background = green;
                parentBlock.BorderBrush = white;
                parentBlock.BorderThickness = highlight_thickness;
            }
            else
            {
                parentBlock.Background = gray;
                parentBlock.BorderBrush = white;
                parentBlock.BorderThickness = normal_thickness;
                Window.Current.CoreWindow.PointerCursor = new CoreCursor(CoreCursorType.Arrow, 1);
            }

        }
        private void timeline_canvas_PointerMoved(object sender, PointerRoutedEventArgs e)
        {
            if (isScaling) { e.Handled = true; return; }
            PointerPoint p = e.GetCurrentPoint(timeline_canvas);
            Point point = p.Position;
            if (!isPanning)
            {
                if (selectedBlock != null)
                {
                    highlightBlock(selectedBlock, true);
                    double rect_x = (double)selectedBlock.GetValue(Canvas.LeftProperty);
                    double rect_y = (double)selectedBlock.GetValue(Canvas.TopProperty);

                    if ((point.X > (rect_x + selectedBlock.Width) - blockEdgeSize) || point.X < rect_x + blockEdgeSize)
                    {
                        Window.Current.CoreWindow.PointerCursor = new CoreCursor(CoreCursorType.SizeWestEast, 1);
                    }
                    else
                    {
                        Window.Current.CoreWindow.PointerCursor = new CoreCursor(CoreCursorType.Arrow, 1);
                    }

                    if (isDraggingLeft)
                    {
                        if (point.X <= (rect_x + selectedBlock.Width - 5) && rect_x > 1)
                        {
                            double offset = point.X - rect_x;
                            selectedBlock.SetValue(Canvas.LeftProperty, point.X);
                            selectedBlock.Width += -offset;
                            updateBlockText(selectedBlock);
                        }
                    }
                    else
                    if (isDraggingRight)
                    {
                        if (point.X >= (rect_x + 5))
                        {
                            double offset = point.X - (rect_x + selectedBlock.Width);
                            selectedBlock.Width += offset;
                            updateBlockText(selectedBlock);
                        }
                    }
                    else
                    {
                        if (isHeld)
                        {
                            Point delta = prevPointer.Position;
                            double x_delta = point.X - delta.X;
                            double y_delta = point.Y - delta.Y;
                            if ((rect_x + x_delta) > 1 - XPanTracker)
                            {
                                selectedBlock.SetValue(Canvas.LeftProperty, x_delta + rect_x);
                            }
                            else
                            {
                                //timeline_canvas_PointerExited(null, null);
                            }

                            if ((rect_y + y_delta) > 1 - YPanTracker)
                            {
                                selectedBlock.SetValue(Canvas.TopProperty, y_delta + rect_y);
                            }
                            else
                            {
                                //timeline_canvas_PointerExited(null, null);
                            }


                            
                            prevPointer = p;
                            timelineBlock tb = timelineBlockList.Single(t => t.block == selectedBlock);
                            foreach (var border in timeline_canvas.Children)
                            {
                                if (border.GetType() == typeof(Rectangle))
                                {
                                    Rectangle rect = (Rectangle)border;
                                    double targetX = (double)border.GetValue(Canvas.TopProperty) + 1;
                                    double parentX = (double)selectedBlock.GetValue(Canvas.TopProperty);
                                    double posdelta = Math.Abs(targetX - parentX);

                                    if (posdelta < 7)
                                    {
                                        if ((targetX + YPanTracker) / 15  < 7)
                                        {
                                            rect.Fill = green;
                                        }
                                        else
                                        {
                                            rect.Fill = red;
                                        }
                                    }
                                    else
                                    {
                                        rect.Fill = null;
                                    }
                                }
                            }
                        }
                    }
                }
            }
            else
            {
                if (markerPan_start)
                {
                    if (prevPointer != null)
                    {
                        Point delta = prevPointer.Position;
                        double x_delta = point.X - delta.X;

                        double line_x = (double)scrubber_arrow_start.GetValue(Canvas.LeftProperty);

                        if ((line_x + x_delta) > 1 - XPanTracker)
                        {
                            double marker_location = x_delta + line_x;
                            marker_location += XPanTracker;
                            currentMarkerPosition = marker_location;

                            timeMarker_line_current.SetValue(Canvas.LeftProperty, (x_delta + line_x) + 4);
                            scrubber_arrow_start.SetValue(Canvas.LeftProperty, (x_delta + line_x));
                            markerPosition_corrected = currentMarkerPosition;

                        }

                        int CMP = (int)currentMarkerPosition;
                        bool stateChange = false;

                        foreach (timelineBlock tb in timelineBlockList)
                        {
                            if (CMP >= tb.startTime + 1 && CMP <= tb.endTime - 1)
                            {
                                highlightBlock(tb.block, true);
                                tb.Start();
                            }
                            else
                            {
                                highlightBlock(tb.block, false);
                                tb.Stop();
                            }
                        }

                        if (realtimeScrubber)
                        {
                            if ((realtimeLockout.ElapsedMilliseconds - prevRealtimeLockout) > 150)
                            {
                                foreach (timelineBlock tb in timelineBlockList)
                                {
                                    if (tb.hasStateChange)
                                    {
                                        stateChange = true;
                                    }
                                    tb.clearStateChange();
                                }
                                if (stateChange)
                                {
                                    JsonObject jsonObject = new JsonObject();
                                    jsonObject["request"] = JsonValue.CreateStringValue("freeze");
                                    jsonObject["frameID"] = JsonValue.CreateNumberValue(CMP);
                                    //Debug.WriteLine(jsonObject.Stringify());
                                    sendMessage(jsonObject);
                                    prevRealtimeLockout = realtimeLockout.ElapsedMilliseconds;
                                    alternateRealtimeLockout = realtimeLockout.ElapsedMilliseconds;
                                }
                                else
                                {
                                    if ((realtimeLockout.ElapsedMilliseconds - alternateRealtimeLockout) > 150)
                                    {
                                        JsonObject jsonObject = new JsonObject();
                                        jsonObject["request"] = JsonValue.CreateStringValue("freeze");
                                        jsonObject["frameID"] = JsonValue.CreateNumberValue(CMP);
                                        //Debug.WriteLine(jsonObject.Stringify());
                                        sendMessage(jsonObject);
                                        alternateRealtimeLockout = realtimeLockout.ElapsedMilliseconds;
                                    }
                                }
                            }


                        }
                    }
                    prevPointer = p;
                }

                if (markerPan_end)
                {
                    if (prevPointer != null)
                    {
                        Point delta = prevPointer.Position;
                        double x_delta = point.X - delta.X;
                        double line_x = (double)scrubber_arrow_end.GetValue(Canvas.LeftProperty);

                        if ((line_x + x_delta) > 1 - XPanTracker)
                        {
                            double marker_location = x_delta + line_x;
                            marker_location += XPanTracker;
                            endMarkerPosition = marker_location;
                            timeMarker_line_end.SetValue(Canvas.LeftProperty, (x_delta + line_x) + 4);
                            scrubber_arrow_end.SetValue(Canvas.LeftProperty, (x_delta + line_x));
                        }
                        endtimeChanged = true;
                    }
                    prevPointer = p;
                }


                if (isPanning)
                {
                    if (prevPointer != null)
                    {
                        Point delta = prevPointer.Position;
                        double x_delta = -(point.X - delta.X);
                        double y_delta = -(point.Y - delta.Y);
                        bool positiveX = false;
                        bool negativeX = false;
                        bool positiveY = false;
                        bool negativeY = false;

                        if (x_delta > 0)
                        {
                            positiveX = true;
                        }
                        else
                        {
                            negativeX = true;
                        }
                        if (y_delta > 0)
                        {
                            positiveY = true;
                        }
                        else
                        {
                            negativeY = true;
                        }


                        if (negativeX)
                        {
                            if (XPanTracker > 0)
                            {
                                XPanTracker += x_delta * xPanSpeed;

                                foreach (var border in timeline_canvas.Children)
                                {
                                    border.SetValue(Canvas.LeftProperty, (double)border.GetValue(Canvas.LeftProperty) - x_delta * xPanSpeed);

                                }
                                foreach (var border in timeline_ticks_box.Children)
                                {
                                    border.SetValue(Canvas.LeftProperty, (double)border.GetValue(Canvas.LeftProperty) - x_delta * xPanSpeed);

                                }
                            }
                        }
                        if (positiveX)
                        {
                            if (XPanTracker < timelineWidth * .8)
                            {
                                XPanTracker += x_delta * xPanSpeed;
                                foreach (var border in timeline_canvas.Children)
                                {
                                    border.SetValue(Canvas.LeftProperty, (double)border.GetValue(Canvas.LeftProperty) - x_delta * xPanSpeed);
                                }
                                foreach (var border in timeline_ticks_box.Children)
                                {
                                    border.SetValue(Canvas.LeftProperty, (double)border.GetValue(Canvas.LeftProperty) - x_delta * xPanSpeed);
                                }
                            }
                        }

                        if (negativeY)
                        {
                            if (YPanTracker > 3)
                            {
                                YPanTracker += y_delta * yPanSpeed;
                                foreach (var border in timeline_canvas.Children)
                                {
                                    border.SetValue(Canvas.TopProperty, (double)border.GetValue(Canvas.TopProperty) - y_delta * yPanSpeed);
                                }
                                foreach (var border in block_id_box.Children)
                                {
                                    border.SetValue(Canvas.TopProperty, (double)border.GetValue(Canvas.TopProperty) - y_delta * yPanSpeed);
                                }
                            }
                        }
                        if (positiveY)
                        {
                            if (YPanTracker < 100)
                            {
                                YPanTracker += y_delta * yPanSpeed;
                                foreach (var border in timeline_canvas.Children)
                                {
                                    border.SetValue(Canvas.TopProperty, (double)border.GetValue(Canvas.TopProperty) - y_delta * yPanSpeed);
                                }
                                foreach (var border in block_id_box.Children)
                                {
                                    border.SetValue(Canvas.TopProperty, (double)border.GetValue(Canvas.TopProperty) - y_delta * yPanSpeed);
                                }
                            }
                        }
                    }
                    prevPointer = p;
                }
            }
        }
        private void updateBlockText(Border parentBlock)
        {
            timelineBlock tb = timelineBlockList.Single(e => e.block == parentBlock);
            double rowID = 0;
            double y = (double)tb.block.GetValue(Canvas.TopProperty)+5;
            y += YPanTracker;

            if (y > 10 && y < 25)
            {
                rowID = 1;
            }
            if (y > 25 && y < 40)
            {
                rowID = 2;
            }
            if (y > 40 && y < 55)
            {
                rowID = 3;
            }
            if (y > 55 && y < 70)
            {
                rowID = 4;
            }
            if (y > 70 && y < 85)
            {
                rowID = 5;
            }
            if (y > 85 && y < 100)
            {
                rowID = 6;
            }
            if (y > 100 && y < 115)
            {
                rowID = 7;
            }

            TextBlock textBlock = (TextBlock)parentBlock.Child;
            Int16 block_duration = Convert.ToInt16(parentBlock.Width * 10);

            if (tb.pwmStart == 100 && tb.pwmEnd == 100)
            {
                textBlock.Text = block_duration.ToString();
            }
            else
            {
                textBlock.Text = block_duration.ToString() + ":" + tb.pwmStart.ToString() + "-" + tb.pwmEnd.ToString();
            }

            rowController rc = rowControllers.FirstOrDefault(q => q.rowID == rowID);
            if (rc != null)
            {
                tb.update(XPanTracker, YPanTracker, rc.uID);
            }
            else
            {
                // Delete_Click
                JsonObject jsonObject = new JsonObject();
                jsonObject["remove"] = JsonValue.CreateStringValue("block");
                jsonObject["uniqueID"] = JsonValue.CreateNumberValue(tb.uID);

                sendMessage(jsonObject);
                activeBlockIDs.Remove(tb.uID);
                timelineBlockList.Remove(tb);
                timeline_canvas.Children.Remove(selectedBlock);
                selectedBlock = null;
            }
        }
        private void updateBlock(Border parentBlock)
        {
            highlightBlock(parentBlock, false);
            snapBlock(parentBlock);
            updateBlockText(parentBlock);
        }
        private void snapBlock(Border parentBlock)
        {
            if (parentBlock != null)
            {
                double xSnap = 5;
                parentBlock.Width = Math.Round(parentBlock.Width / 5) * 5;
                parentBlock.SetValue(Canvas.LeftProperty, (Math.Round((double)parentBlock.GetValue(Canvas.LeftProperty) / xSnap) * xSnap));
                foreach (var border in timeline_canvas.Children)
                {
                    if (border.GetType() == typeof(Rectangle))
                    {
                        Rectangle rect = (Rectangle)border;
                        double targetY = (double)border.GetValue(Canvas.TopProperty) + 1;
                        double parentY = (double)parentBlock.GetValue(Canvas.TopProperty);
                        double posdelta = Math.Abs(targetY - parentY);

                        if (posdelta < 7)
                        {
                            parentBlock.SetValue(Canvas.TopProperty, targetY);
                            rect.Fill = null;
                            break;
                        }
                    }
                }
            }
        }
        private void timeline_canvas_PointerReleased(object sender, PointerRoutedEventArgs e)
        {
            mouseHoldTimer.Stop();
            if (isScaling) { e.Handled = true; return; }

            isDraggingLeft = false;
            isDraggingRight = false;
            isPanning = false;
            markerPan_start = false;
            markerPan_end = false;
            if (selectedBlock != null)
            {
                updateBlock(selectedBlock);
                if (!isHeld)
                {
                    selectedBlock = null;
                }
                isHeld = false;
            }
            Window.Current.CoreWindow.PointerCursor = new CoreCursor(CoreCursorType.Arrow, 1);
            prevPointer = null;

            if (endtimeChanged)
            {
                JsonObject jsonObject = new JsonObject();
                jsonObject["request"] = JsonValue.CreateStringValue("end marker");
                jsonObject["time"] = JsonValue.CreateNumberValue(endMarkerPosition);
                //Debug.WriteLine(jsonObject.Stringify());
                sendMessage(jsonObject);

                endtimeChanged = false;
            }

            triggerBlockUpdates();
        }
        private void triggerBlockUpdates()
        {
            foreach (timelineBlock tb in timelineBlockList)
            {
                if (tb.flagForUpdate)
                {
                    sendMessage(tb.getBlockData());
                }
            }
        }
        private void rescaleTimeline(int delta, bool slow)
        {

            if (delta > 0)
            {
                if (zoom_scale < 2.5)
                {
                    zoom_scale += .05;
                    if (slow)
                    {
                        zoom_scale += -.03;
                    }
                }
            }
            else
            {
                if (zoom_scale > 1)
                {
                    zoom_scale += -.05;
                    if (slow)
                    {
                        zoom_scale += .03;
                    }
                }
            }

            if (zoom_scale == prev_zoom_scale)
                return;

            prev_zoom_scale = zoom_scale;

            float converted_scale = 0f;
            try
            {
                converted_scale = (float)zoom_scale;
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.ToString());
            }

            if (converted_scale < 0)
                return;

            block_id_scroll.ChangeView(null, null, converted_scale);
            timeline_scroll.ChangeView(null, null, converted_scale);
            timeline_ticks_scroll.ChangeView(null, null, converted_scale);
            blockEdgeSize = blockEdgeSize_const - zoom_scale * 1.8;

            foreach (var border in timeline_canvas.Children)
            {
                if (border.GetType() == typeof(Border))
                {
                    Border border1 = (Border)border;
                    TextBlock block = border1.Child as TextBlock;
                    double newFont = 8 / (zoom_scale * .8);
                    if (newFont > 4)
                    {
                        newFont = 4;
                    }
                    block.FontSize = newFont;
                }
            }

            foreach (Border border in block_id_box.Children)
            {
                TextBlock block = border.Child as TextBlock;
                double newFont = 8 / (zoom_scale * .8);

                if (newFont > 5)
                {
                    newFont = 5;
                }

                block.FontSize = newFont;
            }

            foreach (var border in timeline_ticks_box.Children)
            {
                if (border.GetType() == typeof(TextBlock))
                {
                    TextBlock block = (TextBlock)border;
                    double newFont = 8 / (zoom_scale * .8);
                    if (newFont > 8)
                    {
                        newFont = 8;
                    }
                    block.FontSize = newFont;
                }
            }

            scrubber_arrow_start.SetValue(Canvas.TopProperty, 17 - (zoom_scale * 8));
            scrubber_arrow_end.SetValue(Canvas.TopProperty, 17 - (zoom_scale * 8));
        }
        private void timeline_canvas_PointerWheelChanged(object sender, PointerRoutedEventArgs e)
        {
            PointerPoint point = e.GetCurrentPoint(null);
            if (point != null)
            {
                int mousedelta = point.Properties.MouseWheelDelta;
                rescaleTimeline(mousedelta, false);
            }
        }
        private void timeline_canvas_PointerPressed(object sender, PointerRoutedEventArgs e)
        {

            if (isScaling) { e.Handled = true; return; }
            var pointer = e.GetCurrentPoint(timeline_canvas);
            if (pointer.Properties.IsMiddleButtonPressed)
            {
                isPanning = true;
            }
            if (pointer.Properties.IsRightButtonPressed)
            {

            }
            if (selectedBlock != null)
            {
                Border rect = selectedBlock;
                PointerPoint p = e.GetCurrentPoint(timeline_canvas);
                Point point = p.Position;

                if(point.Y > (double)scrubber_arrow.GetValue(Canvas.TopProperty) + 20)
                {
                    markerPan_end = false;
                    markerPan_start = false;
                }

                double rect_x = (double)rect.GetValue(Canvas.LeftProperty);

                if ((point.X > (rect_x + rect.Width) - blockEdgeSize))
                {
                    isDraggingRight = true;
                }
                if (point.X < rect_x + blockEdgeSize)
                {
                    isDraggingLeft = true;
                }
                if (!isDraggingLeft && !isDraggingRight)
                {
                    isHeld = true;
                    prevPointer = p;
                }
            }
            else
            {
                mouseHoldTimer.Start();
            }
        }
        private void timeline_canvas_PointerExited(object sender, PointerRoutedEventArgs e)
        {
            if (isScaling)
            {
                if (!pinchDebounceTimer.IsEnabled)
                {
                    pinchDebounceTimer.Start();
                }
            }
            pointertracker.Clear();
            isDraggingLeft = false;
            isDraggingRight = false;
            isHeld = false;
            isPanning = false;
            if (selectedBlock != null)
            {
                updateBlock(selectedBlock);
            }
            Window.Current.CoreWindow.PointerCursor = new CoreCursor(CoreCursorType.Arrow, 1);
            prevPointer = null;
        }
        private void timeline_canvas_PointerEntered(object sender, PointerRoutedEventArgs e)
        {
            PointerPoint pt = e.GetCurrentPoint(null);

            if (pt.PointerDevice.PointerDeviceType == PointerDeviceType.Touch)
            {
                pointertracker.Add(pt.PointerId);
                if (pointertracker.Count > 1)
                {
                    isScaling = true;
                    isPanning = false;
                    selectedBlock = null;
                }
            }
        }
        private void timeline_canvas_ManipulationDelta(object sender, ManipulationDeltaRoutedEventArgs e)
        {
            float change = e.Delta.Scale * 100;
            if (change > 100)
            {
                rescaleTimeline(1, true);
            }
            else
            {
                rescaleTimeline(-1, true);
            }
        }
        private void timeline_canvas_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
        {
            mouseHoldTimer.Stop();
            isPanning = false;
            if (selectedBlock == null)
            {
                Point p = e.GetPosition(timeline_canvas);
                double generatedWidth = p.X - 1;
                if (generatedWidth > 20)
                {
                    generatedWidth = 20;
                }

                generatedWidth = Math.Round(generatedWidth / 5) * 5;

                if (p.X > 5) // Beyond protected range
                {
                    generateNewTimeblock(-1, p.Y - 5, p.X - generatedWidth, p.X + generatedWidth);
                }
            }
        }
        private void TimeBlock_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
        {
            Border rect = (Border)sender;
            highlightBlock(rect, true);
            selectedBlock = rect;
            blockMenu.ShowAt(selectedBlock);
        }
        private uint getUnusedPin()
        {
            MenuFlyoutItem unused = (MenuFlyoutItem)rowPinMenu.Items.First();
            if(rPin_1 == unused)
            {
                return D1;
            }

            if (rPin_2 == unused)
            {
                return D2;
            }

            if (rPin_3 == unused)
            {
                return D3;
            }

            if (rPin_4 == unused)
            {
                return D4;
            }

            if (rPin_5 == unused)
            {
                return D5;
            }

            if (rPin_6 == unused)
            {
                return D6;
            }

            if (rPin_7 == unused)
            {
                return D8;
            }
            return 99;
        }
        private void generateNewTimeblock(int uid, int rid, int st, int et, int pwmStart, int pwmEnd)
        {
            rowController rc = rowControllers.FirstOrDefault(q=>q.uID == rid);
           
            if(rc != null)
            {
                int actualRow = (int)rc.rowID;

                double y = 0;
                if (rid > 1)
                {
                    y = actualRow * 15;
                }
                else
                {
                    y = actualRow + 4;
                }
         
                //-- correct Y position based off existing rows Y position --
                createBasicBlock(st, y, et - st, uid, rid, pwmStart, pwmEnd);
                Debug.WriteLine("Incoming Block:" + actualRow + ":y:" + y);
            }    
        }
        private void generateNewTimeblock(int uid, double y, double st, double et)
        {
            double   rowID = 0;

            if (y > 10 && y < 25)
            {
                rowID = 1;
            }
            if (y > 25 && y < 40)
            {
                rowID = 2;
            }
            if (y > 40 && y < 55)
            {
                rowID = 3;
            }
            if (y > 55 && y < 70)
            {
                rowID = 4;
            }
            if (y > 70 && y < 85)
            {
                rowID = 5;
            }
            if (y > 85 && y < 100)
            {
                rowID = 6;
            }
            if (y > 100 && y < 115)
            {
                rowID = 7;
            }

            rowController rc = rowControllers.FirstOrDefault(q => q.rowID == rowID);

            if (rc != null)
            {
                //-- Assign block to controller in the same Y plane -- 
                createBasicBlock(st, y, et - st, uid, rc.uID, 100, 100);
            }    
        }
        private void generateNewRowController(int _uID, uint _rowID, uint _pin, uint _type, string _description)
        {
            Border rowControl = new Border();
            //rowControl.Background = gray;
            rowControl.BorderBrush = blue;
            rowControl.BorderThickness = row_thickness;
            rowControl.Width = 35;
            rowControl.Height = 10;
            rowControl.PointerPressed += RowControl_PointerPressed;
            rowControl.DoubleTapped += RowControl_DoubleTapped;
            rowControl.CornerRadius = new CornerRadius(3, 3, 3, 3);
            rowControl.SetValue(Canvas.LeftProperty, 0);
            rowControl.SetValue(Canvas.TopProperty, (_rowID * 15));

            TextBlock textBlock = new TextBlock();
            textBlock.Text = _description;
            double newFont = 8 / (zoom_scale * .8);
            if (newFont > 4)
            {
                newFont = 4;
            }
            textBlock.FontSize = newFont;
            textBlock.SetValue(Canvas.LeftProperty, 0);
            textBlock.HorizontalAlignment = HorizontalAlignment.Left;
            textBlock.VerticalAlignment = VerticalAlignment.Bottom;
            textBlock.FontWeight = FontWeights.Bold;
            rowControl.Child = textBlock;

            block_id_box.Children.Add(rowControl);
            rowControllers.Add(new rowController(rowControl, _uID, _rowID, _pin, _type, _description));
            assignOutputToPin(_pin);

            //-- Move Add Box Down 1 Space --
            add_output_border.SetValue(Canvas.TopProperty, (_rowID+1) * 15);

            if (rowControllers.Count > 6)
            {
                add_output_border.Visibility = Visibility.Collapsed;
            }
            else
            {
                add_output_border.Visibility = Visibility.Visible;  
            }
           
        }
        private void assignOutputToPin(uint _pin)
        {
 
            switch (_pin)
            {
                case D1:
                    rowPinMenu.Items.Remove(rPin_1);
                    break;

                case D2:
                    rowPinMenu.Items.Remove(rPin_2);
                    break;

                case D3:
                    rowPinMenu.Items.Remove(rPin_3);
                    break;

                case D4:
                    rowPinMenu.Items.Remove(rPin_4);
                    break;

                case D5:
                    rowPinMenu.Items.Remove(rPin_5);
                    break;

                case D6:
                    rowPinMenu.Items.Remove(rPin_6);
                    break;

                case D8:
                    rowPinMenu.Items.Remove(rPin_7);
                    break;
            }
        }
        private void unassignOutputToPin(uint _pin)
        {
           switch(_pin)
           {
                case D1:
                    rowPinMenu.Items.Add(rPin_1);
                    break;

                case D2:
                    rowPinMenu.Items.Add(rPin_2);
                    break;

                case D3:
                    rowPinMenu.Items.Add(rPin_3);
                    break;

                case D4:
                    rowPinMenu.Items.Add(rPin_4);
                    break;

                case D5:
                    rowPinMenu.Items.Add(rPin_5);
                    break;

                case D6:
                    rowPinMenu.Items.Add(rPin_6);
                    break;

                case D8:
                    rowPinMenu.Items.Add(rPin_7);
                    break;

                case 99:
                    rowPinMenu.Items.Clear();
                    rowPinMenu.Items.Add(rPin_1);
                    rowPinMenu.Items.Add(rPin_2);
                    rowPinMenu.Items.Add(rPin_3);
                    rowPinMenu.Items.Add(rPin_4);
                    rowPinMenu.Items.Add(rPin_5);
                    rowPinMenu.Items.Add(rPin_6);
                    rowPinMenu.Items.Add(rPin_7);
                    rowPinMenu.Items.Add(rPin_none);
                    break;
            }
        }
        private void RowControl_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
        {
            Border b = sender as Border;
            rowController rc = rowControllers.SingleOrDefault(q => q.block == b);
            //-- open output editor
            if (rc != null)
            {
                row_description_edit.Text = rc.description;
                pin_selection_box.Text = outputMapping.First(q => q.Value == rc.assignedPin).Key;
                selectedRow = rc;
                toggleOutputConfigurationWindow(true);
            }
        }
        private void RowControl_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            //-- momentary toggle?
        }
        private void play_btn_Click(object sender, RoutedEventArgs e)
        {
            if(markerPosition_corrected < 0)
            {

                JsonObject jsonObject = new JsonObject();
                jsonObject["request"] = JsonValue.CreateStringValue("play");
                jsonObject["frameID"] = JsonValue.CreateNumberValue(0);
                //Debug.WriteLine(jsonObject.Stringify());
                sendMessage(jsonObject);

            }
            else
            {

                JsonObject jsonObject = new JsonObject();
                jsonObject["request"] = JsonValue.CreateStringValue("play");
                jsonObject["frameID"] = JsonValue.CreateNumberValue(markerPosition_corrected);
                //Debug.WriteLine(jsonObject.Stringify());
                sendMessage(jsonObject);
            }
            currentMarkerPosition = markerPosition_corrected;
            //Debug.WriteLine(currentMarkerPosition - XPanTracker + " : " +endMarkerPosition);
            timeMarkerTimer.Start();
            stopwatch.Start();
        }
        public async Task ShowAddDialogAsync(string title)
        {
            var inputTextBox = new TextBox { AcceptsReturn = false };
            inputTextBox.VerticalAlignment = VerticalAlignment.Bottom;
            var dialog = new ContentDialog
            {
                Content = inputTextBox,
                Title = title,
                PrimaryButtonText = "Ok",
            };
            if (await dialog.ShowAsync() == ContentDialogResult.Primary)
            {
                changeDeviceName(inputTextBox.Text);
            }
        }
        private async void outputDiscriptionShortDialog()
        {
            ContentDialog eDialog = new ContentDialog()
            {
                Title = "Output Description Error",
                Content = "Description must be greater than 2 characters.",
                CloseButtonText = "Ok"
            };

            await eDialog.ShowAsync();
        }
        private async void changeDeviceName(string deviceName)
        {
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                lock (this)
                {
                    if (deviceName != null && deviceName.Length <= 24 && deviceName.Length > 1)
                    {
                        deviceName = "PropMagic " + deviceName;
                        controller_name_label.Text = deviceName;

                        JsonObject jsonObject = new JsonObject();
                        jsonObject["edit"] = JsonValue.CreateStringValue("option");
                        jsonObject["name"] = JsonValue.CreateStringValue(deviceName);
                        //Debug.WriteLine(jsonObject.Stringify());
                        sendMessage(jsonObject);
                    }
                }
            });
        }
        private async void controller_name_label_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            await ShowAddDialogAsync("Rename Device");
        }
        private void pause_btn_Click(object sender, RoutedEventArgs e)
        {
            timeMarkerTimer.Stop();
            stopwatch.Stop();

            JsonObject jsonObject = new JsonObject();
            jsonObject["request"] = JsonValue.CreateStringValue("stop");
            //Debug.WriteLine(jsonObject.Stringify());
            sendMessage(jsonObject);
        }
        private void stop_btn_Click(object sender, RoutedEventArgs e)
        {
            resetMarker();
            timeMarkerTimer.Stop();
            stopwatch.Reset();

            JsonObject jsonObject = new JsonObject();
            jsonObject["request"] = JsonValue.CreateStringValue("stop");
            //Debug.WriteLine(jsonObject.Stringify());
            sendMessage(jsonObject);

            clearHighlights();
        }
        private async void clearHighlights()
        {
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
            {
                lock (this)
                {
                    foreach (timelineBlock tb in timelineBlockList)
                    {
                        highlightBlock(tb.block, false);
                    }
                }
            });
        }
        private void scrubber_active_check_Checked(object sender, RoutedEventArgs e)
        {
            if (scrubber_active_check.IsChecked == true)
            {
                realtimeScrubber = true;
                realtimeLockout.Restart();
                prevRealtimeLockout = realtimeLockout.ElapsedMilliseconds;
            }
            else
            {
                realtimeScrubber = false;
                realtimeLockout.Stop();
                JsonObject jsonObject = new JsonObject();
                jsonObject["request"] = JsonValue.CreateStringValue("stop");
                //Debug.WriteLine(jsonObject.Stringify());
                sendMessage(jsonObject);
            }
        }
        private void scrubber_active_check_Unchecked(object sender, RoutedEventArgs e)
        {
            realtimeScrubber = false;
            realtimeLockout.Stop();
            JsonObject jsonObject = new JsonObject();
            jsonObject["request"] = JsonValue.CreateStringValue("stop");
            //Debug.WriteLine(jsonObject.Stringify());
            sendMessage(jsonObject);

        }
        private void active_controller_disconnect_btn_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            SYSTEM_STATE = SYSTEM_BLE_DISCONNECTING;
        }
        private void add_output_border_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
        {
            // -- Open new output menu
            //-- New Block Creation --
            bool uniqueID_Found = false;
            int uID = 0;
            while (!uniqueID_Found)
            {
                uID += 1;
                uniqueID_Found = activeControllerIDs.Add(uID);
            }
            generateNewRowController(uID, (uint)rowControllers.Count(), getUnusedPin(), 1, "New");
            selectedRow = rowControllers.Last();
            sendMessage(selectedRow.getBlockData());
            assignOutputToPin(selectedRow.assignedPin);
            //toggleOutputConfigurationWindow(true);
        }
        private void exit_canvas_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            if(row_controller_menu.Visibility == Visibility.Visible)
            {
                toggleOutputConfigurationWindow(false);
            }

            if (block_control_menu.Visibility == Visibility.Visible)
            {
                toggleBlockConfigurationWindow(false);
            }

        }
        private void type_selection_box_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
        {
            rowTypeMenu.ShowAt((TextBlock)sender);
        }
        private void pin_selection_box_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
        {
            rowPinMenu.ShowAt((TextBlock)sender);
        }
        private void delete_row_button_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
        {

            Border border = selectedRow.block;           

            JsonObject jsonObject = new JsonObject();
            jsonObject["remove"] = JsonValue.CreateStringValue("output");
            jsonObject["uniqueID"] = JsonValue.CreateNumberValue(selectedRow.uID);
            sendMessage(jsonObject);
            unassignOutputToPin(selectedRow.assignedPin);

            List<timelineBlock> pendingDelete = new List<timelineBlock>();


            int deletionRowID = selectedRow.uID;
            uint shiftingRowID = selectedRow.rowID;

            foreach (timelineBlock tb in timelineBlockList)
            {
                if(tb.rowID == deletionRowID)
                {              
                    Border block = tb.block;
                    timeline_canvas.Children.Remove(block);
                    pendingDelete.Add(tb);
                }
                else
                {           
                    Border block = tb.block;
                    double newY = (double)block.GetValue(Canvas.TopProperty);
                    if (newY > shiftingRowID * 15)
                    {
                        block.SetValue(Canvas.TopProperty, newY - 15);
                        //updateBlock(block);
                    }
                }          
            }

            foreach(timelineBlock tb in pendingDelete)
            {
                Debug.WriteLine("Deleting block @ row:" + tb.rowID);
                activeBlockIDs.Remove(tb.uID);
                timelineBlockList.Remove(tb);
            }
            
            foreach(rowController r in rowControllers)
            {          
                Border rb = r.block;
                double prevY = (double)rb.GetValue(Canvas.TopProperty);
                if (prevY > shiftingRowID * 15)
                {
                    rb.SetValue(Canvas.TopProperty, prevY - 15);
                    r.rowID = r.rowID - 1;
                }
            }
            
            block_id_box.Children.Remove(border);
            rowControllers.Remove(selectedRow);
            activeControllerIDs.Remove(selectedRow.uID);
            add_output_border.SetValue(Canvas.TopProperty, rowControllers.Count()*15);
            selectedRow = null;
            toggleOutputConfigurationWindow(CLOSE);

            if (rowControllers.Count > 3)
            {
                add_output_border.Visibility = Visibility.Collapsed;
            }
            else
            {
                add_output_border.Visibility = Visibility.Visible;
            }
        }
        private void play_mode_combo_box_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
        {
            playbackMenu.ShowAt((TextBlock)sender);
        }
        private void ambient_combo_box_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
        {
            ambientMenu.ShowAt((TextBlock)sender);
        }
        private void scare_combo_box_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
        {
            scareMenu.ShowAt((TextBlock)sender);
        }

        //----------------------------------------------------------------------------------------------------
        //---------- Bluetooth Routines ----------------------------------------------------------------------
        //----------------------------------------------------------------------------------------------------
        private void BleConnectionCheckTimer_Tick(object sender, object e)
        {
            if (SYSTEM_STATE == SYSTEM_BLE_REQUEST_DEVICE_DATA || SYSTEM_STATE == SYSTEM_BLE_CONNECTING)
            {
                SYSTEM_STATE = SYSTEM_BLE_DROPPED;
                bleConnectionCheckTimer.Stop();
            }
            else
            {
                bleConnectionCheckTimer.Stop();
            }
        }
        private void BleScanTimer_Tick(object sender, object e)
        {
            if (deviceWatcher.Status == DeviceWatcherStatus.Stopped)
            {
                bleScanTimer.Stop();
                restartScanner();
            }
        }
        private void restartScanner()
        {
            BLE_Scanning = false;
            BLE_Ready = false;
            scannerDisable();
            scannerEnable();
        }
        private async void requestDisconnection()
        {
            JsonObject jsonObject = new JsonObject();
            jsonObject["request"] = JsonValue.CreateStringValue("disconnect");

            toggleDeviceWindow(CLOSE);
            toggleScanWindow(CLOSE);
            toggleTimelineWindow(CLOSE);
            toggleLoadWindow(CLOSE);

            timelineRequest_Sent = false;
            timelineRequest_Complete = false;
            timelineLoaded = false;
            timelineBlockList.Clear();
            activeBlockIDs.Clear();
            unassignOutputToPin(99);


            resetMarker();
            scannerDisable();
            Dispatcher.StopProcessEvents();
            try
            {
                if (readCharacteristic != null)
                {
                    GattCommunicationStatus status = await readCharacteristic.WriteClientCharacteristicConfigurationDescriptorAsync(GattClientCharacteristicConfigurationDescriptorValue.None);
                    sendMessage(jsonObject);
                    readCharacteristic.Service.Dispose();
                    
                }
            }
            catch (Exception e)
            {
                Debug.WriteLine("BLE Disconnected Abruptly");
            }
            try
            {
                bluetoothLeDevice?.Dispose();
                bluetoothLeDevice = null;
                KnownDevices.Clear();
                UnknownDevices.Clear();

            }
            catch (Exception e)
            {
                Debug.WriteLine("BLE Force Clear");
            }
            BLE_Scanning = false;
            BLE_Ready = false;
            BLE_Connection_Sent = false;
            BLE_Connection_Rejection = false;
            initializeTimelineGrid();
            initializeTimelineScrubber();
        }
        private void forceDisconnection()
        {
            toggleDeviceWindow(CLOSE);
            toggleScanWindow(CLOSE);
            toggleTimelineWindow(CLOSE);
            toggleLoadWindow(CLOSE);

            unassignOutputToPin(99);

            writeCharacteristic = null;
            BLE_Scanning = false;
            BLE_Ready = false;
            BLE_Connection_Sent = false;
            BLE_Connection_Rejection = false;
            timelineRequest_Sent = false;
            timelineRequest_Complete = false;
            timelineLoaded = false;
            timelineBlockList.Clear();
            activeBlockIDs.Clear();
            resetMarker();
            scannerDisable();
            Dispatcher.StopProcessEvents();
            try
            {
                bluetoothLeDevice?.Dispose();
                bluetoothLeDevice = null;
                KnownDevices.Clear();
                UnknownDevices.Clear();

            }
            catch (Exception e)
            {
                Debug.WriteLine("BLE Force Clear");
            }

            initializeTimelineGrid();
            initializeTimelineScrubber();
        }
        private async void attemptConnection()
        {
            toggleConnectingWindow(OPEN);


            if (bleDeviceDisplay != null)
            {
                bluetoothLeDevice = await BluetoothLEDevice.FromIdAsync(bleDeviceDisplay.Id);
                SelectedBleDeviceName = bleDeviceDisplay.Name;

                GattDeviceServicesResult services = null;

                try
                {
                    services = await bluetoothLeDevice.GetGattServicesAsync(BluetoothCacheMode.Uncached);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("Service Check Failed");
                    SYSTEM_STATE = SYSTEM_BLE_DISCONNECTING;
                    return;
                }
                bleConnectionCheckTimer.Start();


                if (services.Status == GattCommunicationStatus.Success)
                {
                    //-- Check for UART UUID --
                    Guid guid = new Guid("6e400001-b5a3-f393-e0a9-e50e24dcca9e");
                    var service = services.Services.SingleOrDefault(t => t.Uuid == guid);
                    //--------------------------

                    if (service != null)
                    {
                        try
                        {
                            var characteristics = await service.GetCharacteristicsAsync();
                            foreach (var character in characteristics.Characteristics)
                            {
                                if (character.CharacteristicProperties == GattCharacteristicProperties.Write)
                                {
                                    writeCharacteristic = character;
                                    BLE_Ready = true;
                                    controller_name_label.Text = bleDeviceDisplay.Name;
                                    bluetoothLeDevice.GattServicesChanged += BluetoothLeDevice_GattServicesChanged;
                                    toggleDeviceWindow(OPEN);
                                }
                                else
                                {
                                    readCharacteristic = character;
                                    GattCommunicationStatus status = GattCommunicationStatus.Unreachable;
                                    var cccdValue = GattClientCharacteristicConfigurationDescriptorValue.Notify;
                                    status = await readCharacteristic.WriteClientCharacteristicConfigurationDescriptorAsync(cccdValue);

                                    if (status == GattCommunicationStatus.Success)
                                    {
                                        readCharacteristic.ValueChanged += ReadCharacteristic_ValueChanged;
                                    }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine("Unable to verify characteristics");
                            SYSTEM_STATE = SYSTEM_BLE_DISCONNECTING;
                            bleConnectionCheckTimer.Stop();
                            return;

                        }

                    }
                    BLE_Connection_Accepted = true;
                }
                else
                {
                    Debug.WriteLine(services.Status.ToString());
                }
            }
            else
            {
                bluetoothLeDevice = null;
                BLE_Connection_Rejection = true;
            }

        }
        private void BluetoothLeDevice_GattServicesChanged(BluetoothLEDevice sender, object args)
        {
            SYSTEM_STATE = SYSTEM_BLE_DROPPED;

        }
        private void scanForDevice()
        {
            if (!BLE_Ready)
            {
                if (!BLE_Scanning)
                {
                    scannerEnable();
                    toggleScanWindow(OPEN);
                }
            }
        }
        private async void sendMessage(JsonObject data)
        {

            if (BLE_Ready)
            {
                string outgoingJSON = data.Stringify();
                var writeBuffer = CryptographicBuffer.ConvertStringToBinary(outgoingJSON, BinaryStringEncoding.Utf8);
                var writeSuccessful = await WriteBufferTowriteCharacteristicAsync(writeBuffer, outgoingJSON);
            }
            else
            {
                Debug.WriteLine("Message Denied - BLE Offline");
            }
        }
        private void scannerEnable()
        {

            if (!SCANNER_INIT_COMPLETE)
            {

                // Additional properties we would like about the device.
                // Property strings are documented here https://msdn.microsoft.com/en-us/library/windows/desktop/ff521659(v=vs.85).aspx
                string[] requestedProperties = { "System.Devices.Aep.DeviceAddress", "System.Devices.Aep.IsConnected", "System.Devices.Aep.Bluetooth.Le.IsConnectable" };

                //deviceWatcher = DeviceInformation.CreateWatcher(aqsAllBluetoothLEDevices, requestedProperties, DeviceInformationKind.AssociationEndpoint);
                deviceWatcher = DeviceInformation.CreateWatcher(BluetoothLEDevice.GetDeviceSelectorFromConnectionStatus(BluetoothConnectionStatus.Disconnected), requestedProperties, DeviceInformationKind.AssociationEndpointContainer);

                // Register event handlers before starting the watcher.
                deviceWatcher.Added += DeviceWatcher_Added;
                deviceWatcher.Updated += DeviceWatcher_Updated;
                deviceWatcher.Removed += DeviceWatcher_Removed;
                deviceWatcher.EnumerationCompleted += DeviceWatcher_EnumerationCompleted;
                deviceWatcher.Stopped += DeviceWatcher_Stopped;
                SCANNER_INIT_COMPLETE = true;
                DEVICE_WATCHER_READY_TO_START = true;
            }

            if(DEVICE_WATCHER_READY_TO_START)
            {
                deviceWatcher.Start();
                BLE_Scanning = true;
                DEVICE_WATCHER_READY_TO_START = false;
            }
        }
        private async void DeviceWatcher_Updated(DeviceWatcher sender, DeviceInformationUpdate deviceInfoUpdate)
        {
            // We must update the collection on the UI thread because the collection is databound to a UI element.
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                lock (this)
                {

                    // Protect against race condition if the task runs after the app stopped the deviceWatcher.
                    if (sender == deviceWatcher)
                    {
                        BluetoothLEDeviceDisplay bleDeviceDisplay = FindBluetoothLEDeviceDisplay(deviceInfoUpdate.Id);
                        if (bleDeviceDisplay != null)
                        {
                            // Device is already being displayed - update UX.
                            bleDeviceDisplay.Update(deviceInfoUpdate);
                            return;
                        }

                        DeviceInformation deviceInfo = FindUnknownDevices(deviceInfoUpdate.Id);
                        if (deviceInfo != null)
                        {
                            deviceInfo.Update(deviceInfoUpdate);
                            // If device has been updated with a friendly name it's no longer unknown.
                            if (deviceInfo.Name != String.Empty)
                            {
                                if (deviceInfo.Name.Contains("Prop") || deviceInfo.Name.Contains("ESP32"))
                                {
                                    // if ((bool)deviceInfo.Properties["System.Devices.Aep.Bluetooth.Le.IsConnectable"])
                                    KnownDevices.Add(new BluetoothLEDeviceDisplay(deviceInfo));
                                }

                                //KnownDevices.Add(new BluetoothLEDeviceDisplay(deviceInfo));
                                UnknownDevices.Remove(deviceInfo);
                            }
                        }
                    }
                }
            });
        }
        private async void DeviceWatcher_Removed(DeviceWatcher sender, DeviceInformationUpdate deviceInfoUpdate)
        {
            // We must update the collection on the UI thread because the collection is databound to a UI element.
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                lock (this)
                {
                    // Protect against race condition if the task runs after the app stopped the deviceWatcher.
                    if (sender == deviceWatcher)
                    {
                        // Find the corresponding DeviceInformation in the collection and remove it.
                        BluetoothLEDeviceDisplay bleDeviceDisplay = FindBluetoothLEDeviceDisplay(deviceInfoUpdate.Id);
                        if (bleDeviceDisplay != null)
                        {
                            KnownDevices.Remove(bleDeviceDisplay);
                            //Debug.WriteLine("Removing Device");
                        }

                        DeviceInformation deviceInfo = FindUnknownDevices(deviceInfoUpdate.Id);
                        if (deviceInfo != null)
                        {
                            UnknownDevices.Remove(deviceInfo);
                        }
                    }
                }
            });
        }
        private async void DeviceWatcher_EnumerationCompleted(DeviceWatcher sender, object e)
        {
            // We must update the collection on the UI thread because the collection is databound to a UI element.
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                lock (this)
                {
                    // Protect against race condition if the task runs after the app stopped the deviceWatcher.
                    if (sender == deviceWatcher)
                    {
                        deviceWatcher.Stop();
                        bleScanTimer.Start();
                    }
                }
            });
        }
        private async void DeviceWatcher_Stopped(DeviceWatcher sender, object e)
        {
            // We must update the collection on the UI thread because the collection is databound to a UI element.
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                lock (this)
                {
                    // Protect against race condition if the task runs after the app stopped the deviceWatcher.
                    if (sender == deviceWatcher)
                    {
                        DEVICE_WATCHER_READY_TO_START = true;
                    }
                }
            });
        }
        private void scannerDisable()
        {
            if (deviceWatcher.Status == DeviceWatcherStatus.Started)
                deviceWatcher.Stop();
        }
        private BluetoothLEDeviceDisplay FindBluetoothLEDeviceDisplay(string id)
        {
            foreach (BluetoothLEDeviceDisplay bleDeviceDisplay in KnownDevices)
            {
                if (bleDeviceDisplay.Id == id)
                {
                    return bleDeviceDisplay;
                }
            }
            return null;
        }
        private DeviceInformation FindUnknownDevices(string id)
        {
            foreach (DeviceInformation bleDeviceInfo in UnknownDevices)
            {
                if (bleDeviceInfo.Id == id)
                {
                    return bleDeviceInfo;
                }
            }
            return null;
        }
        private async void DeviceWatcher_Added(DeviceWatcher sender, DeviceInformation deviceInfo)
        {
            // We must update the collection on the UI thread because the collection is databound to a UI element.
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                lock (this)
                {
                    // Protect against race condition if the task runs after the app stopped the deviceWatcher.
                    if (sender == deviceWatcher)
                    {
                        // Make sure device isn't already present in the list.
                        if (FindBluetoothLEDeviceDisplay(deviceInfo.Id) == null)
                        {
                            if (deviceInfo.Name.Contains("Prop") || deviceInfo.Name.Contains("ESP32"))
                            {
                                // if ((bool)deviceInfo.Properties["System.Devices.Aep.Bluetooth.Le.IsConnectable"])
                                UnknownDevices.Add(deviceInfo);
                            }
                        }

                    }
                }
            });
        }
        private async Task<bool> WriteBufferTowriteCharacteristicAsync(IBuffer buffer, string d)
        {
            try
            {
                if (writeCharacteristic != null)
                {

                    // BT_Code: Writes the value from the buffer to the characteristic.
                    var result = await writeCharacteristic.WriteValueWithResultAsync(buffer);

                    if (result.Status == GattCommunicationStatus.Success)
                    {
                        return true;
                    }
                    else
                    {
                        return false;
                    }
                }
                else
                    return false;
            }
            catch (Exception ex) when (ex.HResult == E_BLUETOOTH_ATT_INVALID_PDU)
            {
                return false;
            }
            catch (Exception ex) when (ex.HResult == E_BLUETOOTH_ATT_WRITE_NOT_PERMITTED || ex.HResult == E_ACCESSDENIED)
            {
                return false;
            }
            catch (Exception ex)
            {
                return false;
            }
        }
        private void ble_scanner_results_view_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ble_scanner_results_view.SelectedIndex > -1)
            {
                bleDeviceDisplay = ble_scanner_results_view.SelectedItem as BluetoothLEDeviceDisplay;
                SYSTEM_STATE = SYSTEM_BLE_CONNECTING;
                ble_scanner_results_view.SelectedIndex = -1;
            }
        }
        private void ReadCharacteristic_ValueChanged(GattCharacteristic sender, GattValueChangedEventArgs args)
        {
            CryptographicBuffer.CopyToByteArray(args.CharacteristicValue, out byte[] data);
            string dataFromNotify;
            try
            {
                dataFromNotify = Encoding.ASCII.GetString(data);

                //dataFromNotify.TrimEnd('\n');
                // Debug.WriteLine(dataFromNotify.TrimEnd('\n'));

                processResponse(dataFromNotify);
            }
            catch (ArgumentException)
            {
                Debug.Write("Unknown format");
            }
        }
        private async void processResponse(string data)
        {
            JsonObject jsonObject = JsonObject.Parse(data);
            string response_code = jsonObject.GetNamedString("response", "none");
            if(jsonObject.ContainsKey("edit"))
            {
                string edit_code = jsonObject.GetNamedString("edit");
                if(edit_code == "block")
                {
                    int uID = (int)jsonObject.GetNamedNumber("uniqueID");
                    int rID = (int)jsonObject.GetNamedNumber("assignedRow");
                    int sTime = (int)jsonObject.GetNamedNumber("sTime");
                    int eTime = (int)jsonObject.GetNamedNumber("eTime");
                    int pwmStart = (int)jsonObject.GetNamedNumber("pwmStart");
                    int pwmEnd = (int)jsonObject.GetNamedNumber("pwmEnd");

                    await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                    {
                        lock (this)
                        {
                            generateNewTimeblock(uID, rID, sTime, eTime, pwmStart, pwmEnd);
                            load_bar.Value += transferIterator;
                            if (load_bar.Value >= 96)
                            {
                                timelineRequest_Complete = true;
                            }
                        }
                    });
                }
                if (edit_code == "output")
                {
                    int uniqueID = (int)jsonObject.GetNamedNumber("uniqueID");
                    uint pin = (uint)jsonObject.GetNamedNumber("assignedPin");
                    uint type = (uint)jsonObject.GetNamedNumber("assignedType");
                    string desc = jsonObject.GetNamedString("name");

                    await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                    {
                        lock (this)
                        {
                            activeControllerIDs.Add(uniqueID);
                            generateNewRowController(uniqueID, (uint)rowControllers.Count(), pin, type, desc);
                            load_bar.Value += transferIterator;
                            if (load_bar.Value >= 96)
                            {
                                timelineRequest_Complete = true;
                            }
                        }
                    });
                }
            }
            switch (response_code)
            {

                case "scare":

                    await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                    {
                        lock (this)
                        {
                            double scare = jsonObject.GetNamedNumber("trackID");
                            scare_combo_box.Text = scare.ToString();
                        }
                    });

                    break;

                case "ambient":

                    await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                    {
                        lock (this)
                        {
                            double ambient = jsonObject.GetNamedNumber("trackID");
                            ambient_combo_box.Text = ambient.ToString();
                        }
                    });

                    break;

                case "transfer size":

                    transferLength = (int)jsonObject.GetNamedNumber("size");
                    if (transferLength == 0)
                    {
                        timelineRequest_Complete = true;
                        
                        break;
                    }
                    transferIterator = (double)(100 / transferLength) + 1;

                    await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                    {
                        lock (this)
                        {
                            firmware_label.Text = jsonObject.GetNamedNumber("version").ToString();
                            if(jsonObject.GetNamedBoolean("loop"))
                            {
                                play_mode_combo_box.Text = "Loop";
                            }
                            else
                            {
                                play_mode_combo_box.Text = "Trigger";
                            }
                        }
                    });

                    break;

                case "end marker":

                    double newEndtime = jsonObject.GetNamedNumber("time");
                    await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                    {
                        lock (this)
                        {
                            endMarkerPosition = newEndtime;
                            scrubber_arrow_end.SetValue(Canvas.LeftProperty, endMarkerPosition);
                            timeMarker_line_end.SetValue(Canvas.LeftProperty, endMarkerPosition + 4);
                            load_bar.Value += transferIterator;
                        }
                    });

                    break;
                             
            }
        }
        private void output_config_label_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
        {
            JsonObject jsonObject = new JsonObject();
            jsonObject["request"] = JsonValue.CreateStringValue("factory reset");
            sendMessage(jsonObject);
            forceDisconnection();
        }


        private void PrintRow(byte[] bytes, uint currByte)
        {
            var rowStr = "";

            // Format the address of byte i to have 8 hexadecimal digits and add the address
            // of the current byte to the output string.
            //rowStr += currByte.ToString("x8");

            // Format the output:
            for (int i = 0; i < bytes.Length; i++)
            {
                // If finished with a segment, add a space.
                if (i % 2 == 0)
                {
                    // rowStr += " ";

                }

                // Convert the current byte value to hex and add it to the output string.
                rowStr += bytes[i].ToString("x2");

            }
            //rowStr += "é";
            // Append the current row to the HexDump textblock.
            Debug.WriteLine(rowStr);

            JsonObject jsonObject1 = new JsonObject();
            jsonObject1["ota"] = JsonValue.CreateStringValue("ok");
            jsonObject1["data"] = JsonValue.CreateStringValue(rowStr.ToUpper());
            jsonObject1["otaStep"] = JsonValue.CreateNumberValue(55);
            //Debug.WriteLine(jsonObject.Stringify());
            sendMessage(jsonObject1);
        }
        private async void Button_Click(object sender, RoutedEventArgs e)
        {
            var picker = new Windows.Storage.Pickers.FileOpenPicker();
            picker.ViewMode = Windows.Storage.Pickers.PickerViewMode.Thumbnail;
            picker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.DocumentsLibrary;
            picker.FileTypeFilter.Add(".bin");
            picker.FileTypeFilter.Add(".uf2");
            uint bytesPerRow = 16;

            uint chunkSize = 4096;

        StorageFile file = await picker.PickSingleFileAsync();

            StringBuilder fileProperties = new StringBuilder();

            // Get file's basic properties.
            Windows.Storage.FileProperties.BasicProperties basicProperties =
                await file.GetBasicPropertiesAsync();
            string fileSize = string.Format("{0:n0}", basicProperties.Size);
            fileProperties.AppendLine("File size: " + fileSize + " bytes");
            int total_messages = 0;
            using (var inputStream = await file.OpenSequentialReadAsync())
            {
                // Pass the input stream to the DataReader.
                using (var dataReader = new DataReader(inputStream))
                {
                    uint currChunk = 0;
                    uint numBytes;
                    
                    // Create a byte array which can hold enough bytes to populate a row of the hex dump.
                    var bytes = new byte[bytesPerRow];

                    JsonObject jsonObject = new JsonObject();
                    jsonObject["ota"] = JsonValue.CreateStringValue("ok");
                    jsonObject["data"] = JsonValue.CreateStringValue("none");
                    jsonObject["otaStep"] = JsonValue.CreateNumberValue(0);
                    //Debug.WriteLine(jsonObject.Stringify());
                    sendMessage(jsonObject);
                    total_messages++;

                    do
                    {
                        // Load the next chunk into the DataReader buffer.
                        numBytes = await dataReader.LoadAsync(chunkSize);

                        // Read and print one row at a time.
                        var numBytesRemaining = numBytes;
                        while (numBytesRemaining >= bytesPerRow)
                        {
                            // Use the DataReader and ReadBytes() to fill the byte array with one row worth of bytes.
                            dataReader.ReadBytes(bytes);
                            string resultString = BitConverter.ToString(bytes);
                            // Debug.WriteLine(resultString);
                            int d1 = Convert.ToInt32(resultString.Substring(0, 2), 16);
                            int d2 = Convert.ToInt32(resultString.Substring(3, 2), 16);
                            int d3 = Convert.ToInt32(resultString.Substring(6, 2), 16);
                            int d4 = Convert.ToInt32(resultString.Substring(9, 2), 16);
                            int d5 = Convert.ToInt32(resultString.Substring(12, 2), 16);
                            int d6 = Convert.ToInt32(resultString.Substring(15, 2), 16);
                            int d7 = Convert.ToInt32(resultString.Substring(18, 2), 16);
                            int d8 = Convert.ToInt32(resultString.Substring(21, 2), 16);

                            JsonObject jsonObject1 = new JsonObject();
                            jsonObject1["ota"] = JsonValue.CreateStringValue("ok");
                            jsonObject1["data"] = JsonValue.CreateStringValue("233");
                            jsonObject1["otaStep"] = JsonValue.CreateNumberValue(55);
                            jsonObject1["d1"] = JsonValue.CreateNumberValue(d1);
                            jsonObject1["d2"] = JsonValue.CreateNumberValue(d2);
                            jsonObject1["d3"] = JsonValue.CreateNumberValue(d3);
                            jsonObject1["d4"] = JsonValue.CreateNumberValue(d4);
                            jsonObject1["d5"] = JsonValue.CreateNumberValue(d5);
                            jsonObject1["d6"] = JsonValue.CreateNumberValue(d6);
                            jsonObject1["d7"] = JsonValue.CreateNumberValue(d7);
                            jsonObject1["d8"] = JsonValue.CreateNumberValue(d8);
                            sendMessage(jsonObject1);
                            total_messages++;
                            numBytesRemaining -= bytesPerRow;
                            //Debug.WriteLine(numBytesRemaining);
                        }
                        
                        // If there are any bytes remaining to be read, allocate a new array that will hold
                        // the remaining bytes read from the DataReader and print the final row.
                        // Note: ReadBytes() fills the entire array so if the array being passed in is larger
                        // than what's remaining in the DataReader buffer, an exception will be thrown.
                        if (numBytesRemaining > 0)
                        {
                            bytes = new byte[numBytesRemaining];
                            //Debug.WriteLine(numBytesRemaining);
                            // Use the DataReader and ReadBytes() to fill the byte array with the last row worth of bytes.
                            dataReader.ReadBytes(bytes);
                            //PrintRow(bytes, (numBytes - numBytesRemaining) + (currChunk * chunkSize));
                            string resultString = BitConverter.ToString(bytes);
                            //Debug.WriteLine(resultString);

                            int d1 = Convert.ToInt32(resultString.Substring(0, 2), 16);
                            int d2 = Convert.ToInt32(resultString.Substring(3, 2), 16);
                            int d3 = Convert.ToInt32(resultString.Substring(6, 2), 16);
                            int d4 = Convert.ToInt32(resultString.Substring(9, 2), 16);
                            int d5 = Convert.ToInt32(resultString.Substring(12, 2), 16);
                            int d6 = Convert.ToInt32(resultString.Substring(15, 2), 16);
                            int d7 = Convert.ToInt32(resultString.Substring(18, 2), 16);
                            int d8 = Convert.ToInt32(resultString.Substring(21, 2), 16);

                            JsonObject jsonObject1 = new JsonObject();
                            jsonObject1["ota"] = JsonValue.CreateStringValue("ok");
                            jsonObject1["data"] = JsonValue.CreateStringValue("233");
                            jsonObject1["otaStep"] = JsonValue.CreateNumberValue(55);
                            jsonObject1["d1"] = JsonValue.CreateNumberValue(d1);
                            jsonObject1["d2"] = JsonValue.CreateNumberValue(d2);
                            jsonObject1["d3"] = JsonValue.CreateNumberValue(d3);
                            jsonObject1["d4"] = JsonValue.CreateNumberValue(d4);
                            jsonObject1["d5"] = JsonValue.CreateNumberValue(d5);
                            jsonObject1["d6"] = JsonValue.CreateNumberValue(d6);
                            jsonObject1["d7"] = JsonValue.CreateNumberValue(d7);
                            jsonObject1["d8"] = JsonValue.CreateNumberValue(d8);
                            sendMessage(jsonObject1);
                            total_messages++;

                            JsonObject jsonObject2 = new JsonObject();
                            jsonObject2["ota"] = JsonValue.CreateStringValue("ok");
                            jsonObject2["data"] = JsonValue.CreateStringValue("none");
                            jsonObject2["otaStep"] = JsonValue.CreateNumberValue(99999);
                            //Debug.WriteLine(jsonObject.Stringify());
                            sendMessage(jsonObject2);
                            total_messages++;
                        }

                        currChunk++;
                        // If the number of bytes read is anything but the chunk size, then we've just retrieved the last
                        // chunk of data from the stream.  Otherwise, keep loading data into the DataReader buffer.
                    } while (numBytes == chunkSize);
                        
                }
            }
            Debug.WriteLine(total_messages);
        }

        private void timeline_ticks_box_PointerExited(object sender, PointerRoutedEventArgs e)
        {

        }
    }

    public class rowController
    {
        public int uID {  get; set; }
        public uint rowID { get; set; }  
        public uint assignedPin { get; set; }
        public uint assignedType { get; set; }    
        public Border block { get; set; }
        public string description { get; set; }
        public bool isActive { get; set; }
        public bool flagForUpdate { get; set; }

        public rowController(Border block, int uID, uint rowID, uint assignedPin, uint assignedType, string description)
        {
            this.uID = uID;
            this.rowID = rowID;
            this.assignedPin = assignedPin;
            this.assignedType = assignedType;
            this.block = block;
            this.description = description;
            isActive = true;
        }

        public rowController(Border block, int uID, uint rowID)
        {
            this.block = block;
            this.uID = uID;
            this.rowID = rowID;
            assignedPin = 0;
            assignedType = 1;
            description = "Unassigned";
            isActive = true;
        }

        public JsonObject getBlockData()
        {

            JsonObject jsonObject = new JsonObject();
            jsonObject["edit"] = JsonValue.CreateStringValue("output");
            jsonObject["name"] = JsonValue.CreateStringValue(description);
            jsonObject["uniqueID"] = JsonValue.CreateNumberValue(uID);
            jsonObject["assignedRow"] = JsonValue.CreateNumberValue(rowID);
            jsonObject["assignedType"] = JsonValue.CreateNumberValue(assignedType);
            jsonObject["assignedPin"] = JsonValue.CreateNumberValue(assignedPin);

            flagForUpdate = false;

            return jsonObject;
        }

        public string addLeadingZeros(uint i)
        {
            string str = "";

            if (i < 10)
            {
                str += "0";
            }

            return str + i;
        }
    };

    public class timelineBlock
    {
        public double startTime { get; set; }
        public double endTime { get; set; }
        public int pwmStart { get; set; }
        public int pwmEnd { get; set; }
        public int rowID { get; set; }
        public int uID { get; set; }   
        public bool isPlaying { get; set; }
        public Border block { get; set; }
        public bool flagForUpdate { get; set; }
        public bool hasStateChange { get; set; }

        //------- Create new block and flag for controller transfer -----------
        public timelineBlock(Border timeblock, double xPan, double yPan, int uid, int rid, bool isNew, int _pwmStart, int _pwmEnd)
        {
            block = timeblock;
            startTime = (double)block.GetValue(Canvas.LeftProperty) + xPan;
            endTime = startTime + block.Width;
            rowID = rid;
            uID = uid;
            flagForUpdate = true;
            pwmStart = _pwmStart;
            pwmEnd = _pwmEnd;
        }


        //-------- Create block from existing block data ------------------------
        public timelineBlock(Border timeblock, int uid, int rid, int stime, int etime, int _pwmStart, int _pwmEnd)
        {
            block = timeblock;
            startTime = stime;
            endTime = etime;          
            uID = uid;
            rowID = rid;
            flagForUpdate = false;
            hasStateChange = false;
            pwmStart = Scale(_pwmStart, 0, 255, 0, 100);
            pwmEnd = Scale(_pwmEnd, 0, 255, 0, 100);
        }

        public void split()
        {
            double calculateHalf = (endTime - startTime)/2;
            endTime = endTime - calculateHalf;
            block.Width = calculateHalf;
            flagForUpdate = true;
            var b = (TextBlock)block.Child;
            b.Text = (block.Width*10).ToString() + ": " + pwmStart.ToString() + " - " + pwmEnd.ToString();
        }

        private int Scale(int value, int min, int max, int minScale, int maxScale)
        {
            double scaled = minScale + (double)(value - min) / (max - min) * (maxScale - minScale);
            return (int)scaled;
        }

        public JsonObject getBlockData()
        {
            JsonObject jsonObject = new JsonObject();
            jsonObject["edit"] = JsonValue.CreateStringValue("block");
            jsonObject["uniqueID"] = JsonValue.CreateNumberValue(uID);
            jsonObject["assignedRow"] = JsonValue.CreateNumberValue(rowID);
            jsonObject["sTime"] = JsonValue.CreateNumberValue(startTime);
            jsonObject["eTime"] = JsonValue.CreateNumberValue(endTime);
            jsonObject["pwmStart"] = JsonValue.CreateNumberValue(pwmStart);
            jsonObject["pwmEnd"] = JsonValue.CreateNumberValue(pwmEnd);

            flagForUpdate = false;

            return jsonObject;
        }

        public void Start()
        {
            if(!isPlaying)
            {
                isPlaying = true;
                hasStateChange = true;
            }       
        }
        
        public void Stop()
        {
            if (isPlaying)
            {
                isPlaying = false;
                hasStateChange = true;
            }
        }

        public void clearStateChange()
        {
             hasStateChange = false; 
        }

        public string addLeadingZeros(int i)
        {
            string str = "";

            if (i < 1000)
            {
                str += "0";
            }
            if (i < 100)
            {
                str += "0";
            }
            if (i < 10)
            {
                str += "0";
            }

            return str + i;
        }

        public void update(double xPan, double yPan, int rid)
        {
            var prevStart = startTime;
            var prevRow = rowID;
            var prevEndTime = endTime; 
            startTime = (double)block.GetValue(Canvas.LeftProperty) + xPan;
            endTime = startTime + block.Width;
            rowID = rid; 

            if (prevRow != rowID || prevStart != startTime || prevEndTime != endTime)
            {
                flagForUpdate = true;
            }
            
        }
    }

}
