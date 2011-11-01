using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using OpenNI;
using System.Diagnostics;

namespace KinectGesturesServer
{
    public class HandTracker
    {
        private Context context;
        public HandsGenerator handsGenerator;
        private GestureGenerator gestureGenerator;

        //private Point3D handPosition;
        //public Point3D HandPosition { get { return handPosition; } 

        public event EventHandler<HandCreateEventArgs> HandCreate;
        public event EventHandler<HandUpdateEventArgs> HandUpdate;
        public event EventHandler<HandDestroyEventArgs> HandDestroy;

        public bool IsTracking { get; private set; }

        public HandTracker(Context context)
        {
            this.context = context;           

            //gestureGenerator = context.FindExistingNode(NodeType.Gesture) as GestureGenerator;
            handsGenerator = context.FindExistingNode(NodeType.Hands) as HandsGenerator;
            
            //gestureGenerator.GestureRecognized += new EventHandler<GestureRecognizedEventArgs>(gestureGenerator_GestureRecognized);

            handsGenerator.HandCreate += new EventHandler<HandCreateEventArgs>(handsGenerator_HandCreate);
            handsGenerator.HandDestroy += new EventHandler<HandDestroyEventArgs>(handsGenerator_HandDestroy);
            handsGenerator.HandUpdate += new EventHandler<HandUpdateEventArgs>(handsGenerator_HandUpdate);

            handsGenerator.StartGenerating();
            //gestureGenerator.AddGesture("Wave");
 
            Trace.WriteLine("HandSensor initialized");
        }

        /*void gestureGenerator_GestureRecognized(object sender, GestureRecognizedEventArgs e)
        {
            Trace.WriteLine("Gesture recognized");
            //gestureGenerator.RemoveGesture(e.Gesture);
            handsGenerator.StartTracking(e.EndPosition);
        }*/

        public void StartTrackingAt(Point3D position)
        {
            handsGenerator.StopTrackingAll();
            handsGenerator.StartTracking(position);
        }

        void handsGenerator_HandUpdate(object sender, HandUpdateEventArgs e)
        {
            //Trace.WriteLine("Hand updated at " + e.Position.X + ", " + e.Position.Y + ", " + e.Position.Z);
            //handPosition = new Point3D(e.Position.X + 320, 240 - e.Position.Y, e.Position.Z);

            if (HandUpdate != null)
            {
                HandUpdate(this, e);
            }
        }

        public void handsGenerator_HandCreate(object sender, HandCreateEventArgs e)
        {
            Trace.WriteLine("Hand created");
            IsTracking = true;
            if (HandCreate != null)
            {
                HandCreate(this, e);
            }
        }   

        public void handsGenerator_HandDestroy(object sender, HandDestroyEventArgs e)
        {
            Trace.WriteLine("Hand lost");
            IsTracking = false;
            //gestureGenerator.AddGesture("Wave");

            if (HandDestroy != null)
            {
                HandDestroy(this, e);
            }
        }
    }
}
