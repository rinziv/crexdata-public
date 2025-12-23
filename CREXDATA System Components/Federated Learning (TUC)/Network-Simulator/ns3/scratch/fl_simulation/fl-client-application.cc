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

#include "fl-client-application.h"
#include "ns3/internet-module.h"

namespace ns3 {

NS_LOG_COMPONENT_DEFINE ("ClientApplication");
NS_OBJECT_ENSURE_REGISTERED (ClientApplication);

ClientApplication::ClientApplication ()
    : m_currentPhase (),
      m_socket (0),
      m_peer (),
      m_packetSize (0),
      m_bytesModel (0),
      m_bytesModelReceived (0),
      m_bytesModelToReceive (0),
      m_bytesModelSent (0),
      m_bytesModelToSend (0),
      m_bytesMonitoringSent (0),
      m_bytesMonitoringToSend (0),
      m_monitorMsgSize (0),
      m_monitorSteps (0),
      m_monitorRemainingSteps (0),
      m_timeConnectionReqToServer (),
      m_timeConnectionRespFromServer (),
      m_timeBeginReceivingModelFromServer (),
      m_timeEndReceivingModelFromServer (),
      m_timeBeginMonitoring (),
      m_timeEndMonitoring (),
      m_currentRequestStartTime (),
      m_totalMonitoringDuration (),
      m_sendEvent ()
{
}

TypeId
ClientApplication::GetTypeId (void)
{
  static TypeId tid =
      TypeId ("ns3::ClientApplication")
          .SetParent<Application> ()
          .SetGroupName ("Applications")
          .AddConstructor<ClientApplication> ()
          .AddAttribute ("MaxPacketSize", "MaxPacketSize to send to client", TypeId::ATTR_SGC,
                         UintegerValue (), MakeUintegerAccessor (&ClientApplication::m_packetSize),
                         MakeUintegerChecker<uint32_t> ())

          .AddAttribute ("BytesModel", "Number of bytes in model", TypeId::ATTR_SGC,
                         UintegerValue (), MakeUintegerAccessor (&ClientApplication::m_bytesModel),
                         MakeUintegerChecker<uint32_t> ())

          .AddAttribute ("BytesSent", "Number of bytes sent from client", TypeId::ATTR_SGC,
                         UintegerValue (),
                         MakeUintegerAccessor (&ClientApplication::m_bytesModelSent),
                         MakeUintegerChecker<uint32_t> ())

          .AddAttribute ("BytesReceived", "Number of bytes sent from server", TypeId::ATTR_SGC,
                         UintegerValue (),
                         MakeUintegerAccessor (&ClientApplication::m_bytesModelReceived),
                         MakeUintegerChecker<uint32_t> ())

          .AddAttribute ("MonitorMsgSize", "Size of monitoring messages", TypeId::ATTR_SGC,
                         UintegerValue (),
                         MakeUintegerAccessor (&ClientApplication::m_monitorMsgSize),
                         MakeUintegerChecker<uint32_t> ())

          .AddAttribute ("MonitorSteps", "Number of monitoring message steps", TypeId::ATTR_SGC,
                         UintegerValue (0),
                         MakeUintegerAccessor (&ClientApplication::m_monitorSteps),
                         MakeUintegerChecker<uint32_t> ())
          .AddAttribute ("BeginConnection", "Time sending connection request to server",
                         TypeId::ATTR_SGC, TimeValue (),
                         MakeTimeAccessor (&ClientApplication::m_timeConnectionReqToServer),
                         MakeTimeChecker ())
          .AddAttribute ("EndConnection", "Time connection established with server",
                         TypeId::ATTR_SGC, TimeValue (),
                         MakeTimeAccessor (&ClientApplication::m_timeConnectionRespFromServer),
                         MakeTimeChecker ())
          .AddAttribute ("BeginDownlink", "Time begin receiving model from server",
                         TypeId::ATTR_SGC, TimeValue (),
                         MakeTimeAccessor (&ClientApplication::m_timeBeginReceivingModelFromServer),
                         MakeTimeChecker ())

          .AddAttribute ("EndDownlink", "Time finish receiving model from server", TypeId::ATTR_SGC,
                         TimeValue (),
                         MakeTimeAccessor (&ClientApplication::m_timeEndReceivingModelFromServer),
                         MakeTimeChecker ())

          .AddAttribute ("BeginMonitoring", "Time begin sending monitoring requests to server",
                         TypeId::ATTR_SGC, TimeValue (),
                         MakeTimeAccessor (&ClientApplication::m_timeBeginMonitoring),
                         MakeTimeChecker ())

          .AddAttribute ("EndMonitoring", "Time finish receiving monitoring responses from server",
                         TypeId::ATTR_SGC, TimeValue (),
                         MakeTimeAccessor (&ClientApplication::m_timeEndMonitoring),
                         MakeTimeChecker ());
  return tid;
}

void
ClientApplication::Setup (Ptr<Socket> socket, Address address, uint32_t packetSize,
                          uint32_t nBytesModel, uint32_t monitorMsgSize, uint32_t monitorSteps)
{
  m_socket = socket;
  m_peer = address;
  m_packetSize = packetSize;
  m_bytesModel = nBytesModel;
  m_monitorMsgSize = monitorMsgSize;
  m_monitorSteps = monitorSteps;
}

ClientApplication::~ClientApplication ()
{
}

void
ClientApplication::StartApplication (void)
{

  // Set up callbacks
  m_socket->SetConnectCallback (MakeCallback (&ClientApplication::ConnectionSucceeded, this),
                                MakeCallback (&ClientApplication::ConnectionFailed, this));
  m_socket->SetCloseCallbacks (MakeCallback (&ClientApplication::NormalClose, this),
                               MakeCallback (&ClientApplication::ErrorClose, this));
  m_socket->Bind ();
  m_timeConnectionReqToServer = Simulator::Now ();
  m_socket->Connect (m_peer);

  NS_LOG_UNCOND ("Client " << (m_socket->GetNode ()->GetId () - 1) << " Sent connection Request.");
}

void
ClientApplication::StopApplication (void)
{
  if (m_sendEvent.IsRunning ())
    {
      Simulator::Cancel (m_sendEvent);
    }

  if (m_socket)
    {
      m_socket->Close ();
      m_socket->SetRecvCallback (MakeNullCallback<void, Ptr<Socket>> ());
    }
}

void
ClientApplication::ConnectionSucceeded (Ptr<Socket> socket)
{
  m_timeConnectionRespFromServer = Simulator::Now ();
  NS_LOG_UNCOND ("Client " << (socket->GetNode ()->GetId () - 1) << " Connected.");
  m_bytesModelToReceive = m_bytesModel;
  m_bytesModelToSend = 0;
  m_currentPhase = MODEL_RECEIVE_PHASE;
  socket->SetRecvCallback (MakeCallback (&ClientApplication::ModelReceiptCallback, this));
}

void
ClientApplication::ConnectionFailed (Ptr<Socket> socket)
{
  NS_LOG_UNCOND ("Connection Failed.");
}

void
ClientApplication::ModelReceiptCallback (Ptr<Socket> socket)
{
  Ptr<Packet> packet;
  while ((packet = socket->Recv ()))
    {
      if (packet->GetSize () == 0)
        {
          break;
        }

      if (m_bytesModelReceived == 0)
        {
          m_timeBeginReceivingModelFromServer = Simulator::Now ();
        }
      // * Used to be setting m_timeBeginning
      m_bytesModelReceived += packet->GetSize ();
      m_bytesModelToReceive -= packet->GetSize ();
      // NS_LOG_UNCOND ("Received: " << m_bytesModelReceived
      //                             << " Remaining: " << m_bytesModelToReceive);
      if (m_bytesModelToReceive <= 0)
      {
          m_timeEndReceivingModelFromServer = Simulator::Now ();
          NS_LOG_UNCOND ("Model received in " << (m_timeEndReceivingModelFromServer.GetSeconds () -
                                                  m_timeBeginReceivingModelFromServer.GetSeconds ())
                                              << " seconds.");

          if (m_monitorSteps > 0)
          {
              // Switch callback to monitor for start flag from server
              socket->SetRecvCallback (MakeCallback (&ClientApplication::MonitorResponseCallback, this));
              m_currentPhase = MODEL_RECEIVE_PHASE; // Wait in holding phase
          }
          else
          {
              // Skip monitoring, go straight to upload
              TransitionToModelTransmissionPhase();
          }
      }
    }
}

void
ClientApplication::TransitionToMonitoringPhase ()
{
  NS_LOG_UNCOND ("Client " << (m_socket->GetNode ()->GetId () - 1)
                           << " Transitioning to Monitoring Phase...");

  m_currentPhase = MONITORING_PHASE;
  m_monitorRemainingSteps = m_monitorSteps;
  m_bytesMonitoringSent = 0;
  m_bytesMonitoringToSend = m_monitorMsgSize;
  m_totalMonitoringDuration = Seconds (0);

  Simulator::Schedule (Seconds (10), &ClientApplication::StartMonitoringTransmission, this);
}

void
ClientApplication::TransitionToModelTransmissionPhase ()
{
  NS_LOG_UNCOND ("Client " << (m_socket->GetNode ()->GetId () - 1)
                           << " Transitioning to Model Transmission Phase...");

  m_currentPhase = MODEL_TRANSMISSION_PHASE;
  m_bytesModelToSend = m_bytesModel;

  Simulator::Schedule (Seconds (10), &ClientApplication::StartModelTransmission, this);
}


void
ClientApplication::StartMonitoringTransmission ()
{
  NS_LOG_UNCOND ("Start monitoring threshold.");
  m_monitorRemainingSteps = m_monitorSteps;
  m_bytesMonitoringSent = 0;
  m_bytesMonitoringToSend = m_monitorMsgSize;
  m_timeBeginMonitoring = Simulator::Now ();
  m_totalMonitoringDuration = Seconds (0); // Initialize total duration

  m_sendEvent = Simulator::ScheduleNow (&ClientApplication::SendMonitoringReq, this, m_socket);
  // SendMonitoringReq (m_socket);
}

void
ClientApplication::SendMonitoringReq (Ptr<Socket> socket)
{
  m_currentRequestStartTime = Simulator::Now ();
  auto sentBytes = TrySendPacket (socket, m_monitorMsgSize);

  if (sentBytes < 0)
    {
      NS_LOG_ERROR ("Failed to send monitoring message.");
      return;
    }
  m_bytesMonitoringSent += sentBytes;
  m_bytesMonitoringToSend -= sentBytes;
  NS_LOG_UNCOND ("Monitoring sent " << m_bytesMonitoringSent << " To send "
                                    << m_bytesMonitoringToSend);

  if (m_bytesMonitoringToSend > 0)
    {
      NS_LOG_UNCOND ("Partition sent");
      std::cin.ignore ();
      m_sendEvent = Simulator::ScheduleNow (&ClientApplication::SendMonitoringReq, this, socket);
    }
  else
    {
      NS_LOG_UNCOND ("Client " << (socket->GetNode ()->GetId () - 1) << " Sent monitoring message "
                               << (m_monitorSteps - m_monitorRemainingSteps + 1) << "/"
                               << m_monitorSteps
                               << " at time: " << Simulator::Now ().GetSeconds ());
      m_bytesMonitoringSent = 0;
      m_bytesMonitoringToSend = m_monitorMsgSize;
    }
}

void
ClientApplication::MonitorResponseCallback (Ptr<Socket> socket)
{

  Ptr<Packet> packet;
  while ((packet = socket->Recv ()))
    {
      if (packet->GetSize () == 0)
        {
          break;
        }

      // TO change state if initialization message is received
      if (m_currentPhase == MODEL_RECEIVE_PHASE)
      {
          // Treat this as start flag
          m_currentPhase = MONITORING_PHASE;
          m_sendEvent = Simulator::Schedule (Seconds (10), &ClientApplication::StartMonitoringTransmission, this);
          return;
      }

      // Calculate the duration of this request-response round trip
      Time duration = Simulator::Now () - m_currentRequestStartTime;
      m_totalMonitoringDuration += duration;

      m_monitorRemainingSteps--;
      NS_LOG_UNCOND ("Client " << (socket->GetNode ()->GetId () - 1)
                               << " Received monitoring response at time: "
                               << Simulator::Now ().GetSeconds ());

      if (m_monitorRemainingSteps > 0)
        {
          Simulator::ScheduleNow (&ClientApplication::SendMonitoringReq, this, socket);
          // SendMonitoringReq (socket);
        }
      else
        {
          m_timeEndMonitoring = Simulator::Now ();
          NS_LOG_UNCOND ("Monitoring completed in " << (m_timeEndMonitoring.GetSeconds () -
                                                        m_timeBeginMonitoring.GetSeconds ()));
          // Proceed to model transmission phase
          m_currentPhase = MODEL_TRANSMISSION_PHASE;
          // std::cin.ignore ();
          Simulator::Schedule (Seconds (10), &ClientApplication::StartModelTransmission, this);
        }
    }
}

void
ClientApplication::StartModelTransmission ()
{
  m_bytesModelToSend = m_bytesModel;
  NS_LOG_UNCOND ("Client " << (m_socket->GetNode ()->GetId () - 1)
                           << " Start sending model at time: " << Simulator::Now ().GetSeconds ());
  // NS_LOG_UNCOND ("Start sending model.");
  // Scheduler to handle recursive calls
  m_sendEvent = Simulator::ScheduleNow (&ClientApplication::SendModel, this, m_socket);
}

void
ClientApplication::SendModel (Ptr<Socket> socket)
{
  if (m_bytesModelToSend == 0)
    {
      NS_LOG_UNCOND ("No more data to send.");
      return;
    }

  // Send packetSize or the rest of bytesRemaining to complete model Transmission, if fit in buffer otherwise as much as possible
  // uint32_t txAvailable = socket->GetTxAvailable ();

  // NS_LOG_UNCOND ("Bytes to send: " << bytesToSend);
  auto sentBytes = TrySendPacket (socket, m_bytesModelToSend);

  if (sentBytes < 0)
    {
      // NS_LOG_UNCOND ("Error sending model message to client.");
      return;
    }
  m_bytesModelSent += sentBytes;
  m_bytesModelToSend -= sentBytes;

  if (m_bytesModelToSend > 0)
    {
      m_sendEvent = Simulator::ScheduleNow (&ClientApplication::SendModel, this, socket);
    }
  else
    {
      m_currentPhase = FINISHED;
      NS_LOG_UNCOND ("Client " << (socket->GetNode ()->GetId () - 1)
                               << " Completed model transmission.");
      //                 Simulator::Stop ();
      // return;
    }
}

void
ClientApplication::ReadyToSendCallback (Ptr<Socket> socket, uint32_t available)
{
  socket->SetSendCallback (MakeNullCallback<void, Ptr<Socket>, uint32_t> ());

  if (m_currentPhase == MONITORING_PHASE)
    {
      Simulator::ScheduleNow (&ClientApplication::SendMonitoringReq, this, socket);
      // SendMonitoringReq (socket);
    }
  else if (m_currentPhase == MODEL_TRANSMISSION_PHASE)
    {
      Simulator::ScheduleNow (&ClientApplication::SendModel, this, socket);
      // SendModel (socket);
    }
}

int32_t
ClientApplication::TrySendPacket (Ptr<Socket> socket, uint32_t desiredBytes)
{
  uint32_t txAvailable = socket->GetTxAvailable ();
  if (txAvailable == 0)
    {
      socket->SetSendCallback (MakeCallback (&ClientApplication::ReadyToSendCallback, this));
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
ClientApplication::NormalClose (Ptr<Socket> socket)
{
  NS_LOG_UNCOND ("Connection closed normally.");
}

void
ClientApplication::ErrorClose (Ptr<Socket> socket)
{
  NS_LOG_UNCOND ("Connection closed with error.");
}

} // namespace ns3