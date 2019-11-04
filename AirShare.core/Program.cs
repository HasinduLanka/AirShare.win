using System;
using static AirShare.Core;

namespace AirShare
{
    public class Program
    {
        static Peer peer;
        static void Main()
        {
            Console.WriteLine();
            Console.WriteLine("     -----------------------------        ");
            Console.WriteLine("               Air Share                  ");
            Console.WriteLine("     -----------------------------        ");
            Console.WriteLine();

            peer = new Peer();
            peer.StartListener();
            peer.Scan();


            System.Threading.Thread.Sleep(2000);


            Peer peer2 = new Peer();
            peer2.StartListener();
            peer2.Scan();

            //Peer peer3 = new Peer();
            //peer3.StartListener();
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
                ShowStatus();
            }
            else if (s == "p" || s == "ping")
            {
                peer.PingAllClients();
            }

            goto Begin;

        }

        public static void ShowStatus()
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
                    clientStats += $"\t{c.name} on {c.ip}:{c.port} ~ {c.LastTime.ToShortTimeString()}\n";
                }

            }

            Console.WriteLine(clientStats);
        }

    }
}
