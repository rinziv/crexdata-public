<template>
	<div class="simulator-input" :class="{ disabled: !isEnabled }">
		<div class="toggle-switch">
			<label class="switch">
				<input type="checkbox" v-model="isEnabled" @change="emitInputData" />
				<span class="slider"></span>
			</label>
			<span>{{ isEnabled ? "Activated" : "Deactivated" }}</span>
		</div>
		<img src="@/assets/floodwaive_logo.webp" alt="Floodwaive Logo" class="simulator-logo" />
		<div class="input-fields">
			<h3>Deepwaive Input</h3>
			<h4>Simulation Area</h4>
			<div class="field-group">
				<label>Rainfall Area:</label>
				<select v-model="rainfallArea" @change="emitInputData">
					<option value="Dortmund">Dortmund</option>
					<option value="Innsbruck">Innsbruck</option>
				</select>
			</div>
			<h4>Rainfall Event</h4>
			<div class="field-group">
				<label>Rainfall Event Name:</label>
				<input type="text" v-model="rainfallEventName" class="no-spinner" @change="emitInputData" />
				<div class="info-container">
					<button class="info-button">i</button>
					<div class="info-textbox">
						Add a name for rainfall event identification 
					</div>
				</div>
			</div>
			<div class="field-group">
				<label>DTM resolution:</label>
				<select v-model="dtmResolution" @change="emitInputData">
					<option value="1 m x 1 m">1 m x 1 m</option>
					<option value="2 m x 2 m">2 m x 2 m</option>
					<option value="4 m x 4 m">4 m x 4 m</option>
					<option value="8 m x 8 m">8 m x 8 m</option>
				</select>
			</div>
			<div class="field-group">
				<label>Rainfall Duration:</label>
				<span>600</span>
				<input type="range" v-model.number="rainfallDuration" min="600" max="43200" step="300" style="flex: 1; margin: 0 8px;"  />
				<span>43200</span>
			</div>
			<div class="slider-value">
				{{ rainfallDuration }} s
			</div>
			<div class="field-group">
				<label>Rainfall Intensity:</label>
				<span>0</span>
				<input type="range" v-model.number="rainfallIntensity" min="0" max="100" step="1" style="flex: 1; margin: 0 8px;"  />
				<span>100</span>
			</div>
			<div class="slider-value">
				{{ rainfallIntensity }} mm/h
			</div>
		
		<div v-for="(point, index) in targetPoints" :key="index" class="outer-block target-point-group">
				<div>
				<label>{{ index + 1 }}.</label>
				<button @click="removeTargetPoint(index)">-</button>
				</div>
				<div class="inner-block field-group">
					<label>Rainfall Duration:</label>
					<span>600</span>
					<input type="range" v-model.number="point.rainfallDuration" min=600 max=43200 step=300 style="flex: 1; margin: 0 8px;" />
					<span>43200</span>
				</div>
				<div class="slider-value">
				{{ point.rainfallDuration }} s
			</div>
				<div class="inner-block field-group">
					<label>Rainfall Intensity:</label>
					<span>0</span>
					<input type="range" v-model.number="point.rainfallIntensity" min="0" max="100" step="1" style="flex: 1; margin: 0 8px;"  />
					<span>100</span>
				</div>
				<div class="slider-value">
				{{ point.rainfallIntensity }} mm/h
			</div>
			</div>
		<button @click="addTargetPoint">+</button>
		<div class="field-group">
			A break of 1 hour with 0 mm/h at the end will be added to the simulation by default
		</div>
		</div>
	</div>


</template>

<script>
export default {
  name: 'DeepwaiveInput',
  data() {
    return {
      isEnabled: false,
      useDefaultMapSection: false,
      useDefaultMeasures: false,
      coordinateFields: [
        { label: 'Latitude', value: '', text: 'Add decimal value for latitude (Example: 14.24842357)', placeholder: 14.24842357 },
        { label: 'Longitude', value: '', text: 'Add decimal value for longitude (Example: 26.5726896)', placeholder: 26.5726896 }
      ],
      simulatorParameters: [    
      ],
      dtmResolution: '4 m x 4 m',
      rainfallArea: 'Dortmund',
      rainfallEventName: '',
      rainfallDuration: 3600,
      rainfallIntensity: 10,
      targetPoints: [
      ]
    };
  },
  watch: {
    coordinateFields: {
      handler() {
        this.emitInputData();
      },
      deep: true
    },
    simulatorParameters: {
      handler() {
        this.emitInputData();
      },
      deep: true
    },
    dtmResolution() {
      this.emitInputData();
    },
	rainfallEventName() {
      this.emitInputData();
	},
	rainfallArea() {
      this.emitInputData();
    },
	rainfallDuration() {
      this.emitInputData();
	},
	rainfallIntensity() {
      this.emitInputData();
	},
	targetPoints() { 
	this.$emit('input', this.getInputData()); 
	}
  },
  methods: {
    emitInputData() {
      this.$emit('input', {
        isEnabled: this.isEnabled,
        useDefaultMapSection: this.useDefaultMapSection,
		targetPoints: this.targetPoints,
		useDefaultMeasures: this.useDefaultMeasures,
        coordinates: this.coordinateFields.reduce((acc, field) => {
          acc[field.label.toLowerCase().replace(/\s+/g, '_')] = field.value;
          return acc;
        }, {}),
        simulatorParameters: this.simulatorParameters.reduce((acc, param) => {
          acc[param.label.toLowerCase().replace(/\s+/g, '_')] = param.value;
          return acc;
        }, {}),
        dtmResolution: this.dtmResolution,
		rainfallEventName: this.rainfallEventName,
        rainfallArea: this.rainfallArea,
		rainfallDuration: this.rainfallDuration,
		rainfallIntensity: this.rainfallIntensity
      });
    },
    handleMapSectionChange() {
      if (this.useDefaultMapSection) {
        this.coordinateFields[0].value = '52.5200'; // Berlin
        this.coordinateFields[1].value = '13.4050'; // Berlin
        //this.coordinateFields[2].value = '0.0';
        //this.coordinateFields[3].value = '1.0';
      } else {
        this.coordinateFields.forEach(field => (field.value = ''));
      }
      if(this.useDefaultMeasures) {
		// Assign Default Values
      } else {
		// Read Values
      }
      this.emitInputData();
    },
	addTargetPoint() {
	this.targetPoints.push({ rainfallDuration: 3600, rainfallIntensity: 10});

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
	background-color: #e0f7fa;
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
	top: 10px;
	right: 10px;
	height: 40px;
	opacity: 1;
	}
	h3, h4 {
	margin-bottom: 10px;
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
	span {
	margin-left: 10px;
	color: #444;
	}
	input[type="text"],
	input[type="number"],
	select {
	flex: 1; 
	padding: 5px;
	margin-left: 10px;
	}
	.field-row {
	display: flex;
    align-items: center;
    gap: 8px;
    margin-bottom: 8px;
    }
    
    .field-row .slider {
    flex: 1;
    }
	.outer-block {
    #border: 1px solid #ccc;
    padding: 12px;
    border-radius: 6px;
    margin-bottom: 16px;
    }

    .inner-block {
    margin-bottom: 10px;
    display: flex;
    align-items: center;
    gap: 10px;
    }

</style>
