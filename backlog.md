# theBFG Backlog

> This is a rough guideline of the feature pipeline for this project. If you are interested in helping out with QA / Dev please open issues and i will respond :)

I have used `Readme Driven Design` (`RDD`) to build this project.

> ReadMe


- fix lookup of vstest, use registry to find valid install locations
 
- add authentication option, basic shared keys?
  
- allow worker to be downloaded from testarena to you can get workers online easier
      
  - need to fix issue with discovering tests not associate with a dll. tests are associated witha unitTestId, lookup testRuns the unitTestId and return the dll its associated with
  
    - fix save cmd UI element not active at startup when save arg used
    - fix issue with graphs not respecting saved cfg
    - save these settings?
      - cmd history?

- allow way to use servicecmd to launch a worker in a new processes

- allow graph to zoom or adjust for overall unit tests so it zooms at the right level
  - or could just use a setting and a level adjuster thingy to allow easy zooming?, mouse wheel?

- auto-scaling of workers on node to allow dynamic sizing based on CPU/MEM usage of test scenario
             - indicate active work on each worker?

 - spawning test environments
   - faciliate being able to script different test environment scenarios such that you can target tests at partciular environments and allow the cluster to be configured with workers of various tag types so you can model complex integration test scenarios 
     - ie. spawn a load test on a website where you have a pool of workers and you can designate some are `users` `customers` `admins` and be able to model different scenarios to really push the boundaries of these test types
   - `thebfg ... and spawn {environment}`
    - Spawn in: docker
      - push standard images to docker hub to allow easy spinning up
    - Spawn in: DevTestLabs
      - thebfg can managed cross platform via integration with AzurePowershell
      - push images to devtestlabs to allow each spinning up
    - Spawn in: Cloud Functions
      - thebfg can managed cross platform via integration with AzurePowershell