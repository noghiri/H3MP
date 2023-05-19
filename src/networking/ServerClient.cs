﻿using System;
using System.Net;
using System.Net.Sockets;
using UnityEngine;
using FistVR;
using System.Collections.Generic;
using H3MP.Patches;
using H3MP.Tracking;

namespace H3MP.Networking
{
    internal class ServerClient
    {
        public static int dataBufferSize = 4096;

        public int ID;
        public Player player;
        public TCP tcp;
        public UDP udp;
        public bool connected;
        public long ping;

        // Customization: Event to let mods know when the client disconnects
        public delegate void OnClientDisconnectDelegate();
        public event OnClientDisconnectDelegate OnClientDisconnect;

        public ServerClient(int ID)
        {
            this.ID = ID;
            tcp = new TCP(ID);
            udp = new UDP(ID);
        }

        public class TCP
        {
            public TcpClient socket;

            public long openTime;

            private readonly int ID;
            private NetworkStream stream;
            private Packet receivedData;
            private byte[] receiveBuffer;

            public TCP(int ID)
            {
                this.ID = ID;
            }

            public void Connect(TcpClient socket)
            {
                openTime = Convert.ToInt64((DateTime.Now.ToUniversalTime() - ThreadManager.epoch).TotalMilliseconds);

                this.socket = socket;
                socket.ReceiveBufferSize = dataBufferSize;
                socket.SendBufferSize = dataBufferSize;

                stream = socket.GetStream();

                receivedData = new Packet();
                receiveBuffer = new byte[dataBufferSize];

                stream.BeginRead(receiveBuffer, 0, dataBufferSize, ReceiveCallback, null);

                ServerSend.Welcome(ID, "Welcome to the server", GameManager.colorByIFF, GameManager.nameplateMode, GameManager.radarMode, GameManager.radarColor, GameManager.maxHealthByInstanceByScene);
            }

            public void SendData(Packet packet)
            {
                try
                {
                    if(socket != null)
                    {
                        stream.BeginWrite(packet.ToArray(), 0, packet.Length(), null, null);
                    }
                }
                catch(Exception ex)
                {
                    Mod.LogError($"Error sending data to player {ID} via TCP: {ex}");
                }
            }

            private void ReceiveCallback(IAsyncResult result)
            {
                int byteLength = 0;
                byte[] data = null;
                try
                {
                    byteLength = stream.EndRead(result);
                    if (byteLength == 0 && Server.clients[ID].connected)
                    {
                        Server.clients[ID].Disconnect(0);
                        return;
                    }

                    data = new byte[byteLength];
                    Array.Copy(receiveBuffer, data, byteLength);

                    receivedData.Reset(HandleData(data));
                    stream.BeginRead(receiveBuffer, 0, dataBufferSize, ReceiveCallback, null);
                }
                catch (Exception ex)
                {
                    if (Server.clients[ID].connected)
                    {
                        if (data != null)
                        {
                            for (int i = 0; i < byteLength; ++i)
                            {
                                Mod.LogWarning("data[" + i + "] = " + data[i]);
                            }
                        }
                        Server.clients[ID].Disconnect(1, ex);
                    }
                }
            }

            private bool HandleData(byte[] data)
            {
                int packetLength = 0;

                receivedData.SetBytes(data);

                if (receivedData.UnreadLength() >= 4)
                {
                    packetLength = receivedData.ReadInt();
                    if (packetLength <= 0)
                    {
                        return true;
                    }
                }

                while (packetLength > 0 && packetLength <= receivedData.UnreadLength())
                {
                    byte[] packetBytes = receivedData.ReadBytes(packetLength);
                    ThreadManager.ExecuteOnMainThread(() =>
                    {
                        if (Server.tcpListener != null)
                        {
                            using (Packet packet = new Packet(packetBytes))
                            {
                                int packetID = packet.ReadInt();

                                if(packetID < 0)
                                {
                                    if(packetID == -1)
                                    {
                                        Mod.GenericCustomPacketReceivedInvoke(ID, packet.ReadString(), packet);
                                    }
                                    else // packetID <= -2
                                    {
                                        int index = packetID * -1 - 2;
                                        if (Mod.customPacketHandlers[index] != null)
                                        {
#if DEBUG
                                            if (Input.GetKey(KeyCode.PageDown))
                                            {
                                                Mod.LogInfo("\tHandling custom TCP packet: " + packetID);
                                            }
#endif
                                            Mod.customPacketHandlers[index](ID, packet);
                                        }
#if DEBUG
                                        else
                                        {
                                            Mod.LogError("\tServer received invalid custom TCP packet ID: " + packetID+" from client "+ID);
                                        }
#endif
                                    }
                                }
                                else
                                {
#if DEBUG
                                    if (Input.GetKey(KeyCode.PageDown))
                                    {
                                        Mod.LogInfo("\tHandling TCP packet: " + packetID);
                                    }
#endif
                                    Server.packetHandlers[packetID](ID, packet);
                                }
                            }
                        }
                    });

                    packetLength = 0;

                    if (receivedData.UnreadLength() >= 4)
                    {
                        packetLength = receivedData.ReadInt();
                        if (packetLength <= 0)
                        {
                            return true;
                        }
                    }
                }

                if (packetLength <= 1)
                {
                    return true;
                }

                return false;
            }

