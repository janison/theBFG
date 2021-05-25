angular.module('systemstatus').controller('testArenaCtrl', function ($rootScope, $scope, testArenaApi, $document, $interval, $timeout) {
    
    function pad(number) {
        if (number < 10) {
          return '0' + number;
        }
        return number;
      }

    var toPrettyTime = function(time) {
        return ""+ pad(time.getUTCHours()) +
        ':' + pad(time.getUTCMinutes()) +
        ':' + pad(time.getUTCSeconds()) +
        '.' + (time.getUTCMilliseconds() / 1000).toFixed(3).slice(2, 5);
    }

    $scope.focusedOn = undefined;
    $scope.publish = function(destination, cmd) {  
        
        if(cmd.startsWith('FocusOn')) {
            $scope.focusedOn = cmd.split(" ")[1]
        }

        if(cmd.startsWith('StopFocusing')) {
            $scope.focusedOn = undefined;
        }
        
        testArenaApi.sendCommand(destination, cmd);
    };

    
    $scope.cmdReload = function() {
        testArenaApi.sendCommand("", "Reload");
    }

    $scope.setLightsOut = function() {
        if($scope.testArenaCfg.isLightsOut) {
            angular.element($document[0].querySelector("html")).addClass("dark-theme");
        }
        else {                  
            angular.element($document[0].querySelector("html")).removeClass("dark-theme");
            
        }
    }

    
    
    $scope.cmdLightsOutToggle = function() {        
        if(!$scope.testArenaCfg.isLightsOut) {
            $scope.testArenaCfg.isLightsOut = false;
        }
        
        $scope.testArenaCfg.isLightsOut = !$scope.testArenaCfg.isLightsOut;

        $scope.setLightsOut();
    }

    $scope.cmdShowTopic = function(result) {
        $scope.filter = result;

        if (result == "Passed" || result == "Failed") {
            $scope.currentTopic = $scope.tests.filter(t => t.result === result);
        } else if (result == 'slow') {
            $scope.currentTopic = $scope.slowTests;
        } else if (result == 'all') {
            $scope.currentTopic = $scope.tests;    
        } else if (result.indexOf(".") >= 0) {
            let dll = getDllFromFullName(result);
                $scope.currentTopic = $scope.tests.filter(t => getDllFromTest(t.testId) === dll);
        } else if (result == 'log' || result == 'flakey') {
            if ($scope.currentTopic.length === 0) {
                $scope.logDisabled = !$scope.logDisabled;
            }

            $scope.currentTopic = [];
            $scope.filter = '';
        } else if (result == 'testRunsDetails') {
            $scope.showTestRunDetailed = !$scope.showTestRunDetailed;
        }
    };

    $scope.addToTopicIfFilterActive = function(msg) {
        if($scope.filter === '' || !msg.result) return;

        if(msg.result == "Passed" && $scope.filter === msg.result ) {
            $scope.currentTopic.push(msg);
        }
        else if(msg.result == "Failed" && $scope.filter === msg.result ) {
            $scope.currentTopic.push(msg);
        }
        else if($scope.filter === "slow" && msg.duration && parseInt(msg.duration) > 100) {
            $scope.currentTopic.push(msg);
        }        
        else if($scope.filter == 'all') {
            $scope.currentTopic.push(msg);            
        }
        else if($scope.filter == 'log' || $scope.filter == 'flakey') {
            $scope.currentTopic = [];
        }
    }

    $scope.cmdClear = function() {
        resetResults();
    }

    
        //animations
    $scope.fire = function() {            
        $scope.firedTimes++;

        if($scope.firedTimes > 1)
        {
            return;
        }

        $scope.stop = $interval(() => {
            $scope.shouldFire = !$scope.shouldFire
        }, 800);
    }

    $scope.stopFiring = function() {
        $scope.firedTimes--;

        if($scope.firedTimes > 0)
        {
            return;
        }
        $interval.cancel($scope.stop);
        $scope.shouldFire = false;
    }

    $scope.cmdSaveTestArenaCfg = function() {
        testArenaApi.updateCfg($scope.testArenaCfg);
    }

    // resets the test arena modal, ready for few data
    resetResults = function() {

        $scope.showCfg = false;
        $scope.showFullLog = false;
        $scope.showLogStream = function() {
            $scope.showFullLog = !$scope.showFullLog;
        };
        $scope.stop = 0;
        $scope.shouldFire = false;
        $scope.firedTimes = 0
        $scope.log = [];
        $scope.tests = [];
        $scope.currentTopic = [];
        $scope.testSummary = [];
        $scope.slowTests = [];
        $scope.testRuns = [];
        $scope.showTestRunDetailed = false;
        $scope.testsQueued = 0;
        $scope.sendCmdDisabled = true;
        $scope.testArenaInfo = {
            isConnected: false
        };        
        $scope.workerInfo = {};

        $scope.testGlance = {
            total: 0,
            errors: 0,
            flakey: 0,
            slow: 0,
            startedAt: new Date()
        }
    };

    //only set on startup, not reset
    $scope.testArenaCfg = {
        slowTestMs: 2000,
        testDurationMax: 1000 * 5,
        isLightsOut: false,
        alwaysSave: false             
    };

    testArenaApi.isConnected.subscribe(function(isonline) {
        $scope.testArenaInfo.isConnected = isonline;
    });

    resetResults();

    var getDllFromTest = function(testId) {
        var dll = $scope.testRuns.filter(w => w.results.filter(e => e.testId === testId).length > 0)[0];
        return dll ? getDllFromFullName(dll.dll) : testId;
    }

    

    function getDllFromFullName(fullname) {
        let tokens = fullname.split('/');
        return tokens[tokens.length -1]
    }

    function pushIfNotExist(list, msg, test) {
        var existinTest = list.filter(w => w.testName === test && getDllFromFullName(w.dll) == getDllFromFullName(msg.dll));

        if(!existinTest[0]) {
            list.push({ testName: test, dll: msg.dll, isNew: true });
        }
    }
    
    var updateTestArenaWith = function(msg, maxLogs) {      
        console.log("Saw Event: " +JSON.stringify(msg));

        if(msg.hasOwnProperty("testDurationMax"))
        {
            $scope.testArenaCfg = msg;
            $scope.setLightsOut();
        }
        
        if(msg.testName) {
            $scope.log.unshift(msg);
        }

        if(msg.testName && $scope.log.length > maxLogs) {
            $scope.log.pop();            
        }

        if(msg.tests) {
            var existingRun = $scope.testRuns.filter(w => w.results.filter(a => a.testId === msg.testId))
              
            if(existingRun[0]) {
                existingRun[0].workerId = msg.workerId;
                existingRun[0].worker = msg.worker;
            }
        }
       
        //there is a bug here, logs will not be attached to tests and therefor 
        //not be saved in cases where log is disabled from view

        // handler: updates test with correct log message when TestAssetEvent
        if(msg.logMessage) {                        
            var existinLog = $scope.log.filter(w => w.unitTestId === msg.unitTestId);
            var existinTest = $scope.tests.filter(w => w.unitTestId === msg.unitTestId);

            if(existinLog.length > 0) {
                if(!existinLog[0].logMessage) {
                    existinLog[0].logMessage = "";
                }
                existinLog[0].logMessage += msg.logMessage;            
                existinLog[0].logMessage += "\r\n";            

            }

            if(existinTest.length > 0) {
                if(!existinTest[0].logMessage) {
                    existinTest[0].logMessage = "";
                }
                existinTest[0].logMessage += msg.logMessage;             
                existinTest[0].logMessage += "\r\n"; 
            }
        };

        // handler: adds the log file to the test results topic
        if(msg.logUrl) {

            var existingRun = !msg.unitTestId ? $scope.testRuns.filter(w => w.results.filter(a => a.testId === msg.testId))
            : $scope.tests.filter(a => a.unitTestId === msg.unitTestId);            

            if(existingRun[0]) {
                if(!existingRun[0].assets) {
                    existingRun[0].assets = [];
                }
                existingRun[0].assets.push(msg.logUrl);            
            }
        }

        // if($scope.log[msg.testId]) {
        //     $scope.log[msg.testId].status = "In progress";
        // } 
        
        //handler: testsuite completed with a overall result
        if(msg.result && msg.testName) {
            $scope.testsQueued--;

            

            var test = $scope.testRuns.filter(w => {
                return w.testIds && w.testIds.filter(ww => ww == msg.testId ) !== null;
            });

            if(test[0]) {
                msg.dll = test[0].dll;                
            }

            if($scope.testSummary.length > maxLogs) {
                $scope.testSummary.shift();
            }
            
    
            if(parseInt(msg.duration) > testArenaApi.slowTestMs) {
                
                if($scope.slowTests.length > maxLogs) {
                    $scope.testSummary.shift();
                }    
                
                $scope.slowTests.push(msg);                 
            }

            
            if(msg.result == "Failed") {
                $scope.testGlance.errors++;
            }

            msg.info = `${msg.result} in ${msg.duration}`;

            $scope.testGlance.slow = $scope.slowTests.length;              
            $scope.testGlance.total++;

            $scope.testSummary.push(msg);

            
            
            var testDll = getDllFromTest(msg.testId);
            var existinTest = $scope.tests.filter(w => w.testName === msg.testName && getDllFromTest(w.testId) == testDll)[0]; //dll is not in test
            
            if(!existinTest) {
                $scope.tests.push(msg);                                          
                $scope.addToTopicIfFilterActive(msg); 
            }
            else {
                
                Object.keys(msg).forEach(k => {
                    if(!existinTest[k]) {
                        existinTest[k] = msg[k];
                    }
                })
            }
  

            $scope.stopFiring();
        }

        //handler: test summary
        if(msg.RunThisTest) {
            
            var test = $scope.testRuns.filter(t => t.dll == msg.RunThisTest)[0];   
            
            if(!test.testIds) {
                test.testIds = [];
            }

            test.testIds.push(msg.unitTestId);
        }

        //handler: worker info graphs

        if(msg.$type.indexOf('theBFG.TestArenaWorkerHeadbeat') > -1) {
            var host = msg.host;

            if(!host) {
                return;
            }


            if(!$scope.workerInfo[host]) {
                $scope.workerInfo[host] = {}
            }

            $scope.workerInfo[host].ipAddress = msg.ipAddress;
            $scope.workerInfo[host].name = msg.name;
            $scope.workerInfo[host].computerName = msg.computerName;
            $scope.workerInfo[host].userName     = msg.userName;
            $scope.workerInfo[host].workers = msg.workers;
            $scope.workerInfo[host].route = msg.route;
            $scope.workerInfo[host].tags = msg.tags;
        }

        if(msg.$type.indexOf('Rxns.Health.AppResourceInfo') > -1) {


            var host = msg.host;

            if(!$scope.workerInfo[host]) {

                console.log('Found worker: '+ msg.host + ' : '+ msg.name);
                $scope.workerInfo[host] = {
                    name: msg.name,
                    host: host
                };
            }

            if(!$scope.workerInfo[host].resources) {
                $scope.workerInfo[host].resources = [{
                    category: 'blue',
                    values: []
                },
                {
                    category: 'grey',
                    values: []
                }]
            }

            if($scope.workerInfo[host].resources[0].values.length > maxLogs) {
                $scope.workerInfo[host].resources[0].values.shift();            
            }

            if($scope.workerInfo[host].resources[0].values.length > maxLogs) {
                $scope.workerInfo[host].resources[0].values.shift();            
            }

            $scope.workerInfo[host].resources[0].values.push(msg.cpuUsage);                     
            $scope.workerInfo[host].resources[1].values.push((parseInt(msg.memUsage) / (16 * 1000)) * 100);                     

            return;
        }

        //handler: displays the todo tests TestDiscoveredEvent
        if(msg.dll && msg.hasOwnProperty("discoveredTests"))
        {
            var test = $scope.testRuns.filter(t => t.dll == msg.dll)[0];                        

            if(test) {//hack to make new tests come under same umbrella                                
                if(!test.testIds) {
                    test.testIds = [];
                }
                                
                test.testIds.push(msg.testId);
                test.total = msg.discoveredTests.length
                
            }
            else {
                var dll =  {
                    testIds: [msg.Id],
                    dll: msg.dll,
                    info: "Not Run",
                    results: [],
                    assets: [],
                    failed: 0,
                    passed: 0,
                    total: msg.discoveredTests.length
                };

                $scope.testRuns.push(dll);       
            }

            //show new tests as discovered
            msg.discoveredTests.forEach(t => pushIfNotExist($scope.tests, msg, t));

            
            $scope.testsQueued = $scope.tests.filter(w => w.isNew).length;
        }

        // handler: indicates a test is about to startunittest, adds test to results section
        else if(msg.dll && !msg.hasOwnProperty("passed")) {

            var test = $scope.testRuns.filter(t => t.dll == msg.dll)[0];            

            if(test) {//hack to make new tests come under same umbrella                                
                test.completedAt = undefined;                
                test.startedAt = new Date();
                test.info = `In progress ${msg.dll}`;
            }
            else {                
                msg.startedAt = new Date();
                msg.info = `In progress ${msg.dll}`;
                msg.results = [];
                msg.assets = [];
                $scope.testRuns.push(msg);
            }

            $scope.fire();

        }

        //handler: result of a unit test has been foound
        if(msg.inResponseTo && msg.hasOwnProperty("passed")) {
            
            var test = $scope.testRuns.filter(t => t.dll == msg.dll)[0];
           
            if(!test) {                                        
                msg.results = [];
                msg.assets = [];                                
                $scope.testRuns.push(msg);
                test = msg;
            }
            
            test.completedAt = new Date();
            test.startedAt = test.startedAt ?? new Date();

            test.results.push({            
                info: `${msg.failed + " / " ?? ""}/${msg.passed + msg.failed} in ${toPrettyTime(new Date(test.completedAt.getTime() - test.startedAt.getTime()))}`,
                failed: msg.failed,
                passed: msg.passed,                        
                testId: msg.inResponseTo,
                result: msg.failed > 0 ? "Failed" : "Passed",
                duration: new Date(test.completedAt.getTime() - test.startedAt.getTime()).getTime() / 1000
            });

            test.info = `${test.result ? "Pass" : "Fail"} ${test.dll} in ${toPrettyTime(new Date(test.completedAt.getTime() - test.startedAt.getTime()))}`;           

            if(msg.failed > 0) {
                $scope.failSFX();
            }
            else {
                $scope.passSFX();
            }
        }

        console.log("AppInfo: " +JSON.stringify(msg));
    };


    //handler: a message is received from the testarena, apply it to our basic one-way flow data model built up off event hanlders    
    var sub = testArenaApi.updates.subscribe(function (testEvent) {

        $scope.$apply(function () {
            var progress = angular.fromJson(testEvent);
            //console.log(progress);

            updateTestArenaWith(progress, 100);            
        });
    });

    //init: sounds
    var passSound = new Audio('sfx/pass.ogg');
    var failSound = new Audio('sfx/fail.ogg');

    $scope.playSound = function(audio) {
        if(!$scope.testArenaCfg.soundsOn) {
            return;
        }
        
        audio.volume = 0.5;
        audio.play();
    }    
    

    $scope.failSFX = function() {
        $scope.playSound(failSound);//.then(s => s());
    }

    $scope.passSFX = function() {
        $scope.playSound(passSound);//.then(s => s());        
    }
});
