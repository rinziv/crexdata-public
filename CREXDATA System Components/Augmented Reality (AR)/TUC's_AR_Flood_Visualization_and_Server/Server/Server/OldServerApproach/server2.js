// Import necessary modules
const pool = require("./dbPoolConfig");
const express = require("express");
const fs = require("fs");
const path = require("path");
const os = require("os");
const { Kafka } = require("kafkajs");
const WebSocket = require("ws");
const { log } = require("console");

// Constants
const CONFIG = {
  PORT: process.env.PORT || 3000,
  WS_PORT: process.env.WS_PORT || 8080,
  KAFKA_BROKERS: ["localhost:9092"],
  KAFKA_TOPIC: "test",
  FLOOD_DATA_PATH: "C:/Users/CREX DATA/Downloads/AR_workaround.csv",
};

// Define client class
class Client {
  constructor(ws, type, id) {
    this.ws = ws;
    this.type = type; // "unity", "phone"
    this.id = id;
    this.connectionTime = new Date().toISOString();
    this.ipAddress = ws.ipAddress;
    this.pairedClientId = null;
  }

  setPairedClient(clientId) {
    this.pairedClientId = clientId;
  }
}

const pendingPairings = new Map();
const activePairings = new Map();

// Store all clients in one unified structure
const clients = new Map();

//TO BE DECIDED
const consumers = new Map();
const phone_clients = {}; //Depricated

const app = express();

const dataMaps = {
  c8r7: new Map(),
  c9r7: new Map(),
  c8r8: new Map(),
  c9r9: new Map(),
};

// Logging
const logger = {
  info: (message, data = {}) => {
    console.log(`[INFO] ${new Date().toISOString()} - ${message}`, data);
  },
  error: (message, error) => {
    console.error(`[ERROR] ${new Date().toISOString()} - ${message}`, error);
  },
  warn: (message, data = {}) => {
    console.warn(`[WARN] ${new Date().toISOString()} - ${message}`, data);
  },
  debug: (message, data = {}) => {
    if (process.env.DEBUG) {
      console.log(`[DEBUG] ${new Date().toISOString()} - ${message}`, data);
    }
  },
};

// Process uncaught exceptions and unhandled rejections
process.on("uncaughtException", (error) => {
  logger.error("Uncaught Exception:", error);
  // Avoid crushing!!
});

process.on("unhandledRejection", (reason, promise) => {
  logger.error("Unhandled Rejection at:", { promise, reason });
  // Keep the process running and not crush
});

// Server stats tracking
const serverStats = {
  startTime: new Date(),
  totalConnections: 0,
  messagesSent: 0,
  messagesReceived: 0,
  errors: 0,
  lastError: null,
};

// API Routes
app.get("/api/dashboard", (req, res) => {
  try {
    const uptime = Math.floor((new Date() - serverStats.startTime) / 1000);
    const activeClients = [];
    const activePairingsList = [];
    const pendingPairingsList = [];

    // Process all clients
    for (const [clientId, client] of clients.entries()) {
      if (client.ws.readyState === WebSocket.OPEN) {
        // Add to active clients
        activeClients.push({
          id: clientId,
          type: client.type,
          ip: client.ws.ipAddress || "unknown",
          connectedSince: client.connectionTime,
          pairedWith: client.pairedClientId || null,
        });

        // Track pairings (only for unity to avoid duplicates)
        if (client.type === "unity" && client.pairedClientId) {
          const pairedClient = getPhoneClient(client.pairedClientId);
          if (pairedClient && pairedClient.ws.readyState === WebSocket.OPEN) {
            activePairingsList.push({
              unityId: clientId,
              phoneId: client.pairedClientId,
            });
          }
        }
      }
    }

    // Process pending pairings
    for (const [unityId, phoneId] of pendingPairings.entries()) {
      pendingPairingsList.push({
        unityId,
        awaitingPhoneId: phoneId,
      });
    }

    // Create statistics
    const stats = {
      clients: {
        total: activeClients.length,
        byType: {
          unity: activeClients.filter((c) => c.type === "unity").length,
          phone: activeClients.filter((c) => c.type === "phone").length,
          other: activeClients.filter((c) => c.type !== "unity" && c.type !== "phone").length,
        },
      },
      pairings: {
        active: activePairingsList.length,
        pending: pendingPairingsList.length,
      },
      messages: {
        sent: serverStats.messagesSent,
        received: serverStats.messagesReceived,
      },
    };

    res.json({
      server: {
        status: "online",
        uptime: formatUptime(uptime),
        startTime: serverStats.startTime.toISOString(),
        currentTime: new Date().toISOString(),
      },
      stats,
      clients: activeClients,
      pairings: {
        active: activePairingsList,
        pending: pendingPairingsList,
      },
    });
  } catch (err) {
    logger.error("Error generating dashboard", err);
    res.status(500).json({ error: "Failed to generate dashboard" });
  }
});

