using System;
using static AirShare.Core;

namespace AirShare
{
    public class Program
    {
        static Peer peer1;
        static Peer peer2;
        static Peer peer3;
        static void Main()
        {
            Console.WriteLine();
            Console.WriteLine("     -----------------------------        ");
            Console.WriteLine("               Air Share                  ");
            Console.WriteLine("     -----------------------------        ");
            Console.WriteLine();

            peer1 = new Peer();
            peer1.StartListener();
            //peer1.ScanIP("127.0.0.1");


            System.Threading.Thread.Sleep(2000);


            peer2 = new Peer();
            peer2.StartListener();
            //peer3.Scan();


            System.Threading.Thread.Sleep(2000);


            peer3 = new Peer();
            peer3.StartListener();
            //peer3.Scan();

            CLIRun();

        }


        private static void CLIRun()
        {
            Begin:

            string s = Prompt("Enter command");

            if (s == "")
            {
                //Console.Clear();
                ShowStatus(peer1);
                ShowStatus(peer2);
                ShowStatus(peer3);
            }
            else if (s == "p" || s == "ping")
            {
                peer1.PingAllClients();
            }
            else if (s == "s" || s == "scan")
            {
                peer1.ScanIP("127.0.0.1");
                Core.Sleep(1000);
                peer2.ScanIP("127.0.0.1");
                Core.Sleep(1000);
                peer3.ScanIP("127.0.0.1");
            }
            else if (s.StartsWith("/"))
            {
                foreach (var cl in peer1.clients)
                {
                    peer1.SendMsg(s.Substring(1), cl);
                }

            }

            goto Begin;

        }

        public static void ShowStatus(Peer peer)
        {
            Console.WriteLine();
            Console.WriteLine("     -----------------------------        ");
            Console.WriteLine("               Air Share                  ");
            Console.WriteLine("     -----------------------------        ");
            Console.WriteLine();

            Console.WriteLine($"IP {peer.ip} Port {peer.port} Name '{peer.name}'");

            string clientStats = "Connected clients : \n";
            lock (peer.clients)
            {
                foreach (var c in peer.clients)
                {
                    clientStats += $"\t{c.name} on {c.ip}:{c.port} ~ {c.LastTime.ToLongTimeString()}\n";
                }

            }

            Console.WriteLine(clientStats);

            Console.WriteLine();


        }

    }
}
