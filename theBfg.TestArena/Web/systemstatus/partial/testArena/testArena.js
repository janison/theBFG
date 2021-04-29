angular.module('systemstatus').controller('testArenaCtrl', function ($rootScope, $scope, testArenaApi, $document, moment) {
    
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

    var startedTestArenaAt = new Date();
    
    $scope.publish = function(destination, cmd) {        
        testArenaApi.sendCommand(destination, cmd);
    };

    
    $scope.cmdReload = function() {
        testArenaApi.sendCommand("", "Reload");
    }
    
    $scope.cmdLightsOutToggle = function() {        
        if(!$scope.lightsOut) {
            $scope.lightsOut = false;
        }
        
        $scope.lightsOut = !$scope.lightsOut;

        if($scope.lightsOut) {
            angular.element($document[0].querySelector("html")).css("background-color", "black");
            angular.element($document[0].querySelector("html")).css("-webkit-filter", "invert(100%)");
            angular.element($document[0].querySelector("html")).css("-moz-filter", "invert(100%)");
            angular.element($document[0].querySelector("html")).css("-o-filter", "invert(100%)");
            angular.element($document[0].querySelector("html")).css("-ms-filter", "invert(100%)");            
        }
        else {            
            angular.element($document[0].querySelector("html")).css("-webkit-filter", "invert(0%)");
            angular.element($document[0].querySelector("html")).css("-moz-filter", "invert(0%)");
            angular.element($document[0].querySelector("html")).css("-o-filter", "invert(0%)");
            angular.element($document[0].querySelector("html")).css("-ms-filter", "invert(0%)");
        }
    }

    $scope.cmdShowTopic = function(result) {
        $scope.filter = result;

        if (result == "Passed" || result == "Failed") {
            $scope.currentTopic = $scope.tests.filter(t => t.result === result);
        } else if (result == 'slow') {
            $scope.currentTopic = $scope.testOutcomes;
        } else if (result == 'all') {
            $scope.currentTopic = $scope.tests;
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

    // resets the test arena modal, ready for few data
    resetResults = function() {
        $scope.log = [];
        $scope.tests = [];
        $scope.currentTopic = [];
        $scope.testSummary = [];
        $scope.testOutcomes = [];
        $scope.testRuns = [];
        $scope.showTestRunDetailed = false;
        $scope.testsQueued = 0;
        $scope.sendCmdDisabled = true;
        $scope.testArenaInfo = {
            isConnected: false
        };

        
        $scope.workerInfo = [{

            category: 'blue',
            values: []
        },

        {
            category: 'grey',
            values: []
        }
        ];

        $scope.testGlance = {
            total: 0,
            errors: 0,
            flakey: 0,
            slow: 0,
            startedAt: new Date()
        }
    }

    testArenaApi.isConnected.subscribe(function(isonline) {
        $scope.testArenaInfo.isConnected = isonline;
    });

    resetResults();
    
    var updateTestArenaWith = function(msg, maxLogs) {

        // if(!msg.at) {
        //     console.log("saw unknown message "+ JSON.stringify(msg));
        // } else {
        //     msg.at = new Date(msg.at)
        // };

        // if(msg.at > startedTestArenaAt) {

        // }
        console.log("1AppInfo: " +JSON.stringify(msg));
        
        if(msg.testName) {
            $scope.log.unshift(msg);
        }

        if(msg.testName && $scope.log.length > maxLogs) {
            $scope.log.pop();            
        }

        // if(msg.tests) {
        //     if(!$scope.log[msg.testId]) {
        //         $scope.log[msg.testId] = { };
        //     }

        //     $scope.log[msg.testId].status = "Waiting";
        // }
       
        //there is a bug here, logs will not be attached to tests and therefor 
        //not be saved in cases where log is disabled from view

        // handler: updates test with correct log message when TestAssetEvent
        if(msg.logMessage) {                        
            var existinLog = $scope.log.filter(w => w.unitTestId === msg.unitTestId);
            var existinTest = $scope.tests.filter(w => w.unitTestId === msg.unitTestId);

            if(existinLog.length > 0) {
                existinLog[0].logMessage = msg.logMessage;            
            }

            if(existinTest.length > 0) {
                existinTest[0].logMessage = msg.logMessage;             
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
            
    
            if(parseInt(msg.duration) > 100) {
                
                if($scope.testOutcomes.length > maxLogs) {
                    $scope.testSummary.shift();
                }    
                
                $scope.testOutcomes.push(msg); 
            }

            
            if(msg.result == "Failed") {
                $scope.testGlance.errors++;
            }

            msg.info = `${msg.result} in ${msg.duration}`;


            $scope.testGlance.slow = $scope.testOutcomes.length;              
            $scope.testGlance.total++;

            $scope.testSummary.push(msg);
            $scope.tests.push(msg);          
            $scope.addToTopicIfFilterActive(msg);            
        }

        if(msg.RunThisTest) {
            
            var test = $scope.testRuns.filter(t => t.dll == msg.RunThisTest)[0];   
            
            test.testIds.push(msg.Id);
        }

        if(msg.memUsage) {

            if($scope.workerInfo[0].values.length > maxLogs) {
                $scope.workerInfo[0].values.shift();            
            }

            if($scope.workerInfo[1].values.length > maxLogs) {
                $scope.workerInfo[1].values.shift();            
            }

            $scope.workerInfo[0].values.push(msg.cpuUsage);                     
            $scope.workerInfo[1].values.push((parseInt(msg.memUsage) / (16 * 1000)) * 100);                     

            return;
        }

        //handler: displays the todo tests TestDiscoveredEvent
        if(msg.dll && msg.hasOwnProperty("discoveredTests"))
        {
            var test = $scope.testRuns.filter(t => t.dll == msg.dll)[0];                        

            if(test) {//hack to make new tests come under same umbrella                                
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

            $scope.testsQueued += msg.discoveredTests.length;
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
    
    
});
