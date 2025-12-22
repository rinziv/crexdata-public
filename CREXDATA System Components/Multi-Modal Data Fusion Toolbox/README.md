# mmdf-toolbox
Multimodal data ingestion/fusion, developing algorithms and techniques incorporated in operators composing a data ingestion and fusion toolbox for bringing together and aligning data of multiple modalities and formats.


# Core concepts and  workflow:

  - *workflow-generation*
    - In RM studio the end-user provides a information required to design the fusion workflow .
      - E.g a serie of streaming join operators that join pairwise kafka topics.
    - The RM generated the data fusion specification file in JSON format and starts  the MMDF toolbox service
      - The specification file declares:
        - A list of sources (kafka topics) 
        - A list of tranformations
        - A list or pairwise relations. 
  - *runtime-environment*        
    -  The MMDF is an service that consumes the MMDF JSON specification file and starts the necessary processes
    -  All sources are accessible from the service
    -  Intermediate results are written either on a in-memory state object (or they are published on an intermediate topic)

  - *methodology*
    - The data fusion toolkit focuses on spatio-temporal is built upon apache kafka-streams and it focuses on spatio-temporal joins for streamming data making the following assumptions:
      - All data sources have already spatial and temporal metadata available
        - e.g If a data source is a video stream , text or anything else the mmdf toolbox will use the metadata (spatial,temporal) to fuse information. 
      - The latest message from each topic for each identifier  can wait in state for a maximum number of seconds (*join_temporal_window_secs*) and if spatial conditions (*join_spatial_resolution*) are met between two records , then the records are concatenated and send to the output.
      - As soon as newer message arrives it replaces the old one in state and updates its timestamp
      - As soon as (*join_temporal_window_secs*) expires for a  message, it is discarded from state unless stated explicitly otherwise from design.
  
    


# Code Availability
There are two repositories consist the mmdf toolbox. 
 -  Core service code that can be run as a stand-alone java application: [https://github.com/ITSLab-UAegean/mmdf-kstreams](https://github.com/ITSLab-UAegean/mmdf-kstreams)
    -  Requires a JSON configuration file as input to run as a standalone app
 -  The RapidMiner installable extension repository that requires a fat-jar lib file from the previous repo : [https://github.com/ITSLab-UAegean/mmdf-rm-extension](https://github.com/ITSLab-UAegean/mmdf-rm-extension)
    -  Defines required custom rapidminer operators to design a workflow, convert it into the configuration file and initiate the execution of the service in AI studio.
      
