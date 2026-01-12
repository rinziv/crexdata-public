const logger = require("../utils/logger");
const clientService = require("./clientService");
const { sendWsMessage } = require("../websocket/wsUtils");

// Stores pending pairings: Map<unityId, phoneId>
const pendingPairings = new Map();
// Stores active pairings: Map<phoneId, unityId> (or could use client.pairedClientId)
// Using client.pairedClientId is likely sufficient and avoids redundant state.
// const activePairings = new Map(); // Let's rely on client.pairedClientId

/**
 * Attempts to pair a phone and a Unity client.
 * Handles cases where one client might not be connected yet (pending).
 * @param {string} phoneId
 * @param {string} unityId
 * @returns {boolean} True if pairing is successful immediately, false if pending or failed.
 */
function pairDevices(phoneId, unityId) {
  const phoneClient = clientService.getPhoneClient(phoneId);
  const unityClient = clientService.getUnityClient(unityId);

  if (!unityId || !phoneId) {
    logger.warn("Pairing attempt failed: Missing phoneId or unityId.", { phoneId, unityId });
    return false;
  }

  // Case 1: Unity client doesn't exist (or isn't registered yet)
  if (!unityClient) {
    logger.warn(`Cannot pair: Unity client ${unityId} not found or not registered.`);
    // Optionally notify the phone if it exists
    if (phoneClient) {
      sendWsMessage(phoneClient.ws, { type: "pairingStatus", status: "error", reason: `Unity client ${unityId} not found.` });
    }
    return false;
  }

  // Case 2: Phone client doesn't exist (or isn't registered yet) -> Create Pending Pairing
  if (!phoneClient) {
    if (pendingPairings.has(unityId)) {
      // Avoid duplicate pending requests if phoneId is the same
      if (pendingPairings.get(unityId) === phoneId) {
        logger.info(`Pairing request already pending for Unity ${unityId} waiting for Phone ${phoneId}`);
      } else {
        // Update the pending request if a different phoneId is requested
        logger.info(`Updating pending pairing for Unity ${unityId} to wait for Phone ${phoneId} (was ${pendingPairings.get(unityId)})`);
        pendingPairings.set(unityId, phoneId);
      }
    } else {
      pendingPairings.set(unityId, phoneId);
      logger.info(`Added pending pairing: Unity ${unityId} waiting for Phone ${phoneId}`);
    }

    // Notify Unity client it's waiting
    sendWsMessage(unityClient.ws, { type: "pairingStatus", status: "waiting_for_phone", phoneId: phoneId });
    return false; // Pairing is not complete yet
  }

  // Case 3: Both clients exist - Complete the pairing
  logger.info(`Attempting to establish pairing: Phone ${phoneId} with Unity ${unityId}`);

  // Check if either is already paired differently
  if (phoneClient.pairedClientId && phoneClient.pairedClientId !== unityId) {
    logger.warn(`Phone ${phoneId} is already paired with ${phoneClient.pairedClientId}. Cannot re-pair with ${unityId}.`);
    sendWsMessage(phoneClient.ws, { type: "pairingStatus", status: "error", reason: `Already paired with ${phoneClient.pairedClientId}` });
    sendWsMessage(unityClient.ws, { type: "pairingStatus", status: "error", reason: `Phone ${phoneId} already paired elsewhere.` });
    return false;
  }
  if (unityClient.pairedClientId && unityClient.pairedClientId !== phoneId) {
    logger.warn(`Unity ${unityId} is already paired with ${unityClient.pairedClientId}. Cannot re-pair with ${phoneId}.`);
    sendWsMessage(unityClient.ws, { type: "pairingStatus", status: "error", reason: `Already paired with ${unityClient.pairedClientId}` });
    sendWsMessage(phoneClient.ws, { type: "pairingStatus", status: "error", reason: `Unity ${unityId} already paired elsewhere.` });
    return false;
  }

  phoneClient.setPairedClient(unityId);
  unityClient.setPairedClient(phoneId);

  //Update unity client name to be the same as the phone client name
  unityClient.name = phoneClient.name;

  // Remove from pending if it existed
  if (pendingPairings.has(unityId)) {
    pendingPairings.delete(unityId);
    logger.info(`Removed pending pairing for Unity ${unityId} as Phone ${phoneId} connected.`);
  }

  logger.info(`Pairing established: Phone ${phoneId} (Conn: ${phoneClient.connectionId}) with Unity ${unityId} (Conn: ${unityClient.connectionId})`);

  // Notify both clients
  sendWsMessage(phoneClient.ws, { type: "paired", status: "success", pairedWith: unityId });
  sendWsMessage(unityClient.ws, { type: "paired", status: "success", pairedWith: phoneId });

  return true; // Pairing successful
}

/**
 * Checks if a newly registered phone client completes any pending pairings.
 * @param {Client} phoneClient The newly registered phone client instance.
 */
