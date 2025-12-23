/* -*- Mode:C++; c-file-style:"gnu"; indent-tabs-mode:nil; -*- */
/*
 * Copyright (c) 2022 Emily Ekaireb
 *
 * This program is free software; you can redistribute it and/or modify
 * it under the terms of the GNU General Public License version 2 as
 * published by the Free Software Foundation;
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with this program; if not, write to the Free Software
 * Foundation, Inc., 59 Temple Place, Suite 330, Boston, MA  02111-1307  USA
 *
 * Author: Emily Ekaireb <eekaireb@ucsd.edu>
 */
#include "fl-experiment.h"
#include "fl-client-application.h"
#include "ns3/core-module.h"
#include "ns3/csma-module.h"
#include "ns3/applications-module.h"
#include "ns3/internet-module.h"
#include "fl-server-helper.h"
#include "ns3/reliability-helper.h"
#include "ns3/energy-module.h"
#include "ns3/internet-module.h"
#include "ns3/reliability-module.h"
#include "ns3/yans-error-rate-model.h"
#include "ns3/ssid.h"
#include "ns3/nix-vector-routing.h"
#include "ns3/nix-vector-helper.h"

// #include "ns3/olsr-helper.h"
// #include "ns3/aodv-module.h"

#include <iomanip> // For std::setprecision and std::fixed
#include <algorithm> // For std::shuffle
#include <random> // For std::default_random_engine
#include <chrono> // For std::chrono::system_clock

