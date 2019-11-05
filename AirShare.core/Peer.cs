using System;
using System.Collections.Generic;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using static AirShare.Core;

namespace AirShare
{
    public class Peer
    {
        public int StartingPort = 49611;

        public string ip;
        public int port;
        public string name;

        public List<Client> clients = new List<Client>();
        public Dictionary<string, Client> clientIPs = new Dictionary<string, Client>();
        public Dictionary<string, bool> HotPeers = new Dictionary<string, bool>();

        public System.Timers.Timer responder = new System.Timers.Timer(300);

        TcpListener listener;

        public Peer()
        {
            ip = "127.0.0.1";//GetLocalIPAddress();
            port = StartingPort;
            name = Environment.UserName;
        }

        public static string GetLocalIPAddress()
        {
            var host = Dns.GetHostEntry(Dns.GetHostName());
            foreach (var ip in host.AddressList)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                {
                    return ip.ToString();
                }
            }
            throw new Exception("No network adapters with an IPv4 address in the system!");
        }


        public void StartListener()
        {
            responder.Elapsed += Responder_Elapsed;
            responder.Start();

            ReconnectToSocket:

            listener = new TcpListener(IPAddress.Any, port);
            try
            {
                listener.Start();
            }
            catch (SocketException)
            {
                Log($"Port {port} error");
                port++;
                goto ReconnectToSocket;
            }
            catch (Exception ex)
            {
                LogErr(ex, $"StartListener Error on listener.Start(); on port {port}");
                port++;
                goto ReconnectToSocket;
            }


            Log($"@{ip} Listening on port {port}...");

            new System.Threading.Thread(new System.Threading.ThreadStart(ThrListen)) { Name = $"Listen {port}" }.Start();

        }

        private void ThrListen()
        {
            while (true)   //we wait for a connection
            {

                var tcpclient = listener.AcceptTcpClient();  //if a connection exists, the server will accept it

                Client client = new Client() { tcp = tcpclient, ns = tcpclient.GetStream() };

                Log($"@{port} Client Accepted. Handle {client.tcp.Client.Handle.ToInt64()}");


                if (client.tcp.Connected)  //while the client is connected, we look for incoming messages
                {
                    bool connected = false;
                    for (int i = 0; i < 10; i++)
                    {
                        Transmit tr = ReadFromPeer(client.ns);

                        if (tr != null)
                        {
                            string addr = ((IPEndPoint)client.tcp.Client.RemoteEndPoint).Address.ToString();
                            Log($"First transmition recieved from {addr} {tr.ToString()}");
                            // int prt = ((IPEndPoint)client.tcp.Client.RemoteEndPoint).Port;
                            tr.ip = addr;
                            Ping ping = (Ping)tr;
                            if (ping != null)
                            {
                                string addrPort = addr + ":" + ping.port;
                                if (clientIPs.TryGetValue(addrPort, out Client client1))
                                {
                                    client = client1;
                                }
                                else
                                {
                                    //New client

                                    lock (clients)
                                    {
                                        client.ip = addr;
                                        client.port = ping.port;
                                        client.LastTime = DateTime.Now;
                                        clients.Add(client);
                                        clientIPs.Add(addrPort, client);
                                        HotPeers[addrPort] = false;
                                    }
                                }

                                if (Respond(tr))
                                {
                                    Log($"@{port} New client added. Name {client.name}, IP {client.ip}:{client.port}", ConsoleColor.Cyan);
                                    connected = true;
                                }
                                else
                                {
                                    lock (clients)
                                    {
                                        clients.Remove(client);
                                        clientIPs.Remove(ping.ip + ":" + ping.port.ToString());
                                    }
                                }
                                break;
                            }
                            else
                            {
                                Log($"Transmition recieved is not a ping.");
                            }
                        }
                        else
                        {
                            Log($"Transmition recieved is null. Retrying...");
                        }
                    }

                    if (connected)
                    {

                        Log($"Client Connected {client.ip} {client.name}");
                    }
                    else
                    {
                        Log($"Client not properly Connected. {client.ip} {client.name}");
                    }
                }
            }
        }

