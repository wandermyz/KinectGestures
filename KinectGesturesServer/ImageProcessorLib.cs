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
    }
}
