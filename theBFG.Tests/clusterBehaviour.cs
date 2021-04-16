using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Linq;
using System.Net.WebSockets;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using Rxns;
using Rxns.Cloud;
using Rxns.DDD;
using Rxns.DDD.Commanding;
using Rxns.Logging;
using Rxns.NewtonsoftJson;
using theBFG.TestDomainAPI;

namespace theBFG.Tests
{
    /// <summary>
    /// WARNING: these tests are all used for spiking dev. will be cleaned up later once API
    /// is stable and fit for purpose. most tests will run indefinitely atm
    /// </summary>
    [TestClass]
    public class clusterBehaviour
    {
        private string theGimp = "../../../../theTestGimp/bin/Debug/netcoreapp3.1/theTestGimp.dll";
        [TestInitialize]
        public void Setup()
        {
            
        }
        
        [TestMethod]
        public void should_keep_updated()
        {
            //todo:
            //theBfg.ReloadAnd("launch testApp ../../../../theBfgtestApp/bin/debug/testApp.dll".Split(' ')).WaitR();
        }

        [TestMethod]
        public void should_auto_detect_workers()
        {
            ReportStatus.Log.ReportToConsole();
            var foundWorker = new Subject<Unit>();

            var autoDiscovery = new SsdpDiscoveryService();
            bfgTestApi.AdvertiseForWorkers(autoDiscovery, "compete");

            bfgTestApi
                .DiscoverWork(autoDiscovery, "compete")
                .FirstAsync()
                .Select(_ => new Unit())
                .Subscribe(foundWorker);

            foundWorker.FirstAsync().WaitR();
        }
        
        [TestMethod]
        public void should_detect_testagents()
        {
            try
            {
                var workerLifetime = new Subject<Unit>();
                theBfg.ReloadAnd(args: "fire".Split(' ')).LastAsync().Until();

                theBfg.ReloadAnd(args: $"target {theGimp}".Split(' ')).LastAsync().Until();

                "waiting for test to complete".LogDebug();
                workerLifetime.WaitR();
            }
            catch (Exception e)
            {
               Assert.Fail(e.ToString());
            }
        }

        [TestMethod]
        public void should_support_rapid_fire_mode()
        {
            //this is failing now because the .dll is being downloaded incorrectly when
            //rapidly is appended download function
            //need to detect local vs remote worker

            theBfg.ReloadAnd(args: @$"target {theGimp} and fire rapidly".Split(' ')).LastAsync().Until();

            "waiting for test to complete".LogDebug();
            new Subject<int>().Wait();
        }


        [TestMethod]
        public void should_support_dotnet_test_arena()
        {
            var ta = new DotNetTestArena();

            var allTest = ta.ListTests(theGimp).WaitR().ToArray();

            allTest.Count().Should().Be(6, "4 tests should be found in theTestGimp");

            //todo: write unit test for start
            //ta.Start()
        }

        [TestMethod]
        public void should_upload_logs_on_completion()
        {
            //this is implemented now, need to write test
        }

        [TestMethod]
        [Timeout(5000)]
        public void should_support_integration_test_workflow_parallel()
        {
            var parallelExecution = $@"
                StartUnitTest  {theGimp}!the_test_3,StartUnitTest  {theGimp}!the_test_2, StartUnitTest  {theGimp}!the_test_2,StartUnitTest  {theGimp}!the_test_2,
                StartUnitTest  {theGimp}!the_test_1,     
                StartUnitTest  {theGimp}!the_test_1,StartUnitTest  {theGimp}!the_test_1,     StartUnitTest  {theGimp}!the_test_1,     StartUnitTest  {theGimp}!the_test_1,          
                ";

            var called = 0;
            var results = new Subject<UnitTestResult>();
            var appCmds = Substitute.For<IServiceCommandFactory>();
            
            appCmds.Get("StartUnitTest", Arg.Any<object[]>()).Returns(ci =>
            {
                var cmdArg = (ci.ArgAt<object[]>(1)[0] as IEnumerable<string>).ToStringEach(" ");

                cmdArg.Should().Contain("theTestGimp.dll!the_test_", "the params should be correctly parsed");

                return new StartUnitTest();
            });

            TestWorkflow.StartIntegrationTest(parallelExecution, appCmds, results).Do(t =>
            {
                called++;
                "Saw Unit test".LogDebug();
                results.OnNext((UnitTestResult) new UnitTestResult() {WasSuccessful = true}.AsResultOf(t));
            }).WaitR();
            called.Should().Be(9, "9 commands are in the parallel test");

            called = 0;
        }

        [TestMethod]
        //[Timeout(5000)]
        public void should_support_integration_test_workflow_rapidfire()
        {
            var rapidFireMode = $@"
                StartUnitTest  {theGimp}!the_test_3;5
                StartUnitTest  {theGimp}!the_test_2;2
                StartUnitTest  {theGimp}!the_test_1;1
                StartUnitTest  {theGimp}!the_test_1;0
                StartUnitTest  {theGimp}!the_test_1;
                ";

            var called = 0;
            var results = new Subject<UnitTestResult>();
            var appCmds = Substitute.For<IServiceCommandFactory>();

            appCmds.Get("StartUnitTest", Arg.Any<object[]>()).Returns(ci =>
            {
                var cmdArg = (ci.ArgAt<object[]>(1)[0] as IEnumerable<string>).ToStringEach(" ");
                
                cmdArg.Should().Contain("theTestGimp.dll!the_test_", "the params should be correctly parsed");
                cmdArg.Should().NotContain(";", "the expression should parse the target correctly and not include rapid syntax");
                
                return new StartUnitTest();
            });
            
            TestWorkflow.StartIntegrationTest(rapidFireMode, appCmds, results).Do(t =>
            {
                called++;
                "Saw Unit test".LogDebug();
                results.OnNext((UnitTestResult)new UnitTestResult() { WasSuccessful = true }.AsResultOf(t));
            }).WaitR();

            called.Should().Be(9, "rapidfire syntax was used to trigger the tests");

            called = 0;
        }

        [TestMethod]
        [Timeout(5000)]
        public void should_support_integration_test_workflow_serial()
        {
            var serialExecution = $@"
                StartUnitTest  {theGimp}!the_test_3, 
                StartUnitTest  {theGimp}!the_test_2,     
                StartUnitTest  {theGimp}!the_test_1,     
                ";

            var called = 0;
            var results = new Subject<UnitTestResult>();
            var appCmds = Substitute.For<IServiceCommandFactory>();
            var reserveOrder = 3;

            appCmds.Get("StartUnitTest", Arg.Any<object[]>()).Returns(ci =>
            {
                var cmdArg = (ci.ArgAt<object[]>(1)[0] as IEnumerable<string>).ToStringEach(" ");

                cmdArg.LastOrDefault().Should().Be(reserveOrder--.ToString()[0], "the test validates reverse order");
                
                return new StartUnitTest();
            });

            TestWorkflow.StartIntegrationTest(serialExecution, appCmds, results).Do(t =>
            {
                called++;
                "Saw Unit test".LogDebug();
                results.OnNext((UnitTestResult)new UnitTestResult() { WasSuccessful = true }.AsResultOf(t));
            }).WaitR();

            called.Should().Be(3, "3 commands are in the serial test");
        }



        [TestMethod]
        public void should_support_compete()
        {
            theBfg.ReloadAnd(args: $"target {theGimp} and fire compete 2".Split(' ')).Until();

            new Subject<Unit>().WaitR();
        }
    }
}