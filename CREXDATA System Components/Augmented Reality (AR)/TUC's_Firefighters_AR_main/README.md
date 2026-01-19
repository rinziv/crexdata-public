# AR System for Wildfire Emergency Response

## Overview

This project is an Augmented Reality (AR) application developed for the Microsoft HoloLens 2 to assist firefighting team leaders during real-time wildfire operations. Built using Unity and the Mixed Reality Toolkit 3 (MRTK3), the system enhances situational awareness by overlaying critical data, such as team positions, active fire locations, fire spread simulations, and weather conditions, directly into the user's field of view.

## Features

- **Real-Time 3D Mapping:** Interactive 3D map powered by Mapbox that visualizes the operational area, including satellite and street views.
- **Team Coordination:** Real-time visualization of team members' locations on both the map and as AR overlays, synchronized via a Node.js server and WebSocket communication.
- **Navigation & Routing:** Optimal pathfinding to team members or Points of Interest (POIs) using Mapbox Directions API, featuring AR-guided directional arrows.
- **Fire Monitoring:** Active fire detection and the SPARK tool for real-time fire spread simulation.
- **Weather Visualization:** Live weather data (temperature, wind speed/direction) from OpenMeteo API, including 3D wind direction arrows in the AR environment.
- **Points of Interest (POIs):** Identification of critical locations such as water sources, power lines, and danger zones.
- **Dynamic UI:** Hand-following menus and gaze-based interactions designed for high-stress environments.

## System Architecture

The system consists of three main components:
1.  **HoloLens 2 Application:** The main AR interface for the team leader.
2.  **Mobile GPS Provider App:** An Android application used by team members to stream their real-time GPS coordinates.
3.  **Backend Server:** A Node.js server that handles WebSocket communication and data synchronization between the mobile apps, external APIs, and the HoloLens.

## Installation

### Prerequisites
* Unity (2021.3 or later recommended)
* Mixed Reality Toolkit 3 (MRTK3)
* Node.js (for the server)
* Microsoft HoloLens 2 device
* Android Smartphone (for GPS testing)

### Steps

1. Clone the repository: git clone https://github.com/kostasCH20/FireEmergency_AR_System.git
2. Open the project in Unity.
3. Ensure you have the MRTK and necessary packages installed.
4. Build and deploy to HoloLens 2.

## Usage

1.  **Start the Server:** Ensure the Node.js server is running and accessible.
2.  **Launch the Apps:** Open the application on HoloLens 2 and the GPS Provider app on the Android phone.
3.  **Pairing & Calibration:**
    * **GPS Setup:** Enter the pairing ID on the mobile app to connect to the HoloLens session.
    * **Compass Calibration:** Follow the on-screen prompts to look North (guided by the mobile app's compass) and click "Calibrate" to align the AR world with the real world.
4.  **Menu Interaction:** Raise your palm to access the Hand Menu. From here you can:
    * Toggle **Map Visibility**.
    * View **Weather Data** and enable wind arrows.
    * Show/Hide **Active Fires** and **Fire Spread** simulations.
    * Select **Team Members** or **POIs** to initiate navigation.
5.  **Navigation:** Select a destination on the map or a team member; follow the 3D arrows projected in your view to reach the target.

## Tech Stack

* **Engine:** Unity
* **Hardware:** Microsoft HoloLens 2
* **Toolkit:** MRTK3
* **Mapping:** Mapbox SDK
* **Data Sources:** NASA FIRMS, OpenMeteo, SPARK Fire Simulation
* **Backend:** Node.js, WebSockets, Kafka

## Contributions

Contributions are welcome! Please fork the repository and submit a pull request for any changes.
