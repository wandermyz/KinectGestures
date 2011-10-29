using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace KinectGesturesServer
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        private NuiSensor nuiSensor;
        private VideoWindow rawVideoWindow, depthVideoWindow, multiTouchVideoWindow, multiTouchResultVideoWindow;
        private Dictionary<int, TrackingDataControl> handTrackingControlMap;
        private Server server;
        private bool isSlidersValueLoaded = false;
        
        public MainWindow()
        {
            InitializeComponent();
            handTrackingControlMap = new Dictionary<int, TrackingDataControl>();

            nuiSensor = new NuiSensor();
            nuiSensor.HandTracker.HandCreate += new EventHandler<OpenNI.HandCreateEventArgs>(HandTracker_HandCreate);
            nuiSensor.HandTracker.HandDestroy += new EventHandler<OpenNI.HandDestroyEventArgs>(HandTracker_HandDestroy);
            nuiSensor.HandTracker.HandUpdate += new EventHandler<OpenNI.HandUpdateEventArgs>(HandTracker_HandUpdate);

            server = new Server(nuiSensor);
            server.Start();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            threshold1Slider.Value = Properties.Settings.Default.BlindThreshold;
            threshold2Slider.Value = Properties.Settings.Default.FingerThreshold;
            threshold3Slider.Value = Properties.Settings.Default.NoiseThreshold;
            isSlidersValueLoaded = true;
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (rawVideoWindow != null)
            {
                rawVideoWindow.Close();
            }

            if (depthVideoWindow != null)
            {
                depthVideoWindow.Close();
            }

            if (multiTouchVideoWindow != null)
            {
                multiTouchVideoWindow.Close();
            }

            if (multiTouchResultVideoWindow != null)
            {
                multiTouchResultVideoWindow.Close();
            }

            server.Stop();
            nuiSensor.Dispose();
        }

        private void rawVideoToggleButton_Click(object sender, RoutedEventArgs e)
        {
            handleVideoToggleButtonClick(rawVideoToggleButton, ref rawVideoWindow, VideoType.Raw);
        }

        private void depthVideoToggleButton_Click(object sender, RoutedEventArgs e)
        {
            handleVideoToggleButtonClick(depthVideoToggleButton, ref depthVideoWindow, VideoType.Depth);
        }

        private void multiTouchVideoToggleButton_Click(object sender, RoutedEventArgs e)
        {
            handleVideoToggleButtonClick(multiTouchVideoToggleButton, ref multiTouchVideoWindow, VideoType.MultiTouch);
        }


        private void multiTouchResultVideoToggleButton_Click(object sender, RoutedEventArgs e)
        {
            handleVideoToggleButtonClick(multiTouchResultVideoToggleButton, ref multiTouchResultVideoWindow, VideoType.MultiTouchResult);
        }

        private void handleVideoToggleButtonClick(System.Windows.Controls.Primitives.ToggleButton button, ref VideoWindow window, VideoType videoType)
        {
            if (button.IsChecked.GetValueOrDefault(false))
            {
                if (window != null)
                {
                    window.Close();
                }

                window = new VideoWindow();
                window.SetSensorVideo(nuiSensor, videoType);
                window.Closed += delegate(object cSender, EventArgs cArgs)
                {
                    button.IsChecked = false;
                };
                window.Show();
            }
            else
            {
                if (window != null)
                {
                    window.Close();
                    window = null;
                }
            }
        }


        void HandTracker_HandCreate(object sender, OpenNI.HandCreateEventArgs e)
        {
            Dispatcher.BeginInvoke((Action)delegate
            {
                TrackingDataControl trackingDataControl = new TrackingDataControl();
                trackingDataControl.Tag = e.UserID;
                handTrackingControlMap.Add(e.UserID, trackingDataControl);

                trackingDataControl.idLabel.Content = e.UserID;
                trackingDataControl.colorRect.Fill = new SolidColorBrush(IntColorConverter.ToColor(e.UserID));

                handTrackingStackPanel.Children.Add(trackingDataControl);
            });
        }

        void HandTracker_HandUpdate(object sender, OpenNI.HandUpdateEventArgs e)
        {
            Dispatcher.BeginInvoke((Action)delegate
            {
                if (!handTrackingControlMap.ContainsKey(e.UserID))
                {
                    return;
                }

                TrackingDataControl trackingDataControl = handTrackingControlMap[e.UserID];
                trackingDataControl.xTextBox.Text = e.Position.X.ToString();
                trackingDataControl.yTextBox.Text = e.Position.Y.ToString();
                trackingDataControl.zTextBox.Text = e.Position.Z.ToString();
            });                      
        }

        void HandTracker_HandDestroy(object sender, OpenNI.HandDestroyEventArgs e)
        {
           Dispatcher.BeginInvoke((Action)delegate
           {
               if (!handTrackingControlMap.ContainsKey(e.UserID))
               {
                   return;
               }

               handTrackingStackPanel.Children.Remove(handTrackingControlMap[e.UserID]);
               handTrackingControlMap.Remove(e.UserID);
           });
        }

        private void thresholdSliders_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            e.Handled = true;

            if (sender == threshold1Slider)
            {
                if (threshold2Slider.Value < e.NewValue - double.Epsilon)
                {
                    threshold2Slider.Value = e.NewValue;
                }

                if (threshold3Slider.Value < e.NewValue - double.Epsilon)
                {
                    threshold3Slider.Value = e.NewValue;
                }

                threshold1TextBox.Text = e.NewValue.ToString("0.00");
                nuiSensor.MultiTouchTrackerOmni.FingerWidthMin = e.NewValue;
            }
            else if (sender == threshold2Slider)
            {
                if (e.NewValue < threshold1Slider.Value - double.Epsilon)
                {
                    e.Handled = false;
                    threshold2Slider.Value = threshold1Slider.Value;
                }
                else
                {
                    if (threshold3Slider.Value < e.NewValue - double.Epsilon)
                    {
                        threshold3Slider.Value = e.NewValue;
                    }

                    threshold2TextBox.Text = e.NewValue.ToString("0.00");
                    nuiSensor.MultiTouchTrackerOmni.FingerWidthMax = e.NewValue;
                }
            }
            else if (sender == threshold3Slider)
            {
                if (e.NewValue < threshold2Slider.Value - double.Epsilon)
                {
                    e.Handled = false;
                    threshold3Slider.Value = threshold2Slider.Value;
                }
                else
                {
                    threshold3TextBox.Text = e.NewValue.ToString("0.00");
                    //nuiSensor.MultiTouchTracker.BlindThreshold = e.NewValue;
                }
            }

            if (e.Handled && isSlidersValueLoaded)
            {
                Properties.Settings.Default.NoiseThreshold = threshold1Slider.Value;
                Properties.Settings.Default.FingerThreshold = threshold2Slider.Value;
                Properties.Settings.Default.BlindThreshold = threshold3Slider.Value;
                Properties.Settings.Default.Save();
            }
        }

        private void calibrationButton_Click(object sender, RoutedEventArgs e)
        {
            /*if (nuiSensor.MultiTouchTracker.CalibrationState == CalibrationState.None || nuiSensor.MultiTouchTracker.CalibrationState == CalibrationState.Finished)
            {
                calibrationButton.Content = "Calibrating...";
                calibrationButton.IsEnabled = false;

                nuiSensor.MultiTouchTracker.Calibrate(5000000, delegate() 
                {
                    Dispatcher.BeginInvoke((Action)delegate()
                        {
                            calibrationButton.IsEnabled = true;
                            calibrationButton.Content = "Calibrate";
                        }, null);
                });

            }*/
        }

        private void captureButton_Click(object sender, RoutedEventArgs e)
        {
            string folder = @"Z:\文稿\Program\Projects\Kinect\MatlabTest\";
            string fileNamePrefix = DateTime.Now.ToString("yyyyMMdd-hhmmss-");
            nuiSensor.Capture(folder, fileNamePrefix);
        }

    }
}
