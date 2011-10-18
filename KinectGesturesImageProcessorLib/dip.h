#ifndef _DIP_H_
#define _DIP_H_

typedef unsigned char byte;

#define proc_m extern "C" __declspec(dllexport) int								//processor function modifier
#define proc_para byte* srcPtr, byte* dstPtr, int width, int height, int stride		//common parameters

#define srcPixelBit(row, col) ((srcPtr + (row) * stride + (col)))
#define dstPixelBit(row, col) ((dstPtr + (row) * stride + (col)))
#define bufferPixelBit(bufferPtr, row, col) (((bufferPtr) + (row) * stride + (col)))

#define newBufferBit() ((int)(new byte[stride*height]))
#define delBufferBit(intPtr) (delete [] (intPtr))	//TODO: memory leak?
#define copyBufferBit(fromPtr, toPtr) { for (int copy_buffer_i = 0; copy_buffer_i < height; copy_buffer_i++) memcpy((void*)(toPtr + copy_buffer_i * stride), (void*)(fromPtr + copy_buffer_i * stride), width); }

#define bwBit(value) ((value) ? 0xFF : 0)

#endif