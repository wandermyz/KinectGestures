#include "depth.h"
#include <memory.h>
#include <assert.h>
#include <math.h>

//#define FINGER_WIDTH_MIN 2
//#define FINGER_WIDTH_MAX 40	//in millimeters
#define FINGER_EDGE_THRESHOLD 1000

typedef enum
{
	StripSmooth,
	StripRising,
	StripMidSmooth,
	StripFalling
} StripState;

static int *hDerivativeRes = NULL, *vDerivativeRes = NULL, *histogram = NULL;
static byte* tmpPixelBuffer;
static int maxHistogramSize = 0, deviceMaxDepth;
static double realWorldXToZ, realWorldYToZ;

int sobel(proc_para_depth)
{
	const double tpl[5][5] =	
	{
		{1, 2, 0, -2, -1},
		{4, 8, 0, -8, -4},
		{6, 12, 0, -12, -6},
		{4, 8, 0, -8, -4},
		{1, 2, 0, -2, -1}
	};

	const int tpl_offset = 2;

	for (int i = 0; i < height; i++)
	{
		for (int j = 0; j < width; j++)
		{


			if (i == 240 && j == 320)
			{
				int abc = 123;
			}

			double depthH = 0; //, depthV = 0;
			for (int ti = 0; ti < 5; ti++)
			{
				int neighbor_row = i + ti - tpl_offset;
				if(neighbor_row < 0 || neighbor_row >= height)
					continue;

				for (int tj = 0; tj < 5; tj++)
				{
					int neighbor_col = j + tj - tpl_offset;
					if(neighbor_col < 0 || neighbor_col >= width)
						continue;

					ushort srcDepthVal = *srcDepth(neighbor_row, neighbor_col);
					depthH += tpl[ti][tj] * (srcDepthVal == 0 ? deviceMaxDepth : srcDepthVal);
					//depthV += tpl[tj][ti] * *srcDepth(neighbor_row, neighbor_col);
				}
			}
			*bufferDepth(hDerivativeRes, i, j) = (int)(depthH + 0.5);
			//*bufferDepth(vDerivativeRes, i, j) = (int)(depthV + 0.5);
		}
	}

	return 0;
}

/*
int sobelLinear(proc_para_depth)
{
	const int tpl[9] = {1, 2, 4, 8, 0, -8, -4, -2, -1};

	const int tpl_offset = 4;

	for (int i = 0; i < height; i++)
	{
		for (int j = 0; j < width; j++)
		{
			double depthH = 0, depthV = 0;
			for (int ti = 0; ti < 9; ti++)
			{
				int neighbor_row = i + ti - tpl_offset;
				if(neighbor_row >= 0 && neighbor_row < height)
				{
					depthV += tpl[ti] * *srcDepth(neighbor_row, j);
				}

				int neighbor_col = j + ti - tpl_offset;
				if (neighbor_col >= 0 && neighbor_col < width)
				{
					depthH += tpl[ti] * *srcDepth(i, neighbor_col);
				}
			}
			*bufferDepth(hDerivativeRes, i, j) = (int)(depthH + 0.5);
			*bufferDepth(vDerivativeRes, i, j) = (int)(depthV + 0.5);
		}
	}

	return 0;
}
*/

void generateOutputImage(proc_para_depth)
{
	//generate histogram
	//int min = 65535, max = -65535;
	int min = 65535, max = 0;
	for (int i = 0; i < height; i++)
	{
		for (int j = 0; j < width; j++)
		{
			int h = (int)abs(*bufferDepth(hDerivativeRes, i, j));
			//int v = *bufferDepth(vDerivativeRes, i, j);
			if (h > max) max = h;
			//if (v > max) max = v;
			if (h < min) min = h;
			//if (v < min) min = v;
		}
	}
	
	int histogramSize = max - min + 1;
	assert(histogramSize < maxHistogramSize);
	int histogramOffset = min;

	memset(histogram, 0, histogramSize * sizeof(int));

	//int points = 0;
	for (int i = 0; i < height; i++)
	{
		for (int j = 0; j < width; j++)
		{
			int h = (int)abs(*bufferDepth(hDerivativeRes, i, j));
			//int v = *bufferDepth(vDerivativeRes, i, j);
			histogram[h - histogramOffset]++;
			//histogram[v - histogramOffset]++;
		}
	}

	for (int i = 1; i < histogramSize; i++)
	{
		histogram[i] += histogram[i-1];
	}

	//int points = width * height * 2;
	int points = width * height;
	for (int i = 0; i < histogramSize; i++)
	{
		histogram[i] = (int)(256 * ((double)histogram[i] / (double)points) + 0.5);
	}

	//draw the image
	for (int i = 0; i < height; i++)
	{
		for (int j = 0; j < width; j++)
		{
			int depth = *bufferDepth(hDerivativeRes, i, j);
			if (depth >= 0)
			{
				dstPixel(i, j)[0] = 0;
				dstPixel(i, j)[2] = histogram[depth - histogramOffset];
			}
			else
			{
				dstPixel(i, j)[0] = histogram[-depth - histogramOffset];
				dstPixel(i, j)[2] = 0;
			}
			dstPixel(i, j)[1] = bufferPixel(tmpPixelBuffer, i, j)[1];

			//dstPixel(i, j)[0] = 0;
			//dstPixel(i, j)[1] = histogram[*bufferDepth(hDerivativeRes, i, j) - histogramOffset];
			//dstPixel(i, j)[2] = histogram[*bufferDepth(vDerivativeRes, i, j) - histogramOffset];
		}
	}
}

