// src/handlers/messageHandler.js
export function handleIncomingMessage(ws, message) {
  try {
    const data = JSON.parse(message);
    switch (data.type) {
      case "registration":
        handleRegistration(ws, data);
        break;
      case "pair":
        handlePairing(ws, data);
        break;
      case "gps":
        handleGpsData(ws, data);
        break;
      default:
        handleUnknownMessage(ws, data);
    }
  } catch (error) {
    console.error("Error handling incoming message:", error);
    sendErrorMessage(ws, "Invalid message format");
  }
}

function handleRegistration(ws, data) {
  // Logic for handling registration messages
}

function handlePairing(ws, data) {
  // Logic for handling pairing messages
}

function handleGpsData(ws, data) {
  // Logic for handling GPS data messages
}

function handleUnknownMessage(ws, data) {
  console.warn("Received unknown message type:", data.type);
  sendErrorMessage(ws, "Unknown message type");
}

function sendErrorMessage(ws, message) {
  ws.send(JSON.stringify({ type: "error", message }));
}