// Import necessary modules

const pool = require("./dbPoolConfig");
const express = require("express");
const app = express();

const fs = require("fs");
const path = require("path");
const os = require("os");
const filePath = "C:/Users/CREX DATA/Downloads/AR_workaround.csv";
const { Kafka } = require("kafkajs");
const WebSocket = require("ws");

let c8r7Mapped = new Map();
let c9r7Mapped = new Map();
let c8r8Mapped = new Map();
let c9r9Mapped = new Map();

app.get("/data", async (req, res) => {
  try {
    const connection = await pool.getConnection(); // Get a connection from the pool
    const [rows, fields] = await connection.query("SELECT * FROM nodes"); // Replace 'your_table' with your actual table name
    connection.release(); // Release the connection back to the pool
    console.log(rows);
    res.json(rows); // Send the retrieved rows as JSON response
  } catch (err) {
    console.error("Error executing query:", err);
    res.status(500).send("Error " + err.message); // Send an error response with status code 500
  }
});

// Start the Express server
const PORT = process.env.PORT || 3000;
app.listen(PORT, () => {
  console.log(`Server is running on port ${PORT}`);
});

let brokers = ["192.168.0.111:9092"];
let currentTopic = "my-topic";
// Create a WebSocket server
const wss = new WebSocket.Server({ port: 8080 }, () => {
  console.log("Server started");
});

// Log when the server starts listening
wss.on("listening", () => console.log("Listening on port 8080"));

// Log any errors that occur on the server
wss.on("error", (error) => console.error("WebSocket server error:", error));

//Funvtion to send messages to all connected consumers via ws
async function sendToAll(message) {
  wss.clients.forEach((client) => {
    if (client.readyState === WebSocket.OPEN) {
      client.send(message);
    }
  });
}

