const WebSocket = require("ws");
const logger = require("../utils/logger");
const clientService = require("../services/clientService");
const pairingService = require("../services/pairingService");
const kafkaService = require("../services/kafkaService");
const messageFormatter = require("../services/messageFormatterService");
const legacyHandler = require("../services/legacyHandler");
const statsService = require("../services/statsService");
const { sendWsMessage, broadcastToAllOthers } = require("./wsUtils");
const { CONFIG, HARDCODED_DATA, topicForGPSData, topicForRouteData } = require("../config");
const Client = require("../models/Client"); // Import Client model for registration
const dataService = require("../services/dataService"); // Import dataService for static POIs
const idGenerator = require("../utils/idGeneratorOptimized"); // Import idGenerator for unique ID generation

/**
 * Handles incoming WebSocket messages, parsing JSON and routing to appropriate services.
 * @param {WebSocket} ws The WebSocket connection instance.
 * @param {Buffer} message The raw message buffer.
 */
async function handleWebSocketMessage(ws, message) {
  statsService.incrementMessagesReceived();
  const messageString = message.toString();
  const clientInfo = clientService.getClientByWs(ws) || { id: "unregistered", type: "unknown", connectionId: ws.connectionId };

  try {
    // Attempt to parse as JSON first
    const data = JSON.parse(messageString);
    if (data.type !== "ping") {
      logger.debug(`Received JSON message type: ${data.type}`, { connectionId: ws.connectionId, clientId: clientInfo.id });
    }

    // Validate basic structure
    if (!data || typeof data.type !== "string") {
      throw new Error("Invalid message format: 'type' field is missing or not a string.");
    }
    // Route based on message type
    switch (data.type) {
      case "ping":
        handlePing(ws, data);
        break;

      case "registration":
        handleRegistration(ws, data);
        break;

      case "pair":
        handlePairingRequest(ws, data, clientInfo);
        break;

      case "gps":
        handleGpsMessage(ws, data, clientInfo);
        break;

      case "getRoutingProfile":
        // Respond with hardcoded driving profile for routing
        logger.info(`Client ${clientInfo.id || ws.connectionId} requested routing profile.`);
        sendWsMessage(ws, {
          type: "routingProfile",
          profile: HARDCODED_DATA.drivingProfileForRouting,
        });
        break;

      case "getStaticPoisFlood": // New message type
        handleGetStaticPois(ws, data, clientInfo, "Flood");
        break;

      case "getStaticPoisFire": // New message type
        handleGetStaticPois(ws, data, clientInfo, "Fire");
        break;

      case "alert":
        handleAlertMessageBroadcast(ws, data);
        break;

      case "getSpoofedGps":
        handleGetSpoofedGps(ws, data, clientInfo);
        break;

      case "routePublish":
        handleRoutePublishMessage(ws, data, clientInfo);
        break;

      case "routeClear":
        handleRouteClearMessage(ws, data, clientInfo);
        break;

      case "deletePOI":
        broadcastToAllOthers(ws, {
          type: "deletePOI",
          poiId: data.poiId,
          meta: {
            source: "server",
            timestamp: new Date().toISOString(),
          },
        });
        logger.info(`Broadcasting deletePOI for ID: ${data.poiId}`, { connectionId: ws.connectionId, clientId: clientInfo.id });
        break;

      case "requestHardcodedData":
        SendHardcodedDataToClient(ws, clientInfo);
        break;

      default:
        logger.warn(`Received unknown JSON message type: ${data.type}`, { connectionId: ws.connectionId, clientId: clientInfo.id });
        sendWsMessage(ws, { type: "error", message: `Unknown message type: ${data.type}` });
        break;
    }
  } catch (error) {
    // If JSON parsing fails or handler throws error
    if (error instanceof SyntaxError) {
      // JSON parsing failed, attempt legacy handling
      logger.warn("Message is not valid JSON, attempting legacy handling.", { connectionId: ws.connectionId, messageStart: messageString.substring(0, 50) });
      legacyHandler.handleLegacyMessage(ws, messageString);
    } else {
      // Error occurred during message handling logic
      logger.error(`Error processing message type ${error.messageType || "unknown"} for client ${clientInfo.id}:`, error);
      statsService.recordError(error);
      // Send a generic error response back to the client
      sendWsMessage(ws, {
        type: "error",
        message: "Failed to process message on server.",
        details: error.message, // Send back the error message for debugging
        originalType: error.messageType, // Include original type if available
      });
    }
  }
}
//#region --- Specific Message Type Handlers ---

