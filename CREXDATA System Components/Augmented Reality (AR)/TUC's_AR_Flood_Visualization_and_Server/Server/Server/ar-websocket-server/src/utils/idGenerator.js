const logger = require("../utils/logger");

const idPool = new Set();
const MAX_CLIENTS = 100;

/**
 * Generates a unique integer identifier for each Hololens client.
 * @returns {string | null} A unique identifier or null if all IDs are currently in use.
 */
function generateUniqueId() {
  if (idPool.size >= MAX_CLIENTS) {
    logger.warn("Cannot generate new ID: ID pool is full.");
    return null;
  }

  let id;
  do {
    id = Math.floor(Math.random() * MAX_CLIENTS) + 1;
  } while (idPool.has(id));

  logger.info(`Generated new unique ID: ${id}`);
  idPool.add(id);
  return String(id);
}

/**
 * Removes an identifier from the pool.
 * This is used when a client disconnects.
 * @param {number} id - The numeric ID to remove.
 * @returns {boolean}
 */
function removeId(id) {
  if (idPool.has(id)) {
    //logger.info(`ID ${id} found in the pool. Removing it.`);
    idPool.delete(id);
    return true;
  }
  return false;
}

/**
 * Adds an identifier to the pool.
 * @param {number} id - The numeric ID to add.
 * @returns {boolean}
 */
function addID(id) {
  // Make sure id is a number
  id = Number(id);
  if (idPool.has(id)) {
    // Check for the number in the pool
    //logger.warn(`ID ${id} already exists in the pool. Generating a new one.`);
    return false;
  }
  idPool.add(id);
  return true;
}

function getIdPool() {
  //log the array to show specific ids
  logger.info(`Current ID pool: ${Array.from(idPool).join(", ")}`);
}

module.exports = {
  generateUniqueId,
  removeId,
  addID,
  getIdPool,
};
