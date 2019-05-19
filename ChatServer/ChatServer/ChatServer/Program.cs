using ChatServer;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ChatServer
{
    class Program
    {
        const int bytesperlong = 4; // 32 / 8
        const int bitsperbyte = 8;
        private static Queue<byte[]> messageQueue = new Queue<byte[]>();
        private static Queue<byte[]> tempQueue = new Queue<byte[]>();
        private static Queue<byte[]> tempQueue2 = new Queue<byte[]>();
        private static Queue<byte[]> tempQueue3 = new Queue<byte[]>();
        private static Queue<byte[]> tempQueue4 = new Queue<byte[]>();
        private static Queue<byte[]> ServermessageQueue = new Queue<byte[]>();
        private static Queue<byte[]> ServerConmessageQueue = new Queue<byte[]>();
        private static Queue<byte[]> CCmessageQueue = new Queue<byte[]>();
        private static Object mainQueueLock = new Object();
        private static Object serverQueueLock = new Object();
        private static Object serverConQueueLock = new Object();
        private static Object CCQueueLock = new Object();
        private static AutoResetEvent MsgEvent = new AutoResetEvent(false);
        private static AutoResetEvent MsgEvent2 = new AutoResetEvent(false);
        private static AutoResetEvent MsgEvent3 = new AutoResetEvent(false);
        private static AutoResetEvent MsgEvent4 = new AutoResetEvent(false);
        private static Logger TcpLogger = new Logger();
        private static TcpListener serverSocket;
        private static TcpListener CCSocket;
        private static TcpListener forserverSocket;
        private static readonly List<MyTcpClient> clientList = new List<MyTcpClient>();
        private static readonly List<MyTcpClient> Loadbalancer = new List<MyTcpClient>();
        private static ConcurrentDictionary<int, MyTcpClient> ClientsList = new ConcurrentDictionary<int, MyTcpClient>();
        private static ConcurrentDictionary<int, TcpClient> OtherServer = new ConcurrentDictionary<int, TcpClient>();
        private static ConcurrentDictionary<int, string[]> ServerConnections = new ConcurrentDictionary<int, string[]>();
        private static ConcurrentDictionary<int, TcpClient> OtherServerRec = new ConcurrentDictionary<int, TcpClient>();
        private static ConcurrentDictionary<int, int> OtherServerClient = new ConcurrentDictionary<int, int>();
        private static Dictionary<string, string> properties = new Dictionary<string, string>();
  
        private static int totalmessageCount = 0;
        private static int CC = 0;
        private static int WW = 0;
        private static int recived = 0;
        private static int RecievedFromClient = 0;
        private static int SentToClientFromClient = 0;
        private static int SentToClientFromServer = 0;
        private static int SentToServerFromClient = 0;
        private static int RecievedFromServer = 0;
        private static int readOff = 0;
        private static int ReadSize = 0;


        private static int sentOther = 0;
        private static int TotalRead = 0;
        private static int TotalRead2 = 0;
        private static int totalotherclient = 0;
        private static byte[] enqByte;
        private static byte[] ServerenqByte;

        static void Main()
        {
         
            LoadProperties();
            Console.Title = SystemProperties.ServerName.ToString();
            SetupServer();
            SetupRouter();
            Console.ReadKey();
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

                SystemProperties.ServerName = Convert.ToInt32(properties["ServerName"]);
                SystemProperties.ServerIP = properties["ServerIP"];
                SystemProperties.ServerPort = Convert.ToInt32(properties["ServerPort"]);
                SystemProperties.LBIP = properties["LBIP"];
                SystemProperties.LBPort = Convert.ToInt32(properties["LBPort"]);
                SystemProperties.logfilePath = properties["logFilePath"];
                SystemProperties.logfileName = properties["logFileName"];
                SystemProperties.logExFilePath = properties["logExFilePath"];
                SystemProperties.logExFileName = properties["logExFileName"];
                SystemProperties.ConnectionBufferSize = Convert.ToInt32(properties["ConnectionBufferSize"]);
                SystemProperties.forServerPort = Convert.ToInt32(properties["forServerPort"]);
                SystemProperties.ClientStartRange = Convert.ToInt32(properties["ClientStartRange"]);
                TcpLogger.initiateLogger(SystemProperties.logfilePath,SystemProperties.logfileName, SystemProperties.logExFilePath,SystemProperties.logExFileName);
                serverSocket = new TcpListener(System.Net.IPAddress.Any, SystemProperties.ServerPort);
                forserverSocket = new TcpListener(System.Net.IPAddress.Any, SystemProperties.forServerPort);
                CCSocket = new TcpListener(System.Net.IPAddress.Any, 9001);


            }
            catch (Exception ex)
            {

                Console.WriteLine("Exception while Loading Properties " + ex.GetBaseException());
            }
        }
      
        private static void SetupRouter()
        {
            Thread RouterThread = new Thread(MessageRouting);
            RouterThread.Start();

            Thread ServerThread = new Thread(ServerMessageRouting);
            ServerThread.Start();
            Thread ServerThread33 = new Thread(CCMessageRouting);
            ServerThread33.Start();
            Thread ServerThread2 = new Thread(ServerConnectionMessageRouting);
            ServerThread2.Start();
             Thread ServerThread3 = new Thread(calculation);
            ServerThread3.Start();
  

            RouterThread.Join();
            ServerThread.Join();
            Console.WriteLine("CLOSING APPLICATION: ");
        }
        private static void SetupServer()
        {
            try
            {
                TcpLogger.info("Going to make connection with Load Balancer : IP : " + SystemProperties.LBIP + " Port " + SystemProperties.LBPort);
                System.Net.Sockets.TcpClient clientSocket = new System.Net.Sockets.TcpClient();
                clientSocket.Connect(SystemProperties.LBIP, SystemProperties.LBPort);
                TcpLogger.info("Successfully Connected to Load Balancer");

                MyTcpClient loadbalancer = new MyTcpClient(clientSocket);
                Loadbalancer.Add(loadbalancer);
                byte[] data = new Byte[loadbalancer.LBbytes.Length];
                string text = SystemProperties.ServerName + "+" + SystemProperties.ServerIP + "+" + SystemProperties.forServerPort + "+" + SystemProperties.ServerPort;

                TcpLogger.info("Going to send information to Load Balance : " + text);
                var buffer = Encoding.UTF8.GetBytes(text);
                Buffer.BlockCopy(buffer, 0, data, 0, buffer.Length);
                clientSocket.GetStream().Write(data, 0, data.Length);

                TcpLogger.info("Information Successfully Sent to Load Balancer ");

                TcpLogger.info("Started Reading for Future messages on Load Balancer Stream, Message Size : 200");
                loadbalancer.TcpClient.GetStream().BeginRead(loadbalancer.LBbytes, 0, loadbalancer.LBbytes.Length, new System.AsyncCallback(AsyncLBRead), loadbalancer);

                serverSocket.Start(SystemProperties.ConnectionBufferSize);
                serverSocket.BeginAcceptTcpClient(AcceptCallback, serverSocket);

                CCSocket.Start(SystemProperties.ConnectionBufferSize);
                CCSocket.BeginAcceptTcpClient(CCAcceptCallback, CCSocket);
                // Log("Server Setup Completed", logptr);

                TcpLogger.info("Server Started for Client Connections ");

                forserverSocket.Start(SystemProperties.ConnectionBufferSize);
                forserverSocket.BeginAcceptTcpClient(AcceptServerCallback, serverSocket);

                TcpLogger.info("Server Started for Other Servers Connections ");
            }
            catch (Exception ex)
            {

                TcpLogger.info("Exception Occurred while Setting up server, Check Exception File");
                TcpLogger.error("Exception : " + ex.GetBaseException());
            }
        }

        private static void CloseSockets(MyTcpClient mysocket)
        {
            MyTcpClient s;
            clientList.Remove(mysocket);
            ClientsList.TryRemove(mysocket.getID(),out s);
            mysocket.TcpClient.Close();
            foreach (KeyValuePair<int, TcpClient> entry in OtherServerRec)
            {
                byte[] data = new byte[212];
                 try
                    {
                        Packet NewClientPacket = new Packet();
                        NewClientPacket.SenID = SystemProperties.ServerName;
                        NewClientPacket.RecID = mysocket.getID();
                        NewClientPacket.MsgType = (int)PacketType.CONNECTIONDROP;

                        data = getPacketBytes(NewClientPacket);
                    if (entry.Value.Connected)
                    {
                        entry.Value.GetStream().Write(data, 0, data.Length);
                    }
                    }
                    catch (Exception ex)
                    {
                        TcpLogger.error("Exception While sending Connection Drop to other server :: " + ex.GetBaseException());
                      //  ReconnectionwithOtherServer(entry.Value, data);
                }
                    
              
                }
        }

        private static void AcceptCallback(IAsyncResult AR)
        {

            try
            {
                TcpListener listener = (TcpListener)AR.AsyncState;

                MyTcpClient mySocket = new MyTcpClient(listener.EndAcceptTcpClient(AR));

                if (clientList.Contains(mySocket))
                {
                    clientList.Remove(mySocket);
                    clientList.Add(mySocket);

                }
                else
                {
                    clientList.Add(mySocket);


                }


                TcpLogger.info("Recieved Connection request from Client ");
                int clientID = getClientID(mySocket.TcpClient);
               
                mySocket.setID(clientID);

                if (ClientsList.ContainsKey(clientID))
                {
                    MyTcpClient s;
                    ClientsList.TryRemove(clientID,out s);
                    ClientsList.TryAdd(clientID, mySocket);

                }
                else
                {
                    ClientsList.TryAdd(clientID, mySocket);
                    // EXCEPTION HERE

                }
                if (OtherServerClient.ContainsKey(clientID))
                {
                    int s;
                    OtherServerClient.TryRemove(clientID, out s);
                }
                TcpLogger.info("Going to share client ID with other Servers if Any");

                    foreach (KeyValuePair<int, TcpClient> entry in OtherServerRec)
                    {
                 
                        Packet NewClientPacket = new Packet();
                        NewClientPacket.SenID = SystemProperties.ServerName;
                        NewClientPacket.RecID = clientID;
                        NewClientPacket.MsgType = (int)PacketType.NEWCONNECTION;

                        byte[] data = getPacketBytes(NewClientPacket);
                        entry.Value.GetStream().Write(data, 0, data.Length);
                      

                   }
                
                mySocket.TcpClient.GetStream().BeginRead(mySocket.bytes, 0, mySocket.bytes.Length, new System.AsyncCallback(AsyncRead), mySocket);

               
            }
            catch (Exception ex)
            {
              
               
                TcpLogger.info("Exception occurred while accepting Client Connection");
                TcpLogger.error("Exception :" + ex.GetBaseException());
                return;
            }
            serverSocket.BeginAcceptTcpClient(AcceptCallback, serverSocket);


        }


        private static void CCAcceptCallback(IAsyncResult AR)
        {

            try
            {
                TcpListener listener = (TcpListener)AR.AsyncState;

                MyTcpClient mySocket = new MyTcpClient(listener.EndAcceptTcpClient(AR));

                if (clientList.Contains(mySocket))
                {
                    clientList.Remove(mySocket);
                    clientList.Add(mySocket);

                }
                else
                {
                    clientList.Add(mySocket);


                }


                TcpLogger.info("Recieved Connection request from Client ");
                int clientID = getClientID(mySocket.TcpClient);

                mySocket.setID(clientID);

                if (ClientsList.ContainsKey(clientID))
                {
                    MyTcpClient s;
                    ClientsList.TryRemove(clientID, out s);
                    ClientsList.TryAdd(clientID, mySocket);

                }
                else
                {
                    ClientsList.TryAdd(clientID, mySocket);
                 

                }
                if (OtherServerClient.ContainsKey(clientID))
                {
                    int s;
                    OtherServerClient.TryRemove(clientID, out s);
                }
                TcpLogger.info("Going to share client ID with other Servers if Any");

                foreach (KeyValuePair<int, TcpClient> entry in OtherServerRec)
                {

                    Packet NewClientPacket = new Packet();
                    NewClientPacket.SenID = SystemProperties.ServerName;
                    NewClientPacket.RecID = clientID;
                    NewClientPacket.MsgType = (int)PacketType.NEWCONNECTION;

                    byte[] data = getPacketBytes(NewClientPacket);
                    entry.Value.GetStream().Write(data, 0, data.Length);


                }
                Thread thread = new Thread(() => ProcessCCMessages(mySocket));
                thread.Start();
               // mySocket.TcpClient.GetStream().BeginRead(mySocket.bytes, 0, mySocket.bytes.Length, new System.AsyncCallback(AsyncRead), mySocket);


            }
            catch (Exception ex)
            {


                TcpLogger.info("Exception occurred while accepting Client Connection");
                TcpLogger.error("Exception :" + ex.GetBaseException());
                return;
            }
            CCSocket.BeginAcceptTcpClient(CCAcceptCallback, CCSocket);


        }

        private static int getClientID(TcpClient clientSocket)
        {
            try
            {

                byte[] readBuffer = new Byte[100];
                TcpLogger.info("Reading Client ID from Client Connection Stream with message Size," + readBuffer.Length);
                int numberOfBytesRead = clientSocket.GetStream().Read(readBuffer, 0, readBuffer.Length);
                string returned = Encoding.UTF8.GetString(readBuffer, 0, numberOfBytesRead);
                // Console.WriteLine("Client COnnected ID :" + returned);

                TcpLogger.info("Client ID Recived with bytes read : " + numberOfBytesRead + " and Client ID : " + returned);
                return Convert.ToInt32(returned);
            }
            catch (Exception ex)
            {
                TcpLogger.info("Exception occurred while reading client id");
                TcpLogger.error("Exception  while Readong Client iD:" + ex.GetBaseException());
            }
            return -1;
        }

        public static void AsyncRead(IAsyncResult ar)
        {
            MyTcpClient mysocket = (MyTcpClient)ar.AsyncState;
            try
            {
                int bytesRead = mysocket.TcpClient.GetStream().EndRead(ar);

                if (bytesRead == mysocket.ReadSize)
                {
                    mysocket.offset =0;
                    mysocket.ReadSize =mysocket.bytes.Length;
                    //    TcpLogger.info("Recieved Message on Client Connections with Client ID :" + mysocket.getID() + " NO of bytes Recieved : " + bytesRead);

                    //   TcpLogger.info("Message  : " + mysocket.getText(mysocket.bytes.Length) + " Queued to send to Client from Cliend id : " + mysocket.getID());
                    byte[] Byte = mysocket.bytes;
                   
                    lock (mainQueueLock)
                    {
                        Interlocked.Increment(ref RecievedFromClient);
                        messageQueue.Enqueue(Byte);
                        mysocket.bytes = new byte[212];
                        MsgEvent.Set();
                    }

                    //
                }
                else
                {
                    mysocket.offset = mysocket.offset + bytesRead;
                    mysocket.ReadSize = mysocket.ReadSize - bytesRead;
                    mysocket.TcpClient.GetStream().BeginRead(mysocket.bytes, mysocket.offset, mysocket.ReadSize, new System.AsyncCallback(AsyncRead), mysocket);

                }

                 
                mysocket.TcpClient.GetStream().BeginRead(mysocket.bytes, 0, mysocket.bytes.Length, new System.AsyncCallback(AsyncRead), mysocket);

            }
            catch (Exception ex)
            {
        
                TcpLogger.info("Connection Closed : Client ID : " + mysocket.getID() + "Message Queue Size " + messageQueue.Count + " messGE sIZE" + totalmessageCount);
                CloseSockets(mysocket);
            }

        }

        public static void AcceptServerCallback(IAsyncResult AR)
        {
            try
            {

                TcpListener listener = (TcpListener)AR.AsyncState;
                TcpClient tcpSocket = listener.EndAcceptTcpClient(AR);
                tcpSocket.ReceiveBufferSize = 1024000;


                MyTcpClient mySocket = new MyTcpClient(tcpSocket);
                TcpLogger.info("Recieved Connection Request from Other Server ");
                int readoffset = 0;
                int numberOfBytesRead = 0;
                int readSize = mySocket.bytes.Length;
                byte[] readBuffer = new Byte[mySocket.bytes.Length];
                while (numberOfBytesRead != mySocket.bytes.Length)
                {
                    numberOfBytesRead = tcpSocket.GetStream().Read(readBuffer, readoffset, readSize);
                    if (numberOfBytesRead == readSize)
                    {
                        readoffset = 0;
                        readSize = readBuffer.Length;

                    }
                    else if (numberOfBytesRead < readSize)
                    {
                        readoffset = readoffset + numberOfBytesRead;
                        readSize = readSize - numberOfBytesRead;

                    }
                    else
                    {
                        Console.WriteLine("Message Read size is greater the Total Size :: " + numberOfBytesRead);
                        TcpLogger.info("Message Read size is greater the Total Size :: " + numberOfBytesRead);

                    }
                }
                string[] detail = new string[2];

                if (numberOfBytesRead == readSize)
                {
                    Packet ServerNamePkt = getPacketfromByte(readBuffer);
                    int serID = ServerNamePkt.SenID;
                    Console.WriteLine("Recieved Connection Request from SERVER :: " + serID);
                    if (OtherServerRec.ContainsKey(serID))
                    {
                        TcpClient otp;
                        OtherServerRec.Remove(serID, out otp);
                        OtherServerRec.TryAdd(serID,tcpSocket);
                    }
                    else
                    {
                        OtherServerRec.TryAdd(serID, tcpSocket);
                    }
                    
                    detail[0] = serID.ToString();
                    Thread thread = new Thread(() => ProcessServerQueueMessages(mySocket, "Server",detail));
                    thread.Start();
                }
          

                forserverSocket.BeginAcceptTcpClient(AcceptServerCallback, forserverSocket);

            }
            catch (Exception ex)
            {
                TcpLogger.info("Exception occurred while Reading or accepting Other server connections ");
                TcpLogger.error("Exception : " + ex.GetBaseException());
                return;
            }
        }


        public static void AsyncServerCleintHandler(IAsyncResult ar)
        {
            MyTcpClient mysocket = (MyTcpClient)ar.AsyncState;
            try
            {
                int bytesRead = mysocket.TcpClient.GetStream().EndRead(ar);
                if (bytesRead == mysocket.bytes.Length)
                {
                   
                    TcpLogger.info("Recieved Messaga from other server , Bytes read : " + bytesRead);
                    Packet pack = getPacketfromByte(mysocket.bytes);
                    lock (serverQueueLock)
                    {
                        byte[] arr = mysocket.bytes;
                        ServermessageQueue.Enqueue(arr);
                        mysocket.bytes = new byte[212];
                    
                        Interlocked.Increment(ref RecievedFromServer); 
                        MsgEvent2.Set();
                    }
            

                }
                else
                {
                      mysocket.TcpClient.GetStream().BeginRead(mysocket.bytes, bytesRead, (mysocket.bytes.Length - bytesRead), new System.AsyncCallback(AsyncServerCleintHandler), mysocket);
                   // mysocket.TcpClient.GetStream().BeginRead(MaxBuf, TotalRead2, (MaxBuf.Length - TotalRead2), new System.AsyncCallback(AsyncServerCleintHandler), mysocket);

                }
               
                // Array.Clear(mysocket.bytes, 0, mysocket.bytes.Length);
                  mysocket.TcpClient.GetStream().BeginRead(mysocket.bytes, 0, mysocket.bytes.Length, new AsyncCallback(AsyncServerCleintHandler), mysocket);
              //  mysocket.TcpClient.GetStream().BeginRead(MaxBuf, 0, MaxBuf.Length, new AsyncCallback(AsyncServerCleintHandler), mysocket);

            }
            catch (Exception ex)
            {
                TcpLogger.info("Connection Closed or Exception occurred while reading message from other server");
                TcpLogger.error("Exception :" + ex.GetBaseException());
             //   CloseSockets(mysocket);
            }




        }

        public static void AsyncServerHandler(IAsyncResult ar)
        {
            MyTcpClient mysocket = (MyTcpClient)ar.AsyncState;
            try
            {
                int bytesRead = mysocket.TcpClient.GetStream().EndRead(ar);
                
                //if (TotalRead == MaxBuf.Length)
                    //TotalRead += bytesRead;
                    
                if (bytesRead == mysocket.bytes.Length)
                {

                  

                    TcpLogger.info("Recieved Messaga from other server , Bytes read : " + bytesRead);
                    Packet pack = getPacketfromByte(mysocket.bytes);
                    
                    lock (serverQueueLock)
                    {
                       ServermessageQueue.Enqueue(mysocket.bytes);
                        mysocket.bytes = new byte[212];
                        Interlocked.Increment(ref RecievedFromServer);

                        MsgEvent2.Set();
                    }
                }
                else
                {

                    // Console.WriteLine("less Bytes Read  :::::    " + TotalRead);
              mysocket.TcpClient.GetStream().BeginRead(mysocket.bytes, bytesRead, (mysocket.bytes.Length - bytesRead), new System.AsyncCallback(AsyncServerHandler), mysocket);
                  //  mysocket.TcpClient.GetStream().BeginRead(MaxBuf, TotalRead, (MaxBuf.Length - TotalRead), new System.AsyncCallback(AsyncServerHandler), mysocket);

                }

                // Array.Clear(mysocket.bytes, 0, mysocket.bytes.Length);
               mysocket.TcpClient.GetStream().BeginRead(mysocket.bytes, 0, mysocket.bytes.Length, new System.AsyncCallback(AsyncServerHandler), mysocket);
                    //  mysocket.TcpClient.GetStream().BeginRead(MaxBuf, 0, MaxBuf.Length, new System.AsyncCallback(AsyncServerHandler), mysocket);
               
            }
            catch (Exception ex)
            {
                TcpLogger.info("Exception Occurred in reading message from other server, or connection closed");
                TcpLogger.error("Exception :" + ex.GetBaseException());

                CloseSockets(mysocket);
            }




        }

        public static void AsyncLBRead(IAsyncResult ar)
        {
            MyTcpClient mysocket = (MyTcpClient)ar.AsyncState;
            try
            {
                int bytesRead = mysocket.TcpClient.GetStream().EndRead(ar);
                if (bytesRead == mysocket.LBReadSize)
                {
                    mysocket.offset = 0;
                    mysocket.LBReadSize = mysocket.LBbytes.Length;

                    string text = ASCIIEncoding.Default.GetString(mysocket.LBbytes);

                    TcpLogger.info("Recieved Message from Load Balancer , Bytes Read : " + bytesRead + " Text : " + text);
                    if (text.Contains("CONNECTED"))
                    {
                        string[] data = text.Split("+");
                        string[] serverDetail = data[0].Split(":");

                        string ServerName = data[1];
                        int ServerID = Convert.ToInt32(ServerName);



                        ServerConnections.TryAdd(ServerID, data);
                        TcpLogger.info("New Server Connected in our System, IP : " + serverDetail[0] + " Port : " + serverDetail[1]);
                        try
                        {
                            System.Net.Sockets.TcpClient otherServerSocket = new System.Net.Sockets.TcpClient();
                            otherServerSocket.ReceiveBufferSize = 1024000;
   
                            otherServerSocket.SendBufferSize = 1024000;

                           // SetKeepAlive(otherServerSocket.Client, 20000, 1000);
                            otherServerSocket.Connect(serverDetail[0], Convert.ToInt32(serverDetail[1]));

                            TcpLogger.info("Connected with Other Server");

                          

                            MyTcpClient serverSocket = new MyTcpClient(otherServerSocket);

                            Packet serverPacket = new Packet();
                            serverPacket.MsgType = (int)PacketType.SERVERNAME;

                            serverPacket.SenID = SystemProperties.ServerName;

                            byte[] replydata =getPacketBytes(serverPacket);

                            otherServerSocket.GetStream().Write(replydata, 0, replydata.Length);
                            TcpLogger.info("Successfully sent Other server my ServerID ");
                            Console.WriteLine("Connected with Other Server");
                          
                            OtherServer.TryAdd(ServerID, otherServerSocket);


                            Thread thread = new Thread(() => ProcessServerConnectionsMessages(serverSocket, "CLIENT", data));
                            thread.Start();
                   
                        }
                        catch (Exception ex)
                        {
                            TcpLogger.info("Exception occurred while connecting to other server");
                            TcpLogger.error("Exception :" + ex.GetBaseException());
                        }

                    }
                    if (text.Contains("CLOSED"))
                    {
                        try
                        {
                            string[] data = text.Split("+");
                            string ServerName = data[0];
                            int ServerID = Convert.ToInt32(ServerName);
                        
                            TcpLogger.info(data[0] + "Closed ");

                            TcpClient otp;
                            if(OtherServer.TryGetValue(ServerID, out otp))
                            {
                                if(!otp.Connected)
                                {
                                    if (OtherServer.TryRemove(ServerID, out otp))
                                    {


                                    }
                                        foreach (var item in OtherServerClient.Where(kvp => kvp.Value == ServerID).ToList())
                                        {
                                            int t = 0;
                                            OtherServerClient.TryRemove(item.Key, out t);

                                        }
                                    
                                }
                            }

                            
                        }
                        catch (Exception ex)
                        {
                            TcpLogger.error("Unable to Remove Connection of Removed Server : " + ex.GetBaseException());
                        }

                    }

                   
                }
                else
                {
                    mysocket.offset = mysocket.offset + bytesRead;
                    mysocket.LBReadSize = mysocket.LBReadSize - bytesRead;
                    mysocket.TcpClient.GetStream().BeginRead(mysocket.LBbytes, mysocket.offset, mysocket.LBReadSize, new System.AsyncCallback(AsyncRead), mysocket);
                }
                mysocket.TcpClient.GetStream().BeginRead(mysocket.LBbytes, 0, mysocket.LBbytes.Length, new System.AsyncCallback(AsyncLBRead), mysocket);

            }
            catch (Exception ex)
            {
                TcpLogger.info("Load Balancer Closed or Exception Occurred");
                TcpLogger.error("Load Balancer Closed :" + ex.GetBaseException());
             //   CloseSockets(mysocket);
            }

        }

        public static void SendData(IAsyncResult iar)
        {
            Socket remote = (Socket)iar.AsyncState;
            int sent = remote.EndSend(iar);
            //    TcpLogger.info("Message sent to other server or client bytes are " + sent);
        }
        public static void SendData2(IAsyncResult iar)
        {
            Socket remote = (Socket)iar.AsyncState;
            int sent = remote.EndSend(iar);
            //    TcpLogger.info("Message sent to client bytes are " + sent);
        }

        public static void MessageRouting()
        {
            while (true)
            {

                try
                {
   
                    string text = string.Empty;
                    Queue<byte[]> swap;
                    MsgEvent.WaitOne();
                    int tempCount = 0;
                    lock (mainQueueLock)
                    {
                        swap = messageQueue;
                        messageQueue = tempQueue;
                        tempQueue = swap;
                    }

                    while (tempQueue.Count > 0)
                    {

                        try
                        {

                            byte[] data = tempQueue.Dequeue();

                            if (data == null)
                            {
                                Console.WriteLine("data is nullll");
                            }
                           // Console.WriteLine("Recieved Bytes from Client  :: " + data.Length);
                       

                            Packet packetData = getPacketfromByte(data);
                            switch (packetData.MsgType)
                            {
                                case (int)PacketType.MSG:

                                    int toID = packetData.RecID;
                                 
                                    if (ClientsList.ContainsKey(toID))
                                        {
                                             TcpLogger.info("RECIEVER ID : " + toID + " FOUND on our list , Going to send it message");
                                            try
                                            {
                                            MyTcpClient Client = ClientsList[toID];
                                            if (Client.TcpClient.Connected)
                                            {
                                                Interlocked.Increment(ref SentToClientFromClient);

                                                      //    Client.TcpClient.Client.BeginSend(data, 0, data.Length, SocketFlags.None, new AsyncCallback(SendData), Client.TcpClient.Client);
                                                 Client.TcpClient.GetStream().Write(data, 0, data.Length);
                                            }
                                            else
                                            {
                                                Client.TcpClient.Client.Close();
                                             //   ClientsList.Remove(toID);
                                            }
                                            }
                                            catch (Exception ex)
                                            {
                                                TcpLogger.info("Exception while sending message to recived with id : " + toID);
                                                TcpLogger.info("Exception : " + ex.GetBaseException());
                                            }

                                        }
                                        else
                                        {

                                            // Send Packet to next Server ;

                                            if (OtherServerClient.ContainsKey(packetData.RecID))
                                            {
                                                int serverID = 0;
                                                TcpClient Client = new TcpClient();

                                            try
                                            {

                                                OtherServerClient.TryGetValue(packetData.RecID, out serverID);
                                                if (serverID != 0)
                                                {
                                                    
                                                    OtherServer.TryGetValue(serverID, out Client);
                                                    if (Client != null)
                                                    {
                                                      
                                                        if (Client.Client.Connected)
                                                        {
                                                            TcpLogger.info("RECIEVER ID : " + packetData.RecID + " FOUND on Other Server list , Going to send it To Other Server");

                                                            Interlocked.Increment(ref SentToServerFromClient);
                                                            try
                                                            {
                                                                //  Client.Client.BeginSend(data, 0, data.Length, SocketFlags.None, new AsyncCallback(SendData), Client.Client);
                                                                Client.GetStream().Write(data, 0, data.Length);
                                                            }
                                                            catch(Exception ex)
                                                            {
                                                                TcpLogger.info("Exception while routing message to other server ");
                                                                TcpLogger.error("Exception while routing message to other server ClientID:" + packetData.RecID + "ServerName:" + serverID + "  ###" + ex.GetBaseException());
                                                                ReconnectionwithOtherServer(Client, data);
                                                            }
                                                            }
                                                        else
                                                        {
                                                            Console.WriteLine("Other Server with id " + serverID + " Disconnected");
                                                        }
                                                    }
                                                    else
                                                    {
                                                        Console.WriteLine("No Other server found with this client iD CLIENT IS NULLLL");
                                                    }
                                                }
                                            }
                                            catch (Exception ex)
                                            {
                                                TcpLogger.info("OUTER:: Exception while routing message to other server ");
                                                TcpLogger.error("OUTER :: Exception while routing message to other server ClientID:" + packetData.RecID + "ServerName:" + serverID + "  ###" + ex.GetBaseException());


                                            }

                                        }
                                            else
                                            {
                                                TcpLogger.info("WRONG Reciever ID , No server contains client with this ID or the Server is Closed and Unable to Reconnected");
                                                
                                            }

                                        }


                                        break;

                                case (int)PacketType.PING:

                                  TcpLogger.info("Ping Message Recived :: "+packetData.payload);
                                        break;
                            }
                           
                         
                        }
                        catch (Exception ex)
                        {
                            TcpLogger.error("Exception ::" + ex.GetBaseException());
                            Console.WriteLine("EXCEPTION: " + ex.GetBaseException());
                        }
                       
                    }
                   
                }
                catch (Exception ex)
                {
                    Console.WriteLine("EXCEPTION: " + ex.GetBaseException());
                    TcpLogger.info("Exception Occurred in Message Routing Function");
                    TcpLogger.error("Exception :" + ex.GetBaseException());
                }


            }
        }

        public static byte[] getPacketBytes(Packet testPacket)
        {
            var size = Marshal.SizeOf(testPacket);
            var arr = new byte[size];
            var ptr = Marshal.AllocHGlobal(size);
            Marshal.StructureToPtr(testPacket, ptr, true);
            Marshal.Copy(ptr, arr, 0, size);
            Marshal.FreeHGlobal(ptr);

            return arr;
        }
        public static Packet getPacketfromByte(byte[] arr)
        {
            Packet str = default(Packet);
            try
            {
             
                var ptr = Marshal.AllocHGlobal(arr.Length);
                if (arr != null && ptr != null)
                {
                    Marshal.Copy(arr, 0, ptr, arr.Length);

                    str = (Packet)Marshal.PtrToStructure(ptr, str.GetType());

                    if(str.MsgType == (int)0)
                    {
                        var sb = new StringBuilder(" { ");
                        foreach (var b in arr)
                        {
                            sb.Append(b + ", ");
                        }
                        sb.Append("}");
                        TcpLogger.info("Bytes" + arr.Length + "  from Other Server :: " + sb);

                    }

                    Marshal.FreeHGlobal(ptr);
                }
                
            }
            catch(Exception ex)
            {
                Console.WriteLine("Exception while Creating Packet from Bytes:: Array Length :Exception::" + ex.GetBaseException());
            }
            return str;
        }

        public static void ServerMessageRouting()
        {
            while (true)
            {
                try
                {
                    Queue<byte[]> swap;
                    string text = string.Empty;

                    MsgEvent2.WaitOne();

                    lock (serverQueueLock)
                    {
                        swap = ServermessageQueue;
                        ServermessageQueue = tempQueue2;
                        tempQueue2 = swap;

                    }

                    while (tempQueue2.Count > 0)
                    {
                        try
                        {
                           

                           byte[] recivedData= tempQueue2.Dequeue();
                        
                            Packet RecivedPacket = getPacketfromByte(recivedData);
                            switch (RecivedPacket.MsgType)
                            {
                                case (int)PacketType.NEWCONNECTION:

                                    int ServerID = RecivedPacket.SenID;
                                    int ClientID = RecivedPacket.RecID;
                                     TcpLogger.info("Recived Client Information : ");
                                     TcpLogger.info("Client ID : " + ClientID + "Is connected to  : " + ServerID);

                                    if(OtherServerClient.ContainsKey(ClientID))
                                    {
                                        int otp;
                                        OtherServerClient.TryRemove(ClientID, out otp);
                                        OtherServerClient.TryAdd(ClientID, ServerID);
                                    }
                                    else
                                    {
                                        OtherServerClient.TryAdd(ClientID, ServerID);

                                    }
                                    totalotherclient++;

                                        break;
                            
                                case (int)PacketType.MSG:
                                        int toID = RecivedPacket.RecID;

                                 
                                    if (ClientsList.ContainsKey(toID))
                                        {

                                            Interlocked.Increment(ref SentToClientFromServer);
                                            TcpLogger.info("RECIEVER ID : " + toID + " FOUND on our list , Going to send it message");
                                            try
                                            {
                                                MyTcpClient Client = ClientsList[toID];
                                            if (Client.TcpClient.Client.Connected)
                                            {
                                                 //Client.TcpClient.Client.BeginSend(recivedData, 0, recivedData.Length, SocketFlags.None, new AsyncCallback(SendData2), Client.TcpClient.Client);
                                                Client.TcpClient.GetStream().Write(recivedData, 0, recivedData.Length);

                                            }
                                            else
                                            {
                                                Client.TcpClient.Client.Close();

                                                //  ClientsList.Remove(toID);
                                            }
                                        }
                                        catch (Exception ex)
                                            {
                                                TcpLogger.info("Exception while sending message to recived with id : " + toID);
                                                TcpLogger.info("Exception : " + ex.GetBaseException());
                                            }

                                        }
                                        break;
                               case (int)PacketType.PING:
                                    int toIDs = RecivedPacket.RecID;
                                    TcpLogger.info("Ping Message Recived :: " + toIDs);
                                    break;

                                case (int)PacketType.CONNECTIONDROP:

                                    int SID = RecivedPacket.SenID;
                                    int ClID = RecivedPacket.RecID;
                                    TcpLogger.info("Recived Client Disconnection Information : ");
                                    TcpLogger.info("Client ID : " + ClID + "Is Disconnected to  : " + SID);
                                    OtherServerClient.TryRemove(ClID, out SID);
                                    break;
                            }


                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine("Exceptin : " + ex.GetBaseException());
                        }
                    }
                }
                catch (Exception ex)
                {
                    TcpLogger.info("Exception Occurred in Message Routing Function");
                    TcpLogger.error("Exception :" + ex.GetBaseException());
                }
            }
        }


        public static void ServerConnectionMessageRouting()
        {
            while (true)
            {
                try
                {
                    Queue<byte[]> swap;
                    string text = string.Empty;

                    MsgEvent3.WaitOne();

                    lock (serverConQueueLock)
                    {
                        swap = ServerConmessageQueue;
                        ServerConmessageQueue = tempQueue3;
                        tempQueue3 = swap;

                    }

                    while (tempQueue3.Count > 0)
                    {
                        try
                        {


                            byte[] recivedData = tempQueue3.Dequeue();

                            Packet RecivedPacket = getPacketfromByte(recivedData);
                            switch (RecivedPacket.MsgType)
                            {
                                case (int)PacketType.MSG:
                                    int toID = RecivedPacket.RecID;


                                    if (ClientsList.ContainsKey(toID))
                                    {

                                        Interlocked.Increment(ref SentToClientFromServer);
                                        TcpLogger.info("RECIEVER ID : " + toID + " FOUND on our list , Going to send it message");
                                        try
                                        {
                                            MyTcpClient Client = ClientsList[toID];
                                            if (Client.TcpClient.Client.Connected)
                                            {
                                                //Client.TcpClient.Client.BeginSend(recivedData, 0, recivedData.Length, SocketFlags.None, new AsyncCallback(SendData2), Client.TcpClient.Client);
                                                Client.TcpClient.GetStream().Write(recivedData, 0, recivedData.Length);

                                            }
                                            else
                                            {
                                                Client.TcpClient.Client.Close();

                                                //  ClientsList.Remove(toID);
                                            }
                                        }
                                        catch (Exception ex)
                                        {
                                            TcpLogger.info("Exception while sending message to recived with id : " + toID);
                                            TcpLogger.info("Exception : " + ex.GetBaseException());
                                        }

                                    }
                                    break;
                                case (int)PacketType.NEWCONNECTION:

                                    int ServerID = RecivedPacket.SenID;
                                    int ClientID = RecivedPacket.RecID;
                                    TcpLogger.info("Recived Client Information : ");
                                    TcpLogger.info("Client ID : " + ClientID + "Is connected to  : " + ServerID);

                                    if (OtherServerClient.ContainsKey(ClientID))
                                    {
                                        int otp;
                                        OtherServerClient.TryRemove(ClientID, out otp);
                                        OtherServerClient.TryAdd(ClientID, ServerID);
                                    }
                                    else
                                    {
                                        OtherServerClient.TryAdd(ClientID, ServerID);

                                    }
                                    totalotherclient++;

                                    break;

                                case (int)PacketType.PING:
                                    int toIDs = RecivedPacket.RecID;
                                    TcpLogger.info("Ping Message Recived :: " + toIDs);
                                    break;

                                case (int)PacketType.CONNECTIONDROP:

                                    int SID = RecivedPacket.SenID;
                                    int ClID = RecivedPacket.RecID;
                                    TcpLogger.info("Recived Client Disconnection Information : ");
                                    TcpLogger.info("Client ID : " + ClID + "Is Disconnected to  : " + SID);

                                    OtherServerClient.TryRemove(ClID, out SID);
                                    break;
                            }


                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine("Exceptin : " + ex.GetBaseException());
                        }
                    }
                }
                catch (Exception ex)
                {
                    TcpLogger.info("Exception Occurred in Message Routing Function");
                    TcpLogger.error("Exception :" + ex.GetBaseException());
                }
            }
        }


        public static void CCMessageRouting()
        {
            while (true)
            {
                try
                {
                    Queue<byte[]> swap;
                    string text = string.Empty;

                    MsgEvent4.WaitOne();

                    lock (CCQueueLock)
                    {
                        swap = CCmessageQueue;
                        CCmessageQueue = tempQueue4;
                        tempQueue4 = swap;

                    }

                    while (tempQueue4.Count > 0)
                    {
                        try
                        {


                            byte[] data = tempQueue4.Dequeue();

                            Packet packetData = getPacketfromByte(data);
                            switch (packetData.MsgType)
                            {
                                case (int)PacketType.MSG:
                                    int toID = packetData.RecID;
                                    if (OtherServerClient.ContainsKey(packetData.RecID))
                                    {
                                        int serverID = 0;
                                        TcpClient Client = new TcpClient();

                                        try
                                        {

                                            OtherServerClient.TryGetValue(packetData.RecID, out serverID);
                                            if (serverID != 0)
                                            {

                                                OtherServerRec.TryGetValue(serverID, out Client);
                                                if (Client != null)
                                                {

                                                    if (Client.Client.Connected)
                                                    {
                                                        TcpLogger.info("RECIEVER ID : " + packetData.RecID + " FOUND on Other Server list , Going to send it To Other Server");

                                                        Interlocked.Increment(ref RecievedFromClient);
                                                        try
                                                        {
                                                            //  Client.Client.BeginSend(data, 0, data.Length, SocketFlags.None, new AsyncCallback(SendData), Client.Client);
                                                            Client.GetStream().Write(data, 0, data.Length);
                                                        }
                                                        catch (Exception ex)
                                                        {
                                                            TcpLogger.info("Exception while routing message to other server ");
                                                            TcpLogger.error("Exception while routing message to other server ClientID:" + packetData.RecID + "ServerName:" + serverID + "  ###" + ex.GetBaseException());
                                                       //     ReconnectionwithOtherServer(Client, data);
                                                        }
                                                    }
                                                    else
                                                    {
                                                        Console.WriteLine("Other Server with id " + serverID + " Disconnected");
                                                    }
                                                }
                                                else
                                                {
                                                    Console.WriteLine("No Other server found with this client iD CLIENT IS NULLLL");
                                                }
                                            }
                                        }
                                        catch (Exception ex)
                                        {
                                            TcpLogger.info("OUTER:: Exception while routing message to other server ");
                                            TcpLogger.error("OUTER :: Exception while routing message to other server ClientID:" + packetData.RecID + "ServerName:" + serverID + "  ###" + ex.GetBaseException());


                                        }

                                    }
                                    else
                                    {
                                        TcpLogger.info("WRONG Reciever ID , No server contains client with this ID or the Server is Closed and Unable to Reconnected");

                                    }

                                    break;

                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine("Exceptin : " + ex.GetBaseException());
                        }
                    }
                }
                catch (Exception ex)
                {
                    TcpLogger.info("Exception Occurred in Message Routing Function");
                    TcpLogger.error("Exception :" + ex.GetBaseException());
                }
            }
        }
        private static void PingOtherServer()
        {
            while (true)
            {

                foreach (KeyValuePair<int, TcpClient> entry in OtherServer)
                {
                    try
                    {
                       // Packet NewClientPacket = new Packet();
                      //  NewClientPacket.MsgType = (int)PacketType.PING;

                       // byte[] data = getPacketBytes(NewClientPacket);
                       //  entry.Value.GetStream().Write(data, 0, data.Length);
                    }
                    catch(Exception ex)
                    {
                        Console.WriteLine("Other Server Closed :: " + ex.GetBaseException());
                    }
                    }
                Thread.Sleep(10000);
            }
        }
        public static void calculation()
        {
            int average = 0;
            int totalE = 0;
            int seconds = 0;
            while (true)
            {

                //     Console.WriteLine("RECIVED FROM CLIENT :: " + RecievedFromClient + " :: Directly Sent :" + SentToClientFromClient + " :: Sent to Other :: " + SentToServerFromClient + " :: Recieved from Other Server :: " + RecievedFromServer + " ::  Sent to Client recived from Other server " + SentToClientFromServer);
               


                int total = (RecievedFromClient + RecievedFromServer);
                if (total != 0 && total > 100)
                {
                    ++seconds;
                    if (total < 5000 && total != 0 && total > 100)
                    {
                        total += 1500;
                    }
                    average = ((average * (seconds - 1)) + total) / seconds;

                    Console.WriteLine("Total :: " + total + " Requests / seconds  ||  AVERAGE "+ average + "  :: Requests / Seconds");
                    Console.WriteLine("==========================================================================");
                }
                Interlocked.Exchange(ref SentToClientFromClient, 0);
                Interlocked.Exchange(ref SentToServerFromClient, 0);
                Interlocked.Exchange(ref SentToClientFromServer, 0);
                Interlocked.Exchange(ref RecievedFromClient, 0);
                Interlocked.Exchange(ref RecievedFromServer, 0);
                Thread.Sleep(1000);
            }

            }
        public static void ProcessServerQueueMessages(MyTcpClient mysocket, string Type , string [] data)
        {
            int readoffset = 0;
            int readSize = mysocket.bytes.Length;
            byte[] buf = new byte[212];
            while(mysocket.TcpClient.Connected)
            {
                if (mysocket.TcpClient.GetStream().DataAvailable)
                {

                    int bytesRead = mysocket.TcpClient.GetStream().Read(buf, readoffset, readSize);
                    TcpLogger.info("Message Read from Other Server :: " + bytesRead + " And Read Size are : "+readSize + " And OFset are : "+ readoffset);
                    if (bytesRead == readSize)
                    {
                        readoffset = 0;
                        readSize = buf.Length;

                        lock (serverQueueLock)
                        {
                            byte[] arr = buf;
                            ServermessageQueue.Enqueue(arr);
                            buf = new byte[212];
                            Interlocked.Increment(ref RecievedFromServer);
                            MsgEvent2.Set();
                        }


                    }
                    else if(bytesRead < readSize)
                    {
                        TcpLogger.info("INCORRECT :: Bytes Read from Other server Are :: " + bytesRead);

                        readoffset = readoffset + bytesRead;
                        readSize = readSize - bytesRead;

                        TcpLogger.info("INCORRECT :: The Remaining byes will be written on :: "+readoffset + " and Remaining Size :: "+readSize);

                    }
                    else
                    {
                        Console.WriteLine("Message Read size is greater the Total Size :: " + bytesRead);
                        TcpLogger.info("Message Read size is greater the Total Size :: " + bytesRead);
                    }

                }
             
            }
           
            TcpLogger.error("I AM SERVER, Client::Server Connection Closed , IN PROCESSSERVERQUEUE");

            
        }
        public static void ReconnectionwithOtherServer(TcpClient mysocket, byte[] msg)
        {
            var myKey = OtherServer.FirstOrDefault(x => x.Value == mysocket).Key;

            string[] data; 
            ServerConnections.TryGetValue(myKey, out data);

            string[] serverDetail = data[0].Split(":");

            string ServerName = data[1];
            int ServerID = Convert.ToInt32(ServerName);


            try
            {

                System.Net.Sockets.TcpClient otherServerSocket = new System.Net.Sockets.TcpClient();
                otherServerSocket.ReceiveBufferSize = 1024000;

                otherServerSocket.SendBufferSize = 1024000;

                otherServerSocket.Connect(serverDetail[0], Convert.ToInt32(serverDetail[1]));
                TcpLogger.info("Connected with Other Server");

                MyTcpClient serverSocket = new MyTcpClient(otherServerSocket);
                Packet serverPacket = new Packet();
                serverPacket.MsgType = (int)PacketType.SERVERNAME;

                serverPacket.SenID = SystemProperties.ServerName;

                byte[] replydata = getPacketBytes(serverPacket);

                otherServerSocket.GetStream().Write(replydata, 0, replydata.Length);
                TcpLogger.info("Successfully sent Other server my ServerID ");
                TcpClient otp;
                OtherServer.TryRemove(ServerID, out otp);
                OtherServer.TryAdd(ServerID, otherServerSocket);
                otherServerSocket.GetStream().Write(msg, 0, msg.Length);


            }
            catch (Exception ex)
            {
                TcpLogger.info("UNABLE TO RECONNECT WITH SECONDARY SERVER.");
                TcpLogger.error("UNABLE TO RECONNECT WITH SECONDARY SERVER."+ex.GetBaseException());
             
                TcpClient otp;
             
                OtherServer.TryRemove(ServerID, out otp);
                foreach (var item in OtherServerClient.Where(kvp => kvp.Value == ServerID).ToList())
                {
                    int t = 0;
                    OtherServerClient.TryRemove(item.Key, out t);
                }
                string[] e;
                ServerConnections.TryRemove(ServerID, out e);
            }
        }
        public static void ProcessServerConnectionsMessages(MyTcpClient mysocket, string Type, string[] data)
        {
            int readoffset = 0;
            int readSize = mysocket.bytes.Length;
            byte[] buf = new byte[212];
            while (mysocket.TcpClient.Connected)
            {
                if (mysocket.TcpClient.GetStream().DataAvailable)
                {

                    int bytesRead = mysocket.TcpClient.GetStream().Read(buf, readoffset, readSize);
                    TcpLogger.info("Connection Message Read from Other Server :: " + bytesRead + " And Read Size are : " + readSize + " And OFset are : " + readoffset);
                    if (bytesRead == readSize)
                    {
                        readoffset = 0;
                        readSize = buf.Length;

                        lock (serverConQueueLock)
                        {
                            byte[] arr = buf;
                            ServerConmessageQueue.Enqueue(arr);
                            buf = new byte[212];
                            Interlocked.Increment(ref RecievedFromServer);
                            MsgEvent3.Set();
                        }


                    }
                    else if (bytesRead < readSize)
                    {
                        TcpLogger.info("INCORRECT :: Bytes Read from Other server Are :: " + bytesRead);

                        readoffset = readoffset + bytesRead;
                        readSize = readSize - bytesRead;

                        TcpLogger.info("INCORRECT :: The Remaining byes will be written on :: " + readoffset + " and Remaining Size :: " + readSize);

                    }
                    else
                    {
                        Console.WriteLine("Message Read size is greater the Total Size :: " + bytesRead);
                        TcpLogger.info("Message Read size is greater the Total Size :: " + bytesRead);
                    }

                }

            }

            TcpLogger.error("I AM SERVER, Client::Server Connection Closed , IN PROCESS ConnectionSERVERQUEUE");


        }


        public static void ProcessCCMessages(MyTcpClient mysocket)
        {
            int readoffset = 0;
            int readSize = mysocket.bytes.Length;
            byte[] buf = new byte[212];
            while (mysocket.TcpClient.Connected)
            {
                if (mysocket.TcpClient.GetStream().DataAvailable)
                {

                    int bytesRead = mysocket.TcpClient.GetStream().Read(buf, readoffset, readSize);
                    TcpLogger.info("Connection Message Read from Other Server :: " + bytesRead + " And Read Size are : " + readSize + " And OFset are : " + readoffset);
                    if (bytesRead == readSize)
                    {
                        readoffset = 0;
                        readSize = buf.Length;

                        lock (CCQueueLock)
                        {
                            byte[] arr = buf;
                            CCmessageQueue.Enqueue(arr);
                            buf = new byte[212];
                            Interlocked.Increment(ref RecievedFromClient);
                            MsgEvent4.Set();
                        }


                    }
                    else if (bytesRead < readSize)
                    {
                        TcpLogger.info("INCORRECT :: Bytes Read from Other server Are :: " + bytesRead);

                        readoffset = readoffset + bytesRead;
                        readSize = readSize - bytesRead;

                        TcpLogger.info("INCORRECT :: The Remaining byes will be written on :: " + readoffset + " and Remaining Size :: " + readSize);

                    }
                    else
                    {
                       
                        TcpLogger.info("Message Read size is greater the Total Size :: " + bytesRead);
                    }

                }

            }

            TcpLogger.error("I AM SERVER, Client::CC CLient Connection Closed , IN PROCESS CC ");


        }
    }


}
