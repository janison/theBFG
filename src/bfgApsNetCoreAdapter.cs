using System;
using Rxns.Hosting;
using Rxns.WebApiNET5;

namespace theBFG
{
    public class theBFGAspNetCoreAdapter : ConfigureAndStartAspnetCore
    {
        public static IRxnAppCfg Appcfg = null;
        public static IWebApiCfg Cfg = new WebApiCfg()
        {
            BindingUrl = "http://*:888",
            Html5IndexHtml = "index.html",
            Html5Root = @"C:\jan\Rxns\Rxns.AppSatus\Web\dist" // @"/Users/janison/rxns/Rxns.AspSatus/Web/dist/" //the rxns appstatus portal // @"TestArena" //
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
