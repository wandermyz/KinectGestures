using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.InteropServices;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows;
using System.Collections;
using OpenNI;

namespace KinectGesturesServer
{
    public class MultiTouchTracker
    {
        public const int MAX_FINGERS = 10;

        private NuiSensor sensor;
        int width, height;

        #region properties for calibration
        private Action calibrationFinishedHandler;
        private long calibrationDuration;
        private long calibrationStartTime;
        private int calibratedFrame;
        private double[,] calibrationMap;

        public CalibrationState CalibrationState { get; private set; }
        #endregion

        #region buffers for multi-touch sensing
        //These 3 buffers are allocated for image processing.
        private byte[] bufferSrc, bufferDst, bufferSwitch;
        private int[] fingersRaw;   //data returned from native side

        public List<Point3D> Fingers { get; private set; }

        #endregion

        #region threshold
        /*
         * blindThreshold               ---------------     When hand is put in the hand tracking blind area, hand tracking by NITE won't work
         *                           (hand trakcing blind)
         * fingerThreshold              ---------------     Pixels between fingerTop and noise will regarded as multitouch point
         *                                (finger)
         * noiseThreshold               ---------------     Threashold to filter noise; a "dynamic" thickness of the table
         *                                (noise)
         * Table (height = 0)           ===============
        */
        public double BlindThreshold { get; set; }
        public double FingerThreshold { get; set; }
        public double NoiseThreshold { get; set; }
        #endregion

        #region Bitmap
        private WriteableBitmap bitmap;
        public ImageSource MultiTouchImageSource
        {
            get
            {
                if (bitmap != null)
                {
                    bitmap.Lock();

                    unsafe
                    {
                        ushort* pDepth = (ushort*)sensor.DepthGenerator.DepthMapPtr.ToPointer();

                        for (int y = 0; y < sensor.DepthMetaData.YRes; ++y)
                        {
                            byte* pDest = (byte*)bitmap.BackBuffer.ToPointer() + y * bitmap.BackBufferStride;
                            for (int x = 0; x < sensor.DepthMetaData.XRes; ++x, ++pDepth, pDest += 3)
                            {
                                if (CalibrationState == CalibrationState.Finished)
                                {
                                    double dist = calibrationMap[y, x] - *pDepth;
                                    byte gray = (byte)sensor.Histogram[*pDepth];

                                    //pDest[0] = pDest[1] = pDest[2] = (byte)sensor.Histogram[(int)calibrationMap[y, x]];
                                    pDest[0] = pDest[1] = pDest[2] = 0;

                                    if (dist < NoiseThreshold)
                                    {
                                        pDest[2] = 0xFF;    //to near: blue
                                    }
                                    else if (dist < FingerThreshold)
                                    {
                                        pDest[0] = 0xFF;    //finger: red
                                    }
                                    else if (dist < BlindThreshold)
                                    {
                                        pDest[1] = 0xFF;    //too far: green
                                    }
                                    else
                                    {
                                        pDest[1] = 0xFF;
                                    }

                                }
                                else
                                {
                                    pDest[0] = pDest[1] = pDest[2] = (byte)sensor.Histogram[*pDepth];
                                    //pDest[0] = pDest[1] = pDest[2] = 0;
                                }
                            }
                        }
                    }

                    bitmap.AddDirtyRect(new Int32Rect(0, 0, sensor.DepthMetaData.XRes, sensor.DepthMetaData.YRes));
                    bitmap.Unlock();
                }

                return bitmap;
            }
        }

        private WriteableBitmap resultBitmap;
        public ImageSource MultiTouchResultImageSource
        {
            get
            {
                if (resultBitmap != null && CalibrationState == CalibrationState.Finished)
                {

                    if (resultBitmap.BackBufferStride != resultBitmap.PixelWidth * resultBitmap.Format.BitsPerPixel / 8)
                    {
                        throw new NotImplementedException("Currently we only support bitmap with width that is power of 2");
                    }

                    resultBitmap.Lock();
                    lock (bufferDst)
                    {
                        unsafe
                        {
                            byte* pDest = (byte*)resultBitmap.BackBuffer.ToPointer();
                            for (int i = 0; i < calibrationMap.Length; i++, pDest += 3)
                            {
                                pDest[0] = pDest[1] = pDest[2] = bufferDst[i];
                            }
                        }
                    }

                    resultBitmap.AddDirtyRect(new Int32Rect(0, 0, sensor.DepthMetaData.XRes, sensor.DepthMetaData.YRes));
                    resultBitmap.Unlock();
                }

                return resultBitmap;
            }
        }

        #endregion