        public Transmit ReadFromPeer(NetworkStream ns)
        {
            int start;
            int tries = 0;
            ReadAgain:

            for (int i = 1; i < 10; i++)
            {
                if (ns.DataAvailable)
                {
                    goto ReadNow;
                }
                else
                {
                    Sleep(500);
                }
            }
            ns.Flush();
            return null;

            ReadNow:
            start = ns.ReadByte();
            if (start == -1)
            {
                if (tries > 4) { Log("@ReadFromPeer : StartByte time out"); return null; }
                tries++;
                System.Threading.Thread.Sleep(100);
                goto ReadAgain;
            }
            if (start != 42)
            {

                Log($"@{port}ReadFromPeer : StartByte is wrong");
                ns.Flush();
                return null;
            }

            // ns.WriteByte(43);


            for (int i = 1; i < 10; i++)
            {
                if (ns.DataAvailable)
                {
                    goto ReadLenght;
                }
                else
                {
                    Sleep(500);
                }
            }
            ns.Flush();
            return null;

            ReadLenght:
            byte[] lengthBytes = new byte[4];
            ns.Read(lengthBytes, 0, 4);

            Int32 length = BitConverter.ToInt32(lengthBytes, 0);


            byte[] transmitBytes = new byte[length];
            ns.Read(transmitBytes, 0, length);
            try
            {
                Newtonsoft.Json.Linq.JObject obj = (Newtonsoft.Json.Linq.JObject)Newtonsoft.Json.JsonConvert.DeserializeObject(Encoding.Unicode.GetString(transmitBytes));
                Transmit tr = obj.ToObject<Transmit>();
                return tr.t switch
                {
                    'T' => tr,
                    'P' => obj.ToObject<Ping>(),
                    'R' => obj.ToObject<Request>(),
                    'r' => obj.ToObject<Response>(),
                    'X' => obj.ToObject<ContentTransmit>(),
                    _ => null,
                };
            }
            catch (Exception ex)
            {
                LogErr(ex, $"Cannot DeserializeObject from client");
                try
                {
                    Log($"Recieved string is {Encoding.Unicode.GetString(transmitBytes)}", ConsoleColor.Yellow);
                }
                catch (Exception)
                {
                    Log("Cannot convert recieved bytes into string", ConsoleColor.Red);
                }
                return null;
            }


        }


        public void WriteToPeer(Transmit tr, NetworkStream ns)
        {
            ns.WriteByte(42);


            byte[] bytes = System.Text.Encoding.Unicode.GetBytes(Newtonsoft.Json.JsonConvert.SerializeObject(tr));
            byte[] lengthBytes = BitConverter.GetBytes(bytes.Length);

            ns.Write(lengthBytes, 0, 4);
            ns.Write(bytes, 0, bytes.Length);

        }

        bool Responding = false;
        private void Responder_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            if (Responding) return;
            Responding = true;

            int n = 0;
            while (n < clients.Count)
            {
                var client = clients[n];
                string adrpt = $"{client.ip}:{client.port}";
                if (!HotPeers[adrpt])
                {
                    HotPeers[adrpt] = true;
                    try
                    {
                        if (client.ns.DataAvailable)
                        {
                            Transmit t = ReadFromPeer(client.ns);
                            if (t != null)
                            {
                                Respond(t);
                                Sleep(20);
                            }
                        }
                        HotPeers[adrpt] = false;
                    }
                    catch (Exception)
                    {
                        HotPeers[adrpt] = false;
                        Responding = false;
                        throw;
                    }
                }

                n++;

            }

