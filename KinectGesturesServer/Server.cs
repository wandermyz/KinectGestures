using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net.Sockets;
using System.Net;
using System.Threading;
using System.Diagnostics;
using System.IO;

namespace KinectGesturesServer
{
    public class Server
    {
        private TcpListener tcpServer;
        private Thread tcpServerThread;
        private NuiSensor sensor;

        private bool stopRequested = false;
        private bool serverRunning = false;

        private List<TcpClient> clients;
        private List<StreamWriter> clientStreamWriters;


        public int ClientCount { get { return clients.Count; } }
        public bool IsServerRunning { get { return serverRunning; } }

        public event EventHandler ClientConnected;
        public event EventHandler ClientDisconnected;
        public event EventHandler ServerStopped;

        public Server(NuiSensor sensor)
        {
            tcpServer = new TcpListener(new IPAddress(new byte[] { 192, 168, 53, 130 }), 2011);
            tcpServerThread = new Thread(tcpServerThreadWorker);
            clients = new List<TcpClient>();
            clientStreamWriters = new List<StreamWriter>();

            this.sensor = sensor;

            sensor.HandTracker.HandCreate += new EventHandler<OpenNI.HandCreateEventArgs>(HandTracker_HandCreate);
            sensor.HandTracker.HandUpdate += new EventHandler<OpenNI.HandUpdateEventArgs>(HandTracker_HandUpdate);
            sensor.HandTracker.HandDestroy += new EventHandler<OpenNI.HandDestroyEventArgs>(HandTracker_HandDestroy);
        }

        public void Start()
        {
            tcpServerThread.Start();
        }

        public void Stop()
        {
            foreach (TcpClient client in clients)
            {
                client.Close();
            }

            stopRequested = true;
        }

        private void tcpServerThreadWorker()
        {
            try
            {
                tcpServer.Start();
                serverRunning = true;
                Trace.WriteLine("Server started on " + (tcpServer.LocalEndpoint as IPEndPoint).Address.ToString() + ":" + (tcpServer.LocalEndpoint as IPEndPoint).Port.ToString());
                while (!stopRequested)
                {
                    TcpClient client = tcpServer.AcceptTcpClient();
                    clients.Add(client);
                    clientStreamWriters.Add(new StreamWriter(client.GetStream(), Encoding.UTF8));
                    Trace.WriteLine((client.Client.RemoteEndPoint as IPEndPoint).Address.ToString() + " connected on " + (client.Client.LocalEndPoint as IPEndPoint).Port.ToString());
                    if (ClientConnected != null)
                    {
                        ClientConnected(this, EventArgs.Empty);
                    }
                }
            }
            catch (SocketException e)
            {
                Trace.WriteLine("Server stopped: " + e.ToString());
            }
            finally
            {
                serverRunning = false;
                stopRequested = false;
                tcpServer.Stop();
                if (ServerStopped != null)
                {
                    ServerStopped(this, EventArgs.Empty);
                }
            }
        }

        private void broadcast(string message)
        {
            for (int i = 0; i < clients.Count; i++)
            {
                if (clients[i].Connected)
                {
                    try
                    {
                        StreamWriter sw = clientStreamWriters[i];
                        sw.WriteLine(message);
                        sw.Flush();
                    }
                    catch (IOException e)
                    {
                        Trace.WriteLine("Broadcast to client " + i.ToString() + " failed: " + e.ToString());
                        if (!clients[i].Connected)
                        {
                            if (ClientDisconnected != null)
                            {
                                ClientDisconnected(this, EventArgs.Empty);
                            }

                            clients.RemoveAt(i);
                            i--;
                            Trace.WriteLine("Client " + i.ToString() + " disconnected");
                        }
                    }
                }
            }
        }

        #region event handler

        void HandTracker_HandCreate(object sender, OpenNI.HandCreateEventArgs e)
        {
            broadcast("HC " + e.UserID.ToString());
        }

        void HandTracker_HandUpdate(object sender, OpenNI.HandUpdateEventArgs e)
        {
            broadcast(string.Format("HU {0},{1},{2},{3}", e.UserID, e.Position.X, e.Position.Y, e.Position.Z));
        }

        void HandTracker_HandDestroy(object sender, OpenNI.HandDestroyEventArgs e)
        {
            broadcast("HD " + e.UserID.ToString());
        }

        #endregion
    }
}
