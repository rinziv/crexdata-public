class Client {
  constructor(ws, type, clientId) {
    this.ws = ws;
    this.type = type;
    this.clientId = clientId;
    this.connectionTime = new Date().toISOString();
    this.pairedClientId = null;
  }

  setPairedClient(clientId) {
    this.pairedClientId = clientId;
  }

  getClientInfo() {
    return {
      id: this.clientId,
      type: this.type,
      connectedSince: this.connectionTime,
      pairedWith: this.pairedClientId,
    };
  }
}

export default Client;