using System;
using System.Net;
using System.Net.Sockets;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Server
{
    class Server
    {
        static void Main(string[] args)
        {
            ClientEvents events = new ClientEvents();
            WickedServer WS = new WickedServer(events, WickedServer.ServerType.TCP_AND_UDP, 500, 700, 3);
            events.WS = WS;
            Console.WriteLine("Server Setup Complete...");
            Console.ReadKey();
            WS.KillServer();
        }
    }

    public class ClientEvents
    {
        public WickedServer WS;

        public void recvPacketTCP(string text, Socket client)
        {
            if (text == "#EXIT#")
            {
                WS.closeClientSocket(client);
                Console.WriteLine("Client peacefully disconnected...");
            }
            else
            {
                Console.WriteLine("TCP Packet Received: " + text);

                foreach (Socket s in WS.getClients())
                {
                    if (s != client)
                        WS.SendString(WickedServer.ServerProtocol.TCP, text, s);
                }
            }
        }

        public void recvPacketUDP(string text, Socket client)
        {
            if (text == "#EXIT#")
            {
                WS.closeClientSocket(client);
                Console.WriteLine("Client peacefully disconnected...");
            }
            else
            {
                Console.WriteLine("UDP Packet Received: " + text);

                foreach (Socket s in WS.getClients())
                {
                    if (s != client)
                        WS.SendString(WickedServer.ServerProtocol.UDP, text, s);
                }
            }
        }

        public void clientConnect (Socket client)
        {
            Console.WriteLine("Client Connected...");
        }

        public void clientForceDisconnect (Socket client)
        {
            Console.WriteLine("Client Forcefully Disconncted...");
        }

        public void clientUnidentified (Socket socket)
        {

        }
    }
}
