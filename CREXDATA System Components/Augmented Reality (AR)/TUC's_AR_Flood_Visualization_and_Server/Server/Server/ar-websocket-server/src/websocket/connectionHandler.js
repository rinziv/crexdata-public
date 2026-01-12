const WebSocket = require("ws");
const logger = require("../utils/logger");
const { generateUniqueId } = require("../utils/helpers");
const { sendWsMessage } = require("./wsUtils");
const { handleWebSocketMessage } = require("./messageHandler");
const clientService = require("../services/clientService");
const pairingService = require("../services/pairingService");
const kafkaService = require("../services/kafkaService");
const statsService = require("../services/statsService");
const heartbeatService = require("../services/heartbeatService");

/**
 * Handles new incoming WebSocket connections.
 * @param {WebSocket} ws The WebSocket connection instance.
 * @param {import('http').IncomingMessage} req The initial HTTP request.
 */
function handleNewConnection(ws, req) {
  statsService.incrementTotalConnections(); // Track connection attempt
  statsService.recordLastConnectionTime(); // Update last connection time

  // Extract connection details
  const connectionId = generateUniqueId();
  // Use 'x-forwarded-for' header if behind a proxy, otherwise fallback to socket address
  const ipAddress = req.headers["x-forwarded-for"]?.split(",")[0].trim() || req.socket.remoteAddress || req.connection?.remoteAddress || "unknown";
  const userAgent = req.headers["user-agent"] || "unknown";
  const connectionTime = new Date().toISOString();

  // Attach info to the WebSocket object for easier access later
  ws.connectionId = connectionId;
  ws.ipAddress = ipAddress;
  ws.connectionTime = connectionTime;
  ws.userAgent = userAgent;
  // ws.clientInfo = null;

  logger.info("New WebSocket connection opened", { connectionId, ipAddress, userAgent });

  // Start heartbeat monitoring for this connection
  heartbeatService.startHeartbeat(ws);

  // Send acknowledgment back to the client
  sendWsMessage(ws, {
    type: "connectionAck",
    message: "Connection established. Please register.",
    connectionId: connectionId,
    serverTime: connectionTime,
  });

  // --- Event Listeners for this connection ---

  // Handle incoming messages
  ws.on("message", (message) => {
    // Delegate message handling to the messageHandler module
    handleWebSocketMessage(ws, message).catch((error) => {
      // Catch errors specifically from the async message handler if not caught internally
      logger.error(`Unhandled error during message processing for ${connectionId}:`, error);
      statsService.recordError(error);
    });
  });

  // Handle connection closure
  ws.on("close", async (code, reason) => {
    const reasonStr = reason ? reason.toString() : "No reason given";
    logger.info(`WebSocket connection closed: ${connectionId}`, { code, reason: reasonStr, ipAddress });

    // Stop heartbeat monitoring for this connection
    heartbeatService.stopHeartbeat(connectionId);

    // removeClientByWs finds the client, removes it from the service map, and marks it disconnected.
    // It does NOT decrement statsService.currentConnections anymore (see change in step 4).
    const disconnectedClient = clientService.removeClientByWs(ws);

    if (disconnectedClient) {
      logger.info(`Registered client ${disconnectedClient.type} ${disconnectedClient.id} disconnected. Processing cleanup.`, { connectionId });
      // disconnectedClient.markDisconnected(); // Already done by removeClientByWs

      // Perform cleanup related to the specific client type
      if (pairingService.handleDisconnectionPairingCleanup) {
        try {
          pairingService.handleDisconnectionPairingCleanup(disconnectedClient);
          logger.debug(`Pairing cleanup processed for ${disconnectedClient.id}`);
        } catch (pairingError) {
          logger.error(`Error during pairing cleanup for ${disconnectedClient.id}:`, pairingError);
        }
      }

      // Disconnect ALL Kafka consumers for this client
      try {
        await kafkaService.disconnectAllConsumersForClient(disconnectedClient);
      } catch (kafkaErr) {
        logger.error(`Error during Kafka consumer cleanup for ${disconnectedClient.id}:`, kafkaErr);
      }

      statsService.decrementCurrentConnections(); // Decrement for the registered client that was cleaned up
      logger.info(`Cleanup completed for registered client ${disconnectedClient.type} ${disconnectedClient.id}.`, { connectionId });
    } else {
      logger.info(`Disconnected WebSocket was not a registered client or already removed.`, { connectionId });
    }
  });

  // Handle connection errors
  ws.on("error", (error) => {
    logger.error(`WebSocket connection error for ${connectionId}:`, error);
    statsService.recordError(error);

    // Stop heartbeat monitoring
    heartbeatService.stopHeartbeat(connectionId);

    if (ws.readyState !== WebSocket.CLOSED && ws.readyState !== WebSocket.CLOSING) {
      logger.warn(`Forcing close for connection ${connectionId} after error.`);
      ws.terminate(); // Force close the connection, should trigger 'close' event for cleanup
      // Call the fallback cleanup with error handling
      handleDisconnectionCleanupOnError(ws).catch((cleanupError) => {
        logger.error(`Error in handleDisconnectionCleanupOnError for ${connectionId}:`, cleanupError);
      });
    }
  });
}

/**
 * Helper function to ensure cleanup happens even if 'close' event doesn't fire after an error.
 * This duplicates some logic from the 'close' handler but is safer in error scenarios.
 * @param {WebSocket} ws
 */
async function handleDisconnectionCleanupOnError(ws) {
  logger.info(`Performing fallback cleanup for connection ${ws.connectionId} after error (if 'close' event didn't handle).`);
  const disconnectedClient = clientService.getClientByWs(ws); // Get client without removing

  if (disconnectedClient) {
    if (!disconnectedClient.isConnected) {
      logger.info(`Client ${disconnectedClient.id} already marked disconnected, skipping error cleanup.`);
      return;
    }
    // Manually perform the steps because 'close' might not have run or completed
    disconnectedClient.markDisconnected();
    // Explicitly remove from map here if not already handled by a 'close' event that might still fire
    const clientStillInMap = clientService.getClientByWs(ws);
    if (clientStillInMap) {
      clientService.removeClientByWs(ws); // This will also call markDisconnected again, which is fine.
      // It will also log removal again if called here.
    }

    if (pairingService.handleDisconnectionPairingCleanup) {
      try {
        pairingService.handleDisconnectionPairingCleanup(disconnectedClient);
        logger.debug(`Pairing cleanup on error processed for ${disconnectedClient.id}`);
      } catch (pairingError) {
        logger.error(`Pairing cleanup error on error handler for ${disconnectedClient.id}:`, pairingError);
      }
    }

    try {
      await kafkaService.disconnectAllConsumersForClient(disconnectedClient);
    } catch (err) {
      logger.error(`Error during Kafka consumer disconnect on error cleanup for ${disconnectedClient.id}:`, err);
    }
    statsService.decrementCurrentConnections();
    logger.info(`Client ${disconnectedClient.type} ${disconnectedClient.id} fully removed after error cleanup.`, { connectionId: ws.connectionId });
  } else {
    logger.info(`Unregistered client or client already removed during error cleanup for ${ws.connectionId}.`);
  }
}

module.exports = {
  handleNewConnection,
};
