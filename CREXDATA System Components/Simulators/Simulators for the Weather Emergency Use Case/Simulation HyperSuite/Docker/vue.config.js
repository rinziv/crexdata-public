const { defineConfig } = require('@vue/cli-service')
module.exports = defineConfig({
  transpileDependencies: true,
  publicPath: '/upb-webapp/',
  devServer: {
    host: '0.0.0.0',
    port: 5173,
    allowedHosts: 'all',
    hot: false,
    liveReload: false,
    webSocketServer: false, // Add this line to disable WebSocket
    // client: {
    //   webSocketURL: 'auto://0.0.0.0:0/ws', 
    // },
    proxy: {
      '/webapi': {
        target: 'https://server.crexdata.eu',
        changeOrigin: true,
        secure: false,
        logLevel: 'debug',
      },
    },
  },
})