function SendHardcodedDataToClient(ws, clientInfo) {
  sendWsMessage(ws, {
    type: "hardcodedData",
    data: HARDCODED_DATA,
  });
}

function handleRegistration(ws, data) {
  const { deviceType, useCase, userName, deviceId } = data;
  let { clientId } = data; // Let in order to be able to generate a new one if not provided

  if (!deviceType) {
    logger.warn("Invalid registration message: Missing deviceType.", { data, connectionId: ws.connectionId });
    sendWsMessage(ws, { type: "registrationResult", status: "error", message: "Missing deviceType." });
    return;
  }

  if (deviceType === "phone" && !clientId) {
    logger.warn("Invalid registration message: Missing clientId for phone.", { data, connectionId: ws.connectionId });
    sendWsMessage(ws, { type: "registrationResult", status: "error", message: "Missing clientId for phone." });
    return;
  }

  const type = deviceType.toLowerCase();
  if (type !== "unity" && type !== "phone") {
    logger.warn(`Unsupported deviceType for registration: ${deviceType}`, { clientId, connectionId: ws.connectionId });
    sendWsMessage(ws, { type: "registrationResult", status: "error", message: `Unsupported deviceType: ${deviceType}` });
    return;
  }

  try {
    // If it is unity client and clientId is not provided, generate a new one
    // In the future we need to check if id was provided and if not it means that the pool is empty so we let the user know he has to wait
    // for a new id to be generated
    if (type === "unity" && !clientId) {
      clientId = idGenerator.generateUniqueId();
    } else if (type === "unity" && clientId) {
      if (!idGenerator.addID(clientId)) {
        clientId = idGenerator.generateUniqueId();
      }
    }

    // Create and register the client
    const client = new Client(ws, type, clientId, deviceId, userName, useCase);
    const added = clientService.addClient(client); // addClient handles logging and potential old connection closing

    if (added) {
      sendWsMessage(ws, { type: "registrationResult", status: "success", message: `Registered with uniqueID: ${clientId}`, clientId: clientId, clientType: type });
      logger.info(`${type} client registered successfully: ${clientId}`, { connectionId: ws.connectionId });

      if (type === "unity") {
        handleUnityRegistration(client, clientId, ws);
      } else if (type === "phone") {
        // Check if this phone completes any pending pairings
        pairingService.checkPendingPairingsForPhone(client);
      }
    } else {
      // addClient logs errors, but we can send a generic failure back
      sendWsMessage(ws, { type: "registrationResult", status: "error", message: "Registration failed on server (client not added)." });
    }
  } catch (error) {
    logger.error(`Error during registration for ${clientId} (${type}):`, error);
    statsService.recordError(error);
    sendWsMessage(ws, { type: "registrationResult", status: "error", message: `Server error during registration: ${error.message}` });
  }
}

function handleUnityRegistration(client, clientId, ws) {
  // For Unity clients, automatically try to connect to kafka configuration (CREXDATA).
  // In the future, data.kafkaConfigKey could come from the client registration message.
  const kafkaConfigToUse = client.useCase == "marine" ? require("../config").kafkaConfigsToUseForMarine : require("../config").kafkaConfigsToUse;

  logger.info(`Initiating Kafka consumer for ${clientId} with config: ${kafkaConfigToUse.join(", ")}`);

  kafkaConfigToUse.forEach((configKey) => {
    if (CONFIG[configKey]) {
      logger.info(`Initiating Kafka consumer for ${clientId} with config: ${configKey}`);
      kafkaService.createConsumerForClient(client, configKey).catch((err) => {
        logger.error(`Critical error initiating Kafka consumer creation for ${clientId} (config: ${configKey}):`, err);
        if (client.isConnected && ws.readyState === WebSocket.OPEN) {
          sendWsMessage(ws, { type: "kafkaStatus", configKey: configKey, status: "error", reason: `Server error setting up Kafka for ${configKey}: ${err.message}` });
        }
      });
    } else {
      logger.warn(`Kafka configuration key "${configKey}" not found in server config. Skipping for client ${clientId}.`);
      if (client.isConnected && ws.readyState === WebSocket.OPEN) {
        sendWsMessage(ws, { type: "kafkaStatus", configKey: configKey, status: "error", reason: `Server-side configuration for "${configKey}" not found.` });
      }
    }
  });
}

