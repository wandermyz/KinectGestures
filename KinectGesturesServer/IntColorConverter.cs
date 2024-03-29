﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Media;

namespace KinectGesturesServer
{
    public static class IntColorConverter
    {
        private static readonly Color[] ColorList = { Colors.Red, Colors.Green, Colors.Yellow, Colors.Blue, Colors.Cyan, Colors.Magenta };
        public static Color ToColor(int n)
        {
            return ColorList[n % ColorList.Count()];
        }
    }
}
