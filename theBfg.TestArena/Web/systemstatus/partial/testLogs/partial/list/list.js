angular.module('testLogs').controller('ListCtrl', function($scope, testLogsService) {
    $scope.testLogs = testLogsService.getTestLogs({ systemName: 'all', top: 100 });
});
