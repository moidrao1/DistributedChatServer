using System;
using System.Collections.Generic;
using System.Text;

namespace ChatServer
{
    public class SystemProperties
    {
        public static int  ServerName =0;
        public static int ServerPort = 0;
        public static string ServerIP = String.Empty;
       
        public static string LBIP = String.Empty;
        public static int LBPort = 0;
        public static int ClientStartRange = 0;
        public static string logfilePath = string.Empty;
        public static string logfileName = string.Empty;
      
        public static string logExFilePath = string.Empty;
        public static string logExFileName = string.Empty;
        public static int forServerPort = 0;
        public static int ConnectionBufferSize = 100000;
          
}
}
