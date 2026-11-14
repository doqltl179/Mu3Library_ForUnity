using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NUnit.Framework;
using NUnit.Framework.Interfaces;
using NUnit.Framework.Internal;
using NUnit.Framework.Internal.Builders;
using NUnit.Framework.Internal.Commands;

namespace Mu3Library.Tests
{
    /// <summary>
    /// The refusals of <see cref="CountedRepeatAttribute"/> before its first run. The fixtures it wraps
    /// here carry no test attribute, so the run loads none of them, and a refusal logs no line, so
    /// test-unity.sh's summary reports no counted repeat for them.
    /// </summary>
    public class CountedRepeatAttributeTests
    {
        private static readonly List<string> Calls = new List<string>();

        [SetUp]
        public void SetUp()
        {
            Calls.Clear();
        }

        [TestCase(typeof(TaskSetUpShape), nameof(TaskSetUpShape.SetUpAsync))]
        [TestCase(typeof(TaskTearDownShape), nameof(TaskTearDownShape.TearDownAsync))]
        [TestCase(typeof(InheritsATaskSetUp), nameof(TaskSetUpShape.SetUpAsync))]
        public void RefusesAFixtureWithASetUpOrTearDownThatReturnsATask_BeforeTheFirstRun(Type fixture, string method)
        {
            TestMethod target = BuildExplicit(fixture);
            TestExecutionContext context = new TestExecutionContext
            {
                CurrentTest = target,
                CurrentResult = target.MakeTestResult(),
            };

            TestResult result = new CountedRepeatAttribute().Wrap(new RecordsTheRun(target)).Execute(context);

            Assert.That(result.ResultState, Is.EqualTo(ResultState.NotRunnable));
            Assert.That(result.Message, Is.EqualTo($"[CountedRepeat] runs each run, its [SetUp] and [TearDown] included, in one call, but {fixture.Name}.{method} is a [SetUp] or [TearDown] that returns a Task, which the framework waits for over several frames; in one call a task that continues on Unity's main thread never ends."));
            Assert.That(Calls, Is.Empty);
        }

        private static TestMethod BuildExplicit(Type fixtureType)
        {
            TestFixture fixture = new TestFixture(new TypeWrapper(fixtureType));
            TestMethod test = new NUnitTestCaseBuilder().BuildTestMethod(new MethodWrapper(fixtureType, nameof(TaskSetUpShape.Run)), fixture, null);
            // [CountedRepeat] holds only an [Explicit] test, and refuses any other first.
            test.RunState = RunState.Explicit;
            fixture.Add(test);
            return test;
        }

        // The command that [CountedRepeat] wraps: the framework's setup, test, and teardown.
        private sealed class RecordsTheRun : TestCommand
        {
            public RecordsTheRun(Test test)
                : base(test) { }

            public override TestResult Execute(ITestExecutionContext context)
            {
                Calls.Add("run");
                context.CurrentResult.SetResult(ResultState.Success);
                return context.CurrentResult;
            }
        }

        private class TaskSetUpShape
        {
            [SetUp]
            public Task SetUpAsync()
            {
                return Task.CompletedTask;
            }

            public void Run() { }
        }

        private sealed class InheritsATaskSetUp : TaskSetUpShape { }

        private sealed class TaskTearDownShape
        {
            [SetUp]
            public void SetUp() { }

            [TearDown]
            public Task TearDownAsync()
            {
                return Task.CompletedTask;
            }

            public void Run() { }
        }
    }
}
