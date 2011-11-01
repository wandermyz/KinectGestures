using System;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Diagnostics;
using OpenNI;

namespace KinectGesturesServer
{
    /// <summary>
    /// Represents a Natural User Interface (image and depth) sensor handler.
    /// </summary>
    public class NuiSensor
    {
        #region Constants

        /// <summary>
        /// Default configuration file path.
        /// </summary>
        private const string CONFIGURATION = @"KinectGesturesConfig.xml";

        /// <summary>
        /// Horizontal bitmap dpi.
        /// </summary>
        public const int DPI_X = 96;

        /// <summary>
        /// Vertical bitmap dpi.
        /// </summary>
        public const int DPI_Y = 96;

        #endregion

        #region Members

        /// <summary>
        /// Thread responsible for image and depth camera updates.
        /// </summary>
        private Thread cameraThread;

        /// <summary>
        /// Indicates whether the thread is running.
        /// </summary>
        private bool isRunning = true;

        /// <summary>
        /// Image camera source.
        /// </summary>
        private WriteableBitmap imageBitmap;

        /// <summary>
        /// Depth camera source.
        /// </summary>
        private WriteableBitmap depthBitmap;

        /// <summary>
        /// Raw image metadata.
        /// </summary>
        private ImageMetaData imgMD = new ImageMetaData();

        /// <summary>
        /// Depth image metadata.
        /// </summary>
        private DepthMetaData depthMD = new DepthMetaData();

        #endregion

        #region Properties

        #region Bitmap properties

        /// <summary>
        /// Returns the image camera's bitmap source.
        /// </summary>
        public ImageSource RawImageSource
        {
            get
            {
                if (imageBitmap != null)
                {
                    imageBitmap.Lock();
                    imageBitmap.WritePixels(new Int32Rect(0, 0, imgMD.XRes, imgMD.YRes), imgMD.ImageMapPtr, (int)imgMD.DataSize, imageBitmap.BackBufferStride);
                    imageBitmap.Unlock();
                }

                return imageBitmap;
            }
        }

        /// <summary>
        /// Returns the depth camera's bitmap source.
        /// </summary>
        public ImageSource DepthImageSource
        {
            get
            {
                if (depthBitmap != null)
                {
                    UpdateHistogram(depthMD);
                    depthBitmap.Lock();

                    unsafe
                    {
                        ushort* pDepth = (ushort*)DepthGenerator.DepthMapPtr.ToPointer();

                        for (int y = 0; y < depthMD.YRes; ++y)
                        {
                            byte* pDest = (byte*)depthBitmap.BackBuffer.ToPointer() + y * depthBitmap.BackBufferStride;
                            for (int x = 0; x < depthMD.XRes; ++x, ++pDepth, pDest += 3)
                            {
                                byte pixel;
                                pixel = (byte)Histogram[*pDepth];
                                pDest[0] = pDest[1] = pDest[2] = pixel;
                            }
                        }
                    }

                    depthBitmap.AddDirtyRect(new Int32Rect(0, 0, depthMD.XRes, depthMD.YRes));
                    depthBitmap.Unlock();
                }

                return depthBitmap;
            }
        }

        #endregion

        #region OpenNI properties

        /// <summary>
        /// OpenNI main Context.
        /// </summary>
        public Context Context { get; private set; }

        /// <summary>
        /// OpenNI image generator.
        /// </summary>
        public ImageGenerator ImageGenerator { get; private set; }

        /// <summary>
        /// OpenNI depth generator.
        /// </summary>
        public DepthGenerator DepthGenerator { get; private set; }

        public DepthMetaData DepthMetaData { get { return depthMD; } }
        public ImageMetaData ImageMetaData { get {return imgMD;}}

        /// <summary>
        /// OpenNI histogram.
        /// </summary>
        public int[] Histogram { get; private set; }
        #endregion

        public HandTracker HandTracker { get; private set; }
        //public MultiTouchTracker MultiTouchTracker { get; private set; }
        public MultiTouchTrackerOmni MultiTouchTrackerOmni { get; private set; }
        #endregion

        #region Events
        public event EventHandler<FrameUpdateEventArgs> FrameUpdate;
        public event EventHandler<CaptureEventArgs> CaptureRequested;
        #endregion

        #region Constructor

        /// <summary>
        /// Creates a new instance of NuiSensor with the default configuration file.
        /// </summary>
        public NuiSensor()
            : this(CONFIGURATION)
        {
        }

        /// <summary>
        /// Creates a new instance of NuiSensor with the specified configuration file.
        /// </summary>
        /// <param name="configuration">Configuration file path.</param>
        public NuiSensor(string configuration)
        {
            InitializeCamera(configuration);
            InitializeBitmaps();

            HandTracker = new HandTracker(Context);
            //MultiTouchTracker = new MultiTouchTracker(this);
            MultiTouchTrackerOmni = new MultiTouchTrackerOmni(this);

            InitializeThread();
        }

