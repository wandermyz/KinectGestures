#include "depth.h"
#include <memory.h>
#include <assert.h>
#include <math.h>
#include <vector>
#include <algorithm>
using namespace std;

//#define FINGER_WIDTH_MIN 2
//#define FINGER_WIDTH_MAX 40	//in millimeters
#define FINGER_EDGE_THRESHOLD 1000
#define STRIP_MAX_BLANK_PIXEL 10
#define FINGER_MIN_PIXEL_LENGTH 10
#define FINGER_TO_HAND_OFFSET 100   //in millimeters

typedef enum
{
	StripSmooth,
	StripRising,
	StripMidSmooth,
	StripFalling
} StripState;

typedef struct Strip
{
	int row;
	int leftCol, rightCol;
	bool visited;
	struct Strip(int row, int leftCol, int rightCol) : row(row), leftCol(leftCol), rightCol(rightCol), visited(false) { }
} Strip;

typedef struct Finger
{
	int tipX, tipY, tipZ;
	int endX, endY, endZ;
	struct Finger(int tipX, int tipY, int tipZ, int endX, int endY, int endZ) : tipX(tipX), tipY(tipY), tipZ(tipZ), endX(endX), endY(endY), endZ(endZ) { }
	bool operator<(const Finger& ref) const { return endY - tipY > ref.endY - ref.tipY; }	//sort more to less
} Finger;

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

					ushort srcDepthVal = *srcDepth(neighbor_row, neighbor_col);
					depthH += tpl[ti][tj] * (srcDepthVal == 0 ? deviceMaxDepth : srcDepthVal);
					depthV += tpl[tj][ti] * *srcDepth(neighbor_row, neighbor_col);
				}
			}
			*bufferDepth(hDerivativeRes, i, j) = (int)(depthH + 0.5);
			*bufferDepth(vDerivativeRes, i, j) = (int)(depthV + 0.5);
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
			if (bufferPixel(tmpPixelBuffer, i, j)[0] == 255 || bufferPixel(tmpPixelBuffer, i, j)[1] == 255 || bufferPixel(tmpPixelBuffer, i, j)[2] == 255)
			{
				dstPixel(i ,j)[0] = bufferPixel(tmpPixelBuffer, i, j)[0];
				dstPixel(i ,j)[1] = bufferPixel(tmpPixelBuffer, i, j)[1];
				dstPixel(i ,j)[2] = bufferPixel(tmpPixelBuffer, i, j)[2];
			}
			else
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
				//dstPixel(i, j)[1] = 0;
			}
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

void convertProjectiveToRealWorld(int px, int py, int depth, double& rx, double& ry, int width, int height)
{
	rx = ((double)px / (double)width - 0.5) * depth * realWorldXToZ;
	ry = (0.5 - (double)py / (double)height) * depth * realWorldYToZ;
}