app.get("/api/status", (req, res) => {
  try {
    const uptime = Math.floor((new Date() - serverStats.startTime) / 1000);

    // Count clients by type and connection status
    let connectedClientCount = 0;
    let connectedUnityCount = 0;
    let connectedPhoneCount = 0;
    let pendingPairingCount = pendingPairings.size;
    let activePairingCount = 0;

    // Count clients and pairings
    for (const [_, client] of clients.entries()) {
      if (client.ws.readyState === WebSocket.OPEN) {
        connectedClientCount++;

        if (client.type === "unity") {
          connectedUnityCount++;
        } else if (client.type === "phone") {
          connectedPhoneCount++;
        }

        // Count active pairings (only count unity clients to avoid doubles)
        if (client.type === "unity" && client.pairedClientId) {
          const pairedClient = getPhoneClient(client.pairedClientId);
          if (pairedClient && pairedClient.ws.readyState === WebSocket.OPEN) {
            activePairingCount++;
          }
        }
      }
    }

    res.json({
      status: "online",
      uptime: formatUptime(uptime),
      startTime: serverStats.startTime.toISOString(),
      currentTime: new Date().toISOString(),
      clients: {
        total: connectedClientCount,
        unity: connectedUnityCount,
        phone: connectedPhoneCount,
      },
      pairings: {
        active: activePairingCount,
        pending: pendingPairingCount,
      },
      stats: {
        totalConnections: serverStats.totalConnections,
        messagesSent: serverStats.messagesSent,
        messagesReceived: serverStats.messagesReceived,
        errors: serverStats.errors,
        lastError: serverStats.lastError,
      },
    });
  } catch (err) {
    logger.error("Error generating status report", err);
    res.status(500).json({ error: "Failed to generate status report" });
  }
});

app.get("/api/pairings", (req, res) => {
  try {
    const activePairings = [];
    const pendingPairingsList = [];

    // Get active pairings from clients
    for (const [clientId, client] of clients.entries()) {
      if (client.pairedClientId && client.type === "unity") {
        const pairedClient = getPhoneClient(client.pairedClientId);

        if (pairedClient && pairedClient.ws.readyState === WebSocket.OPEN) {
          activePairings.push({
            unityClient: {
              id: client.id,
              ip: client.ws.ipAddress || "unknown",
            },
            phoneClient: {
              id: client.pairedClientId,
              ip: pairedClient.ws.ipAddress || "unknown",
            },
            established: client.connectionTime,
          });
        }
      }
    }

    // Get pending pairings
    for (const [unityId, phoneId] of pendingPairings.entries()) {
      const unityClient = getUnityClient(unityId);

      if (unityClient && unityClient.ws.readyState === WebSocket.OPEN) {
        pendingPairingsList.push({
          unityClient: {
            id: unityId,
            ip: unityClient.ws.ipAddress || "unknown",
          },
          waitingForPhoneId: phoneId,
        });
      }
    }

    res.json({
      activePairings: activePairings,
      pendingPairings: pendingPairingsList,
    });
  } catch (err) {
    logger.error("Error generating pairings report", err);
    res.status(500).json({ error: "Failed to generate pairings report" });
  }
});

// Utility function to format uptime in a human-readable way
function formatUptime(seconds) {
  const days = Math.floor(seconds / 86400);
  seconds %= 86400;
  const hours = Math.floor(seconds / 3600);
  seconds %= 3600;
  const minutes = Math.floor(seconds / 60);
  seconds %= 60;

  let result = "";
  if (days > 0) result += `${days}d `;
  if (hours > 0) result += `${hours}h `;
  if (minutes > 0) result += `${minutes}m `;
  result += `${seconds}s`;

  return result;
}