        #endregion

        #region Private methods

        /// <summary>
        /// Initializes the image and depth camera.
        /// </summary>
        /// <param name="configuration">Configuration file path.</param>
        private void InitializeCamera(string configuration)
        {
            ScriptNode scriptNode;
            Context.CreateFromXmlFile(configuration, out scriptNode);
            Context = scriptNode.Context;

            ImageGenerator = Context.FindExistingNode(NodeType.Image) as ImageGenerator;
            DepthGenerator = Context.FindExistingNode(NodeType.Depth) as DepthGenerator;
            //Histogram = new int[DepthGenerator.GetDeviceMaxDepth()];
            Histogram = new int[DepthGenerator.DeviceMaxDepth];
        }

        /// <summary>
        /// Initializes the image and depth bitmap sources.
        /// </summary>
        private void InitializeBitmaps()
        {
            MapOutputMode mapMode = DepthGenerator.MapOutputMode;
            
            int width = mapMode.XRes;
            int height = mapMode.YRes;

            imageBitmap = new WriteableBitmap(width, height, DPI_X, DPI_Y, PixelFormats.Rgb24, null);
            depthBitmap = new WriteableBitmap(width, height, DPI_X, DPI_Y, PixelFormats.Rgb24, null);
        }

        /// <summary>
        /// Initializes the background camera thread.
        /// </summary>
        private void InitializeThread()
        {
            isRunning = true;

            cameraThread = new Thread(CameraThread);
            cameraThread.IsBackground = true;
            cameraThread.Start();
        }

        /// <summary>
        /// Updates image and depth values.
        /// </summary>
        private unsafe void CameraThread()
        {
            while (isRunning)
            {
                Context.WaitAndUpdateAll();

                ImageGenerator.GetMetaData(imgMD);
                DepthGenerator.GetMetaData(depthMD);

                if (FrameUpdate != null)
                {
                    FrameUpdate(this, new FrameUpdateEventArgs(imgMD, depthMD));
                }
            }
        }

        #endregion

        #region Public methods

        //get the real world position of pixel coordinate (x,y). Do not use it in loops.
        public Point3D getRealWorldPosition(int x, int y)
        {
            if (depthMD == null)
            {
                return Point3D.ZeroPoint;
            }

            int depth;
            unsafe
            {
                ushort* pDepth = (ushort*)DepthGenerator.DepthMapPtr.ToPointer();
                depth = pDepth[y * depthMD.XRes + x];
            }

            return DepthGenerator.ConvertProjectiveToRealWorld(new Point3D(x, y, depth));
        }

        /// <summary>
        /// Re-creates the depth histogram.
        /// </summary>
        /// <param name="depthMD"></param>
        public unsafe void UpdateHistogram(DepthMetaData depthMD)
        {
            // Reset.
            for (int i = 0; i < Histogram.Length; ++i)
                Histogram[i] = 0;

            ushort* pDepth = (ushort*)depthMD.DepthMapPtr.ToPointer();

            int points = 0;
            for (int y = 0; y < depthMD.YRes; ++y)
            {
                for (int x = 0; x < depthMD.XRes; ++x, ++pDepth)
                {
                    ushort depthVal = *pDepth;
                    if (depthVal != 0)
                    {
                        Histogram[depthVal]++;
                        points++;
                    }
                }
            }

            for (int i = 1; i < Histogram.Length; i++)
            {
                Histogram[i] += Histogram[i - 1];
            }

            if (points > 0)
            {
                for (int i = 1; i < Histogram.Length; i++)
                {
                    Histogram[i] = (int)(256 * (1.0f - (Histogram[i] / (float)points)));
                }
            }
        }

        /// <summary>
        /// Releases any resources.
        /// </summary>
        public void Dispose()
        {
            MultiTouchTrackerOmni.Dispose();

            imageBitmap = null;
            depthBitmap = null;
            isRunning = false;
            cameraThread.Join();
            Context.Dispose();
            cameraThread = null;
            Context = null;
        }

        public void Capture(string folder, string fileNamePrefix)
        {
            //TODO capture raw & depth
            if (CaptureRequested != null)
            {
                CaptureRequested(this, new CaptureEventArgs(folder, fileNamePrefix));
            }
        }

        #endregion

        public class FrameUpdateEventArgs : EventArgs
        {
            public ImageMetaData ImageMetaData { get; private set; }
            public DepthMetaData DepthMetaData { get; private set; }

            public FrameUpdateEventArgs(ImageMetaData imgMD, DepthMetaData depthMD)
            {
                this.ImageMetaData = imgMD;
                this.DepthMetaData = depthMD;
            }
        }

        public class CaptureEventArgs : EventArgs
        {
            public string Folder { get; private set; }
            public string FileNamePrefix { get; private set; }
            public CaptureEventArgs(string folder, string fileNamePrefix)
            {
                this.Folder = folder;
                this.FileNamePrefix = fileNamePrefix; 
            }
        }
    }
}
