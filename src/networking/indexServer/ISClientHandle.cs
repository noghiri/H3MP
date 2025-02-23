﻿using H3MP.Scripts;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;

namespace H3MP.Networking
{
    public class ISClientHandle
    {
        public static void Welcome(Packet packet)
        {
            string msg = packet.ReadString();
            int ID = packet.ReadInt();

            Mod.LogInfo("Message from server: "+msg, false);

            ISClient.gotWelcome = true;
            ISClient.ID = ID;
            ISClientSend.WelcomeReceived();

            ISClientSend.RequestHostEntries();
        }

        public static void Ping(Packet packet)
        {
            GameManager.ping = Convert.ToInt64((DateTime.Now.ToUniversalTime() - ISThreadManager.epoch).TotalMilliseconds) - packet.ReadLong();
        }

        public static void HostEntries(Packet packet)
        {
            List<ISEntry> entries = new List<ISEntry>();
            int count = packet.ReadInt();
            for (int i = 0; i < count; ++i) 
            {
                ISEntry newEntry = new ISEntry();
                newEntry.ID = packet.ReadInt();
                newEntry.name = packet.ReadString();
                newEntry.playerCount = packet.ReadInt();
                newEntry.limit = packet.ReadInt();
                newEntry.locked = packet.ReadBool();
                entries.Add(newEntry);
            }
            ISClient.OnReceiveHostEntriesInvoke(entries);
        }

        public static void Listed(Packet packet)
        {
            int listedID = packet.ReadInt();

            ISClient.OnListedInvoke(listedID);
        }

        public static void Connect(Packet packet)
        {
            if(ServerListController.instance != null)
            {
                bool gotEndPoint = packet.ReadBool();
                if (gotEndPoint)
                {
                    ServerListController.instance.gotEndPoint = true;
                    int byteCount = packet.ReadInt();
                    IPAddress address = new IPAddress(packet.ReadBytes(byteCount));
                    IPEndPoint endPoint = new IPEndPoint(address, packet.ReadInt());

                    if (ServerListController.instance.state == ServerListController.State.ClientWaiting)
                    {
                        ServerListController.instance.SetClientPage(true);
                        Client.punchThrough = true;
                        Mod.OnConnectClicked(endPoint);
                        ThreadManager.pingTimer = ThreadManager.pingTime;
                    }
                    else if (Mod.managerObject != null && ThreadManager.host)
                    {
                        int clientID = -1;
                        for (int i = 1; i <= Server.maxClientCount; ++i)
                        {
                            if (Server.clients[i].tcp.socket == null && !Server.clients[i].attemptingPunchThrough)
                            {
                                Server.clients[i].attemptingPunchThrough = true;
                                clientID = i;
                                break;
                            }
                        }

                        if(clientID == -1)
                        {
                            Mod.LogError("Received PT connect order from IS but we are at player limit");
                            return;
                        }
                        ServerClient currentClient = Server.clients[clientID];
                        currentClient.PTEndPoint = endPoint;

                        currentClient.punchThrough = true;

                        currentClient.PTUDP = new UdpClient((ISClient.socket.Client.LocalEndPoint as IPEndPoint).Port);

                        Mod.LogInfo("Attempting connection to " + address + ":" + endPoint.Port, false);

                        using (Packet initialPacket = new Packet((int)ServerPackets.punchThrough))
                        {
                            currentClient.PTUDP.BeginSend(packet.ToArray(), packet.Length(), endPoint, null, null);
                        }
                        currentClient.PTUDPEstablished = true;
                        currentClient.punchThroughAttemptCounter = 0;

                        Server.PTClients.Add(currentClient);
                    }
                }
                else
                {
                    ServerListController.instance.gotEndPoint = false;
                    ServerListController.instance.joiningEntry = -1;
                    ServerListController.instance.SetClientPage(true);
                }
            }
            else
            {
                bool gotEndPoint = packet.ReadBool();
                if (gotEndPoint)
                {
                    if (Mod.managerObject != null && ThreadManager.host)
                    {
                        int byteCount = packet.ReadInt();
                        IPAddress address = new IPAddress(packet.ReadBytes(byteCount));
                        IPEndPoint endPoint = new IPEndPoint(address, packet.ReadInt());

                        int clientID = -1;
                        for (int i = 1; i <= Server.maxClientCount; ++i)
                        {
                            if (Server.clients[i].tcp.socket == null && !Server.clients[i].attemptingPunchThrough)
                            {
                                Server.clients[i].attemptingPunchThrough = true;
                                break;
                            }
                        }

                        if (clientID == -1)
                        {
                            Mod.LogError("Received PT connect order from IS but we are at player limit");
                            return;
                        }
                        ServerClient currentClient = Server.clients[clientID];
                        currentClient.PTEndPoint = endPoint;

                        currentClient.punchThrough = true;

                        currentClient.PTUDP = new UdpClient((ISClient.socket.Client.LocalEndPoint as IPEndPoint).Port);

                        Mod.LogInfo("Attempting connection to " + address + ":" + endPoint.Port, false);

                        using (Packet initialPacket = new Packet((int)ServerPackets.punchThrough))
                        {
                            currentClient.PTUDP.BeginSend(packet.ToArray(), packet.Length(), endPoint, null, null);
                        }
                        currentClient.PTUDPEstablished = true;
                        currentClient.punchThroughAttemptCounter = 0;

                        Server.PTClients.Add(currentClient);
                    }
                }
            }
        }

        public static void ConfirmConnection(Packet packet)
        {
            int forClient = packet.ReadInt();

            ISClientSend.ConfirmConnection(Mod.managerObject != null && ThreadManager.host && ISClient.isConnected && ISClient.listed && GameManager.players.Count < Server.maxClientCount, forClient);
        }
    }
}
