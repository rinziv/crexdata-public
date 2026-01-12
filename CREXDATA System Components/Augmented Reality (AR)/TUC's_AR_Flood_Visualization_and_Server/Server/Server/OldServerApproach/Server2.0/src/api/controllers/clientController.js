class ClientController {
  constructor(clientService) {
    this.clientService = clientService;
  }

  async getClients(req, res) {
    try {
      const clients = await this.clientService.getAllClients();
      res.status(200).json(clients);
    } catch (error) {
      res.status(500).json({ error: 'Failed to retrieve clients' });
    }
  }

  async getClientById(req, res) {
    const { id } = req.params;
    try {
      const client = await this.clientService.getClientById(id);
      if (client) {
        res.status(200).json(client);
      } else {
        res.status(404).json({ error: 'Client not found' });
      }
    } catch (error) {
      res.status(500).json({ error: 'Failed to retrieve client' });
    }
  }

  async createClient(req, res) {
    const clientData = req.body;
    try {
      const newClient = await this.clientService.createClient(clientData);
      res.status(201).json(newClient);
    } catch (error) {
      res.status(500).json({ error: 'Failed to create client' });
    }
  }

  async updateClient(req, res) {
    const { id } = req.params;
    const clientData = req.body;
    try {
      const updatedClient = await this.clientService.updateClient(id, clientData);
      if (updatedClient) {
        res.status(200).json(updatedClient);
      } else {
        res.status(404).json({ error: 'Client not found' });
      }
    } catch (error) {
      res.status(500).json({ error: 'Failed to update client' });
    }
  }

  async deleteClient(req, res) {
    const { id } = req.params;
    try {
      const deleted = await this.clientService.deleteClient(id);
      if (deleted) {
        res.status(204).send();
      } else {
        res.status(404).json({ error: 'Client not found' });
      }
    } catch (error) {
      res.status(500).json({ error: 'Failed to delete client' });
    }
  }
}

export default ClientController;