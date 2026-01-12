const { Kafka, logLevel } = require("kafkajs");
const { CONFIG } = require("../src/config");
const readline = require("readline");

/**
 * Interactive Kafka Consumer
 * This script creates a live consumer that continuously reads messages from the Kafka topic
 */

class InteractiveKafkaConsumer {
  constructor() {
    this.kafkaConfig = CONFIG.KAFKA_CREX_NO_AUTH;
    this.kafka = null;
    this.consumer = null;
    this.isRunning = false;
    this.messageCount = 0;
    this.rl = null;
    this.startTime = null;
  }

  /**
   * Initialize Kafka client
   */
  initializeKafka() {
    console.log("🔧 Initializing Kafka Interactive Consumer...");
    console.log(`📡 Brokers: ${this.kafkaConfig.BROKERS.join(", ")}`);
    console.log(`📝 Topic: ${this.kafkaConfig.TOPIC}`);
    console.log(`🆔 Client ID: ${this.kafkaConfig.CLIENT_ID}_interactive_consumer`);

    const kafkaJsConfig = {
      clientId: `${this.kafkaConfig.CLIENT_ID}_interactive_consumer`,
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
        username: this.kafkaConfig.USERNAME || "consumer-user",
        password: this.kafkaConfig.PASSWORD || "consumer-password",
      };
    }