// Start Express server
app.listen(CONFIG.PORT, () => {
  logger.info(`Express server running on port ${CONFIG.PORT}`);
});

// WebSocket server with error handling
let wss;
try {
  wss = new WebSocket.Server({
    port: CONFIG.WS_PORT,
    // Handle connection errors
    perMessageDeflate: {
      zlibDeflateOptions: {
        level: 6,
        memLevel: 8,
      },
    },
  });

  logger.info(`WebSocket server initialized on port ${CONFIG.WS_PORT}`);

  wss.on("listening", () => logger.info(`WebSocket server listening on port ${CONFIG.WS_PORT}`));

  wss.on("error", (error) => {
    logger.error("WebSocket server error:", error);

    // Try to recover if possible
    if (error.code === "EADDRINUSE") {
      logger.warn(`Port ${CONFIG.WS_PORT} is already in use. Trying another port...`);
      // Try a different port after a short delay
      setTimeout(() => {
        try {
          CONFIG.WS_PORT++;
          wss = new WebSocket.Server({ port: CONFIG.WS_PORT });
          logger.info(`WebSocket server restarted on port ${CONFIG.WS_PORT}`);
        } catch (retryError) {
          logger.error("Failed to restart WebSocket server:", retryError);
        }
      }, 1000);
    }
  });
} catch (error) {
  logger.error("Failed to initialize WebSocket server:", error);
}

// Send messages to all connected WebSocket clients
async function sendToAll(message) {
  try {
    let clientCount = 0;
    wss.clients.forEach((client) => {
      if (client.readyState === WebSocket.OPEN) {
        client.send(message);
        clientCount++;
      }
    });
    logger.debug(`Message broadcast to ${clientCount} clients`);
  } catch (error) {
    logger.error("Error broadcasting message:", error);
  }
}

