#include "depth.h"
#include <memory.h>
#include <assert.h>

static int *hDerivativeRes = NULL, *vDerivativeRes = NULL, *histogram = NULL;
static int maxHistogramSize = 0;

int sobel(proc_para_depth)
{
	const double tpl[5][5] = 
	{
		{1, 2, 0, -2, 1},
		{4, 8, 0, -8, 4},
		{6, 12, 0, -12, -6},
		{4, 8, 0, -8, -4},
		{1, 2, 0, -2, -1}
	};

	const int tpl_offset = 2;

	for (int i = 0; i < height; i++)
	{
		for (int j = 0; j < width; j++)
		{
			double depthH = 0, depthV = 0;
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

					depthH += tpl[ti][tj] * *srcDepth(neighbor_row, neighbor_col);
					depthV += tpl[tj][ti] * *srcDepth(neighbor_row, neighbor_col);
				}
			}
			*bufferDepth(hDerivativeRes, i, j) = (int)(depthH + 0.5);
			*bufferDepth(vDerivativeRes, i, j) = (int)(depthV + 0.5);
		}
	}

	return 0;
}

void generateOutputImage(proc_para_depth)
{
	//generate histogram
	int min = 65535, max = -65535;
	for (int i = 0; i < height; i++)
	{
		for (int j = 0; j < width; j++)
		{
			int h = *bufferDepth(hDerivativeRes, i, j);
			int v = *bufferDepth(vDerivativeRes, i, j);
			if (h > max) max = h;
			if (v > max) max = v;
			if (h < min) min = h;
			if (v < min) min = v;
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
			int h = *bufferDepth(hDerivativeRes, i, j);
			int v = *bufferDepth(vDerivativeRes, i, j);
			histogram[h - histogramOffset]++;
			histogram[v - histogramOffset]++;
		}
	}

	for (int i = 1; i < histogramSize; i++)
	{
		histogram[i] += histogram[i-1];
	}

	int points = width * height * 2;
	for (int i = 0; i < histogramSize; i++)
	{
		histogram[i] = (int)(256 * (1.0 - ((double)histogram[i] / (double)points)) + 0.5);
	}

	//draw the image
	for (int i = 0; i < height; i++)
	{
		for (int j = 0; j < width; j++)
		{
			dstPixel(i, j)[0] = 0;
			dstPixel(i, j)[1] = 0; //histogram[*bufferDepth(hDerivativeRes, i, j) - histogramOffset];
			dstPixel(i, j)[2] = histogram[*bufferDepth(vDerivativeRes, i, j) - histogramOffset];
		}
	}
}

proc_m derivativeFingerDetectorInit(proc_para_depth, int deviceMaxDepth)
{
	hDerivativeRes = new int[depthStride * height];
	vDerivativeRes = new int[depthStride * height];

	maxHistogramSize = deviceMaxDepth * 48 * 2;
	histogram = new int[maxHistogramSize];	//allocate enough memory

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

	return 0;
}

proc_m derivativeFingerDetectorWork(proc_para_depth)
{
	sobel(srcDepthPtr, NULL, width, height, depthStride, pixelStride);
	generateOutputImage(srcDepthPtr, dstPixelPtr, width, height, depthStride, pixelStride);

	return 0;
}

