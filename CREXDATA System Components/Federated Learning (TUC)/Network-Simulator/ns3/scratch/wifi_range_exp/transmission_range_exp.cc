#include "ns3/core-module.h"
#include "ns3/network-module.h"
#include "ns3/mobility-module.h"
#include "ns3/wifi-module.h"
#include "ns3/internet-module.h"
#include "ns3/applications-module.h"
#include "ns3/config-store-module.h"
#include "ns3/csma-helper.h"

using namespace ns3;

NS_LOG_COMPONENT_DEFINE ("WifiRangeExperiment");
void
Pause ()
{
  std::cout << "Enter any key to continue..." << std::endl;
  std::cin.ignore ();
}
void
ReceivePacket (Ptr<Socket> socket)
{
  while (socket->Recv ())
    {
      NS_LOG_UNCOND ("Received one packet!");
    }
}

void
SetupMobility (NodeContainer &c, double distance)
{
  // Setup Mobility
  MobilityHelper mobility;

  Ptr<ListPositionAllocator> positionAlloc = CreateObject<ListPositionAllocator> ();
  positionAlloc->Add (Vector (0.0, 0.0, 0.0));
  positionAlloc->Add (Vector (1.7, 0.0, 0.0));
  positionAlloc->Add (Vector (1.7 + distance, 0.0, 0.0));

  mobility.SetPositionAllocator (positionAlloc);
  mobility.SetMobilityModel ("ns3::ConstantPositionMobilityModel");
  mobility.Install (c);
}

NetDeviceContainer
SetupDevicesAndMobility (NodeContainer &c, double distance)
{
  // Split the nodes into Server, AP, and Clients
  NodeContainer csmaNodes;
  NodeContainer serverNode;
  NodeContainer apNode;
  NodeContainer staNodes;

  NetDeviceContainer allDevices;

  serverNode.Add (c.Get (0)); // First node as Server
  apNode.Add (c.Get (1)); // Second node as AP
  csmaNodes.Add (serverNode);
  csmaNodes.Add (apNode);

  for (uint32_t i = 2; i < c.GetN (); ++i)
    {
      staNodes.Add (c.Get (i)); // Remaining nodes as Clients
    }

  // Setup Ethernet connection for the server and the AP
  CsmaHelper csma;
  csma.SetChannelAttribute ("DataRate", StringValue ("100Mbps"));
  csma.SetChannelAttribute ("Delay", TimeValue (NanoSeconds (6560)));
  NetDeviceContainer csmaDevices = csma.Install (csmaNodes);
  allDevices.Add (csmaDevices);

  // Setup Wi-Fi for the clients and the AP
  YansWifiChannelHelper wifiChannel = YansWifiChannelHelper::Default ();
  wifiChannel.SetPropagationDelay ("ns3::ConstantSpeedPropagationDelayModel");

  YansWifiPhyHelper wifiPhy = YansWifiPhyHelper ();
  wifiPhy.SetErrorRateModel ("ns3::YansErrorRateModel");
  wifiPhy.SetChannel (wifiChannel.Create ());
  wifiPhy.Set ("TxPowerStart", DoubleValue (18.0)); // Example setting for min power
  wifiPhy.Set ("TxPowerEnd", DoubleValue (18.0)); // Example setting for max power

  WifiHelper wifi;
  wifi.SetStandard (WIFI_STANDARD_80211n_2_4GHZ);
  wifi.SetRemoteStationManager ("ns3::MinstrelHtWifiManager");

  WifiMacHelper wifiMac;
  Ssid ssid = Ssid ("home-wifi-ssid");

  wifiMac.SetType ("ns3::ApWifiMac", "Ssid", SsidValue (ssid));
  NetDeviceContainer apDevices = wifi.Install (wifiPhy, wifiMac, apNode);

  wifiMac.SetType ("ns3::StaWifiMac", "Ssid", SsidValue (ssid), "ActiveProbing",
                   BooleanValue (false));
  NetDeviceContainer staDevices = wifi.Install (wifiPhy, wifiMac, staNodes);

  allDevices.Add (apDevices);
  allDevices.Add (staDevices);
  SetupMobility (c, distance);

  return allDevices;
}

