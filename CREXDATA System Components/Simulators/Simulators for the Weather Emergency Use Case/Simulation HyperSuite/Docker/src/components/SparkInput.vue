<template>
	<div class="simulator-input" :class="{ disabled: !isEnabled }">
		<div class="toggle-switch">
			<label class="switch">
				<input type="checkbox" v-model="isEnabled" />
				<span class="slider"></span>
			</label>
			<span>{{ isEnabled ? "Activated" : "Deactivated" }}</span>
		</div>
		<img src="@/assets/spark_logo.webp" alt="Spark Logo" class="simulator-logo" />
		<div class="input-fields">
			<h3>Spark Input</h3>
			<!-- Simulation Area -->
			<h4>Simulation Area</h4>
			<div class="field-group">
				<label>Latitude:</label>
				<input type="number" v-model="latitude" :disabled="useDefaultMapSection" class="no-spinner" placeholder=14.24842357 />
				<div class="info-container">
					<button class="info-button">i</button>
					<div class="info-textbox">
						Add decimal value for latitude (Example: 14.24842357)
					</div>
				</div>
			</div>
			<div class="field-group">
				<label>Longitude:</label>
				<input type="number" v-model="longitude" :disabled="useDefaultMapSection" class="no-spinner" placeholder=26.5726896 />
				<div class="info-container">
					<button class="info-button">i</button>
					<div class="info-textbox">
						Add decimal value for longitude (Example: 26.5726896)
					</div>
				</div>
			</div>
			<div class="field-group">
				<label>Direction Vector X:</label>
				<input type="number" v-model="directionVectorX" :disabled="useDefaultMapSection" class="no-spinner" placeholder=23.84 />
			</div>
			<div class="field-group">
				<label>Direction Vector Y:</label>
				<input type="number" v-model="directionVectorY" :disabled="useDefaultMapSection" class="no-spinner" placeholder=98.14 />
			</div>
			<!-- Weather Data -->
			<h4>Weather Data</h4>
			<label>
				<input type="checkbox" v-model="useWeatherDataFromArgos" />
				Use weather data from current forecast
			</label>
			<div v-if="!useWeatherDataFromArgos">
				<div class="field-group">
					<label>Temperature:</label>
					<input type="number" v-model="temperature" class="no-spinner" />
					<span>°C</span>
				</div>
				<div class="field-group">
					<label>Humidity:</label>
					<input type="number" v-model="humidity" class="no-spinner" />
					<span>%</span>
				</div>
			</div>
			<!-- Start Conditions -->
			<h4>Start Conditions</h4>
			<div class="field-group">
				<label>Radius of fire spot:</label>
				<span>10</span>
				<input type="range" v-model.number="radiusOfFireSpot" min="10" max="100" step="5" />
				<span>100</span>
			</div>
			<div class="slider-value">
				{{ radiusOfFireSpot }} m
			</div>
			<div class="field-group">
				<label>Start Latitude:</label>
				<input type="number" v-model="startLat" class="no-spinner" placeholder=14.24842357 />
				<div class="info-container">
					<button class="info-button">i</button>
					<div class="info-textbox">
						Add decimal value for latitude(Example: 14.24842357)
					</div>
				</div>
			</div>
			<div class="field-group">
				<label>Start Longitude:</label>
				<input type="number" v-model="startLong" class="no-spinner" placeholder=26.5726896 />
				<div class="info-container">
					<button class="info-button">i</button>
					<div class="info-textbox">
						Add decimal value for longitude (Example: 26.5726896)
					</div>
				</div>
			</div>
			<!-- Additional Simulation Parameters -->
			<h4>Additional Simulation Parameters</h4>
			<div class="field-group">
				<label>Classification:</label>
				<select v-model="classification">
					<option v-for="n in 12" :key="n" :value="n">{{ n }}</option>
				</select>
			</div>
			<div class="field-group">
				<label>Simulated time period:</label>
				<span>1</span>
				<input type="range" v-model.number="simulatedTimePeriod" min="1" max="10" step="0.5" />
				<span>10</span>
			</div>
			<div class="slider-value">
				{{ simulatedTimePeriod }} h
			</div>
			<div class="field-group">
				<label>Resolution:</label>
				<span>20</span>
				<input type="range" v-model.number="resolution" min="20" max="50" step="2" />
				<span>50</span>
			</div>
			<div class="slider-value" >
				{{ resolution }} m
			</div>
		</div>
	</div>
</template>

