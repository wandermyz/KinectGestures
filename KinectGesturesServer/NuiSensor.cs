using System;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Diagnostics;
using OpenNI;

namespace Nui
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
        private const string CONFIGURATION = @"SamplesConfig.xml";

        /// <summary>
        /// Horizontal bitmap dpi.
        /// </summary>
        private readonly int DPI_X = 96;

        /// <summary>
        /// Vertical bitmap dpi.
        /// </summary>
        private readonly int DPI_Y = 96;

        #endregion

        #region Members

        /// <summary>
        /// Thread responsible for image and depth camera updates.
        /// </summary>
        private Thread _cameraThread;

        /// <summary>
        /// Indicates whether the thread is running.
        /// </summary>
        private bool _isRunning = true;

        /// <summary>
        /// Image camera source.
        /// </summary>
        private WriteableBitmap _imageBitmap;

        /// <summary>
        /// Depth camera source.
        /// </summary>
        private WriteableBitmap _depthBitmap;

        /// <summary>
        /// Raw image metadata.
        /// </summary>
        private ImageMetaData _imgMD = new ImageMetaData();

        /// <summary>
        /// Depth image metadata.
        /// </summary>
        private DepthMetaData _depthMD = new DepthMetaData();

        private HandSensor handSensor;

        //surface model
        private double[,] surfaceModelDepthMap = null;  //use double to calculate average
        private double[,] surfaceModelNoiseMap = null;  //use to estimate noise of each pixel
        private bool isCalibrating = false;
        private int calibrateFrameCount = 0;
        
        #endregion

        #region Properties

        public HandSensor HandSensor { get { return handSensor; } }

        public int SurfaceShellDepth { get; set; }
        public int SurfaceNoiseDepth { get; set; }

        #region Bitmap properties

        /// <summary>
        /// Returns the image camera's bitmap source.
        /// </summary>
        public ImageSource RawImageSource
        {
            get
            {
                if (_imageBitmap != null)
                {
                    _imageBitmap.Lock();
                    _imageBitmap.WritePixels(new Int32Rect(0, 0, _imgMD.XRes, _imgMD.YRes), _imgMD.ImageMapPtr, (int)_imgMD.DataSize, _imageBitmap.BackBufferStride);
                    _imageBitmap.Unlock();
                }

                return _imageBitmap;
            }
        }

        /// <summary>
        /// Returns the depth camera's bitmap source.
        /// </summary>
        public ImageSource DepthImageSource
        {
            get
            {
                if (_depthBitmap != null)
                {
                    UpdateHistogram(_depthMD);

                    _depthBitmap.Lock();

                    unsafe
                    {
                        ushort* pDepth = (ushort*)DepthGenerator.DepthMapPtr.ToPointer();
                        
                        for (int y = 0; y < _depthMD.YRes; ++y)
                        {
                            byte* pDest = (byte*)_depthBitmap.BackBuffer.ToPointer() + y * _depthBitmap.BackBufferStride;
                            for (int x = 0; x < _depthMD.XRes; ++x, ++pDepth, pDest += 3)
                            {
                                byte pixel;
                                pixel = (byte)Histogram[*pDepth];

                                if (surfaceModelDepthMap == null || isCalibrating)
                                {
                                    pDest[0] = 0;
                                    pDest[1] = 0;
                                    pDest[2] = pixel;
                                    //pixel = (byte)Histogram[*pDepth];
                                }
                                else
                                {
                                    //double surfaceDepth = surfaceModelNoiseMap[y, x] * 5;
                                    double surfaceNoiseDepth = SurfaceNoiseDepth;
                                    double touchBottomDepth = surfaceModelDepthMap[y, x] - surfaceNoiseDepth;

                                    pDest[0] = (*pDepth > touchBottomDepth - SurfaceShellDepth) ? (byte)0 : (byte)128;    //R: too far from surface
                                    pDest[1] = (*pDepth < touchBottomDepth) ? (byte)0 : (byte)128;   //G: too near to surface
                                    //pDest[2] = (pDest[0] > 0 || pDest[1] > 0) ? (byte)0 : (byte)255;
                                    pDest[2] = (pDest[0] > 0 || pDest[1] > 0) ? (byte)0 : (byte)(((double)touchBottomDepth - *pDepth) / SurfaceShellDepth * 255);
                                }
                            }
                        }
                    }

                    _depthBitmap.AddDirtyRect(new Int32Rect(0, 0, _depthMD.XRes, _depthMD.YRes));
                    _depthBitmap.Unlock();
                }

                return _depthBitmap;
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

        /// <summary>
        /// OpenNI histogram.
        /// </summary>
        public int[] Histogram { get; private set; }

        #endregion

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

            handSensor = new HandSensor(Context);

            InitializeThread();
        }

        public void startCalibrate()
        {
            surfaceModelDepthMap = new double[_depthMD.YRes, _depthMD.XRes];
            surfaceModelNoiseMap = new double[_depthMD.YRes, _depthMD.XRes];

            for (int y = 0; y < _depthMD.YRes; ++y)
            {
                for (int x = 0; x < _depthMD.XRes; ++x)
                {
                    surfaceModelDepthMap[y, x] = 0;
                    surfaceModelNoiseMap[y, x] = 0;
                }
            }

            calibrateFrameCount = 0;
            isCalibrating = true;
        }

        public void stopCalibrate()
        {
            isCalibrating = false;

            double avg = 0;
            for (int y = 0; y < _depthMD.YRes; ++y)
            {
                for (int x = 0; x < _depthMD.XRes; ++x)
                {
                    double test1 = surfaceModelNoiseMap[y, x] / calibrateFrameCount;    //E(X^2)
                    double test2 = Math.Pow(surfaceModelDepthMap[y, x] / calibrateFrameCount, 2);   //(EX)^2

                    surfaceModelDepthMap[y, x] /= calibrateFrameCount;  //average distance
                    surfaceModelNoiseMap[y, x] = Math.Sqrt(surfaceModelNoiseMap[y, x] / calibrateFrameCount - Math.Pow(surfaceModelDepthMap[y, x], 2));
                        //calculate the standard deviation
                    
                    avg += double.IsNaN(surfaceModelNoiseMap[y, x]) ? 0 : surfaceModelNoiseMap[y, x];   //FIXME: why there is NaN?
                }
            }
            avg /= (_depthMD.YRes * _depthMD.XRes);
            Trace.WriteLine("Average Noise: " + avg.ToString());
        }

        #endregion

        #region Private methods

        /// <summary>
        /// Initializes the image and depth camera.
        /// </summary>
        /// <param name="configuration">Configuration file path.</param>
        private void InitializeCamera(string configuration)
        {
            try
            {
                //Context = new Context(configuration);
                ScriptNode scriptNode;
                Context.CreateFromXmlFile(configuration, out scriptNode);
                Context = scriptNode.Context;
            }
            catch (Exception e)
            {
                throw e;
            }

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
            //MapOutputMode mapMode = DepthGenerator.GetMapOutputMode();
            MapOutputMode mapMode = DepthGenerator.MapOutputMode;

            //int width = (int)mapMode.nXRes;
            //int height = (int)mapMode.nYRes;

            int width = mapMode.XRes;
            int height = mapMode.YRes;

            _imageBitmap = new WriteableBitmap(width, height, DPI_X, DPI_Y, PixelFormats.Rgb24, null);
            _depthBitmap = new WriteableBitmap(width, height, DPI_X, DPI_Y, PixelFormats.Rgb24, null);
        }

        /// <summary>
        /// Initializes the background camera thread.
        /// </summary>
        private void InitializeThread()
        {
            _isRunning = true;

            _cameraThread = new Thread(CameraThread);
            _cameraThread.IsBackground = true;
            _cameraThread.Start();
        }

        /// <summary>
        /// Updates image and depth values.
        /// </summary>
        private unsafe void CameraThread()
        {
            while (_isRunning)
            {
                Context.WaitAndUpdateAll();

                ImageGenerator.GetMetaData(_imgMD);
                DepthGenerator.GetMetaData(_depthMD);

                if (isCalibrating)
                {
                    refineSurfaceModel();
                    calibrateFrameCount++;
                }
            }
        }

        private void refineSurfaceModel()
        {
            unsafe
            {
                ushort* pDepth = (ushort*)DepthGenerator.DepthMapPtr.ToPointer();

                for (int y = 0; y < _depthMD.YRes; ++y)
                {
                    for (int x = 0; x < _depthMD.XRes; ++x, ++pDepth)
                    {
                        surfaceModelDepthMap[y, x] += (double)*pDepth;    //any accurate problem?
                        surfaceModelNoiseMap[y, x] += (double)*pDepth * (double)*pDepth;
                    }
                }
            }
        }

        #endregion

        #region Public methods

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
            _imageBitmap = null;
            _depthBitmap = null;
            _isRunning = false;
            _cameraThread.Join();
            Context.Dispose();
            _cameraThread = null;
            Context = null;
        }

        #endregion
    }
}
