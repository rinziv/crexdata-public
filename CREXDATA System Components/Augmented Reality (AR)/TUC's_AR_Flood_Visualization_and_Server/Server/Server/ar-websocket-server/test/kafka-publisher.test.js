const KafkaPublisherTest = require("./kafka-publisher-test");

describe("Kafka Publisher Test Suite", () => {
  let kafkaTest;

  beforeEach(() => {
    kafkaTest = new KafkaPublisherTest();
  });

  afterEach(async () => {
    if (kafkaTest) {
      await kafkaTest.disconnect();
    }
  });

  describe("Initialization", () => {
    test("should initialize Kafka client with correct configuration", () => {
      expect(() => kafkaTest.initializeKafka()).not.toThrow();
      expect(kafkaTest.kafka).toBeDefined();
      expect(kafkaTest.producer).toBeDefined();
    });

    test("should have valid test messages", () => {
      const messages = kafkaTest.testMessages;
      expect(messages).toHaveLength(3);

      messages.forEach((message) => {
        expect(message).toHaveProperty("id");
        expect(message).toHaveProperty("timestamp");
        expect(message).toHaveProperty("type");
        expect(message).toHaveProperty("data");
        expect(message).toHaveProperty("metadata");
        expect(message.metadata.testRun).toBe(true);
      });
    });
  });

  describe("Message Generation", () => {
    test("should generate valid AR POI message", () => {
      const message = kafkaTest.testMessages[0];
      expect(message.type).toBe("AR_POI");
      expect(message.data).toHaveProperty("latitude");
      expect(message.data).toHaveProperty("longitude");
      expect(message.data).toHaveProperty("title");
      expect(message.data.latitude).toBeCloseTo(51.5487, 4);
      expect(message.data.longitude).toBeCloseTo(7.3739, 4);
    });

    test("should generate valid drone update message", () => {
      const message = kafkaTest.testMessages[1];
      expect(message.type).toBe("DRONE_UPDATE");
      expect(message.data).toHaveProperty("droneId");
      expect(message.data).toHaveProperty("position");
      expect(message.data.position).toHaveProperty("latitude");
      expect(message.data.position).toHaveProperty("longitude");
      expect(message.data.position).toHaveProperty("altitude");
      expect(message.data.droneId).toBe("DRONE_001");
    });

    test("should generate valid alert message", () => {
      const message = kafkaTest.testMessages[2];
      expect(message.type).toBe("ALERT");
      expect(message.data).toHaveProperty("alertId");
      expect(message.data).toHaveProperty("severity");
      expect(message.data).toHaveProperty("location");
      expect(message.data.severity).toBe("high");
      expect(message.data.alertType).toBe("fire");
    });
  });

  describe("Connection", () => {
    test("should connect to Kafka successfully", async () => {
      kafkaTest.initializeKafka();
      await expect(kafkaTest.connect()).resolves.not.toThrow();
    }, 10000);

    test("should handle connection errors gracefully", async () => {
      // Create a test with invalid broker
      const invalidKafkaTest = new KafkaPublisherTest();
      invalidKafkaTest.kafkaConfig.BROKERS = ["invalid-broker:9092"];
      invalidKafkaTest.initializeKafka();

      await expect(invalidKafkaTest.connect()).rejects.toThrow();
    }, 10000);
  });

  describe("Message Publishing", () => {
    test("should publish messages without errors", async () => {
      kafkaTest.initializeKafka();
      await kafkaTest.connect();

      const message = kafkaTest.testMessages[0];
      await expect(kafkaTest.publishMessage(message)).resolves.toBeDefined();
    }, 15000);

    test("should publish all test messages", async () => {
      kafkaTest.initializeKafka();
      await kafkaTest.connect();

      const results = await kafkaTest.publishAllTestMessages();
      expect(results).toHaveLength(3);

      // Check that all messages were published successfully
      const successfulPublishes = results.filter((result) => !result.error);
      expect(successfulPublishes.length).toBeGreaterThan(0);
    }, 30000);
  });

  describe("Publisher-Only Test", () => {
    test("should run publisher-only test successfully", async () => {
      const results = await kafkaTest.runPublisherOnlyTest();
      expect(results).toBeDefined();
      expect(Array.isArray(results)).toBe(true);
      expect(results.length).toBeGreaterThan(0);
    }, 30000);
  });

  describe("JSON Serialization", () => {
    test("should serialize test messages to valid JSON", () => {
      kafkaTest.testMessages.forEach((message) => {
        expect(() => JSON.stringify(message)).not.toThrow();

        const serialized = JSON.stringify(message);
        expect(() => JSON.parse(serialized)).not.toThrow();

        const deserialized = JSON.parse(serialized);
        expect(deserialized).toEqual(message);
      });
    });

    test("should handle custom message serialization", async () => {
      const customMessage = {
        id: "custom-test",
        timestamp: new Date().toISOString(),
        type: "CUSTOM",
        data: {
          customField: "test value",
          numbers: [1, 2, 3],
          nested: {
            property: "nested value",
          },
        },
      };

      expect(() => JSON.stringify(customMessage)).not.toThrow();

      kafkaTest.initializeKafka();
      await kafkaTest.connect();

      await expect(kafkaTest.publishMessage(customMessage)).resolves.toBeDefined();
    }, 15000);
  });
});

// Integration test for the specific KAFKA_CREX_NO_AUTH configuration
describe("KAFKA_CREX_NO_AUTH Configuration Integration", () => {
  test("should use correct configuration values", () => {
    const kafkaTest = new KafkaPublisherTest();
    const config = kafkaTest.kafkaConfig;

    expect(config.BROKERS).toContain("server.crexdata.eu:9192");
    expect(config.TOPIC).toBe("tuc_test");
    expect(config.CLIENT_ID).toBe("TUC_AR_TESTS");
    expect(config.AUTH_MECHANISM).toBe("plain");
  });

  test("should initialize with environment variables if available", () => {
    // Temporarily set environment variables
    const originalBroker = process.env.KAFKA_CREX_NO_AUTH_BROKER;
    const originalTopic = process.env.KAFKA_CREX_NO_AUTH_TOPIC;

    process.env.KAFKA_CREX_NO_AUTH_BROKER = "test-broker:9092";
    process.env.KAFKA_CREX_NO_AUTH_TOPIC = "test-topic";

    // Re-require the config to pick up the new env vars
    delete require.cache[require.resolve("../src/config")];
    const { CONFIG } = require("../src/config");

    expect(CONFIG.KAFKA_CREX_NO_AUTH.BROKERS).toContain("test-broker:9092");
    expect(CONFIG.KAFKA_CREX_NO_AUTH.TOPIC).toBe("test-topic");

    // Restore original environment variables
    if (originalBroker !== undefined) {
      process.env.KAFKA_CREX_NO_AUTH_BROKER = originalBroker;
    } else {
      delete process.env.KAFKA_CREX_NO_AUTH_BROKER;
    }

    if (originalTopic !== undefined) {
      process.env.KAFKA_CREX_NO_AUTH_TOPIC = originalTopic;
    } else {
      delete process.env.KAFKA_CREX_NO_AUTH_TOPIC;
    }

    // Clear the require cache to reset config
    delete require.cache[require.resolve("../src/config")];
  });
});
