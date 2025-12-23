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

#ifndef SERVER_H
#define SERVER_H

#include "ns3/application.h"
#include "fl-client-session.h"
#include "ns3/event-id.h"
#include "ns3/ptr.h"
#include "ns3/traced-callback.h"
#include "ns3/address.h"
#include "ns3/inet-socket-address.h"
#include "ns3/seq-ts-size-header.h"
#include "ns3/data-rate.h"
#include <unordered_map>
#include <map>
#include <memory>

namespace ns3 {

class Address;
class Socket;
class Packet;

/**
 * \ingroup fl-server
 * \brief Server application for federated learning.
 *
 * This class manages server-side operations in a federated learning setup.
 * It handles receiving data from clients, sending models, and managing
 * client sessions.
 */
class Server : public Application
{
private:
  enum ApplicationPhase {
    MODEL_TRANSMISSION_PHASE, //!< Sending the model to clients.
    MONITORING_PHASE, //!< Responding to monitoring messages.
    MODEL_RECEIVE_PHASE, //!< Receiving models from clients.
    FINISHED
  };

public:
  /**
   * \brief Data structure to track statistics and session state for each client.
   */

  class ClientSessionData
  {
  public:
    ClientSessionData ()
  : m_timeBeginReceivingModelFromClient (),
    m_timeEndReceivingModelFromClient (),
    m_timeBeginSendingModelToClient (),
    m_timeEndSendingModelToClient (),

    m_bytesModelSent (0),
    m_bytesModelToSend (0),
    m_bytesModelReceived (0),
    m_bytesModelToReceive (0),

    m_bytesMonitoringReceived (0),
    m_bytesMonitoringToReceive (0),

    m_address (),
    m_currentPhase ()
{
}

    // === Timing ===
    ns3::Time m_timeBeginReceivingModelFromClient; //!< Time when receiving starts.
    ns3::Time m_timeEndReceivingModelFromClient;   //!< Time when receiving ends.
    ns3::Time m_timeBeginSendingModelToClient;     //!< Time when sending starts.
    ns3::Time m_timeEndSendingModelToClient;       //!< Time when sending ends.

    // === Model Transfer ===
    uint32_t m_bytesModelSent;          //!< Total bytes sent to the client.
    uint32_t m_bytesModelToSend;        //!< Remaining bytes to send to the client.
    uint32_t m_bytesModelReceived;      //!< Total bytes received from the client.
    uint32_t m_bytesModelToReceive;     //!< Remaining bytes to receive from the client.

    // === Monitoring Data ===
    uint32_t m_bytesMonitoringReceived;     //!< Total bytes received from the client.
    uint32_t m_bytesMonitoringToReceive;    //!< Remaining bytes to receive from the client.

    // === Session Info ===
    ns3::Address m_address;                 //!< Client's address.
    ApplicationPhase m_currentPhase;       //!< Current phase of the session.
  };

  /**
   * \brief Get the TypeId of the application.
   * \return The TypeId.
   */
  static TypeId GetTypeId (void);

  Server ();
  virtual ~Server ();

  /**
   * \brief Get a list of all connected sockets and their client sessions.
   * \return A map of connected sockets to their session data.
   */
  std::map<Ptr<Socket>, std::shared_ptr<ClientSessionData>> GetAcceptedSockets (void) const;

  /**
   * \brief Print information about all accepted sockets and their sessions.
   */
  void PrintAcceptedSocketsInfo (void) const;

  /**
   * \brief Set the session manager and simulation parameters.
   * \param pSessionManager Pointer to the session manager.
   * \param round Current simulation round.
   */
  void SetClientSessionManager (ClientSessionManager *pSessionManager, int round);

protected:
  virtual void DoDispose (void);

private:
  // ---------------------------------------------------------------------------
  //                            Application Lifecycle
  // ---------------------------------------------------------------------------
  virtual void StartApplication (void); //!< Start the application.
  virtual void StopApplication (void); //!< Stop the application.

  // ---------------------------------------------------------------------------
  //                          Connection Handling
  // ---------------------------------------------------------------------------
  bool ConnectionRequestCallback (Ptr<Socket> socket,
                                  const Address &from); //!< Handle connection requests.
  void NewConnectionCreatedCallback (Ptr<Socket> socket,
                                     const Address &from); //!< Handle new connections.
  void HandlePeerClose (Ptr<Socket> socket); //!< Handle connection closure.
  void HandlePeerError (Ptr<Socket> socket); //!< Handle connection errors.

  // ---------------------------------------------------------------------------
  //                              Receive Logic
  // ---------------------------------------------------------------------------
  void ModelReceiptCallback (Ptr<Socket> socket); //!< Handle incoming data.
  void MonitorReqCallback (Ptr<Socket> socket); //!< Handle monitoring requests from clients.

  // ---------------------------------------------------------------------------
  //                            Transmit Logic
  // ---------------------------------------------------------------------------
  void StartModelTransmission (Ptr<Socket> socket); //!< Begin model transmission to client.
  void
  StartMonitoringTransmission (Ptr<Socket> socket); //!< Begin model transmission to all client.
  void StartModelReceipt (Ptr<Socket> socket); //!< Begin model transmission to client.

  void SendModel (Ptr<Socket> socket); //!< Send a model message to the client.
  void ReadyToSendCallback (Ptr<Socket> socket,
                            uint32_t available); //!< Handle socket readiness to send.

  void SendMonitoringResponses (); //!< Send monitoring responses to clients.

  // -------------------------
  // Utility Functions
  // -------------------------
  void PacketReceived (const Ptr<Packet> &packet, const Address &from,
                       const Address &localAddress); //!< Process packets and extract headers.
  int32_t TrySendPacket (Ptr<Socket> socket, uint32_t bytesToSend);

  void TransitionToMonitoringPhase();
  void TransitionToModelReceptionPhase();


  // -------------------------
  // Member Variables
  // -------------------------
  // === Simulation Phase Tracking ===
  ApplicationPhase m_currentPhase;           //!< Current application phase.
  uint32_t m_clientsInCurrentPhase;          //!< Number of clients in the current phase.
  uint32_t m_monitorMsgSize;                 //!< Size of monitoring message.
  uint32_t m_monitorSteps;                   //!< Total monitoring steps.
  uint32_t m_remainingSteps;                 //!< Remaining steps in this round.
  uint32_t m_remainingRequests;              //!< Remaining client requests.
  uint32_t m_currentRound;                   //!< Current simulation round.

  // === Communication ===
  Ptr<Socket> m_socket;                      //!< Listening socket.
  std::map<Ptr<Socket>, std::shared_ptr<ClientSessionData>> m_socketList; //!< Socket-session map.
  ClientSessionManager *m_clientSessionManager; //!< Manages client sessions.
  Address m_local;                           //!< Local address to bind the socket.
  uint64_t m_totalRx;                        //!< Total bytes received.
  TypeId m_tid;                              //!< Protocol TypeId.
  uint32_t m_packetSize;                     //!< Max packet size.
  EventId m_sendEvent;                       //!< Event for sending data.
  uint32_t m_bytesModel;                     //!< Model size to send/receive.
  DataRate m_dataRate;                       //!< Transmission data rate.
  int m_round;                               //!< Simulation round.

};

} // namespace ns3

#endif
