angular.module('systemstatus').controller('testArenaCtrl', function ($rootScope, $scope, testArenaApi, rx, moment) {
    
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

    resetResults = function() {
        $scope.log = [];
        $scope.tests = [];
        $scope.currentTopic = [];
        $scope.testSummary = [];
        $scope.testOutcomes = [];
        $scope.testRuns = [];
        $scope.showTestRunDetailed = false;
        
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
        if(msg.logMessage) {                        
            var existinLog = $scope.log.filter(w => w.unitTestId === msg.unitTestId);
            var existinTest = $scope.tests.filter(w => w.unitTestId === msg.unitTestId);

            if(existinLog) {
                existinLog[0].logMessage = msg.logMessage;            
            }

            if(existinTest) {
                existinTest[0].logMessage = msg.logMessage;            
            }
        };

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
        
        if(msg.result && msg.testName) {

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

        if(msg.dll && !msg.hasOwnProperty("passed")) {

            var test = $scope.testRuns.filter(t => t.dll == msg.dll)[0];

            msg.startedAt = new Date();
            msg.info = `In progress ${msg.dll}`;
            if(test) {//hack to make new tests come under same umbrella                                
                test.completedAt = undefined;
            }
            else {
                msg.results = [];
                msg.assets = [];
                $scope.testRuns.push(msg);
            }
        }

        if(msg.inResponseTo && msg.hasOwnProperty("passed")) {

            var test = $scope.testRuns.filter(t => t.dll == msg.dll)[0];

           
            if(!test) {                                        
                msg.results = [];
                msg.assets = [];                
                $scope.testRuns.push(msg);
                test = msg;
            }
            
            test.completedAt = new Date();

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


    var sub = testArenaApi.updates.subscribe(function (testEvent) {

        $scope.$apply(function () {
            var progress = angular.fromJson(testEvent);
            //console.log(progress);

            updateTestArenaWith(progress, 100);            
        });
    });
    
    
});