async function kafkaConnection() {
  // Create a new Kafka client
  const kafka = new Kafka({
    clientId: "HoloLensDevice",
    brokers: brokers,
    connectionTimeout: 3000, // 3 seconds timeout
    retry: {
      retries: 2, // Number of retries
      initialRetryTime: 1000, // Initial retry interval (1 second)
      maxRetryTime: 3000, // Maximum retry interval (3 seconds)
    },
    logCreator: () => {
      return ({ namespace, level, label, log }) => {
        // Ignore log messages from KafkaJS
        if (namespace === "kafkajs") {
          return;
        }

        // Log other messages in a custom format
        const { message, ...rest } = log;
        console.log(`${label}: ${message}`, rest);
      };
    },
  });

  // Map of WebSocket clients to Kafka consumers
  const consumers = new Map();
  let clients = {};
  let phone_clients = {};

  wss.on("connection", async (ws, req) => {
    const userAgent = req.headers["user-agent"];
    const ipAddress = req.socket.remoteAddress || req.connection.remoteAddress;
    const connectionTime = new Date().toISOString();

    console.log("User-Agent:", userAgent);
    console.log("IP Address:", ipAddress);
    console.log("Connection Time:", connectionTime);

    ws.send(
      JSON.stringify({
        message: "Connection established",
        userAgent: userAgent,
        ipAddress: ipAddress,
        connectionTime: connectionTime,
      })
    );

    let consumer;
    let phone = false;
    let isConnected = true; // Flag to track if the client is connected
    //let recievedID = false;

    ws.on("close", async () => {
      console.log("WebSocket client disconnected");
      isConnected = false;

      // Remove the client from the clients list
      for (let deviceName in clients) {
        if (clients[deviceName] === ws) {
          delete clients[deviceName];
          break;
        }
      }

      // Close the Kafka consumer only if it exists and is not a phone connection
      if (!phone && consumers.has(ws)) {
        try {
          const consumer = consumers.get(ws);
          await consumer.disconnect();
          consumers.delete(ws);
          console.log("Kafka consumer disconnected");
        } catch (error) {
          console.error("Error closing Kafka consumer:", error);
        }
      }
    });

    ws.on("message", (message) => {
      const rows = message.toString().split("\n");
      const firstRowSplit = rows[0].toString().split(",");
      const separatedStrings = message.toString().split(",");
      if (separatedStrings[1] === "ID") {
        //Create a new Kafka consumer for this WebSocket client
        consumer = kafka.consumer({ groupId: separatedStrings[0] });
        // Store the consumer in the map
        consumers.set(ws, consumer);

        //attemptKafkaConnection(); // Start the Kafka connection attempt when id is recieved

        clients[separatedStrings[0]] = ws;
      } else if (separatedStrings[0].includes("flood")) {
        if (separatedStrings[0].includes("1")) {
          console.log(1);

          ws.send(getValuesForTime(separatedStrings[1], c9r7Mapped) + ",water");
        } else {
          console.log(2);

          ws.send(getValuesForTime(separatedStrings[1], c8r8Mapped) + ",water");
        }
      } else if (separatedStrings[2] === "seek") {
        // When consumer connect to kafka topic, start seeking from specific offset
        consumer.seek({
          topic: currentTopic,
          partition: parseInt(separatedStrings[0]),
          offset: parseInt(separatedStrings[1]), //+1
        });
      } else if (message.toString() === "phone") {
        console.log("PHONE CONNECTED\n");
        phone = true;
        phone_clients[separatedStrings[1]] = ws;
      } else if (separatedStrings[2] != null) {
        if (separatedStrings[2].includes("gps")) {
          // Send the location to the paired Unity device
          // Exctract 1 from string "HOLOLENS ID: 1"
          let clientID = separatedStrings[1].split(":")[1].trim();
          let unityClient = clients[clientID]; //UNITY ws of client

          //Retrieve clinet gps data
          let client_gps_lat = separatedStrings[2].split(":")[1].trim();
          let client_gps_long = separatedStrings[3].trim();
          if (unityClient && unityClient.readyState === WebSocket.OPEN) {
            unityClient.send(client_gps_lat + "," + client_gps_long + ", gps");
          }
          console.log("Sending location to Unity client: %s\n", message.toString());
          //sendToAll(message.toString());
        } else console.log("Received: %s", message);
      } else console.log("Received: %s", message);
      //  }
    });

    const attemptKafkaConnection = async () => {
      while (!phone && isConnected) {
        try {
          // Try to connect to Kafka
          await consumer.connect();
          // If the connection is successful, subscribe to the topic
          await consumer.subscribe({ topic: currentTopic, fromBeginning: true });
          // Start consuming messages
          await consumer.run({
            // This function will be called for each message received
            eachMessage: async ({ topic, partition, message }) => {
              // Log the details of the message
              console.log("Received message:", {
                topic,
                partition,
                offset: message.offset,
                message: message.value.toString(),
              });

              if (ws.readyState === WebSocket.OPEN) {
                //Send message to unity client
                ws.send(topic + "," + partition + "," + message.value.toString() + "," + message.offset + ",info");
              } else {
                // Client is not connected, disconnect the consumer
                await consumer.disconnect();
              }
            },
          });

          ws.send("Readys");

          // Read the JSON file

          // If we reach this point, the connection was successful, so we break the loop
          break;
        } catch (error) {
          // If an error occurs, log it and retry after a delay
          console.error("\nFailed to connect to Kafka, retrying...\n");

          // If the WebSocket client has disconnected, stop trying to connect
          if (!isConnected) {
            console.log("BREAK");
            break;
          }

          await new Promise((resolve) => setTimeout(resolve, 500)); // Wait for 0.5 seconds
        }
      }
    };
  });
}

// Start the Kafka connection
kafkaConnection().catch(console.error);

