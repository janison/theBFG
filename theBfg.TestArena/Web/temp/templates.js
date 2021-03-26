angular.module('portal').run(['$templateCache', function($templateCache) {
  'use strict';

  $templateCache.put('authentication/partial/login/login.html',
    "<div class=col-md-12 ng-controller=LoginCtrl><input type=text ng-model=loginData.userName id=\"username\"> <input type=password ng-model=loginData.password id=\"password\"> <input type=Submit id=login value=Login ng-click=\"login()\"> <input type=button id=login value=Logout ng-click=\"logout()\"><div>{{message}}</div></div>"
  );


  $templateCache.put('authentication/partial/refresh/refresh.html',
    "<div class=col-md-12 ng-controller=RefreshCtrl></div>"
  );


  $templateCache.put('directive/fileDownload/fileDownload.html',
    "<div></div>"
  );


  $templateCache.put('errors/partial/errors/errors.html',
    "<div ng-controller=ErrorsCtrl><div infinite-scroll=getMoreErrors() infinite-scroll-distance=1 infinite-scroll-immediate-check=true><accordion class=bg-danger close-others=true><accordion-group heading={{error.error}} ng-repeat=\"error in errors\"><accordion-heading><div class=media><div class=pull-left><i style=\"vertical-align: central\" class=\"fa fa-chain-broken fa-3x fa-border\"></i></div><div class=media-body><div class=\"media-heading h4\"><i>{{error.tenant}}</i> - {{error.system}}</div></div><div class=\"media-body h5 no-overflow\">{{error.error}}</div><div class=\"pull-right h5 media-body\"><div am-time-ago=error.timestamp></div></div></div></accordion-heading><tabset><tab heading=Stacktrace><div class=\"well well-large\"><code class=bold>{{error.error}}</code><p></p><code>{{error.stackTrace}}</code></div></tab><tab heading=Log ng-click=getLog(error.errorId)><table class=\"table table-hover table-striped table-condensed h6\"><thead><th>Timestamp</th><th>Reporter</th><th>Level</th><th>Message</th></thead><tbody><tr ng-class=\"{error: entry.level === 'Error', infor: entry.level === 'Info', verbose: entry.level === 'Verbose'}\" ng-repeat=\"entry in log\"><td>{{entry.timestamp | amDateFormat:'hh:mm:ss.SSS'}}</td><td>{{entry.reporter}}</td><td>{{entry.level}}</td><td>{{entry.message}}</td></tr></tbody></table></tab><li class=dropdown><a class=dropdown-toggle data-toggle=dropdown href=#>Action <span class=caret></span></a><ul class=dropdown-menu role=menu><li><a href=\"\">Resolve...</a></li><li><a href=\"\">Dismiss...</a></li><li class=divider></li><li><a class=dropdown-menu-item href=\"\">File Bug...</a></li></ul></li></tabset></accordion-group></accordion></div></div>"
  );


  $templateCache.put('metrics/partial/metrics/metrics.html',
    "<div ng-controller=MetricsCtrl style=\"font-family: monospace\"><h2>Events Publishing Overview</h2><vis-graph2d data=graphData groups=graphGroups options=graphOptions events=graphEvents></vis-graph2d><vis-network data=networkData groups=networkGroups options=networkOptions events=networkEvents></vis-network></div>"
  );


  $templateCache.put('partials/allModules.html',
    "<div class=col-md-12 ng-controller=AllmodulesCtrl><ul class=\"nav nav-pills nav-stacked\"><li class=\"btn btn-group fa-4x\"><a ui-sref=testArena><span class=\"fa fa-bullseye text-muted\"></span></a></li><li class=\"btn btn-group fa-4x\"><a ui-sref=appStatus><span class=\"fa fa-cloud\"></span></a></li><li class=\"btn btn-group\"><a ui-sref=errors><span class=\"fa fa-chain-broken fa-4x\"></span></a></li><li class=\"btn btn-group\"><a ui-sref=metrics><span class=\"fa fa-bar-chart-o fa-4x\"></span></a></li><li class=\"btn btn-group\"><a ui-sref=systemLog><span class=\"fa fa-list-alt fa-4x\"></span></a></li><li class=\"btn btn-group\"><a ui-sref=remoteCommand><span class=\"fa fa-caret-square-o-right fa-4x\"></span></a></li><li class=\"btn btn-group\"><a ui-sref=updates><span class=\"fa fa-cloud-download fa-4x\"></span></a></li></ul></div>"
  );


  $templateCache.put('systemstatus/partial/appStatus/appStatus.html',
    "<div class=col-md-12 ng-controller=AppstatusCtrl><span class=\"fa fa-question-circle fa-4x\" ng-hide=hasMachines></span><ul id=machine-status ng-repeat=\"machine in machines\"><li id=machine-status class=\"fa fa-laptop\">{{machine.Tenant}}</li><ul><li class=list-unstyled ng-repeat=\"status in machine.systems\"><span class=\"fa fa-cloud\" ng-class=\"{error: status.system.status == '1', infor: status.system.status == '0'}\"></span>[{{status.system.version}}] <span id=machine-status-application>{{status.system.systemName}}</span><div id=machine-status-last-update am-time-ago=status.System.Timestamp></div><div class=\"fa fa-gears\" style=\"padding-left: 20px\">{{status.system.ipAddress}}</div><div style=\"padding-left: 20px\" ng-repeat=\"meta in status.meta\"><div class=\"fa fa-inbox\">{{meta.key}} : {{meta.value}}</div></div><div style=\"padding-left: 20px\" ng-repeat=\"meta in status.meta | filter: onlyQueues\"><div class=\"fa fa-inbox\" ng-if=\"meta.queueName != null\">{{meta.queueName}} : {{meta.queueCurrent}} / {{meta.queueSize}}</div></div></li></ul></ul></div>"
  );


  $templateCache.put('systemstatus/partial/applicationStatus/applicationStatus.html',
    "<div class=col-md-12 ng-controller=ApplicationStatusCtrl><span class=\"fa fa-question-circle fa-4x\" ng-hide=hasMachines></span><ul id=machine-status ng-repeat=\"machine in machines\"><li id=machine-status class=\"fa fa-laptop\">{{machine.tenant}}</li><ul><li class=list-unstyled ng-repeat=\"status in machine.systems.$values\"><span class=\"fa fa-cloud\" ng-class=\"{error: status.system.status == '1', infor: status.system.status == '0'}\"></span> [{{status.system.version}}] <span id=machine-status-application>{{status.system.systemName}}</span><div id=machine-status-last-update am-time-ago=status.system.timestamp></div><div class=\"fa fa-gears\" style=\"padding-left: 20px\">{{status.system.ipAddress}}</div><div style=\"padding-left: 20px\" ng-repeat=\"meta in status.meta\"><div class=\"fa fa-bar-chart\" ng-class=\"{error: meta.CpuAverage > '70' || meta.MemAverage > '89', infor: meta.CpuAverage < '70' || meta.MemAverage < '89'}\" ng-if=\"meta.CpuAverage != null\">CPU: [{{meta.CpuAverage | number:1}}%] RAM: [{{meta.MemAverage | number:1}}%] Threads: [{{meta.Threads}}] TaskPool: [{{meta.AppThreadsSize}} / {{meta.AppThreadsMax}}]</div><div style=\"padding-left: 20px\" ng-repeat=\"(key,value) in meta\"><div class=\"fa fa-task\">{{key}} : <i>{{value}}</i></div></div><div class=\"fa fa-gears\" ng-if=\"meta.OS != null\">OS: {{meta.OS}}</div></div></li></ul></ul></div>"
  );


  $templateCache.put('systemstatus/partial/remoteCommand/remoteCommand.html',
    "<div class=col-md-12 ng-controller=remoteCommandCtrl><form name=remote><div class=input-group><input type=text class=form-control ng-init=\"destination = 'Not Set\\\\App'\" ng-model=destination> <input type=text class=form-control ng-init=\"cmd = 'Update'\" ng-model=cmd> <span class=input-group-btn><button class=\"btn btn-default\" type=button ng-click=\"publish(destination, cmd)\">Send</button></span></div><div class=span6><table class=\"table table-hover table-striped table-condensed h6\"><thead><th>Timestamp</th><th>System</th><th>Reporter</th><th>Level</th><th>Message</th></thead><tbody><tr ng-class=\"{error: entry.Level === 'Error', infor: entry.Level === 'Info', verbose: entry.Level === 'Verbose'}\" ng-repeat=\"entry in log\"><td>{{entry.Timestamp | amDateFormat:'hh:mm:ss.SSS'}}</td><td>{{entry.Tenant}}</td><td>{{entry.Reporter}}</td><td>{{entry.Level}}</td><td>{{entry.Message}}</td></tr></tbody></table></div></form></div>"
  );


  $templateCache.put('systemstatus/partial/systemLog/systemLog.html',
    "<div class=col-md-12 ng-controller=SystemLogCtrl><table class=\"table table-hover table-striped table-condensed h6\"><thead><th>Timestamp</th><th>Reporter</th><th>Level</th><th>Message</th></thead><tbody><tr ng-class=\"{error: entry.level === 'Error', infor: entry.level === 'Info', verbose: entry.level === 'Verbose'}\" ng-repeat=\"entry in log\"><td>{{entry.timestamp | amDateFormat:'hh:mm:ss.SSS'}}</td><td>{{entry.reporter}}</td><td>{{entry.level}}</td><td>{{entry.message}}</td></tr></tbody></table></div>"
  );


  $templateCache.put('systemstatus/partial/testArena/testArena.html',
    "<div class=col-md-12 ng-controller=testArenaCtrl><form name=remote><div class=grouped><div><span ng-if=!showTestRunDetailed ng-repeat=\"entry in testRuns\"><span ng-if=!entry.completedAt ng-click=\"cmdShowTopic('testRunsDetails')\" class=\"cmd fa fa-space-shuttle\" ng-attr-title={{entry.info}}></span> <span ng-if=\"entry.result === 'Failed'\" ng-click=\"cmdShowTopic('testRunsDetails')\" class=\"cmd fa fa-thumbs-o-down error\" ng-attr-title={{entry.info}}></span> <span ng-if=\"entry.result === 'Passed'\" ng-click=\"cmdShowTopic('testRunsDetails')\" class=\"cmd fa fa-trophy infor\" ng-attr-title={{entry.info}}></span></span><table ng-if=showTestRunDetailed class=\"table table-hover table-striped table-condensed h6\"><tbody><tr ng-repeat=\"entry in testRuns\"><td ng-class=\"{error: entry.result === 'Failed', infor: entry.result === 'Passed'}\"><span ng-if=!entry.completedAt ng-click=\"cmdShowTopic('testRunsDetails')\" class=\"cmd fa fa-space-shuttle\" ng-attr-title={{entry.info}}></span> <span ng-if=\"entry.result === 'Failed'\" ng-click=\"cmdShowTopic('testRunsDetails')\" class=\"cmd fa fa-thumbs-o-down error\" ng-attr-title={{entry.info}}></span> <span ng-if=\"entry.result === 'Passed'\" ng-click=\"cmdShowTopic('testRunsDetails')\" class=\"cmd fa fa-trophy infor\" ng-attr-title={{entry.info}}></span></td><td>{{entry.dll}}</td><td>{{entry.errors}} / {{entry.total}}</td><td>{{entry.duration}}</td><td><bar-chart class=cmd height=30 data=entry.results></bar-chart></td></tr></tbody></table></div><div class=\"pieChart gfirst\"><pie-chart class=cmd ng-click=\"cmdShowTopic('slow')\" data=testOutcomes></pie-chart></div><div class=\"barchart gsecond\"><bar-chart class=cmd height=250 ng-click=\"cmdShowTopic('log')\" data=testSummary></bar-chart></div></div><div><worker-status data=workerInfo></worker-status></div><div><span width=40><span title=Reload ng-click=cmdReload() ng-class=\"{error: !testArenaInfo.isConnected, infor: testArenaInfo.isConnected}\" class=\"cmd fa fa-rocket fa-2x\"></span> <i title=Errors ng-click=\"cmdShowTopic('Failed')\" class=cmd style=\"font-size:larger; padding-right:5px; padding-left: 5px\" ng-class=\"{error : testGlance.errors != '0', infor : testGlance.errors == '0'}\">{{testGlance.errors}}</i></span> / <span class=cmd ng-click=\"cmdShowTopic('all')\">{{testGlance.total}}</span> <span><span ng-click=\"cmdShowTopic('slow')\" class=\"cmd fa fa-truck fa-2x\" title=\"Slow: Duration > 100ms\"></span>{{testGlance.slow}}</span> <span><span ng-click=\"cmdShowTopic('flakey')\" class=\"cmd fa fa-rmb fa-2x\" title=Flakey></span> {{testGlance.flakey}}</span> <span><span ng-click=\"cmdShowTopic('log')\" class=\"cmd fa fa-clock-o fa-2x\"></span> {{msSince(testGlance.startedAt)}}</span> <span><span style=float:right ng-click=cmdClear() title=\"CLEAR ALL DATA!!!\" class=\"cmd fa fa-scissors fa-2x\"></span></span> <span><span class=\"cmd fa fa-search-plus fa-2x\"></span><input type=text ng-model=searchTerm width=\"50\"></span> <span><span ng-click=\"sendCmdDisabled = !sendCmdDisabled\" class=\"cmd fa fa-share-square-o fa-2x\" title=\"Send Custom Commands\"></span></span> <a href=/tenantLogs target=_tenantlogs><span class=\"cmd fa fa-stack-overflow fa-2x\"></span></a></div><div><span ng-if=!sendCmdDisabled style=color:cadetblue class=\"fa fa-share-square-o fa-2x\"></span><div ng-if=!sendCmdDisabled class=input-group style=\"padding-left:20px;padding-right:20px;padding-top: 5px;;padding-bottom: 5px\"><span class=input-group-btn style=\"width: 200px\"><input type=text class=form-control width=80 ng-init=\"cmd = 'Reload'\" ng-model=cmd> <input type=text class=form-control width=80 ng-init=\"destination = 'TestArena\\\\bfgWorker'\" ng-model=destination> <button class=\"cmd btn btn-default\" type=button ng-click=\"publish(destination, cmd)\">Send</button></span></div></div><div><span style=color:cadetblue ng-if=\"currentTopic != ''\" class=\"fa fa-search fa-2x\"></span><table ng-if=\"currentTopic != ''\" class=\"table table-hover table-striped table-condensed h6\"><thead><th>Outcome</th><th>Test</th><th>Worker</th><th>Duration</th><th>Log</th></thead><tbody><tr ng-class=\"{error: entry.result === 'Failed', infor: entry.result === 'Passed'}\" ng-repeat=\"entry in currentTopic\"><td>{{entry.result}}</td><td>{{entry.testName}}</td><td>{{entry.worker}}</td><td>{{entry.duration}}</td><td style=\"white-space: pre\" ng-if=entry.logMessage><pre class=stacktrace>{{entry.logMessage}}</pre></td><td ng-if=!entry.logMessage><i></i></td></tr></tbody></table><table ng-if=!logDisabled class=\"table table-hover table-striped table-condensed h6\"><thead><th>Outcome</th><th>Test</th><th>Worker</th><th>Duration</th><th>Log</th></thead><tbody><tr ng-class=\"{error: entry.result === 'Failed', infor: entry.result === 'Passed'}\" ng-repeat=\"entry in log | filter:searchTerm\"><td>{{entry.result}}</td><td>{{entry.testName}}</td><td>{{entry.worker}}</td><td>{{entry.duration}}</td><td style=\"white-space: pre\" ng-if=\"entry.logMessage || entry.logMessage.length > 1\"><pre class=stacktrace>{{entry.logMessage}}</pre></td><td ng-if=\"!entry.logMessage || entry.logMessage < 2\"></td></tr></tbody></table></div></form></div>"
  );


  $templateCache.put('systemstatus/partial/testLogs/partial/list/list.html',
    "<div class=col-md-12 ng-controller=ListCtrl><h2>Latest App testLogs</h2><table><tr ng-repeat=\"u in testLogs\"><td><a link={{u.name}} ng-attr-url=testLog/{{u.name}}/get file-download></a></td><td>-</td></tr></table></div>"
  );


  $templateCache.put('updates/partial/list/list.html',
    "<div class=\"col-md-12\" ng-controller=\"ListCtrl\">\r" +
    "\n" +
    "        <h2>Latest App Updates</h2>\r" +
    "\n" +
    "        <table>\r" +
    "\n" +
    "            <tr ng-repeat=\"u in appUpdates\">                \r" +
    "\n" +
    "                <td> <a link=\"{{u.systemName}}\" ng-attr-url=\"updates/{{u.systemName}}/{{u.version}}/get\" file-download></a></td><td><a link=\"{{u.version}}\" ng-attr-url=\"updates/{{u.systemName}}/{{u.version}}/get\" file-download><</td></li>\r" +
    "\n" +
    "            </tr>\r" +
    "\n" +
    "        </table>\r" +
    "\n" +
    "    \r" +
    "\n" +
    "</div>\r" +
    "\n"
  );

}]);
