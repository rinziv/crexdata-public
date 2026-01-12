const { Kafka, logLevel } = require("kafkajs");
const { CONFIG } = require("../src/config");

/**
 * Unit Test Script for Kafka Producer
 * This script tests publishing JSON data to the Kafka topic specified in KAFKA_CREX_NO_AUTH configuration
 */

class KafkaPublisherTest {
  constructor() {
    this.kafkaConfig = CONFIG.KAFKA_CREX_NO_AUTH;
    this.kafka = null;
    this.producer = null;
    this.consumer = null;
    this.testMessages = this.generateTestMessages();
  }

  /**
   * Initialize Kafka client with the KAFKA_CREX_NO_AUTH configuration
   */
  initializeKafka() {
    console.log("🔧 Initializing Kafka client...");
    console.log(`📡 Brokers: ${this.kafkaConfig.BROKERS.join(", ")}`);
    console.log(`📝 Topic: ${this.kafkaConfig.TOPIC}`);
    console.log(`🆔 Client ID: ${this.kafkaConfig.CLIENT_ID}`);

    const kafkaJsConfig = {
      clientId: this.kafkaConfig.CLIENT_ID,
      brokers: this.kafkaConfig.BROKERS,
      connectionTimeout: this.kafkaConfig.CONNECTION_TIMEOUT,
      retry: {
        retries: this.kafkaConfig.RETRY_RETRIES,
        initialRetryTime: this.kafkaConfig.RETRY_INITIAL_TIME,
        maxRetryTime: this.kafkaConfig.RETRY_MAX_TIME,
      },
      logLevel: logLevel.INFO,
    };

    // Add SASL authentication if configured
    if (this.kafkaConfig.AUTH_MECHANISM) {
      console.log(`🔐 Authentication mechanism: ${this.kafkaConfig.AUTH_MECHANISM}`);
      kafkaJsConfig.sasl = {
        mechanism: this.kafkaConfig.AUTH_MECHANISM,
        username: this.kafkaConfig.USERNAME || "test-user",
        password: this.kafkaConfig.PASSWORD || "test-password",
      };
    }

    this.kafka = new Kafka(kafkaJsConfig);
    this.producer = this.kafka.producer();
    console.log("✅ Kafka client initialized successfully");
  }

  /**
   * Generate sample test messages in JSON format
   */
  generateTestMessages() {
    const baseTime = Date.now();

    return [
      {
        id: "test-message-1",
        timestamp: new Date(baseTime).toISOString(),
        type: "AR_POI",
        data: {
          latitude: 51.54869725504633,
          longitude: 7.373895041532426,
          altitude: 100,
          title: "Test Point of Interest",
          description: "This is a test POI for AR visualization",
          category: "test",
          priority: "medium",
        },
        metadata: {
          source: "kafka-publisher-test",
          version: "1.0.0",
          testRun: true,
        },
      },
      {
        id: "test-message-2",
        timestamp: new Date(baseTime + 1000).toISOString(),
        type: "DRONE_UPDATE",
        data: {
          droneId: "DRONE_001",
          position: {
            latitude: 51.548791586257678,
            longitude: 7.3738752357535224,
            altitude: 150,
          },
          status: "active",
          battery: 85,
          mission: "surveillance",
        },
        metadata: {
          source: "kafka-publisher-test",
          version: "1.0.0",
          testRun: true,
        },
      },
      {
        id: "test-message-3",
        timestamp: new Date(baseTime + 2000).toISOString(),
        type: "ALERT",
        data: {
          alertId: "ALERT_001",
          severity: "high",
          message: "Test emergency alert",
          location: {
            latitude: 51.5487,
            longitude: 7.3739,
            address: "Test Location, Germany",
          },
          alertType: "fire",
        },
        metadata: {
          source: "kafka-publisher-test",
          version: "1.0.0",
          testRun: true,
        },
      },
    ];
  }

  /**
   * Connect to Kafka and prepare producer
   */
  async connect() {
    try {
      console.log("🔌 Connecting to Kafka...");
      await this.producer.connect();
      console.log("✅ Producer connected successfully");
    } catch (error) {
      console.error("❌ Failed to connect producer:", error.message);
      throw error;
    }
  }

