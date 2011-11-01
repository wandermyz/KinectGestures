using System;
using System.Runtime.InteropServices;


namespace KinectGesturesServer
{
    public static class ImageProcessorLib
    {
        [DllImport("KinectGesturesImageProcessorLib.dll")]
        public static extern unsafe int open(byte* srcPtr, byte* dstPtr, int width, int height, int stride, byte* switchPtr);

        [DllImport("KinectGesturesImageProcessorLib.dll")]
        public static extern unsafe int extractPoints(byte* srcPtr, byte* dstPtr, int width, int height, int stride, byte* switchPtr, int maxPointNum, int maxPointArea, int* resultPtr);

        [DllImport("KinectGesturesImageProcessorLib.dll")]
        public static extern unsafe int derivativeFingerDetectorInit(ushort* srcDepthPtr, byte* dstPixelPtr, int width, int height, int depthStride, int pixelStride, int deviceMaxDepth, double realWorldXToZ, double realWorldYToZ);

        [DllImport("KinectGesturesImageProcessorLib.dll")]
        public static extern unsafe int derivativeFingerDetectorDispose();

        [DllImport("KinectGesturesImageProcessorLib.dll")]
        public static extern unsafe int derivativeFingerDetectorWork(ushort* srcDepthPtr, byte* dstPixelPtr, int width, int height, int depthStride, int pixelStride, 
                                                                    double fingerWidthMin, double fingerWidthMax, double fingerLengthMin, double fingerLengthMax,
                                                                    int maxPointNum, int* resultPtr, int* handHint);

        [DllImport("KinectGesturesImageProcessorLib.dll")]
        public static extern unsafe int derivativeFingerDetectorGetDerivativeFrame(int** hResPtr, int** vResPtr);
    }
}