namespace ns3 {

// Example configuration function
void
ConfigureTCP ()
{
  // Use BBR congestion control algorithm
  // Config::SetDefault ("ns3::TcpL4Protocol::SocketType", StringValue ("ns3::TcpBbr"));

  // Increase TCP buffer sizes
  Config::SetDefault ("ns3::TcpSocket::RcvBufSize", UintegerValue (1048576)); // 1MB
  Config::SetDefault ("ns3::TcpSocket::SndBufSize", UintegerValue (1048576)); // 1MB

  // Enable TCP window scaling
  Config::SetDefault ("ns3::TcpSocketBase::WindowScaling", BooleanValue (true));

  // Optimize TCP segment size
  //Config::SetDefault ("ns3::TcpSocket::SegmentSize", UintegerValue (1440)); // Example MSS size

  // // Adjust Delayed ACKs
  // Config::SetDefault ("ns3::TcpSocket::DelAckCount", UintegerValue (1));
  // Increase initial congestion window
  Config::SetDefault ("ns3::TcpSocket::InitialCwnd", UintegerValue (10)); // 10 segments

  // Tune fast retransmit and recovery parameters
  // Config::SetDefault ("ns3::TcpSocketBase::MinRTO", TimeValue (MilliSeconds (200))); // Minimum RTO
  // Config::SetDefault ("ns3::TcpSocketBase::MinSSThresh", UintegerValue (2)); // Minimum ssthresh

  // Enable SACK
  // Config::SetDefault ("ns3::TcpSocketBase::Sack", BooleanValue (true));

  // Enable TCP timestamps
  // Config::SetDefault ("ns3::TcpSocket::Timestamps", BooleanValue (true));
}

double
GetDistanceFrom (const Vector &element, const Vector &from)
{
  double distance = std::sqrt (std::pow (element.x - from.x, 2) + std::pow (element.y - from.y, 2) +
                               std::pow (element.z - from.z, 2));
  return distance;
}

// Calback for monitoring
void
Monitor (std::string context, Ptr<const Packet> pkt, unsigned short channel, WifiTxVector txVector,
         MpduInfo mpdu, SignalNoiseDbm snr, uint16_t staId)
{
  NS_LOG_UNCOND(context << std::endl);
  NS_LOG_UNCOND("\tChannel: " << channel << "Tx: " << txVector.GetMode ()
            << "\tSignal= " << snr.signal << "\tNoise: " << snr.noise << std::endl);
}

void
Pause ()
{
  NS_LOG_UNCOND("Enter any key to continue..." << std::endl);
  std::cin.ignore ();
}

void
PrintNixRoutingTable (Ptr<Node> node)
{
  Ptr<Ipv4> ipv4 = node->GetObject<Ipv4> ();
  Ptr<Ipv4ListRouting> listRouting = DynamicCast<Ipv4ListRouting> (ipv4->GetRoutingProtocol ());
  if (listRouting)
    {
      int16_t priority;
      Ptr<Ipv4RoutingProtocol> routingProtocol = listRouting->GetRoutingProtocol (0, priority);
      Ptr<Ipv4StaticRouting> staticRouting = DynamicCast<Ipv4StaticRouting> (routingProtocol);
      if (staticRouting)
        {
          Ptr<OutputStreamWrapper> routingStream = Create<OutputStreamWrapper> (&std::cout);
          staticRouting->PrintRoutingTable (routingStream);
        }
    }
}

void
printRoutingTable (NodeContainer &allNodes)
{
  for (NodeContainer::Iterator i = allNodes.Begin (); i != allNodes.End (); ++i)
    {
      Ptr<Node> node = *i;
      NS_LOG_UNCOND("Routing table for node " << node->GetId () << std::endl);
      PrintNixRoutingTable (node);
    }
}

void
runPingExp (NodeContainer &allNodes, Ipv4InterfaceContainer &interfaces, int16_t udp_server_index)
{
  // Install Echo Server on the server node
  UdpEchoServerHelper echoServer (9999);
  ApplicationContainer serverApps;
  serverApps = echoServer.Install (allNodes.Get (udp_server_index));
  NS_LOG_UNCOND("Server Node: " << udp_server_index << std::endl);

  serverApps.Start (Seconds (2.0));
  serverApps.Stop (Seconds (1000000));
  // Install Echo Client on client nodes to ping the server
  for (uint16_t i = 0; i < allNodes.GetN (); ++i)
    {
      if (i != udp_server_index)
        {
          Ptr<Node> clientNode;
          clientNode = allNodes.Get (i);

          if (udp_server_index == 0)
            {
              InetSocketAddress serverAddress =
                  InetSocketAddress (interfaces.GetAddress (udp_server_index), 9999);
              UdpEchoClientHelper echoClient (serverAddress);
              echoClient.SetAttribute ("MaxPackets", UintegerValue (10));
              echoClient.SetAttribute ("Interval", TimeValue (Seconds (1.0)));
              echoClient.SetAttribute ("PacketSize", UintegerValue (1024));

              ApplicationContainer pingApps = echoClient.Install (clientNode);
              NS_LOG_UNCOND("Client Node: " << i << "send to " << serverAddress.GetIpv4 ()
                        << std::endl);

              pingApps.Start (Seconds (3.0));
              pingApps.Stop (Seconds (10));
            }
          else if (udp_server_index == 1)
            {
              if (i < 2)
                {
                  InetSocketAddress serverAddress =
                      InetSocketAddress (interfaces.GetAddress (udp_server_index), 9999);
                  UdpEchoClientHelper echoClient (serverAddress);
                  echoClient.SetAttribute ("MaxPackets", UintegerValue (10));
                  echoClient.SetAttribute ("Interval", TimeValue (Seconds (1.0)));
                  echoClient.SetAttribute ("PacketSize", UintegerValue (1024));

                  ApplicationContainer pingApps = echoClient.Install (clientNode);
                  NS_LOG_UNCOND("Client Node: " << i << "send to " << serverAddress.GetIpv4 ()
                            << std::endl);
                  pingApps.Start (Seconds (3.0));
                  pingApps.Stop (Seconds (10));
                }
              else
                {
                  InetSocketAddress serverAddress =
                      InetSocketAddress (interfaces.GetAddress (udp_server_index + 1), 9999);
                  UdpEchoClientHelper echoClient (serverAddress);
                  echoClient.SetAttribute ("MaxPackets", UintegerValue (10));
                  echoClient.SetAttribute ("Interval", TimeValue (Seconds (1.0)));
                  echoClient.SetAttribute ("PacketSize", UintegerValue (1024));

                  ApplicationContainer pingApps = echoClient.Install (clientNode);
                  NS_LOG_UNCOND("Client Node: " << i << "send to " << serverAddress.GetIpv4 ()
                            << std::endl);
                  pingApps.Start (Seconds (3.0));
                  pingApps.Stop (Seconds (10));
                }
            }
          else
            {
              InetSocketAddress serverAddress =
                  InetSocketAddress (interfaces.GetAddress (udp_server_index + 1), 9999);
              UdpEchoClientHelper echoClient (serverAddress);

              echoClient.SetAttribute ("MaxPackets", UintegerValue (10));
              echoClient.SetAttribute ("Interval", TimeValue (Seconds (1.0)));
              echoClient.SetAttribute ("PacketSize", UintegerValue (1024));

              ApplicationContainer pingApps = echoClient.Install (clientNode);
              NS_LOG_UNCOND("Client Node: " << i << "send to " << serverAddress.GetIpv4 ()
                        << std::endl);
              pingApps.Start (Seconds (3.0));
              pingApps.Stop (Seconds (10));
            }
        }
    }
}

Experiment::Experiment (int networkStandard, bool mobileClients, std::string &networkType,
                        int maxPacketSize, double txGain, std::string serverDatarate, double modelSize,
                        std::vector<double> serverCoords, int round)
    : m_networkStandard (networkStandard),
      m_mobileClients (mobileClients),
      m_networkType (networkType),
      m_maxPacketSize (maxPacketSize),
      m_txGain (txGain),
      m_serverDatarate (serverDatarate),
      m_modelSize (modelSize),
      m_serverCoordinates (serverCoords),
      m_routerCoordinates (2),
      m_round (round)
{
  // Assign values to m_routerCoordinates based on m_serverCoordinates
  m_routerCoordinates[0] = m_serverCoordinates[0] + 2;
  m_routerCoordinates[1] = m_serverCoordinates[1] - 2;
}

//>>>>>>>>>>>>>>>>>>>>>>>>>>>>> Helping Functions >>>>>>>>>>>>>>>>>>>>>>>>>>>>>>

void
Experiment::LogClientCharacteristics (NodeContainer allNodes, int clientIndex,
                                      const Vector &routerPosition)
{
  // Get the client's position
  Vector clientPosition = GetPosition (allNodes.Get (clientIndex + 2));
  // Calculate the distance from the server
  double distance = GetDistanceFrom (clientPosition, routerPosition);
  // Print the client's position and distance from the server
  NS_LOG_UNCOND ("Client " << clientIndex << " Position: (" << clientPosition.x << ", "
                                    << clientPosition.y << ", " << clientPosition.z
                                    << "), Distance from Router: " << distance << " meters");
}
void
Experiment::SchedulePrintEvent (double eventTime, std::string RoundStats)
{
  Simulator::Schedule (Seconds (eventTime), &Experiment::PrintEvent, this, RoundStats);
}

void
Experiment::PrintEvent (std::string RoundStats)
{
  NS_LOG_UNCOND("Print Event at " << Simulator::Now ().GetSeconds () << "s: " << RoundStats
            << std::endl);
}

void
Experiment::UpdateClientPositions (NodeContainer allNodes,
                                   std::map<int, std::shared_ptr<ClientSession>> &clientSessions)
{
  // Update client positions directly
  for (uint32_t j = 2; j < allNodes.GetN (); ++j)
    {
      Vector pos = GetPosition (allNodes.Get (j));
      clientSessions[j - 2]->SetX (pos.x);
      clientSessions[j - 2]->SetY (pos.y);
    }
}

void
Experiment::PrintClientPositions (NodeContainer allNodes,
                                  std::map<int, std::shared_ptr<ClientSession>> &clientSessions)
{
  Vector routerPosition = GetPosition (allNodes.Get (1));

  for (const auto &clientPair : clientSessions)
    {
      int clientId = clientPair.first;
      LogClientCharacteristics (allNodes, clientId, routerPosition);
    }
}

//>>>>>>>>>>>>>>>>>>>>>>>>>>>>> Experiment Round Run >>>>>>>>>>>>>>>>>>>>>>>>>>>>>>

std::map<int, Experiment::RoundStats>
Experiment::ExecRoundCom (std::map<int, std::shared_ptr<ClientSession>> &clientSessions,
                          int monitorSteps, int monitorMsgSize)
{
  NS_LOG_UNCOND ("================================ Experiment Round Setup "
                 "===================================");
  double server_startTime = 3.0;
  double stopTime;

  // if (clientSessions.size () <= 10)
  //   stopTime = 300.0;
  // else if (clientSessions.size () > 10 && clientSessions.size () >= 15)
  //   stopTime = 600.0;
  // else
  stopTime = 1000;

  double clientStartTime = 5.0;
  uint16_t serverPort = 80;
  ConfigureTCP ();

  // 1.Devices Setup
  NodeContainer allNodes;
  // take into account server and router and the rest of the clients
  allNodes.Create (clientSessions.size () + 2);
  NetDeviceContainer devices = SetupDevices (allNodes, clientSessions, clientStartTime);

  // 2.Internet Stack Setup
  Ipv4InterfaceContainer interfaces = SetupInternetStack (allNodes, devices);

  // Ipv4GlobalRoutingHelper::PopulateRoutingTables ();
  // 3.Server Setups
  Ptr<Server> serverApp = SetupServer (allNodes.Get (0), serverPort, interfaces, monitorMsgSize,
                                       monitorSteps, server_startTime, stopTime);

  // 4.Clients Setup
  std::map<Ipv4Address, int> addrToIdMap;
  ApplicationContainer clientApps =
      SetupClients (allNodes, clientSessions, serverPort, interfaces, addrToIdMap,
                    monitorMsgSize, clientStartTime, stopTime);

  // 5.Client Session Manager Setup
  ClientSessionManager client_session_manager (clientSessions);
  serverApp->GetObject<ns3::Server> ()->SetClientSessionManager (&client_session_manager, m_round);
  // 6. Netanim Interface Setup
  // AnimationInterface anim = ConfigureAnimation (allNodes, clientStartTime);
  NS_LOG_UNCOND (
      "================================ Round Setup Complete ===================================");

  // 7.Run Simulation
  NS_LOG_UNCOND (
      "============================= Starting Ns3 Round Simulation =============================");
  PrintClientPositions (allNodes, clientSessions);
  Simulator::Stop (Seconds (stopTime));
  Simulator::Run ();
  // 8.Extract Simulation Results
  std::map<Ipv4Address, RoundStats> stats;
  ExtractDownlinkResults (clientSessions, serverPort, interfaces, stats);
  ExtractUplinkResults (serverApp, serverPort, interfaces, stats);

  // Todo1: add mnitor message time in results
  // Todo2: Handle logic for useFda or not!
  // Process Results
  std::map<int, RoundStats> roundStats = ProcessResults (allNodes, addrToIdMap, stats);

  UpdateClientPositions (allNodes, clientSessions);
  Simulator::Destroy ();
  return roundStats;
}

//>>>>>>>>>>>>>>>>>>>>>>>>>>>> \Experiment Round Run >>>>>>>>>>>>>>>>>>>>>>>>>>>>>>
//>>>>>>>>>>>>>>>>>>>>>>>>>>>>>> Positioning Helpers >>>>>>>>>>>>>>>>>>>>>>>>>>>>>>
void
Experiment::SetPositionPolar (Ptr<Node> node, double radius, double theta)
{
  double x = radius * cos (theta);
  double y = radius * sin (theta);
  double z = 0;
  Ptr<MobilityModel> mobility = node->GetObject<MobilityModel> ();
  mobility->SetPosition (Vector (x, y, z));
}

void
Experiment::SetPositionCartesian (Ptr<Node> node, double x, double y)
{
  double z = 0;
  Ptr<MobilityModel> mobility = node->GetObject<MobilityModel> ();
  mobility->SetPosition (Vector (x, y, z));
}

Vector
Experiment::GetPosition (Ptr<Node> node)
{
  Ptr<MobilityModel> mobility = node->GetObject<MobilityModel> ();
  return mobility->GetPosition ();
}
//>>>>>>>>>>>>>>>>>>>>>>>>>>>>>> \Positioning Helpers >>>>>>>>>>>>>>>>>>>>>>>>>>>>>

//>>>>>>>>>>>>>>>>>>>>>>>>>>>>> Network Setup Helpers >>>>>>>>>>>>>>>>>>>>>>>>>>>>>

void
Experiment::SetupMobilityForStaNodes (NodeContainer &staNodes,
                                      const std::vector<double> &routerCoords, double radius,
                                      double startTime)
{

  // Schedule the installation of the desired mobility model at the simulation start time
  if (m_mobileClients)
    {
      InstallRandomWalkMobility (staNodes, routerCoords, radius);
    }
  else
    {
      // Initially set a constant position for all nodes
      MobilityHelper constantMobility;
      constantMobility.SetMobilityModel ("ns3::ConstantPositionMobilityModel");
      constantMobility.Install (staNodes);
    }
}

void
Experiment::InstallRandomWalkMobility (NodeContainer &staNodes,
                                       const std::vector<double> &routerCoords, double radius)
{
  MobilityHelper mobility;
  mobility.SetMobilityModel (
      "ns3::RandomWalk2dMobilityModel", "Bounds",
      RectangleValue (Rectangle (routerCoords[0] - radius, routerCoords[0] + radius,
                                 routerCoords[1] - radius, routerCoords[1] + radius)),
      "Distance", DoubleValue (1.0), // Maximum distance the node can move in one step
      "Direction",
      StringValue ("ns3::UniformRandomVariable[Min=0.0|Max=6.2830]"), // Random direction in radians
      "Speed", StringValue ("ns3::ConstantRandomVariable[Constant=1.0]") // Constant speed
  );
  mobility.Install (staNodes);
}

// Network Setup Helpers
NetDeviceContainer
Experiment::MyWifi (NodeContainer &allNodes,
                    std::map<int, std::shared_ptr<ClientSession>> &clientSessions,
                    double clientStartTime)
{

  // Split the nodes into Server, AP, and Clients
  NodeContainer csmaNodes;
  NodeContainer serverNode;

  NodeContainer apNode;
  NodeContainer staNodes;

  NetDeviceContainer allDevices;

  serverNode.Add (allNodes.Get (0)); // First node as Server
  apNode.Add (allNodes.Get (1)); // Second node as AP
  csmaNodes.Add (serverNode);
  csmaNodes.Add (apNode);

  for (uint32_t i = 2; i < allNodes.GetN (); ++i)
    {
      staNodes.Add (allNodes.Get (i)); // Remaining nodes as Clients
    }

  // First The Ethernet connection between the server and the clientSessions
  // Setup Ethernet connection for the server and the router
  CsmaHelper csma;
  csma.SetChannelAttribute ("DataRate", StringValue (m_serverDatarate));
  csma.SetChannelAttribute ("Delay", TimeValue (NanoSeconds (6560)));
  NetDeviceContainer csmaDevices = csma.Install (csmaNodes);
  // Store all the Devices created
  allDevices.Add (csmaDevices);

  // Now The wifi Devices of the clientSessions and their AP's

  // 0. Define Helpers
  YansWifiChannelHelper wifiChannel = YansWifiChannelHelper::Default ();
  YansWifiPhyHelper wifiPhy;
  WifiHelper wifi;
  WifiMacHelper wifiMac;

  // 1. Setup Channel
  // wifiChannel.AddPropagationLoss ("ns3::LogDistancePropagationLossModel", "Exponent",
  //                                 DoubleValue (3.0));
  // wifiChannel.AddPropagationLoss ("ns3::NakagamiPropagationLossModel");
  wifiChannel.SetPropagationDelay ("ns3::ConstantSpeedPropagationDelayModel");

  // 2. Setup Physical Layer
  wifiPhy.SetErrorRateModel ("ns3::YansErrorRateModel");
  wifiPhy.SetChannel (wifiChannel.Create ());
  // wifiPhy.Set ("TxGain", DoubleValue (m_txGain));
  // wifiPhy.Set ("RxGain", DoubleValue (0));
  wifiPhy.Set ("TxPowerStart", DoubleValue (20.0)); // Example setting for min power
  wifiPhy.Set ("TxPowerEnd", DoubleValue (20.0)); // Example setting for max power

  // std::string phyMode ("HtMcs2");
  // Config::SetDefault ("ns3::WifiRemoteStationManager::NonUnicastMode", StringValue (phyMode));

  // 3. Setup Wifi for client connectivity
  wifi.SetStandard (WIFI_STANDARD_80211a);
  // wifi.SetRemoteStationManager ("ns3::MinstrelHtWifiManager");
  wifi.SetRemoteStationManager ("ns3::AarfWifiManager");

  // 4. Setup Mac for AP
  Ssid ssid = Ssid ("home-wifi-ssid");
  wifiMac.SetType ("ns3::ApWifiMac", "Ssid", SsidValue (ssid));
  NetDeviceContainer apDevices = wifi.Install (wifiPhy, wifiMac, apNode);

  // 4. Setup Mac for STA
  wifiMac.SetType ("ns3::StaWifiMac", "Ssid", SsidValue (ssid), "ActiveProbing",
                   BooleanValue (false));
  NetDeviceContainer staDevices = wifi.Install (wifiPhy, wifiMac, staNodes);

  allDevices.Add (apDevices);
  allDevices.Add (staDevices);

  MobilityHelper mobility;

  // Setup Mobility
  mobility.SetMobilityModel ("ns3::ConstantPositionMobilityModel");
  mobility.Install (apNode);
  mobility.Install (serverNode);

  // If mobile clientSessions
  SetupMobilityForStaNodes (staNodes, m_routerCoordinates, MAX_DISTANCE_FROM_ROUTER,
                            clientStartTime);
  ;

  // Setup Devices positions
  SetPositionCartesian (allNodes.Get (0), m_serverCoordinates[0], m_serverCoordinates[1]);
  // assume they are next to each other in the lab
  SetPositionCartesian (allNodes.Get (1), m_routerCoordinates[0], m_routerCoordinates[1]);

  for (uint32_t j = 2; j < allNodes.GetN (); ++j)
    {
      SetPositionCartesian (allNodes.Get (j), clientSessions[j - 2]->GetX (),
                            clientSessions[j - 2]->GetY ());
    }

  return allDevices;
}

NetDeviceContainer
Experiment::WeakWifi (NodeContainer &allNodes,
                      std::map<int, std::shared_ptr<ClientSession>> &clientSessions,
                      double clientStartTime)
{
  NodeContainer csmaNodes;
  NodeContainer serverNode;
  NodeContainer apNode;
  NodeContainer staNodes;
  NetDeviceContainer allDevices;

  serverNode.Add (allNodes.Get (0)); // First node as Server
  apNode.Add (allNodes.Get (1)); // Second node as AP
  csmaNodes.Add (serverNode);
  csmaNodes.Add (apNode);

  for (uint32_t i = 2; i < allNodes.GetN (); ++i)
    {
      staNodes.Add (allNodes.Get (i)); // Remaining nodes as Clients
    }

  // Setup Ethernet connection for the server and the router
  CsmaHelper csma;
  csma.SetChannelAttribute ("DataRate", StringValue (m_serverDatarate));
  csma.SetChannelAttribute ("Delay", TimeValue (NanoSeconds (6560)));
  NetDeviceContainer csmaDevices = csma.Install (csmaNodes);
  allDevices.Add (csmaDevices);

  // Setup Wi-Fi devices
  YansWifiChannelHelper wifiChannel = YansWifiChannelHelper::Default ();
  YansWifiPhyHelper wifiPhy;
  WifiHelper wifi;
  WifiMacHelper wifiMac;

  // Setup Channel
  wifiChannel.SetPropagationDelay ("ns3::ConstantSpeedPropagationDelayModel");
  // wifiChannel.AddPropagationLoss ("ns3::LogDistancePropagationLossModel", "Exponent",
  //                                 DoubleValue (2.0));

  // Setup Physical Layer
  wifiPhy.SetChannel (wifiChannel.Create ());
  wifiPhy.Set ("TxPowerStart", DoubleValue (11.0)); // Lower transmit power
  wifiPhy.Set ("TxPowerEnd", DoubleValue (11.0)); // Lower transmit power
  wifiPhy.SetErrorRateModel ("ns3::YansErrorRateModel");

  // Setup Wi-Fi for client connectivity
  wifi.SetStandard (WIFI_STANDARD_80211g);
  wifi.SetRemoteStationManager ("ns3::AarfWifiManager");

  // Setup Mac for AP
  Ssid ssid = Ssid ("weak-wifi-ssid");
  wifiMac.SetType ("ns3::ApWifiMac", "Ssid", SsidValue (ssid));
  NetDeviceContainer apDevices = wifi.Install (wifiPhy, wifiMac, apNode);

  // Setup Mac for STA
  wifiMac.SetType ("ns3::StaWifiMac", "Ssid", SsidValue (ssid), "ActiveProbing",
                   BooleanValue (false));
  NetDeviceContainer staDevices = wifi.Install (wifiPhy, wifiMac, staNodes);

  allDevices.Add (apDevices);
  allDevices.Add (staDevices);

  MobilityHelper mobility;

  // Setup Mobility
  mobility.SetMobilityModel ("ns3::ConstantPositionMobilityModel");
  mobility.Install (apNode);
  mobility.Install (serverNode);

  // If mobile clientSessions

  SetupMobilityForStaNodes (staNodes, m_routerCoordinates, MAX_DISTANCE_FROM_ROUTER,
                            clientStartTime);
  ;

  // Setup Devices positions
  SetPositionCartesian (allNodes.Get (0), m_serverCoordinates[0], m_serverCoordinates[1]);
  SetPositionCartesian (allNodes.Get (1), m_routerCoordinates[0], m_routerCoordinates[1]);
  for (uint32_t j = 2; j < allNodes.GetN (); ++j)
    {
      SetPositionCartesian (allNodes.Get (j), clientSessions[j - 2]->GetX (),
                            clientSessions[j - 2]->GetY ());
    }

  return allDevices;
}

NetDeviceContainer
Experiment::MidWifi (NodeContainer &allNodes,
                     std::map<int, std::shared_ptr<ClientSession>> &clientSessions,
                     double clientStartTime)
{
  NodeContainer csmaNodes;
  NodeContainer serverNode;
  NodeContainer apNode;
  NodeContainer staNodes;
  NetDeviceContainer allDevices;

  serverNode.Add (allNodes.Get (0)); // First node as Server
  apNode.Add (allNodes.Get (1)); // Second node as AP
  csmaNodes.Add (serverNode);
  csmaNodes.Add (apNode);

  for (uint32_t i = 2; i < allNodes.GetN (); ++i)
    {
      staNodes.Add (allNodes.Get (i)); // Remaining nodes as Clients
    }

  // Setup Ethernet connection for the server and the router
  CsmaHelper csma;
  csma.SetChannelAttribute ("DataRate", StringValue (m_serverDatarate));
  csma.SetChannelAttribute ("Delay", TimeValue (NanoSeconds (6560)));
  NetDeviceContainer csmaDevices = csma.Install (csmaNodes);
  allDevices.Add (csmaDevices);

  // Setup Wi-Fi devices
  YansWifiChannelHelper wifiChannel = YansWifiChannelHelper::Default ();
  YansWifiPhyHelper wifiPhy;
  WifiHelper wifi;
  WifiMacHelper wifiMac;

  // Setup Channel
  wifiChannel.SetPropagationDelay ("ns3::ConstantSpeedPropagationDelayModel");
  // wifiChannel.AddPropagationLoss ("ns3::LogDistancePropagationLossModel", "Exponent",
  //                                 DoubleValue (3.0));

  // Setup Physical Layer
  wifiPhy.SetChannel (wifiChannel.Create ());
  wifiPhy.Set ("TxPowerStart", DoubleValue (16.0)); // Moderate transmit power
  wifiPhy.Set ("TxPowerEnd", DoubleValue (16.0)); // Moderate transmit power
  wifiPhy.SetErrorRateModel ("ns3::YansErrorRateModel");

  // Setup Wi-Fi for client connectivity
  wifi.SetStandard (WIFI_STANDARD_80211n_2_4GHZ);
  wifi.SetRemoteStationManager ("ns3::MinstrelHtWifiManager");

  // Setup Mac for AP
  Ssid ssid = Ssid ("mid-wifi-ssid");
  wifiMac.SetType ("ns3::ApWifiMac", "Ssid", SsidValue (ssid));
  NetDeviceContainer apDevices = wifi.Install (wifiPhy, wifiMac, apNode);

  // Setup Mac for STA
  wifiMac.SetType ("ns3::StaWifiMac", "Ssid", SsidValue (ssid), "ActiveProbing",
                   BooleanValue (false));
  NetDeviceContainer staDevices = wifi.Install (wifiPhy, wifiMac, staNodes);

  allDevices.Add (apDevices);
  allDevices.Add (staDevices);

  MobilityHelper mobility;

  // Setup Mobility
  mobility.SetMobilityModel ("ns3::ConstantPositionMobilityModel");
  mobility.Install (apNode);
  mobility.Install (serverNode);

  // If mobile clientSessions

  SetupMobilityForStaNodes (staNodes, m_routerCoordinates, MAX_DISTANCE_FROM_ROUTER,
                            clientStartTime);
  ;
  // Setup Devices positions
  SetPositionCartesian (allNodes.Get (0), m_serverCoordinates[0], m_serverCoordinates[1]);
  SetPositionCartesian (allNodes.Get (1), m_routerCoordinates[0], m_routerCoordinates[1]);

  for (uint32_t j = 2; j < allNodes.GetN (); ++j)
    {
      SetPositionCartesian (allNodes.Get (j), clientSessions[j - 2]->GetX (),
                            clientSessions[j - 2]->GetY ());
    }

  return allDevices;
}

NetDeviceContainer
Experiment::FastWifi (NodeContainer &allNodes,
                      std::map<int, std::shared_ptr<ClientSession>> &clientSessions,
                      double clientStartTime)
{
  NodeContainer csmaNodes;
  NodeContainer serverNode;
  NodeContainer apNode;
  NodeContainer staNodes;
  NetDeviceContainer allDevices;

  serverNode.Add (allNodes.Get (0)); // First node as Server
  apNode.Add (allNodes.Get (1)); // Second node as AP
  csmaNodes.Add (serverNode);
  csmaNodes.Add (apNode);

  for (uint32_t i = 2; i < allNodes.GetN (); ++i)
    {
      staNodes.Add (allNodes.Get (i)); // Remaining nodes as Clients
    }

  // Setup Ethernet connection for the server and the router
  CsmaHelper csma;
  csma.SetChannelAttribute ("DataRate", StringValue (m_serverDatarate));
  csma.SetChannelAttribute ("Delay", TimeValue (NanoSeconds (6560)));
  NetDeviceContainer csmaDevices = csma.Install (csmaNodes);
  allDevices.Add (csmaDevices);

  // Setup Wi-Fi devices
  YansWifiChannelHelper wifiChannel = YansWifiChannelHelper::Default ();
  YansWifiPhyHelper wifiPhy;
  WifiHelper wifi;
  WifiMacHelper wifiMac;

  // Setup Channel
  wifiChannel.SetPropagationDelay ("ns3::ConstantSpeedPropagationDelayModel");
  // wifiChannel.AddPropagationLoss ("ns3::LogDistancePropagationLossModel", "Exponent",
  //                                 DoubleValue (2.0));

  // Setup Physical Layer
  wifiPhy.SetChannel (wifiChannel.Create ());
  wifiPhy.Set ("TxPowerStart", DoubleValue (19.0)); // High transmit power
  wifiPhy.Set ("TxPowerEnd", DoubleValue (19.0)); // High transmit power
  wifiPhy.SetErrorRateModel ("ns3::YansErrorRateModel");

  // Setup Wi-Fi for client connectivity
  wifi.SetStandard (WIFI_STANDARD_80211ax_5GHZ);
  wifi.SetRemoteStationManager ("ns3::MinstrelHtWifiManager");

  // Setup Mac for AP
  Ssid ssid = Ssid ("fast-wifi-ssid");
  wifiMac.SetType ("ns3::ApWifiMac", "Ssid", SsidValue (ssid));
  NetDeviceContainer apDevices = wifi.Install (wifiPhy, wifiMac, apNode);

  // Setup Mac for STA
  wifiMac.SetType ("ns3::StaWifiMac", "Ssid", SsidValue (ssid), "ActiveProbing",
                   BooleanValue (false));
  NetDeviceContainer staDevices = wifi.Install (wifiPhy, wifiMac, staNodes);

  allDevices.Add (apDevices);
  allDevices.Add (staDevices);
  // Setup Mobility
  MobilityHelper mobility;

  // Setup Mobility
  mobility.SetMobilityModel ("ns3::ConstantPositionMobilityModel");
  mobility.Install (apNode);
  mobility.Install (serverNode);

  // If mobile clientSessions
  SetupMobilityForStaNodes (staNodes, m_routerCoordinates, MAX_DISTANCE_FROM_ROUTER,
                            clientStartTime);
  ;
  // Setup Devices positions
  SetPositionCartesian (allNodes.Get (0), m_serverCoordinates[0], m_serverCoordinates[1]);
  SetPositionCartesian (allNodes.Get (1), m_routerCoordinates[0], m_routerCoordinates[1]);

  for (uint32_t j = 2; j < allNodes.GetN (); ++j)
    {
      SetPositionCartesian (allNodes.Get (j), clientSessions[j - 2]->GetX (),
                            clientSessions[j - 2]->GetY ());
    }

  return allDevices;
  return allDevices;
}

NetDeviceContainer
Experiment::Ethernet (NodeContainer &allNodes,
                      std::map<int, std::shared_ptr<ClientSession>> &clientSessions)
{
  CsmaHelper csma;
  csma.SetChannelAttribute ("DataRate", StringValue ("100Mbps"));
  csma.SetChannelAttribute ("Delay", TimeValue (NanoSeconds (6560)));

  NetDeviceContainer csmaDevices;
  csmaDevices = csma.Install (allNodes);

  return csmaDevices;
}
//>>>>>>>>>>>>>>>>>>>>>>>>>>>> \Network Setup Helpers >>>>>>>>>>>>>>>>>>>>>>>>>>>>>

//>>>>>>>>>>>>>>>>>>>>>>>>>>>>>> Experiment Helpers >>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>
NetDeviceContainer
Experiment::SetupDevices (NodeContainer &allNodes,
                          std::map<int, std::shared_ptr<ClientSession>> &clientSessions,
                          double clientStartTime)
{

  // Default Network Topology
  //
  //           WIFI 192.168.3.0
  //        AP
  //        *       *    *    *
  // srvr   |       |    |    |
  //  no   n1      n2   n3   n4
  //   |    |
  //   =======
  // CSMA 192.168.1.0
  //
  if (m_networkType.compare ("wifi") == 0)
    {
      if (m_networkStandard == 0) // Slow speed network
        {
          NS_LOG_UNCOND ("Weak Wifi Setup");
          return WeakWifi (allNodes, clientSessions, clientStartTime);
        }
      else if (m_networkStandard == 1) // Mid speed network
        {
          NS_LOG_UNCOND ("Mid Wifi Setup");
          return MidWifi (allNodes, clientSessions, clientStartTime);
        }
      else
        {
          NS_LOG_UNCOND ("Fast Wifi Setup"); // High speed network
          return FastWifi (allNodes, clientSessions, clientStartTime);
        }
    }
  else
    { // assume ethernet if not specified
      return Ethernet (allNodes, clientSessions);
    }
}

Ipv4InterfaceContainer
Experiment::SetupInternetStack (NodeContainer &allNodes, NetDeviceContainer &allDevices)
{

  Ipv4NixVectorHelper nixRouting;
  InternetStackHelper internet;
  internet.SetRoutingHelper (nixRouting); // has effect on the next Install ()
  internet.Install (allNodes);

  Ipv4AddressHelper ipv4;
  NetDeviceContainer csmaDevices;
  NetDeviceContainer wifiDevices;

  // Assuming the first 2 devices are CSMA and the rest are WiFi
  uint32_t nDevices = allDevices.GetN ();
  for (uint32_t i = 0; i < 2; ++i)
    {
      csmaDevices.Add (allDevices.Get (i));
    }
  for (uint32_t i = 2; i < nDevices; ++i)
    {
      wifiDevices.Add (allDevices.Get (i));
    }

  Ipv4InterfaceContainer csmaInterfaces;
  Ipv4InterfaceContainer wifiInterfaces;

  // ethernet devices
  ipv4.SetBase ("192.168.1.0", "255.255.255.0");
  csmaInterfaces = ipv4.Assign (csmaDevices);

  // wifi devices
  ipv4.SetBase ("192.168.3.0", "255.255.255.0");
  wifiInterfaces = ipv4.Assign (wifiDevices);

  // Combine interfaces
  Ipv4InterfaceContainer interfaces;
  interfaces.Add (csmaInterfaces);
  interfaces.Add (wifiInterfaces);

  // for (uint32_t i = 0; i < interfaces.GetN (); ++i)
  //   {
  //     NS_LOG_UNCOND ("IP: " << interfaces.GetAddress (i));
  //   }

  return interfaces;
}

Ptr<Server>
Experiment::SetupServer (Ptr<Node> server, int16_t serverPort, Ipv4InterfaceContainer &interfaces,
                         int monitorMsgSize, int monitorSteps,
                         double startTime, double stopTime)
{

  NS_LOG_UNCOND ("Setup Server Address: " << interfaces.GetAddress (0) << ":" << serverPort);

  ServerHelper server_helper ("ns3::TcpSocketFactory",
                              InetSocketAddress (interfaces.GetAddress (0), serverPort));
  server_helper.SetAttribute ("MaxPacketSize", UintegerValue (m_maxPacketSize));
  server_helper.SetAttribute ("BytesModel", UintegerValue (m_modelSize));
  server_helper.SetAttribute ("DataRate", StringValue (m_serverDatarate));
  server_helper.SetAttribute ("MonitorMsgSize", UintegerValue (monitorMsgSize));
  server_helper.SetAttribute ("MonitorSteps", UintegerValue (monitorSteps));

  ApplicationContainer sinkApps = server_helper.Install (server);

  sinkApps.Start (Seconds (startTime));
  sinkApps.Stop (Seconds (stopTime));

  return DynamicCast<Server> (sinkApps.Get (0));
}

ApplicationContainer
Experiment::SetupClients (NodeContainer &allNodes,
                          std::map<int, std::shared_ptr<ClientSession>> &clientSessions,
                          int16_t serverPort, Ipv4InterfaceContainer &interfaces,
                          std::map<Ipv4Address, int> &addrToIdMap,
                          int monitorMsgSize, double startTime, double stopTime)
{
  ApplicationContainer clientApps;
  Ipv4Address serverAddress = interfaces.GetAddress (0);
  Address peerAddress (InetSocketAddress (serverAddress, serverPort));

  // NS_LOG_UNCOND ("Setup Client, Server Address: " << interfaces.GetAddress (0) << ":"
  //                                                 << serverPort);
  // Get the server position
  // Vector routerPosition = GetPosition (allNodes.Get (1));
  for (uint32_t j = 2; j < allNodes.GetN (); ++j)
    {
      if (clientSessions[j - 2]->GetInRound ())
        {

          Ptr<Node> clientNode = allNodes.Get (j);

          // Create the client application and set its attributes
          Ptr<ClientApplication> clientApp = CreateObject<ClientApplication> ();
          Ptr<Socket> socket = Socket::CreateSocket (clientNode, TcpSocketFactory::GetTypeId ());
          socket->SetAttribute ("ConnCount", UintegerValue (5000));
          socket->SetAttribute ("DataRetries", UintegerValue (50));

          Ipv4Address clientAddress = interfaces.GetAddress (j + 1);
          addrToIdMap[clientAddress] = j - 2;

          // std::cout << "Address " << clientAddress << " And Id " << j - 2;
          clientApp->Setup (socket, peerAddress, m_maxPacketSize, m_modelSize, monitorMsgSize,
                            clientSessions[j - 2]->GetLocalSteps ());
          clientApp->SetStartTime (Seconds (startTime));
          clientApp->SetStopTime (Seconds (stopTime));

          clientNode->AddApplication (clientApp);
          clientSessions[j - 2]->SetClient (socket);
          clientSessions[j - 2]->SetCycle (0);
          clientApps.Add (clientApp); // Add this app to the container to be returned
        }
    }
  return clientApps;
}

AnimationInterface
Experiment::ConfigureAnimation (NodeContainer &allNodes, double startTime)
{
  AnimationInterface anim ("animation.xml");
  // Set the background image with a scale that includes all nodes
  anim.SetBackgroundImage ("fl_app/campus.jpg", 0, 0, .165, .145, 0.9);
  anim.EnablePacketMetadata (true); // Optional: Depends on your ns-3 build configuration
  if (m_mobileClients)
    anim.SetMobilityPollInterval (Seconds (1)); // Not moving so no need to update
  else
    anim.SetMobilityPollInterval (Seconds (100000)); // Not moving so no need to update

  anim.SetStartTime (Seconds (startTime));

  // Set the positions for the server and client nodes and configure their properties
  for (uint32_t j = 0; j < allNodes.GetN (); ++j)
    {
      if (j == 0) // The first node is the server
        {
          // Set the server properties
          anim.UpdateNodeDescription (allNodes.Get (j), "Server");
          anim.UpdateNodeColor (allNodes.Get (j), 0, 255, 0); // Green color for the server
          anim.UpdateNodeSize (j, 4.0, 4.0); // Make the server node larger
        }
      else if (j == 1) // The second node is the router
        {
          anim.UpdateNodeDescription (allNodes.Get (j), "Router");
          anim.UpdateNodeColor (allNodes.Get (j), 0, 0, 255); // Blue color for the router
          anim.UpdateNodeSize (j, 2.0, 2.0); // Make the router node medium size
        }
      else
        {
          // Set the client properties
          anim.UpdateNodeDescription (allNodes.Get (j), "Client " + std::to_string (j - 1));
          anim.UpdateNodeColor (allNodes.Get (j), 255, 0,
                                0); // Red color for the clientSessions
          anim.UpdateNodeSize (j, 3.0, 3.0); // Standard size for client nodes
        }
    }

  return anim;
}

void
Experiment::ExtractDownlinkResults (std::map<int, std::shared_ptr<ClientSession>> &clientSessions,
                                    int16_t serverPort, Ipv4InterfaceContainer &interfaces,
                                    std::map<Ipv4Address, RoundStats> &stats)
{
  int numClients = clientSessions.size ();
  // interfaces 0-> server, 1-> AP:ethernet, 2-> AP: WiFi
  for (int j = 2; j < numClients + 2; j++)
    {
      if (clientSessions[j - 2]->GetInRound ())
        {
          auto app = clientSessions[j - 2]->GetClient ()->GetNode ()->GetApplication (0);
          UintegerValue sent;
          UintegerValue rec;
          TimeValue beginConnection;
          TimeValue endConnection;
          TimeValue beginDownLink;
          TimeValue endDownLink;
          TimeValue beginMonitoring;
          TimeValue endMonitoring;
          Ipv4Address clientAddress;
          app->GetAttribute ("BytesSent", sent);
          app->GetAttribute ("BytesReceived", rec);
          app->GetAttribute ("BeginConnection", beginConnection);
          app->GetAttribute ("EndConnection", endConnection);
          app->GetAttribute ("BeginDownlink", beginDownLink);
          app->GetAttribute ("EndDownlink", endDownLink);
          app->GetAttribute ("BeginMonitoring", beginMonitoring);
          app->GetAttribute ("EndMonitoring", endMonitoring);

          clientAddress = InetSocketAddress::ConvertFrom (
                              InetSocketAddress (interfaces.GetAddress (j + 1), serverPort))
                              .GetIpv4 ();
          stats[clientAddress].downlinkTime =
              (endDownLink.Get () - beginDownLink.Get ()).GetDouble () / 1000000000.0;
          stats[clientAddress].connectionTime =
              (endConnection.Get () - beginConnection.Get ()).GetDouble () / 1000000000.0;
          stats[clientAddress].monitoringTime =
              (endMonitoring.Get () - beginMonitoring.Get ()).GetDouble () / 1000000000.0;
          NS_LOG_UNCOND ("[CLIENT]  "
                         << interfaces.GetAddress (0, 0) << " -> " << clientAddress << std::endl
                         << "  Recv=" << rec.Get () << " bytes" << std::endl
                         << "  Sent=" << sent.Get () << " bytes" << std::endl
                         << "  Begin downlink=" << beginDownLink.Get ().As (Time::S) << std::endl
                         << "  End downlink=" << endDownLink.Get ().As (Time::S) << std::endl
                         << "  Total Stats Analysis: " << std::endl
                         << "    Connection establishment duration="
                         << stats[clientAddress].connectionTime << std::endl
                         << "    Downlink duration=" << stats[clientAddress].downlinkTime
                         << std::endl
                         << "    monitoring duration=" << stats[clientAddress].monitoringTime
                         << std::endl);
        }
    }
}
void
Experiment::ExtractUplinkResults (Ptr<Server> server, int16_t serverPort,
                                  Ipv4InterfaceContainer &interfaces,
                                  std::map<Ipv4Address, RoundStats> &stats)
{
  auto sk = server->GetAcceptedSockets ();
  for (auto itr = sk.begin (); itr != sk.end (); itr++)
    {
      auto beginUplink = itr->second->m_timeBeginReceivingModelFromClient;
      auto endUplink = itr->second->m_timeEndReceivingModelFromClient;
      auto clientAddress = InetSocketAddress::ConvertFrom (itr->second->m_address).GetIpv4 ();
      auto serverAddress = interfaces.GetAddress (0, 0);

      NS_LOG_UNCOND (
          "[SERVER]  " << clientAddress << " -> " << serverAddress << std::endl
                       << "  Sent=     " << itr->second->m_bytesModelSent << " bytes" << std::endl
                       << "  Recv=     " << itr->second->m_bytesModelReceived << " bytes"
                       << std::endl
                       << "  Begin uplink=" << beginUplink.As (Time::S) << std::endl
                       << "  End uplink=" << endUplink.As (Time::S) << std::endl
                       << "  Uplink duration=" << (endUplink - beginUplink).As (Time::S));

      stats[clientAddress].uplinkTime = (endUplink - beginUplink).GetDouble () / 1000000000.0;
      // To Mbps
      stats[clientAddress].throughput =
          itr->second->m_bytesModelReceived * 8.0 / 1000000.0 /
          ((endUplink.GetDouble () - beginUplink.GetDouble ()) / 1000000000.0);
    }
}

// Method to combine and process the extracted results
std::map<int, Experiment::RoundStats>
Experiment::ProcessResults (NodeContainer &allNodes, std::map<Ipv4Address, int> addrToIdMap,
                            std::map<Ipv4Address, RoundStats> &stats)
{
  std::map<int, Experiment::RoundStats> roundStats;
  for (auto itr : stats)
    {

      int id = addrToIdMap[itr.first];
      // std::cout << "Address " << itr.first << " And Id " << id;
      NS_LOG_UNCOND ("ID " << id << "  ,ADDRESS: " << itr.first
                           << "| ConnectionTime= " << std::fixed << std::setprecision (2)
                           << itr.second.connectionTime << "s"
                           << "| DownlinkTime= " << std::fixed << std::setprecision (2)
                           << itr.second.downlinkTime << "s"
                           << "| MonitoringTime= " << std::fixed << std::setprecision (2)
                           << itr.second.monitoringTime << "s"
                           << "| UplinkTime= " << std::fixed << std::setprecision (2)
                           << itr.second.uplinkTime << "s"
                           << "| Round Throughput= " << std::fixed << std::setprecision (2)
                           << itr.second.throughput << "Mbps");

      roundStats[id].throughput = itr.second.throughput;
      roundStats[id].connectionTime = itr.second.connectionTime;
      roundStats[id].downlinkTime = itr.second.downlinkTime;
      roundStats[id].monitoringTime = itr.second.monitoringTime;
      roundStats[id].uplinkTime = itr.second.uplinkTime;
      Vector client_pos = GetPosition (allNodes.Get (id + 2));
      Vector router_pos = GetPosition (allNodes.Get (1));
      roundStats[id].routerDistance = GetDistanceFrom (client_pos, router_pos);
    }
  return roundStats;
}

//>>>>>>>>>>>>>>>>>>>>>>>>>>>>> \Experiment Helpers >>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>

} // namespace ns3