//strips: first vector: rows; second vector: a list of all strip in a row;
void findStrips(proc_para_depth, double fingerWidthMin, double fingerWidthMax, vector<vector<Strip> >& strips)
{
	for (int i = 0; i < height; i++)
	{
		strips.push_back(vector<Strip>());

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
					int depth = *srcDepth(i, (partialMaxPos + partialMinPos) / 2);	//use the middle point of the strip to measure depth, assuming it is the center of the finger
					double distSquared = distSquaredInRealWorld(
						partialMaxPos, i, depth,
						partialMinPos, i, depth,
						width, height);

					if (distSquared >= fingerWidthMin * fingerWidthMin && distSquared <= fingerWidthMax * fingerWidthMax)
					{
						for (int tj = partialMaxPos; tj <= partialMinPos; tj++)
						{
							//bufferPixel(tmpPixelBuffer, i, tj)[0] = 0;
							bufferPixel(tmpPixelBuffer, i, tj)[1] = 255;
							//bufferPixel(tmpPixelBuffer, i, tj)[2] = 0;
						}
						strips[i].push_back(Strip(i, partialMaxPos, partialMinPos));
						
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

//handhint: the result for estimating the hand position, in real world coordinate. int x, int y, int z, int pixelLength. pixel lenth is used as the measure of confidence.
int findFingers(proc_para_depth, double fingerLengthMin, double fingerLengthMax, vector<vector<Strip> >& strips, int maxFingers, int* resultPtr, int* handHint)
{
	vector<Strip*> stripBuffer;	//used to fill back
	vector<Finger> fingers;

	for (int i = 0; i < height; i++)
	{
		for (vector<Strip>::iterator it = strips[i].begin(); it != strips[i].end(); ++it)
		{
			if (it->visited)
			{
				continue;
			}

			stripBuffer.clear();
			stripBuffer.push_back(&(*it));
			it->visited = true;

			//search down
			int blankCounter = 0;
			for (int si = i; si < height; si++)
			{
				Strip* currTop = stripBuffer[stripBuffer.size() - 1];

				//search strip
				bool stripFound = false;
				for (vector<Strip>::iterator sIt = strips[si].begin(); sIt != strips[si].end(); ++sIt)
				{
					if (sIt->visited)
					{
						continue;
					}

					if (sIt->rightCol > currTop->leftCol && sIt->leftCol < currTop->rightCol)	//overlap!
					{
						stripBuffer.push_back(&(*sIt));
						sIt->visited = true;
						stripFound = true;
						break;
					}
				}

				if (!stripFound) //blank
				{
					blankCounter++;
					if (blankCounter > STRIP_MAX_BLANK_PIXEL)
					{
						//Too much blank, give up
						break;
					}
				}
			}

			//check length
			Strip* first = stripBuffer[0];
			int firstMidCol = (first->leftCol + first->rightCol) / 2;
			Strip* last = stripBuffer[stripBuffer.size() - 1];
			int lastMidCol = (last->leftCol + last->rightCol) / 2;
			int depth = *srcDepth((first->row + last->row) / 2, (firstMidCol + lastMidCol) / 2);	//jst a try
			double lengthSquared = distSquaredInRealWorld(
				firstMidCol, first->row, depth, // *srcDepth(first->row, firstMidCol),
				lastMidCol, last->row, depth, //*srcDepth(last->row, lastMidCol),
				width, height
				);
			int pixelLength = last->row - first->row +1;
			
			if (pixelLength >= FINGER_MIN_PIXEL_LENGTH 
				&& lengthSquared >= fingerLengthMin * fingerLengthMin 
				&& lengthSquared <= fingerLengthMax * fingerLengthMax)	//finger!
			{
				//fill back
				int bufferPos = -1;
				for (int row = first->row; row <= last->row; row++)
				{
					int leftCol, rightCol;
					if (row == stripBuffer[bufferPos + 1]->row)	//find next detected row
					{
						bufferPos++;
						leftCol = stripBuffer[bufferPos]->leftCol;
						rightCol = stripBuffer[bufferPos]->rightCol;
					}
					else	//in blank area, interpolate
					{
						double ratio = (double)(row - stripBuffer[bufferPos]->row) / (double)(stripBuffer[bufferPos + 1]->row - stripBuffer[bufferPos]->row);
						leftCol = stripBuffer[bufferPos]->leftCol + (stripBuffer[bufferPos + 1]->leftCol - stripBuffer[bufferPos]->leftCol) * ratio;
						rightCol = stripBuffer[bufferPos]->rightCol + (stripBuffer[bufferPos + 1]->rightCol - stripBuffer[bufferPos]->rightCol) * ratio;
					}

					for (int col = leftCol; col <= rightCol; col++)
					{
						bufferPixel(tmpPixelBuffer, row, col)[0] = 255;
						//bufferPixel(tmpPixelBuffer, row, col)[1] = 255;
						bufferPixel(tmpPixelBuffer, row, col)[2] = 255;
					}
				}

				fingers.push_back(Finger(firstMidCol, first->row, depth, lastMidCol, last->row, depth));	//TODO: depth?
			}
		}
	}

	sort(fingers.begin(), fingers.end());
	int i;
	for (i = 0; i < maxFingers && i < fingers.size(); i++)
	{
		resultPtr[2 * i] = fingers[i].tipX;
		resultPtr[2 * i + 1] = fingers[i].tipY;
	}
	
	//hand hint	TODO: if tip and end are not in the same depth
	if(fingers.size() > 0)
	{
		double rx1, ry1, rx2, ry2;
		convertProjectiveToRealWorld(fingers[0].tipX, fingers[0].tipY, fingers[0].tipZ, rx1, ry1, width, height);
		convertProjectiveToRealWorld(fingers[0].endX, fingers[0].endY, fingers[0].endZ, rx2, ry2, width, height);
		double scale = FINGER_TO_HAND_OFFSET / sqrt((rx2 - rx1) * (rx2 - rx1) + (ry2 - ry1) * (ry2 - ry1));

		/*double rx = fingers[0].tipZ * realWorldXToZ;
		double ry = fingers[0].tipZ * realWorldYToZ;
		double dx = fingers[0].endX - fingers[0].tipX;
		double dy = fingers[0].endY - fingers[0].tipY;
		double scale = FINGER_TO_HAND_OFFSET / sqrt(rx * rx * dx * dx + ry * ry * dy * dy);
		handHint[0] = fingers[0].tipX + (int)(scale * dx + 0.5);
		handHint[1] = fingers[0].tipY + (int)(scale * dy + 0.5);*/

		handHint[0] = rx1 + (rx2 - rx1) * scale;
		handHint[1] = ry1 + (ry2 - ry1) * scale;

		handHint[2] = fingers[0].tipZ;
		handHint[3] = fingers[0].endY - fingers[0].tipY + 1;
	}

	return i;
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

proc_m derivativeFingerDetectorWork(proc_para_depth, double fingerWidthMin, double fingerWidthMax, double fingerLengthMin, double fingerLengthMax, int maxFingers, int* resultPtr, int* handHint)
{
	memset(tmpPixelBuffer, 0, pixelStride * height * 3);

	sobel(srcDepthPtr, NULL, width, height, depthStride, pixelStride);
	//sobelLinear(srcDepthPtr, NULL, width, height, depthStride, pixelStride);

	vector<vector<Strip> > strips;
	findStrips(srcDepthPtr, dstPixelPtr, width, height, depthStride, pixelStride, fingerWidthMin, fingerWidthMax, strips);
	int fingerNum = findFingers(srcDepthPtr, dstPixelPtr, width, height, depthStride, pixelStride, fingerLengthMin, fingerLengthMax, strips, maxFingers, resultPtr, handHint);
	generateOutputImage(srcDepthPtr, dstPixelPtr, width, height, depthStride, pixelStride);

	return fingerNum;
}

proc_m derivativeFingerDetectorGetDerivativeFrame(int** hResPtr, int** vResPtr)	//not robust, just for debugging
{
	*hResPtr = hDerivativeRes;
	*vResPtr = vDerivativeRes;

	return 0;
}
