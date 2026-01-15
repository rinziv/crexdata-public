# Gazebo

Gazebo is used in the CREXDATA system to simulate routes for Unmanned Aerial Vehicles (UAVs) under dynamic environmental conditions and, based on this, to evaluate flight routes and necessary resources.

## Structure
The Gazebo repository contains example outputs from UAV simulations and a Docker setup including [Gazebo 11](https://gazebosim.org/home) and the [Robot Operating System](https://www.ros.org/) version noetic. The drones and world files from the [GitHub PX4-Autopilot Repository](https://github.com/PX4/PX4-Autopilot.git) are used. The drone model and environmental model can be replaced. Installation instructions to use the Docker container are described below.
The scripts folder contains Python scripts used within the Docker container to control the drone as well as a bash script which can be called to automatically start the simulation including the simulator setup. In addition, it contains further Python scripts used in pre- and post-processing workflows, which can be found in the Workflows folder. The overall drone simulation process is split into three RapidMiner AI Studio workflows. In the pre-processing workflow, parameters for simulation setup are retrieved, configuration files are updated, and the simulation is triggered on a Virtual Machine by calling the bash script. Simulation results are transferred from the Virtual Machine to RapidMiner AI Studio using the "03 - Refresh Gazebo Data" workflow. Finally, the results are post-processed, prepared for visualization and decision support, and published to a Kafka topic.

## IPO diagram
<p align="center">
  <img src="https://github.com/user-attachments/assets/643a2e8f-46c4-4a73-a772-f1d1113b3382"
       alt="image"
       width="600" />
</p>

## Datasets
The datasets folder contains example outputs of the drone simulation in csv format. These include the outputs from the final trials in Dortmund and Innsbruck. In Dortmund, a drone simulation was used to plan a flight over a flooded area, in which specific points of interest and selected posts from [Text Mining]([CREXDATA System Components/Text Analytics](https://github.com/altairengineering/crexdata-public/tree/be28e1b10445a0b670e6d1a069cf1a21bbaded9b/CREXDATA%20System%20Components/Text%20Analytics)) had to be checked. In Innsbruck, the drone flight was planned to check points of interest around a wildfire area. The outputs include all route alternatives simulated for a specific mission. For all alternative routes, timestamps and battery levels were stored when reaching a target point. 

## Installation and setup
Download everything inside the [Docker_setup](https://github.com/altairengineering/crexdata-private/tree/master/WP2/use-cases/weather-emergency/Gazebo/Docker_setup) folder. In a terminal, navigate to the folder and run the following command to build the Docker image.
```bash
docker build -t ros_gazebo_px4 .
```
Next, build the Docker compose services.
```bash
docker compose build
```
Then, start the services.
```bash
docker compose up -d
```
The container should be running now. Use the following command to enter the container.
```bash
docker exec -it --user root px4_debug /bin/bash
```
Navigate to the scripts folder within the automatic_launch package.
```bash
cd catkin_ws/src/automatic_launch/scripts
```
You can now run the following command which will call the bash script and run the simulation.
```bash
./start_simulation_multiple_runs.sh
```
Parameters can be set in the config folder catkin_ws/src/automatic_launch/config. This includes a json file with routes and the simulation speed. 

## License
Software components are licensed under Apache License 2.0.

Scientific documentation and methodological descriptions are licensed under CC BY 4.0.


