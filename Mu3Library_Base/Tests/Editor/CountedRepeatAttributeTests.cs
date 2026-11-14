using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using NUnit.Framework;
using NUnit.Framework.Interfaces;
using NUnit.Framework.Internal;
using NUnit.Framework.Internal.Builders;
using NUnit.Framework.Internal.Commands;
using UnityEngine.TestTools;

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

        [Test]
        public void RefusesATestThatIsNotExplicit_BeforeTheFirstRun()
        {
            TestMethod target = BuildExplicit(typeof(PlainShape));
            // As the run builds a [Test] with no [Explicit] on it or on a suite above it.
            target.RunState = RunState.Runnable;

            TestResult result = ExecuteWrapped(target);

            Assert.That(result.ResultState, Is.EqualTo(ResultState.NotRunnable));
            Assert.That(result.Message, Is.EqualTo("[CountedRepeat] holds only an [Explicit] test, which the default run leaves out; mark the test [Explicit]."));
            Assert.That(Calls, Is.Empty);
        }

        [TestCase(typeof(UnitySetUpShape))]
        [TestCase(typeof(UnityTearDownShape))]
        [TestCase(typeof(InheritsAUnitySetUp))]
        public void RefusesAFixtureWithAUnitySetUpOrTearDown_BeforeTheFirstRun(Type fixture)
        {
            TestResult result = ExecuteWrapped(BuildExplicit(fixture));

            Assert.That(result.ResultState, Is.EqualTo(ResultState.NotRunnable));
            Assert.That(result.Message, Is.EqualTo($"[CountedRepeat] calls [SetUp] and [TearDown] in each run, but the framework calls the [UnitySetUp] and [UnityTearDown] of {fixture.Name} once around all the runs."));
            Assert.That(Calls, Is.Empty);
        }

        [TestCase(typeof(MethodOuterActionShape))]
        [TestCase(typeof(OuterActionShape))]
        [TestCase(typeof(InheritsAnOuterAction))]
        public void RefusesATestWithAnOuterUnityTestAction_BeforeTheFirstRun(Type fixture)
        {
            TestMethod target = BuildExplicit(fixture);

            TestResult result = ExecuteWrapped(target);

            Assert.That(result.ResultState, Is.EqualTo(ResultState.NotRunnable));
            Assert.That(result.Message, Is.EqualTo($"[CountedRepeat] calls [SetUp] and [TearDown] in each run, but the framework calls the IOuterUnityTestAction attributes of {target.FullName} once around all the runs."));
            Assert.That(Calls, Is.Empty);
        }

        [Test]
        public void RefusesATestUnderASuiteWithAnOuterUnityTestAction_BeforeTheFirstRun()
        {
            TestMethod target = BuildExplicit(typeof(PlainShape));
            // The framework also takes the actions of each suite above the fixture that has a type,
            // such as a set-up fixture.
            new SetUpFixture(new TypeWrapper(typeof(SuiteOuterActionShape))).Add((Test)target.Parent);

            TestResult result = ExecuteWrapped(target);

            Assert.That(result.ResultState, Is.EqualTo(ResultState.NotRunnable));
            Assert.That(result.Message, Is.EqualTo($"[CountedRepeat] calls [SetUp] and [TearDown] in each run, but the framework calls the IOuterUnityTestAction attributes of {target.FullName} once around all the runs."));
            Assert.That(Calls, Is.Empty);
        }

        private static TestResult ExecuteWrapped(TestMethod target)
        {
            TestExecutionContext context = new TestExecutionContext
            {
                CurrentTest = target,
                CurrentResult = target.MakeTestResult(),
            };

            return new CountedRepeatAttribute().Wrap(new RecordsTheRun(target)).Execute(context);
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

        private sealed class PlainShape
        {
            public void Run() { }
        }

        private class UnitySetUpShape
        {
            [UnitySetUp]
            public IEnumerator UnitySetUp()
            {
                yield break;
            }

            public void Run() { }
        }

        private sealed class InheritsAUnitySetUp : UnitySetUpShape { }

        private sealed class UnityTearDownShape
        {
            [UnityTearDown]
            public IEnumerator UnityTearDown()
            {
                yield break;
            }

            public void Run() { }
        }

        private sealed class MethodOuterActionShape
        {
            [OuterAction]
            public void Run() { }
        }

        [OuterAction]
        private class OuterActionShape
        {
            public void Run() { }
        }

        private sealed class InheritsAnOuterAction : OuterActionShape { }

        // The test wraps it as a set-up fixture. It carries no [SetUpFixture], with which the run would
        // load it and give its action to every test in the namespace.
        [OuterAction]
        private sealed class SuiteOuterActionShape { }

        [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
        private sealed class OuterActionAttribute : NUnitAttribute, IOuterUnityTestAction
        {
            public IEnumerator BeforeTest(ITest test)
            {
                yield break;
            }

            public IEnumerator AfterTest(ITest test)
            {
                yield break;
            }
        }
    }
}
