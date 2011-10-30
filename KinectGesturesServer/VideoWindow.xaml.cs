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
using System.Windows.Shapes;
using System.ComponentModel;

using OpenNI;

namespace KinectGesturesServer
{
    public partial class VideoWindow : Window
    {
        private VideoType videoType;
        private NuiSensor sensor;

        private BackgroundWorker refreshWorker;
        private Dictionary<int, Ellipse> handPoints;

        private List<Ellipse> fingerPoints;

        public VideoWindow()
        {
            InitializeComponent();

            handPoints = new Dictionary<int, Ellipse>();
            fingerPoints = new List<Ellipse>();
        }

        public void SetSensorVideo(NuiSensor sensor, VideoType videoType)
        {
            this.videoType = videoType;
            this.sensor = sensor;

            Title = videoType.ToString();

            sensor.HandTracker.HandDestroy += new EventHandler<HandDestroyEventArgs>(HandTracker_HandDestroy);
            sensor.HandTracker.HandUpdate += new EventHandler<HandUpdateEventArgs>(HandTracker_HandUpdate);

            refreshWorker = new BackgroundWorker();
            refreshWorker.DoWork += new DoWorkEventHandler(refreshWorker_DoWork);
            CompositionTarget.Rendering += new EventHandler(CompositionTarget_Rendering);

            //create finger points
            for (int i = 0; i < MultiTouchTracker.MAX_FINGERS; i++)
            {
                Ellipse ellipse = new Ellipse()
                {
                    Width = 10,
                    Height = 10,
                    Fill = Brushes.Orange,
                    Stroke = Brushes.White,
                    StrokeThickness = 2,
                    Opacity = 0,
                };
                fingerPoints.Add(ellipse);
                canvas.Children.Add(ellipse);
            }
        }

        void HandTracker_HandUpdate(object sender, HandUpdateEventArgs e)
        {
            Dispatcher.BeginInvoke((Action)delegate()
            {
                Ellipse ellipse;

                if (!handPoints.ContainsKey(e.UserID))
                {
                    ellipse = new Ellipse()
                    {
                        Width = 50,
                        Height = 50,
                        Fill = new SolidColorBrush(IntColorConverter.ToColor(e.UserID)),
                    };
                    handPoints.Add(e.UserID, ellipse);
                    canvas.Children.Add(ellipse);
                }
                else
                {
                    ellipse = handPoints[e.UserID];
                }

                Point3D pos = sensor.DepthGenerator.ConvertRealWorldToProjective(e.Position);
                Canvas.SetLeft(ellipse, pos.X);
                Canvas.SetTop(ellipse, pos.Y);
            });
        }

        void HandTracker_HandDestroy(object sender, HandDestroyEventArgs e)
        {
            Dispatcher.BeginInvoke((Action)delegate()
            {
                if (!handPoints.ContainsKey(e.UserID))
                {
                    return;
                }

                canvas.Children.Remove(handPoints[e.UserID]);
            });
        }

        void CompositionTarget_Rendering(object sender, EventArgs e)
        {
            if (!refreshWorker.IsBusy)
            {
                refreshWorker.RunWorkerAsync();
            }
        }

        void refreshWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            Dispatcher.BeginInvoke((Action)delegate
            {
                switch (videoType)
                {
                    case VideoType.Raw:
                        videoImage.Source = sensor.RawImageSource;
                        break;

                    case VideoType.Depth:
                        videoImage.Source = sensor.DepthImageSource;
                        break;

                    case VideoType.MultiTouch:
                        //videoImage.Source = sensor.MultiTouchTracker.MultiTouchImageSource;
                        videoImage.Source = sensor.MultiTouchTrackerOmni.OutputImageSource;                        
                        break;

                    case VideoType.MultiTouchResult:
                        //videoImage.Source = sensor.MultiTouchTracker.MultiTouchResultImageSource;
                        break;
                }

                //draw fingers
                var fingers = sensor.MultiTouchTrackerOmni.Fingers;
                for (int i = 0; i < fingers.Count; i++)
                {
                    Canvas.SetLeft(fingerPoints[i], fingers[i].X);
                    Canvas.SetTop(fingerPoints[i], fingers[i].Y);
                    fingerPoints[i].Opacity = 1.0;
                }

                for (int i = fingers.Count; i < fingerPoints.Count; i++)
                {
                    fingerPoints[i].Opacity = 0;
                }
            });
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            CompositionTarget.Rendering -= CompositionTarget_Rendering;
            sensor.HandTracker.HandDestroy -= HandTracker_HandDestroy;
            sensor.HandTracker.HandUpdate -= HandTracker_HandUpdate;
        }
    }

    public enum VideoType
    {
        Raw,
        Depth,
        MultiTouch,
        MultiTouchResult
    }
}