<script>
	export default {
	name: 'SparkInput',
	data() {
	return {
	isEnabled: false,
	useDefaultMapSection: false,
	latitude: '',
	longitude: '',
	directionVectorX: '',
	directionVectorY: '',
	useWeatherDataFromArgos: false,
	temperature: '',
	humidity: '',
	simulatedTimePeriod: 5, // Default: 5 h
	resolution: 30, // Default: 30 m
	radiusOfFireSpot: 20, // Default: 20 m
	startLat: '',
	startLong: '',
	classification: 1, // Default: class 1
	};
	},
	watch: {
	isEnabled() {
	this.$emit('input', this.getInputData());
	},
	useDefaultMapSection(newVal) {
	if (newVal) {
	this.latitude = '51.732224561853435';
	this.longitude = '8.734926928441183';
	this.directionVectorX = '1.0';
	this.directionVectorY = '0.0';
	} else {
	this.latitude = '';
	this.longitude = '';
	this.directionVectorX = '';
	this.directionVectorY = '';
	}
	this.$emit('input', this.getInputData());
	},
	latitude() { this.$emit('input', this.getInputData()); },
	longitude() { this.$emit('input', this.getInputData()); },
	directionVectorX() { this.$emit('input', this.getInputData()); },
	directionVectorY() { this.$emit('input', this.getInputData()); },
	useWeatherDataFromArgos() { this.$emit('input', this.getInputData()); },
	temperature() { this.$emit('input', this.getInputData()); },
	humidity() { this.$emit('input', this.getInputData()); },
	radiusOfFireSpot() { this.$emit('input', this.getInputData()); },
	startLat() { this.$emit('input', this.getInputData()); },
	startLong() { this.$emit('input', this.getInputData()); },
	classification() { this.$emit('input', this.getInputData()); },
	simulatedTimePeriod() { this.$emit('input', this.getInputData()); },
	resolution() { this.$emit('input', this.getInputData()); },
	},
	methods: {
	getInputData() {
	return {
	isEnabled: this.isEnabled,
	useDefaultMapSection: this.useDefaultMapSection,
	latitude: this.latitude,
	longitude: this.longitude,
	directionVectorX: this.directionVectorX,
	directionVectorY: this.directionVectorY,
	useWeatherDataFromArgos: this.useWeatherDataFromArgos,
	temperature: this.temperature,
	humidity: this.humidity,
	simulatedTimePeriod: this.simulatedTimePeriod,
	resolution: this.resolution,
	radiusOfFireSpot: this.radiusOfFireSpot,
	startLat: this.startLat,
	startLong: this.startLong,
	classification: this.classification,
	};
	}
	}
	};
</script>

<style scoped="">
	.simulator-input {
	position: relative;
	background-color: #fce4ec;
	padding: 16px;
	border-radius: 8px;
	width: 30%;
	transition: opacity 0.3s ease;
	}
	/* Chrome, Safari, Edge, Opera */
	.no-spinner::-webkit-outer-spin-button,
	.no-spinner::-webkit-inner-spin-button {
	-webkit-appearance: none;
	margin: 0;
	}

	/* Firefox */
	.no-spinner {
	-moz-appearance: textfield;
	}
	.info-container {
	position: relative;
	display: inline-block;
	}
	.slider-value {
	text-align: center;
	margin-left: 100px;
	margin-top: -15px;  
	font-weight: 500;
	color: #333;
	}
	.info-button {
	width: 24px;
	height: 24px;
	margin-left: 6px;
	border-radius: 50%;
	background-color: #f1f1f1;
	border: 1px solid #ccc;
	color: #333;
	font-weight: bold;
	font-size: 14px;
	cursor: pointer;
	text-align: center;
	line-height: 22px;
	padding: 0;
	transition: background-color 0.3s;
	}
	.info-button:hover {
	background-color: #ddd;
	}
	.info-textbox {
	visibility: hidden;
	opacity: 0;
	position: absolute;
	top: 30px;
	left: 50%;
	transform: translateX(-50%);
	width: 200px;
	background-color: #444;
	color: #fff;
	text-align: left;
	padding: 8px;
	border-radius: 4px;
	box-shadow: 0 2px 8px rgba(0,0,0,0.3);
	font-size: 12px;
	transition: opacity 0.3s;
	z-index: 10;
	}
	.info-container:hover .info-textbox {
	visibility: visible;
	opacity: 1;
	}
	.simulator-input.disabled {
	opacity: 0.5;
	}
	.toggle-switch {
	display: flex;
	align-items: center;
	margin-bottom: 10px;
	}
	.switch {
	position: relative;
	display: inline-block;
	width: 40px;
	height: 20px;
	margin-right: 10px;
	}
	.switch input {
	display: none;
	}
	.slider {
	position: absolute;
	cursor: pointer;
	top: 0;
	left: 0;
	right: 0;
	bottom: 0;
	background-color: #ccc;
	transition: 0.4s;
	border-radius: 20px;
	}
	.slider:before {
	position: absolute;
	content: "";
	height: 14px;
	width: 14px;
	left: 3px;
	bottom: 3px;
	background-color: white;
	transition: 0.4s;
	border-radius: 50%;
	}
	input:checked + .slider {
	background-color: #2196f3;
	}
	input:checked + .slider:before {
	transform: translateX(20px);
	}
	.input-fields {
	opacity: 1;
	transition: opacity 0.3s ease;
	}
	.simulator-input.disabled .input-fields {
	opacity: 0.5;
	}
	.simulator-logo {
	position: absolute;
	top: 10px;
	right: 10px;
	height: 40px;
	opacity: 1;
	}
	h3, h4 {
	margin-bottom: 10px;
	}
	.field-group {
	display: flex;
	align-items: center;
	margin-bottom: 10px;
	}
	.field-group label {
	flex: 0 0 30%;
	font-weight: bold;
	}
	.field-group input[type="text"],
	.field-group input[type="number"],
	.field-group select {
	flex: 1;
	padding: 5px;
	margin-left: 10px;
	}
	.field-group input[type="range"] {
	flex: 1;
	margin-left: 10px;
	}
	span {
	margin-left: 10px;
	}
</style>
