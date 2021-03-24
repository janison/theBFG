angular.module('testLogs', ['ui.bootstrap','ui.utils','ui.router','ngAnimate']);

angular.module('testLogs').config(function($stateProvider) {

    $stateProvider.state('testLogs', {
        url: '/testLogs',
        templateUrl: 'testLogs/partial/list/list.html'
    });
    /* Add New States Above */

});

