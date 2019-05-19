using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace LoadBalancer
{
    class LoadBalancer
    {

        private static StreamWriter logptr;
        private static TcpListener serverSocket;
        private static TcpListener ClientsSocket;
        private static TcpListener ClientsSocket2;
        private static TcpListener ClientsSocket3;
        private static object QueueLock = new object();
        private static ConcurrentDictionary<string, TcpClient> ServersList = new ConcurrentDictionary<string, TcpClient>();
        private static ConcurrentDictionary<string, string> ServerConnections = new ConcurrentDictionary<string, string>();
        private static Dictionary<string, string> properties = new Dictionary<string, string>();
        private static Dictionary<string, int> ServerLoad = new Dictionary<string, int>();
        private static Queue<string> roundrobbin = new Queue<string>();
        private static Hashtable serverPorts = new Hashtable();

        private static string[] serverKeys;

        private static string text1 = string.Empty;
        private static string text2 = string.Empty;
        private static string text3 = string.Empty;
        private static int ServerCount1= 0;
        private static int ServerCount2= 0;
        private static int ServerCount3= 0;
        private static int ServerCount= 0;
        private static int ClientsCount = 0;
        private static int totalmessageCount = 0;
        private static int recMsgIl = 0;
        static void Main(string[] args)
        {
            LoadProperties();
            Console.Title =SystemProperties.ServerName;
            SetupLB();
            Console.ReadLine();
        }
        private static void SetupLB()
        {
            Console.WriteLine("Setting up Load Balancer...");

            serverSocket.Start(SystemProperties.ConnectionBufferSize);
            serverSocket.BeginAcceptTcpClient(AcceptServerCallback, serverSocket);
            Console.WriteLine("Load Balancer Listening on"+ SystemProperties.ServerPort +" for Servers ");
           
            ClientsSocket.Start(100000);
            ClientsSocket.BeginAcceptTcpClient(AcceptCallback, ClientsSocket);
            Console.WriteLine("Load Balancer Listening on" + SystemProperties.ClientPort + "for Clients ");

            ClientsSocket2.Start(100000);
            ClientsSocket2.BeginAcceptTcpClient(AcceptCallback2, ClientsSocket2);
            Console.WriteLine("Load Balancer Listening on" + (SystemProperties.ClientPort+1) + "for Clients ");
            ClientsSocket3.Start(100000);

            ClientsSocket3.BeginAcceptTcpClient(AcceptCallback3, ClientsSocket3);
            Console.WriteLine("Load Balancer Listening on" + (SystemProperties.ClientPort+2) + "for Clients ");



            Thread PingThread = new Thread(PingServer);
           PingThread.Start();

        }
        private static void LoadProperties()
        {
            try
            {
                string fileName = "properties.txt";
                string fullPath = Path.Combine(Path.GetDirectoryName(System.Reflection.Assembly.GetEntryAssembly().Location), fileName);

                foreach (var row in File.ReadAllLines(fullPath))
                {
                    properties.Add(row.Split('=')[0], string.Join("=", row.Split('=').Skip(1).ToArray()));
                }

                SystemProperties.ServerName = properties["ServerName"];
                SystemProperties.ServerIP = properties["ServerIP"];
                SystemProperties.ServerPort = Convert.ToInt32(properties["ServerPort"]);
                SystemProperties.ClientPort = Convert.ToInt32(properties["ClientPort"]);
                SystemProperties.loadbuffer = Convert.ToInt32(properties["loadbuffer"]);
                SystemProperties.totalNoOfServers = Convert.ToInt32(properties["totalNoOfServers"]);
                SystemProperties.logfilePath = properties["logFilePath"];
                SystemProperties.ConnectionBufferSize = Convert.ToInt32(properties["ConnectionBufferSize"]);
                serverKeys = new string[SystemProperties.totalNoOfServers];
                logptr = File.AppendText(SystemProperties.logfilePath);
                serverSocket = new TcpListener(System.Net.IPAddress.Any, SystemProperties.ServerPort);
                ClientsSocket = new TcpListener(System.Net.IPAddress.Any, SystemProperties.ClientPort);
                ClientsSocket2 = new TcpListener(System.Net.IPAddress.Any, SystemProperties.ClientPort+1);
                ClientsSocket3 = new TcpListener(System.Net.IPAddress.Any, SystemProperties.ClientPort+2);

            }
            catch (Exception ex)
            {

                Console.WriteLine("Exception while Loading Properties " + ex.GetBaseException());
            }
        }

        private static void AcceptCallback(IAsyncResult AR)
        {

            
            try
            {
                ++ClientsCount;
                TcpListener listener = (TcpListener)AR.AsyncState;
                TcpClient Client = listener.EndAcceptTcpClient(AR);

                byte[] data = new Byte[200];
                 if (ServersList.Count <=0)
                {
                    text1 = "NO SERVER Available";

                }
                else  
                {
                    if (ServerCount1 == 0 || ServerCount1 == 5)
                    {
                        text1 = getAvailableServer();
                        ServerCount1 = 0;
                    }
                }
                ++ServerCount1;


                var buffer = Encoding.UTF8.GetBytes(text1);
                Buffer.BlockCopy(buffer, 0, data, 0, buffer.Length);
                Client.GetStream().Write(data, 0, data.Length);
                Client.Close();   
            }
            catch (Exception ex)
            {
                Console.WriteLine("Exception in Accept Call back " + ex.GetBaseException());
                return;
            }
           
            ClientsSocket.BeginAcceptTcpClient(AcceptCallback, ClientsSocket);
        }
        private static void AcceptCallback2(IAsyncResult AR)
        {


            try
            {
                ++ClientsCount;
                TcpListener listener = (TcpListener)AR.AsyncState;
                TcpClient Client = listener.EndAcceptTcpClient(AR);

                byte[] data = new Byte[200];
              

                if (ServersList.Count <= 0)
                {
                    text2 = "NO SERVER Available";

                }
                else
                {
                    if (ServerCount2 == 0 || ServerCount2 == 5)
                    {
                        text2 = getAvailableServer();
                        ServerCount2 = 0;
                    }
                }
                ++ServerCount2;



                var buffer = Encoding.UTF8.GetBytes(text2);
                Buffer.BlockCopy(buffer, 0, data, 0, buffer.Length);
                Client.GetStream().Write(data, 0, data.Length);
                Client.Close();
            }
            catch (Exception ex)
            {
                Console.WriteLine("Exception in Accept Call back " + ex.GetBaseException());
                return;
            }

            ClientsSocket2.BeginAcceptTcpClient(AcceptCallback2, ClientsSocket2);
        }
        private static void AcceptCallback3(IAsyncResult AR)
        {

         

            try
            {
                ++ClientsCount;
                TcpListener listener = (TcpListener)AR.AsyncState;
                TcpClient Client = listener.EndAcceptTcpClient(AR);

                //  Servers.GetStream().BeginRead(mySocket.bytes, 0, mySocket.bytes.Length, new System.AsyncCallback(AsyncRead), mySocket);
                //  serverSocket.BeginAcceptTcpClient(AcceptCallback, serverSocket);
                byte[] data = new Byte[200];
            
                if (ServersList.Count <= 0)
                {
                    text3 = "NO SERVER Available";

                }
                else
                {
                    if (ServerCount3 == 0 || ServerCount3 == 5)
                    {
                        text3 = getAvailableServer();
                        ServerCount3 = 0;
                    }
                }
                ++ServerCount3;


                var buffer = Encoding.UTF8.GetBytes(text3);
                Buffer.BlockCopy(buffer, 0, data, 0, buffer.Length);
                Client.GetStream().Write(data, 0, data.Length);
                Client.Close();
            }
            catch (Exception ex)
            {
                Console.WriteLine("Exception in Accept Call back :TEXT::  "+text3 +" :::  "+ ex.GetBaseException());
                return;
            }

            ClientsSocket3.BeginAcceptTcpClient(AcceptCallback3, ClientsSocket3);
        }
        private static void AcceptServerCallback(IAsyncResult AR)
        {

            try
            {
                TcpListener listener = (TcpListener)AR.AsyncState;
                TcpClient Servers = listener.EndAcceptTcpClient(AR);
                ++ServerCount;
                string [] Serverdata = getServerName(Servers);

                byte[] data = new Byte[200];
                byte[] data2 = new Byte[200];
                string text = Serverdata[1] + ":" + Serverdata[2].Replace("\0", string.Empty) + "+" + Serverdata[0] + "+ SERVER CONNECTED";

               
                //ServerLoad.Add(Serverdata[0], 0);
                foreach (KeyValuePair<string, TcpClient> entry in ServersList)
                {
                   
                    var buffer = Encoding.UTF8.GetBytes(text);
                    Buffer.BlockCopy(buffer, 0, data, 0, buffer.Length);

                    entry.Value.GetStream().Write(data, 0, data.Length);

                }
                foreach (KeyValuePair<string, string> entry in ServerConnections)
                {

                    var buffer = Encoding.UTF8.GetBytes(entry.Value);
                    Buffer.BlockCopy(buffer, 0, data2, 0, buffer.Length);

                   Servers.GetStream().Write(data2, 0, data2.Length);

                }

                if (ServerConnections.ContainsKey(Serverdata[0]))
                {
                    string s;
                    ServerConnections.TryRemove(Serverdata[0], out s);
                    ServerConnections.TryAdd(Serverdata[0], text);

                }
                else
                {
                    ServerConnections.TryAdd(Serverdata[0], text);

                }
                if (ServersList.ContainsKey(Serverdata[0]))
                {
                    TcpClient s;
                    ServersList.TryRemove(Serverdata[0], out s);
                    ServersList.TryAdd(Serverdata[0], Servers);

                }
                else
                {
                    ServersList.TryAdd(Serverdata[0], Servers);
                   
                }
                lock (QueueLock)
                {
                    roundrobbin.Enqueue(Serverdata[0]);
                }
             
            }
            catch (Exception ex)
            {
                Console.WriteLine("Exception in Accept Call back " + ex.GetBaseException());
             
            }
            serverSocket.BeginAcceptTcpClient(AcceptServerCallback, serverSocket);


        }

        private static string[] getServerName(TcpClient clientSocket)
        {
            byte[] readBuffer = new Byte[200];
            int numberOfBytesRead = clientSocket.GetStream().Read(readBuffer, 0, readBuffer.Length);
            string returned = Encoding.UTF8.GetString(readBuffer, 0, numberOfBytesRead);
          
            string [] servername = returned.Split("+");
            if (serverPorts.ContainsKey(servername[0]))
            {
                serverPorts.Remove(servername[0]);
                serverPorts.Add(servername[0], servername[3]);

            }
            else
            {
                serverPorts.Add(servername[0], servername[3]);

            }
            return servername;
        }

        private static string getAvailableServer()
        {
            string key = String.Empty;
            lock (QueueLock)
            {
                key = roundrobbin.Dequeue();


                TcpClient server;
                string text = string.Empty;
                string[] Actualtext = new string[3];
                try
                {
                    if (ServersList.TryGetValue(key, out server))
                    {
                        if (server.Connected)
                        {
                            text = server.Client.RemoteEndPoint.ToString();
                            Actualtext = text.Split(":");
                            Actualtext[0] = Actualtext[0] + ":" + serverPorts[key];

                            roundrobbin.Enqueue(key);

                        }
                        else
                        {
                            ServersList.TryRemove(key, out server);
                            if (roundrobbin.Count > 0)
                            {
                                text = getAvailableServer();

                            }
                            else
                            {
                                text = "NO SERVER AVAILABLE";
                            }
                        }
                    }
                    else
                    {
                        text = "NO SERVER AVAILABLE";
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Exception while Get Available server " + ex.GetBaseException());
                }
                return Actualtext[0];
            }
          
        }
       


        private static void PingServer()
        {
            while (true)
            {
                while(ServersList.Count > 0)
                {
                   
                    
                    byte[] ping = new byte[200];
                    foreach (KeyValuePair<string, TcpClient> entry in ServersList)
                    {
                    try
                        {

                            entry.Value.GetStream().Write(ping, 0, 200);
                            

                        }
                        catch (Exception ex)
                        {
                            byte[] data = new Byte[100];
                            TcpClient ot;
                            ServersList.TryRemove(entry.Key,out ot);
                            serverPorts.Remove(entry.Key);
                            Console.WriteLine("SERVCER CLOSED : " + entry.Key);

                            foreach (KeyValuePair<string, TcpClient> entry2 in ServersList)
                            {
                                
                                string text = entry.Key + "+ SERVER CLOSED";

                                var buffer = Encoding.UTF8.GetBytes(text);
                                Buffer.BlockCopy(buffer, 0, data, 0, buffer.Length);
                                try
                                {
                                    entry2.Value.GetStream().Write(data, 0, data.Length);
                                }
                                catch (Exception ee)
                                {
                                    TcpClient otp;

                                    ServersList.TryRemove(entry2.Key, out otp);
                                    ServerLoad.Remove(entry2.Key);
                                    Console.WriteLine("SERVCER CLOSED : " + entry2.Key);


                                }
                            }
                        }


                    }

                    Thread.Sleep(6000);
                }
              
            }
        }
        public class SystemProperties
        {
            public static string ServerName = String.Empty;
            public static int ServerPort = 0;
            public static int ClientPort = 0;
            public static string ServerIP = String.Empty;
            public static int loadbuffer = 0;
            public static int totalNoOfServers = 0;
            public static string logfilePath = string.Empty;
            public static int ConnectionBufferSize = 33000;

        }

    }
}
