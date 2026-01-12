const fs = require("fs");
const logger = require("../utils/logger");
const { CONFIG } = require("../config");

// In-memory storage for flood data
const dataMaps = {
  c8r7: new Map(),
  c9r7: new Map(),
  c8r8: new Map(),
  c9r9: new Map(),
};

let isDataLoaded = false;

let staticPois_flood = []; // To store loaded static POIs
let staticPois_fire = []; // To store loaded static POIs

let isStaticPoisLoaded = false;

// Utility: Clean array strings
function cleanArray(arr) {
  return arr.map((str) => (typeof str === "string" ? str.trim().replace(/\s+/g, " ") : str));
}

// Utility: Convert Map to Object
function mapToObject(map) {
  const obj = {};
  for (let [key, value] of map) {
    obj[key] = value;
  }
  return obj;
}

/**
 * Loads static POI data from the JSON file specified in config.
 */
function loadStaticPois() {
  if (isStaticPoisLoaded) {
    logger.info("Static POI data already loaded. Skipping.");
    return;
  }

  const poiFileConfigurations = [
    {
      name: "Flood",
      path: CONFIG.STATIC_POIS_PATH_FLOOD,
      setData: (data) => {
        staticPois_flood = data;
      },
      getData: () => staticPois_flood,
    },
    {
      name: "Fire",
      path: CONFIG.STATIC_POIS_PATH_FIRE,
      setData: (data) => {
        staticPois_fire = data;
      },
      getData: () => staticPois_fire,
    },
    // More config if needed
  ];

  poiFileConfigurations.forEach((config) => {
    let loadedPois = [];

    if (!config.path) {
      logger.warn(`Path for static ${config.name} POIs is not configured. Skipping loading for this type.`);
      config.setData([]); // Ensure the target array is empty if path is not configured
      return;
    }

    try {
      if (!fs.existsSync(config.path)) {
        logger.warn(`Static ${config.name} POI file not found: ${config.path}. No static ${config.name} POIs will be loaded.`);
        // loadedPois remains an empty array
      } else {
        const fileContent = fs.readFileSync(config.path, "utf8");
        const parsedPois = JSON.parse(fileContent);

        if (Array.isArray(parsedPois)) {
          loadedPois = parsedPois;
          logger.info(`Successfully loaded ${loadedPois.length} static ${config.name} POIs from ${config.path}.`);
        } else {
          logger.error(`Static ${config.name} POI file content from ${config.path} is not a valid JSON array. No ${config.name} POIs loaded from this file.`);
          // loadedPois remains an empty array
        }
      }
    } catch (error) {
      logger.error(`Error loading or parsing static ${config.name} POI data from ${config.path}:`, error);
    }
    config.setData(loadedPois); // Set the data to the corresponding staticPois variable
  });

  isStaticPoisLoaded = true;
}

/**
 * Retrieves all loaded static POIs.
 * @returns {Array} An array of static POI objects.
 */
function getStaticPois(type) {
  if (!isStaticPoisLoaded) {
    logger.warn("Attempted to get static POIs, but data is not loaded yet. Call loadStaticPois first.");
    return [];
  }
  if (type === "Fire") {
    return staticPois_fire;
  }
  if (type === "Flood") {
    return staticPois_flood;
  }
}

/**
 * Loads flood data from the CSV file specified in config.
 */
function loadFloodData() {
  // Prevent reloading if already loaded
  if (isDataLoaded) {
    logger.info("Flood data already loaded. Skipping.");
    return;
  }

  const filePath = CONFIG.FLOOD_DATA_PATH;
  logger.info(`Loading flood data from ${filePath}`);

  fs.readFile(filePath, "utf8", (err, data) => {
    if (err) {
      logger.error(`Error reading flood data file: ${filePath}`, err);
      // Decide how to handle this - maybe the server can run without it?
      return;
    }

    try {
      // Process the data
      const processedData = data.replace(/(\d+),(\d+)/g, "$1.$2"); // Handle comma decimals
      const rows = processedData.split("\n").filter((row) => row.trim() !== ""); // Split and remove empty lines

      if (rows.length < 2) {
        logger.error("Flood data file is empty or has no header/data rows.");
        return;
      }

      const headerRow = cleanArray(rows[0].split(";"));

      // Get column indices dynamically
      const timeStampCol = headerRow.indexOf("time_stamp");
      const c8r7Col = headerRow.indexOf("c8r7");
      const c9r7Col = headerRow.indexOf("c9r7");
      const c8r8Col = headerRow.indexOf("c8r8");
      const c9r9Col = headerRow.indexOf("c9r9"); // Corrected from c9r9

      // Validate column indices
      if ([timeStampCol, c8r7Col, c9r7Col, c8r8Col, c9r9Col].includes(-1)) {
        logger.error("Required columns missing in flood data file.", {
          header: headerRow,
          required: ["time_stamp", "c8r7", "c9r7", "c8r8", "c9r9"],
          indices: { timeStampCol, c8r7Col, c9r7Col, c8r8Col, c9r9Col },
        });
        return;
      }

      let dataCount = 0;
      // Regex to validate timestamp format more strictly (DD.MM.YYYY, HH:MM:SS)
      const timeRegex = /^\d{2}\.\d{2}\.\d{4}, \d{2}:\d{2}:\d{2}$/;

      // Start from the first data row (index 1)
      for (let i = 1; i < rows.length; i++) {
        let splitRow = cleanArray(rows[i].split(";"));

        // Ensure row has enough columns before accessing indices
        if (splitRow.length > Math.max(timeStampCol, c8r7Col, c9r7Col, c8r8Col, c9r9Col)) {
          const timestamp = splitRow[timeStampCol];

          if (timeRegex.test(timestamp)) {
            // Avoid duplicates by checking one map (assuming timestamps are unique across maps)
            if (!dataMaps.c8r7.has(timestamp)) {
              dataMaps.c8r7.set(timestamp, splitRow[c8r7Col]);
              dataMaps.c9r7.set(timestamp, splitRow[c9r7Col]);
              dataMaps.c8r8.set(timestamp, splitRow[c8r8Col]);
              dataMaps.c9r9.set(timestamp, splitRow[c9r9Col]);
              dataCount++;
            } else {
              logger.warn(`Duplicate timestamp found in flood data, skipping row ${i + 1}: ${timestamp}`);
            }
          } else {
            logger.warn(`Invalid timestamp format in flood data, skipping row ${i + 1}: ${timestamp}`);
          }
        } else {
          logger.warn(`Skipping row ${i + 1} due to insufficient columns.`);
        }
      }

      isDataLoaded = true;
      logger.info(`Processed ${dataCount} flood data entries.`);

      // Optionally save the processed data to a JSON file
      // saveFloodDataToJson(); // Consider if this is needed every time
    } catch (processingError) {
      logger.error("Error processing flood data content:", processingError);
    }
  });
}

