const { Kafka, logLevel } = require("kafkajs");
const { CONFIG } = require("../src/config");
const readline = require("readline");

/**
 * Interactive Kafka Producer-Consumer
 * This script allows you to both publish messages and consume messages live in an interactive mode
 */

class InteractiveKafkaManager {
  constructor() {
    this.kafkaConfig = CONFIG.KAFKA_CREX_NO_AUTH;
    this.kafka = null;
    this.producer = null;
    this.consumer = null;
    this.isConsuming = false;
    this.messageCount = 0;
    this.publishedCount = 0;
    this.rl = null;
    this.startTime = null;
    this.mode = "menu"; // 'menu', 'consuming', 'publishing'
  }

  /**
   * Initialize Kafka client
   */
  initializeKafka() {
    console.log("🔧 Initializing Kafka Interactive Manager...");
    console.log(`📡 Brokers: ${this.kafkaConfig.BROKERS.join(", ")}`);
    console.log(`📝 Topic: ${this.kafkaConfig.TOPIC}`);
    console.log(`🆔 Client ID: ${this.kafkaConfig.CLIENT_ID}_interactive`);

    const kafkaJsConfig = {
      clientId: `${this.kafkaConfig.CLIENT_ID}_interactive`,
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
        username: this.kafkaConfig.USERNAME || "interactive-user",
        password: this.kafkaConfig.PASSWORD || "interactive-password",
      };
    }

