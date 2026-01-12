const logger = require("../utils/logger");

/**
 * Message Formatter Service
 * Formats messages into standardized event-driven format for Kafka publishing
 */

/**
 * Formats GPS message into event-driven format for Kafka
 * @param {object} gpsData The GPS data from Unity Android app
 * @returns {object} Event-driven formatted message
 */
function formatGpsForKafka(gpsData) {
  const { messageId, senderId, senderName, deviceId, targetDeviceId, location, device, session, timestamp, metadata } = gpsData;

  return {
    // Event metadata
    eventType: "LOCATION_UPDATE",
    eventId: messageId || `gps-${Date.now()}-${Math.random().toString(36).substr(2, 9)}`,
    eventTimestamp: timestamp || new Date().toISOString(),

    // Source information
    source: {
      userId: senderId,
      userName: senderName || "Unknown User",
      senderDeviceId: deviceId,
      targetDeviceId: targetDeviceId,
      senderDeviceType: "android",
      application: "unity-ar-mobile",
    },

    // Location payload (all GPS data)
    payload: {
      location: {
        latitude: location?.latitude,
        longitude: location?.longitude,
        altitude: location?.altitude,
        accuracy: {
          horizontal: location?.horizontalAccuracy,
          vertical: location?.verticalAccuracy,
        },
        capturedAt: location?.timestamp,
      },

      // Device information
      device: {
        model: device?.model,
        operatingSystem: device?.os,
        battery: {
          level: device?.batteryLevel,
          status: device?.batteryStatus,
        },
      },

      // Session information
      session: {
        startTime: session?.startTime,
        uptimeSeconds: session?.uptime,
      },

      // Metadata
      metadata: {
        updateIntervalMs: metadata?.updateInterval,
        dataSource: metadata?.source || "live",
        relayedBy: metadata?.relayedBy || "server",
      },
    },

    // Processing metadata
    processing: {
      receivedAt: new Date().toISOString(),
      processedBy: "ar-tuc-server",
    },
  };
}

/**
 * Formats alert message into event-driven format for Kafka
 * @param {object} alertData The alert data
 * @returns {object} Event-driven formatted message
 */
function formatAlertForKafka(alertData) {
  return {
    eventType: "ALERT_CREATED",
    eventId: alertData.alertID || `alert-${Date.now()}`,
    eventTimestamp: new Date().toISOString(),
    eventVersion: "1.0",

    source: {
      application: "ar-websocket-server",
      trigger: alertData.trigger || "manual",
    },

    payload: {
      alertId: alertData.alertID,
      alertType: alertData.alertType,
      severity: alertData.severity || "medium",
      message: alertData.message,
      location: alertData.location,
      metadata: alertData.metadata || {},
    },

    processing: {
      receivedAt: new Date().toISOString(),
      processedBy: "ar-websocket-server",
      version: "1.0.0",
    },
  };
}

/**
 * Formats pairing event into event-driven format for Kafka
 * @param {object} pairingData The pairing data
 * @returns {object} Event-driven formatted message
 */
function formatPairingForKafka(pairingData) {
  return {
    eventType: "DEVICE_PAIRING",
    eventId: `pairing-${Date.now()}`,
    eventTimestamp: new Date().toISOString(),
    eventVersion: "1.0",

    source: {
      application: "ar-websocket-server",
      initiator: pairingData.initiator,
    },

    payload: {
      phoneId: pairingData.phoneId,
      unityId: pairingData.unityId,
      pairingStatus: pairingData.status,
      timestamp: pairingData.timestamp || new Date().toISOString(),
    },

    processing: {
      receivedAt: new Date().toISOString(),
      processedBy: "ar-websocket-server",
      version: "1.0.0",
    },
  };
}

/**
 * Generic message formatter - routes to specific formatter based on message type
 * @param {string} messageType The type of message (gps, alert, pairing, etc.)
 * @param {object} data The message data
 * @returns {object} Formatted event-driven message
 */
function formatMessageForKafka(messageType, data) {
  try {
    switch (messageType.toLowerCase()) {
      case "gps":
      case "location":
        return formatGpsForKafka(data);

      case "alert":
        return formatAlertForKafka(data);

      case "pairing":
        return formatPairingForKafka(data);

      case "routePublish":
        return formatRoutePublishForKafka(data);

      case "routeClear":
        return formatRouteClearForKafka(data);

      default:
        logger.warn(`No specific formatter for message type: ${messageType}. Using generic format.`);
        return {
          eventType: messageType.toUpperCase(),
          eventId: `${messageType}-${Date.now()}`,
          eventTimestamp: new Date().toISOString(),
          eventVersion: "1.0",
          payload: data,
          processing: {
            receivedAt: new Date().toISOString(),
            processedBy: "ar-websocket-server",
            version: "1.0.0",
          },
        };
    }
  } catch (error) {
    logger.error(`Error formatting message of type ${messageType}:`, error);
    // Return original data with error info if formatting fails
    return {
      eventType: "FORMATTING_ERROR",
      eventId: `error-${Date.now()}`,
      eventTimestamp: new Date().toISOString(),
      originalMessageType: messageType,
      originalData: data,
      error: error.message,
    };
  }
}

