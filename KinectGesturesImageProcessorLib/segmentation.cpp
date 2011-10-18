#include "dip.h"

#include <deque>
#include <vector>
#include <algorithm>
using namespace std;

typedef pair<int, int> pos;

bool sortComparator(const vector<pos>& a, const vector<pos>& b)
{
	return a.size() > b.size();
}

//find all continuous blocks from a black&white bitmap, and return the center point of each block. If blocks are more than maxNum, return the first maxNum bigger ones. 
//dstPtr won't be used. Set it to 0.
//return the num of points, along with an integer array with coordinate pairs (x, y) stored in resultPtr;
proc_m extractPoints(proc_para, byte* switchPtr, int maxNum, int maxArea, int* resultPtr)
{
	vector<vector<pos> > blocks; 
	deque<pos> searchingQueue;
	
	memset(switchPtr, 0, height * stride * sizeof(byte));	//use switchPtr as visit flag
	
	//find blocks
	for (int i = 0; i < height; i++)
	{
		for (int j = 0; j < width; j++)
		{
			if (*bufferPixelBit(switchPtr, i, j))
			{
				continue;
			}

			*bufferPixelBit(switchPtr, i, j) = 0xFF;	
			
			if (!*srcPixelBit(i, j))
			{
				continue;
			}

			//create a new block
			blocks.push_back(vector<pair<int, int> >());
			blocks[blocks.size() - 1].push_back(pos(i, j));
			searchingQueue.push_back(pos(i, j));

			while(!searchingQueue.empty())
			{
				pos curr = searchingQueue.front();
				searchingQueue.pop_front();

				//search the neighbor, BFS
				for (int ni = curr.first - 1; ni <= curr.first + 1; ni++)	//we only need to search neighbors "behind" the current point
				{
					 if (ni < 0 || ni >= height)
					 {
						 continue;
					 }

					 for (int nj = curr.second - 1; nj <= curr.second + 1; nj++)
					 {
						 if ( (ni == 0 && nj == 0) || (nj < 0 || nj >= width) || *bufferPixelBit(switchPtr, ni, nj))
						 {
							 continue;	//skip self, out of bound point and visited point
						 }

						 *bufferPixelBit(switchPtr, ni, nj) = 0xFF;	//visited
						 
						 if (*srcPixelBit(ni, nj))
						 {
							 blocks[blocks.size() - 1].push_back(pos(ni, nj));
							 searchingQueue.push_back(pos(ni, nj));
						 }
					 }
				}
			}
		}
	}

	//sort the blocks from max to min
	sort(blocks.begin(), blocks.end(), sortComparator);

	int pi;
	for (pi = 0; pi < blocks.size() && pi < maxNum; pi++)
	{
		if (blocks[pi].size() < maxArea)
		{
			break;	//then pi is the num of points
		}

		int xSum = 0, ySum = 0;
		for (int j = 0; j < blocks[pi].size(); j++)
		{
			xSum += blocks[pi][j].second;	//x = col = second
			ySum += blocks[pi][j].first;
		}

		resultPtr[2 * pi] = xSum / blocks[pi].size();
		resultPtr[2 * pi + 1] = ySum / blocks[pi].size(); 
	}

	return pi;
}