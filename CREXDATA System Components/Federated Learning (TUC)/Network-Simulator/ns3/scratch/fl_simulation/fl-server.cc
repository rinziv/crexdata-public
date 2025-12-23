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
#include "ns3/address.h"
#include "ns3/address-utils.h"
#include "ns3/log.h"
#include "ns3/inet-socket-address.h"
#include "ns3/inet6-socket-address.h"
#include "ns3/node.h"
#include "ns3/socket.h"
#include "ns3/udp-socket.h"
#include "ns3/simulator.h"
#include "ns3/socket-factory.h"
#include "ns3/packet.h"
#include "ns3/trace-source-accessor.h"
#include "ns3/udp-socket-factory.h"
#include "fl-server.h"
#include "ns3/boolean.h"
#include "ns3/integer.h"
#include "ns3/uinteger.h"
#include "ns3/inet-socket-address.h"

// TODO: handle case with bigger msg size than the packet
namespace ns3 {

NS_LOG_COMPONENT_DEFINE ("Server");

NS_OBJECT_ENSURE_REGISTERED (Server);

TypeId
Server::GetTypeId (void)
{
  static TypeId tid =
      TypeId ("ns3::Server")
          .SetParent<Application> ()
          .SetGroupName ("Applications")
          .AddConstructor<Server> ()

          .AddAttribute ("DataRate", "The data rate in on state.",
                         DataRateValue (DataRate ("1b/s")),
                         MakeDataRateAccessor (&Server::m_dataRate), MakeDataRateChecker ())

          .AddAttribute ("Local", "The Address on which to Bind the rx socket.", AddressValue (),
                         MakeAddressAccessor (&Server::m_local), MakeAddressChecker ())
          .AddAttribute ("Protocol", "The type id of the protocol to use for the rx socket.",
                         TypeIdValue (UdpSocketFactory::GetTypeId ()),
                         MakeTypeIdAccessor (&Server::m_tid), MakeTypeIdChecker ())
          .AddAttribute ("MaxPacketSize", "MaxPacketSize to send to client", TypeId::ATTR_SGC,
                         UintegerValue (), MakeUintegerAccessor (&Server::m_packetSize),
                         MakeUintegerChecker<uint32_t> ())
          .AddAttribute ("BytesModel", "Number of bytes in model", TypeId::ATTR_SGC,
                         UintegerValue (), MakeUintegerAccessor (&Server::m_bytesModel),
                         MakeUintegerChecker<uint32_t> ())
          .AddAttribute ("MonitorMsgSize", "Number of Monitoring steps", TypeId::ATTR_SGC,
                         UintegerValue (), MakeUintegerAccessor (&Server::m_monitorMsgSize),
                         MakeUintegerChecker<uint32_t> ())
          .AddAttribute ("MonitorSteps", "Number of Monitoring steps", TypeId::ATTR_SGC,
                         UintegerValue (), MakeUintegerAccessor (&Server::m_monitorSteps),
                         MakeUintegerChecker<uint32_t> ());

  return tid;
}

Server::Server ()
    : m_currentPhase (),
      m_clientsInCurrentPhase (0),
      m_monitorMsgSize (1),
      m_monitorSteps (0),
      m_remainingSteps (0),
      m_remainingRequests (0),
      m_currentRound (0),
      m_socket (0),
      m_socketList (),
      m_clientSessionManager (nullptr),
      m_local (),
      m_totalRx (0),
      m_tid (),
      m_packetSize (0),
      m_sendEvent (),
      m_bytesModel (0),
      m_dataRate (),
      m_round (0)
{
}
Server::~Server ()
{
  NS_LOG_FUNCTION (this);
}
void
Server::SetClientSessionManager (ClientSessionManager *pSessionManager, int round)
{
  m_clientSessionManager = pSessionManager;
  m_round = round;
}

void
Server::PrintAcceptedSocketsInfo (void) const
{
  NS_LOG_FUNCTION (this);
  NS_LOG_UNCOND ("Printing Accepted Sockets Info:");

  for (const auto &socketPair : m_socketList)
    {
      Ptr<Socket> socket = socketPair.first;
      std::shared_ptr<ClientSessionData> sessionData = socketPair.second;

      if (socket && sessionData)
        {
          NS_LOG_UNCOND ("Socket: "
                         << socket << " Address: "
                         << InetSocketAddress::ConvertFrom (sessionData->m_address).GetIpv4 ()
                         << " Bytes Received: " << sessionData->m_bytesModelReceived);
        }
      else
        {
          NS_LOG_UNCOND ("Invalid socket or session data.");
        }
    }
}

std::map<Ptr<Socket>, std::shared_ptr<Server::ClientSessionData>>
Server::GetAcceptedSockets (void) const
{
  NS_LOG_FUNCTION (this);
  return m_socketList;
}

void
Server::DoDispose (void)
{
  NS_LOG_FUNCTION (this);
  m_socket = 0;
  m_socketList.clear ();

  // chain up
  Application::DoDispose ();
}

// Application Methods
void
Server::StartApplication () // Called at time specified by Start
{
  NS_LOG_FUNCTION (this);
  // Create the socket if not already
  if (!m_socket)
    {
      m_socket = Socket::CreateSocket (GetNode (), m_tid);
      if (m_socket->Bind (m_local) == -1)
        {
          NS_FATAL_ERROR ("Failed to bind socket");
        }

      if (m_socket->Listen () == -1)
        {
          NS_FATAL_ERROR ("Failed to listen socket");
        }
      // NS_LOG_UNCOND ("Accept is called from server " << Simulator::Now ().GetSeconds () << "s");
    }

  m_currentPhase = MODEL_TRANSMISSION_PHASE;
  NS_LOG_UNCOND ("Server Starts " << Simulator::Now ().GetSeconds () << "s");
  // m_socket->SetRecvCallback (MakeCallback (&Server::ReceivedDataCallback, this));
  m_socket->SetAcceptCallback (MakeCallback (&Server::ConnectionRequestCallback, this),
                               MakeCallback (&Server::NewConnectionCreatedCallback, this));
  m_socket->SetCloseCallbacks (MakeCallback (&Server::HandlePeerClose, this),
                               MakeCallback (&Server::HandlePeerError, this));
}

void
Server::StopApplication () // Called at time specified by Stop
{
  NS_LOG_FUNCTION (this);
  NS_LOG_UNCOND ("Stopping Application");

  if (m_sendEvent.IsRunning ())
    {
      Simulator::Cancel (m_sendEvent);
    }

  //Close all connections
  for (auto const &itr : m_socketList)
    {
      itr.first->Close ();
      itr.first->SetRecvCallback (MakeNullCallback<void, Ptr<Socket>> ());
    }

  if (m_socket)
    {
      m_socket->Close ();
      m_socket->SetRecvCallback (MakeNullCallback<void, Ptr<Socket>> ());
    }
}

bool
Server::ConnectionRequestCallback (Ptr<Socket> socket, const Address &address)
{
  NS_LOG_UNCOND ("Connection Request Received " << Simulator::Now ().GetSeconds () << "s");
  return true;
}

void
Server::NewConnectionCreatedCallback (Ptr<Socket> socket, const Address &from)
{
  NS_LOG_UNCOND ("Connection Accepted " << Simulator::Now ().GetSeconds ());

  NS_LOG_FUNCTION (this << socket << from);
  auto clientSession = std::make_shared<ClientSessionData> ();

  auto nsess = m_socketList.insert (std::make_pair (socket, clientSession));
  nsess.first->second->m_address = from;

  //* Clients start receiving model
  nsess.first->second->m_currentPhase = MODEL_TRANSMISSION_PHASE;
  m_clientsInCurrentPhase++;

  // Necessary to have a little gap to ensure that correct functionality with very small models
  Simulator::Schedule(Seconds(1),&Server::StartModelTransmission, this, socket);

  // NS_LOG_UNCOND ("Accept:" << m_clientSessionManager->ResolveToIdFromServer (socket) + 1);
}


void
Server::StartModelTransmission (Ptr<Socket> socket)
{
  auto itr = m_socketList.find (socket);
  if (itr == m_socketList.end ())
    {
      NS_LOG_ERROR ("Socket not found in session list!");
      return;
    }
  itr->second->m_bytesModelToSend = m_bytesModel;
  itr->second->m_timeBeginSendingModelToClient = Simulator::Now ();

  // TODO: Assign at another point that is fitting to the correct phase

  NS_LOG_UNCOND ("Server Start Sending : "
                 << Simulator::Now ().GetSeconds ()
                 << "s To Client: " << m_clientSessionManager->ResolveToIdFromServer (socket) + 1);

  SendModel (socket);
}

void
Server::SendModel (Ptr<Socket> socket)
{
  auto itr = m_socketList.find (socket);

  if (itr == m_socketList.end () || itr->second->m_bytesModelToSend == 0)
    {
      // std::cin.ignore ();
      NS_LOG_UNCOND ("No scheduling will be performed");
      return;
    }

  // uint32_t txAvailable = socket->GetTxAvailable ();

  // NS_LOG_UNCOND ("Bytes to send: " << bytesToSend);
  auto sentBytes = TrySendPacket (socket, itr->second->m_bytesModelToSend);

  if (sentBytes < 0)
    {
      // NS_LOG_UNCOND ("Error sending model message to client.");
      return;
    }

  itr->second->m_bytesModelSent += sentBytes;
  itr->second->m_bytesModelToSend -= sentBytes;
  // NS_LOG_UNCOND ("Bytes remaining to send to: "
  //  << m_clientSessionManager->ResolveToIdFromServer (socket) + 1 << " "
  //  << itr->second->m_bytesModelToSend);
  if (itr->second->m_bytesModelToSend > 0)
    {
      m_sendEvent = Simulator::ScheduleNow (&Server::SendModel, this, socket);
    }
  else
    {
      NS_LOG_UNCOND ("Finished Transmitting data to Client: "
                     << m_clientSessionManager->ResolveToIdFromServer (socket) + 1);
      // All data sent
      itr->second->m_timeEndSendingModelToClient = Simulator::Now ();
      m_clientsInCurrentPhase--;
      NS_LOG_UNCOND ("Remaining clients to receive initial model: " << m_clientsInCurrentPhase);

      // Transition to the monitoring phase when all received the model
      if (m_clientsInCurrentPhase == 0)
        {

          if (m_monitorSteps>0){
            TransitionToMonitoringPhase();
          }
          else{
            TransitionToModelReceptionPhase();
          }
        }
    }
}

// === Monitoring Phase Transition Helper ===
void Server::TransitionToMonitoringPhase()
{
  NS_LOG_UNCOND("Models Transmitted to all Clients. Starting Monitoring Phase...");

  for (auto &entry : m_socketList)
  {
    Ptr<Socket> clientSocket = entry.first;
    clientSocket->SetRecvCallback(MakeCallback(&Server::MonitorReqCallback, this));
    Simulator::Schedule(Seconds(10), &Server::StartMonitoringTransmission, this, clientSocket);

    auto session = entry.second;
    session->m_currentPhase = MONITORING_PHASE;
    m_clientsInCurrentPhase++; // Reset to monitor client completions
  }

  m_currentPhase = MONITORING_PHASE;
  m_remainingSteps = m_monitorSteps;
  m_remainingRequests = m_socketList.size();
}

// === Model Reception Phase Transition Helper ===
void Server::TransitionToModelReceptionPhase()
{
  NS_LOG_UNCOND("Transitioning to model reception phase.");

  for (auto &entry : m_socketList)
  {
    Ptr<Socket> socket = entry.first;
    socket->SetRecvCallback(MakeCallback(&Server::ModelReceiptCallback, this));

    auto session = entry.second;
    session->m_bytesModelReceived = 0;
    session->m_bytesModelToReceive = m_bytesModel;
    session->m_currentPhase = MODEL_RECEIVE_PHASE;
    m_clientsInCurrentPhase++; // For final aggregation tracking
  }

  m_currentPhase = MODEL_RECEIVE_PHASE;
}

void
Server::StartMonitoringTransmission (Ptr<Socket> socket)
{
  // * Ensures all the clients start monitoring together after they have received the whole model!
  NS_LOG_FUNCTION (this);
  int32_t startFlag = 1;
  int32_t sentBytes = TrySendPacket (socket, startFlag);

  if (sentBytes < 0)
    {
      NS_LOG_UNCOND ("Error sending start flag to client.");
      return;
    }
  NS_LOG_UNCOND (
      "Sent Start Flag to Client: " << m_clientSessionManager->ResolveToIdFromServer (socket) + 1);
}



void
Server::MonitorReqCallback (Ptr<Socket> socket)
{
  NS_LOG_FUNCTION (this << socket);

  // Ensure the socket is valid and in the monitoring phase
  auto itr = m_socketList.find (socket);
  if (itr == m_socketList.end ())
    {
      NS_LOG_ERROR ("Unexpected monitoring message from untracked client.");
      return;
    }

  Ptr<Packet> packet;
  while ((packet = socket->Recv ()))
    {
      if (packet->GetSize () == 0)
        {
          NS_LOG_UNCOND ("EOF");
          break; // EOF
        }

      auto sessionData = itr->second;

      // Accumulate bytes received for the current monitoring message
      sessionData->m_bytesMonitoringReceived += packet->GetSize ();

      // Check if the full monitoring message has been received
      if (sessionData->m_bytesMonitoringReceived >= m_monitorMsgSize)
        {
          // Reset bytes received for the next monitoring message
          sessionData->m_bytesMonitoringReceived = 0;

          // Mark client as having responded for this step
          m_remainingRequests--;
          NS_LOG_UNCOND ("Monitoring message received from Client: "
                         << m_clientSessionManager->ResolveToIdFromServer (socket) + 1);
          NS_LOG_UNCOND ("Remaining Requests: " << m_remainingRequests);

          // If all clients have responded, send responses
          if (m_remainingRequests == 0)
            {
              SendMonitoringResponses ();
            }
        }
    }
}

void
Server::SendMonitoringResponses ()
{
  m_remainingSteps--;
  uint32_t currentStep = m_monitorSteps - m_remainingSteps;
  NS_LOG_UNCOND ("All clients completed monitoring step " << currentStep
                                                          << ". Sending responses...");
  // Send 1-byte response to all clients
  for (auto &entry : m_socketList)
    {
      auto &socket = entry.first;
      uint32_t responseBytes = 1;

      // NS_LOG_UNCOND ("Bytes to send: " << bytesToSend);
      auto sentBytes = TrySendPacket (socket, responseBytes);

      if (sentBytes > 0)
        {
          NS_LOG_UNCOND ("Sent monitoring response to Client: "
                         << m_clientSessionManager->ResolveToIdFromServer (socket) + 1);
        }
      else
        {
          NS_LOG_UNCOND ("Unable to Send response."
                         << m_clientSessionManager->ResolveToIdFromServer (socket) + 1);
        }
    }

  // Prepare for the next step or complete the phase
  if (m_remainingSteps > 0)
    {
      // NS_LOG_UNCOND ("Initializing step " << currentStep + 1 << " of " << m_monitorSteps);
      m_remainingRequests = m_socketList.size ();
    }
  else
    {
      NS_LOG_UNCOND ("Monitoring phase completed after " << m_monitorSteps << " steps.");

      // Set callback for receiving models
      for (auto &entry : m_socketList)
        {
          auto &socket = entry.first;
          socket->SetRecvCallback (MakeCallback (&Server::ModelReceiptCallback, this));
          auto sessionData = entry.second;
          sessionData->m_bytesModelReceived = 0;
          sessionData->m_bytesModelToReceive = m_bytesModel;
          sessionData->m_currentPhase = MODEL_RECEIVE_PHASE;
        }

      NS_LOG_UNCOND ("Transitioning to model reception phase.");
      m_currentPhase = MODEL_RECEIVE_PHASE;
    }
}

void
Server::ModelReceiptCallback (Ptr<Socket> socket)
{
  NS_LOG_FUNCTION (this << socket);
  Ptr<Packet> packet;
  Address from;
  Address localAddress;
  while ((packet = socket->RecvFrom (from)))
    {

      if (packet->GetSize () == 0)
        { //EOF
          break;
        }

      auto itr = m_socketList.find (socket);

      if (itr == m_socketList.end ())
        {
          NS_LOG_UNCOND ("In Return");
          return;
        }
      if (itr->second->m_bytesModelReceived == 0)
        {
          itr->second->m_timeBeginReceivingModelFromClient = Simulator::Now ();
        }

      itr->second->m_bytesModelReceived += packet->GetSize ();
      itr->second->m_bytesModelToReceive -= packet->GetSize ();
      // NS_LOG_UNCOND ("Bytes Model To receive " << itr->second->m_bytesModelToReceive);
      // std::cin.ignore ();
      if (itr->second->m_bytesModelToReceive == 0)
        {
          itr->second->m_timeEndReceivingModelFromClient = Simulator::Now ();

          NS_LOG_UNCOND ("Received whole model from client: "
                         << m_clientSessionManager->ResolveToIdFromServer (socket) + 1 << " at "
                         << itr->second->m_timeEndReceivingModelFromClient.GetSeconds () << "s");
          itr->second->m_currentPhase = FINISHED;
          m_clientsInCurrentPhase--;
          if (m_clientsInCurrentPhase == 0)
            {
              NS_LOG_UNCOND ("Model received by all clients for aggregation. Round Completed.");
              m_currentPhase = FINISHED;
              Simulator::Stop ();
              return;
            }
        }
    }
}

// TODO: 4. Fix Bug with one more byte sent causing Error in small csma datarates!
void
Server::ReadyToSendCallback (Ptr<Socket> socket, uint32_t available)
{
  socket->SetSendCallback (MakeNullCallback<void, Ptr<Socket>, uint32_t> ());
  if (m_currentPhase == MODEL_TRANSMISSION_PHASE)
    {
      Simulator::ScheduleNow (&Server::SendModel, this, socket);
      // SendMonitoringMessage (socket);
    }
  else if (m_currentPhase == MONITORING_PHASE)
    {
      Simulator::ScheduleNow (&Server::SendMonitoringResponses, this);
      // SendModel (socket);
    }
}

int32_t
Server::TrySendPacket (Ptr<Socket> socket, uint32_t desiredBytes)
{
  uint32_t txAvailable = socket->GetTxAvailable ();
  if (txAvailable == 0)
    {
      socket->SetSendCallback (MakeCallback (&Server::ReadyToSendCallback, this));
      // NS_LOG_UNCOND ("Send buffer full or not ready. Waiting for ReadyToSendCallback.");
      return -1;
    }

  uint32_t bytesToSend = std::min (std::min (desiredBytes, m_packetSize), txAvailable);
  Ptr<Packet> packet = Create<Packet> (bytesToSend);
  int32_t sentBytes = socket->Send (packet);
  // NS_LOG_UNCOND ("Bytes Sent " << sentBytes);

  return sentBytes;
}

void
Server::HandlePeerClose (Ptr<Socket> socket)
{
  NS_LOG_UNCOND ("Peer Close " << Simulator::Now ().GetSeconds ());

  NS_LOG_FUNCTION (this << socket);
}

void
Server::HandlePeerError (Ptr<Socket> socket)
{
  NS_LOG_UNCOND ("Peer Error " << Simulator::Now ().GetSeconds ());

  NS_LOG_FUNCTION (this << socket);
}

} // Namespace ns3