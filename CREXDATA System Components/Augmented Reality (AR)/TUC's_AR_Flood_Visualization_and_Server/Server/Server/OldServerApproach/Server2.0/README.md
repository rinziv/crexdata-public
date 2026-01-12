# Server2.0 Project

## Overview
Server2.0 is a Node.js application designed to manage client connections and facilitate communication between different types of clients, including Unity and phone clients. The application utilizes WebSocket for real-time communication and Kafka for message handling.

## Project Structure
```
Server2.0
├── src
│   ├── config
│   │   ├── config.js
│   │   └── constants.js
│   ├── api
│   │   ├── routes.js
│   │   └── controllers
│   │       ├── clientController.js
│   │       └── pairingController.js
│   ├── services
│   │   ├── kafkaService.js
│   │   ├── websocketService.js
│   │   └── dataService.js
│   ├── handlers
│   │   ├── messageHandler.js
│   │   ├── legacyMessageHandler.js
│   │   └── pairingHandler.js
│   ├── models
│   │   ├── Client.js
│   │   └── ServerStats.js
│   ├── utils
│   │   ├── logger.js
│   │   ├── idGenerator.js
│   │   └── formatters.js
│   ├── data
│   │   ├── floodDataProcessor.js
│   │   └── dataMaps.js
│   └── server.js
├── package.json
├── package-lock.json
├── .env
├── .env.example
├── .gitignore
└── README.md
```

## Installation
1. Clone the repository:
   ```
   git clone <repository-url>
   ```
2. Navigate to the project directory:
   ```
   cd Server2.0
   ```
3. Install the dependencies:
   ```
   npm install
   ```

## Configuration
- Create a `.env` file in the root directory and define the necessary environment variables as specified in `.env.example`.

## Usage
To start the application, run:
```
npm start
```

## API Endpoints
- `/api/clients`: Retrieve active client information.
- `/api/pairings`: Get active and pending pairings between clients.

## Contributing
Contributions are welcome! Please submit a pull request or open an issue for discussion.

## License
This project is licensed under the MIT License.