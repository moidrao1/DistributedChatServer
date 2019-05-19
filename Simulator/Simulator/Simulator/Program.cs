using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Simulator
{
    class Program
    {
         

        private static int threadcount = 0;
        private static int recMsg = 0;
        private static int senMsg = 0;
        private static int SleepTime = 250;
        private static int senMsgCheck = 2000;
        private static  List<MyTcpClient> clientList = new List<MyTcpClient>();
        private static ConcurrentDictionary<int, MyTcpClient> ClientsList = new ConcurrentDictionary<int, MyTcpClient>();
        private static  List<int> ClosedConnectionList = new List<int>();
        private static Object mainQueueObj = new object();
        private static Object recQueueObj = new object();
        private static Object ClosedLock = new object();
        private static Object ClientListLock = new object();
        private static byte[] MainBuffer = new byte[61800];
        private static Queue<byte[]> messageQueue = new Queue<byte[]>();
        private static Queue<MyTcpClient> ReconnectionQueue = new Queue<MyTcpClient> ();
        private static Queue<MyTcpClient> ReconnectionQueueTemp = new Queue<MyTcpClient> ();
        private static Queue<byte[]> tempQueue = new Queue<byte[]>();
        private static AutoResetEvent MsgEvent = new AutoResetEvent(false);
        private static AutoResetEvent ConnectionEvent = new AutoResetEvent(false);
        private static AutoResetEvent processingEvent = new AutoResetEvent(true);
        private static Dictionary<string, string> properties = new Dictionary<string, string>();
        private static byte[] ClientEnqBytes;
        static void Main(string[] args)
        {
            Random a =new Random();
           
            LoadProperties();
       
            int i = 0;
          
            while (i < SystemProperties.TotalClients)
            {
                try
                {
                    string[] serverInfo=getConnectionString();

                    if (serverInfo[0].ToString().Contains("NO SERVER Available"))
                    {
                        Console.WriteLine("No Server Available for this");
                    }
                    else
                    {
                      
                        // Convert.ToInt32(serverInfo[1])
                        System.Net.Sockets.TcpClient clientSocket = new System.Net.Sockets.TcpClient();

                        clientSocket.ReceiveBufferSize = 1024000;


                        // Set the send buffer size to 8k.
                        clientSocket.SendBufferSize = 1024000;
                        clientSocket.Connect(serverInfo[0], Convert.ToInt32(serverInfo[1]));
                    
                        MyTcpClient newClient = new MyTcpClient(clientSocket);


                        clientList.Add(newClient);
                        ClientsList.TryAdd(newClient.getID(), newClient);


                        byte[] data = new Byte[100];
                        string text = newClient.getID().ToString();
                        var buffer = Encoding.UTF8.GetBytes(text);
                        Buffer.BlockCopy(buffer, 0, data, 0, buffer.Length);
                        newClient.TcpClient.GetStream().Write(data, 0, data.Length);
                     
                    clientSocket.GetStream().BeginRead(newClient.bytes, 0, newClient.bytes.Length, new System.AsyncCallback(AsyncRead), newClient);
                  
                    }
                    i++;

                    Thread.Sleep(10);
                }
                catch (Exception ex)
                {
                    threadcount--;
                    Console.WriteLine("Closing Application"+ex.GetBaseException());
               
                    return;
                 }

            }
             SetupLogger();
            SetupPingService();
            Console.WriteLine("Press Enter to send messages ");
            Console.ReadKey();

            SetupBroker();

            while (true)

            {
                Console.WriteLine("Input Sleep Time for increase or decrese Speed and Q to quit");
                string x = Console.ReadLine();
                try
                {
                    switch (x.ToLower())
                    {
                        case "q":
                            return;
                            break;
                        default:

                            int value = Convert.ToInt32(x);

                            SleepTime = value;
                            break;
                    }
                }
                catch (Exception ex)
                {

                }

            }
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
                SystemProperties.logfilePath = properties["logFilePath"];
                SystemProperties.ClientStartRange = Convert.ToInt32(properties["ClientStartRange"]);
                SystemProperties.SleepTime = Convert.ToInt32(properties["SleepTime"]);
                SystemProperties.TotalServers = Convert.ToInt32(properties["TotalServers"]);
                SystemProperties.LBIP = properties["LBIP"];
                SystemProperties.LBPort = Convert.ToInt32(properties["LBPort"]);
                SystemProperties.senderID = Convert.ToInt32(properties["senderID"]);
                SystemProperties.TotalClients = Convert.ToInt32(properties["TotalClients"]);


            }
            catch (Exception ex)
            {

                Console.WriteLine("Exception while Loading Properties " + ex.GetBaseException());
            }
        }
        private static String[] getConnectionString()
        {
            System.Net.Sockets.TcpClient clientSocket = new System.Net.Sockets.TcpClient();

            clientSocket.Connect(SystemProperties.LBIP, SystemProperties.LBPort);
            
            byte[] readBuffer = new Byte[100];
            try
            {
                int numberOfBytesRead = clientSocket.GetStream().Read(readBuffer, 0, readBuffer.Length);
           
            string returned = Encoding.UTF8.GetString(readBuffer, 0, numberOfBytesRead);

            clientSocket.Client.Close();
            if(returned.Contains("NO SERVER Available"))
            {
                string[] dataa = new string[1];
                dataa[0] = "NO SERVER Available";
                return dataa;
            }
            else
            {
                string[] data = returned.Split(":");

                return data;
            }
            }
            catch (IOException ex)
            {
                string[] dataa = new string[1];
                dataa[0] = "NO SERVER Available";
                return dataa;
            }
            catch (Exception ex)
            {
                string[] dataa = new string[1];
                dataa[0] = "NO SERVER Available";
                return dataa;
            }
        }
        private static void SetupLogger()
        {
            Thread loggerThread = new Thread(MessageRouting);
        //    loggerThread.Start();
            Thread logger1Thread = new Thread(ProcessReconnection);
            logger1Thread.Start();
            Thread logger2Thread = new Thread(calculation);
            //logger2Thread.Start();
        }
        private static void SetupBroker()
        {
            Thread loggerThread = new Thread(DoProcessing);
            loggerThread.Start();

        }

        private static void SetupPingService()
        {

            Thread PingThread = new Thread(PingConnections);
          //  PingThread.Start();
        }
        private static void DoProcessing()
        {
           
             try
            {
               
          
               int counter = 0;
                Random rnd = new Random();
                int xs = SystemProperties.ClientStartRange + 1;
                while (true)
                {
                    
                    int xp =0;
                    int kk = 0;

                    int snd = 0;
                    
                    Stopwatch s = new Stopwatch();
                    s.Start();

                   while (kk < SystemProperties.SleepTime - SystemProperties.TotalServers)
                    {
                     
                        int inner = 0;
                         try
                            {
                                ++inner;
                                Packet mypacket = new Packet();
                                if (SystemProperties.senderID != 0)
                                {
                                    mypacket.RecID = SystemProperties.senderID;
                                }
                                else
                                {
                                int RecieverID = rnd.Next(SystemProperties.ClientStartRange+1,(SystemProperties.ClientStartRange + SystemProperties.TotalClients));
                            //    mypacket.RecID = xp + inner;
                                mypacket.RecID = RecieverID;

                                    
                                    if(inner == SystemProperties.TotalServers)
                                    {
                                    inner = 0;
                                    }
                                }

                            mypacket.payload = "HELLO THIS IS MY BROTHER TESTING YoUR TESTICALS |";
                          

                                mypacket.SenID = xs;
                                mypacket.MsgType = (int)PacketType.MSG;
                                byte[] mesg = getPacketBytes(mypacket);

                                  MyTcpClient sender;
                                  MyTcpClient reciever;
                            lock (ClientListLock)
                            {
                                ClientsList.TryGetValue(xs, out sender);
                                ClientsList.TryGetValue(mypacket.RecID, out reciever);
                            }
                            if ((sender != null && reciever != null) )
                                {
                                try
                                {
                                    lock (recQueueObj) { 
                                    if (sender.flag == true && reciever.flag == true)
                                    {
                                        sender.TcpClient.GetStream().Write(mesg, 0, mesg.Length);
                                    }
                                    }
                                }
                                catch (Exception ex)
                                {
                                       
                                          
                                            lock(recQueueObj)
                                            {
                                            sender.flag = false;
                                    }


                                }
                            }
                              counter++;
                                kk++;
                                xp++;
                                xs++;
                                snd++;

                            }
                            catch (Exception ex)
                            {
                              
                            }
                            if (counter == SystemProperties.TotalClients - 3)
                            {
                                counter = 0;
                          
                                xs = SystemProperties.ClientStartRange + 1;
                        }
                            if (xp == 5000)
                            {
                            xp = 0;
                                Thread.Sleep(800);
                            }

                     }
                        

                    Thread.Sleep(SleepTime);
                }
               




            }
            catch (Exception ex)
            {
                Console.WriteLine("CLIENT: Exception while DO processing" + ex);

            }

        }
        public static void AsyncRead(IAsyncResult ar)
        {
           MyTcpClient mysocket = (MyTcpClient)ar.AsyncState;
            try
            {

                int bytesRead = mysocket.TcpClient.GetStream().EndRead(ar);
                if (bytesRead == mysocket.bytes.Length)
                {

                    mysocket.offset = 0;
                    mysocket.ReadSize = mysocket.bytes.Length;
                  
                    Array.Clear(mysocket.bytes, 0, mysocket.bytes.Length);
                    
                    mysocket.TcpClient.GetStream().BeginRead(mysocket.bytes, 0, mysocket.bytes.Length, new System.AsyncCallback(AsyncRead), mysocket);
                }
                else
                {
                    mysocket.offset = mysocket.offset + bytesRead;
                    mysocket.ReadSize = mysocket.ReadSize - bytesRead;

                    mysocket.TcpClient.GetStream().BeginRead(mysocket.bytes, mysocket.offset, mysocket.ReadSize, new System.AsyncCallback(AsyncRead), mysocket);

                }
     
            }
            catch (Exception ex)
            {
             
                           
                            lock (recQueueObj)
                            {
                              mysocket.flag = false;
                            }
                             return;
            }

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
                    lock (mainQueueObj)
                    {
                        swap = messageQueue;
                        messageQueue = tempQueue;
                        tempQueue = swap;

                    }
                    while (tempQueue.Count > 0)
                    {
                      
                        byte [] message = tempQueue.Dequeue();
                        
                       Packet Recvied = getPacketfromByte(message);

                      Console.WriteLine("Recieved Message  ::  " + Recvied.payload);
                    }
                        
                }
                    catch (Exception ex)
                    {

                        Console.WriteLine("Exception while Reading Message " + ex.GetBaseException() + "::");
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
            var ptr = Marshal.AllocHGlobal(arr.Length);

            Marshal.Copy(arr, 0, ptr, arr.Length);

            str = (Packet)Marshal.PtrToStructure(ptr, str.GetType());
            Marshal.FreeHGlobal(ptr);

            return str;
        }

        public static void ProcessReconnection()
        {
         //   Console.WriteLine("Conenctions Established");
            while (true)
            {
                
                try
                {
                    foreach (KeyValuePair<int, MyTcpClient> entry in ClientsList)
                    {
                        bool flag = true;

                        lock (recQueueObj)
                        {
                         flag = entry.Value.getFlag();
                        }
                        if(!flag)
                        {
                            Console.WriteLine("Client Disconnected");
                            ReconnectConnection(entry.Value);
                        }
                    }

                    Thread.Sleep(5000);

                }
                catch (Exception ex)
                {

                    Console.WriteLine("Something Wrong");
                    return;
                  //  Console.WriteLine("Exception while Reading Message " + ex.GetBaseException() + "::");
                }



            }
        }



        public static void ReconnectConnection(MyTcpClient socket)
        {

            
            string[] serverInfo = getConnectionString();

            if (serverInfo[0].ToString().Contains("NO SERVER Available"))
            {
                Console.WriteLine("No Server Available for this");
            }
            else
            {


            
                System.Net.Sockets.TcpClient clientSocket = new System.Net.Sockets.TcpClient();

                clientSocket.ReceiveBufferSize = 1024000;


                // Set the send buffer size to 8k.
                clientSocket.SendBufferSize = 1024000;
                clientSocket.Connect(serverInfo[0], Convert.ToInt32(serverInfo[1]));
               
             
                socket.setClient(clientSocket);
                byte[] data = new Byte[100];
                string text = socket.getID().ToString();

                var buffer = Encoding.UTF8.GetBytes(text);
                Buffer.BlockCopy(buffer, 0, data, 0, buffer.Length);
                clientSocket.GetStream().Write(data, 0, data.Length);

                lock (recQueueObj)
                {
                    socket.flag = true;
                }
                lock (ClientListLock)
                {
                   
                    if (ClientsList.ContainsKey(socket.getID()))
                    {
                        MyTcpClient otp;
                        ClientsList.Remove(socket.getID(), out otp);
                        ClientsList.TryAdd(socket.getID(), socket);
                       
                    }
                    else
                    {
                        ClientsList.TryAdd(socket.getID(), socket);
                    }
                  
                }
                Console.WriteLine("<::>" + socket.getID());
                clientSocket.GetStream().BeginRead(socket.bytes, 0, socket.bytes.Length, new System.AsyncCallback(AsyncRead), socket);
               

            }

        }
        public static void PingConnections()
        {
            while (true)
            {      for (int i = 0; i < clientList.Count; i++)
                {
                    try
                    {
                        Packet mypacket = new Packet();
                        mypacket.MsgType = (int)PacketType.PING;
                        byte[] mesg = getPacketBytes(mypacket);
                       
                        clientList[i].TcpClient.GetStream().Write(mesg, 0, mesg.Length);
                    }
                    catch(Exception ex)
                    {

                        clientList[i].flag = false;
                      //lock(ClosedLock)
                      //  {
                      //      if (!ClosedConnectionList.Contains(clientList[i].getID()))

                      //      {
                      //          ClosedConnectionList.Add(clientList[i].getID());
                      //          ReconnectConnection(clientList[i]);
                      //      }

                      //  }
                      

                      }
                    Thread.Sleep(300);
                }
                Thread.Sleep(2000);
            }
        }
        public static void calculation()
        {
            while (true)
            {

                //     Console.WriteLine("RECIVED FROM CLIENT :: " + RecievedFromClient + " :: Directly Sent :" + SentToClientFromClient + " :: Sent to Other :: " + SentToServerFromClient + " :: Recieved from Other Server :: " + RecievedFromServer + " ::  Sent to Client recived from Other server " + SentToClientFromServer);

                Console.WriteLine("Total Recieved : " + recMsg );
              
                Interlocked.Exchange(ref recMsg, 0);
               
                Thread.Sleep(1000);
            }

        }
    }




    public class CustomMessage
    {
        // private static string text = "hello this is my testing application you can change it according to your need but what i will be doing here is to test the data througput. SO please keep your CPU Utilization in control THis is going to brust it into pieces ahhahahahahah :D now look."+ threadcount;
        private static string text = "HELLO THIS IS MY BROTHER |";
        private  string inputext;

        public static int textlength = 500;
        private static int threadcount = 0;

        public void setText(string s, int id)
        {
            inputext = id + "+"+s+"|";
          
            var tmp = Encoding.UTF8.GetBytes(inputext);
            // copy as much as we can from tmp to buffer
            Buffer.BlockCopy(tmp, 0, this.data, 0, tmp.Length);
        }
        public int FromClientid
        {
            get;
           set;
        }
        public String getTextfromData()
        {
            return Encoding.UTF8.GetString(data);
        }
        public int ToClientid
        {
            get;
             set;
        }
        public int getlength()
        {
            return textlength;
        }

        public Byte[] data
        {
            get;
            private set;
        }
       public void SetRecieverID(int id, string tx)
        {
            if(tx == null)
            {
                setText(text, id);
            }
            else
            {
                setText(tx, id);
            }
            

        }
        public CustomMessage()
        {

            threadcount++;
            this.FromClientid =0;
            this.ToClientid = 0;
            this.data = new Byte[500];
           
            //var tmp = Encoding.UTF8.GetBytes(text);
            //// copy as much as we can from tmp to buffer
            //Buffer.BlockCopy(tmp, 0, this.data, 0, tmp.Length);


        }
    }

    public class MyTcpClient
    {
        private static int Counter = SystemProperties.ClientStartRange;
        public byte[] bytes;
        public bool flag;
        public int offset;
        public int ReadSize;

        public bool getFlag() { return flag; }
        public int Id
        {
            get;
            private set;
        }
        public int PrevCount
        {
            get;
            private set;
        }
        public int NewCount
        {
            get;
            private set;
        }
        public int getID()
        {
            return this.Id;
        }
        public void setClient(TcpClient s)
        {
            this.TcpClient = s;

        }
        public string getText(int size)
        {
            StringBuilder myCompleteMessage = new StringBuilder();
            int numberOfBytesRead = 0;

            // Incoming message may be larger than the buffer size.

            myCompleteMessage.AppendFormat("{0}", Encoding.ASCII.GetString(bytes, 0, bytes.Length));
            string[] tex = myCompleteMessage.ToString().Split("|");

            return tex[0];
        }
        public TcpClient TcpClient
        {
            get;
            private set;
        }

        public MyTcpClient(TcpClient tcpClient)
        {
            if (tcpClient == null)
            {
                throw new ArgumentNullException("tcpClient");
            }

            this.TcpClient = tcpClient;
            this.Id = ++MyTcpClient.Counter;
            this.PrevCount = 0;
            this.NewCount = 0;
            this.offset = 0;
            this.ReadSize = 212;
            this.flag = true;
            this.bytes = new byte[212];
          
        }
    }

    
}


