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

#ifndef CLIENT_APPLICATION_H
#define CLIENT_APPLICATION_H

#include "ns3/applications-module.h"
#include "ns3/type-id.h"
#include "ns3/application.h"
#include "ns3/event-id.h"
#include "ns3/ptr.h"
#include "ns3/traced-callback.h"
#include "ns3/address.h"
#include "ns3/inet-socket-address.h"
#include "ns3/seq-ts-size-header.h"

namespace ns3 {

/**
 * \ingroup fl-client
 * \brief Client application for federated learning.
 * 
 * This application manages interactions between the client and server during
 * federated learning, including receiving the model, monitoring computations,
 * and transmitting the updated model back to the server.
 */
class ClientApplication : public Application
{
public:
  /**
   * \brief Get the TypeId of the application.
   * \return The TypeId.
   */
  static TypeId GetTypeId (void);

  ClientApplication ();
  virtual ~ClientApplication ();

  /**
   * \brief Configure the application.
   * 
   * \param socket The socket to use for communication.
   * \param address The server address.
   * \param packetSize Maximum size of packets for communication.
   * \param nBytesModel The size of the model to send/receive.
   * \param monitorMsgSize Size of monitoring messages.
   * \param monitorSteps Number of monitoring steps.
   */
  void Setup (Ptr<Socket> socket, Address address, uint32_t packetSize, uint32_t nBytesModel,
              uint32_t monitorMsgSize, uint32_t monitorSteps);

private:
  // ---------------------------------------------------------------------------
  //                            Application Logic
  // ---------------------------------------------------------------------------
  /**
   * \brief Called when the application starts.
   */
  virtual void StartApplication (void);

  /**
   * \brief Called when the application stops.
   */
  virtual void StopApplication (void);

  /**
   * \brief Called when a connection is successfully established.
   * \param socket The connected socket.
   */
  void ConnectionSucceeded (Ptr<Socket> socket);

  /**
   * \brief Called when a connection attempt fails.
   * \param socket The socket.
   */
  void ConnectionFailed (Ptr<Socket> socket);

  /**
   * \brief Called when the socket closes normally.
   * \param socket The socket.
   */
  void NormalClose (Ptr<Socket> socket);

  /**
   * \brief Called when the socket encounters an error.
   * \param socket The socket.
   */
  void ErrorClose (Ptr<Socket> socket);

  // ---------------------------------------------------------------------------
  //                              Receive Logic
  // ---------------------------------------------------------------------------
  /**
   * \brief Handles receiving the model from the server.
   * \param socket The socket used for receiving.
   */
  void ModelReceiptCallback (Ptr<Socket> socket);

  /**
   * \brief Handles receiving monitoring responses.
   * \param socket The socket used for receiving.
   */
  void MonitorResponseCallback (Ptr<Socket> socket);

  // ---------------------------------------------------------------------------
  //                            Transmit Logic
  // ---------------------------------------------------------------------------
  /**
   * \brief Initiates the transmission of monitoring messages.
   */
  void StartMonitoringTransmission ();

  /**
   * \brief Initiates the transmission of the model to the server.
   */
  void StartModelTransmission ();

  /**
   * \brief Sends a monitoring message.
   * \param socket The socket used for sending.
   */
  void SendMonitoringReq (Ptr<Socket> socket);

  /**
   * \brief Sends a portion of the model to the server.
   * \param socket The socket used for sending.
   */
  void SendModel (Ptr<Socket> socket);

  /**
   * \brief Handles cases where the socket becomes ready to send.
   * \param socket The socket.
   * \param available Available space in the socket buffer.
   */
  void ReadyToSendCallback (Ptr<Socket> sock, uint32_t available);

  // ---------------------------------------------------------------------------
  //                              Utility Functions
  // ---------------------------------------------------------------------------
  void TransitionToMonitoringPhase ();
  void TransitionToModelTransmissionPhase ();



  /**
   * \brief Tries to send a packet through the specified socket.
   * 
   * If the socket buffer is full, it will wait until the buffer becomes
   * available before trying again.
   * 
   * \param socket The socket to use for sending.
   * \param bytesToSend Number of bytes to send.
   * \return The number of bytes successfully sent, or -1 on failure.
   */
  int32_t TrySendPacket (Ptr<Socket> socket, uint32_t bytesToSend);

  // ---------------------------------------------------------------------------
  //                            Member Variables
  // ---------------------------------------------------------------------------
  /**
   * \brief Enum representing the phases of the application.
   */
  enum ApplicationPhase {
    MODEL_RECEIVE_PHASE, //!< Receiving the model from the server.
    MONITORING_PHASE, //!< Monitoring and computation phase.
    MODEL_TRANSMISSION_PHASE, //!< Sending the model back to the server.
    FINISHED
  };

 // === Phase Control ===
ApplicationPhase m_currentPhase; //!< Current phase of application execution.

// === Communication Socket ===
Ptr<Socket> m_socket; //!< Socket for communication.
Address m_peer;       //!< Address of the server.
uint32_t m_packetSize; //!< Maximum packet size for communication.

// === Model Transmission ===
uint32_t m_bytesModel;            //!< Size of the model to send/receive.
uint32_t m_bytesModelReceived;    //!< Bytes of model received so far.
uint32_t m_bytesModelToReceive;   //!< Remaining bytes of the model to receive.
uint32_t m_bytesModelSent;        //!< Total bytes sent so far.
uint32_t m_bytesModelToSend;      //!< Remaining bytes of the model to send.

// === Monitoring Transmission ===
uint32_t m_bytesMonitoringSent;      //!< Total monitoring bytes sent.
uint32_t m_bytesMonitoringToSend;    //!< Remaining monitoring bytes to send.
uint32_t m_monitorMsgSize;           //!< Size of individual monitoring messages.
uint32_t m_monitorSteps;             //!< Total number of monitoring steps.
uint32_t m_monitorRemainingSteps;    //!< Remaining monitoring steps.

// === Timing Events ===
Time m_timeConnectionReqToServer;        //!< Time of connection request to server.
Time m_timeConnectionRespFromServer;     //!< Time of server's response.
Time m_timeBeginReceivingModelFromServer;//!< Start time of model reception.
Time m_timeEndReceivingModelFromServer;  //!< End time of model reception.
Time m_timeBeginMonitoring;              //!< Start time of monitoring.
Time m_timeEndMonitoring;                //!< End time of monitoring.
Time m_currentRequestStartTime;          //!< Start time of the current request.
Time m_totalMonitoringDuration;          //!< Total duration of monitoring.

// === Event Management ===
EventId m_sendEvent; //!< Event handle for pending transmissions.
};

} // namespace ns3

#endif