            Responding = false;
        }

        public bool Respond(Transmit t)
        {
            if (t == null)
            {
                return false;
            }
            else if (t is Ping ping)
            {

                if (clientIPs.TryGetValue(ping.ip + ":" + ping.port.ToString(), out Client client))
                {
                    client.LastTime = DateTime.Now;
                    client.name = ping.nm;

                    WriteToPeer(new Response() { ip = ip, time = DateTime.Now.ToBinary(), stat = Response.Status.ok, port = port, nm = name }, client.ns);

                    return true;

                }
                else
                {
                    return false;
                }

            }
            else if (t is ContentTransmit ct)
            {
                switch (ct.c)
                {
                    case ContentTransmit.Command.none:
                        break;
                    case ContentTransmit.Command.msg:
                        Log($" {ct.nm} >> {ct.content} \t ({ct.ip}:{ct.port} -> {ip}:{port}) ");
                        break;
                    default:
                        break;
                }
                return true;
            }
            else
            {
                return false; ;
            }
        }



        public void Scan()
        {
            foreach (NetworkInterface ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                foreach (UnicastIPAddressInformation ip in ni.GetIPProperties().UnicastAddresses)
                {
                    if (!ip.IsDnsEligible)
                    {
                        if (ip.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                        {
                            string addr = ip.Address.ToString();
                            Log($"IP found {addr}");

                            ScanIP(addr);
                        }
                    }
                }
            }

        }

        public void ScanIP(string addr)
        {
            new System.Threading.Thread(new System.Threading.ParameterizedThreadStart(ThrScanIP)) { Name = $"Scan {addr}" }.Start(addr);
        }

        private void ThrScanIP(object obj)
        {
            string addr = (string)obj;
            for (int p = StartingPort; p < StartingPort + 3; p++)
            {
                if (!(addr == ip && p == port))
                {
                    if (PingIP(addr, p))
                    {
                        Log($"Client connected @{port} {addr}:{p}");
                        Sleep(50);
                    }
                    else
                    {
                        // Log($"Client connection failed {addr}:{p}");
                    }
                }
                else
                {
                    Log($"Skip scanning myself {addr}:{p}", ConsoleColor.Yellow);
                }
            }
        }

        public void PingAllClients()
        {
            foreach (var client in clients)
            {
                Log(PingIP(client.ip, client.port) ? $"Ping {client.ip}:{client.port}" : $"Ping failed {client.ip}:{client.port}");
            }
        }


        public bool PingIP(string address, int p)
        {
            TcpClient tcpclient;
            NetworkStream ns;
            bool ClientExists = false;

            if (address == ip && p == port)
            {
                Log($"Skip Pinging myself {address}:{p}", ConsoleColor.Yellow);
                return false;
            }

            string addrPort = address + ":" + p;



            if (clientIPs.TryGetValue(addrPort, out Client oldClient))
            {

                while (HotPeers[addrPort])
                {
                    Sleep(100);
                }

                HotPeers[addrPort] = true;

                tcpclient = oldClient.tcp;
                ns = oldClient.ns;
                ClientExists = true;
            }
            else
            {
                try
                {
                    tcpclient = new TcpClient(address, p);
                    ns = tcpclient.GetStream();
                }
                catch (SocketException)
                {
                    return false;
                }
                catch (Exception)
                {
                    // LogErr(ex, "PingIP failed");
                    return false;
                }

            }




            var resp = PingPeer(ns);

            if (resp == null) return false;
            else if (resp.stat == Response.Status.ok)
            {
                if (ClientExists)
                {
                    oldClient.LastTime = DateTime.Now;
                    oldClient.name = resp.nm;

                    HotPeers[addrPort] = false;
                    return true;
                }
                else
                {
                    lock (clients)
                    {
                        if (clientIPs.ContainsKey(addrPort))
                        {
                            HotPeers[addrPort] = false;
                            return true;
                        }

                        Client client = new Client() { tcp = tcpclient, ns = ns, ip = address, name = resp.nm, port = resp.port, LastTime = DateTime.Now };
                        clients.Add(client);
                        clientIPs.Add(addrPort, client);
                        HotPeers[addrPort] = false;

                        Log($"@{port} Ping New client added. Name {client.name}, IP {client.ip}:{client.port}", ConsoleColor.Cyan);

                        return true;
                    }
                }

            }
            else
            {
                HotPeers[addrPort] = false;
                return false;
            }

        }

        public Response PingPeer(NetworkStream ns)
        {

            WriteToPeer(new Ping() { ip = ip, port = port, time = DateTime.Now.ToBinary(), nm = name }, ns);

            for (int i = 1; i < 10; i++)
            {
                if (ns.DataAvailable)
                {
                    goto ReadNow;
                }
                else
                {
                    Sleep(500);
                }
            }
            ns.Flush();
            return null;

            ReadNow:
            var tr = ReadFromPeer(ns);

            if (tr == null) return null;
            if (tr is Response resp)
            {
                return resp;
            }
            else
            {
                return null;
            }
        }


        public void SendMsg(string s, Client client)
        {

            string addrPort = client.ip + ":" + client.port;

            while (HotPeers[addrPort])
            {
                Sleep(100);
            }

            HotPeers[addrPort] = true;

            ContentTransmit ct = new ContentTransmit() { c = ContentTransmit.Command.msg, ip = ip, port = port, time = DateTime.Now.ToBinary(), nm = name, content = s };
            WriteToPeer(ct, client.ns);

            HotPeers[addrPort] = false;
        }

    }
    public class Client
    {
        public NetworkStream ns;
        public TcpClient tcp;
        public int port;
        public string ip;
        public string name;
        public DateTime LastTime;
    }
}
