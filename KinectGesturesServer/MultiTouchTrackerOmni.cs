using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using OpenNI;
using System.Windows.Media.Imaging;
using System.Windows;
using System.Windows.Media;
using System.IO;
using System.Runtime.InteropServices;
using System.Diagnostics;

namespace KinectGesturesServer
{
    public class MultiTouchTrackerOmni
    {
        private const int MAX_FINGERS = 10;
        private const int HAND_CHANGE_CONFIDENCE_THRESHOLD = 20;

        private NuiSensor sensor;
        private int width, height;

        private int lastHandDetectConfidence = 0;

        #region buffers for multi-touch sensing
        private byte[] bufferOutputColored; 
        private int[] fingersRaw;   //data returned from native side
        #endregion

        private WriteableBitmap outputImageSource;

        public List<Point3D> Fingers { get; private set; }
        public double FingerWidthMin { get; set; }
        public double FingerWidthMax { get; set; }
        public double FingerLengthMax { get; set; }
        public double FingerLengthMin { get; set; }

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

            fingersRaw = new int[MAX_FINGERS * 2];
            Fingers = new List<Point3D>(MAX_FINGERS);

            int bufferSize = width * height * 3;
            bufferOutputColored = new byte[bufferSize];

            sensor.CaptureRequested += new EventHandler<NuiSensor.CaptureEventArgs>(sensor_CaptureRequested);

            double realWorldXToZ, realWorldYToZ;
            hackXnConvertProjectiveToRealWorld(out realWorldXToZ, out realWorldYToZ);

            unsafe
            {
                ImageProcessorLib.derivativeFingerDetectorInit(null, null, width, height, width, width * 3, sensor.DepthGenerator.DeviceMaxDepth, realWorldXToZ, realWorldYToZ);
            }
        }

        void sensor_CaptureRequested(object sender, NuiSensor.CaptureEventArgs e)
        {
            string fileNameH = e.Folder + e.FileNamePrefix + "derivativeH.csv";
            string fileNameV = e.Folder + e.FileNamePrefix + "derivativeV.csv";
            string fileNameBmp = e.Folder + e.FileNamePrefix + "derivative.bmp";

            StreamWriter swH = new StreamWriter(fileNameH);
            StreamWriter swV = new StreamWriter(fileNameV);

            lock (bufferOutputColored)
            {
                unsafe
                {
                    int* hDerivativeRes, vDerivativeRes;
                    ImageProcessorLib.derivativeFingerDetectorGetDerivativeFrame(&hDerivativeRes, &vDerivativeRes);

                    for (int i = 0; i < height; i++)
                    {
                        for (int j = 0; j < width; j++)
                        {
                            swH.Write(hDerivativeRes[i * width + j].ToString() + ", ");
                            swV.Write(vDerivativeRes[i * width + j].ToString() + ", ");
                        }
                        swH.WriteLine();
                        swV.WriteLine();
                    }
                }
            }

            swH.Close();
            swV.Close();

            BmpBitmapEncoder bmpEncoder = new BmpBitmapEncoder();
            bmpEncoder.Frames.Add(BitmapFrame.Create(outputImageSource));
            FileStream fs = new FileStream(fileNameBmp, FileMode.Create);
            bmpEncoder.Save(fs);
            fs.Close();
        }

        void sensor_FrameUpdate(object sender, NuiSensor.FrameUpdateEventArgs e)
        {
            int fingersNum = 0;
            int[] handHint = new int[4];

            lock (bufferOutputColored)
            {
                unsafe
                {
                    fixed (byte* bufferOutputColorPtr = bufferOutputColored)
                    {
                        fixed (int* fingerRawPtr = fingersRaw, handHintPtr = handHint)
                        {
                            ushort* pDepth = (ushort*)sensor.DepthMetaData.DepthMapPtr.ToPointer();
                            fingersNum = ImageProcessorLib.derivativeFingerDetectorWork(pDepth, bufferOutputColorPtr, width, height, width, width * 3, 
                                FingerWidthMin, FingerWidthMax, FingerLengthMin, FingerLengthMax, 
                                MAX_FINGERS, fingerRawPtr, handHintPtr);
                        }
                    }
                }
            }

            Fingers.Clear();
            for (int i = 0; i < fingersNum; i++)
            {
                Fingers.Add(new Point3D(fingersRaw[2 * i], fingersRaw[2 * i + 1], 0)); 
            }

            if (Fingers.Count > 0 && (!sensor.HandTracker.IsTracking || handHint[3] - lastHandDetectConfidence > HAND_CHANGE_CONFIDENCE_THRESHOLD))
            {
                sensor.HandTracker.HandDestroy += new EventHandler<HandDestroyEventArgs>(HandTracker_HandDestroy);
                sensor.HandTracker.StartTrackingAt(new Point3D(handHint[0], handHint[1], handHint[2]));
                lastHandDetectConfidence = handHint[3];
                Trace.WriteLine("Hand hint at " + string.Format("{0}, {1}, {2}", handHint[0], handHint[1], handHint[2]) + " with confidence" + handHint[3].ToString());
            }
        }

        void HandTracker_HandDestroy(object sender, HandDestroyEventArgs e)
        {
            lastHandDetectConfidence = 0;
            sensor.HandTracker.HandDestroy -= HandTracker_HandDestroy;
        }

        /// <summary>
        /// We need to call xnConvertProjectiveToRealWorld in the native side, but impossible. In this function, 2 internal parameters are used. We use this hack to solve the 2 unknown parameter.
        /// </summary>
        private void hackXnConvertProjectiveToRealWorld(out double realWorldXToZ, out double realWorldYToZ)
        {
            Random random = new Random(0);
            Point3D[] testPoints = new Point3D[100];

            for (int i = 0; i < 100; i++)
            {
                testPoints[i] = new Point3D(random.Next(0, width - 1), random.Next(0, height - 1), random.Next(0, sensor.DepthGenerator.DeviceMaxDepth - 1));
            }

            Point3D[] testResults = sensor.DepthGenerator.ConvertProjectiveToRealWorld(testPoints);
            realWorldXToZ = 0;
            realWorldYToZ = 0;
            for (int i = 0; i < testPoints.Length; i++)
            {
                realWorldXToZ += testResults[i].X / (testPoints[i].X / width - 0.5) / testResults[i].Z;
                realWorldYToZ += testResults[i].Y / (0.5 - testPoints[i].Y / height) / testResults[i].Z;
            }

            realWorldXToZ /= testPoints.Length;
            realWorldYToZ /= testPoints.Length;
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

