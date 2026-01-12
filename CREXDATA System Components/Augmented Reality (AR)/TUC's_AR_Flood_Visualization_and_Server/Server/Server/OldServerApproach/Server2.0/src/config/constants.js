// This file defines constants used throughout the application, such as error messages and status codes.

const ERROR_MESSAGES = {
    CLIENT_NOT_FOUND: "Client not found",
    PAIRING_NOT_FOUND: "Pairing not found",
    INVALID_REQUEST: "Invalid request",
    SERVER_ERROR: "Internal server error",
};

const STATUS_CODES = {
    SUCCESS: 200,
    CREATED: 201,
    NOT_FOUND: 404,
    BAD_REQUEST: 400,
    INTERNAL_SERVER_ERROR: 500,
};

module.exports = {
    ERROR_MESSAGES,
    STATUS_CODES,
};