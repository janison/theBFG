angular.module('portal').factory('testArenaApi', function ($interval, $localStorage, rxnPortalConfiguration, rx) {


    function toCamel(o) {
        var newO, origKey, newKey, value
        if (o instanceof Array) {
          return o.map(function(value) {
              if (typeof value === "object") {
                value = toCamel(value)
              }
              return value
          })
        } else {
          newO = {}
          for (origKey in o) {
            if (o.hasOwnProperty(origKey)) {
              newKey = (origKey.charAt(0).toLowerCase() + origKey.slice(1) || origKey).toString()
              value = o[origKey]
              if (value instanceof Array || (value !== null && value.constructor === Object)) {
                value = toCamel(value)
              }
              newO[newKey] = value
            }
          }
        }
        return newO
      }

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
        JSON.parse(remoteEvents).forEach(e => {
            log.onNext(toCamel(e));            
        });
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