function handlePairingRequest(ws, data, clientInfo) {
  const { phoneId, unityId } = data;
  if (!phoneId || !unityId) {
    logger.warn("Invalid pair message: Missing phoneId or unityId.", { data, connectionId: ws.connectionId, clientId: clientInfo.id });
    sendWsMessage(ws, { type: "pairingStatus", status: "error", message: "Missing phoneId or unityId." });
    return;
  }

  logger.info(`Processing pairing request from ${clientInfo.type} ${clientInfo.id}: Phone ${phoneId} <-> Unity ${unityId}`);
  pairingService.pairDevices(phoneId, unityId); // This function handles logic and notifications
}

function handleGpsMessage(ws, data, clientInfo) {
  // Extract all fields from GPS message
  const { senderId, senderName, messageId, location, device, session, timestamp, metadata } = data;
  const { deviceId } = clientInfo;

  if (!senderId || !location || location.latitude === undefined || location.longitude === undefined) {
    logger.warn("Invalid GPS message: Missing senderId or location data.", { data, connectionId: ws.connectionId, clientId: clientInfo.id });
    return; // Don't send error back for potentially high-frequency messages
  }

  if (clientInfo.type !== "phone" || clientInfo.id !== senderId) {
    logger.warn(`GPS message senderId ${senderId} does not match connection client ${clientInfo.id} (${clientInfo.type}). Ignoring.`, { connectionId: ws.connectionId });
    return;
  }

  // Find the paired Unity client
  const phoneClient = clientService.getPhoneClient(senderId);
  if (!phoneClient || !phoneClient.pairedClientId) {
    logger.warn(`GPS message from unpaired or unknown phone: ${senderId}. Ignoring.`, { connectionId: ws.connectionId });
    return;
  }

  const targetUnityClient = clientService.getUnityClient(phoneClient.pairedClientId);
  if (targetUnityClient && targetUnityClient.ws.readyState === WebSocket.OPEN) {
    // Prepare comprehensive GPS message
    const gpsMessage = {
      type: "gps",
      messageId: messageId,

      // Sender information
      senderId: senderId,
      senderName: senderName,
      senderDeviceId: deviceId,
      targetDeviceId: targetUnityClient.deviceId,

      // Location data
      location: {
        latitude: location.latitude,
        longitude: location.longitude,
        altitude: location.altitude,
        horizontalAccuracy: location.horizontalAccuracy,
        verticalAccuracy: location.verticalAccuracy,
        timestamp: location.timestamp,
      },

      // Device information
      device: {
        model: device?.model,
        os: device?.os,
        batteryLevel: device?.batteryLevel,
        batteryStatus: device?.batteryStatus,
      },

      // Session information
      session: {
        startTime: session?.startTime,
        uptime: session?.uptime,
      },

      // Timestamp
      timestamp: timestamp,

      // Metadata
      metadata: {
        updateInterval: metadata?.updateInterval,
        source: metadata?.source || "live",
        relayedBy: "server",
      },
    };

    // Forward the GPS data to the paired Unity client
    sendWsMessage(targetUnityClient.ws, gpsMessage);

    logger.debug(`Relayed GPS from ${senderId} (${senderName}) to Unity ${targetUnityClient.id}`, {
      lat: location.latitude,
      lon: location.longitude,
      accuracy: location.horizontalAccuracy,
      battery: device?.batteryLevel,
    });

    // Publish GPS data to Kafka in event-driven format
    const kafkaConfigToUse = topicForGPSData;

    // Format GPS message for Kafka using event-driven format
    const formattedKafkaMessage = messageFormatter.formatGpsForKafka({
      messageId,
      senderId,
      senderName,
      deviceId,
      targetDeviceId: targetUnityClient.deviceId,
      location,
      device,
      session,
      timestamp,
      metadata: {
        ...metadata,
        relayedBy: "server",
      },
    });

    kafkaService
      .publishToKafka(kafkaConfigToUse, formattedKafkaMessage, {
        key: senderId, // Use senderId as partition key for consistent routing
        headers: {
          source: "ar-websocket-server",
          messageType: "gps",
          eventType: "LOCATION_UPDATE",
          senderId: senderId,
          timestamp: timestamp || new Date().toISOString(),
        },
      })
      .then((result) => {
        logger.debug(`GPS data published to Kafka successfully`, {
          senderId,
          topic: result.topic,
          partition: result.partition,
          offset: result.offset,
        });
      })
      .catch((error) => {
        // Log error but don't fail the GPS relay to Unity client
        logger.error(`Failed to publish GPS data to Kafka for ${senderId}:`, error);
        // Note: Error is already recorded by kafkaService, no need to duplicate
      });
  } else {
    logger.warn(`Cannot relay GPS: Target Unity client ${phoneClient.pairedClientId} not found or not connected for phone ${senderId}.`);
  }
}

