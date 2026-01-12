// src/services/websocketService.js
export function initializeWebSocketServer(server) {
  const wss = new WebSocket.Server({ server });

  wss.on('connection', (ws) => {
    console.log('New client connected');

    ws.on('message', (message) => {
      console.log(`Received message: ${message}`);
      // Handle incoming messages
    });

    ws.on('close', () => {
      console.log('Client disconnected');
    });
  });

  return wss;
}

export function sendMessage(ws, message) {
  if (ws.readyState === WebSocket.OPEN) {
    ws.send(message);
  } else {
    console.error('WebSocket is not open. Unable to send message.');
  }
}

export function broadcast(wss, message) {
  wss.clients.forEach((client) => {
    if (client.readyState === WebSocket.OPEN) {
      client.send(message);
    }
  });
}