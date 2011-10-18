#include "dip.h"

proc_m dilate(proc_para)
{
	for (int i = 0; i < height; i++)
	{
		for (int j = 0; j < width; j++)
		{
			bool result = false;
			for (int mi = 0; mi < 3; mi++)
			{
				for (int mj = 0; mj < 3; mj++)
				{
					int img_row = i + mi - 1;
					int img_col = j + mj - 1;
					if(img_row < 0 || img_row >= height || img_col < 0 || img_col >= width)
					{
						continue;	//TODO: How to?
					}

					if(*srcPixelBit(img_row, img_col))	
					{
						result = true;
						break;
					}
				}
				if(result)
					break;
			}
			*dstPixelBit(i, j) = bwBit(result);
		}
	}

	return 0;
}

proc_m erose(proc_para)
{
	bool mask[3][3] = { {1,1,1}, {1,1,1}, {1,1,1} };	

	for (int i = 0; i < height; i++)
	{
		for (int j = 0; j < width; j++)
		{
			bool result = true;
			for (int mi = 0; mi < 3; mi++)
			{
				for (int mj = 0; mj < 3; mj++)
				{
					int img_row = i + mi - 1;
					int img_col = j + mj - 1;
					if(img_row < 0 || img_row >= height || img_col < 0 || img_col >= width)
					{
						continue;	//TODO: How to?
					}

					if(!*srcPixelBit(img_row, img_col))	//TODO: (efficiency) TOBW 9 times more!
					{
						result = false;
						break;
					}
				}
				if(!result)
					break;
			}
			*dstPixelBit(i, j) = bwBit(result);
		}
	}

	return 0;
}

proc_m open(proc_para, byte* switchPtr)
{
	erose(srcPtr, switchPtr, width, height, stride);
	dilate(switchPtr, dstPtr, width, height, stride);
	return 0;
}