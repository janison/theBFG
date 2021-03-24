angular.module('portal').factory('testArenaApi', function ($localStorage, rxnPortalConfiguration, rx) {


    var log = new rx.Subject();

    const hub = new signalR.HubConnectionBuilder()
        .withUrl("/testArena")
        //.rootPath(rxnPortalConfiguration.baseWebServicesUrl)
        //   .configureLogging(signalR.LogLevel.Information)
        //    .withAutomaticReconnection()
        //'bearer_token': ''//$localStorage.authorizationData.token
        .build();

    hub.start().then(function () {
        console.log("TestArena connected")
    }).catch(function (err) {
        return console.error("TestArena connected failed: " + err.toString());
    });

    hub.on("OnUpdate", function (remoteEvents) {
        log.onNext(remoteEvents);
    });


    var testArenaApi = {
        sendCommand: function(destination, cmd) {

            hub.invoke("sendCommand", destination, cmd);

            console.log(destination + ": " + cmd);
        },

        updates: log,
    };

    return testArenaApi;
});
