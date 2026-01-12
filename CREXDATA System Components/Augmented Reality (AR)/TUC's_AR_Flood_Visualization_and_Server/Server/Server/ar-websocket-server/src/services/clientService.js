const WebSocket = require("ws");
const Client = require("../models/Client");
const logger = require("../utils/logger");
const { getCompositeKey } = require("../utils/helpers");
const statsService = require("./statsService");
const idGenerator = require("../utils/idGeneratorOptimized");

// Store all connected clients (Client instances) using composite key "type:id"
const clients = new Map();

/**
 * Adds or updates a client in the central map.
 * @param {Client} clientInstance An instance of the Client class.
 * @returns {boolean} True if added/updated, false otherwise.
 */
function addClient(clientInstance) {
  if (!(clientInstance instanceof Client)) {
    logger.error("Attempted to add invalid client object", { clientInstance });
    return false;
  }
  const key = getCompositeKey(clientInstance.type, clientInstance.id);
  if (!key) return false;

  // Check if a client with the same key already exists (e.g., reconnection)
  const existingClient = clients.get(key);
  if (existingClient && existingClient.ws !== clientInstance.ws) {
    logger.warn(`Client with key ${key} already exists. Closing old connection.`, { oldConnectionId: existingClient.connectionId, newConnectionId: clientInstance.connectionId });
    // Optionally send a message to the old client before closing
    existingClient.ws.close(1000, "Replaced by new connection");
    // Note: The 'close' event handler for the old connection should handle cleanup.
  }

  clients.set(key, clientInstance);
  statsService.incrementCurrentConnections(); // Increment active connections count
  logger.info(`Client added/updated: ${key}`, { connectionId: clientInstance.connectionId, ip: clientInstance.ipAddress });
  return true;
}

/**
 * Removes a client from the map using its WebSocket object.
 * Also performs cleanup like marking the client as disconnected.
 * @param {WebSocket} ws The WebSocket object of the client to remove.
 * @returns {Client | null} The removed Client object or null if not found.
 */
function removeClientByWs(ws) {
  let removedClient = null;
  let clientKey = null;

  for (const [key, client] of clients.entries()) {
    if (client.ws === ws) {
      clientKey = key;
      removedClient = client;
      break;
    }
  }

  if (removedClient && clientKey) {
    clients.delete(clientKey);
    removedClient.markDisconnected(); // Mark internal state as disconnected

    const clientId = Number(removedClient.id); // Ensure ID is a number
    idGenerator.removeId(clientId); // Remove ID from the pool

    logger.info(`Client removed: ${clientKey}`, { connectionId: removedClient.connectionId });
  } else {
    logger.warn("Attempted to remove client by WS, but not found.", { connectionId: ws?.connectionId });
  }
  return removedClient;
}

/**
 * Retrieves a client by its type and ID.
 * @param {string} type Client type ('unity', 'phone').
 * @param {string} id Client ID.
 * @returns {Client | undefined} The Client object or undefined if not found.
 */
function getClientByTypeAndId(type, id) {
  const key = getCompositeKey(type, id);
  if (!key) return undefined;
  return clients.get(key);
}

/**
 * Retrieves a phone client by its ID.
 * @param {string} id Phone client ID.
 * @returns {Client | undefined}
 */
function getPhoneClient(id) {
  return getClientByTypeAndId("phone", id);
}

/**
 * Retrieves a Unity client by its ID.
 * @param {string} id Unity client ID.
 * @returns {Client | undefined}
 */
function getUnityClient(id) {
  return getClientByTypeAndId("unity", id);
}

/**
 * Finds a client associated with a specific WebSocket connection.
 * @param {WebSocket} ws The WebSocket object.
 * @returns {Client | undefined} The found Client object or undefined.
 */
function getClientByWs(ws) {
  for (const client of clients.values()) {
    if (client.ws === ws) {
      return client;
    }
  }
  return undefined;
}

/**
 * Gets a list of all currently connected clients.
 * @returns {Client[]} An array of Client objects.
 */
function getAllClients() {
  // Filter out any clients that might somehow still be in the map but disconnected
  return Array.from(clients.values()).filter((client) => client.isConnected && client.ws.readyState === WebSocket.OPEN);
}

module.exports = {
  addClient,
  removeClientByWs,
  getClientByTypeAndId,
  getPhoneClient,
  getUnityClient,
  getClientByWs,
  getAllClients,
  // Expose the map directly if needed
  // _clientsMap: clients,
};
