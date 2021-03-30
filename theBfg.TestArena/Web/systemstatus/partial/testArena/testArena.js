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
        
        if(msg.testName) {
            $scope.log.unshift(msg);
        }

        if(msg.testName && $scope.log.length > maxLogs) {
            $scope.log.pop();            
        }
       
        //there is a bug here, logs will not be attached to tests and therefor 
        //not be saved in cases where log is disabled from view
        if(msg.logMessage && $scope.log[0]) {                        
            $scope.log[0].logMessage = msg.logMessage != '-' ? msg.logMessage : "";            
            return;
        };

        if($scope.log[msg.id]) {
            $scope.log[msg.id].status = "In progress";
            return;
        } 
        
        if(msg.result && msg.duration) {

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



            $scope.testGlance.slow = $scope.testOutcomes.length;              
            $scope.testGlance.total++;

            $scope.testSummary.push(msg);
            $scope.tests.push(msg);          
            $scope.addToTopicIfFilterActive(msg);

            return;
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

        if(msg.dll) {

            var test = $scope.testRuns.filter(t => t.dll == msg.dll)[0];

            msg.startedAt = new Date();
            msg.info = `In progress ${msg.dll}`;
            if(test) {//hack to make new tests come under same umbrella                
                test.id = msg.id;
                test.result = undefined;
                test.completedAt = undefined;
            }
            else {
                msg.results = [];
                $scope.testRuns.push(msg);
            }
        }

        if(msg.inResponseTo) {
            var found = false;            
            $scope.testRuns.forEach(t => {
                if(t.id === msg.inResponseTo) {                    
                    t.result = msg.result === "0" ? "Passed" : "Failed";
                    t.completedAt = new Date();

                    t.results.push({
                        result: t.result,
                        duration: new Date(t.completedAt.getTime() - t.startedAt.getTime()).getTime() / 1000
                    });

                    t.info = `${t.result ? "Pass" : "Fail"} ${t.dll} in ${toPrettyTime(new Date(t.completedAt.getTime() - t.startedAt.getTime()))}`;
                    found = true;
                }
            });            
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
