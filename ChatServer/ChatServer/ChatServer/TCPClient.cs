using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;

namespace ChatServer
{
    public class MyTcpClient
    {
        private static int Counter = SystemProperties.ClientStartRange;
        public byte[] bytes;
        public byte[] LBbytes;
        public int offset;
        public int ReadSize;
        public int LBReadSize;
        public int Id
        {
            get;
            private set;
        }
        public int getID() { return Id; }
        public void setID(int x)
        {
            Id = x;
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
        public void SetNewMemory(byte[] arr)
        {
            this.bytes = arr;
        }
        public MyTcpClient(TcpClient tcpClient)
        {
            if (tcpClient == null)
            {
                throw new ArgumentNullException("tcpClient");
            }
               this.bytes = new byte[212];
               this.LBbytes = new byte[200];
                     this.offset = 0;
                     this.ReadSize = 212;
                     this.LBReadSize = 200;
               this.TcpClient = tcpClient;
               this.Id = ++MyTcpClient.Counter;
        }
    }
}
