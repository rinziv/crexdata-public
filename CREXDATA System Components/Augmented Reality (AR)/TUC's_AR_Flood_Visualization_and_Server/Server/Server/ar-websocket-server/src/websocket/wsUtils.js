const WebSocket = require("ws");
const clientService = require("../services/clientService");
const logger = require("../utils/logger");
const statsService = require("../services/statsService");

/**
 * Sends a message through a WebSocket connection if it's open.
 * Automatically stringifies JSON objects.
 * @param {WebSocket} ws The WebSocket client instance.
 * @param {string | object} message The message to send (string or JSON object).
 * @returns {boolean} True if the message was sent successfully, false otherwise.
 */
function sendWsMessage(ws, message) {
  if (!ws || ws.readyState !== WebSocket.OPEN) {
    logger.warn("Attempted to send message to closed or invalid WebSocket.", { connectionId: ws?.connectionId });
    return false;
  }
  try {
    const messageToSend = typeof message === "string" ? message : JSON.stringify(message);
    ws.send(messageToSend);
    statsService.incrementMessagesSent();
    if (typeof message !== "object" || message.type !== "pong") {
      logger.debug("WebSocket message sent", { connectionId: ws.connectionId, type: typeof message === "object" ? message.type : "raw" });
    }
    return true;
  } catch (error) {
    statsService.recordError(error);
    logger.error("Error sending WebSocket message:", { connectionId: ws.connectionId, error: error.message });
    return false;
  }
}

/**
 * Broadcasts a message to all connected clients, optionally filtering by type.
 * @param {WebSocket.Server} wss The WebSocket server instance.
 * @param {string | object} message The message to send.
 * @param {string | null} [targetType=null] Optional client type ('unity', 'phone') to target.
 */
function broadcast(wss, message, targetType = null) {
  if (!wss) {
    logger.error("Cannot broadcast: WebSocket server instance is missing.");
    return;
  }
  let clientCount = 0;
  const messageToSend = typeof message === "string" ? message : JSON.stringify(message);

  // Note: This iterates over wss.clients, which might not have our custom Client object directly.
  // If filtering is needed based on our Client model, we need access to the clientService map.
  // For now, this basic broadcast sends to all connected sockets.
  // A more robust broadcast would likely involve clientService.getAllClients().
  wss.clients.forEach((client) => {
    // Basic check if the socket is open
    if (client.readyState === WebSocket.OPEN) {
      sendWsMessage(client, messageToSend); // Send to all open sockets for now
      clientCount++;
    }
  });
  logger.debug(`Broadcast message sent to ${clientCount} clients`, { type: typeof message === "object" ? message.type : "raw", targetType });
}

function broadcastToAllOthers(senderWs, message) {
  if (!senderWs || senderWs.readyState !== WebSocket.OPEN) {
    logger.warn("Attempted to broadcast from a closed or invalid WebSocket.", { connectionId: senderWs?.connectionId });
    return;
  }
  let clientCount = 0;
  const messageToSend = typeof message === "string" ? message : JSON.stringify(message);

  // Broadcast to all clients on the clientService map except the sender
  clientService.getAllClients().forEach((client) => {
    if (client.ws && client.ws.readyState === WebSocket.OPEN) {
      // && client.ws !== senderWs) {
      sendWsMessage(client.ws, messageToSend);
      clientCount++;
    }
  });
  logger.debug(`Broadcast to others sent to ${clientCount} clients`, { type: typeof message === "object" ? message.type : "raw" });
}

module.exports = {
  sendWsMessage,
  broadcast,
  broadcastToAllOthers,
};
