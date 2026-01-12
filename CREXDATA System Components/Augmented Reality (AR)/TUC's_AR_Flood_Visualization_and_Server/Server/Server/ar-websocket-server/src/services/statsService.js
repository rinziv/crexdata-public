const logger = require("../utils/logger");

const serverStats = {
  startTime: new Date(),
  totalConnections: 0, // Incremented on new connection attempt
  currentConnections: 0, // Incremented on successful connection, decremented on close
  lastConnectionTime: null, // Updated on each new connection
  messagesSent: 0,
  messagesReceived: 0,
  errors: 0,
  lastError: null,
  kafkaErrors: 0,
  lastKafkaError: null,
};

function incrementTotalConnections() {
  serverStats.totalConnections++;
}

function recordLastConnectionTime() {
  serverStats.lastConnectionTime = new Date().toISOString();
}

function incrementCurrentConnections() {
  serverStats.currentConnections++;
}

function decrementCurrentConnections() {
  // Prevent going below zero
  serverStats.currentConnections = Math.max(0, serverStats.currentConnections - 1);
}

function incrementMessagesSent() {
  serverStats.messagesSent++;
}

function incrementMessagesReceived() {
  serverStats.messagesReceived++;
}

function recordError(error) {
  serverStats.errors++;
  serverStats.lastError = {
    time: new Date().toISOString(),
    message: error?.message || "Unknown error",
  };
}

function recordKafkaError(error) {
  serverStats.kafkaErrors++;
  serverStats.lastKafkaError = {
    time: new Date().toISOString(),
    message: error?.message || "Unknown Kafka error",
  };
  // Optionally log the error here as well
  // logger.error("Recorded Kafka error:", error);
}

function getStats() {
  // Return a copy to prevent direct modification
  return { ...serverStats };
}

module.exports = {
  incrementTotalConnections,
  incrementCurrentConnections,
  recordLastConnectionTime,
  decrementCurrentConnections,
  incrementMessagesSent,
  incrementMessagesReceived,
  recordError,
  recordKafkaError,
  getStats,
};
