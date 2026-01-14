<template>
  <div id="app">
    <SimulatorHyperSuite />
  </div>
</template>

<script>
import SimulatorHyperSuite from "./views/SimulatorHyperSuite.vue";

export default {
  name: "App",
  components: {
    SimulatorHyperSuite,
  },
};
</script>

<style>
	#app {
	text-align: center;
	margin-top: 50px;
	}
	body {
	font-family: 'Arial', sans-serif;
	}
</style>