function checkPendingPairingsForPhone(phoneClient) {
  if (phoneClient.type !== "phone") return;

  const phoneId = phoneClient.id;
  let completedPairing = false;

  // Iterate through pending pairings to see if any are waiting for this phoneId
  for (const [unityId, pendingPhoneId] of pendingPairings.entries()) {
    if (pendingPhoneId === phoneId) {
      logger.info(`Phone ${phoneId} connected, attempting to complete pending pairing with Unity ${unityId}`);
      const success = pairDevices(phoneId, unityId); // This will handle notifications and remove from pending
      if (success) {
        completedPairing = true;
        // Assuming a phone pairs with only one Unity device at a time, we can break
        break;
      } else {
        logger.warn(`Failed to complete pending pairing for Phone ${phoneId} and Unity ${unityId} despite phone connecting.`);
        // Keep the pending pairing for now, maybe the Unity client disconnected? pairDevices should handle logging.
      }
    }
  }
  if (!completedPairing) {
    logger.info(`Phone ${phoneId} registered, no pending pairings found waiting for it.`);
  }
}

/**
 * Cleans up pairings when a client disconnects.
 * @param {Client} disconnectedClient The client object that disconnected.
 */
function handleDisconnectionPairingCleanup(disconnectedClient) {
  const { id: disconnectedId, type: disconnectedType, pairedClientId } = disconnectedClient;

  logger.info(`Handling pairing cleanup for disconnected ${disconnectedType} client ${disconnectedId}`);

  // If the disconnected client was a Unity client
  if (disconnectedType === "unity") {
    // 1. Remove any pending pairings initiated by this Unity client
    if (pendingPairings.has(disconnectedId)) {
      const pendingPhoneId = pendingPairings.get(disconnectedId);
      pendingPairings.delete(disconnectedId);
      logger.info(`Removed pending pairing for disconnected Unity ${disconnectedId} (was waiting for Phone ${pendingPhoneId})`);
    }

    // 2. If it was actively paired, notify the phone and clear its pairing
    if (pairedClientId) {
      const phoneClient = clientService.getPhoneClient(pairedClientId);
      if (phoneClient) {
        logger.info(`Notifying Phone ${pairedClientId} that paired Unity ${disconnectedId} disconnected.`);
        sendWsMessage(phoneClient.ws, { type: "unityLost", unityId: disconnectedId });
        phoneClient.clearPairedClient(); // Clear pairing on the phone side
      } else {
        logger.warn(`Paired Phone ${pairedClientId} not found for disconnected Unity ${disconnectedId}.`);
      }
    }
  }

  // If the disconnected client was a Phone client
  if (disconnectedType === "phone") {
    // 1. Check if any Unity client was waiting for this specific phone in pending pairings
    //    (This is less common, usually Unity initiates, but good practice)
    let waitingUnityId = null;
    for (const [unityId, pendingPhoneId] of pendingPairings.entries()) {
      if (pendingPhoneId === disconnectedId) {
        waitingUnityId = unityId;
        break;
      }
    }
    if (waitingUnityId) {
      // Should we remove the pending pairing? Or just log?
      // If the phone disconnects, the pending pairing is now invalid.
      pendingPairings.delete(waitingUnityId);
      logger.info(`Removed pending pairing for Unity ${waitingUnityId} because waiting Phone ${disconnectedId} disconnected.`);
      // Optionally notify the waiting Unity client
      const unityClient = clientService.getUnityClient(waitingUnityId);
      if (unityClient) {
        sendWsMessage(unityClient.ws, { type: "pairingStatus", status: "error", reason: `Required Phone ${disconnectedId} disconnected.` });
      }
    }

    // 2. If it was actively paired, notify the Unity client and clear its pairing
    if (pairedClientId) {
      const unityClient = clientService.getUnityClient(pairedClientId);
      if (unityClient) {
        logger.info(`Notifying Unity ${pairedClientId} that paired Phone ${disconnectedId} disconnected.`);
        sendWsMessage(unityClient.ws, { type: "phoneLost", phoneId: disconnectedId });
        unityClient.clearPairedClient(); // Clear pairing on the Unity side
      } else {
        logger.warn(`Paired Unity ${pairedClientId} not found for disconnected Phone ${disconnectedId}.`);
      }
    }
  }
}

function getActivePairingsList() {
  const active = [];
  const clients = clientService.getAllClients(); // Get currently connected clients
  for (const client of clients) {
    // Only report from one side (e.g., Unity) to avoid duplicates
    if (client.type === "unity" && client.pairedClientId) {
      const pairedClient = clientService.getPhoneClient(client.pairedClientId);
      // Ensure the paired client is also currently connected
      if (pairedClient && pairedClient.isConnected) {
        active.push({
          unityId: client.id,
          phoneId: client.pairedClientId,
          unityIp: client.ipAddress,
          phoneIp: pairedClient.ipAddress,
          established: client.connectionTime, // Or store pairing time if needed
        });
      }
    }
  }
  return active;
}

function getPendingPairingsList() {
  const pending = [];
  for (const [unityId, phoneId] of pendingPairings.entries()) {
    const unityClient = clientService.getUnityClient(unityId);
    // Only list pending pairings where the initiating Unity client is still connected
    if (unityClient && unityClient.isConnected) {
      pending.push({
        unityId: unityId,
        awaitingPhoneId: phoneId,
        unityIp: unityClient.ipAddress,
      });
    } else {
      // Clean up stale pending pairings where Unity client disconnected
      logger.warn(`Removing stale pending pairing for disconnected Unity ${unityId}`);
      pendingPairings.delete(unityId);
    }
  }
  return pending;
}

module.exports = {
  pairDevices,
  checkPendingPairingsForPhone,
  handleDisconnectionPairingCleanup,
  getActivePairingsList,
  getPendingPairingsList,
  // Expose maps directly
  // _pendingPairingsMap: pendingPairings,
};
