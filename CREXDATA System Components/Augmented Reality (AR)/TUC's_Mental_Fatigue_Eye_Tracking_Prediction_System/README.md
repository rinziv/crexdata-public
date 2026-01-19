# AR Application for Emergency Response

## Overview

This project is an Augmented Reality (AR) application designed to assist **first responders in stressful field situations** by providing **real-time eye tracking mental fatigue monitoring**. The application is developed using Unity and the Mixed Reality Toolkit (MRTK3) for the Microsoft HoloLens 2 device. It visualizes the responder’s **mental state in AR** and supports **team monitoring** based on different eye patterns.

The device is built around a **Raspberry Pi 4 Model B**. Video is captured with a **Raspberry Pi Camera Module V3 NoIR Wide**, connected through a short flex extender for flexible placement. Illumination is provided by a single **940 nm IR LED** with a **150° emission angle**. To reject visible light while transmitting the LED output, an **850 nm IR long-pass filter** is mounted in front of the lens (cut to size). A **120 Ω resistor** is used for the LED circuit.

This is a real-time mental fatigue monitoring system for first responders and is for team monitoring based on different eye patterns. It can send every second the mental state. We have 4 different states:
- **1** is the low 🟩 (green)
- **2** is the mild 🟨 (yellow)
- **3** is the moderate 🟧 (orange)
- **4** is the severe 🟥 (red)

## Features

- **Real-time eye tracking mental fatigue monitoring:** Every second sends results for every user. Can extract eye tracking data alongside with mental fatigue state.
- **ui display of mental state on the AR environment through hololens 2**
- **User-Friendly Interface:** Easy-to-use interface designed for emergency responders.

## Installation

1. Clone the repository: `git clone https://github.com/astavroulakis/mentalFatigue_CREXDATA.git`
2. In this repository you will find a unity poject (to be uploaded). Open the project in Unity.
3. Ensure you have the MRTK and necessary packages installed.
4. Build and deploy to HoloLens 2.
5. Ensure you have a **Server PC** and a **Raspberry Pi** that is compatible with **Raspberry Pi Camera Module V3 NoIR Wide** (tested with **Raspberry Pi 4 Model B**).
6. The **Node.js server** and the **Kafka server** have to be actived. More information you can find in `CREXDATA System Components/Augmented Reality (AR)/TUC's_AR_Flood_Visualization_and_Server/Server`
7. Find the **IP address** of the **Server PC**.
8. Download/clone the GitHub folder on the **Server PC**.
9. On the **Server PC**, run the main server script: `server_client/serverSocket.py`
10. On the **Raspberry Pi**, download and run the client script: `client.py` (for better functionality we suggest the script to run as a service).
11. In order to run the server properly, some paths has to change according to the Server PC that the GitHub folder was downloaded.

The paths that needs to be changed are:

1. `serverSocket.py`  
   Line 23: Model path (relative path - should work if file is in same directory)

2. `dfTransferer.py`  
   Line 44: KNN model directory  
   `model_dir = "C:/Users/AlexisPC/Desktop/saved_models/knn"`

3. `dfTransferer.py`  
   Line 45: Update Kafka bootstrap server (replace localhost with the IP of the Kafka CREXDATA server):  
   Change:
   bootstrap_servers="localhost:9092" to
   bootstrap_servers="<KAFKA_SERVER_IP>:9092" (will be given)


## Usage

1. Start the application on **HoloLens 2** (with the add-on installed).
2. On the **Server PC**, ensure the **Kafka server** and the **Node.js server** are running.
3. On the **Server PC**, start the Python server: `server_client/serverSocket.py`
4. On the **Raspberry Pi**, start `client.py` (recommended to run as a service).
5. The system will send the mental fatigue state **every second** (states 1–4) and display the result in the AR environment.

## Contributions

Contributions are welcome! Please fork the repository and submit a pull request for any changes.
