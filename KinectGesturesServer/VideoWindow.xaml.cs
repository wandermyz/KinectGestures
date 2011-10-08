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

using OpenNI;

namespace KinectGesturesServer
{
    /// <summary>
    /// VideoWindow.xaml 的交互逻辑
    /// </summary>
    public partial class VideoWindow : Window
    {
        private VideoType videoType;
        private Context context;

        public VideoWindow()
        {
            InitializeComponent();
        }

        public void SetVideo(Context context, VideoType videoType)
        {
            this.videoType = videoType;
            this.context = context;
        }
    }

    public enum VideoType
    {
        Raw,
        Depth,
    }
}
