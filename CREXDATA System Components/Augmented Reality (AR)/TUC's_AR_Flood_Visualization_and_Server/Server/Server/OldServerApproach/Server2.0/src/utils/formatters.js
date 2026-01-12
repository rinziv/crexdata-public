// This file exports functions for formatting data, such as converting timestamps and other data types.

function formatTimestamp(timestamp) {
    const date = new Date(timestamp);
    return date.toISOString();
}

function formatClientData(client) {
    return {
        id: client.id,
        type: client.type,
        connectedSince: formatTimestamp(client.connectionTime),
        ip: client.ws.ipAddress || "unknown",
        pairedWith: client.pairedClientId || null,
    };
}

function formatPairingData(pairing) {
    return {
        unityClient: {
            id: pairing.unityClient.id,
            ip: pairing.unityClient.ip,
        },
        phoneClient: {
            id: pairing.phoneClient.id,
            ip: pairing.phoneClient.ip,
        },
        established: formatTimestamp(pairing.established),
    };
}

export { formatTimestamp, formatClientData, formatPairingData };