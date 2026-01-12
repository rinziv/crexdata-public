const { Kafka, logLevel } = require("kafkajs");
const fs = require("fs");
const logger = require("../utils/logger");
const { CONFIG } = require("../config");
const { sendWsMessage } = require("../websocket/wsUtils");
const statsService = require("./statsService");

// Map<kafkaConfigKey, KafkaInstance> - Stores initialized Kafka client instances
const kafkaInstances = new Map();
// Map<clientId, Map<kafkaConfigKey, KafkaConsumer>> - Stores active consumers
const clientConsumers = new Map();
// Map<kafkaConfigKey, KafkaProducer> - Stores initialized Kafka producers for publishing
const kafkaProducers = new Map();

/**
 * Initializes the main Kafka client.
 */
function getKafkaInstance(kafkaConfigKey) {
  if (kafkaInstances.has(kafkaConfigKey)) {
    return kafkaInstances.get(kafkaConfigKey);
  }

  const selectedConfig = CONFIG[kafkaConfigKey];
  if (!selectedConfig) {
    const errMsg = `Kafka configuration for key "${kafkaConfigKey}" not found.`;
    logger.error(errMsg);
    throw new Error(errMsg);
  }

  logger.info(`Initializing Kafka client instance for config "${kafkaConfigKey}"...`, { brokers: selectedConfig.BROKERS });

  const kafkaJsConfig = {
    clientId: selectedConfig.CLIENT_ID || `ar-ws-${kafkaConfigKey}`,
    brokers: selectedConfig.BROKERS,
    connectionTimeout: selectedConfig.CONNECTION_TIMEOUT,
    retry: {
      retries: selectedConfig.RETRY_RETRIES,
      initialRetryTime: selectedConfig.RETRY_INITIAL_TIME,
      maxRetryTime: selectedConfig.RETRY_MAX_TIME,
    },
    logLevel: CONFIG.DEBUG ? logLevel.DEBUG : logLevel.INFO,
    logCreator: () => logger.kafka,
  };

  if (selectedConfig.SSL_ENABLED) {
    logger.info(`SSL is enabled for Kafka config: ${kafkaConfigKey}`);
    try {
      kafkaJsConfig.ssl = {
        rejectUnauthorized: selectedConfig.SSL_REJECT_UNAUTHORIZED !== undefined ? selectedConfig.SSL_REJECT_UNAUTHORIZED : true,
        ca: [fs.readFileSync(selectedConfig.SSL_CA_PATH, "utf-8")],
        key: fs.readFileSync(selectedConfig.SSL_KEY_PATH, "utf-8"),
        cert: fs.readFileSync(selectedConfig.SSL_CERT_PATH, "utf-8"),
      };
      if (selectedConfig.SSL_KEY_PASSPHRASE) {
        kafkaJsConfig.ssl.passphrase = selectedConfig.SSL_KEY_PASSPHRASE;
      }
    } catch (sslError) {
      logger.error(`Failed to read SSL certs for Kafka config ${kafkaConfigKey}:`, sslError);
      throw new Error(`SSL configuration error for ${kafkaConfigKey}: ${sslError.message}`);
    }
  }

  if (selectedConfig.USERNAME && selectedConfig.PASSWORD && selectedConfig.AUTH_MECHANISM) {
    logger.info(`SASL authentication enabled for Kafka config ${kafkaConfigKey} with mechanism: ${selectedConfig.AUTH_MECHANISM}`);
    kafkaJsConfig.sasl = {
      mechanism: selectedConfig.AUTH_MECHANISM,
      username: selectedConfig.USERNAME,
      password: selectedConfig.PASSWORD,
    };
  }

  try {
    const newInstance = new Kafka(kafkaJsConfig);
    kafkaInstances.set(kafkaConfigKey, newInstance);
    logger.info(`Kafka client instance for config "${kafkaConfigKey}" initialized successfully.`);
    return newInstance;
  } catch (initError) {
    logger.error(`Failed to initialize Kafka client instance for config ${kafkaConfigKey}:`, initError);
    statsService.recordKafkaError({ error: initError.message, config: kafkaConfigKey, stage: "instanceInitialization" });
    throw initError;
  }
}