// Kafka connection
async function initializeKafkaConnection() {
  try {
    logger.info("Initializing Kafka connection...");

    // Create a new Kafka client with configuration
    const kafka = new Kafka({
      clientId: "HoloLensDevice",
      brokers: CONFIG.KAFKA_BROKERS,
      connectionTimeout: 3000,
      retry: {
        retries: 3,
        initialRetryTime: 1000,
        maxRetryTime: 5000,
      },
      logCreator: () => {
        return ({ namespace, level, label, log }) => {
          if (namespace === "kafkajs") return;
          const { message, ...rest } = log;
          logger.debug(`Kafka ${label}: ${message}`, rest);
        };
      },
    });

    // Handle WebSocket connections
    wss.on("connection", async (ws, req) => {
      // Increment total connections
      serverStats.totalConnections++;

      const connectionId = generateUniqueId();
      const userAgent = req.headers["user-agent"];
      const ipAddress = req.socket.remoteAddress || req.connection.remoteAddress;
      const connectionTime = new Date().toISOString();

      // Store connection info on the websocket object for later reference
      ws.connectionId = connectionId;
      ws.ipAddress = ipAddress;
      ws.connectionTime = connectionTime;
      ws.userAgent = userAgent;

      logger.info("New WebSocket connection", {
        connectionId,
        ipAddress,
        connectionTime,
      });

      // Send connection acknowledgment
      sendWsMessage(
        ws,
        JSON.stringify({
          type: "connectionAck",
          message: "Connection established",
          connectionId,
          userAgent,
          ipAddress,
          connectionTime,
        })
      );

      let consumer;
      let phone = false;
      let isConnected = true;

      ws.on("close", async () => {
        logger.info("WebSocket client disconnected", { connectionId });
        isConnected = false;

        // Find client in our map
        let disconnectedClientInfo = null;
        for (const [key, client] of clients.entries()) {
          if (client.ws === ws) {
            // Parse the composite key to get type and id
            const [type, id] = key.split(":");
            disconnectedClientInfo = {
              id: id,
              compositeKey: key,
              type: type,
              pairedClientId: client.pairedClientId,
            };
            logger.info(`Client ${ws.connectionId} (type ${type}, id ${id}) disconnected and was paired with ${client.pairedClientId}`);
            clients.delete(key);
            break;
          }
        }

        if (disconnectedClientInfo && disconnectedClientInfo.type === "unity") {
          // Clean up any pending pairings for this Unity client
          if (pendingPairings.has(disconnectedClientInfo.id)) {
            const pendingPhoneId = pendingPairings.get(disconnectedClientInfo.id);
            logger.info(`Removing pending pairing for disconnected Unity client ${disconnectedClientInfo.id} with Phone ${pendingPhoneId}`);
            pendingPairings.delete(disconnectedClientInfo.id);
          }

          // If it was paired with a phone, clean up the phone client pairedClient field
          if (disconnectedClientInfo.pairedClientId) {
            const phoneClient = getPhoneClient(disconnectedClientInfo.pairedClientId);
            if (phoneClient) {
              logger.info(`Clearing pairing for Phone ${disconnectedClientInfo.pairedClientId} after Unity ${disconnectedClientInfo.id} disconnected`);
              phoneClient.pairedClientId = null;
              // Remove from activePairings
              activePairings.delete(disconnectedClientInfo.pairedClientId);

              // Notify phone that Unity client disconnected
              sendWsMessage(
                phoneClient.ws,
                JSON.stringify({
                  type: "unityLost",
                  unityId: disconnectedClientInfo.id,
                })
              );
            }
          }
        }

        // If this was a phone and it was paired, notify the unity client
        if (disconnectedClientInfo && disconnectedClientInfo.type === "phone" && disconnectedClientInfo.pairedClientId) {
          const unityClient = getUnityClient(disconnectedClientInfo.pairedClientId);
          if (unityClient) {
            logger.info(`Notifying Unity ${disconnectedClientInfo.pairedClientId} that Phone ${disconnectedClientInfo.id} disconnected`);
            sendWsMessage(
              unityClient.ws,
              JSON.stringify({
                type: "phoneLost",
                phoneId: disconnectedClientInfo.id,
              })
            );

            logger.info(`Clearing pairing for Unity ${disconnectedClientInfo.pairedClientId} after Phone ${disconnectedClientInfo.id} disconnected`);
            unityClient.pairedClientId = null;
            // Remove from activePairings
            activePairings.delete(disconnectedClientInfo.pairedClientId);
          } else {
            logger.warn(`Could not find Unity client ${disconnectedClientInfo.pairedClientId} to notify about phone disconnection`);
          }
        }

        // Clean up Kafka consumer
        if (!phone && consumers.has(ws)) {
          try {
            const consumer = consumers.get(ws);
            await consumer.disconnect();
            consumers.delete(ws);
            logger.info("Kafka consumer disconnected");
          } catch (error) {
            logger.error("Error closing Kafka consumer:", error);
          }
        }
      });

      // Handle WebSocket messages
      ws.on("message", async (message) => {
        serverStats.messagesReceived++;
        try {
          // Always try to parse as JSON first
          const data = JSON.parse(message.toString());

          // Handle messages based on type field
          switch (data.type) {
            case "registration":
              if (data.deviceType === "unity") {
                // Register Unity client
                const client = new Client(ws, "unity", data.clientId);
                const compositeKey = getCompositeKey("unity", data.clientId);
                clients.set(compositeKey, client);
                logger.info(`Unity client registered with ID: ${data.clientId}, key: ${compositeKey}`);

                // Create Kafka consumer if needed
                try {
                  const consumer = kafka.consumer({ groupId: data.clientId });
                  consumers.set(ws, consumer);

                  // Start Kafka connection attempt
                  attemptKafkaConnection(consumer, ws); //*****************
                } catch (error) {
                  logger.error(`Failed to create Kafka consumer for ${data.clientId}:`, error);
                }
              } else if (data.deviceType === "phone") {
                // Register phone client
                handlePhoneRegistration(ws, data.clientId);
              }
              break;

            case "ping":
              ws.send(
                JSON.stringify({
                  type: "pong",
                  timestamp: data.timestamp,
                })
              );
              break;

            case "pair":
              // Handle pairing request
              pairDevices(data.phoneId, data.unityId);
              break;

            case "gps":
              logger.debug("GPS data received", {
                type: data.type,
                senderId: data.senderId,
                lat: data.lat,
                lon: data.lon,
                data: data.data,
              });
              const senderId = getPhoneClient(data.senderId);
              // Handle GPS data
              const targetClient = getUnityClient(senderId.pairedClientId);
              if (targetClient && targetClient.ws.readyState === WebSocket.OPEN) {
                sendWsMessage(
                  targetClient.ws,
                  JSON.stringify({
                    type: "gps",
                    senderId: data.senderId,
                    data: data.data,
                    lat: data.lat,
                    lon: data.lon,
                  })
                );
              } else {
                logger.warn(`Target client ${data.targetId} not found or not connected`);
              }
              break;

            // Other Message Handling Cases to be added

            default:
              // Legacy message handling (can be removed once all clients are updated)
              const messageStr = message.toString();
              const separatedStrings = messageStr.split(",");

              // Handle legacy message formats...
              // This is temporary until all clients are updated
              handleLegacyMessage(ws, messageStr, separatedStrings, consumer);
          }
        } catch (error) {
          // If JSON parsing fails, try legacy message handling
          const messageStr = message.toString();
          const separatedStrings = messageStr.split(",");

          try {
            handleLegacyMessage(ws, messageStr, separatedStrings, consumer);
          } catch (innerError) {
            serverStats.errors++;
            serverStats.lastError = {
              time: new Date().toISOString(),
              message: innerError.message,
              stack: innerError.stack,
            };
            logger.error("Error processing WebSocket message:", innerError);

            sendWsMessage(
              ws,
              JSON.stringify({
                type: "error",
                message: "Failed to process message",
                details: innerError.message,
              })
            );
          }
        }
      });

      // Kafka connection attempt function
      const attemptKafkaConnection = async (consumer, ws) => {
        let attempts = 0;
        const maxAttempts = 5;

        while (!phone && isConnected && attempts < maxAttempts) {
          try {
            attempts++;
            logger.info(`Attempting to connect to Kafka (attempt ${attempts}/${maxAttempts})...`);

            await consumer.connect();
            await consumer.subscribe({ topic: CONFIG.KAFKA_TOPIC, fromBeginning: true });

            await consumer.run({
              eachMessage: async ({ topic, partition, message }) => {
                logger.debug("Kafka message received", {
                  topic,
                  partition,
                  offset: message.offset,
                });

                if (ws.readyState === WebSocket.OPEN) {
                  try {
                    const kafkaData = JSON.parse(message.value.toString());

                    // Wrapper for Kafka POI messages
                    const formattedMessage = {
                      type: "spawnPOI",
                      payload: kafkaData,
                      meta: {
                        topic: topic,
                        partition: partition,
                        offset: message.offset,
                      },
                    };

                    // Send as JSON
                    sendWsMessage(ws, JSON.stringify(formattedMessage));
                  } catch (err) {
                    logger.warn("Non-JSON Kafka message received, wrapping in standard format", { error: err.message });

                    // Create a basic wrapper to maintain consistency
                    const formattedMessage = {
                      type: "spawnPOI",
                      payload: {
                        poiType: "default",
                        position: {
                          latitude: 0,
                          longitude: 0,
                        },
                        rawMessage: message.value.toString(),
                      },
                      meta: {
                        topic: topic,
                        partition: partition,
                        offset: message.offset,
                      },
                    };

                    sendWsMessage(ws, JSON.stringify(formattedMessage));
                  }
                } else {
                  await consumer.disconnect();
                }
              },
            });

            sendWsMessage(ws, "Ready");
            logger.info("Kafka connection established successfully");
            break;
          } catch (error) {
            logger.error("Failed to connect to Kafka:", error);

            if (!isConnected || attempts >= maxAttempts) {
              logger.warn("Maximum Kafka connection attempts reached or client disconnected");
              break;
            }

            // Exponential backoff
            const delay = Math.min(1000 * Math.pow(2, attempts - 1), 10000);
            logger.debug(`Retrying in ${delay}ms...`);
            await new Promise((resolve) => setTimeout(resolve, delay));
          }
        }
      };
    });

    logger.info("Kafka connection handler initialized");
  } catch (error) {
    logger.error("Failed to initialize Kafka connection:", error);
  }
}