            public void Disconnect()
            {
                socket.Close();
                stream = null;
                receiveBuffer = null;
                receivedData = null;
                socket = null;
            }
        }

        public class UDP
        {
            public IPEndPoint endPoint;

            private int ID;

            public UDP(int ID)
            {
                this.ID = ID;
            }

            public void Connect(IPEndPoint endPoint)
            {
                this.endPoint = endPoint;
            }

            public void SendData(Packet packet)
            {
                Server.SendUDPData(endPoint, packet);
            }

            public void HandleData(Packet packetData)
            {
                int packetLength = packetData.ReadInt();
                byte[] packetBytes = packetData.ReadBytes(packetLength);

                ThreadManager.ExecuteOnMainThread(() =>
                {
                    if (Server.tcpListener != null)
                    {
                        using (Packet packet = new Packet(packetBytes))
                        {
                            int packetID = packet.ReadInt();

                            if (packetID < 0)
                            {
                                if (packetID == -1)
                                {
                                    Mod.GenericCustomPacketReceivedInvoke(ID, packet.ReadString(), packet);
                                }
                                else // packetID <= -2
                                {
                                    int index = packetID * -1 - 2;
                                    if (Mod.customPacketHandlers[index] != null)
                                    {
#if DEBUG
                                        if (Input.GetKey(KeyCode.PageDown))
                                        {
                                            Mod.LogInfo("\tHandling custom UDP packet: " + packetID);
                                        }
#endif
                                        Mod.customPacketHandlers[index](ID, packet);
                                    }
#if DEBUG
                                    else
                                    {
                                        Mod.LogError("\tServer received invalid custom UDP packet ID: " + packetID + " from client " + ID);
                                    }
#endif
                                }
                            }
                            else
                            {
#if DEBUG
                                if (Input.GetKey(KeyCode.PageDown))
                                {
                                    Mod.LogInfo("\tHandling UDP packet: " + packetID);
                                }
#endif
                                Server.packetHandlers[packetID](ID, packet);
                            }
                        }
                    }
                });
            }

            public void Disconnect()
            {
                endPoint = null;
            }
        }

