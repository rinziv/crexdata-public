<template>
	<div class="weather-data">
		<div class="historical-weather">
			<label>User Name:</label>
			<input type="Text" v-model="localUserName" @change="$emit('update:userName', localUserName)" />
		</div>
		<div class="historical-weather">
			<label>Date:</label>
			<input type="date" v-model="localWeatherDate" @change="$emit('update:weatherDate', localWeatherDate)" />
		</div>
		<div class="historical-weather">
			<label>Time:</label>
			<input type="time" v-model="localWeatherTime" @change="$emit('update:weatherTime', localWeatherTime)" />
		</div>
		<div class="historical-weather">
			<label>Request Name:</label>
			<input type="Text" v-model="localRequestName" @change="$emit('update:requestName', localRequestName)" />
			<div class="info-container">
				<button class="info-button">i</button>
				<div class="info-textbox">
					Add a name to identify your simulation request
				</div>
				</div> 
		</div>

	</div>
</template>

<script>
	export default {
		props: {
			userName: String,
			weatherDate: String,
			weatherTime: String,
			requestName: String
		},
		emits: ["update:userName", "update:weatherDate", "update:weatherTime", "update:requestName"],
		computed: {
			localUserName: {
				get() {
					return this.userName;
				},
				set(value) {
					this.$emit("update:userName", value);
				}
			},
			localWeatherDate: {
				get() {
					return this.weatherDate;
				},
				set(value) {
					this.$emit("update:weatherDate", value);
				}
			},
			localWeatherTime: {
				get() {
					return this.weatherTime;
				},
				set(value) {
					this.$emit("update:weatherTime", value);
				}
			},
			localRequestName: {
				get() {
					return this.requestName;
				},
				set(value) {
					this.$emit("update:requestName", value);
				}
			},
		},
		mounted() {
            if (!this.weatherDate) {
                const today = new Date().toISOString().split("T")[0];
                this.$emit("update:weatherDate", today);
            }
            if (!this.weatherTime) {
                const now = new Date().toTimeString().slice(0, 5); // HH:MM
                this.$emit("update:weatherTime", now);
            }
        }
	};
</script>

<style scoped="">
	.weather-data {
	background-color: lightgrey;
	border: 1px solid #ccc;
	padding: 16px;
	border-radius: 8px;
	}

	h2 {
	text-align: left;
	margin-bottom: 10px;
	}

	h3 {
	margin-top: 10px;
	margin-bottom: 10px;
	text-align: left;
	}

	.weather-option {
	display: flex;
	align-items: center;
	margin-bottom: 10px;
	}

	.historical-weather {
	display: flex;
	align-items: left;
	margin-bottom: 10px;
	}

	.info-container {
	position: relative;
	display: inline-block;
	}
	.info-button {
	width: 24px;
	height: 24px;
	margin-top: 7px;
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

	.checkbox-label {
	white-space: nowrap;
	margin-right: 5px;
	display: flex;
	align-items: left;
	}

	.checkbox-label input {
	margin-right: 10px;
	}

	label {
	font-weight: bold;
	margin-right: 10px;
	display: flex;
	align-items: center;
	}

	input,
	select {
	width: 50%;
	padding: 5px;
	margin-top: 5px;
	}
</style>
