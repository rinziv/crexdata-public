import express from 'express';
import ClientController from './controllers/clientController.js';
import PairingController from './controllers/pairingController.js';

const router = express.Router();
const clientController = new ClientController();
const pairingController = new PairingController();

// Client routes
router.get('/clients', clientController.getClients);
router.get('/clients/:id', clientController.getClientById);
router.post('/clients', clientController.createClient);
router.delete('/clients/:id', clientController.deleteClient);

// Pairing routes
router.get('/pairings', pairingController.getPairings);
router.post('/pairings', pairingController.createPairing);
router.delete('/pairings/:id', pairingController.deletePairing);

export default router;