        public void SendIntoGame(string playerName, string scene, int instance, int IFF, int colorIndex)
        {
            player = new Player(ID, playerName, Vector3.zero, IFF, colorIndex);
            player.scene = scene;
            player.instance = instance;

            // Spawn this client's player in all connected client but itself
            foreach(ServerClient client in Server.clients.Values)
            {
                if(client.player != null)
                {
                    if(client.ID != ID)
                    {
                        ServerSend.SpawnPlayer(ID, client.player, scene, instance, IFF, colorIndex);
                    }
                }
            }

            // Also spawn player for host
            GameManager.singleton.SpawnPlayer(player.ID, player.username, scene, instance, player.position, player.rotation, IFF, colorIndex);

            // Spawn all clients' players in this client
            bool inControl = true;
            foreach (ServerClient client in Server.clients.Values)
            {
                if(client.player != null && client.ID != ID)
                {
                    ServerSend.SpawnPlayer(client.ID, player, client.player.scene, client.player.instance, IFF, colorIndex, true);
                    inControl &= !scene.Equals(client.player.scene);
                }
            }

            // Also spawn host player in this client
            ServerSend.SpawnPlayer(ID, 0, Mod.config["Username"].ToString(), GameManager.sceneLoading ? LoadLevelBeginPatch.loadingLevel : GameManager.scene, GameManager.instance, GM.CurrentPlayerBody.transform.position, GM.CurrentPlayerBody.transform.rotation, GM.CurrentPlayerBody == null ? -3 : GM.CurrentPlayerBody.GetPlayerIFF(), GameManager.colorIndex, true);
            inControl &= !scene.Equals(GameManager.sceneLoading ? LoadLevelBeginPatch.loadingLevel : GameManager.scene);

            Mod.LogInfo("Player " + ID + " join server in scene " + scene, false);
            if (!GameManager.nonSynchronizedScenes.ContainsKey(scene))
            {
                if (GameManager.playersByInstanceByScene.TryGetValue(scene, out Dictionary<int, List<int>> instances) &&
                    instances.TryGetValue(player.instance, out List<int> otherPlayers) && otherPlayers.Count > 1)
                {
                    List<int> waitingFromClients = new List<int>();

                    // There are other players in the client's scene/instance, request up to date objects before sending
                    for (int i = 0; i < otherPlayers.Count; ++i)
                    {
                        if (otherPlayers[i] != ID)
                        {
                            if (Server.clientsWaitingUpDate.ContainsKey(otherPlayers[i]))
                            {
                                Server.clientsWaitingUpDate[otherPlayers[i]].Add(ID);
                            }
                            else
                            {
                                Server.clientsWaitingUpDate.Add(otherPlayers[i], new List<int> { ID });
                            }
                            ServerSend.RequestUpToDateObjects(otherPlayers[i], false, ID);
                            waitingFromClients.Add(otherPlayers[i]);
                        }
                    }

                    if (waitingFromClients.Count > 0)
                    {
                        if (Server.loadingClientsWaitingFrom.ContainsKey(ID))
                        {
                            Server.loadingClientsWaitingFrom[ID] = waitingFromClients;
                        }
                        else
                        {
                            Server.loadingClientsWaitingFrom.Add(ID, waitingFromClients);
                        }
                    }
                    else
                    {
                        Mod.LogInfo("Client " + ID + " just got sent into game, no other player in scene/instance, sending relevant tracked objects");
                        SendRelevantTrackedObjects();
                    }
                }
                else // No other player in the client's scene/instance 
                {
                    Mod.LogInfo("Client " + ID + " just got sent into game, no other player in scene/instance, sending relevant tracked objects");
                    SendRelevantTrackedObjects();
                }

                // Tell the client to sync its items
                ServerSend.ConnectSync(ID, inControl);
            }

            // Also send TNH instances
            ServerSend.InitTNHInstances(ID);
        }

        public void SendRelevantTrackedObjects(int fromClient = -1)
        {
            Mod.LogInfo("Sending relevant object to " + ID + " from " + fromClient+" in "+ player.scene+"/"+ player.instance);

            if (GameManager.objectsByInstanceByScene.TryGetValue(player.scene, out Dictionary<int, List<int>> objectInstances) &&
                objectInstances.TryGetValue(player.instance, out List<int> objects))
            {
                for(int i=0; i < objects.Count; ++i)
                {
                    TrackedObjectData trackedObjectData = Server.objects[objects[i]];
                    if(trackedObjectData != null && (fromClient == -1 || trackedObjectData.controller == fromClient))
                    {
                        // If this is ours
                        if(trackedObjectData.controller == 0)
                        {
                            // Check if should send
                            if(GameManager.sceneLoading && trackedObjectData.scene.Equals(GameManager.scene))
                            {
                                // We don't want to send an object that is ours from the scene we are currently loading away from
                                // So just continue to next object
                                continue;
                            }

                            // If sending, make sure it init otherwise we might be missing data
                            trackedObjectData.Update();
                        }
                        ServerSend.TrackedObjectSpecific(trackedObjectData, ID);
                    }
                }
            }
        }

        public void Disconnect(int code, Exception ex = null)
        {
            connected = false;

            switch (code)
            {
                case 0:
                    Mod.LogInfo("Client "+ID+" : " + tcp.socket.Client.RemoteEndPoint + " disconnected, end of stream.", false);
                    break;
                case 1:
                    Mod.LogInfo("Client "+ID+" : " + tcp.socket.Client.RemoteEndPoint + " forcibly disconnected.", false);
                    Mod.LogWarning("Exception: " + ex.Message + "\n" + ex);
                    break;
                case 2:
                    Mod.LogInfo("Client "+ID+" : " + tcp.socket.Client.RemoteEndPoint + " disconnected.", false);
                    break;
            }

            tcp.Disconnect();
            udp.Disconnect();

            Mod.RemovePlayerFromLists(ID);
            GameManager.DistributeAllControl(ID);
            SpecificDisconnect();
            if (OnClientDisconnect != null)
            {
                OnClientDisconnect();
            }
            ServerSend.ClientDisconnect(ID);

            player = null;
        }

        private void SpecificDisconnect()
        {
            if (GameManager.TNHInstances.TryGetValue(player.instance, out TNHInstance TNHInstance) && TNHInstance.currentlyPlaying.Contains(ID)) // TNH_Manager was set to null and we are currently playing
            {
                TNHInstance.RemoveCurrentlyPlaying(true, ID, true);
            }
        }
    }
}