/**
 * Creates and manages a Kafka consumer for a specific client and Kafka configuration.
 * @param {Client} client The client instance.
 * @param {string} kafkaConfigKey The key for the Kafka configuration in CONFIG.js (e.g., "KAFKA_AUTH").
 */
async function createConsumerForClient(client, kafkaConfigKey) {
  if (!client || !client.id) {
    logger.error("Invalid client object provided to createConsumerForClient.");
    return;
  }

  let kafkaInstance;
  try {
    kafkaInstance = getKafkaInstance(kafkaConfigKey);
  } catch (instanceError) {
    logger.error(`Cannot create consumer for client ${client.id}: Failed to get Kafka instance for config ${kafkaConfigKey}:`, instanceError);
    sendWsMessage(client.ws, { type: "kafkaStatus", configKey: kafkaConfigKey, status: "error", reason: `Server Kafka instance error for ${kafkaConfigKey}.` });
    return;
  }

  const selectedKafkaConfig = CONFIG[kafkaConfigKey]; // Already validated in getKafkaInstance
  const topicName = selectedKafkaConfig.TOPIC;

  if (!clientConsumers.has(client.id)) {
    clientConsumers.set(client.id, new Map());
  }
  const clientConfigConsumersMap = clientConsumers.get(client.id);

  if (client.type !== "unity") {
    // Only Unity clients consume, adjust as needed
    logger.debug(`Kafka consumer for config ${kafkaConfigKey} (topic ${topicName}) not typically created for client type: ${client.type} (id: ${client.id}). Skipping.`);
    return;
  }

  if (clientConfigConsumersMap.has(kafkaConfigKey) || client.getKafkaConsumer(kafkaConfigKey)) {
    logger.warn(`Consumer for config ${kafkaConfigKey} (topic ${topicName}) already exists or is registered for client ${client.id}. Skipping creation.`);
    return;
  }

  const groupId = `ws-consumer-${client.id}-${topicName}`;
  logger.info(`Creating Kafka consumer for client ${client.id} using config '${kafkaConfigKey}' (Group: ${groupId}) for topic ${topicName}`);

  const consumerOptions = { groupId: groupId };
  // Add other consumer-specific options from selectedKafkaConfig if they exist
  // e.g., if (selectedKafkaConfig.SESSION_TIMEOUT) consumerOptions.sessionTimeout = selectedKafkaConfig.SESSION_TIMEOUT;

  const consumer = kafkaInstance.consumer(consumerOptions);
  clientConfigConsumersMap.set(kafkaConfigKey, consumer);
  client.setKafkaConsumer(kafkaConfigKey, consumer);

  consumer._disconnectInitiated = false;

  let attempts = 0;
  const maxAttempts = selectedKafkaConfig.CONSUMER_MAX_ATTEMPTS || 5;
  const fromBeginning = selectedKafkaConfig.FROM_BEGINNING !== undefined ? selectedKafkaConfig.FROM_BEGINNING : true;

  while (client.isConnected && attempts < maxAttempts) {
    try {
      attempts++;

      // Check if disconnect was initiated during a previous attempt's error/delay
      if (consumer._disconnectInitiated) {
        logger.warn(`Kafka consumer creation for ${client.id} (config ${kafkaConfigKey}, topic ${topicName}) aborted: disconnect was initiated. Attempt ${attempts}.`);
        break; // Exit retry loop; disconnect path handles actual cleanup.
      }

      logger.info(`Attempting Kafka consumer connection for ${client.id} to topic ${topicName} (config: ${kafkaConfigKey}, Attempt ${attempts}/${maxAttempts})...`);
      await consumer.connect();

      if (consumer._disconnectInitiated) {
        logger.warn(`Kafka consumer for ${client.id} (config ${kafkaConfigKey}, topic ${topicName}) was marked for disconnect after connect. Aborting run.`);
        break;
      }

      logger.info(`Consumer connected for ${client.id} to topic ${topicName}. Subscribing (fromBeginning: ${fromBeginning})...`);
      await consumer.subscribe({ topic: topicName, fromBeginning: fromBeginning });

      if (consumer._disconnectInitiated) {
        logger.warn(`Kafka consumer for ${client.id} (config ${kafkaConfigKey}, topic ${topicName}) was marked for disconnect after subscribe. Aborting run.`);
        break;
      }

      logger.info(`Consumer subscribed for ${client.id} to topic ${topicName}. Starting message processing.`);

      await consumer.run({
        eachMessage: async ({ topic, partition, message }) => {
          if (!client.isConnected || client.ws.readyState !== require("ws").OPEN) {
            logger.warn(`Kafka consumer for client ${client.id} (topic ${topicName}, config ${kafkaConfigKey}) is stopping because its client instance is no longer connected or WebSocket is closed.`);
            // Throwing an error here will stop this consumer.run() loop.
            throw new Error(`Client ${client.id} (for consumer on topic ${topicName}) is disconnected.`);
          }

          processKafkaMessage({
            client,
            topic,
            partition,
            message,
            kafkaConfigKey,
            topicName,
          });
        },
      });
      // This block is reached if consumer.run() completes (e.g., due to consumer.disconnect() or error in eachMessage)
      if (consumer._disconnectInitiated) {
        logger.info(`Kafka consumer for ${client.id} on topic ${topicName} (config: ${kafkaConfigKey}) run completed and was marked for disconnect (normal shutdown).`);
      } else if (!client.isConnected) {
        logger.info(`Kafka consumer for ${client.id} on topic ${topicName} (config: ${kafkaConfigKey}) run completed due to client disconnect.`);
      } else {
        // This might indicate an unexpected stop if the client is still connected and no disconnect was initiated.
        logger.warn(
          `Kafka consumer for ${client.id} on topic ${topicName} (config: ${kafkaConfigKey}) run completed for an unknown reason while client still connected. (This is normal if the client is connected normally.)`
        );
      }

      sendWsMessage(client.ws, { type: "kafkaStatus", configKey: kafkaConfigKey, topic: topicName, status: "ready" });
      break; // Exit retry loop on successful run setup
    } catch (error) {
      logger.error(`Kafka consumer error for client ${client.id} on topic ${topicName} (config: ${kafkaConfigKey}, Attempt ${attempts}):`, error);
      statsService.recordKafkaError({ error: error.message, client: client.id, config: kafkaConfigKey, topic: topicName, stage: "consumerRun" });

      if (consumer._disconnectInitiated || (error.message.includes(`Client ${client.id}`) && (error.message.includes("is disconnected") || error.message.includes("marked for disconnect")))) {
        logger.info(`Stopping Kafka connection attempts for topic ${topicName} (config: ${kafkaConfigKey}) for client ${client.id} as it's disconnected or consumer is marked for disconnect.`);
        break; // Exit retry loop
      }

      if (client.isConnected && attempts < maxAttempts) {
        const delayBase = selectedKafkaConfig.CONSUMER_RETRY_DELAY_BASE || 1000;
        const delayMax = selectedKafkaConfig.CONSUMER_RETRY_DELAY_MAX || 10000;
        const delay = Math.min(delayBase * Math.pow(2, attempts - 1), delayMax);
        logger.info(`Retrying Kafka consumer connection for ${client.id} to topic ${topicName} (config: ${kafkaConfigKey}) in ${delay}ms...`);
        await new Promise((resolve) => setTimeout(resolve, delay));
      } else {
        logger.error(`Max Kafka consumer connection attempts reached or client disconnected for ${client.id} on topic ${topicName} (config: ${kafkaConfigKey}). Giving up.`);
        if (client.isConnected) {
          sendWsMessage(client.ws, { type: "kafkaStatus", configKey: kafkaConfigKey, topic: topicName, status: "failed", reason: "Max connection attempts reached." });
        }
        await disconnectConsumerForClient(client, kafkaConfigKey); // Clean up this specific failed consumer
        break;
      }
    }
    if (consumer._disconnectInitiated) {
      logger.info(`Post-loop check: Consumer for ${client.id} (config ${kafkaConfigKey}, topic ${topicName}) was marked for disconnect. Ensuring cleanup if not already handled.`);
    }
  }
}

