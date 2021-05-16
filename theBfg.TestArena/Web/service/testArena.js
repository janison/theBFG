angular.module('portal').factory('testArenaApi', function ($interval, $localStorage, rxnPortalConfiguration, rx) {


    var log = new rx.Subject();
    var isConnected = new rx.Subject();

    var hub = new signalR.HubConnectionBuilder()
        .withUrl("/testArena")
        //.rootPath(rxnPortalConfiguration.baseWebServicesUrl)
        //.configureLogging(signalR.LogLevel.Information)
       // .withAutomaticReconnection()
        //'bearer_token': ''//$localStorage.authorizationData.token
        .build();

    var isConnecting = false;
    var connectNow = function () {

        if (isConnecting) {
            return;
        }
        isConnecting = true;
        hub.start().then(function () {
            console.log("TestArena connected");
            isConnecting = false;
        }).catch(function (err) {
            isConnecting = false;
            return console.error("TestArena connected failed: " + err.toString());
        });
    }

    //omg yes i resorted this after after trying for an hour to find the correct
    //way to subscribe to events in this V of signalr. should upgrade to latest v i know...
    //until then..
    $interval(function () {
        var online = hub.connectionState == "Connected";
        if (!online) {
            connectNow();
        };

        isConnected.onNext(online);
    }, 1000);
    
    hub.on("OnUpdate", function (remoteEvents) {
        log.onNext(remoteEvents);
    });


    var testArenaApi = {
        sendCommand: function(destination, cmd) {

            hub.invoke("sendCommand", destination, cmd);

            console.log(destination + ": " + cmd);
        },
        isConnected,
        updates: log,
        updateCfg: function(cfg) {
             hub.invoke("saveCfg", cfg)
        }
    };

    return testArenaApi;
});
