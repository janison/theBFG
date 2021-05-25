# theBFG Backlog

> This is a rough guideline of the feature pipeline for this project. If you are interested in helping out with QA / Dev please open issues and i will respond :)

I have used `Readme Driven Design` (`RDD`) to build this project.

> ReadMe

- make uploading of versions more accurate, use timestamp of last time to create version number which then allows us to efficently only upload when changed

- make coverage seamless with compete mode
  - either merge results ahead of time? or push into sonar somehow?
  - need a good flow here
  - uploads extract automatically, but are they easy to reference for external tooling? predictable output dir?

- fix issue with compete 20 rapid 8 not working as it could be a common syntax for local dev compete:20 better, hmm, rather not
- tech debt args parsing, centralise and use fluent config like ShouldSave() instead of always args matching

- fix issue with not capturing skipped / ignored tests causing testarena to think more tests are still to be executed or the count to be off

- allow current screen config to be saved and restored so if reset of browser required no effort is lost
  
- support having a picker for the fire mode, which will swap the icon and then fire with compete/rapid or whatever the user has set/saved/startup params
- allow worker to be downloaded from testarena to you can get workers online easier

- allow additional params to be passed through to underling testarena for lift-and-shift support
  
  
    - fix save cmd UI element not active at startup when save arg used
    - fix issue with graphs not respecting saved cfg
    - save these settings?
      - cmd history?

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

- add authentication option, basic shared keys?
