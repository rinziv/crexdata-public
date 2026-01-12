const path = require("path");
const os = require("os");

const DEFAULT_SSL_ENABLED = true;

const kafkaConfigsToUse = ["KAFKA_ALEX"];
const kafkaConfigsToUseForMarine = [""];

const topicForGPSData = "KAFKA_SIMPLE";
const topicForRouteData = "KAFKA_ROUTE";

const HARDCODED_DATA = {
  spoofedGPSCoordinatesForDrone: {
    latitude: 51.54869725504633,
    longitude: 7.373895041532426,
    usecase: "Drone",
    useSpoofedGnss: true,
  },
  spoofedGPSCoordinatesForPhone: {
    latitude: 51.54879120799747,
    longitude: 7.373878661377548,
    usecase: "Phone",
  },

  drivingProfileForRouting: "driving",

  waterHeightsForVisualization: [0.1, 0.3, 0.9],
};

const CONFIG = {
  PORT: process.env.PORT || 3000,
  WS_PORT: process.env.WS_PORT || 8080,

  SSL_isEnabled: true,

  // WebSocket Heartbeat Configuration
  HEARTBEAT: {
    PING_INTERVAL: 5000, // Send ping every 30 seconds
    PONG_TIMEOUT: 3000, // Wait 10 seconds for pong response
    MAX_MISSED_PINGS: 2, // Allow 2 missed pongs before considering connection dead
  },

  // Simple Kafka Configuration (existing)
  KAFKA_SIMPLE: {
    TYPE: "gps",
    BROKERS: ["localhost:9092"],
    TOPIC: "HololensUserGPS",
    CLIENT_ID: "HoloLensDevice",
    CONNECTION_TIMEOUT: 3000,
    RETRY_RETRIES: 3,
    RETRY_INITIAL_TIME: 1000,
    RETRY_MAX_TIME: 5000,
    CONSUMER_MAX_ATTEMPTS: 5,
    CONSUMER_RETRY_DELAY_BASE: 1000, // Base delay in ms for consumer retry
    CONSUMER_RETRY_DELAY_MAX: 10000, // Max delay in ms
  },

  KAFKA_ROUTE: {
    TYPE: "gps",
    BROKERS: ["localhost:9092"],
    TOPIC: "HololensUserRouteUpdates",
    CLIENT_ID: "HoloLensDevice",
    CONNECTION_TIMEOUT: 3000,
    RETRY_RETRIES: 3,
    RETRY_INITIAL_TIME: 1000,
    RETRY_MAX_TIME: 5000,
    CONSUMER_MAX_ATTEMPTS: 5,
    CONSUMER_RETRY_DELAY_BASE: 1000, // Base delay in ms for consumer retry
    CONSUMER_RETRY_DELAY_MAX: 10000, // Max delay in ms
  },

  // Simple Kafka Configuration (existing)
  KAFKA_ALEX: {
    TYPE: "mental",
    BROKERS: ["147.27.41.144:9092"],
    TOPIC: "mental",
    CLIENT_ID: "HoloLensDevice",
    FROM_BEGINNING: false,
    CONNECTION_TIMEOUT: 3000,
    RETRY_RETRIES: 3,
    RETRY_INITIAL_TIME: 1000,
    RETRY_MAX_TIME: 5000,
    CONSUMER_MAX_ATTEMPTS: 5,
    CONSUMER_RETRY_DELAY_BASE: 1000, // Base delay in ms for consumer retry
    CONSUMER_RETRY_DELAY_MAX: 10000, // Max delay in ms
  },

  KAFKA_TUC: {
    BROKERS: ["polytechnix.softnet.tuc.gr:9092"],
    TOPIC: "Forecast24",
    CLIENT_ID: "HoloLensDevice",
    CONNECTION_TIMEOUT: 3000,
    RETRY_RETRIES: 3,
    RETRY_INITIAL_TIME: 1000,
    RETRY_MAX_TIME: 5000,
    CONSUMER_MAX_ATTEMPTS: 5,
    CONSUMER_RETRY_DELAY_BASE: 1000, // Base delay in ms for consumer retry
    CONSUMER_RETRY_DELAY_MAX: 10000, // Max delay in ms
  },

  // Authenticated Kafka Configuration (new)
  KAFKA_AUTH: {
    BROKERS: [process.env.KAFKA_AUTH_BROKER || "server.crexdata.eu:9092"],
    TOPIC: process.env.KAFKA_AUTH_TOPIC || "kpler-crex-maritime-ca-result",
    CLIENT_ID: "AR_TUC", // (group id)
    CONNECTION_TIMEOUT: 3000,
    RETRY_RETRIES: 3,
    RETRY_INITIAL_TIME: 1000,
    RETRY_MAX_TIME: 5000,
    CONSUMER_MAX_ATTEMPTS: 5,
    CONSUMER_RETRY_DELAY_BASE: 1000, // Base delay in ms for consumer retry
    CONSUMER_RETRY_DELAY_MAX: 10000, // Max delay in ms
    USERNAME: process.env.KAFKA_AUTH_USERNAME || "user",
    PASSWORD: process.env.KAFKA_AUTH_PASSWORD || "password",
    AUTH_MECHANISM: "plain",
    // SSL Configuration
    SSL_ENABLED: process.env.KAFKA_AUTH_SSL_ENABLED === "true" || DEFAULT_SSL_ENABLED, // Flag to enable/disable SSL easily
    SSL_CA_PATH: process.env.KAFKA_AUTH_SSL_CA_PATH || "./certs/ca.crt.pem",
    SSL_KEY_PATH: process.env.KAFKA_AUTH_SSL_KEY_PATH || "./certs/client.key.pem",
    SSL_CERT_PATH: process.env.KAFKA_AUTH_SSL_CERT_PATH || "./certs/client.crt.pem",
    SSL_KEY_PASSPHRASE: process.env.KAFKA_AUTH_SSL_KEY_PASSPHRASE || null,
  },

  KAFKA_CREX_NO_AUTH: {
    BROKERS: [process.env.KAFKA_CREX_NO_AUTH_BROKER || "server.crexdata.eu:9192"],
    TOPIC: process.env.KAFKA_CREX_NO_AUTH_TOPIC || "tuc_test",
    CLIENT_ID: "TUC_AR_TESTS", // (group id)
    CONNECTION_TIMEOUT: 3000,
    RETRY_RETRIES: 3,
    RETRY_INITIAL_TIME: 1000,
    RETRY_MAX_TIME: 5000,
    CONSUMER_MAX_ATTEMPTS: 5,
    CONSUMER_RETRY_DELAY_BASE: 1000, // Base delay in ms for consumer retry
    CONSUMER_RETRY_DELAY_MAX: 10000, // Max delay in ms
  },

  DEBUG: false,

  FLOOD_DATA_PATH: process.env.FLOOD_DATA_PATH || "C:/Users/CREX DATA/Downloads/AR_workaround.csv",
  OUTPUT_DATA_PATH: process.env.OUTPUT_DATA_PATH || path.join(os.homedir(), "desktop", "maps.json"),
  STATIC_POIS_PATH_FLOOD: process.env.STATIC_POIS_PATH || path.join(__dirname, "..", "data", "static_pois_flood.json"),
  STATIC_POIS_PATH_FIRE: process.env.STATIC_POIS_PATH || path.join(__dirname, "..", "data", "static_pois_fire.json"),
};

module.exports = {
  CONFIG,
  kafkaConfigsToUse,
  topicForGPSData,
  topicForRouteData,
  kafkaConfigsToUseForMarine,
  HARDCODED_DATA,
};
