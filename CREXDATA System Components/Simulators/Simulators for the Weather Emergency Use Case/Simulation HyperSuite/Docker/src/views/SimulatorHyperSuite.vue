<template>
	<div class="simulator-hyper-suite">
		<AppHeader />
		<WeatherData
			v-model:userName="weather.userName"
			v-model:weatherDate="weather.weatherDate"
			v-model:weatherTime="weather.weatherTime"
			v-model:requestName="weather.requestName"
		/>
		<SimulatorInputs :weatherData="weather" />
	</div>
</template>

<script>
	import AppHeader from '@/components/AppHeader.vue';
	import WeatherData from "@/components/WeatherData.vue";
	import SimulatorInputs from "@/components/SimulatorInputs.vue";

	export default {
	name: 'SimulatorHyperSuite',
	components: {
	AppHeader,
	WeatherData,
	SimulatorInputs
	},
	data() {
	return {
	weather: {
	userName: "",
	weatherDate: "",
	weatherTime: "",
	requestName: ""
	}
	};
	}
	};
</script>

<style scoped="">
	.simulator-hyper-suite {
	display: flex;
	flex-direction: column;
	align-items: left;
	gap: 20px;
	padding: 20px;
	}
</style>