/**
 * Handles a request from a client to get all static POIs.
 * @param {WebSocket} ws The WebSocket connection instance.
 * @param {object} data The parsed message data.
 * @param {Client | object} clientInfo Information about the connected client.
 */
function handleGetStaticPois(ws, data, clientInfo, type) {
  logger.info(`Client ${clientInfo.id || ws.connectionId} requested static POIs.`);

  const pois = dataService.getStaticPois(type);

  if (!pois || pois.length === 0) {
    logger.info("No static POIs available to send.", { clientId: clientInfo.id });
    sendWsMessage(ws, { type: "staticPoisResult", status: "empty", message: "No static POIs found on server." });
    return;
  }

  logger.info(`Sending ${pois.length} static POIs to client ${clientInfo.id || ws.connectionId}.`);
  // Send each POI as a separate 'spawnPOI' message
  pois.forEach((poi, index) => {
    const formattedMessage = {
      type: type == "Flood" ? "spawnPOI" : "addPOI",
      payload: poi,
      meta: {
        source: "static",
        index: index,
        total: pois.length,
        timestamp: new Date().toISOString(),
      },
    };
    sendWsMessage(ws, formattedMessage);
  });

  // Optionally, send a confirmation that all static POIs have been sent
  sendWsMessage(ws, {
    type: "staticPoisResult",
    status: "success",
    count: pois.length,
    message: `Sent ${pois.length} static POIs.`,
  });
}

function handlePing(ws, data) {
  // Respond to custom JSON ping with pong including original timestamp for latency calculation
  const response = {
    type: "pong",
  };

  // Include timestamp if client sent one for latency measurement
  if (data.timestamp) {
    response.timestamp = data.timestamp;
    response.serverTime = Date.now(); // Add server timestamp for comparison
  }

  sendWsMessage(ws, response);
  // Note: This is separate from WebSocket-level ping/pong which is handled automatically
}

function handleAlertMessageBroadcast(ws, data) {
  broadcastToAllOthers(ws, {
    type: "alert",
    alertType: data.alertType,
    alertID: data.alertID,
    meta: {
      source: "server",
      timestamp: new Date().toISOString(),
    },
  });
}

function handleGetSpoofedGps(ws, data, clientInfo) {
  // Respond with spoofed GPS coordinates for drone
  if (clientInfo.type !== "unity") {
    logger.warn(`Spoofed GPS request from non-unity client: ${clientInfo.id} (${clientInfo.type}). Ignoring.`);
    return;
  }

  const spoofedCoords = data.usecase == "Drone" ? HARDCODED_DATA.spoofedGPSCoordinatesForDrone : HARDCODED_DATA.spoofedGPSCoordinatesForPhone;
  if (!spoofedCoords) {
    logger.error("Spoofed GPS coordinates not configured in server settings.");
    sendWsMessage(ws, { type: "error", message: "Server error: Spoofed GPS coordinates not configured." });
    return;
  }

  sendWsMessage(ws, {
    type: "spoofedGps",
    payload: {
      latitude: spoofedCoords.latitude,
      longitude: spoofedCoords.longitude,
    },
    meta: {
      source: "server",
      usecase: spoofedCoords.usecase || "unknown",
      timestamp: new Date().toISOString(),
    },
  });
}