const cleanArray = (arr) => {
  return arr.map((str) => str.trim().replace(/\s+/g, " "));
};
//#region FLOOD DATA
fs.readFile(filePath, "utf8", (err, data) => {
  if (err) {
    console.error("Error reading the file:", err);
    return;
  }

  // Replace commas with periods for numeric values
  const processedData = data.replace(/(\d+),(\d+)/g, "$1.$2");

  // Split the processed data into rows
  const rows = processedData.split("\n");
  const firstRowSplit = cleanArray(rows[0].split(";"));

  // Print the string
  const area0Column = firstRowSplit.indexOf("time_stamp");

  const area1Column = firstRowSplit.indexOf("c8r7");
  const area2Column = firstRowSplit.indexOf("c9r7");
  const area3Column = firstRowSplit.indexOf("c8r8");
  const area4Column = firstRowSplit.indexOf("c9r9");

  //let c8r7Mapped = new Map();
  //let c9r7Mapped = new Map();
  //let c8r8Mapped = new Map();
  //let c9r9Mapped = new Map();

  let regex = /^\d{2}\.\d{2}\.\d{4}, \d{2}:\d{2}:\d{2}$/;
  console.log(regex);
  rows.forEach((value, index) => {
    if (index > 1) {
      let splitRow = cleanArray(value.split(";")); // Split the second row by space or any other delimiter you need
      if (regex.test(splitRow[0])) {
        if (c8r7Mapped.has(splitRow[0]) === false) console.log(splitRow[area1Column]);
        c8r7Mapped.set(splitRow[0], splitRow[area1Column]);
        // console.log(c8r7Mapped.get(splitRow[0]));
        if (c9r7Mapped.has(splitRow[0]) === false) c9r7Mapped.set(splitRow[0], splitRow[area2Column]);
        if (c8r8Mapped.has(splitRow[0]) === false) c8r8Mapped.set(splitRow[0], splitRow[area3Column]);
        if (c9r9Mapped.has(splitRow[0]) === false) c9r9Mapped.set(splitRow[0], splitRow[area4Column]);
        // console.log( JSON.stringify(Array.from(c9r9Mapped.entries())));
      }
    }
  });

  //****************************
  //console.log(c9r7Mapped);
  //****************************

  const c8r7Object = mapToObject(c8r7Mapped);
  const c9r7Object = mapToObject(c9r7Mapped);
  const c8r8Object = mapToObject(c8r8Mapped);
  const c9r9Object = mapToObject(c9r9Mapped);

  const combinedObject = {
    c8r7: c8r7Object,
    c9r7: c9r7Object,
    c8r8: c8r8Object,
    c9r9: c9r9Object,
  };
  const area1Json = JSON.stringify(c9r7Object, null, 2); // 2 spaces for indentation
  const area2Json = JSON.stringify(c8r8Object, null, 2); // 2 spaces for indentation

  const desktopPath = path.join(os.homedir(), "desktop");
  const filePath = path.join(desktopPath, "maps.json");
  const jsonString = JSON.stringify(combinedObject, null, 2); // 2 spaces for indentation

  //  const fileP = 'C:/Users/CREX DATA/desktop/output.json';
  // Specify the file path on your desktop

  fs.writeFile(filePath, jsonString, "utf8", (err) => {
    if (err) {
      console.error("Error writing to file", err);
    } else {
      console.log("File has been written successfully");
    }
  });
});

//***************** FUNCTIONS **********************//

async function deleteConsumerGroup(groupId) {
  // Create a new Kafka client
  const kafka = new Kafka({
    clientId: "my-app",
    brokers: ["192.168.0.110:9092"],
  });

  // Create an admin client
  const admin = kafka.admin();

  try {
    // Connect to the admin client
    await admin.connect();

    // Delete the consumer group
    await admin.deleteGroups([groupId]);
    console.log(`Consumer group ${groupId} deleted successfully\n`);
  } catch (error) {
    if (error.groups) {
      for (const group of error.groups) {
        console.error(`Failed to delete consumer group ${group.groupId}\n`);
        if (group.errorCode === 69) console.error(`Consumer group ${group.groupId} does not exist\n`);
        else if (group.errorCode === 68) console.error(`Consumer group ${group.groupId} is not empty\n`);
        else console.error("Unknown error\n:", group.error);
      }
    } else {
      console.error("Unknown error\n:", error);
    }
  } finally {
    // Always disconnect the admin client when you're done
    await admin.disconnect();
  }
}

