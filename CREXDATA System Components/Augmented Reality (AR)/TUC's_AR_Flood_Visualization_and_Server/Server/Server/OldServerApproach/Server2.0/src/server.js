const express = require('express');
const bodyParser = require('body-parser');
const logger = require('./utils/logger');
const routes = require('./api/routes');
const { initializeKafkaConnection } = require('./services/kafkaService');
const { initializeWebSocketServer } = require('./services/websocketService');

const app = express();
const PORT = process.env.PORT || 3000;

app.use(bodyParser.json());
app.use(bodyParser.urlencoded({ extended: true }));

// Initialize routes
app.use('/api', routes);

// Start the server
app.listen(PORT, () => {
  logger.info(`Server is running on port ${PORT}`);
});

// Initialize Kafka connection
initializeKafkaConnection();

// Initialize WebSocket server
initializeWebSocketServer();