angular.module('systemstatus').controller('testArenaCtrl', function ($rootScope, $scope, testArenaApi, rx, moment) {

    $scope.msSince = function (startedDate) {
        var start = moment(startedDate);
        var now = moment(new Date());

        var diff =  moment.utc(now.diff(start)).format("HH:mm:ss.SSS");
        return diff;
    }

    
    $scope.publish = function(destination, cmd) {
        testArenaApi.sendCommand(destination, cmd);
    };

    
    $scope.cmdReload = function() {
        testArenaApi.sendCommand("", "Reload");
    }

    $scope.cmdShowTopic = function(result) {
        $scope.filter = result;

        if(result == "Passed" || result == "Failed") {
            $scope.currentTopic = $scope.tests.filter(t => t.result === result);
        }
        else if(result == 'slow') {
            $scope.currentTopic = $scope.testOutcomes;
        }        
        else if(result == 'all') {
            $scope.currentTopic = $scope.tests;            
        }
        else if(result == 'log' || result == 'flakey') {            
            if($scope.currentTopic.length === 0) {
                $scope.logDisabled = !$scope.logDisabled;
            }                      

            $scope.currentTopic = [];                        
            $scope.filter = '';
        }
    }

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