function handleLegacyMessage(ws, messageStr, separatedStrings, consumer) {
  if (separatedStrings[0] === "flood") {
    handleFloodDataRequest(ws, separatedStrings);
  } else if (separatedStrings[0] === "seek") {
    handleKafkaSeek(consumer, separatedStrings);
  } else if (separatedStrings[0] === "phone") {
    handlePhoneConnection(ws, phone_clients, separatedStrings);
  } else if (separatedStrings[0].includes("gps")) {
    handleGpsData(clients, separatedStrings);
  } else {
    logger.warn("Unknown message type:", messageStr);
  }
}

function getCompositeKey(type, id) {
  return `${type}:${id}`;
}

// Modify these functions to use composite keys
function getClientByTypeAndId(type, id) {
  const compositeKey = getCompositeKey(type, id);
  return clients.get(compositeKey);
}

function getPhoneClient(id) {
  return getClientByTypeAndId("phone", id);
}

function getUnityClient(id) {
  return getClientByTypeAndId("unity", id);
}

function pairDevices(phoneId, unityId) {
  // Get clients using the type-specific helper functions
  const phoneClient = getPhoneClient(phoneId);
  const unityClient = getUnityClient(unityId);

  // Verify that unityClient exists
  if (!unityClient) {
    logger.warn(`Cannot pair: Unity client ${unityId} not found`);
    return false;
  }

  // Check if phoneClient exists
  if (!phoneClient) {
    // Phone not connected yet, add to pending pairings
    // Check if already pending
    if (pendingPairings.has(unityId)) {
      logger.warn(`Pairing request already pending for Unity ${unityId}`);
    } else {
      pendingPairings.set(unityId, phoneId);
      logger.info(`Added pending pairing: Unity ${unityId} waiting for Phone ${phoneId}`);
    }

    // Notify unity client that we're waiting for the phone
    sendWsMessage(
      unityClient.ws,
      JSON.stringify({
        type: "pairingStatus",
        status: "waiting",
        phoneId: phoneId,
      })
    );

    return false;
  }

  // Both clients exist and are of the correct types, pair them
  phoneClient.setPairedClient(unityId);
  unityClient.setPairedClient(phoneId);

  activePairings.set(phoneId, unityId);
  logger.info(`Pairing established: Phone ${phoneId} with Unity ${unityId}`);

  // Notify both clients they've been paired
  sendWsMessage(
    phoneClient.ws,
    JSON.stringify({
      type: "paired",
      pairedWith: unityId,
    })
  );

  sendWsMessage(
    unityClient.ws,
    JSON.stringify({
      type: "paired",
      pairedWith: phoneId,
    })
  );

  return true;
}

