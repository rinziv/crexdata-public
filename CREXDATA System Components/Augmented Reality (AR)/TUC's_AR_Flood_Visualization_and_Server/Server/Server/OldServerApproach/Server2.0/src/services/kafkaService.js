// src/services/kafkaService.js
const { Kafka } = require('kafkajs');
const CONFIG = require('../config/config');

let kafka;
let producer;
let consumer;

async function initializeKafka() {
  kafka = new Kafka({
    clientId: CONFIG.KAFKA_CLIENT_ID,
    brokers: CONFIG.KAFKA_BROKERS,
  });

  producer = kafka.producer();
  consumer = kafka.consumer({ groupId: CONFIG.KAFKA_GROUP_ID });

  await producer.connect();
  await consumer.connect();
}

async function sendMessage(topic, message) {
  await producer.send({
    topic,
    messages: [{ value: JSON.stringify(message) }],
  });
}

async function consumeMessages(topic, callback) {
  await consumer.subscribe({ topic, fromBeginning: true });

  await consumer.run({
    eachMessage: async ({ topic, partition, message }) => {
      callback(JSON.parse(message.value.toString()));
    },
  });
}

module.exports = {
  initializeKafka,
  sendMessage,
  consumeMessages,
};