        public MultiTouchTracker(NuiSensor sensor)
        {
            this.sensor = sensor;

            sensor.FrameUpdate += new EventHandler<NuiSensor.FrameUpdateEventArgs>(sensor_FrameUpdate);

            width = sensor.DepthGenerator.MapOutputMode.XRes;
            height = sensor.DepthGenerator.MapOutputMode.YRes;

            bitmap = new WriteableBitmap(width, height, NuiSensor.DPI_X, NuiSensor.DPI_Y, PixelFormats.Rgb24, null);
            resultBitmap = new WriteableBitmap(width, height, NuiSensor.DPI_X, NuiSensor.DPI_Y, PixelFormats.Rgb24, null);

            int bufferSize = width * height;
            bufferSrc = new byte[bufferSize];
            bufferSwitch = new byte[bufferSize];
            bufferDst = new byte[bufferSize];

            fingersRaw = new int[MAX_FINGERS * 2];
            Fingers = new List<Point3D>(MAX_FINGERS);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="duration">10^(-6) second</param>
        /// <param name="calibrationFinishedHandler"></param>
        public void Calibrate(long duration, Action calibrationFinishedHandler)
        {
            if (CalibrationState != CalibrationState.None && CalibrationState != CalibrationState.Finished)
            {
                throw new InvalidOperationException();
            }

            calibrationDuration = duration;
            this.calibrationFinishedHandler = calibrationFinishedHandler;
            calibratedFrame = 0;
            CalibrationState = CalibrationState.Requested;
        }

        void sensor_FrameUpdate(object sender, NuiSensor.FrameUpdateEventArgs e)
        {
            if (CalibrationState == CalibrationState.Requested)
            {
                calibrationStartTime = e.DepthMetaData.Timestamp;
                calibrationMap = new double[e.DepthMetaData.YRes, e.DepthMetaData.XRes];

                unsafe
                {
                    ushort* pDepth = (ushort*)e.DepthMetaData.DepthMapPtr.ToPointer();

                    fixed (double* calibrationMapPtr = calibrationMap)
                    {
                        double* pDest = calibrationMapPtr;
                        for (int i = 0; i < calibrationMap.Length; ++i, ++pDepth, ++pDest)
                        {
                            *pDest = *pDepth;
                        }
                    }
                }

                calibratedFrame = 1;
                CalibrationState = CalibrationState.FirstFrameCaptured;
            }
            else if (CalibrationState == CalibrationState.FirstFrameCaptured)
            {
                unsafe
                {
                    ushort* pDepth = (ushort*)e.DepthMetaData.DepthMapPtr.ToPointer();

                    fixed (double* calibrationMapPtr = calibrationMap)
                    {
                        double* pDest = calibrationMapPtr;
                        for (int i = 0; i < calibrationMap.Length; ++i, ++pDepth, ++pDest)
                        {
                            *pDest = (*pDest * calibratedFrame + *pDepth) / (double)(calibratedFrame + 1);
                        }
                    }
                }

                if (e.DepthMetaData.Timestamp - calibrationStartTime >= calibrationDuration)
                {
                    CalibrationState = CalibrationState.Finished;
                    if (calibrationFinishedHandler != null)
                    {
                        calibrationFinishedHandler();
                    }
                }

                calibratedFrame++;
            }
            else if (CalibrationState == CalibrationState.Finished)
            {
                recognizeFingers(e.DepthMetaData);
            }
        }

        private void recognizeFingers(OpenNI.DepthMetaData depthMetaData)
        {
            //filter bitmap and save it to bufferSrc
            lock (bufferSrc)
            {
                unsafe
                {
                    ushort* pDepth = (ushort*)depthMetaData.DepthMapPtr.ToPointer();

                    fixed (double* calibrationMapPtr = calibrationMap)
                    {
                        double* pCal = calibrationMapPtr;
                        for (int i = 0; i < calibrationMap.Length; ++i, ++pDepth, ++pCal)
                        {
                            double dist = *pCal - (double)*pDepth;
                            bufferSrc[i] = (byte)(dist >= NoiseThreshold && dist < FingerThreshold ? 0xFF : 0);
                        }
                    }
                }
            }

            //do open operation
            lock (bufferDst)
            {
                unsafe
                {
                    fixed (byte* bufferSrcPtr = bufferSrc, bufferDstPtr = bufferDst, bufferSwitchPtr = bufferSwitch)
                    {
                        ImageProcessorLib.open(bufferSrcPtr, bufferDstPtr,width, height, width, bufferSwitchPtr);
                    }
                }
            }

            //extract point!
            int fingersNum;
            unsafe
            {
                fixed (byte* bufferSrcPtr = bufferSrc, bufferDstPtr = bufferDst, bufferSwitchPtr = bufferSwitch)
                {
                    fixed (int* fingersRawPtr = fingersRaw)
                    {
                        fingersNum = ImageProcessorLib.extractPoints(bufferDstPtr, null, width, height, width, 
                            bufferSwitchPtr, MAX_FINGERS, 20, fingersRawPtr);
                    }
                }
            }

            Fingers.Clear();
            for (int i = 0; i < fingersNum; i++)
            {
                Fingers.Add(new Point3D(fingersRaw[2 * i], fingersRaw[2 * i + 1], 0));  //TODO: depth
            }
        }
    }

    public enum CalibrationState
    {
        None = 0,
        Requested,
        FirstFrameCaptured,
        Finished
    }
}