// Helper functions for handling WebSocket messages
function handleFloodDataRequest(ws, data) {
  try {
    logger.debug("Processing flood data request", { data });
    if (data[0].includes("1")) {
      sendWsMessage(ws, getValuesForTime(data[1], dataMaps.c9r7) + ",water");
    } else {
      sendWsMessage(ws, getValuesForTime(data[1], dataMaps.c8r8) + ",water");
    }
  } catch (error) {
    logger.error("Error handling flood data request:", error);
  }
}

function handleKafkaSeek(consumer, data) {
  try {
    if (!consumer) {
      logger.warn("Cannot seek: Kafka consumer not initialized");
      return;
    }

    logger.debug("Processing Kafka seek request", {
      topic: CONFIG.KAFKA_TOPIC,
      partition: data[0],
      offset: data[1],
    });

    consumer.seek({
      topic: CONFIG.KAFKA_TOPIC,
      partition: parseInt(data[0]),
      offset: parseInt(data[1]),
    });
  } catch (error) {
    logger.error("Error during Kafka seek:", error);
  }
}

function handlePhoneConnection(ws, phoneClients, data) {
  try {
    logger.info("Phone client connected");
    phoneClients[data[1]] = ws;
  } catch (error) {
    logger.error("Error handling phone connection:", error);
  }
}

