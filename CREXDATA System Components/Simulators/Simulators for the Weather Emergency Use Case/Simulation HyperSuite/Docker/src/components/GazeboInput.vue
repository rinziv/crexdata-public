<template>
	<div class="simulator-input" :class="{ disabled: !isEnabled }">
		<div class="toggle-switch">
			<label class="switch">
				<input type="checkbox" v-model="isEnabled" />
				<span class="slider"></span>
			</label>
			<span>{{ isEnabled ? "Activated" : "Deactivated" }}</span>
		</div>
		<img src="@/assets/gazebo_logo.svg" alt="Gazebo Logo" class="simulator-logo" />
		<div class="input-fields">
			<h3>Gazebo Input</h3>
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
			<h4>Wind Conditions</h4>
			<label>
				<input type="checkbox" v-model="useWindConditionsFromArgos" />
				Use wind conditions from current forecast
			</label>
			<div v-if="!useWindConditionsFromArgos">
				<div class="field-group">
					<label>Wind speed:</label>
					<input type="number" v-model="windSpeed" class="no-spinner" />
					<span>m/s</span>
				</div>
				<div class="field-group">
					<label>Wind direction:</label>
					<input type="number" v-model="windDirection" class="no-spinner" />
					<span>°</span>
				</div>
			</div>

			<h4>Simulation Configuration</h4>
			<div class="field-group">
				<label>Simulation Speed:</label>
				<span>1</span>
				<input type="range" v-model.number="simulationSpeed" min="1" max="10" step="1" style="flex: 1; margin: 0 8px;"  />
				<span>10</span>
			</div>
			<div class="slider-value" >
				{{ simulationSpeed }}x
			</div>
			<h4>Drone Configuration</h4>
			<div class="field-group">
				<label>Drone model:</label>
				<select v-model="droneModel">
					<option value="iris_quadrotor">Iris quadrotor</option>
				</select>
			</div>
			<div class="field-group">
				<label>Flight speed:</label>
				<input type="number" v-model="flightSpeed" class="no-spinner" />
				<span>m/s</span>
			</div>
			<h5>Start Point of Drone Mission</h5>
			<div class="field-group">
				<label>Latitude:</label>
				<input type="number" v-model="startLat" class="no-spinner" placeholder=14.24842357 />
				<div class="info-container">
					<button class="info-button">i</button>
					<div class="info-textbox">
						Add decimal value for latitude (Example: 14.24842357)
					</div>
				</div>
			</div>
			<div class="field-group">
				<label>Longitude:</label>
				<input type="number" v-model="startLong" class="no-spinner" placeholder=26.5726896 />
				<div class="info-container">
					<button class="info-button">i</button>
					<div class="info-textbox">
						Add decimal value for longitude (Example: 26.5726896)
					</div>
				</div>
			</div>

			<div class="field-group">
				<label>Number of Routes:</label>
				<input type="number" v-model="numberOfRoutes" class="no-spinner" />
				<div class="info-container">
					<button class="info-button">i</button>
					<div class="info-textbox">
						The number n defines how many routes are simulated. From all possible alternatives the n shortest routes are chosen.
					</div>
				</div>
			</div>

			<h5>Target Points 
			<div class="info-container">
				<button class="info-button">i</button>
				<div class="info-textbox" style="font-weight: normal;">
					Add decimal value for latitude and longitude (Example: 26.5726896)
				</div>
			</div>
			</h5>
			<div v-for="(point, index) in targetPoints" :key="index" class="field-group target-point-group">
				<label>{{ index + 1 }}.</label>
				<input type="number" v-model="point.lat" placeholder="Latitude" class="no-spinner" />
				<input type="number" v-model="point.long" placeholder="Longitude" class="no-spinner" />
				<input type="number" v-model="point.alt" placeholder="Altitude" class="no-spinner" />
				<button @click="removeTargetPoint(index)">-</button> <!-- Button zum Entfernen -->
			</div>
			<button @click="addTargetPoint">+</button>
			<h5>End Point of Drone Mission
			<div class="info-container">
				<button class="info-button">i</button>
				<div class="info-textbox" style="font-weight: normal;">
					Add decimal value for latitude and longitude (Example: 26.5726896)
				</div>
			</div>
			</h5>
			<label>
				<input type="checkbox" v-model="endMatchesStart" />
				The end point corresponds to the start point.
			</label>
			<div v-if="!endMatchesStart">
				<div class="field-group">
					<label>Latitude:</label>
					<input type="text" v-model="endLat" />
				</div>
				<div class="field-group">
					<label>Longitude:</label>
					<input type="text" v-model="endLong" />
				</div>
			</div>
		</div>
	</div>
</template>