    this.kafka = new Kafka(kafkaJsConfig);
    console.log("✅ Kafka client initialized successfully");
  }

  /**
   * Create and configure consumer
   */
  async createConsumer(fromBeginning = false) {
    const groupId = `consumer-${Date.now()}`;
    console.log(`👂 Creating consumer with group ID: ${groupId}`);

    this.consumer = this.kafka.consumer({
      groupId: groupId,
      sessionTimeout: 30000,
      heartbeatInterval: 3000,
      maxWaitTimeInMs: 5000,
      retry: {
        retries: this.kafkaConfig.CONSUMER_MAX_ATTEMPTS || 5,
        initialRetryTime: this.kafkaConfig.CONSUMER_RETRY_DELAY_BASE || 1000,
        maxRetryTime: this.kafkaConfig.CONSUMER_RETRY_DELAY_MAX || 10000,
      },
    });

    console.log("🔌 Connecting consumer...");
    await this.consumer.connect();

    console.log(`📋 Subscribing to topic: ${this.kafkaConfig.TOPIC} (fromBeginning: ${fromBeginning})`);
    await this.consumer.subscribe({
      topic: this.kafkaConfig.TOPIC,
      fromBeginning: fromBeginning,
    });

    console.log("✅ Consumer created and subscribed successfully");
  }

  /**
   * Setup readline interface for interactive commands
   */
  setupReadlineInterface() {
    this.rl = readline.createInterface({
      input: process.stdin,
      output: process.stdout,
    });

    console.log("\n🎮 Interactive Commands:");
    console.log("  📊 stats    - Show consumption statistics");
    console.log("  ⏸️  pause    - Pause message consumption");
    console.log("  ▶️  resume   - Resume message consumption");
    console.log("  🔄 clear    - Clear screen");
    console.log("  ❌ quit     - Stop consumer and exit");
    console.log("  ❓ help     - Show this help message");
    console.log("\nType a command and press Enter...\n");

    this.rl.on("line", (input) => {
      this.handleCommand(input.trim().toLowerCase());
    });

    this.rl.on("close", () => {
      this.stop();
    });
  }

  /**
   * Handle interactive commands
   */
  async handleCommand(command) {
    switch (command) {
      case "stats":
        this.showStats();
        break;
      case "pause":
        await this.pauseConsumer();
        break;
      case "resume":
        await this.resumeConsumer();
        break;
      case "clear":
        console.clear();
        console.log("📺 Screen cleared. Consumer still running...\n");
        break;
      case "quit":
      case "exit":
      case "stop":
        console.log("🛑 Stopping consumer...");
        await this.stop();
        break;
      case "help":
        this.showHelp();
        break;
      default:
        if (command) {
          console.log(`❓ Unknown command: "${command}". Type "help" for available commands.`);
        }
        break;
    }
  }

  /**
   * Show consumption statistics
   */
  showStats() {
    const runtime = this.startTime ? Math.floor((Date.now() - this.startTime) / 1000) : 0;
    const rate = runtime > 0 ? (this.messageCount / runtime).toFixed(2) : "0.00";

    console.log("\n📊 Consumer Statistics:");
    console.log(`   📨 Messages received: ${this.messageCount}`);
    console.log(`   ⏱️  Runtime: ${runtime} seconds`);
    console.log(`   📈 Average rate: ${rate} messages/second`);
    console.log(`   📍 Topic: ${this.kafkaConfig.TOPIC}`);
    console.log(`   🎯 Status: ${this.isRunning ? "Running" : "Stopped"}\n`);
  }

  /**
   * Show help message
   */
  showHelp() {
    console.log("\n🎮 Available Commands:");
    console.log("  📊 stats    - Show consumption statistics");
    console.log("  ⏸️  pause    - Pause message consumption");
    console.log("  ▶️  resume   - Resume message consumption");
    console.log("  🔄 clear    - Clear screen");
    console.log("  ❌ quit     - Stop consumer and exit");
    console.log("  ❓ help     - Show this help message\n");
  }

  /**
   * Pause consumer
   */
  async pauseConsumer() {
    if (this.consumer && this.isRunning) {
      try {
        await this.consumer.pause([{ topic: this.kafkaConfig.TOPIC }]);
        console.log('⏸️  Consumer paused. Type "resume" to continue.\n');
      } catch (error) {
        console.error("❌ Failed to pause consumer:", error.message);
      }
    } else {
      console.log("⚠️  Consumer is not running or not initialized.\n");
    }
  }

  /**
   * Resume consumer
   */
  async resumeConsumer() {
    if (this.consumer) {
      try {
        this.consumer.resume([{ topic: this.kafkaConfig.TOPIC }]);
        console.log("▶️  Consumer resumed. Listening for messages...\n");
      } catch (error) {
        console.error("❌ Failed to resume consumer:", error.message);
      }
    } else {
      console.log("⚠️  Consumer is not initialized.\n");
    }
  }

  /**
   * Format and display received message
   */
  displayMessage(topic, partition, message, messageNumber) {
    const timestamp = new Date().toLocaleTimeString();

    console.log(`\n📨 Message #${messageNumber} [${timestamp}]`);
    console.log(`   📍 Topic: ${topic} | Partition: ${partition} | Offset: ${message.offset}`);
    console.log(`   🔑 Key: ${message.key?.toString() || "null"}`);
    console.log(`   🕐 Timestamp: ${message.timestamp ? new Date(parseInt(message.timestamp)).toISOString() : "N/A"}`);

    try {
      const parsedValue = JSON.parse(message.value.toString());
      console.log(`   📄 Value (JSON):`);
      console.log(
        JSON.stringify(parsedValue, null, 4)
          .split("\n")
          .map((line) => `      ${line}`)
          .join("\n")
      );
    } catch (parseError) {
      console.log(`   📄 Value (Raw): ${message.value.toString()}`);
      console.log(`   ⚠️  JSON Parse Error: ${parseError.message}`);
    }

    console.log("─".repeat(80));
  }

  /**
   * Start consuming messages
   */
  async startConsuming() {
    if (!this.consumer) {
      throw new Error("Consumer not initialized. Call createConsumer() first.");
    }

    this.isRunning = true;
    this.startTime = Date.now();
    this.messageCount = 0;

    console.log("🚀 Starting message consumption...");
    console.log("📡 Listening for live messages. Use interactive commands below.\n");

    try {
      await this.consumer.run({
        eachMessage: async ({ topic, partition, message }) => {
          if (!this.isRunning) return;

          this.messageCount++;
          this.displayMessage(topic, partition, message, this.messageCount);
        },
      });
    } catch (error) {
      console.error("❌ Consumer error:", error.message);
      this.isRunning = false;
      throw error;
    }
  }

  /**
   * Stop consumer and cleanup
   */
  async stop() {
    this.isRunning = false;

    console.log("\n🛑 Stopping consumer...");

    if (this.consumer) {
      try {
        await this.consumer.disconnect();
        console.log("✅ Consumer disconnected");
      } catch (error) {
        console.error("❌ Error disconnecting consumer:", error.message);
      }
    }

    if (this.rl) {
      this.rl.close();
    }

    this.showStats();
    console.log("👋 Goodbye!");
    process.exit(0);
  }

  /**
   * Run the interactive consumer
   */
  async run(fromBeginning = false) {
    console.log("🎯 Interactive Kafka Consumer");
    console.log("============================\n");

    try {
      // Setup signal handlers for graceful shutdown
      process.on("SIGINT", async () => {
        console.log("\n\n🛑 Received SIGINT, shutting down gracefully...");
        await this.stop();
      });

      process.on("SIGTERM", async () => {
        console.log("\n\n🛑 Received SIGTERM, shutting down gracefully...");
        await this.stop();
      });

      // Initialize and start
      this.initializeKafka();
      await this.createConsumer(fromBeginning);
      this.setupReadlineInterface();
      await this.startConsuming();
    } catch (error) {
      console.error("\n❌ Consumer failed:", error.message);
      console.error("Stack trace:", error.stack);
      await this.stop();
    }
  }
}

/**
 * CLI function to get user preferences
 */
function askQuestion(question) {
  const rl = readline.createInterface({
    input: process.stdin,
    output: process.stdout,
  });

  return new Promise((resolve) => {
    rl.question(question, (answer) => {
      rl.close();
      resolve(answer);
    });
  });
}

/**
 * Main function to run interactive consumer
 */
async function main() {
  console.log("🎯 Kafka Interactive Consumer Setup");
  console.log("===================================\n");

  try {
    // Ask user preferences
    const fromBeginningAnswer = await askQuestion("📋 Do you want to read messages from the beginning? (y/n): ");
    const fromBeginning = fromBeginningAnswer.toLowerCase().startsWith("y");

    console.log(`\n🔧 Starting consumer (fromBeginning: ${fromBeginning})...\n`);

    // Create and run consumer
    const consumer = new InteractiveKafkaConsumer();
    await consumer.run(fromBeginning);
  } catch (error) {
    console.error("\n💥 Failed to start interactive consumer:", error.message);
    process.exit(1);
  }
}

// Run if this file is executed directly
if (require.main === module) {
  main();
}

module.exports = InteractiveKafkaConsumer;
