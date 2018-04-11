﻿using Backtrace.Base;
using Backtrace.Interfaces;
using Backtrace.Model;
using Backtrace.Types;
using Moq;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Backtrace.Tests.Events
{
    /// <summary>
    /// Tests all available BacktraceClient events 
    /// </summary>
    [TestFixture(Author = "Konrad Dysput", Category = "Events.TestClientEvents", Description = "Test client events")]
    public class EventTests
    {
        /// <summary>
        /// BacktraceClient
        /// </summary>
        private BacktraceClient _backtraceClient;

        /// <summary>
        /// Prepare BacktraceClient
        /// </summary>
        [SetUp]
        public void Setup()
        {
            //prepare mock object
            //mock api
            var api = new Mock<IBacktraceApi<object>>();
            api.Setup(n => n.SendAsync(It.IsAny<BacktraceData<object>>())).Returns(Task.FromResult(new BacktraceResult()));
            api.Setup(n => n.Send(It.IsAny<BacktraceData<object>>())).Returns(new BacktraceResult());

            //mock database
            var database = new Mock<IBacktraceDatabase<object>>();
            throw new NotImplementedException();
            //database.Setup(n => n.GenerateMiniDump(It.IsAny<BacktraceReportBase<object>>(), It.IsAny<MiniDumpType>()));

            //setup new client
            var credentials = new BacktraceCredentials("https://validurl.com/", "validToken");
            //_backtraceClient = new BacktraceClient(credentials, reportPerMin: 0)
            //{
            //    _backtraceApi = api.Object,
            //    Database = database.Object
            //};
        }

        [TestCase(5, 2)]
        [TestCase(15, 5)]
        [Test(Author = "Konrad Dysput", Description = "Test rate limiting and after send event")]
        public async Task TestRateLimiting(int numberOfCycles, int rateLimit)
        {
            //we send reports by using Send method and SendAsync method - 8 per c
            int totalSendReport = numberOfCycles * 8;
            int expectedNumberOfReports = rateLimit > totalSendReport ? totalSendReport : rateLimit;
            int totalNumberOfReports = 0;
            bool eventTrigger = false;

            _backtraceClient.OnClientReportLimitReached = (BacktraceReport report) =>
            {
                eventTrigger = true;
            };

            _backtraceClient.AfterSend = (BacktraceResult res) =>
            {
                if (res.Status == BacktraceResultStatus.Ok)
                {
                    totalNumberOfReports++;
                }
            };

            _backtraceClient.SetClientReportLimit((uint)rateLimit);
            for (int i = 0; i < numberOfCycles; i++)
            {
                //test sync submission
                _backtraceClient.Send("client message");
                _backtraceClient.Send(new Exception("test exception"));
                _backtraceClient.Send(new BacktraceReport("report message"));
                _backtraceClient.Send(new BacktraceReport(new Exception("test exception")));
                //test async submission
                await _backtraceClient.SendAsync("client message");
                await _backtraceClient.SendAsync(new Exception("test exception"));
                await _backtraceClient.SendAsync(new BacktraceReport("report message"));
                await _backtraceClient.SendAsync(new BacktraceReport(new Exception("test exception")));
            }

            Assert.IsTrue(eventTrigger);
            Assert.AreEqual(expectedNumberOfReports, totalNumberOfReports);
        }

        /// <summary>
        /// Test OnReportStart, BeforeSend and AfterSend events
        /// </summary>
        [TestCase(1)]
        [TestCase(5)]
        [TestCase(10)]
        [Test(Author = "Konrad Dysput", Description = "Test OnReportStart, BeforeSend and AfterSend events")]
        public void TestStartAndStopEvents(int numberOfThreads)
        {
            //setup test method
            //Test case tests Send and SendAsync method - magic number 2 means that we test 2 methods
            var expectedNumberOfStarts = numberOfThreads * 2;
            var expectedNumberOfEnds = numberOfThreads * 2;
            var expectedNumberOfBeforeSendEvents = numberOfThreads * 2;

            List<Thread> threads = new List<Thread>();
            int totalStart = 0;
            int totalBeforeSennd = 0;
            int totalEnds = 0;

            _backtraceClient.OnReportStart = (BacktraceReport report) =>
            {
                totalStart++;
            };
            _backtraceClient.BeforeSend = (BacktraceData<object> data) =>
            {
                totalBeforeSennd++;
                return data;
            };
            _backtraceClient.AfterSend = (BacktraceResult result) =>
            {
                totalEnds++;
            };

            for (int threadIndex = 0; threadIndex < numberOfThreads; threadIndex++)
            {

                threads.Add(new Thread(new ThreadStart(() =>
                {
                    _backtraceClient.Send("client message");
                    _backtraceClient.SendAsync("client message").Wait();
                })));
            }

            threads.ForEach(n => n.Start());
            threads.ForEach(n => n.Join());

            Assert.AreEqual(totalStart, expectedNumberOfStarts);
            Assert.AreEqual(totalBeforeSennd, expectedNumberOfBeforeSendEvents);
            Assert.AreEqual(totalEnds, expectedNumberOfEnds);
        }
    }
}
