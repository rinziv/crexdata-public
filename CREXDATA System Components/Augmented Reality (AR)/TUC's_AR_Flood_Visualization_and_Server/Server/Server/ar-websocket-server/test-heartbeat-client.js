#!/usr/bin/env node

/**
 * Simple WebSocket client for testing heartbeat functionality
 * Usage: node test-heartbeat-client.js [ws://localhost:8080]
 */

const WebSocket = require("ws");

const serverUrl = process.argv[2] || "ws://localhost:8080";
const clientType = process.argv[3] || "unity";
const clientId = Math.floor(Math.random() * 50).toString();

console.log(`Connecting to ${serverUrl} as ${clientType} client ${clientId}`);

const ws = new WebSocket(serverUrl);

let isRegistered = false;

ws.on("open", () => {
  console.log("✅ Connected to server");
});

ws.on("message", (data) => {
  try {
    const message = JSON.parse(data.toString());
    console.log("📨 Received:", message);

    if (message.type === "connectionAck" && !isRegistered) {
      // Register with the server
      const registerMessage = {
        type: "registration",
        deviceType: clientType,
        clientId: clientId,
        timestamp: new Date().toISOString(),
      };

      console.log("📤 Registering:", registerMessage);
      ws.send(JSON.stringify(registerMessage));
      isRegistered = true;
    }
  } catch (error) {
    console.error("❌ Error parsing message:", error);
  }
});

ws.on("ping", () => {
  console.log("🏓 Received ping from server");
});

ws.on("pong", () => {
  console.log("🏓 Received pong from server (shouldn't happen normally)");
});

ws.on("close", (code, reason) => {
  console.log(`🔌 Connection closed: ${code} - ${reason}`);
});

ws.on("error", (error) => {
  console.error("❌ WebSocket error:", error);
});

// Graceful shutdown
process.on("SIGINT", () => {
  console.log("\n🛑 Shutting down client...");
  if (ws.readyState === WebSocket.OPEN) {
    ws.close(1000, "Client shutdown");
  } else {
    process.exit(0);
  }
});

// Test different scenarios with command line options
if (process.argv.includes("--no-pong")) {
  console.log("⚠️  Running in NO-PONG mode - will not respond to pings");
  // Override the default pong behavior to simulate unresponsive client
  ws.pong = () => {
    console.log("🚫 Ignoring ping (no-pong mode)");
  };
}

if (process.argv.includes("--disconnect-after")) {
  const seconds = parseInt(process.argv[process.argv.indexOf("--disconnect-after") + 1]) || 30;
  console.log(`⏰ Will disconnect after ${seconds} seconds`);
  setTimeout(() => {
    console.log("⏰ Automatic disconnect");
    ws.close(1000, "Automatic disconnect");
  }, seconds * 1000);
}

console.log("\nOptions:");
console.log("  --no-pong              Don't respond to server pings (test dead connection detection)");
console.log("  --disconnect-after N   Disconnect after N seconds");
console.log("\nPress Ctrl+C to disconnect gracefully");