/**
 * Handles an individual Kafka message for a client.
 * @param {object} params Parameters for message handling.
 * @param {Client} params.client The client instance.
 * @param {string} params.topic The Kafka topic the message came from.
 * @param {number} params.partition The Kafka partition.
 * @param {object} params.message The Kafka message object.
 * @param {string} params.kafkaConfigKey The Kafka configuration key used.
 * @param {string} params.topicName The specific topic name (might be same as topic).
 */
function processKafkaMessage({ client, topic, partition, message, kafkaConfigKey, topicName }) {
  // The original client instance is still valid. Proceed to process and send the message.
  logger.debug(`Kafka message received for ${client.id} on topic ${topicName} (config: ${kafkaConfigKey})`, { partition, offset: message.offset.toString() });
  statsService.incrementMessagesReceived();

  // Message Handler Logic (As of now all messages are considered JSON to spawn a POI)
  // This can be adjusted based on the actual message structure and requirements.
  try {
    const kafkaData = JSON.parse(message.value.toString());

    let messageType = CONFIG[kafkaConfigKey].TYPE || "kafkaData";

    // Override type based on client use case if needed
    if (client.useCase === "marine" && messageType === "kafkaData") {
      messageType = "MarineTraffic_route";
    }

    const formattedMessage = {
      type: messageType,
      payload: kafkaData,
      meta: { topic, partition, offset: message.offset.toString(), kafkaConfigKey },
    };
    sendWsMessage(client.ws, formattedMessage); // Send via the original client's WebSocket
  } catch (parseError) {
    logger.warn(`Non-JSON Kafka message for ${client.id} on topic ${topicName} (config: ${kafkaConfigKey}), wrapping.`, { offset: message.offset.toString(), error: parseError.message });
    const formattedMessage = {
      type: "rawData",
      payload: message.value.toString(),
      meta: { topic, partition, offset: message.offset.toString(), kafkaConfigKey },
    };
    sendWsMessage(client.ws, formattedMessage); // Send via the original client's WebSocket
  }
}

