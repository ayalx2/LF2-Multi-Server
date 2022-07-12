using System;
using System.Collections.Generic;
using System.Text;
using System.Net;
using System.Net.Sockets;

namespace LF2MultiServerConsole
{
    class SocketsUtils
    {
        public const int SOCKET_ERROR_PORT_ALREADY_OPENED = 10048;

        private static ASCIIEncoding encoding = new ASCIIEncoding();

        public static void SendAll(Socket socket, byte[] data)
        {
            socket.Send(data);

            #if _DEBUG
            Console.Write("Send: ");
            TextUtils.PrintBinary(data);
            #endif
        }

        public static void SendAll(Socket socket, String data)
        {
            SendAll(socket, encoding.GetBytes(data));
        }

        public static void ReceiveAll(Socket socket, byte[] receivedBuffer, int size)
        {
            int totalBytesRecevied = 0;
            int recv = 0;

            while (totalBytesRecevied < size)
            {
                recv = socket.Receive(receivedBuffer, totalBytesRecevied, size - totalBytesRecevied, SocketFlags.None);
                
                if (recv == 0)
                {
                    throw new Exception("Session disconnected");
                }
                
                totalBytesRecevied += recv;
            }

            #if _DEBUG
            Console.Write("Recv: ");
            TextUtils.PrintBinary(receivedBuffer);
            #endif
        }

        public static void ReceiveAll(Socket socket, out String receivedString, int size)
        {
            byte[] data = new byte[size];
            ReceiveAll(socket, data, size);
            receivedString = encoding.GetString(data);
        }
    }
}