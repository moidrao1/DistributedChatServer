using System;
using System.Collections.Generic;
using System.Text;

namespace Simulator
{
    enum PacketType
    {
        PING = 0,
        MSG = 1,
        NEWCONNECTION = 2,
        CONNECTIONDROP = 3,
        SERVERNAME = 4,

    }
}
