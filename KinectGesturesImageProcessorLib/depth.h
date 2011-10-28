#ifndef _DEPTH_H_
#define _DEPTH_H_

typedef unsigned char byte;
typedef unsigned short ushort;

#define proc_m extern "C" __declspec(dllexport) int								//processor function modifier
#define proc_para_depth ushort* srcDepthPtr, byte* dstPixelPtr, int width, int height, int depthStride, int pixelStride

#define srcDepth(row, col) ((srcDepthPtr + (row) * depthStride + (col)))
#define bufferDepth(bufDepthPtr, row, col) (((bufDepthPtr) + (row) * depthStride + (col)))

#define dstPixel(row, col) ((dstPixelPtr) + (row) * pixelStride + (col) * 3)
//#define RGB(r, g, b) (((r) << 16) | ((g) << 8) | (b))

#define newBufferDepth() (new ushort[depthStride * height])
#define delBufferDepth(bufDepthPtr) (delete [] (bufDepthPtr))
//#define copyBufferDepth(fromPtr, toPtr) { for (int copy_buffer_i = 0; copy_buffer_i < height; copy_buffer_i++) memcpy((void*)(toPtr + copy_buffer_i * width), (void*)(fromPtr + copy_buffer_i * width), width * sizeof(ushort)); }
	
#ifndef NULL
#define NULL 0
#endif

#endif
