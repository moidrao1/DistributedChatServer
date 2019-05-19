using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace ChatServer
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct Packet
    {
        public Int32 SenID;
        public Int32 RecID;
        public Int32 MsgType;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 100)]
        public string payload;
    }
}
