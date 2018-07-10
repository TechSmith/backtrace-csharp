﻿using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using Backtrace.Base;
using Backtrace.Model;
using Backtrace.Model.JsonData;
using NUnit.Framework;
namespace Backtrace.Tests.EnvironmentTests
{
    /// <summary>
    /// Test built in attributes creation
    /// </summary>
    [TestFixture(Author = "Konrad Dysput", Category = "EnvironmentTests.Attributes")]
    public class BacktraceAttributesTests
    {
        [Test]
        public void TestAttributesCreation()
        {
            var report = new BacktraceReport("testMessage");
            //test object creation
            Assert.DoesNotThrow(() => new BacktraceAttributes(report, null));

            //test empty exception
            Assert.DoesNotThrow(() =>
            {
                var backtraceAttributes = new BacktraceAttributes(report,new Dictionary<string, object>());
                backtraceAttributes.SetExceptionAttributes(new BacktraceReport("message"));
            });
            //test null
            Assert.DoesNotThrow(() =>
            {
                var backtraceAttributes = new BacktraceAttributes(report,new Dictionary<string, object>());
                backtraceAttributes.SetExceptionAttributes(null);
            });
        }
    }
}