/**
 * Disconnects a specific Kafka consumer for a client.
 * @param {Client} client The client instance.
 * @param {string} kafkaConfigKey The Kafka configuration key.
 */
async function disconnectConsumerForClient(client, kafkaConfigKey) {
  if (!client || !client.id || !kafkaConfigKey) {
    logger.debug("disconnectConsumerForClient: Invalid client or kafkaConfigKey.");
    return;
  }

  const clientConfigConsumersMap = clientConsumers.get(client.id);
  const consumer = clientConfigConsumersMap ? clientConfigConsumersMap.get(kafkaConfigKey) : null;
  const topicName = CONFIG[kafkaConfigKey]?.TOPIC || "unknown-topic";

  if (consumer) {
    consumer._disconnectInitiated = true; // Mark the consumer instance
    logger.info(`Disconnecting Kafka consumer for client ${client.id} using config '${kafkaConfigKey}' (topic: ${topicName})...`);
    try {
      await consumer.disconnect();
      logger.info(`Kafka consumer for config '${kafkaConfigKey}' (topic: ${topicName}) disconnected successfully for ${client.id}.`);
    } catch (error) {
      logger.error(`Error disconnecting Kafka consumer for client ${client.id} (config: ${kafkaConfigKey}, topic: ${topicName}):`, error);
      statsService.recordKafkaError({ error: error.message, client: client.id, config: kafkaConfigKey, topic: topicName, stage: "disconnect" });
    } finally {
      if (clientConfigConsumersMap) {
        clientConfigConsumersMap.delete(kafkaConfigKey);
        if (clientConfigConsumersMap.size === 0) {
          clientConsumers.delete(client.id);
        }
      }
      client.clearKafkaConsumer(kafkaConfigKey);
    }
  } else {
    logger.debug(`No active Kafka consumer for config '${kafkaConfigKey}' (topic: ${topicName}) to disconnect for client ${client.id}.`);
  }
}