/**
 * Saves the currently loaded flood data maps to a JSON file.
 */
function saveFloodDataToJson() {
  if (!isDataLoaded) {
    logger.warn("Cannot save flood data to JSON: Data not loaded yet.");
    return;
  }
  try {
    const combinedObject = {
      c8r7: mapToObject(dataMaps.c8r7),
      c9r7: mapToObject(dataMaps.c9r7),
      c8r8: mapToObject(dataMaps.c8r8),
      c9r9: mapToObject(dataMaps.c9r9),
    };

    const filePath = CONFIG.OUTPUT_DATA_PATH;
    const jsonString = JSON.stringify(combinedObject, null, 2); // Pretty print JSON

    fs.writeFile(filePath, jsonString, "utf8", (err) => {
      if (err) {
        logger.error(`Error writing flood data to JSON file: ${filePath}`, err);
      } else {
        logger.info(`Flood data saved to ${filePath}`);
      }
    });
  } catch (error) {
    logger.error("Error saving flood data to JSON:", error);
  }
}

/**
 * Retrieves flood data values for a specific time and map.
 * Used by legacy message handler.
 * @param {string} inputTime Time in HH:MM format.
 * @param {string} mapKey Key for the data map (e.g., 'c8r7').
 * @returns {string} Comma-separated values for time, time+10min, time+20min, or "null,null,null".
 */
function getValuesForTime(inputTime, mapKey) {
  const dataMap = dataMaps[mapKey];
  if (!dataMap || !(dataMap instanceof Map)) {
    logger.error(`Invalid mapKey provided to getValuesForTime: ${mapKey}`);
    return "null,null,null";
  }
  if (!isDataLoaded) {
    logger.warn(`Attempted to get flood values, but data is not loaded yet.`);
    return "null,null,null";
  }

  try {
    // Validate inputTime format HH:MM
    if (!/^\d{2}:\d{2}$/.test(inputTime)) {
      logger.warn(`Invalid inputTime format for getValuesForTime: ${inputTime}`);
      return "null,null,null";
    }

    const [inputHours, inputMinutes] = inputTime.split(":").map(Number);
    const inputTotalMinutes = inputHours * 60 + inputMinutes;

    let value1 = null,
      value2 = null,
      value3 = null;

    // Iterate through the specific map (e.g., dataMaps.c8r7)
    for (const [key, value] of dataMap.entries()) {
      // Key format is expected: DD.MM.YYYY, HH:MM:SS
      if (typeof key !== "string" || !key.includes(", ")) continue; // Skip invalid keys

      const timePart = key.split(", ")[1]; // Get "HH:MM:SS"
      if (!timePart || timePart.split(":").length !== 3) continue; // Skip invalid time format

      const [hours, minutes] = timePart.split(":").map(Number);
      const totalMinutes = hours * 60 + minutes;

      // Check for matches at T, T+10, T+20 minutes
      if (totalMinutes === inputTotalMinutes) value1 = value;
      if (totalMinutes === inputTotalMinutes + 10) value2 = value;
      if (totalMinutes === inputTotalMinutes + 20) value3 = value;
    }

    // Return values, substituting "null" if not found
    return `${value1 ?? "null"},${value2 ?? "null"},${value3 ?? "null"}`;
  } catch (error) {
    logger.error(`Error in getValuesForTime for map ${mapKey}:`, error);
    return "null,null,null";
  }
}

module.exports = {
  loadFloodData,
  saveFloodDataToJson,
  getValuesForTime,
  loadStaticPois,
  getStaticPois,
};
