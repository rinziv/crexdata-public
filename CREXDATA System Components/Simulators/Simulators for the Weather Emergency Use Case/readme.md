# Emergency Case Simulator HyperSuite
The Weather Emergencies Use Case Simulator HyperSuite (EmCaseSHS) is designed as a set of interoperable and extendable operators for data processing workflows in RapidMiner AI Studio, and extended by a specific web application to enable easy configuration access for end users through a widget started from ARGOS.  The EmCaseSHS enables the use of real-time data from the field, from current forecasts, or injected by the Data Injector in simulations. This allows different scenarios to be simulated, results to be compared, and evaluated by decision-makers. The modular design of the EmCaseSHS allows for flexible expansion with additional simulators.

## Functional principle
In the Weather Emergencies Use Case, flood simulation, robotic simulation, and wildfire simulation are used. The workflows include data retrieval and pre-processing, simulator configuration, triggering of simulators, simulation results post-processing, and streaming for visualization. The process is depicted in the following picture.

![EmCase SHS activity diagramm](https://github.com/altairengineering/crexdata-private/blob/da6f18934cb1293da99c4b1d4eda94890966f640/CREXDATA%20System%20Components/Simulators/Simulators%20for%20the%20Weather%20Emergency%20Use%20Case/EmCase%20SHS%20activity%20diagram.svg)

## Workflow overview
### Data Injector
Social Media Posts
<p align="left">
  <img src="https://github.com/user-attachments/assets/351134b3-3544-48b2-8a4b-d8d81b091842"
       alt="image"
       width="500" />
</p>

Biometric Data
<p align="left">
  <img src="https://github.com/user-attachments/assets/e9850888-ba1f-4177-8d99-3e8886ba4a80"
       alt="image"
       width="500" />
</p>

### FloodWaive flood simulation
Creating a flood simulation with rainfall data and measures from ARGOS
<p align="left">
  <img src="https://github.com/user-attachments/assets/44d1ec29-3763-48cc-b19c-bbf8d6a04804"
       alt="image"
       width="500" />
</p>

Configure and start the barrier optimizer
<p align="left">
  <img src="https://github.com/user-attachments/assets/4e080fc4-2069-44e8-80ab-bb78402f3032"
       alt="image"
       width="500" />
</p>

Start flood simulations and extract the maximum water level 
<p align="left">
  <img src="https://github.com/user-attachments/assets/29b82d83-944d-48d1-9d82-790fd5e54b14"
       alt="image"
       width="500" />
</p>

### Gazebo
Parameterization and simulation execution
<p align="left">
  <img src="https://github.com/user-attachments/assets/10b378ad-775a-40af-bd90-b5d187288623"
       alt="image"
       width="500" />
</p>

Simulation results post-processing
<p align="left">
  <img src="https://github.com/user-attachments/assets/a9a440d2-c234-4304-90f9-3a6f3d35e80a"
       alt="image"
       width="500" />
</p>

Retrieval of simulation results
<p align="left">
  <img src="https://github.com/user-attachments/assets/3330e7ac-92a8-4ba5-b707-e2589b50325b"
       alt="image"
       width="500" />
</p>

## License
All Weather Emergencies Use Case software components are licensed under Apache License 2.0.

All Weather Emergencies Use Case scientific documentation and methodological descriptions are licensed under CC BY 4.0.




