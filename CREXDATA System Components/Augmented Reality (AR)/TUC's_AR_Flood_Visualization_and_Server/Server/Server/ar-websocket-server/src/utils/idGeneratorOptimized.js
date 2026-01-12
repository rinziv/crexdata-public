const logger = require("./logger");

const MAX_CLIENTS = 50;
const availableIds = Array.from({ length: MAX_CLIENTS }, (_, i) => i + 1); // [1, 2, ..., 100]
const usedIds = new Set();

/**
 * Generates a unique integer identifier for each Hololens client.
 * @returns {number | null} A unique numeric ID or null if all IDs are currently in use.
 */
function generateUniqueId() {
  if (usedIds.size >= MAX_CLIENTS) {
    logger.warn("Cannot generate new ID: ID pool is full.");
    return null;
  }

  const id = availableIds.shift(); // Take first available ID (O(1))
  usedIds.add(id);
  logger.info(`Generated new unique ID: ${id}`);
  //getIdPool();
  return id;
}

/**
 * Removes an identifier from the pool.
 * This is used when a client disconnects.
 * @param {number} id - The numeric ID to remove.
 * @returns {boolean} True if the ID was removed, false if it wasn't in the pool.
 */
function removeId(id) {
  id = Number(id); // Ensure number
  if (!Number.isInteger(id) || id < 1 || id > MAX_CLIENTS) {
    logger.warn(`Invalid ID ${id} provided for removal. Must be an integer between 1 and ${MAX_CLIENTS}.`);
    return false;
  }
  if (usedIds.has(id)) {
    usedIds.delete(id);
    availableIds.push(id); // Return to pool (O(1))
    availableIds.sort((a, b) => a - b); // Sort list for better id assignment (O(n log n))
    logger.info(`ID ${id} removed from pool.`);
    //getIdPool();
    return true;
  }
  logger.warn(`ID ${id} not found in pool for removal.`);
  return false;
}

/**
 * Adds an identifier to the pool.
 * @param {number} id - The numeric ID to add.
 * @returns {boolean} True if the ID was added, false if it already exists or is invalid.
 */
function addID(id) {
  id = Number(id); // Ensure number
  if (!Number.isInteger(id) || id < 1 || id > MAX_CLIENTS) {
    logger.warn(`Invalid ID ${id} provided for addition. Must be an integer between 1 and ${MAX_CLIENTS}.`);
    return false;
  }
  if (usedIds.has(id)) {
    logger.warn(`ID ${id} already exists in the pool.`);
    //getIdPool();
    return false;
  }
  usedIds.add(id);
  const index = availableIds.indexOf(id);
  if (index !== -1) {
    availableIds.splice(index, 1); // Remove from available pool (O(n))
  }
  logger.info(`ID ${id} added to pool.`);
  //getIdPool();
  return true;
}

/**
 * Returns the current state of the ID pool and logs it.
 * @returns {number[]} Array of currently used IDs.
 */
function getIdPool() {
  const pool = Array.from(usedIds);
  logger.info(`Current ID pool: ${pool.join(", ") || "empty"}`);
  getAvailableIds();
  return pool;
}

function getAvailableIds() {
  const pool = Array.from(availableIds);
  logger.info(`Current available IDs: ${pool.join(", ") || "empty"}`);
}

/**
 * Resets the ID pool to its initial state.
 * CLEARS ALL USED IDs.
 */
function resetIdPool() {
  usedIds.clear();
  availableIds.length = 0;
  for (let i = 1; i <= MAX_CLIENTS; i++) {
    availableIds.push(i);
  }
  logger.info("ID pool reset to initial state.");
}

module.exports = {
  generateUniqueId,
  removeId,
  addID,
  getIdPool,
  resetIdPool,
};
