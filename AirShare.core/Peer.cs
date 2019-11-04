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
        public int StartingPort = 26261;

        public string ip;
        public int port;
        public string name;

        public List<Client> clients = new List<Client>();
        public Dictionary<string, Client> clientIPs = new Dictionary<string, Client>();

        public System.Timers.Timer responder = new System.Timers.Timer(300);

        TcpListener listener;

        public Peer()
        {
            ip = GetLocalIPAddress();
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


            Log($"Listening on port {port}...");

            new System.Threading.Thread(new System.Threading.ThreadStart(ThrListen)) { Name = $"Listen {port}" }.Start();

        }

        private void ThrListen()
        {
            while (true)   //we wait for a connection
            {

                var tcpclient = listener.AcceptTcpClient();  //if a connection exists, the server will accept it

                Client client = new Client() { tcp = tcpclient, ns = tcpclient.GetStream() };

                Log($"Client Accepted. Handle {client.tcp.Client.Handle.ToInt64()}");


                if (client.tcp.Connected)  //while the client is connected, we look for incoming messages
                {
                    bool connected = false;
                    for (int i = 0; i < 10; i++)
                    {
                        Transmit tr = ReadFromPeer(client.ns);

                        if (tr != null)
                        {
                            Log($"First transmition recieved from {tr.ip} {tr.ToString()}");
                            Ping ping = (Ping)tr;
                            if (ping != null)
                            {

                                if (!clientIPs.ContainsKey(ping.ip + ":" + ping.port.ToString()))
                                {
                                    //New client

                                    lock (clients)
                                    {
                                        client.ip = ping.ip;
                                        client.LastTime = DateTime.Now;
                                        clients.Add(client);
                                        clientIPs.Add(ping.ip + ":" + ping.port.ToString(), client);

                                    }
                                }

                                if (Respond(tr))
                                {
                                    Log($"New client added. Name {client.name}, IP {client.ip}:{client.port}");
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

            start = ns.ReadByte();
            if (start == -1)
            {
                if (tries > 10) { Log("@ReadFromPeer : StartByte time out"); return null; }
                tries++;
                System.Threading.Thread.Sleep(100);
                goto ReadAgain;
            }
            if (start != 0b101010)
            {
                Log("@ReadFromPeer : StartByte is wrong");
                return null;
            }

            ns.WriteByte(0b101011);


            byte[] lengthBytes = new byte[start];
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
                    _ => null,
                };
            }
            catch (Exception ex)
            {
                LogErr(ex, "Cannot DeserializeObject from client");
                return null;
            }


        }


        public void WriteToPeer(Transmit tr, NetworkStream ns)
        {
            ns.WriteByte(0b101010);

            int start;
            int tries = 0;
            ReadAgain:

            start = ns.ReadByte();
            if (start == -1)
            {
                if (tries > 10) return;
                tries++;
                System.Threading.Thread.Sleep(100);
                goto ReadAgain;
            }
            else if (start != 0b101011)
            {
                return;
            }




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
                Transmit t = ReadFromPeer(client.ns);
                if (t != null)
                {
                    Respond(t);
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
                    client.port = ping.port;

                    WriteToPeer(new Response() { ip = ip, time = DateTime.Now.ToBinary(), stat = Response.Status.ok, port = port, nm = name }, client.ns);

                    return true;

                }
                else
                {
                    return false;
                }

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

                            new System.Threading.Thread(new System.Threading.ParameterizedThreadStart(ScanIP)) { Name = $"Scan {addr}" }.Start(addr);
                        }
                    }
                }
            }

        }

        private void ScanIP(object obj)
        {
            string addr = (string)obj;
            for (int p = StartingPort; p < StartingPort + 1; p++)
            {
                if (PingIP(addr, p))
                {
                    Log($"Client connected {addr}:{p}");
                }
                else
                {
                    Log($"Client connection failed {addr}:{p}");
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


        public bool PingIP(string address, int port)
        {
            TcpClient tcpclient;
            NetworkStream ns;
            bool ClientExists = false;

            if (clientIPs.TryGetValue(address + ":" + port, out Client oldClient))
            {
                tcpclient = oldClient.tcp;
                ns = oldClient.ns;
                ClientExists = true;
            }
            else
            {
                try
                {
                    tcpclient = new TcpClient(address, port);
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
                    oldClient.port = resp.port;

                    return true;
                }
                else
                {
                    lock (clients)
                    {
                        if (resp.ip == ip) return false;
                        Client client = new Client() { tcp = tcpclient, ns = ns, ip = resp.ip, name = resp.nm, port = resp.port, LastTime = DateTime.Now };
                        clients.Add(client);
                        clientIPs.Add(client.ip + ":" + client.port, client);

                        return true;
                    }
                }

            }
            else
                return false;


        }

        public Response PingPeer(NetworkStream ns)
        {

            WriteToPeer(new Ping() { ip = ip, port = port, time = DateTime.Now.ToBinary(), nm = name }, ns);
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