<script>
	export default {
	name: 'GazeboInput',
	data() {
	return {
	isEnabled: false,
	simulationSpeed: 1,
	useDefaultMapSection: false,
	latitude: '',
	longitude: '',
	directionVectorX: '',
	directionVectorY: '',
	useWindConditionsFromArgos: false,
	windSpeed: '',
	windDirection: '',
	droneModel: 'iris_quadrotor',
	flightSpeed: '10',
	numberOfRoutes: '1',
	startLat: '',
	startLong: '',
	startAlt: 30,
	targetPoints: [
	{ lat: '', long: '', alt: '' },
	{ lat: '', long: '', alt: '' },
	{ lat: '', long: '', alt: '' }
	],
	endMatchesStart: true,
	endLat: '',
	endLong: '',
	endAlt: 30
	};
	},
	watch: {
	isEnabled() {
	this.$emit('input', this.getInputData());
	},
	useDefaultMapSection(newVal) {
	if (newVal) {
	this.latitude = '51.73201686690967';
	this.longitude = '8.734821254449205';
	this.directionVectorX = '300.0';
	this.directionVectorY = '500.0';
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
	useWindConditionsFromArgos() { this.$emit('input', this.getInputData()); },
	simulationSpeed() { this.$emit('input', this.getInputData()); },
	windSpeed() { this.$emit('input', this.getInputData()); },
	windDirection() { this.$emit('input', this.getInputData()); },
	flightSpeed() { this.$emit('input', this.getInputData()); },
	numberOfRoutes() { this.$emit('input', this.getInputData()); },
	startLat() { this.$emit('input', this.getInputData()); },
	startLong() { this.$emit('input', this.getInputData()); },
	targetPoints() { this.$emit('input', this.getInputData()); },
	endMatchesStart() { this.$emit('input', this.getInputData()); },
	endLat() { this.$emit('input', this.getInputData()); },
	endLong() { this.$emit('input', this.getInputData()); },
	},
	methods: {
	getInputData() {
	// set endpoints depending on tick box
    const endLatVal = this.endMatchesStart ? this.startLat : this.endLat;
    const endLongVal = this.endMatchesStart ? this.startLong : this.endLong;
	
	return {
	isEnabled: this.isEnabled,
	useDefaultMapSection: this.useDefaultMapSection,
	latitude: this.latitude,
	longitude: this.longitude,
	directionVectorX: this.directionVectorX,
	directionVectorY: this.directionVectorY,
	useWindConditionsFromArgos: this.useWindConditionsFromArgos,
	simulationSpeed: this.simulationSpeed,
	windSpeed: this.windSpeed,
	windDirection: this.windDirection,
	droneModel: this.droneModel,
	flightSpeed: this.flightSpeed,
	numberOfRoutes: this.numberOfRoutes,
	startLat: this.startLat,
	startLong: this.startLong,
	startAlt: this.startAlt,
	targetPoints: this.targetPoints,
	endMatchesStart: this.endMatchesStart,
	endLat: endLatVal,
    endLong: endLongVal,
	endAlt: this.endAlt
	};
	},
	addTargetPoint() {
	this.targetPoints.push({ lat: '', long: '', alt: '' });
	},
	removeTargetPoint(index) {
	this.targetPoints.splice(index, 1);
	}
	}
	};
</script>

<style scoped="">
	.simulator-input {
	position: relative;
	background-color: #ffe4b5;
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
	text-align: center;   /* center below the slider */
	margin-left: 100px;
	margin-top: -18px;  
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
	top: 30px; /* Adjust this to control where the box appears */
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
	top: -25px;
	right: -25px;
	height: 100px;
	opacity: 1;
	}
	h3, h4, h5 {
	margin-bottom: 10px;
	}
	.field-group{
	display: flex;
	align-items: center;
	margin-bottom: 10px;
	}
	.field-group select {
	display: flex;
	align-items: center;
	margin-bottom: 10px;
	}
	.field-group label {
	flex: 0 0 30%;
	font-weight: bold;
	}
	.target-point-group label {
	flex: 0 0 5%; /* Adjusted for narrower labels */
	}
	.target-point-group input {
	width: 50%;
	margin-left: 5px;
	}
	span {
	margin-left: 10px;
	color: #444;
	}
	button {
	background-color: #2196f3;
	color: white;
	border: none;
	border-radius: 4px;
	padding: 5px 10px;
	cursor: pointer;
	margin: 10px 0;
	font-size: 16px;
	font-weight: bold;
	}

	button:hover {
	background-color: #0b7dda;
	}

	.target-point-group button {
	margin-left: 10px;
	padding: 2px 6px;
	background-color: #2196f3;
	color: white;
	border: none;
	border-radius: 4px;
	cursor: pointer;
	font-size: 16px;
	font-weight: bold;
	}

	.target-point-group button:hover {
	background-color: #0b7dda;
	}

</style>