function containsBothColumnNames(data) {
  // Define regular expressions for the column names
  const timeStampRegex = /\btime_stamp\b/;
  const c89r7Regex = /\bc89r7\b/;

  // Check if the string contains both column names
  const containsTimeStamp = timeStampRegex.test(data);
  const containsC89R7 = c89r7Regex.test(data);
  // Return true only if both column names are found
  return containsTimeStamp && containsC89R7;
}

function mapToObject(map) {
  let obj = {};
  for (let [key, value] of map) {
    obj[key] = value;
  }
  return obj;
}

function dateTime() {
  let date_time = new Date();

  // get current date
  // adjust 0 before single digit date
  let date = ("0" + date_time.getDate()).slice(-2);

  // get current month
  let month = ("0" + (date_time.getMonth() + 1)).slice(-2);

  // get current year
  let year = date_time.getFullYear();

  // get current hours
  let hours = date_time.getHours();

  // get current minutes
  let minutes = date_time.getMinutes();

  // get current seconds
  let seconds = date_time.getSeconds();

  // prints date in YYYY-MM-DD format
  // console.log(year + "-" + month + "-" + date);

  // prints date & time in YYYY-MM-DD HH:MM:SS format
  //console.log(year + "-" + month + "-" + date + " " + hours + ":" + minutes + ":" + seconds);
  return date.concat(".", month, ".", year.toString(), ", ", hours.toString(), ":", minutes.toString(), ":00");
  //   console.log(result);
}

function floodLevel(s1, s2) {
  // Create the formatted string using template literals
  const timeString = "16:00";

  // Split the string by the colon and take the second part (minutes)
  const minutesString = timeString.split(":")[1];

  // Convert the minutes part to an integer
  const minutesInt = parseInt(minutesString, 10);
  if (minutesInt + 10 > 60) {
    const str10 = (10 - (60 - minutesInt)).toString();
    const formattedString10 = `02.07.2016, ${str10}:00`;
  } else if (minutesInt + 20 > 60) {
  }

  // Output the result
  console.log(minutesInt); // 0
  const formattedString = `02.07.2016, ${s2}:00`;
  if (s1 === "flood1") {
    return c9r9Mapped.get(formattedString);
  }
}

function getValuesForTime(inputTime, dataMap) {
  // Convert the inputTime to minutes
  const [inputHours, inputMinutes] = inputTime.split(":").map(Number);
  const inputTotalMinutes = inputHours * 60 + inputMinutes;

  let value1 = null,
    value2 = null,
    value3 = null;

  // Ensure dataMap is properly formatted
  if (!(dataMap instanceof Map)) {
    throw new Error("dataMap is not a Map instance");
  }

  for (const [key, value] of dataMap) {
    // Ensure key is properly formatted
    if (typeof key !== "string" || !key.includes(", ")) {
      console.error(`Invalid key format: ${key}`);
      continue;
    }

    const [date, time] = key.split(", ");

    // Ensure time is properly formatted
    if (typeof time !== "string" || time.split(":").length !== 3) {
      console.error(`Invalid time format: ${time}`);
      continue;
    }

    const [hours, minutes] = time.split(":").map(Number);
    const totalMinutes = hours * 60 + minutes;

    if (totalMinutes === inputTotalMinutes) value1 = value;
    if (totalMinutes === inputTotalMinutes + 10) value2 = value;
    if (totalMinutes === inputTotalMinutes + 20) value3 = value;
  }

  return `${value1 ?? "null"},${value2 ?? "null"},${value3 ?? "null"}`;
}
//const inputTime = "16:42";
//console.log(getValuesForTime(inputTime, c9r7Mapped)); // Ensure c9r7Mapped is properly populated before calling

//#endregion