double distSquaredInRealWorld(int x1, int y1, int depth1, int x2, int y2, int depth2, int width, int height)
{
	double x1Real = ((double)x1 / (double)width - 0.5) * depth1 * realWorldXToZ;
	double y1Real = (0.5 - (double)y1 / (double)height) * depth1 * realWorldYToZ;
	double x2Real = ((double)x2 / (double)width - 0.5) * depth2 * realWorldXToZ;
	double y2Real = (0.5 - (double)y2 / (double)height) * depth2 * realWorldYToZ; 

	return (x1Real - x2Real) * (x1Real - x2Real) + (y1Real - y2Real) * (y1Real - y2Real) + (depth1 - depth2) * (depth1 - depth2);
}

void findStrips(proc_para_depth, double fingerWidthMin, double fingerWidthMax)
{
	memset(tmpPixelBuffer, 0, pixelStride * height * 3);

	for (int i = 0; i < height; i++)
	{
		StripState state = StripSmooth;
		int partialMin, partialMax;
		int partialMinPos, partialMaxPos;
		for (int j = 0; j < width; j++)
		{
			int currVal = *bufferDepth(hDerivativeRes, i, j);
			if (*srcDepth(i, j) == 0)
			{
				state = StripSmooth;
				continue;
			}

			switch(state)
			{
			case StripSmooth:	//TODO: smooth
				if (currVal > FINGER_EDGE_THRESHOLD)
				{
					partialMax = currVal;
					partialMaxPos = j;
					state = StripRising;
				}
				break;

			case StripRising:
				if (currVal > FINGER_EDGE_THRESHOLD)
				{
					if (currVal > partialMax)
					{
						partialMax = currVal;
						partialMaxPos = j;
					}
				}
				else 
				{
					state = StripMidSmooth;
				}
				break;

			case StripMidSmooth:
				if (currVal < -FINGER_EDGE_THRESHOLD)
				{
					partialMin = currVal;
					partialMinPos = j;
					state = StripFalling;
				}
				break;

			case StripFalling:
				if (currVal < -FINGER_EDGE_THRESHOLD)
				{
					if (currVal < partialMin)
					{
						partialMin = currVal;
						partialMinPos = j;
					}
				}
				else
				{
					double distSquared = distSquaredInRealWorld(
						partialMaxPos, i, *srcDepth(i, partialMaxPos),
						partialMinPos, i, *srcDepth(i, partialMinPos),
						width, height);

					if (distSquared >= fingerWidthMin * fingerWidthMin && distSquared <= fingerWidthMax * fingerWidthMax)
					{
						for (int tj = partialMaxPos; tj <= partialMinPos; tj++)
						{
							bufferPixel(tmpPixelBuffer, i, tj)[0] = 0;
							bufferPixel(tmpPixelBuffer, i, tj)[1] = 255;
							bufferPixel(tmpPixelBuffer, i, tj)[2] = 0;
						}
						
						partialMax = currVal;
						partialMaxPos = j;
					}

					state = StripSmooth;
				}
				break;
			}
		}
	}
}

proc_m derivativeFingerDetectorInit(proc_para_depth, int deviceMaxDepth, double realWorldXToZArg, double realWorldYToZArg)
{
	hDerivativeRes = new int[depthStride * height];
	vDerivativeRes = new int[depthStride * height];
	tmpPixelBuffer = new byte[pixelStride * height * 3];

	maxHistogramSize = deviceMaxDepth * 48 * 2;
	histogram = new int[maxHistogramSize];	//allocate enough memory

	realWorldXToZ = realWorldXToZArg;
	realWorldYToZ = realWorldYToZArg;
	::deviceMaxDepth = deviceMaxDepth;

	return 0;
}

proc_m derivativeFingerDetectorDispose()
{
	if (hDerivativeRes != NULL)
	{
		delete [] hDerivativeRes;
	}

	if (vDerivativeRes != NULL)
	{
		delete [] vDerivativeRes;
	}

	if (histogram != NULL)
	{
		delete [] histogram;
	}

	if (tmpPixelBuffer != NULL)
	{
		delete [] tmpPixelBuffer;
	}

	return 0;
}

proc_m derivativeFingerDetectorWork(proc_para_depth, double fingerWidthMin, double fingerWidthMax)
{
	sobel(srcDepthPtr, NULL, width, height, depthStride, pixelStride);
	//sobelLinear(srcDepthPtr, NULL, width, height, depthStride, pixelStride);
	findStrips(srcDepthPtr, dstPixelPtr, width, height, depthStride, pixelStride, fingerWidthMin, fingerWidthMax);
	generateOutputImage(srcDepthPtr, dstPixelPtr, width, height, depthStride, pixelStride);

	return 0;
}

proc_m derivativeFingerDetectorGetDerivativeFrame(int** hResPtr, int** vResPtr)	//not robust, just for debugging
{
	*hResPtr = hDerivativeRes;
	*vResPtr = vDerivativeRes;

	return 0;
}
