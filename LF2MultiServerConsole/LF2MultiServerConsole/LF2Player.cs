using System;
using System.Collections.Generic;
using System.Text;
using System.Net;
using System.Net.Sockets;

namespace LF2MultiServerConsole
{
    class LF2Player
    {
        public Socket Client;
        public int PlayerIndex;

        public bool IsActive = true;
        public byte OutCount = 6;

        public byte LastFInput1;
        public byte LastFInput2;
        public byte LastKeyInput;

        public double TotalWaitAmount = 0;

        public String PlayerName;

        public LF2Player(Socket socketConnection)
        {
            Client = socketConnection;
        }

        public void SetPlayerName(String name)
        {
            //get player name without the "___" in the end
            int nameLength = 0;
            for (int i = name.Length - 1; i >= 0; --i)
            {
                if (name[i] != '_')
                {
                    nameLength = i + 1;
                    break;
                }
            }
            if (nameLength != 0)
            {
                PlayerName = name.Substring(0, nameLength);
            }
            else
            {
                PlayerName = "NoName";
            }
        }
    }
}
