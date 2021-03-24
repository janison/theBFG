angular.module('testLogs').factory('testLogsService', function ($resource, rxnPortalConfiguration) {
    
	var baseUrl = rxnPortalConfiguration.baseWebServicesUrl;

	return $resource(baseUrl + '/testLogs/:systemName/:part', {}, {
	    getTestLogs: { method: 'GET', params: { systemName: '@systemName', part: "list" }, isArray: true },
	    getTestLog: { method: 'GET', params: { systemName: '@systemName', part: '@version' }, isArray: false },
	});
});