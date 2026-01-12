const WebSocket = require("ws");
const logger = require("../utils/logger");
const { CONFIG } = require("../config");
const clientService = require("./clientService");

class HeartbeatService {
  constructor() {
    this.pingIntervals = new Map(); // connectionId -> intervalId
    this.pongTimeouts = new Map(); // connectionId -> timeoutId
    this.missedPings = new Map(); // connectionId -> count
  }

  /**
   * Start heartbeat monitoring for a WebSocket connection
   * @param {WebSocket} ws - The WebSocket connection
   */
  startHeartbeat(ws) {
    const connectionId = ws.connectionId;
    if (!connectionId) {
      logger.warn("Cannot start heartbeat: WebSocket missing connectionId");
      return;
    }

    // Initialize missed ping counter
    this.missedPings.set(connectionId, 0);

    // Set up ping interval
    const pingInterval = setInterval(() => {
      if (ws.readyState === WebSocket.OPEN) {
        this.sendPing(ws);
      } else {
        // Connection is not open, clean up
        this.stopHeartbeat(connectionId);
      }
    }, CONFIG.HEARTBEAT.PING_INTERVAL);

    this.pingIntervals.set(connectionId, pingInterval);

    // Set up pong listener
    ws.on("pong", () => {
      this.handlePong(connectionId);
    });

    logger.debug(`Heartbeat started for connection ${connectionId}`, {
      pingInterval: CONFIG.HEARTBEAT.PING_INTERVAL,
      pongTimeout: CONFIG.HEARTBEAT.PONG_TIMEOUT,
      maxMissedPings: CONFIG.HEARTBEAT.MAX_MISSED_PINGS,
    });
  }

  /**
   * Send a ping to the client and set up pong timeout
   * @param {WebSocket} ws - The WebSocket connection
   */
  sendPing(ws) {
    const connectionId = ws.connectionId;

    try {
      ws.ping();
      //logger.debug(`Ping sent to connection ${connectionId}`);

      // Set up timeout for pong response
      const pongTimeout = setTimeout(() => {
        this.handleMissedPong(ws);
      }, CONFIG.HEARTBEAT.PONG_TIMEOUT);

      // Clear any existing pong timeout
      const existingTimeout = this.pongTimeouts.get(connectionId);
      if (existingTimeout) {
        clearTimeout(existingTimeout);
      }

      this.pongTimeouts.set(connectionId, pongTimeout);
    } catch (error) {
      logger.error(`Error sending ping to connection ${connectionId}:`, error);
      this.handleDeadConnection(ws);
    }
  }

  /**
   * Handle received pong from client
   * @param {string} connectionId - The connection ID
   */
  handlePong(connectionId) {
    //logger.debug(`Pong received from connection ${connectionId}`);

    // Clear the pong timeout
    const pongTimeout = this.pongTimeouts.get(connectionId);
    if (pongTimeout) {
      clearTimeout(pongTimeout);
      this.pongTimeouts.delete(connectionId);
    }

    // Reset missed ping counter
    this.missedPings.set(connectionId, 0);
  }

  /**
   * Handle missed pong response
   * @param {WebSocket} ws - The WebSocket connection
   */
  handleMissedPong(ws) {
    const connectionId = ws.connectionId;
    const currentMissedCount = this.missedPings.get(connectionId) || 0;
    const newMissedCount = currentMissedCount + 1;

    this.missedPings.set(connectionId, newMissedCount);

    logger.warn(`Missed pong from connection ${connectionId}`, {
      missedCount: newMissedCount,
      maxAllowed: CONFIG.HEARTBEAT.MAX_MISSED_PINGS,
    });

    if (newMissedCount >= CONFIG.HEARTBEAT.MAX_MISSED_PINGS) {
      logger.error(`Connection ${connectionId} considered dead after ${newMissedCount} missed pongs`);
      this.handleDeadConnection(ws);
    }
  }

  /**
   * Handle a dead connection by forcefully closing it
   * @param {WebSocket} ws - The WebSocket connection
   */
  handleDeadConnection(ws) {
    const connectionId = ws.connectionId;
    logger.warn(`Terminating dead connection ${connectionId}`);

    try {
      // Clean up heartbeat resources first
      this.stopHeartbeat(connectionId);

      // Force close the connection if it's not already closed
      if (ws.readyState !== WebSocket.CLOSED && ws.readyState !== WebSocket.CLOSING) {
        ws.terminate(); // This will trigger the 'close' event for cleanup
      }
    } catch (error) {
      logger.error(`Error handling dead connection ${connectionId}:`, error);
    }
  }

  /**
   * Stop heartbeat monitoring for a connection
   * @param {string} connectionId - The connection ID
   */
  stopHeartbeat(connectionId) {
    // Clear ping interval
    const pingInterval = this.pingIntervals.get(connectionId);
    if (pingInterval) {
      clearInterval(pingInterval);
      this.pingIntervals.delete(connectionId);
    }

    // Clear pong timeout
    const pongTimeout = this.pongTimeouts.get(connectionId);
    if (pongTimeout) {
      clearTimeout(pongTimeout);
      this.pongTimeouts.delete(connectionId);
    }

    // Remove missed ping counter
    this.missedPings.delete(connectionId);

    logger.debug(`Heartbeat stopped for connection ${connectionId}`);
  }

  /**
   * Get heartbeat status for all connections
   * @returns {Object} Status information
   */
  getHeartbeatStatus() {
    const activeHeartbeats = Array.from(this.pingIntervals.keys());
    const pendingPongs = Array.from(this.pongTimeouts.keys());
    const missedPingCounts = Object.fromEntries(this.missedPings);

    return {
      activeHeartbeats: activeHeartbeats.length,
      pendingPongs: pendingPongs.length,
      connections: activeHeartbeats,
      missedPingCounts,
    };
  }

  /**
   * Clean up all heartbeat resources (useful for server shutdown)
   */
  cleanup() {
    // Clear all intervals and timeouts
    for (const intervalId of this.pingIntervals.values()) {
      clearInterval(intervalId);
    }
    for (const timeoutId of this.pongTimeouts.values()) {
      clearTimeout(timeoutId);
    }

    // Clear all maps
    this.pingIntervals.clear();
    this.pongTimeouts.clear();
    this.missedPings.clear();

    logger.info("Heartbeat service cleaned up");
  }
}

// Create singleton instance
const heartbeatService = new HeartbeatService();

module.exports = heartbeatService;
