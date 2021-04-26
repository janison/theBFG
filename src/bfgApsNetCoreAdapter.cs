using System;
using System.IO;
using System.Reflection;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.FileProviders;
using Rxns;
using Rxns.Hosting;
using Rxns.Hosting.Updates;
using Rxns.WebApiNET5;

namespace theBFG
{
    public class theBFGAspNetCoreAdapter : ConfigureAndStartAspnetCore
    {
        public static IRxnAppCfg Appcfg = null;
        public static IAppStatusCfg AppStatuscfg = new AppStatusCfg()
        {
            AppRoot = theBfg.DataDir.EnsureRooted()
        };

        public static IWebApiCfg Cfg = new WebApiCfg()
        {
            BindingUrl = "http://*:888",
            Html5IndexHtml = "index.html",
            Html5Root = @$"{new FileInfo(Assembly.GetExecutingAssembly().Location).Directory.FullName}\TestArena" // @"/Users/janison/rxns/Rxns.AspSatus/Web/dist/" //the rxns appstatus portal // @"TestArena" //
        };

        public static IAspnetCoreCfg AspnetCfg = new AspnetCoreCfg()
        {
            Cfg = server =>
            {
                var logDir = Path.Combine(AppStatuscfg.AppRoot, "TenantLogs");
                if (!Directory.Exists(logDir))
                    Directory.CreateDirectory(logDir);

                var logFileShareLocation = new PhysicalFileProvider(logDir);

                server
                    .UseFileServer(new FileServerOptions
                    {
                        EnableDefaultFiles = false,
                        EnableDirectoryBrowsing = true,
                        RequestPath = "/tenantlogs",
                        FileProvider = logFileShareLocation,
                        StaticFileOptions = { FileProvider = logFileShareLocation, ServeUnknownFileTypes = true }
                    });
            }
        };

        public theBFGAspNetCoreAdapter()
        {
            var args = Appcfg?.Args ?? new string[0];
            
            WebApiCfg = Cfg;
            AppInfo = new ClusteredAppInfo("bfgTestArena", "1.0.0", args, false);
            App = e => theBFGDef.TestArena(args);
        }

        public override Func<string, Action<IRxnLifecycle>> App { get; }
        public override IRxnAppInfo AppInfo { get; }
        public override IWebApiCfg WebApiCfg { get; }
    }
}
