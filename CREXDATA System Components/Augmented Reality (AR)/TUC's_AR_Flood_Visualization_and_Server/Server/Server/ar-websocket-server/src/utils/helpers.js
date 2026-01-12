const logger = require("./logger");

// Generate unique IDs for connections or other purposes
function generateUniqueId() {
  return `${Date.now()}-${Math.random().toString(36).substr(2, 9)}`;
}

// Format uptime in seconds into a human-readable string
function formatUptime(seconds) {
  const days = Math.floor(seconds / 86400);
  seconds %= 86400;
  const hours = Math.floor(seconds / 3600);
  seconds %= 3600;
  const minutes = Math.floor(seconds / 60);
  seconds %= 60;

  let result = "";
  if (days > 0) result += `${days}d `;
  if (hours > 0) result += `${hours}h `;
  if (minutes > 0) result += `${minutes}m `;
  result += `${Math.floor(seconds)}s`; // Ensure seconds is an integer

  return result.trim() || "0s"; // Handle case where uptime is 0
}

// Creates a standardized key for storing clients in maps
function getCompositeKey(type, id) {
  if (!type || !id) {
    logger.warn("Attempted to create composite key with missing type or id", { type, id });
    return null; // Or throw an error
  }
  return `${type}:${id}`;
}

module.exports = {
  generateUniqueId,
  formatUptime,
  getCompositeKey,
};