Ipv4InterfaceContainer
SetupInternetStack (NodeContainer &c, NetDeviceContainer &devices)
{
  InternetStackHelper internet;
  internet.Install (c);

  Ipv4AddressHelper ipv4;
  NetDeviceContainer csmaDevices;
  NetDeviceContainer wifiDevices;

  // Assuming the first 2 devices are CSMA and the rest are Wi-Fi
  uint32_t nDevices = devices.GetN ();
  for (uint32_t i = 0; i < 2; ++i)
    {
      csmaDevices.Add (devices.Get (i));
    }
  for (uint32_t i = 2; i < nDevices; ++i)
    {
      wifiDevices.Add (devices.Get (i));
    }

  Ipv4InterfaceContainer csmaInterfaces;
  Ipv4InterfaceContainer wifiInterfaces;

  // Ethernet devices
  ipv4.SetBase ("192.168.1.0", "255.255.255.0");
  csmaInterfaces = ipv4.Assign (csmaDevices);

  // Wi-Fi devices
  ipv4.SetBase ("192.168.3.0", "255.255.255.0");
  wifiInterfaces = ipv4.Assign (wifiDevices);

  // Combine interfaces
  Ipv4InterfaceContainer interfaces;
  interfaces.Add (csmaInterfaces);
  interfaces.Add (wifiInterfaces);

  for (uint32_t i = 0; i < interfaces.GetN (); ++i)
    {
      NS_LOG_UNCOND ("IP: " << interfaces.GetAddress (i));
    }

  return interfaces;
}

ApplicationContainer
SetupApplication (NodeContainer &c, NetDeviceContainer &devices,
                  Ipv4InterfaceContainer &intetrfaces)
{
  // Setup UDP Echo Server on the first CSMA node (server)
  UdpEchoServerHelper echoServer (9);
  ApplicationContainer serverApps = echoServer.Install (c.Get (0));
  serverApps.Start (Seconds (1.0));
  serverApps.Stop (Seconds (10.0));

  // Setup UDP Echo Client on the first Wi-Fi STA node
  UdpEchoClientHelper echoClient (intetrfaces.GetAddress (0), 9);
  echoClient.SetAttribute ("MaxPackets", UintegerValue (10));
  echoClient.SetAttribute ("Interval", TimeValue (Seconds (1.0)));
  echoClient.SetAttribute ("PacketSize", UintegerValue (1024));

  ApplicationContainer clientApps = echoClient.Install (c.Get (2));
  clientApps.Start (Seconds (2.0));
  clientApps.Stop (Seconds (10.0));
  ApplicationContainer allApps;
  allApps.Add (serverApps);
  allApps.Add (clientApps);
  return allApps;
}
int
main (int argc, char *argv[])
{

  CommandLine cmd (__FILE__);
  cmd.Parse (argc, argv);

  double distance = 30.0; // Initial distance between nodes in meters.
  double increment = 5.0; // Increment Step to get away
  bool run = true;
  // double m_txGain = 0.0;
  // Create nodes

  while (run)
    {
      NodeContainer nodes;
      nodes.Create (3);
      // Set up WiFi
      NetDeviceContainer devices = SetupDevicesAndMobility (nodes, distance);
      Ipv4InterfaceContainer interfaces = SetupInternetStack (nodes, devices);
      ApplicationContainer allApps = SetupApplication (nodes, devices, interfaces);

      Simulator::Stop (Seconds (10.0));

      Simulator::Run ();

      Ptr<PacketSink> sink1 = DynamicCast<PacketSink> (allApps.Get (0));
      double throughput = sink1->GetTotalRx () * 8 / (10.0 * 1000);
      NS_LOG_UNCOND ("Distance: " << distance << "m, Throughput: " << throughput << " Kbps");

      if (throughput > 0)
        {
          distance += increment; // Increase distance by 5 meters and try again
        }
      else
        {
          run = false; // No throughput, stop the experiment
        }

      Simulator::Destroy ();
    }

  NS_LOG_UNCOND ("Maximum Transmission Range: " << distance - increment << "m");

  return 0;
}