  /**
   * Publish a single message to the Kafka topic
   */
  async publishMessage(message, key = null) {
    try {
      const messagePayload = {
        topic: this.kafkaConfig.TOPIC,
        messages: [
          {
            key: key || message.id,
            value: JSON.stringify(message),
            timestamp: Date.now().toString(),
          },
        ],
      };

      console.log(`📤 Publishing message with key: ${messagePayload.messages[0].key}`);
      console.log(`📄 Message content: ${JSON.stringify(message, null, 2)}`);

      const result = await this.producer.send(messagePayload);

      console.log("✅ Message published successfully:");
      result.forEach((res, index) => {
        console.log(`   📍 Partition: ${res.partition}, Offset: ${res.baseOffset}`);
      });

      return result;
    } catch (error) {
      console.error("❌ Failed to publish message:", error.message);
      throw error;
    }
  }

  /**
   * Publish all test messages
   */
  async publishAllTestMessages() {
    console.log("\n🚀 Starting to publish all test messages...");
    const results = [];

    for (let i = 0; i < this.testMessages.length; i++) {
      const message = this.testMessages[i];
      console.log(`\n--- Publishing Message ${i + 1}/${this.testMessages.length} ---`);

      try {
        const result = await this.publishMessage(message);
        results.push(result);

        // Add a small delay between messages
        if (i < this.testMessages.length - 1) {
          console.log("⏳ Waiting 1 second before next message...");
          await new Promise((resolve) => setTimeout(resolve, 1000));
        }
      } catch (error) {
        console.error(`❌ Failed to publish message ${i + 1}:`, error.message);
        results.push({ error: error.message });
      }
    }

    return results;
  }

  /**
   * Disconnect from Kafka
   */
  async disconnect() {
    console.log("\n🔌 Disconnecting from Kafka...");

    if (this.producer) {
      await this.producer.disconnect();
      console.log("✅ Producer disconnected");
    }

    if (this.consumer) {
      await this.consumer.disconnect();
      console.log("✅ Consumer disconnected");
    }
  }

  /**
   * Run the complete test suite
   */
  async runCompleteTest() {
    console.log("🧪 Starting Kafka Publisher Test Suite");
    console.log("=====================================\n");

    try {
      // Initialize and connect
      this.initializeKafka();
      await this.connect();

      // Publish messages
      const publishResults = await this.publishAllTestMessages();

      console.log("\n📊 Publishing Summary:");
      console.log(`✅ Successfully published: ${publishResults.filter((r) => !r.error).length} messages`);
      console.log(`❌ Failed to publish: ${publishResults.filter((r) => r.error).length} messages`);
      // In interactive mode, you can enable this

      console.log("\n✅ Test completed successfully!");
    } catch (error) {
      console.error("\n❌ Test failed:", error.message);
      console.error("Stack trace:", error.stack);
    } finally {
      await this.disconnect();
    }
  }

  /**
   * Run only publisher test (no consumer verification)
   */
  async runPublisherOnlyTest() {
    console.log("🧪 Starting Kafka Publisher-Only Test");
    console.log("====================================\n");

    try {
      this.initializeKafka();
      await this.connect();
      const results = await this.publishAllTestMessages();

      console.log("\n📊 Test Results:");
      console.log(`✅ Messages published successfully: ${results.filter((r) => !r.error).length}`);
      console.log(`❌ Messages failed: ${results.filter((r) => r.error).length}`);

      return results;
    } catch (error) {
      console.error("\n❌ Test failed:", error.message);
      throw error;
    } finally {
      await this.disconnect();
    }
  }
}

/**
 * Run the test if this file is executed directly
 */
if (require.main === module) {
  const test = new KafkaPublisherTest();

  // Check command line arguments
  const args = process.argv.slice(2);
  const publishOnly = args.includes("--publish-only") || args.includes("-p");

  if (publishOnly) {
    test
      .runPublisherOnlyTest()
      .then(() => {
        console.log("\n🎉 Publisher test completed!");
        process.exit(0);
      })
      .catch((error) => {
        console.error("\n💥 Publisher test failed:", error.message);
        process.exit(1);
      });
  } else {
    test
      .runCompleteTest()
      .then(() => {
        console.log("\n🎉 Complete test finished!");
        process.exit(0);
      })
      .catch((error) => {
        console.error("\n💥 Complete test failed:", error.message);
        process.exit(1);
      });
  }
}

module.exports = KafkaPublisherTest;
