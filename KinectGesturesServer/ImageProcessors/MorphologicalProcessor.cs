using System;
using System.Collections;
using System.Linq;
using System.Text;

namespace KinectGesturesServer.ImageProcessors
{
    public class MorphologicalProcessor
    {
        private BitArray bufferSrc, bufferDst, bufferSwitch;
        private int[,] neighborOffset;
        private int width, height;

        public MorphologicalProcessor(BitArray bufferSrc, BitArray bufferDst, BitArray bufferSwitch, int width, int height)
        {
            this.bufferSrc = bufferSrc;
            this.bufferSwitch = bufferSwitch;
            this.bufferDst = bufferDst;

            this.width = width;
            this.height = height;

            //[x, y]
            neighborOffset = new int[9, 2]
            {
                {0, 0}, {-1, -1}, {0, -1}, {1, -1}, {-1, 0}, {1, 0}, {-1, 1}, {0, 1}, {1, 1}
            };
        }

        private void dilate(BitArray bufferSrc, BitArray bufferDst)
        {
            for (int row = 0; row < height; row++)
            {
                for (int col = 0; col < width; col++)
                {
                    int index = row * width + col;
                    bufferDst[index] = false;
                    for (int i = 0; i < 9; i++)
                    {
                        int neighborRow = row + neighborOffset[i, 1];
                        int neighborCol = col + neighborOffset[i, 0];

                        if (neighborRow < 0 || neighborRow >= height || neighborCol < 0 || neighborCol >= width)
                        {
                            continue;
                        }

                        if (bufferSrc[neighborRow * width + neighborCol])
                        {
                            bufferDst[index] = true;
                            break;
                        }
                    }
                }
            }
        }

        private void erose(BitArray bufferSrc, BitArray bufferDst)
        {
            for (int row = 0; row < height; row++)
            {
                for (int col = 0; col < width; col++)
                {
                    int index = row * width + col;
                    bufferDst[index] = true;
                    for (int i = 0; i < 9; i++)
                    {
                        int neighborRow = row + neighborOffset[i, 1];
                        int neighborCol = col + neighborOffset[i, 0];

                        if (neighborRow < 0 || neighborRow >= height || neighborCol < 0 || neighborCol >= width)
                        {
                            //treat border as false
                            bufferDst[index] = false;
                            break;
                        }

                       if (!bufferSrc[neighborRow * width + neighborCol])
                        {
                            bufferDst[index] = false;
                            break;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Do the algorithm of "Open" in morphological processing
        /// </summary>
        public void OpenOperation()
        {
            erose(bufferSrc, bufferSwitch);
            dilate(bufferSwitch, bufferDst);
            //dilate(bufferSrc, bufferDst);
        }
    }
}