function handlePhoneRegistration(ws, clientId) {
  const client = new Client(ws, "phone", clientId);
  const compositeKey = getCompositeKey("phone", clientId);
  clients.set(compositeKey, client);
  logger.info(`Phone client registered with ID: ${clientId}, key: ${compositeKey}`);

  // Check for pending pairing requests for this phone
  for (const [unityId, phoneId] of pendingPairings.entries()) {
    if (phoneId === clientId) {
      // Try to pair them
      const success = pairDevices(phoneId, unityId);
      if (success) {
        // Remove from pending if successful
        pendingPairings.delete(unityId);
        logger.info(`Completed pending pairing between Unity ${unityId} and Phone ${phoneId}`);
      }
    }
  }
}

function handleGpsData(clients, data) {
  try {
    const clientId = data[1].split(":")[1].trim();
    const unityClient = clients[clientId];

    const clientGpsLat = data[2].split(":")[1].trim();
    const clientGpsLong = data[3].trim();

    logger.debug("GPS data received", {
      clientId,
      lat: clientGpsLat,
      long: clientGpsLong,
    });

    if (unityClient && unityClient.readyState === WebSocket.OPEN) {
      sendWsMessage(unityClient, `${clientGpsLat},${clientGpsLong}, gps`);
      logger.debug(`GPS data sent to Unity client ${clientId}`);
    } else {
      logger.warn(`Unity client ${clientId} not found or not connected`);
    }
  } catch (error) {
    logger.error("Error handling GPS data:", error);
  }
}

// Send WebSocket messages with error handling
function sendWsMessage(ws, message) {
  try {
    if (ws.readyState === WebSocket.OPEN) {
      ws.send(message);
      serverStats.messagesSent++;
      return true;
    }
    return false;
  } catch (error) {
    serverStats.errors++;
    serverStats.lastError = {
      time: new Date().toISOString(),
      message: error.message,
      stack: error.stack,
    };

    logger.error("Error sending WebSocket message:", error);
    return false;
  }
}

// Generate unique IDs for connections
function generateUniqueId() {
  return `${Date.now()}-${Math.random().toString(36).substr(2, 9)}`;
}

// Load and process flood data
function loadFloodData() {
  try {
    logger.info(`Loading flood data from ${CONFIG.FLOOD_DATA_PATH}`);

    fs.readFile(CONFIG.FLOOD_DATA_PATH, "utf8", (err, data) => {
      if (err) {
        logger.error("Error reading flood data file:", err);
        return;
      }

      try {
        // Process the data
        const processedData = data.replace(/(\d+),(\d+)/g, "$1.$2");
        const rows = processedData.split("\n");
        const firstRowSplit = cleanArray(rows[0].split(";"));

        // Get column indices
        const timeStampCol = firstRowSplit.indexOf("time_stamp");
        const c8r7Col = firstRowSplit.indexOf("c8r7");
        const c9r7Col = firstRowSplit.indexOf("c9r7");
        const c8r8Col = firstRowSplit.indexOf("c8r8");
        const c9r9Col = firstRowSplit.indexOf("c9r9");

        // Validate column indices
        if (timeStampCol === -1 || c8r7Col === -1 || c9r7Col === -1 || c8r8Col === -1 || c9r9Col === -1) {
          logger.error("Required columns missing in flood data file", {
            columns: { timeStampCol, c8r7Col, c9r7Col, c8r8Col, c9r9Col },
          });
          return;
        }

        let dataCount = 0;
        const timeRegex = /^\d{2}\.\d{2}\.\d{4}, \d{2}:\d{2}:\d{2}$/;

        rows.forEach((value, index) => {
          if (index > 1) {
            let splitRow = cleanArray(value.split(";"));
            if (timeRegex.test(splitRow[0])) {
              const timestamp = splitRow[0];

              if (!dataMaps.c8r7.has(timestamp)) {
                dataMaps.c8r7.set(timestamp, splitRow[c8r7Col]);
                dataMaps.c9r7.set(timestamp, splitRow[c9r7Col]);
                dataMaps.c8r8.set(timestamp, splitRow[c8r8Col]);
                dataMaps.c9r9.set(timestamp, splitRow[c9r9Col]);
                dataCount++;
              }
            }
          }
        });

        logger.info(`Processed ${dataCount} flood data entries`);

        // Save the processed data to a JSON file
        saveFloodDataToJson();
      } catch (error) {
        logger.error("Error processing flood data:", error);
      }
    });
  } catch (error) {
    logger.error("Error in loadFloodData function:", error);
  }
}

