using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Client
{
    class Client
    {
        static void Main(string[] args)
        {
            ClientEvents events = new ClientEvents();
            WickedClient WC = new WickedClient(events, WickedClient.ClientType.TCP_AND_UDP, "localhost", 500, 700);

            while (true)
            {
                Console.WriteLine("Which protocol do you want to use?\n(a)TCP\n(b)UDP");
                string p = Console.ReadLine();
                if (p == "a")
                {
                    Console.WriteLine("Please Enter A Message: ");
                    string msg = Console.ReadLine();
                    WC.SendString(WickedClient.ClientProtocol.TCP, msg);

                    if (msg == "#EXIT#")
                    {
                        WC.KillClient();
                        break;
                    }
                }
                else if (p == "b")
                {
                    Console.WriteLine("Please Enter A Message: ");
                    string msg = Console.ReadLine();
                    WC.SendString(WickedClient.ClientProtocol.UDP, msg);

                    if (msg == "#EXIT#")
                    {
                        WC.KillClient();
                        break;
                    }
                }
            }
        }
    }

    public class ClientEvents
    {
        public void recvPacketTCP(string text)
        {
            Console.WriteLine("TCP Packet Received: " + text);
        }

        public void recvPacketUDP(string text)
        {
            Console.WriteLine("UDP Packet Received: " + text);
        }

        public void clientForceDisconnect()
        {
            Console.WriteLine("Client Disconnceted...");
        }
    }
}