/**
 * Disconnects all Kafka consumers associated with a client.
 * @param {Client} client The client instance.
 */
async function disconnectAllConsumersForClient(client) {
  if (!client || !client.id) {
    logger.debug("disconnectAllConsumersForClient: Invalid client.");
    return;
  }

  const clientConfigConsumersMap = clientConsumers.get(client.id);
  if (clientConfigConsumersMap && clientConfigConsumersMap.size > 0) {
    logger.info(`Disconnecting all Kafka consumers for client ${client.id}...`);
    for (const [kafkaConfigKey, consumer] of clientConfigConsumersMap.entries()) {
      const topicName = CONFIG[kafkaConfigKey]?.TOPIC || "unknown-topic";
      logger.info(`Disconnecting consumer for config '${kafkaConfigKey}' (topic: ${topicName}) for client ${client.id} (batch)...`);
      consumer._disconnectInitiated = true; // Mark the consumer instance
      try {
        await consumer.disconnect();
        logger.info(`Consumer for config '${kafkaConfigKey}' (topic: ${topicName}) disconnected for ${client.id} (batch).`);
      } catch (error) {
        logger.error(`Error disconnecting consumer (batch) for client ${client.id} (config: ${kafkaConfigKey}, topic: ${topicName}):`, error);
        statsService.recordKafkaError({ error: error.message, client: client.id, config: kafkaConfigKey, topic: topicName, stage: "batchDisconnect" });
      }
    }
    clientConsumers.delete(client.id);
    logger.info(`All Kafka consumers processed for disconnection for client ${client.id}.`);
  } else {
    logger.debug(`No Kafka consumers found in service map to disconnect for client ${client.id}.`);
  }
  client.clearAllKafkaConsumers(); // Ensure client object references are cleared
}

/**
 * Handles seek requests for a specific consumer.
 * @param {string} clientId The ID of the client.
 * @param {string} kafkaConfigKey The Kafka configuration key.
 * @param {number} partition The partition number.
 * @param {string} offset The offset to seek to.
 */
async function seekConsumerOffset(clientId, kafkaConfigKey, partition, offset) {
  const clientConfigConsumersMap = clientConsumers.get(clientId);
  const consumer = clientConfigConsumersMap ? clientConfigConsumersMap.get(kafkaConfigKey) : null;

  if (!consumer) {
    logger.warn(`Cannot seek: Kafka consumer not found for client ${clientId} with config ${kafkaConfigKey}`);
    return false;
  }

  const selectedKafkaConfig = CONFIG[kafkaConfigKey];
  if (!selectedKafkaConfig || !selectedKafkaConfig.TOPIC) {
    logger.error(`Cannot seek: Invalid kafkaConfigKey '${kafkaConfigKey}' or missing TOPIC for client ${clientId}.`);
    return false;
  }
  const topicName = selectedKafkaConfig.TOPIC;

  try {
    logger.info(`Seeking Kafka consumer for ${clientId} (config: ${kafkaConfigKey}, topic: ${topicName})`, { partition, offset });
    const partitionNum = parseInt(partition, 10);
    const offsetStr = offset.toString();
    if (isNaN(partitionNum)) {
      logger.error(`Invalid partition number for seek: ${partition}`);
      return false;
    }
    consumer.seek({ topic: topicName, partition: partitionNum, offset: offsetStr });
    return true;
  } catch (error) {
    logger.error(`Error during Kafka seek for client ${clientId} (config: ${kafkaConfigKey}, topic: ${topicName}):`, error);
    statsService.recordKafkaError({ error: error.message, client: clientId, config: kafkaConfigKey, topic: topicName, action: "seek" });
    return false;
  }
}

/**
 * Gets or creates a Kafka producer for a specific configuration.
 * Producers are reused across multiple publish operations.
 * @param {string} kafkaConfigKey The Kafka configuration key.
 * @returns {Promise<Producer>} The Kafka producer instance.
 */
