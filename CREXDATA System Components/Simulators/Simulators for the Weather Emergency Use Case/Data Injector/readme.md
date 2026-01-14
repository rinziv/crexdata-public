# Data Injector

The Data Injector contains Altair RapidMiner AI Studio workflows and datasets to stream robotic data, biometric data, or social media posts. Stored datasets are retrieved and streamed into respective Kafka topics with the original or an updated timestamp. This allows system tests, using the system for training purposes and injecting "what-if" scenarios without current streaming inputs from the field. The Data Injector is expandable due to its modular design. Existing workflows can be used and linked to additional data sources for streaming.

## Functional principle
Workflows in the EmCase SHS are designed to process data from the field, e.g., sensor streams from sewer network sensors, biometric devices or drone data, or data from other services, such as social media posts and weather forecasts. The Data Injector allows entering the data into the workflows even without real data sources. For example, system tests, demonstrations, and user training are usually conducted outside real emergency situations. Even in an incident there is the explicit use case to run “what-if” scenarios in a way that past situations are considered to happen similarly again. What if the forecasted rain event would evolve like a similar event in 2016 did? Thus, the Data Injector enables the execution of the workflows based on stored data. To do this, the data is first retrieved from the corresponding source. There are then two modes of data injection. In the first mode, the data is injected at the same frequency and with the same timestamps as it was originally recorded. The second mode allows adjusting the frequency at which the data is injected and updating the timestamp of datapoints. The timestamp is increased based on a defined start time in accordance with the set injection frequency.
<p align="center">
  <img src="https://github.com/user-attachments/assets/f2d79005-2373-4dbf-9af4-aaff579893a7"
       alt="image"
       width="600" />
</p>

## Datasets
The repository contains synthetic biometric data and synthetic social media posts. These datasets were generated to test the Data Injector and the processing technologies. The synthetic posts include typical social media posts composed of everyday content and special content in the event of a flood or wildfire. The metadata includes geo-positions in the area of the Emergency Case pilot sites. The biometric data contains all parameters used by the [eXplainable AI](CREXDATA System Components/Explainable AI (XAI)) model for fatigue assessment. For each parameter, the values were set within the range measured during real firefighting exercises.

## License 
Software components are licensed under Apache License 2.0.

Scientific documentation and methodological descriptions are licensed under CC BY 4.0.

