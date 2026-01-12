class Client {
  constructor(ws, type, id, deviceId, name, useCase) {
    if (!ws || !type || !id) {
      throw new Error("Client constructor requires ws, type, and id.");
    }
    this.ws = ws; // The WebSocket connection object
    this.type = type; // e.g., "unity", "phone", "marine"
    this.useCase = useCase || "default"; // Use case for the client, e.g., "marine", "flood"
    this.id = id; // The unique identifier for this client (e.g., unityId, phoneId)
    this.deviceId = deviceId || "unknown"; // Device ID assigned during connection
    this.name = name || "Unknown";
    this.connectionTime = new Date().toISOString();
    this.ipAddress = ws.ipAddress || "unknown"; // IP address assigned during connection
    this.userAgent = ws.userAgent || "unknown"; // User agent assigned during connection
    this.connectionId = ws.connectionId || "unknown"; // Unique ID for the WS connection itself
    this.pairedClientId = null; // ID of the client this one is paired with
    this.kafkaConsumers = new Map(); // Stores consumers, keyed by kafkaConfigKey (e.g., "KAFKA_AUTH")
    this.isConnected = true; // Flag to track connection status
  }

  setPairedClient(clientId) {
    this.pairedClientId = clientId;
  }

  clearPairedClient() {
    this.pairedClientId = null;
  }

  /**
   * Sets a Kafka consumer for a specific configuration.
   * @param {string} kafkaConfigKey The key of the Kafka configuration (e.g., "KAFKA_AUTH").
   * @param {object|null} consumer The Kafka consumer instance, or null to remove.
   */
  setKafkaConsumer(kafkaConfigKey, consumer) {
    if (consumer) {
      this.kafkaConsumers.set(kafkaConfigKey, consumer);
    } else {
      this.kafkaConsumers.delete(kafkaConfigKey);
    }
  }

  /**
   * Gets the Kafka consumer for a specific configuration.
   * @param {string} kafkaConfigKey The key of the Kafka configuration.
   * @returns {object|undefined} The Kafka consumer instance or undefined if not found.
   */
  getKafkaConsumer(kafkaConfigKey) {
    return this.kafkaConsumers.get(kafkaConfigKey);
  }

  /**
   * Gets all Kafka consumers associated with this client.
   * @returns {Array<object>} An array of Kafka consumer instances.
   */
  getAllKafkaConsumers() {
    return Array.from(this.kafkaConsumers.values());
  }

  /**
   * Clears the Kafka consumer reference for a specific configuration.
   * @param {string} kafkaConfigKey The key of the Kafka configuration.
   */
  clearKafkaConsumer(kafkaConfigKey) {
    this.kafkaConsumers.delete(kafkaConfigKey);
  }

  /**
   * Clears all Kafka consumer references for this client.
   */
  clearAllKafkaConsumers() {
    this.kafkaConsumers.clear();
  }

  markDisconnected() {
    this.isConnected = false;
    // Note: Actual consumer disconnection is handled by kafkaService.
    // Clearing references here is good practice.
    this.clearAllKafkaConsumers();
  }
}

module.exports = Client;