async function getKafkaProducer(kafkaConfigKey) {
  if (kafkaProducers.has(kafkaConfigKey)) {
    const existingProducer = kafkaProducers.get(kafkaConfigKey);
    if (existingProducer._connected) {
      return existingProducer;
    }
    // If disconnected, remove and create new one
    kafkaProducers.delete(kafkaConfigKey);
  }

  try {
    const kafkaInstance = getKafkaInstance(kafkaConfigKey);
    const producer = kafkaInstance.producer({
      // Producer-specific configuration
      allowAutoTopicCreation: false, // Prevent accidental topic creation
      transactionTimeout: 30000,
    });

    logger.debug(`Connecting Kafka producer for config '${kafkaConfigKey}'...`);
    await producer.connect();

    // Mark as connected
    producer._connected = true;

    // Handle disconnect events
    producer.on("producer.disconnect", () => {
      logger.warn(`Kafka producer for config '${kafkaConfigKey}' disconnected.`);
      producer._connected = false;
      kafkaProducers.delete(kafkaConfigKey);
    });

    kafkaProducers.set(kafkaConfigKey, producer);
    logger.debug(`Kafka producer for config '${kafkaConfigKey}' connected successfully.`);

    return producer;
  } catch (error) {
    logger.error(`Failed to get/create Kafka producer for config '${kafkaConfigKey}':`, error);
    statsService.recordKafkaError({ error: error.message, config: kafkaConfigKey, stage: "producerInitialization" });
    throw error;
  }
}

/**
 * Publishes a JSON message to a Kafka topic.
 * @param {string} kafkaConfigKey The Kafka configuration key to use for publishing.
 * @param {object} messageData The JSON object to publish.
 * @param {object} options Optional publishing options.
 * @param {string} options.key Optional message key for partitioning.
 * @param {object} options.headers Optional message headers.
 * @param {number} options.partition Optional specific partition to publish to.
 * @returns {Promise<object>} Publishing result with partition and offset information.
 */
async function publishToKafka(kafkaConfigKey, messageData, options = {}) {
  const selectedConfig = CONFIG[kafkaConfigKey];

  if (!selectedConfig || !selectedConfig.TOPIC) {
    const errMsg = `Invalid Kafka configuration key "${kafkaConfigKey}" or missing TOPIC for publishing.`;
    logger.error(errMsg);
    throw new Error(errMsg);
  }

  const topicName = selectedConfig.TOPIC;

  try {
    const producer = await getKafkaProducer(kafkaConfigKey);

    // Prepare message payload
    const messagePayload = {
      topic: topicName,
      messages: [
        {
          key: options.key || `msg-${Date.now()}`,
          value: JSON.stringify(messageData),
          timestamp: Date.now().toString(),
          headers: options.headers || {},
        },
      ],
    };

    // Add partition if specified
    if (options.partition !== undefined) {
      messagePayload.messages[0].partition = options.partition;
    }

    // logger.debug(`Publishing message to Kafka topic '${topicName}' (config: ${kafkaConfigKey})`, {
    //   key: messagePayload.messages[0].key,
    //   messageType: messageData.type,
    // });

    const result = await producer.send(messagePayload);

    logger.debug(`Message published successfully to topic '${topicName}' (config: ${kafkaConfigKey})`, {
      partition: result[0].partition,
      offset: result[0].baseOffset,
      key: options.key,
    });

    return {
      success: true,
      topic: topicName,
      partition: result[0].partition,
      offset: result[0].baseOffset,
      timestamp: messagePayload.messages[0].timestamp,
    };
  } catch (error) {
    logger.error(`Failed to publish message to Kafka topic '${topicName}' (config: ${kafkaConfigKey}):`, error);
    statsService.recordKafkaError({
      error: error.message,
      config: kafkaConfigKey,
      topic: topicName,
      stage: "publish",
    });

    throw error;
  }
}

