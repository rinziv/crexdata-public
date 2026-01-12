const { CONFIG } = require("../config");

// ANSI escape codes for colors
const colors = {
  reset: "\x1b[0m",
  red: "\x1b[31m",
  yellow: "\x1b[33m",
  blue: "\x1b[34m",
  green: "\x1b[32m",
};

const logger = {
  info: (message, data = {}) => {
    const prefix = `${colors.green}[INFO]${colors.reset}`;
    console.log(`${prefix} ${new Date().toISOString()} - ${message}`, Object.keys(data).length ? JSON.stringify(data) : "");
  },
  error: (message, error) => {
    const prefix = `${colors.red}[ERROR]${colors.reset}`;
    // Ensure the error object itself is logged properly, not just its stringified version if it's an Error instance
    if (error instanceof Error) {
      console.error(`${prefix} ${new Date().toISOString()} - ${message}`, error);
    } else {
      console.error(`${prefix} ${new Date().toISOString()} - ${message}`, Object.keys(error || {}).length ? JSON.stringify(error) : "");
    }
  },
  warn: (message, data = {}) => {
    const prefix = `${colors.yellow}[WARN]${colors.reset}`;
    console.warn(`${prefix} ${new Date().toISOString()} - ${message}`, Object.keys(data).length ? JSON.stringify(data) : "");
  },
  debug: (message, data = {}) => {
    if (CONFIG.DEBUG) {
      const prefix = `${colors.blue}[DEBUG]${colors.reset}`;
      console.log(`${prefix} ${new Date().toISOString()} - ${message}`, Object.keys(data).length ? JSON.stringify(data) : "");
    }
  },
  kafka: ({ namespace, level, label, log }) => {
    if (CONFIG.DEBUG) {
      const { message, ...rest } = log;
      logger.debug(`Kafka ${label} [${namespace}]: ${message}`, rest);
    }
  },
};

module.exports = logger;
