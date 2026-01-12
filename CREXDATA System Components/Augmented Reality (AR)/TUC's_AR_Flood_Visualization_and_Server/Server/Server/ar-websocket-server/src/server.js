const http = require("http");
const WebSocket = require("ws");
const logger = require("./utils/logger");
const { CONFIG } = require("./config");
const app = require("./app"); // Import the configured Express app
const { handleNewConnection } = require("./websocket/connectionHandler");
const kafkaService = require("./services/kafkaService");
const statsService = require("./services/statsService");
const dataService = require("./services/dataService");
const heartbeatService = require("./services/heartbeatService");

let wss = null;
let server = null;

// Add these at the top or early in your server.js
process.on("unhandledRejection", (reason, promise) => {
  logger.error("FATAL: Unhandled Rejection at:", { promiseLabel: "Unhandled Promise", reason });
  if (reason instanceof Error) {
    logger.error("Unhandled Rejection Error details:", { message: reason.message, stack: reason.stack });
  }
  // Consider exiting if the app is in an unstable state
  // process.exit(1);
});

process.on("uncaughtException", (error) => {
  logger.error("FATAL: Uncaught Exception:", { message: error.message, stack: error.stack });
  // Exit after an uncaught exception.
  // process.exit(1);
});

/**
 * Initializes and starts the HTTP and WebSocket servers.
 */
async function startServer() {
  logger.info("Starting server initialization...");
  statsService.getStats(); // Initialize stats service (access startTime)

  try {
    // dataService.loadFloodData();
    dataService.loadStaticPois();
  } catch (error) {
    logger.error("Error during initial data loading:", error);
    // Decide if this is fatal or not
  }

  // 3. Create HTTP Server using the Express app
  server = http.createServer(app);

  // 4. Initialize WebSocket Server and attach it to the HTTP server
  try {
    //wss = new WebSocket.Server({ server }); // Attach WebSocket server to the HTTP server
    // OR: Run on a separate port if needed:
    wss = new WebSocket.Server({ port: CONFIG.WS_PORT });

    logger.info(`WebSocket server initializing...`); // Will log listening port via event

    // --- WebSocket Server Event Listeners ---
    wss.on("listening", () => {
      // If WS runs on the same port as HTTP:
      const address = server.address();
      const port = address ? address.port : "unknown";
      //logger.info(`WebSocket server listening on same port as HTTP: ${port}`);
      // WS runs on a separate port:
      logger.info(`WebSocket server listening on port ${CONFIG.WS_PORT}`);
    });

    wss.on("connection", (ws, req) => {
      // Delegate new connection handling
      handleNewConnection(ws, req);
    });

    wss.on("error", (error) => {
      logger.error("WebSocket Server Error:", error);
      statsService.recordError(error); // Record as a general server error
      // Optional: Attempt recovery or shutdown based on error type
      if (error.code === "EADDRINUSE") {
        logger.error(`FATAL: WebSocket port ${CONFIG.WS_PORT} is already in use. Exiting.`);
        process.exit(1);
      }
      // Handle other potential errors
    });

    wss.on("close", () => {
      logger.info("WebSocket Server has closed.");
    });
  } catch (error) {
    logger.error("FATAL: Failed to initialize WebSocket server:", error);
    process.exit(1);
  }

  // 5. Start the HTTP Server (which also starts the attached WebSocket server)
  server.listen(CONFIG.PORT, () => {
    logger.info(`HTTP server running on port ${CONFIG.PORT}`);
    logger.info("Server initialization completed successfully.");
  });

  server.on("error", (error) => {
    logger.error("HTTP Server Error:", error);
    if (error.code === "EADDRINUSE") {
      logger.error(`FATAL: HTTP port ${CONFIG.PORT} is already in use. Exiting.`);
      process.exit(1);
    }
    // Handle other potential errors
  });
}

/**
 * Gracefully shuts down the server.
 */
async function gracefulShutdown() {
  logger.info("Received shutdown signal. Closing connections...");

  // 1. Stop accepting new connections (HTTP server)
  if (server) {
    server.close(async () => {
      logger.info("HTTP server closed.");

      // 2. Close WebSocket Server and disconnect clients
      if (wss) {
        logger.info("Closing WebSocket server...");
        // Disconnect all clients gracefully
        wss.clients.forEach((client) => {
          if (client.readyState === WebSocket.OPEN) {
            client.close(1012, "Server shutting down"); // 1012 = Service Restart
          }
        });
        // Close the server itself
        wss.close(() => {
          logger.info("WebSocket server closed.");
          // Proceed with other cleanup after WS server is fully closed
          shutdownPart2();
        });
        // Set a timeout in case WS close hangs
        setTimeout(() => {
          logger.warn("WebSocket server close timed out. Forcing shutdown.");
          shutdownPart2();
        }, 5000); // 5 second timeout
      } else {
        shutdownPart2(); // No WS server to close
      }
    });
    // Set a timeout in case HTTP close hangs
    setTimeout(() => {
      logger.warn("HTTP server close timed out. Proceeding with shutdown.");
      // Force close WS if still open
      if (wss && !wss._server?.listening) {
        // Check if wss is still trying to close
        wss.close(() => {
          logger.info("WebSocket server force closed.");
          shutdownPart2();
        });
      } else {
        shutdownPart2();
      }
    }, 5000); // 5 second timeout
  } else {
    logger.info("HTTP server not initialized. Proceeding with shutdown.");
    shutdownPart2();
  }
}

/**
 * Part 2 of shutdown: Clean up resources after servers are closed.
 */
async function shutdownPart2() {
  // 3. Clean up heartbeat service
  logger.info("Cleaning up heartbeat service...");
  heartbeatService.cleanup();

  // 4. Disconnect Kafka Consumers (already handled during client disconnect via wss.clients.forEach and client.close())
  //    The `disconnectAllConsumersForClient` called during individual client 'close' events should cover this.
  logger.info("Ensuring all Kafka consumers are disconnected (primarily handled by individual client disconnects).");

  // 5. Disconnect all Kafka Client Instances
  try {
    await kafkaService.disconnectAllKafkaInstances();
    logger.info("All Kafka client instances disconnected.");
  } catch (e) {
    logger.error("Error disconnecting Kafka client instances:", e);
  }

  logger.info("Server shutdown complete.");
  process.exit(0);
}

// --- Signal Handling ---
process.on("SIGTERM", gracefulShutdown); // Standard termination signal
process.on("SIGINT", gracefulShutdown); // Ctrl+C in terminal

// --- Start the Server ---
startServer().catch((error) => {
  logger.error("Unhandled error during server startup:", error);
  process.exit(1);
});