/**
 * Validates that required GPS fields are present
 * @param {object} gpsData The GPS data to validate
 * @returns {boolean} True if valid, false otherwise
 */
function validateGpsData(gpsData) {
  const required = ["senderId", "location"];
  const locationRequired = ["latitude", "longitude"];

  // Check required top-level fields
  for (const field of required) {
    if (!gpsData[field]) {
      logger.warn(`GPS data validation failed: missing ${field}`);
      return false;
    }
  }

  // Check required location fields
  for (const field of locationRequired) {
    if (gpsData.location[field] === undefined || gpsData.location[field] === null) {
      logger.warn(`GPS data validation failed: missing location.${field}`);
      return false;
    }
  }

  return true;
}

/**
 * Formats route publish message into event-driven format for Kafka
 * @param {object} routeData The route data from Unity client
 * @param {object} clientInfo Client information (userId, userName, deviceId)
 * @returns {object} Event-driven formatted message
 */
function formatRoutePublishForKafka(routeData, clientInfo) {
  const { unityId, timestamp, routeInfo } = routeData;
  const { userId, userName, deviceId } = clientInfo || {};

  return {
    // Event metadata
    eventType: "ROUTE_UPDATE",
    eventId: `route-${unityId || userId}-${Date.now()}`,
    eventTimestamp: timestamp || new Date().toISOString(),

    // Source information
    source: {
      userId: unityId || userId,
      userName: userName || `User ${unityId || userId}`,
      deviceId: deviceId,
      deviceType: "Hololens 2",
      application: "Crexdata-AR-for-flood-emergency-response",
    },

    // Route payload following GeoJSON standard
    payload: {
      route: {
        // Route metadata
        profile: routeInfo?.profile || "driving",
        distanceMeters: routeInfo?.distance,
        distanceKilometers: routeInfo?.distance ? (routeInfo.distance / 1000).toFixed(2) : null,
        waypointCount: routeInfo?.waypointCount,

        // Start and end points
        origin: {
          latitude: routeInfo?.startPoint?.latitude,
          longitude: routeInfo?.startPoint?.longitude,
          displayName: "Start Point",
        },
        destination: {
          latitude: routeInfo?.endPoint?.latitude,
          longitude: routeInfo?.endPoint?.longitude,
          displayName: "End Point",
        },

        // GeoJSON geometry
        geometry: {
          type: routeInfo?.geometry?.type || "LineString",
          coordinates: routeInfo?.geometry?.coordinates || [],
        },

        // Additional metadata
        metadata: {
          createdAt: timestamp || new Date().toISOString(),
          routeType: "navigation",
          transportMode: routeInfo?.profile || "driving",
        },
      },
    },

    processing: {
      receivedAt: new Date().toISOString(),
      processedBy: "ar-tuc-server",
    },
  };
}

/**
 * Formats route clear message into event-driven format for Kafka
 * @param {object} routeData The route clear data from Unity client
 * @param {object} clientInfo Client information (userId, userName, deviceId)
 * @returns {object} Event-driven formatted message
 */
function formatRouteClearForKafka(routeData, clientInfo) {
  const { unityId, timestamp, reason } = routeData;
  const { userId, userName, deviceId } = clientInfo || {};

  return {
    // Event metadata
    eventType: "ROUTE_CLEAR",
    eventId: `route-clear-${unityId || userId}-${Date.now()}`,
    eventTimestamp: timestamp || new Date().toISOString(),
    reason: reason || "Unknown",

    // Source information
    source: {
      userId: unityId || userId,
      userName: userName || `User ${unityId || userId}`,
      deviceId: deviceId,
      deviceType: "Hololens 2",
      application: "Crexdata-AR-for-flood-emergency-response",
    },

    // Clear payload
    payload: {
      action: "clear_route",
      userId: unityId || userId,
      userName: userName || `User ${unityId || userId}`,
      clearTimestamp: timestamp || new Date().toISOString(),
    },

    // Processing metadata
    processing: {
      receivedAt: new Date().toISOString(),
      processedBy: "ar-tuc-server",
    },
  };
}

module.exports = {
  formatGpsForKafka,
  formatAlertForKafka,
  formatPairingForKafka,
  formatMessageForKafka,
  formatRoutePublishForKafka,
  formatRouteClearForKafka,
  validateGpsData,
};
