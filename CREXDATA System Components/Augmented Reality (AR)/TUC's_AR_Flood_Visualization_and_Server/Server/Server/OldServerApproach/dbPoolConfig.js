const mysql = require('mysql2/promise');

const pool = mysql.createPool({
    host: 'localhost',
    user: 'root',
    password: 'root', // Replace with your MySQL password
    database: 'crexdata', // Replace with your database name
    waitForConnections: true,
    connectionLimit: 10,
    queueLimit: 0
});

module.exports = pool;