/**
 * Publishes multiple messages to a Kafka topic in a batch.
 * More efficient than multiple single publishes.
 * @param {string} kafkaConfigKey The Kafka configuration key to use for publishing.
 * @param {Array<object>} messages Array of message data objects to publish.
 * @param {object} options Optional publishing options.
 * @returns {Promise<object>} Publishing result.
 */
async function publishBatchToKafka(kafkaConfigKey, messages, options = {}) {
  const selectedConfig = CONFIG[kafkaConfigKey];

  if (!selectedConfig || !selectedConfig.TOPIC) {
    const errMsg = `Invalid Kafka configuration key "${kafkaConfigKey}" or missing TOPIC for batch publishing.`;
    logger.error(errMsg);
    throw new Error(errMsg);
  }

  const topicName = selectedConfig.TOPIC;

  try {
    const producer = await getKafkaProducer(kafkaConfigKey);

    // Prepare batch message payload
    const messagePayload = {
      topic: topicName,
      messages: messages.map((msg, index) => ({
        key: options.keyPrefix ? `${options.keyPrefix}-${index}` : `batch-${Date.now()}-${index}`,
        value: JSON.stringify(msg),
        timestamp: Date.now().toString(),
        headers: options.headers || {},
      })),
    };

    logger.info(`Publishing ${messages.length} messages in batch to Kafka topic '${topicName}' (config: ${kafkaConfigKey})`);

    const result = await producer.send(messagePayload);

    logger.debug(`Batch published successfully to topic '${topicName}' (config: ${kafkaConfigKey})`, {
      messageCount: messages.length,
      partitions: result.map((r) => r.partition),
    });

    return {
      success: true,
      topic: topicName,
      messageCount: messages.length,
      results: result,
    };
  } catch (error) {
    logger.error(`Failed to publish batch to Kafka topic '${topicName}' (config: ${kafkaConfigKey}):`, error);
    statsService.recordKafkaError({
      error: error.message,
      config: kafkaConfigKey,
      topic: topicName,
      stage: "batchPublish",
      messageCount: messages.length,
    });

    throw error;
  }
}

/**
 * Disconnects a specific Kafka producer.
 * @param {string} kafkaConfigKey The Kafka configuration key.
 */
async function disconnectProducer(kafkaConfigKey) {
  const producer = kafkaProducers.get(kafkaConfigKey);

  if (producer) {
    logger.info(`Disconnecting Kafka producer for config '${kafkaConfigKey}'...`);
    try {
      await producer.disconnect();
      logger.info(`Kafka producer for config '${kafkaConfigKey}' disconnected successfully.`);
    } catch (error) {
      logger.error(`Error disconnecting Kafka producer for config '${kafkaConfigKey}':`, error);
    } finally {
      kafkaProducers.delete(kafkaConfigKey);
    }
  }
}

/**
 * Disconnects all Kafka producers.
 * Should be called during server shutdown.
 */
async function disconnectAllProducers() {
  logger.info("Disconnecting all Kafka producers...");
  const promises = [];

  for (const [configKey, producer] of kafkaProducers.entries()) {
    promises.push(
      producer
        .disconnect()
        .then(() => logger.info(`Kafka producer for config '${configKey}' disconnected.`))
        .catch((error) => logger.error(`Error disconnecting producer for config '${configKey}':`, error))
    );
  }

  await Promise.all(promises);
  kafkaProducers.clear();
  logger.info("All Kafka producers disconnected.");
}

/**
 * Disconnects all initialized Kafka client instances.
 * Intended for server shutdown.
 */
async function disconnectAllKafkaInstances() {
  logger.info("Disconnecting all Kafka client instances...");

  // Disconnect all producers first
  await disconnectAllProducers();

  kafkaInstances.clear();
  logger.info("All Kafka client instances processed for shutdown.");
}

module.exports = {
  createConsumerForClient,
  disconnectConsumerForClient,
  disconnectAllConsumersForClient,
  disconnectAllKafkaInstances,
  seekConsumerOffset,
  // Publisher functions
  publishToKafka,
  publishBatchToKafka,
  getKafkaProducer,
  disconnectProducer,
  disconnectAllProducers,
};