/**
 * Handles route publish messages from Unity clients
 * Formats and publishes route data to Kafka for partner map integration
 * @param {WebSocket} ws The WebSocket connection
 * @param {object} data The route publish data
 * @param {object} clientInfo Client information
 */
function handleRoutePublishMessage(ws, data, clientInfo) {
  const { unityId, timestamp, routeInfo } = data;

  if (!routeInfo || !routeInfo.geometry) {
    logger.warn("Invalid route publish message: Missing routeInfo or geometry.", {
      data,
      connectionId: ws.connectionId,
      clientId: clientInfo.id,
    });
    return;
  }

  const formatterClientInfo = {
    userId: clientInfo.id || unityId,
    userName: clientInfo.name || `User ${unityId}`,
    deviceId: clientInfo.deviceId,
    deviceType: clientInfo.type || "unity-client",
  };

  const formattedKafkaMessage = messageFormatter.formatRoutePublishForKafka(data, formatterClientInfo);

  // Publish to Kafka
  const kafkaConfigToUse = topicForRouteData;

  kafkaService
    .publishToKafka(kafkaConfigToUse, formattedKafkaMessage, {
      key: unityId || clientInfo.id, // Partition by user for ordered routing
      headers: {
        source: "ar-tuc-server",
        messageType: "routePublish",
        eventType: "ROUTE_PUBLISHED",
        userId: unityId || clientInfo.id,
        timestamp: timestamp || new Date().toISOString(),
      },
    })
    .then((result) => {
      logger.info(`Route published to Kafka successfully`, {
        userId: unityId || clientInfo.id,
        userName: clientInfo.name,
        topic: result.topic,
        partition: result.partition,
        offset: result.offset,
        distance: routeInfo.distance,
        waypoints: routeInfo.waypointCount,
      });
    })
    .catch((error) => {
      logger.error(`Failed to publish route to Kafka for ${unityId || clientInfo.id}:`, error);
      statsService.recordError(error);
    });
}

/**
 * Handles route clear messages from Unity clients
 * Publishes route clear event to Kafka for partner map integration
 * @param {WebSocket} ws The WebSocket connection
 * @param {object} data The route clear data
 * @param {object} clientInfo Client information
 */
function handleRouteClearMessage(ws, data, clientInfo) {
  const { unityId, timestamp, routeId } = data;

  logger.info(`Client ${clientInfo.id} (${clientInfo.name || unityId}) requested routeClear`);

  const formatterClientInfo = {
    userId: clientInfo.id || unityId,
    userName: clientInfo.name || `User ${unityId}`,
    deviceId: clientInfo.deviceId,
    deviceType: clientInfo.type || "unity-client",
  };

  const formattedKafkaMessage = messageFormatter.formatRouteClearForKafka(data, formatterClientInfo);

  const kafkaConfigToUse = topicForRouteData;

  kafkaService
    .publishToKafka(kafkaConfigToUse, formattedKafkaMessage, {
      key: unityId || clientInfo.id,
      headers: {
        source: "ar-websocket-server",
        messageType: "routeClear",
        eventType: "ROUTE_CLEARED",
        userId: unityId || clientInfo.id,
        timestamp: timestamp || new Date().toISOString(),
      },
    })
    .then((result) => {
      logger.info(`Route clear published to Kafka successfully`, {
        userId: unityId || clientInfo.id,
        userName: clientInfo.name,
        topic: result.topic,
        partition: result.partition,
        offset: result.offset,
      });
    })
    .catch((error) => {
      logger.error(`Failed to publish route clear to Kafka for ${unityId || clientInfo.id}:`, error);
      statsService.recordError(error);
    });
}
//#endregion --- Specific Message Type Handlers ---

module.exports = {
  handleWebSocketMessage,
};
