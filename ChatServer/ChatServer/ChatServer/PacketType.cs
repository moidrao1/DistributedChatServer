using System;
using System.Collections.Generic;
using System.Text;

namespace ChatServer
{
    enum PacketType
    {
        PING = 0,
        MSG = 1,
        NEWCONNECTION = 2,
        CONNECTIONDROP =3,
        SERVERNAME =4


    }
}