    this.kafka = new Kafka(kafkaJsConfig);
    this.producer = this.kafka.producer();
    console.log("✅ Kafka client initialized successfully");
  }

  /**
   * Create consumer
   */
  async createConsumer(fromBeginning = false) {
    const groupId = `interactive-manager-${Date.now()}`;

    this.consumer = this.kafka.consumer({
      groupId: groupId,
      sessionTimeout: 30000,
      heartbeatInterval: 3000,
      maxWaitTimeInMs: 5000,
    });

    await this.consumer.connect();
    await this.consumer.subscribe({
      topic: this.kafkaConfig.TOPIC,
      fromBeginning: fromBeginning,
    });

    console.log(`✅ Consumer created (group: ${groupId}, fromBeginning: ${fromBeginning})`);
  }

  /**
   * Connect producer
   */
  async connectProducer() {
    await this.producer.connect();
    console.log("✅ Producer connected");
  }

  /**
   * Setup readline interface
   */
  setupReadlineInterface() {
    this.rl = readline.createInterface({
      input: process.stdin,
      output: process.stdout,
    });

    this.rl.on("line", (input) => {
      this.handleInput(input.trim());
    });

    this.rl.on("close", () => {
      this.stop();
    });
  }

  /**
   * Show main menu
   */
  showMainMenu() {
    console.log("\n🎮 Interactive Kafka Manager - Main Menu");
    console.log("========================================");
    console.log("1  start-consumer    - Start live message consumer");
    console.log("2  stop-consumer     - Stop message consumer");
    console.log("3  publish           - Publish a custom message");
    console.log("4  publish-test      - Publish predefined test messages");
    console.log("5  stats             - Show statistics");
    console.log("6  clear             - Clear screen");
    console.log("7  help              - Show this menu");
    console.log("8  quit              - Exit application");
    console.log("\nEnter a command:");
  }

  /**
   * Show consumer commands
   */
  showConsumerCommands() {
    console.log("\n🎮 Consumer Mode Commands:");
    console.log("  ⏸️  pause     - Pause consumer");
    console.log("  ▶️  resume    - Resume consumer");
    console.log("  📊 stats     - Show statistics");
    console.log("  🔄 clear     - Clear screen");
    console.log("  🔙 menu      - Return to main menu");
    console.log("  ❌ quit      - Exit application");
  }

  /**
   * Handle user input based on current mode
   */
  async handleInput(input) {
    const command = input.toLowerCase();

    if (this.mode === "consuming") {
      await this.handleConsumerCommand(command);
    } else {
      await this.handleMainCommand(command);
    }
  }

  /**
   * Handle main menu commands
   */
  async handleMainCommand(command) {
    switch (command) {
      case "1":
      case "start-consumer":
      case "consumer":
        await this.startConsumer();
        break;
      case "2":
      case "stop-consumer":
        await this.stopConsumer();
        break;
      case "3":
      case "publish":
        await this.publishCustomMessage();
        break;
      case "4":
      case "publish-test":
        await this.publishTestMessages();
        break;
      case "5":
      case "stats":
        this.showStats();
        break;
      case "6":
      case "clear":
        console.clear();
        this.showMainMenu();
        break;
      case "7":
      case "help":
        this.showMainMenu();
        break;
      case "8":
      case "quit":
      case "exit":
        await this.stop();
        break;
      default:
        if (command) {
          console.log(`❓ Unknown command: "${command}". Type "help" for available commands.`);
        }
        break;
    }
  }

  /**
   * Handle consumer mode commands
   */
  async handleConsumerCommand(command) {
    switch (command) {
      case "pause":
        await this.pauseConsumer();
        break;
      case "resume":
        await this.resumeConsumer();
        break;
      case "stats":
        this.showStats();
        break;
      case "clear":
        console.clear();
        console.log("📺 Screen cleared. Consumer still running...");
        this.showConsumerCommands();
        break;
      case "menu":
        await this.stopConsumer();
        this.mode = "menu";
        this.showMainMenu();
        break;
      case "quit":
      case "exit":
        await this.stop();
        break;
      default:
        if (command) {
          console.log(`❓ Unknown command: "${command}". Type "menu" to return to main menu.`);
        }
        break;
    }
  }

  /**
   * Start consumer
   */
  async startConsumer() {
    try {
      if (!this.consumer) {
        console.log("🔧 Creating consumer...");
        await this.createConsumer(false); // Change to true to read from beginning
      }

      if (this.isConsuming) {
        console.log("⚠️  Consumer is already running.");
        return;
      }

      this.isConsuming = true;
      this.startTime = Date.now();
      this.messageCount = 0;
      this.mode = "consuming";

      console.log("🚀 Starting live consumer...");
      console.log("📡 Listening for messages. Use commands below to control:");
      this.showConsumerCommands();

      await this.consumer.run({
        eachMessage: async ({ topic, partition, message }) => {
          if (!this.isConsuming) return;

          this.messageCount++;
          this.displayMessage(topic, partition, message, this.messageCount);
        },
      });
    } catch (error) {
      console.error("❌ Failed to start consumer:", error.message);
      this.isConsuming = false;
      this.mode = "menu";
    }
  }

  /**
   * Stop consumer
   */
  async stopConsumer() {
    if (!this.isConsuming) {
      console.log("⚠️  Consumer is not running.");
      return;
    }

    this.isConsuming = false;
    console.log("🛑 Stopping consumer...");

    if (this.consumer) {
      try {
        await this.consumer.stop();
        console.log("✅ Consumer stopped");
      } catch (error) {
        console.error("❌ Error stopping consumer:", error.message);
      }
    }

    this.mode = "menu";
  }

  /**
   * Pause consumer
   */
  async pauseConsumer() {
    if (this.consumer && this.isConsuming) {
      try {
        await this.consumer.pause([{ topic: this.kafkaConfig.TOPIC }]);
        console.log("⏸️  Consumer paused.");
      } catch (error) {
        console.error("❌ Failed to pause consumer:", error.message);
      }
    }
  }

  /**
   * Resume consumer
   */
  async resumeConsumer() {
    if (this.consumer) {
      try {
        this.consumer.resume([{ topic: this.kafkaConfig.TOPIC }]);
        console.log("▶️  Consumer resumed.");
      } catch (error) {
        console.error("❌ Failed to resume consumer:", error.message);
      }
    }
  }

  /**
   * Publish custom message
   */
  async publishCustomMessage() {
    console.log("\n📝 Publishing Custom Message");
    console.log("============================");

    try {
      if (!this.producer) {
        console.log("🔧 Connecting producer...");
        await this.connectProducer();
      }

      // Get message details from user
      const messageType = await this.askQuestion("Enter message type (e.g., AR_POI, DRONE_UPDATE, ALERT): ");
      const messageId = (await this.askQuestion("Enter message ID (or press Enter for auto-generated): ")) || `custom-${Date.now()}`;
      const messageData = await this.askQuestion("Enter JSON data (or press Enter for sample data): ");

      let data;
      if (messageData.trim()) {
        try {
          data = JSON.parse(messageData);
        } catch (parseError) {
          console.log("⚠️  Invalid JSON, using as string value.");
          data = { content: messageData };
        }
      } else {
        data = {
          latitude: 51.5487,
          longitude: 7.3739,
          title: "Custom Test Message",
          description: "Message from Kafka Producer",
        };
      }

      const message = {
        id: messageId,
        timestamp: new Date().toISOString(),
        type: messageType || "CUSTOM",
        data: data,
        metadata: {
          source: "kafka-producer_test_tuc",
          version: "1.0.0",
        },
      };

      await this.publishMessage(message);
      this.publishedCount++;

      console.log("✅ Message published successfully!");
    } catch (error) {
      console.error("❌ Failed to publish custom message:", error.message);
    }
  }

  /**
   * Publish predefined test messages
   */
  async publishTestMessages() {
    console.log("\n🧪 Publishing Test Messages");
    console.log("===========================");

    try {
      if (!this.producer) {
        console.log("🔧 Connecting producer...");
        await this.connectProducer();
      }

      const testMessages = this.generateTestMessages();

      for (let i = 0; i < testMessages.length; i++) {
        const message = testMessages[i];
        console.log(`\n📤 Publishing test message ${i + 1}/${testMessages.length}: ${message.type}`);

        await this.publishMessage(message);
        this.publishedCount++;

        if (i < testMessages.length - 1) {
          console.log("⏳ Waiting 1 second...");
          await new Promise((resolve) => setTimeout(resolve, 1000));
        }
      }

      console.log("✅ All test messages published successfully!");
    } catch (error) {
      console.error("❌ Failed to publish test messages:", error.message);
    }
  }

  /**
   * Publish a single message
   */
  async publishMessage(message, key = null) {
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

    const result = await this.producer.send(messagePayload);
    console.log(`   📍 Published to partition: ${result[0].partition}, offset: ${result[0].baseOffset}`);

    return result;
  }

  /**
   * Generate test messages
   */
  generateTestMessages() {
    const baseTime = Date.now();

    return [
      {
        id: `interactive-test-1-${baseTime}`,
        timestamp: new Date(baseTime).toISOString(),
        type: "AR_POI",
        data: {
          latitude: 51.54869725504633,
          longitude: 7.373895041532426,
          altitude: 100,
          title: "Test POI",
          description: "POI created from interactive manager",
          category: "test",
          priority: "medium",
        },
        metadata: {
          source: "kafka-producer_test_tuc",
          version: "1.0.0",
        },
      },
      {
        id: `interactive-test-2-${baseTime}`,
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
          mission: "test",
        },
        metadata: {
          source: "kafka-produrecer_test_tuc",
          version: "1.0.0",
          interactive: true,
        },
      },
    ];
  }

  /**
   * Display received message
   */
  displayMessage(topic, partition, message, messageNumber) {
    const timestamp = new Date().toLocaleTimeString();

    console.log(`\n📨 Message #${messageNumber} [${timestamp}]`);
    console.log(`   📍 Topic: ${topic} | Partition: ${partition} | Offset: ${message.offset}`);
    console.log(`   🔑 Key: ${message.key?.toString() || "null"}`);

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
    }

    console.log("─".repeat(60));
  }

  /**
   * Show statistics
   */
  showStats() {
    const runtime = this.startTime ? Math.floor((Date.now() - this.startTime) / 1000) : 0;
    const rate = runtime > 0 ? (this.messageCount / runtime).toFixed(2) : "0.00";

    console.log("\n📊 Interactive Manager Statistics:");
    console.log(`   📨 Messages consumed: ${this.messageCount}`);
    console.log(`   📤 Messages published: ${this.publishedCount}`);
    console.log(`   ⏱️  Runtime: ${runtime} seconds`);
    console.log(`   📈 Consumption rate: ${rate} messages/second`);
    console.log(`   📍 Topic: ${this.kafkaConfig.TOPIC}`);
    console.log(`   🎯 Consumer status: ${this.isConsuming ? "Running" : "Stopped"}`);
    console.log(`   🎛️  Mode: ${this.mode}`);
  }

  /**
   * Ask question and get user input
   */
  askQuestion(question) {
    return new Promise((resolve) => {
      this.rl.question(`${question} `, (answer) => {
        resolve(answer);
      });
    });
  }

  /**
   * Stop everything and cleanup
   */
  async stop() {
    console.log("\n🛑 Shutting down Interactive Kafka Manager...");

    this.isConsuming = false;

    if (this.consumer) {
      try {
        await this.consumer.disconnect();
        console.log("✅ Consumer disconnected");
      } catch (error) {
        console.error("❌ Error disconnecting consumer:", error.message);
      }
    }

    if (this.producer) {
      try {
        await this.producer.disconnect();
        console.log("✅ Producer disconnected");
      } catch (error) {
        console.error("❌ Error disconnecting producer:", error.message);
      }
    }

    if (this.rl) {
      this.rl.close();
    }

    this.showStats();
    console.log("\n👋 Goodbye!");
    process.exit(0);
  }

  /**
   * Run the interactive manager
   */
  async run() {
    console.log("🎯 Interactive Kafka Producer-Consumer Manager");
    console.log("==============================================\n");

    try {
      // Setup signal handlers
      process.on("SIGINT", async () => {
        console.log("\n🛑 Received SIGINT...");
        await this.stop();
      });

      process.on("SIGTERM", async () => {
        console.log("\n🛑 Received SIGTERM...");
        await this.stop();
      });

      // Initialize
      this.initializeKafka();
      this.setupReadlineInterface();

      console.log("🚀 Interactive manager ready!");
      this.showMainMenu();
    } catch (error) {
      console.error("\n❌ Manager failed:", error.message);
      await this.stop();
    }
  }
}

// Run if this file is executed directly
if (require.main === module) {
  const manager = new InteractiveKafkaManager();
  manager.run().catch(console.error);
}

module.exports = InteractiveKafkaManager;
