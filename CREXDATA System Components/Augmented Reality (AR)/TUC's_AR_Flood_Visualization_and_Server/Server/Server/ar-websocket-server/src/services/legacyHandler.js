const WebSocket = require("ws");
const logger = require("../utils/logger");
const clientService = require("./clientService");
const kafkaService = require("./kafkaService");
const dataService = require("./dataService"); // For flood data
const { sendWsMessage } = require("../websocket/wsUtils");

/**
 * Main entry point for handling messages that fail JSON parsing or are known legacy formats.
 * @param {WebSocket} ws The WebSocket connection instance.
 * @param {string} messageStr The raw message string.
 */
function handleLegacyMessage(ws, messageStr) {
  logger.warn(`Handling message as legacy format: ${messageStr.substring(0, 50)}...`, { connectionId: ws.connectionId });
  const separatedStrings = messageStr.split(","); // Common legacy delimiter

  // Get client info if available (might be needed by handlers)
  const client = clientService.getClientByWs(ws);
  const clientId = client ? client.id : "unknown"; // Use client ID if registered

  try {
    if (!separatedStrings || separatedStrings.length === 0) {
      logger.warn("Received empty or invalid legacy message.", { connectionId: ws.connectionId });
      return;
    }

    const messageType = separatedStrings[0].toLowerCase().trim();

    // Route based on the first part of the message
    if (messageType === "flood") {
      handleFloodDataRequest(ws, separatedStrings);
    } else if (messageType === "seek") {
      // Seek requires knowing which client's consumer to act on
      if (client && client.type === "unity") {
        handleKafkaSeek(client.id, separatedStrings);
      } else {
        logger.warn(`Received legacy 'seek' from non-unity or unregistered client. Ignoring.`, { connectionId: ws.connectionId });
      }
    } else if (messageType === "phone") {
      // This seems like a legacy registration method, prefer JSON registration
      handlePhoneConnectionLegacy(ws, separatedStrings);
    } else if (messageType.includes("gps")) {
      // Check if "gps" is part of the first element
      // Legacy GPS format might be "gps:phoneId,lat:value,lon:value"
      handleGpsDataLegacy(ws, messageStr, separatedStrings); // Pass original string too if needed
    } else {
      logger.warn(`Unknown legacy message type: ${messageType}`, { connectionId: ws.connectionId, fullMessage: messageStr.substring(0, 100) });
    }
  } catch (error) {
    logger.error(`Error processing legacy message for ${clientId}:`, { message: messageStr.substring(0, 100), error });
    // Avoid sending error back for legacy messages unless necessary
    // sendWsMessage(ws, JSON.stringify({ type: "error", message: "Failed to process legacy message" }));
  }
}

// --- Specific Legacy Handlers ---

function handleFloodDataRequest(ws, data) {
  logger.debug("Processing legacy flood data request", { data });
  try {
    // Example logic assumes data[0] contains map info and data[1] contains time HH:MM
    const mapKey = data[0].includes("1") ? "c9r7" : "c8r8"; // Example mapping
    const time = data[1];
    const values = dataService.getValuesForTime(time, mapKey);
    // Send back in the expected legacy format
    sendWsMessage(ws, `${values},water`);
  } catch (error) {
    logger.error("Error handling legacy flood data request:", error);
    // sendWsMessage(ws, "error,null,null,null,water"); // Send error response if needed
  }
}

function handleKafkaSeek(clientId, data) {
  logger.debug(`Processing legacy Kafka seek request for client ${clientId}`, { data });
  try {
    // Assuming data format: "seek", partition, offset
    if (data.length >= 3) {
      const partition = parseInt(data[1], 10);
      const offset = data[2]; // Keep offset as string for KafkaJS
      if (!isNaN(partition) && offset) {
        kafkaService.seekConsumerOffset(clientId, partition, offset);
      } else {
        logger.warn(`Invalid partition or offset in legacy seek request for ${clientId}`, { data });
      }
    } else {
      logger.warn(`Insufficient data in legacy seek request for ${clientId}`, { data });
    }
  } catch (error) {
    logger.error(`Error handling legacy Kafka seek for client ${clientId}:`, error);
  }
}

function handlePhoneConnectionLegacy(ws, data) {
  // This is highly discouraged. Clients should use JSON registration.
  // If absolutely necessary, you might try to register the client here.
  logger.warn("Received legacy phone connection attempt. Please update client to use JSON registration.", { connectionId: ws.connectionId });
  // Example: Try to extract an ID and register
  // if (data.length > 1 && data[1]) {
  //     const phoneId = data[1].trim();
  //     // Check if already registered via JSON
  //     if (!clientService.getPhoneClient(phoneId)) {
  //         logger.info(`Attempting legacy registration for phone ${phoneId}`);
  //         const client = new (require('../models/Client'))(ws, 'phone', phoneId);
  //         // Need to manually add connection info if not done in connectionHandler
  //         ws.connectionId = ws.connectionId || require('../utils/helpers').generateUniqueId();
  //         ws.ipAddress = ws.ipAddress || 'unknown (legacy)';
  //         ws.connectionTime = ws.connectionTime || new Date().toISOString();
  //         ws.userAgent = ws.userAgent || 'unknown (legacy)';
  //         clientService.addClient(client);
  //         // Manually check pairings
  //         require('./pairingService').checkPendingPairingsForPhone(client);
  //     } else {
  //          logger.warn(`Legacy phone connection ignored, client ${phoneId} already registered.`);
  //     }
  // }
}

function handleGpsDataLegacy(ws, messageStr, separatedStrings) {
  logger.debug("Processing legacy GPS data", { message: messageStr.substring(0, 100) });
  try {
    // Attempt to parse the specific legacy format: "gps:phoneId", "lat:value", "lon:value"
    if (separatedStrings.length >= 3) {
      const phoneIdPart = separatedStrings[0].split(":")[1];
      const latPart = separatedStrings[1].split(":")[1];
      const lonPart = separatedStrings[2]; // Longitude might not have a label

      const phoneId = phoneIdPart?.trim();
      const lat = latPart?.trim();
      const lon = lonPart?.trim();

      if (phoneId && lat && lon) {
        const phoneClient = clientService.getPhoneClient(phoneId);
        if (phoneClient && phoneClient.pairedClientId) {
          const targetUnityClient = clientService.getUnityClient(phoneClient.pairedClientId);
          if (targetUnityClient && targetUnityClient.ws.readyState === WebSocket.OPEN) {
            // Send in the expected legacy format to Unity
            sendWsMessage(targetUnityClient.ws, `${lat},${lon}, gps`);
            logger.debug(`Relayed legacy GPS from ${phoneId} to Unity ${targetUnityClient.id}`);
          } else {
            logger.warn(`Legacy GPS: Target Unity client ${phoneClient.pairedClientId} not found or not connected for phone ${phoneId}`);
          }
        } else {
          logger.warn(`Legacy GPS: Source phone ${phoneId} not found or not paired.`);
        }
      } else {
        logger.warn("Could not parse legacy GPS data format.", { separatedStrings });
      }
    } else {
      logger.warn("Insufficient data in legacy GPS message.", { separatedStrings });
    }
  } catch (error) {
    logger.error("Error handling legacy GPS data:", error);
  }
}

module.exports = {
  handleLegacyMessage,
};
