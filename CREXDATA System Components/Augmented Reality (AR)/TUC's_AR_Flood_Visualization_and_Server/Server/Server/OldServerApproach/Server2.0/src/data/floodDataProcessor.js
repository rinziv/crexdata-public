// This file exports functions for processing flood data, including loading and transforming data for use in the application.

import fs from 'fs';
import path from 'path';
import { logger } from '../utils/logger';
import { cleanArray } from '../utils/formatters';

const floodDataPath = path.join(__dirname, 'floodData.csv'); // Adjust the path as necessary
const floodDataMap = new Map();

export function loadFloodData() {
  fs.readFile(floodDataPath, 'utf8', (err, data) => {
    if (err) {
      logger.error('Error reading flood data file:', err);
      return;
    }

    const rows = data.split('\n').map(row => cleanArray(row.split(',')));
    rows.forEach(row => {
      if (row.length > 1) {
        const timestamp = row[0];
        const value = row[1];
        floodDataMap.set(timestamp, value);
      }
    });

    logger.info(`Loaded ${floodDataMap.size} flood data entries`);
  });
}

export function getFloodData(timestamp) {
  return floodDataMap.get(timestamp) || null;
}

export function getAllFloodData() {
  return Array.from(floodDataMap.entries());
}