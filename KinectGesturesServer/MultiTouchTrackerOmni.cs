using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using OpenNI;
using System.Windows.Media.Imaging;
using System.Windows;
using System.Windows.Media;

namespace KinectGesturesServer
{
    public class MultiTouchTrackerOmni
    {
        private NuiSensor sensor;
        private int width, height;

        #region buffers for multi-touch sensing
        private byte[] bufferOutputColored; 
        private int[] fingersRaw;   //data returned from native side
        private float[] perspectiveToRealMap;   //save the coordinate in real world for each pixel
        #endregion

        private WriteableBitmap outputImageSource;

        public List<Point3D> Fingers { get; private set; }

        public WriteableBitmap OutputImageSource
        {
            get
            {
                outputImageSource.Lock();

                unsafe
                {
                    lock (bufferOutputColored)
                    {
                        fixed (byte* bufferOutputColoredPtr = bufferOutputColored)
                        {
                            outputImageSource.WritePixels(new Int32Rect(0, 0, width, height), (IntPtr)bufferOutputColoredPtr, width * height * 3, width * 3);
                        }
                    }
                }

                outputImageSource.AddDirtyRect(new Int32Rect(0, 0, width, height));
                outputImageSource.Unlock();

                return outputImageSource;
            }
        }

        public MultiTouchTrackerOmni(NuiSensor sensor)
        {
            this.sensor = sensor;
            sensor.FrameUpdate += new EventHandler<NuiSensor.FrameUpdateEventArgs>(sensor_FrameUpdate);

            width = sensor.DepthGenerator.MapOutputMode.XRes;
            height = sensor.DepthGenerator.MapOutputMode.YRes;

            outputImageSource = new WriteableBitmap(width, height, NuiSensor.DPI_X, NuiSensor.DPI_Y, PixelFormats.Rgb24, null);
            //TODO: fingers, maps

            int bufferSize = width * height * 3;
            bufferOutputColored = new byte[bufferSize];

            unsafe
            {
                ImageProcessorLib.derivativeFingerDetectorInit(null, null, width, height, width, width * 3, sensor.DepthGenerator.DeviceMaxDepth);
            }
        }

        void sensor_FrameUpdate(object sender, NuiSensor.FrameUpdateEventArgs e)
        {
            lock (bufferOutputColored)
            {
                unsafe
                {
                    fixed (byte* bufferOutputColorPtr = bufferOutputColored)
                    {
                        ushort* pDepth = (ushort*)sensor.DepthMetaData.DepthMapPtr.ToPointer();
                        ImageProcessorLib.derivativeFingerDetectorWork(pDepth, bufferOutputColorPtr, width, height, width, width * 3);
                    }
                }
            }
        }

        public void Dispose()
        {
            unsafe
            {
                ImageProcessorLib.derivativeFingerDetectorDispose();
            }
        }
    }
}

