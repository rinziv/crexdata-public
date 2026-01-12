class ServerStats {
  constructor() {
    this.totalConnections = 0;
    this.messagesSent = 0;
    this.messagesReceived = 0;
    this.errors = 0;
    this.lastError = null;
  }

  incrementConnections() {
    this.totalConnections++;
  }

  incrementMessagesSent() {
    this.messagesSent++;
  }

  incrementMessagesReceived() {
    this.messagesReceived++;
  }

  incrementErrors() {
    this.errors++;
  }

  setLastError(error) {
    this.lastError = {
      time: new Date().toISOString(),
      message: error.message,
      stack: error.stack,
    };
  }

  resetStats() {
    this.totalConnections = 0;
    this.messagesSent = 0;
    this.messagesReceived = 0;
    this.errors = 0;
    this.lastError = null;
  }
}

export default ServerStats;