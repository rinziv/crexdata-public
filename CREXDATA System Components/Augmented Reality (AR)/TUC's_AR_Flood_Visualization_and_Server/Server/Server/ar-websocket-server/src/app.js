const express = require("express");
const WebSocket = require("ws");
const logger = require("./utils/logger");
const { CONFIG } = require("./config");
const { formatUptime } = require("./utils/helpers");
const clientService = require("./services/clientService");
const pairingService = require("./services/pairingService");
const statsService = require("./services/statsService");
const heartbeatService = require("./services/heartbeatService");

const app = express();

// Middleware (optional: add CORS, body-parser if needed for future POST routes)
// const cors = require('cors');
// app.use(cors());
app.use(express.json()); // Enable parsing JSON request bodies

// --- API Routes ---

// Simple health check
app.get("/api/health", (req, res) => {
  res.status(200).json({ status: "ok", timestamp: new Date().toISOString() });
});

// Detailed Dashboard Route
app.get("/api/dashboard", (req, res) => {
  try {
    const serverStats = statsService.getStats();
    const uptimeSeconds = Math.floor((new Date() - serverStats.startTime) / 1000);
    const allClients = clientService.getAllClients(); // Get currently connected clients

    const activeClients = allClients.map((client) => ({
      id: client.id, // Use the client's registered ID
      type: client.type,
      useCase: client.useCase,
      name: client.name,
      ip: client.ipAddress,
      connectedSince: client.connectionTime,
      userAgent: client.userAgent,
      connectionId: client.connectionId, // The unique ID of the WS connection
      pairedWith: client.pairedClientId || null,
    }));

    const activePairingsList = pairingService.getActivePairingsList();
    const pendingPairingsList = pairingService.getPendingPairingsList();
    const heartbeatStatus = heartbeatService.getHeartbeatStatus();

    // Calculate stats based on currently connected clients
    const stats = {
      clients: {
        total: activeClients.length,
        byType: {
          unity: activeClients.filter((c) => c.type === "unity").length,
          phone: activeClients.filter((c) => c.type === "phone").length,
          other: activeClients.filter((c) => c.type !== "unity" && c.type !== "phone").length, // Should be 0 if types are enforced
        },
      },
      pairings: {
        active: activePairingsList.length,
        pending: pendingPairingsList.length,
      },
      heartbeat: {
        activeMonitoring: heartbeatStatus.activeHeartbeats,
        pendingPongs: heartbeatStatus.pendingPongs,
        missedPingCounts: heartbeatStatus.missedPingCounts,
        config: {
          pingInterval: CONFIG.HEARTBEAT.PING_INTERVAL,
          pongTimeout: CONFIG.HEARTBEAT.PONG_TIMEOUT,
          maxMissedPings: CONFIG.HEARTBEAT.MAX_MISSED_PINGS,
        },
      },
      messages: {
        sent: serverStats.messagesSent,
        received: serverStats.messagesReceived,
      },
      connections: {
        totalAttempted: serverStats.totalConnections,
        currentlyActive: serverStats.currentConnections, // Use the dedicated counter
        lastConnectionTime: serverStats.lastConnectionTime,
      },
      errors: {
        application: serverStats.errors,
        kafka: serverStats.kafkaErrors,
        lastAppError: serverStats.lastError,
        lastKafkaError: serverStats.lastKafkaError,
      },
    };

    res.json({
      server: {
        status: "online",
        uptime: formatUptime(uptimeSeconds),
        startTime: serverStats.startTime.toISOString(),
        currentTime: new Date().toISOString(),
        port: CONFIG.PORT,
        wsPort: CONFIG.WS_PORT,
      },
      stats,
      clients: activeClients, // List of active clients
      pairings: {
        // Detailed pairing info
        active: activePairingsList,
        pending: pendingPairingsList,
      },
    });
  } catch (err) {
    logger.error("Error generating dashboard data:", err);
    statsService.recordError(err); // Record the error
    res.status(500).json({ error: "Failed to generate dashboard data", details: err.message });
  }
});

// Simplified Status Route
app.get("/api/status", (req, res) => {
  try {
    const serverStats = statsService.getStats();
    const uptimeSeconds = Math.floor((new Date() - serverStats.startTime) / 1000);
    const allClients = clientService.getAllClients(); // Get currently connected clients

    const connectedUnityCount = allClients.filter((c) => c.type === "unity").length;
    const connectedPhoneCount = allClients.filter((c) => c.type === "phone").length;

    const activePairingCount = pairingService.getActivePairingsList().length;
    const pendingPairingCount = pairingService.getPendingPairingsList().length;

    res.json({
      status: "online",
      uptime: formatUptime(uptimeSeconds),
      startTime: serverStats.startTime.toISOString(),
      currentTime: new Date().toISOString(),
      clients: {
        total: allClients.length,
        unity: connectedUnityCount,
        phone: connectedPhoneCount,
      },
      pairings: {
        active: activePairingCount,
        pending: pendingPairingCount,
      },
      stats: {
        totalConnectionsAttempted: serverStats.totalConnections,
        messagesSent: serverStats.messagesSent,
        messagesReceived: serverStats.messagesReceived,
        errors: serverStats.errors + serverStats.kafkaErrors, // Combined error count
      },
    });
  } catch (err) {
    logger.error("Error generating status report:", err);
    statsService.recordError(err);
    res.status(500).json({ error: "Failed to generate status report", details: err.message });
  }
});

// Pairings Detail Route
app.get("/api/pairings", (req, res) => {
  try {
    const activePairingsList = pairingService.getActivePairingsList();
    const pendingPairingsList = pairingService.getPendingPairingsList();

    res.json({
      activePairings: activePairingsList,
      pendingPairings: pendingPairingsList,
    });
  } catch (err) {
    logger.error("Error generating pairings report:", err);
    statsService.recordError(err);
    res.status(500).json({ error: "Failed to generate pairings report", details: err.message });
  }
});

// Heartbeat Status Route
app.get("/api/heartbeat", (req, res) => {
  try {
    const heartbeatStatus = heartbeatService.getHeartbeatStatus();

    res.json({
      status: "ok",
      timestamp: new Date().toISOString(),
      heartbeat: {
        ...heartbeatStatus,
        config: {
          pingInterval: CONFIG.HEARTBEAT.PING_INTERVAL,
          pongTimeout: CONFIG.HEARTBEAT.PONG_TIMEOUT,
          maxMissedPings: CONFIG.HEARTBEAT.MAX_MISSED_PINGS,
        },
      },
    });
  } catch (err) {
    logger.error("Error generating heartbeat status:", err);
    statsService.recordError(err);
    res.status(500).json({ error: "Failed to generate heartbeat status", details: err.message });
  }
});

// --- Error Handling Middleware ---
// Add a generic error handler for API routes
app.use((err, req, res, next) => {
  logger.error("API Route Error:", { path: req.path, error: err.message, stack: err.stack });
  statsService.recordError(err);
  if (!res.headersSent) {
    res.status(500).json({ error: "Internal Server Error", details: err.message });
  }
});

module.exports = app;
