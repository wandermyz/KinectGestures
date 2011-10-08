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
        private VideoWindow rawVideoWindow, depthVideoWindow;
        private Dictionary<int, TrackingDataControl> handTrackingControlMap;
        
        public MainWindow()
        {
            InitializeComponent();
            handTrackingControlMap = new Dictionary<int, TrackingDataControl>();

            nuiSensor = new NuiSensor();
            nuiSensor.HandTracker.HandCreate += new EventHandler<OpenNI.HandCreateEventArgs>(HandTracker_HandCreate);
            nuiSensor.HandTracker.HandDestroy += new EventHandler<OpenNI.HandDestroyEventArgs>(HandTracker_HandDestroy);
            nuiSensor.HandTracker.HandUpdate += new EventHandler<OpenNI.HandUpdateEventArgs>(HandTracker_HandUpdate);
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            nuiSensor.Dispose();
        }

        private void rawVideoToggleButton_Click(object sender, RoutedEventArgs e)
        {
            if (rawVideoToggleButton.IsChecked.GetValueOrDefault(false))
            {
                if (rawVideoWindow != null)
                {
                    rawVideoWindow.Close();
                }

                rawVideoWindow = new VideoWindow();
                rawVideoWindow.SetSensorVideo(nuiSensor, VideoType.Raw);
                rawVideoWindow.Closed += delegate(object cSender, EventArgs cArgs)
                {
                    rawVideoToggleButton.IsChecked = false;
                };
                rawVideoWindow.Show();
            }
            else
            {
                if (rawVideoWindow != null)
                {
                    rawVideoWindow.Close();
                    rawVideoWindow = null;
                }
            }
        }

        private void depthVideoToggleButton_Click(object sender, RoutedEventArgs e)
        {
            if (depthVideoToggleButton.IsChecked.GetValueOrDefault(false))
            {
                if (depthVideoWindow != null)
                {
                    depthVideoWindow.Close();
                }

                depthVideoWindow = new VideoWindow();
                depthVideoWindow.SetSensorVideo(nuiSensor, VideoType.Depth);
                depthVideoWindow.Closed += delegate(object cSender, EventArgs cArgs)
                {
                    depthVideoToggleButton.IsChecked = false;
                };
                depthVideoWindow.Show();
            }
            else
            {
                if (depthVideoWindow != null)
                {
                    depthVideoWindow.Close();
                    depthVideoWindow = null;
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

    }
}
