<template>
	<div class="simulator-inputs">
		<div class="simulator-grid">
			<DeepwaiveInput @input="updateSimulatorData('deepwaive', $event)" />
			<SparkInput @input="updateSimulatorData('spark', $event)" />
			<GazeboInput @input="updateSimulatorData('gazebo', $event)" />
		</div>
		<button class="create-button" @click="createConfigurationFiles">Create Configuration Files</button>
	</div>
</template>

<script>
import DeepwaiveInput from './DeepwaiveInput.vue';
import SparkInput from './SparkInput.vue';
import GazeboInput from './GazeboInput.vue';

const ENDPOINTS = {
  gazebo: '/webapi/DEFAULT/api/v1/services/shs/gazebo/?name_of_dataset=jsonBodyGazeboData',
  spark: '/webapi/spark/api/v1/...', // insert SPARK endpoint
  deepwaive: '/webapi/DEFAULT/api/v1/services/shs/floodwaive/?name_of_dataset=jsonBodyFloodwaiveData',
};

export default {
  name: 'SimulatorInputs',
  components: {
    DeepwaiveInput,
    SparkInput,
    GazeboInput,
  },
  props: {
    weatherData: Object,
  },
  data() {
    return {
      simulatorData: {
        deepwaive: {},
        spark: {},
        gazebo: {},
      },
    };
  },
  methods: {
    updateSimulatorData(simulatorName, data) {
      this.simulatorData = {
        ...this.simulatorData,
        [simulatorName]: {
          ...this.simulatorData[simulatorName],
          ...data,
        },
      };
    },

    flattenGazeboData(data) {
      const flat = {
        ...(this.weatherData || {}),
        //isEnabled: data.isEnabled,
        //useDefaultMapSection: data.useDefaultMapSection,
        //latitude: data.latitude,
        //longitude: data.longitude,
        //directionVectorX: data.directionVectorX,
        //directionVectorY: data.directionVectorY,
        //useWindConditionsFromArgos: data.useWindConditionsFromArgos,
        simulationSpeed: data.simulationSpeed,
        windSpeed: data.windSpeed,
        windDirection: data.windDirection,
        //droneModel: data.droneModel,
        //flightSpeed: data.flightSpeed,
        numberOfRoutes: data.numberOfRoutes,
        startLat: data.startLat,
        startLong: data.startLong,
        startAlt: data.startAlt,
        //endMatchesStart: data.endMatchesStart,
        endLat: data.endLat,
        endLong: data.endLong,
        endAlt: data.endAlt
      };
      if (Array.isArray(data.targetPoints)) {
        data.targetPoints.forEach((point, index) => {
          const idx = index + 1;
          flat[`targetPoint${idx}Lat`] = point.lat;
          flat[`targetPoint${idx}Long`] = point.long;
          flat[`targetPoint${idx}Alt`] = point.alt;
        });
      }
      return flat;
    },

    flattenSparkData(data) {
      return {
        ...(this.weatherData || {}),
        isEnabled: data.isEnabled,
        // useDefaultMapSection: data.useDefaultMapSection,
        latitude: data.latitude,
        longitude: data.longitude,
        directionVectorX: data.directionVectorX,
        directionVectorY: data.directionVectorY,
        // useWeatherDataFromArgos: data.useWeatherDataFromArgos,
        temperature: data.temperature,
        humidity: data.humidity,
        simulatedTimePeriod: data.simulatedTimePeriod,
        resolution: data.resolution,
        radiusOfFireSpot: data.radiusOfFireSpot,
        startLat: data.startLat,
        startLong: data.startLong,
        classification: data.classification,
      };
    },

    flattenDeepwaiveData(data) {
      const flat = {
        ...(this.weatherData || {}),
        isTrusted: data.isTrusted,
        _vts: data._vts,
        isEnabled: data.isEnabled,
        // useDefaultMapSection: data.useDefaultMapSection,
        // useDefaultMeasures: data.useDefaultMeasures,
        latitude: data.coordinates?.latitude,
        longitude: data.coordinates?.longitude,
        directionVectorX: data.coordinates?.direction_vector_x,
        directionVectorY: data.coordinates?.direction_vector_y,
        precipitation: data.simulatorParameters?.precipitation,
        infiltration: data.simulatorParameters?.infiltration,
        landUse: data.simulatorParameters?.land_use,
        sourceTerm: data.simulatorParameters?.source_term,
        rainfallArea: data.rainfallArea,
        rainfallEventName: data.rainfallEventName,
        dtmResolution: data.dtmResolution,
        rainfallDuration: data.rainfallDuration,
        rainfallIntensity: data.rainfallIntensity,
      };

      if (Array.isArray(data.targetPoints) && data.targetPoints.length > 0) {
        data.targetPoints.forEach((point, index) => {
          const idx = index + 1;
          flat[`targetPoint${idx}rainfallDuration`] = point.rainfallDuration;
          flat[`targetPoint${idx}rainfallIntensity`] = point.rainfallIntensity;
          flat[`targetPoint${idx + 1}rainfallDuration`] = 3600;
          flat[`targetPoint${idx + 1}rainfallIntensity`] = 0;
        });
      } else {
        flat[`targetPoint1rainfallDuration`] = 3600;
        flat[`targetPoint1rainfallIntensity`] = 0;
      }

      return flat;
    },

    async createConfigurationFiles() {
      for (const simulator of Object.keys(this.simulatorData)) {
        if (this.simulatorData[simulator].isEnabled) {
          let config;

          if (simulator === 'gazebo') {
            config = this.flattenGazeboData(this.simulatorData[simulator]);
            console.log(`Downloading ${simulator}_config.json:`, config);
            // this.downloadJSON(config, `${simulator}_config.json`);

          } else if (simulator === 'spark') {
            //config = this.flattenSparkData(this.simulatorData[simulator]);
            console.log(`Downloading ${simulator}_config.json:`, config);
            continue;
            // this.downloadJSON(config, `${simulator}_config.json`);

          } else if (simulator === 'deepwaive') {
            config = this.flattenDeepwaiveData(this.simulatorData[simulator]);
            console.log(`Downloading ${simulator}_config.json:`, config);
            // this.downloadJSON(config, `${simulator}_config.json`);

          } else {
            config = {
              ...(this.weatherData || {}),
              ...this.simulatorData[simulator],
            };
            console.log(`Downloading ${simulator}_config.json:`, config);
            // this.downloadJSON(config, `${simulator}_config.json`);
          }

          const url = ENDPOINTS[simulator];
          try {
            const response = await fetch(url, {
              method: 'POST',
              headers: {
                'Content-Type': 'application/json',
              },
              body: JSON.stringify({ data: [config] }),
            });

            if (!response.ok) {
              // error handling
              const errorText = await response.text();
              console.error('Fehler bei POST-Anfrage:', errorText);
              alert('Fehler beim Senden der Daten an den Server.');
            } else {
              const result = await response.json();
              console.log('POST-Anfrage erfolgreich:', result);
              alert('Daten erfolgreich an den Server gesendet.');
            }
          } catch (error) {
            console.error('Netzwerkfehler:', error);
            alert('Netzwerkfehler beim Senden der Daten.');
          }
        }
      }
    },

    downloadJSON(data, filename) {
      const jsonString = JSON.stringify(data, null, 2);
      const blob = new Blob([jsonString], { type: 'application/json' });
      const url = URL.createObjectURL(blob);
      const a = document.createElement('a');
      a.href = url;
      a.download = filename;
      document.body.appendChild(a);
      a.click();
      document.body.removeChild(a);
      URL.revokeObjectURL(url);
    },
  },
};
</script>



<style scoped="">
	.simulator-inputs {
	display: flex;
	flex-direction: column;
	gap: 20px;
	}
	.simulator-grid {
	display: flex;
	justify-content: space-between;
	gap: 20px;
	}
	.create-button {
	width: 100%;
	padding: 15px;
	background-color: #4CAF50;
	color: white;
	border: none;
	border-radius: 4px;
	cursor: pointer;
	font-size: 18px;
	font-weight: bold;
	}
	.create-button:hover {
	background-color: #45a049;
	}
</style>
