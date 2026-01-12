module.exports = {
  PORT: process.env.PORT || 3000,
  WS_PORT: process.env.WS_PORT || 8080,
  KAFKA_BROKERS: process.env.KAFKA_BROKERS ? process.env.KAFKA_BROKERS.split(',') : ['localhost:9092'],
  KAFKA_TOPIC: process.env.KAFKA_TOPIC || 'default_topic',
  FLOOD_DATA_PATH: process.env.FLOOD_DATA_PATH || './data/flood_data.csv',
};