// Save processed flood data to JSON
function saveFloodDataToJson() {
  try {
    const c8r7Object = mapToObject(dataMaps.c8r7);
    const c9r7Object = mapToObject(dataMaps.c9r7);
    const c8r8Object = mapToObject(dataMaps.c8r8);
    const c9r9Object = mapToObject(dataMaps.c9r9);

    const combinedObject = {
      c8r7: c8r7Object,
      c9r7: c9r7Object,
      c8r8: c8r8Object,
      c9r9: c9r9Object,
    };

    const desktopPath = path.join(os.homedir(), "desktop");
    const filePath = path.join(desktopPath, "maps.json");
    const jsonString = JSON.stringify(combinedObject, null, 2);

    fs.writeFile(filePath, jsonString, "utf8", (err) => {
      if (err) {
        logger.error("Error writing flood data to JSON file:", err);
      } else {
        logger.info(`Flood data saved to ${filePath}`);
      }
    });
  } catch (error) {
    logger.error("Error saving flood data to JSON:", error);
  }
}

// UTILITY FUNCTIONS
function cleanArray(arr) {
  return arr.map((str) => str.trim().replace(/\s+/g, " "));
}

function mapToObject(map) {
  const obj = {};
  for (let [key, value] of map) {
    obj[key] = value;
  }
  return obj;
}

function dateTime() {
  const now = new Date();
  const date = ("0" + now.getDate()).slice(-2);
  const month = ("0" + (now.getMonth() + 1)).slice(-2);
  const year = now.getFullYear();
  const hours = ("0" + now.getHours()).slice(-2);
  const minutes = ("0" + now.getMinutes()).slice(-2);

  return `${date}.${month}.${year}, ${hours}:${minutes}:00`;
}

function getValuesForTime(inputTime, dataMap) {
  try {
    // Convert input time to minutes
    const [inputHours, inputMinutes] = inputTime.split(":").map(Number);
    const inputTotalMinutes = inputHours * 60 + inputMinutes;

    let value1 = null,
      value2 = null,
      value3 = null;

    if (!(dataMap instanceof Map)) {
      throw new Error("dataMap is not a Map instance");
    }

    for (const [key, value] of dataMap) {
      if (typeof key !== "string" || !key.includes(", ")) {
        logger.warn(`Invalid key format in dataMap: ${key}`);
        continue;
      }

      const [date, time] = key.split(", ");

      if (typeof time !== "string" || time.split(":").length !== 3) {
        logger.warn(`Invalid time format in dataMap: ${time}`);
        continue;
      }

      const [hours, minutes] = time.split(":").map(Number);
      const totalMinutes = hours * 60 + minutes;

      if (totalMinutes === inputTotalMinutes) value1 = value;
      if (totalMinutes === inputTotalMinutes + 10) value2 = value;
      if (totalMinutes === inputTotalMinutes + 20) value3 = value;
    }

    return `${value1 ?? "null"},${value2 ?? "null"},${value3 ?? "null"}`;
  } catch (error) {
    logger.error("Error in getValuesForTime:", error);
    return "null,null,null";
  }
}

// Initialize the server
(async function initServer() {
  try {
    logger.info("Starting server initialization...");

    // Load flood data
    //loadFloodData();

    // Initialize Kafka connection
    await initializeKafkaConnection();

    logger.info("Server initialization completed successfully");
  } catch (error) {
    logger.error("Failed to initialize server:", error);
  }
})();

// Handle graceful shutdown
process.on("SIGTERM", gracefulShutdown);
process.on("SIGINT", gracefulShutdown);

async function gracefulShutdown() {
  logger.info("Received shutdown signal, closing connections...");

  // Close WebSocket server
  if (wss) {
    wss.close(() => {
      logger.info("WebSocket server closed");
    });
  }

  // Allow time for connections to close
  setTimeout(() => {
    logger.info("Server shutdown complete");
    process.exit(0);
  }, 1500);
}
