using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using NUnit.Framework.Interfaces;
using NUnit.Framework.Internal;
using NUnit.Framework.Internal.Builders;
using NUnit.Framework.Internal.Commands;
using UnityEngine;
using UnityEngine.TestTools;

namespace Mu3Library.Tests
{
    /// <summary>
    /// <see cref="CountedRepeatRunner"/>, and the number of runs it reads as
    /// <see cref="CountedRepeatAttribute"/> does. The fixtures it repeats here carry no test attribute,
    /// so the run loads none of them and the default run runs none, and its lines go to a list instead
    /// of Unity's log, where test-unity.sh's summary would report them as a counted repeat.
    /// </summary>
    public class CountedRepeatByNameTests
    {
        private static readonly List<string> Calls = new List<string>();
        private readonly List<string> _lines = new List<string>();

        [SetUp]
        public void SetUp()
        {
            Calls.Clear();
            _lines.Clear();
        }

        [Test]
        public void Run_WithoutAName_IsIgnored_AndRunsNothing()
        {
            ITestExecutionContext context = Context();
            TestResult own = context.CurrentResult;

            foreach (string name in new[] { null, "  " })
            {
                TestResult result = CountedRepeatRunner.Run(context, context.CurrentTest, name, "5", _lines.Add);

                Assert.That(result.ResultState, Is.EqualTo(ResultState.Ignored));
                Assert.That(result.Message, Is.EqualTo($"Set {CountedRepeatRunner.NameVariable} to the full name of the test to repeat, as the fullname attribute of the result file gives it."));
            }

            Assert.That(_lines, Is.Empty);
            Assert.That(context.CurrentResult, Is.SameAs(own));
        }

        [Test]
        public void Run_WithAWrongNameOrNumberOfRuns_IsInvalid()
        {
            ITestExecutionContext context = Context();
            string unknown = typeof(CountedRepeatByNameTests).FullName + ".NoSuchTest";

            TestResult wrongName = CountedRepeatRunner.Run(context, context.CurrentTest, unknown, "5", _lines.Add);
            // The name is found; the number of runs is refused before anything else is looked at.
            TestResult wrongRuns = CountedRepeatRunner.Run(context, context.CurrentTest, context.CurrentTest.FullName, "0", _lines.Add);

            Assert.That(wrongName.ResultState, Is.EqualTo(ResultState.NotRunnable));
            Assert.That(wrongName.Message, Does.StartWith($"No EditMode test of the project is named '{unknown}'"));
            Assert.That(wrongRuns.ResultState, Is.EqualTo(ResultState.NotRunnable));
            Assert.That(wrongRuns.Message, Is.EqualTo("MU3LIBRARY_TEST_REPEAT gives the number of runs, a whole number from 1, not '0'."));
            Assert.That(_lines, Is.Empty);
        }

        [Test]
        public void Find_WithoutAName_AsksForOne()
        {
            string refusal = CountedRepeatRunner.Find(Context().CurrentTest, "  ", out TestMethod target);

            Assert.That(refusal, Does.StartWith($"Set {CountedRepeatRunner.NameVariable} to the full name of the test to repeat"));
            Assert.That(target, Is.Null);
        }

        [Test]
        public void Find_GivesTheTestOfThatFullName()
        {
            ITestExecutionContext context = Context();

            string refusal = CountedRepeatRunner.Find(context.CurrentTest, " " + context.CurrentTest.FullName + " ", out TestMethod target);

            Assert.That(refusal, Is.Null);
            Assert.That(target, Is.SameAs(context.CurrentTest));
        }

        [Test]
        public void Find_RefusesANameThatNoTestHas()
        {
            string name = typeof(CountedRepeatByNameTests).FullName + ".NoSuchTest";

            string refusal = CountedRepeatRunner.Find(Context().CurrentTest, name, out _);

            Assert.That(refusal, Is.EqualTo($"No EditMode test of the project is named '{name}'; give the full name of the test, as the fullname attribute of the result file gives it."));
        }

        [Test]
        public void Find_RefusesANameThatMoreThanOneTestHas()
        {
            TestSuite root = new TestSuite("Duplicates");
            TestFixture first = new TestFixture(new TypeWrapper(typeof(Recording)));
            TestFixture second = new TestFixture(new TypeWrapper(typeof(Recording)));
            root.Add(first);
            root.Add(second);
            TestMethod test = Build(first, typeof(Recording), nameof(Recording.FailsOnTheSecondRun));
            Build(second, typeof(Recording), nameof(Recording.FailsOnTheSecondRun));

            string refusal = CountedRepeatRunner.Find(test, test.FullName, out TestMethod target);

            Assert.That(refusal, Is.EqualTo($"2 tests are named '{test.FullName}', and the runner repeats one."));
            Assert.That(target, Is.Null);
        }

        [Test]
        public void Find_RefusesTheNameOfASuite()
        {
            string name = typeof(CountedRepeatByNameTests).FullName + "." + nameof(ParseRuns_RefusesAnythingButAWholeNumberFromOne);

            string refusal = CountedRepeatRunner.Find(Context().CurrentTest, name, out _);

            Assert.That(refusal, Is.EqualTo($"'{name}' names a suite of 4 tests, not a test; give the full name of one of them."));
        }

        [Test]
        public void Prepare_RefusesTheRunnerItself()
        {
            string name = typeof(CountedRepeatRunner).FullName + "." + nameof(CountedRepeatRunner.RepeatTheNamedTest);
            Assert.That(CountedRepeatRunner.Find(Context().CurrentTest, name, out TestMethod runner), Is.Null);

            string refusal = CountedRepeatRunner.Prepare(runner, runner, out TestCommand run);

            Assert.That(refusal, Is.EqualTo($"{name} is this runner; give the full name of the test to repeat."));
            Assert.That(run, Is.Null);
        }

        [TestCase(typeof(Shapes), nameof(Shapes.Steps), "returns IEnumerator, as a [UnityTest] does")]
        [TestCase(typeof(Shapes), nameof(Shapes.WaitsAsync), "returns a Task, which the framework waits for")]
        [TestCase(typeof(Shapes), nameof(Shapes.Repeated), "repeats a test with [CountedRepeat]")]
        [TestCase(typeof(Shapes), nameof(Shapes.WithOuterAction), "has IOuterUnityTestAction attributes")]
        [TestCase(typeof(Shapes), nameof(Shapes.WithPrebuildSetup), "has a [PrebuildSetup] or [PostBuildCleanup]")]
        [TestCase(typeof(UnitySetUpShape), nameof(UnitySetUpShape.Run), "UnitySetUpShape has [UnitySetUp] or [UnityTearDown]")]
        [TestCase(typeof(UnityTearDownShape), nameof(UnityTearDownShape.Run), "UnityTearDownShape has [UnitySetUp] or [UnityTearDown]")]
        [TestCase(typeof(UnityOneTimeSetUpShape), nameof(UnityOneTimeSetUpShape.Run), "UnityOneTimeSetUpShape has [UnityOneTimeSetUp] or [UnityOneTimeTearDown]")]
        [TestCase(typeof(UnityOneTimeTearDownShape), nameof(UnityOneTimeTearDownShape.Run), "UnityOneTimeTearDownShape has [UnityOneTimeSetUp] or [UnityOneTimeTearDown]")]
        [TestCase(typeof(TaskSetUpShape), nameof(TaskSetUpShape.Run), "TaskSetUpShape.SetUpAsync is a [SetUp] or [TearDown] that returns a Task")]
        [TestCase(typeof(OuterActionShape), nameof(OuterActionShape.Run), "has IOuterUnityTestAction attributes")]
        [TestCase(typeof(PrebuildSetupShape), nameof(PrebuildSetupShape.Run), "or its fixture implements IPrebuildSetup or IPostBuildCleanup")]
        [TestCase(typeof(Shapes), nameof(Shapes.WithPostBuildCleanup), "has a [PrebuildSetup] or [PostBuildCleanup]")]
        [TestCase(typeof(PostBuildCleanupShape), nameof(PostBuildCleanupShape.Run), "or its fixture implements IPrebuildSetup or IPostBuildCleanup")]
        public void Prepare_RefusesWhatItCannotRunAsTheFrameworkDoes(Type fixture, string method, string reason)
        {
            string refusal = CountedRepeatRunner.Prepare(Context().CurrentTest, Build(fixture, method), out TestCommand run);

            Assert.That(refusal, Does.Contain(reason));
            Assert.That(run, Is.Null);
        }

        [Test]
        public void Prepare_RefusesATestCaseWhoseMethodHasASuiteAction()
        {
            TestFixture fixture = new TestFixture(new TypeWrapper(typeof(Shapes)));
            ParameterizedMethodSuite cases = new ParameterizedMethodSuite(new MethodWrapper(typeof(Shapes), nameof(Shapes.WithSuiteAction)));
            fixture.Add(cases);

            string refusal = CountedRepeatRunner.Prepare(Context().CurrentTest, Build(cases, typeof(Shapes), nameof(Shapes.WithSuiteAction)), out _);

            Assert.That(refusal, Is.EqualTo($"{cases.FullName} has an ITestAction that targets suites, which the framework calls once around its test cases; the runner sets up only the fixture around the runs."));
        }

        [Test]
        public void Prepare_RefusesATestInNoFixture()
        {
            TestMethod target = Build(new TestSuite("Plain"), typeof(Recording), nameof(Recording.FailsOnTheSecondRun));

            string refusal = CountedRepeatRunner.Prepare(Context().CurrentTest, target, out _);

            Assert.That(refusal, Is.EqualTo($"{target.FullName} is in no test fixture."));
        }

        [Test]
        public void Prepare_RefusesAFixtureThatTakesArguments()
        {
            TestSuite fixture = new NUnitTestFixtureBuilder().BuildFrom(new TypeWrapper(typeof(TakesArguments)), new TestFixtureData(3));
            // As the run builds a fixture that has tests.
            fixture.RunState = RunState.Runnable;

            string refusal = CountedRepeatRunner.Prepare(Context().CurrentTest, Build(fixture, typeof(TakesArguments), nameof(TakesArguments.Run)), out _);

            Assert.That(refusal, Does.Contain("takes arguments, and the runner makes only a fixture that takes none"));
        }

        [Test]
        public void Prepare_RefusesASetUpFixtureAboveTheTest_ThatIsNotAboveTheRunner()
        {
            SetUpFixture setUpFixture = new SetUpFixture(new TypeWrapper(typeof(SetsUpANamespace)));
            TestFixture fixture = new TestFixture(new TypeWrapper(typeof(Recording)));
            setUpFixture.Add(fixture);

            string refusal = CountedRepeatRunner.Prepare(Context().CurrentTest, Build(fixture, typeof(Recording), nameof(Recording.FailsOnTheSecondRun)), out _);

            Assert.That(refusal, Does.Contain("is a [SetUpFixture] above"));
        }

        [Test]
        public void Prepare_RefusesATestTheFrameworkWouldNotRun()
        {
            TestMethod target = Build(typeof(Recording), nameof(Recording.FailsOnTheSecondRun));
            target.RunState = RunState.Ignored;
            target.Properties.Set(PropertyNames.SkipReason, "left out here");

            string refusal = CountedRepeatRunner.Prepare(Context().CurrentTest, target, out _);

            Assert.That(refusal, Is.EqualTo($"The framework would not run {target.FullName}: {target.FullName} is Ignored: left out here"));
        }

        [TestCase("0")]
        [TestCase("-2")]
        [TestCase("1.5")]
        [TestCase("many")]
        public void ParseRuns_RefusesAnythingButAWholeNumberFromOne(string value)
        {
            string refusal = CountedRepeatAttribute.ParseRuns(value, out _, out _);

            Assert.That(refusal, Is.EqualTo($"MU3LIBRARY_TEST_REPEAT gives the number of runs, a whole number from 1, not '{value}'."));
        }

        [Test]
        public void ParseRuns_ReadsTheNumber_AndGivesOneRunWithoutIt()
        {
            Assert.That(CountedRepeatAttribute.ParseRuns(" 12 ", out int runs, out string source), Is.Null);
            Assert.That(runs, Is.EqualTo(12));
            Assert.That(source, Is.EqualTo("MU3LIBRARY_TEST_REPEAT= 12 "));

            Assert.That(CountedRepeatAttribute.ParseRuns(null, out runs, out source), Is.Null);
            Assert.That(runs, Is.EqualTo(1));
            Assert.That(source, Is.EqualTo("MU3LIBRARY_TEST_REPEAT not set"));
        }

        [Test]
        public void Repeat_CallsTheFixtureAsTheFrameworkDoes_AndGoesOnAfterAFailedRun()
        {
            TestMethod target = Build(typeof(Recording), nameof(Recording.FailsOnTheSecondRun));

            TestResult result = RepeatPrepared(target, 3);

            string name = target.FullName;
            Assert.That(Calls, Is.EqualTo(new[]
            {
                "construct", "base one-time setup", "one-time setup",
                "base setup", "setup", "test 1", "teardown", "base teardown",
                "base setup", "setup", "test 2", "teardown", "base teardown",
                "base setup", "setup", "test 3", "teardown", "base teardown",
                "one-time teardown", "base one-time teardown", "dispose",
            }));
            Assert.That(_lines.Count, Is.EqualTo(5));
            Assert.That(_lines[0], Is.EqualTo($"{name}: start, runs 3 (set here)"));
            Assert.That(_lines[1], Does.StartWith($"{name}: run 1 of 3: Passed in "));
            Assert.That(_lines[2], Does.StartWith($"{name}: run 2 of 3: Failed in ").And.Contain("the second run fails"));
            Assert.That(_lines[3], Does.StartWith($"{name}: run 3 of 3: Passed in "));
            Assert.That(_lines[4], Is.EqualTo($"{name}: totals: runs 3 of 3, passed 2, failed 1, inconclusive 0, skipped 0, failure rate 33.33%"));
            Assert.That(result.ResultState, Is.EqualTo(ResultState.Failure));
            Assert.That(result.Message, Does.StartWith("runs 3 of 3, passed 2, failed 1, inconclusive 0, skipped 0, failure rate 33.33%; first failure, run 2: ").And.Contain("the second run fails"));
        }

        [Test]
        public void Repeat_FailsARunThatLogsAnErrorItDidNotExpect()
        {
            // This test sees the log as well, through the framework's check around it.
            LogAssert.Expect(LogType.Error, LogsAnError.Error);
            TestMethod target = Build(typeof(LogsAnError), nameof(LogsAnError.LogsOnTheSecondRun));

            TestResult result = RepeatPrepared(target, 3);

            Assert.That(_lines[1], Does.Contain(": run 1 of 3: Passed in "));
            Assert.That(_lines[2], Does.Contain(": run 2 of 3: Failed").And.Contain(LogsAnError.Error));
            Assert.That(_lines[3], Does.Contain(": run 3 of 3: Passed in "));
            Assert.That(result.ResultState, Is.EqualTo(ResultState.Failure));
        }

        [Test]
        public void Repeat_TimesEachRunOutFromItsOwnStart_WithTheTimeoutOfTheTest()
        {
            TestMethod target = Build(typeof(Sleeps), nameof(Sleeps.SleepsPastTheTimeoutOnTheSecondRun));

            RepeatPrepared(target, 3);

            // The three runs take longer than the timeout together, and only the second alone.
            Assert.That(_lines[1], Does.Contain(": run 1 of 3: Passed in "));
            Assert.That(_lines[2], Does.Contain(": run 2 of 3: Failed").And.Contain($"Timeout value of {Sleeps.TimeoutMilliseconds} ms was exceeded"));
            Assert.That(_lines[3], Does.Contain(": run 3 of 3: Passed in "));
        }

        [Test]
        public void Repeat_TimesEachRunOut_WithTheTimeoutOfTheFixtureClass()
        {
            TestMethod target = Build(typeof(SleepsUnderAClassTimeout), nameof(SleepsUnderAClassTimeout.SleepsPastTheTimeoutOnTheSecondRun));

            RepeatPrepared(target, 3);

            Assert.That(_lines[1], Does.Contain(": run 1 of 3: Passed in "));
            Assert.That(_lines[2], Does.Contain(": run 2 of 3: Failed").And.Contain($"Timeout value of {Sleeps.TimeoutMilliseconds} ms was exceeded"));
            Assert.That(_lines[3], Does.Contain(": run 3 of 3: Passed in "));
        }

        [Test]
        public void Repeat_CallsTheSuiteActionsOfTheFixtureOnce_AndItsTestActionsInEachRun()
        {
            TestMethod target = Build(typeof(WithActions), nameof(WithActions.Run));

            RepeatPrepared(target, 2);

            Assert.That(Calls, Is.EqualTo(new[]
            {
                "before Suite",
                "before Test", "run", "after Test",
                "before Test", "run", "after Test",
                "after Suite",
            }));
        }

        [Test]
        public void Repeat_RunsNothingWhenTheTestCannotChangeTheContext_AndStillTearsTheFixtureDown()
        {
            ITestExecutionContext context = Context();
            int timeout = context.TestCaseTimeout;
            TestMethod target = Build(typeof(BreaksTheContext), nameof(BreaksTheContext.Run));

            TestResult result = RepeatPrepared(target, 3);

            Assert.That(Calls, Is.EqualTo(new[] { "construct", "one-time teardown", "dispose" }));
            Assert.That(_lines, Is.Empty);
            Assert.That(result.ResultState.Status, Is.EqualTo(TestStatus.Failed));
            Assert.That(result.Message, Does.Contain(ThrowsOnTheContextAttribute.Error));
            Assert.That(context.TestCaseTimeout, Is.EqualTo(timeout));
        }

        [Test]
        public void Repeat_RunsNothingAfterAFailedOneTimeSetUp_AndStillTearsTheFixtureDown()
        {
            TestMethod target = Build(typeof(FailsToSetUp), nameof(FailsToSetUp.Run));

            TestResult result = RepeatPrepared(target, 3);

            Assert.That(Calls, Is.EqualTo(new[] { "one-time setup", "one-time teardown" }));
            Assert.That(_lines, Is.Empty);
            Assert.That(result.ResultState.Status, Is.EqualTo(TestStatus.Failed));
            Assert.That(result.Message, Does.StartWith("OneTimeSetUp: ").And.Contain("the one-time setup fails"));
        }

        [Test]
        public void Repeat_FailsWhenTheOneTimeTearDownFails_AfterRunsThatPassed()
        {
            TestMethod target = Build(typeof(FailsToTearDown), nameof(FailsToTearDown.Run));

            TestResult result = RepeatPrepared(target, 2);

            Assert.That(_lines[3], Does.EndWith(": totals: runs 2 of 2, passed 2, failed 0, inconclusive 0, skipped 0, failure rate 0.00%"));
            Assert.That(result.ResultState.Status, Is.EqualTo(TestStatus.Failed));
            Assert.That(result.Message, Does.StartWith("runs 2 of 2, passed 2, failed 0, inconclusive 0, skipped 0, failure rate 0.00%; OneTimeTearDown: ").And.Contain("the one-time teardown fails"));
        }

        [Test]
        public void Repeat_LeavesTheContextAsItFoundIt()
        {
            ITestExecutionContext context = Context();
            Test test = context.CurrentTest;
            TestResult result = context.CurrentResult;
            int timeout = context.TestCaseTimeout;
            DateTime startTime = context.StartTime;
            long startTicks = context.StartTicks;
            TestMethod target = Build(typeof(Sleeps), nameof(Sleeps.SleepsPastTheTimeoutOnTheSecondRun));
            Assert.That(timeout, Is.Not.EqualTo(Sleeps.TimeoutMilliseconds));

            RepeatPrepared(target, 1);

            Assert.That(context.CurrentTest, Is.SameAs(test));
            Assert.That(context.CurrentResult, Is.SameAs(result));
            Assert.That(context.TestObject, Is.SameAs(this));
            Assert.That(context.TestCaseTimeout, Is.EqualTo(timeout));
            Assert.That(context.StartTime, Is.EqualTo(startTime));
            Assert.That(context.StartTicks, Is.EqualTo(startTicks));
            Assert.That(target.Parent.Fixture, Is.Null);
        }

        private TestResult RepeatPrepared(TestMethod target, int runs)
        {
            ITestExecutionContext context = Context();
            Assert.That(CountedRepeatRunner.Prepare(context.CurrentTest, target, out TestCommand run), Is.Null);
            return CountedRepeatRunner.Repeat(context, context.CurrentTest, target, run, runs, "set here", _lines.Add);
        }

        // The framework keeps a test's context where a test cannot reach it; the runner gets its own
        // from the command it takes the place of.
        private static ITestExecutionContext Context()
        {
            Type type = typeof(UnitySetUpAttribute).Assembly.GetType("UnityEngine.TestRunner.NUnitExtensions.Runner.UnityTestExecutionContext");
            return (ITestExecutionContext)type.GetProperty("CurrentContext", BindingFlags.Public | BindingFlags.Static).GetValue(null);
        }

        private static TestMethod Build(Type fixtureType, string methodName)
        {
            return Build(new TestFixture(new TypeWrapper(fixtureType)), fixtureType, methodName);
        }

        private static TestMethod Build(TestSuite fixture, Type fixtureType, string methodName)
        {
            TestMethod test = new NUnitTestCaseBuilder().BuildTestMethod(new MethodWrapper(fixtureType, methodName), fixture, null);
            // NUnit judges a method that returns a value as a test with no expected result, while the
            // run builds each of these shapes, under its attribute, as a runnable test.
            test.RunState = RunState.Runnable;
            fixture.Add(test);
            return test;
        }

        private class RecordingBase
        {
            [OneTimeSetUp]
            public void BaseOneTimeSetUp()
            {
                Calls.Add("base one-time setup");
            }

            [OneTimeTearDown]
            public void BaseOneTimeTearDown()
            {
                Calls.Add("base one-time teardown");
            }

            [SetUp]
            public void BaseSetUp()
            {
                Calls.Add("base setup");
            }

            [TearDown]
            public void BaseTearDown()
            {
                Calls.Add("base teardown");
            }
        }

        private sealed class Recording : RecordingBase, IDisposable
        {
            private int _runs;

            public Recording()
            {
                Calls.Add("construct");
            }

            [OneTimeSetUp]
            public void OneTimeSetUp()
            {
                Calls.Add("one-time setup");
            }

            [OneTimeTearDown]
            public void OneTimeTearDown()
            {
                Calls.Add("one-time teardown");
            }

            [SetUp]
            public void SetUp()
            {
                Calls.Add("setup");
            }

            [TearDown]
            public void TearDown()
            {
                Calls.Add("teardown");
            }

            public void FailsOnTheSecondRun()
            {
                _runs++;
                Calls.Add("test " + _runs);
                if (_runs == 2)
                {
                    Assert.Fail("the second run fails");
                }
            }

            public void Dispose()
            {
                Calls.Add("dispose");
            }
        }

        private sealed class LogsAnError
        {
            public const string Error = "an error that the second run logs";

            private int _runs;

            public void LogsOnTheSecondRun()
            {
                _runs++;
                if (_runs == 2)
                {
                    Debug.LogError(Error);
                }
            }
        }

        private sealed class Sleeps
        {
            public const int TimeoutMilliseconds = 400;

            private int _runs;

            [Timeout(TimeoutMilliseconds)]
            public void SleepsPastTheTimeoutOnTheSecondRun()
            {
                _runs++;
                Thread.Sleep(_runs == 2 ? 600 : 150);
            }
        }

        private sealed class FailsToSetUp
        {
            [OneTimeSetUp]
            public void OneTimeSetUp()
            {
                Calls.Add("one-time setup");
                throw new InvalidOperationException("the one-time setup fails");
            }

            [OneTimeTearDown]
            public void OneTimeTearDown()
            {
                Calls.Add("one-time teardown");
            }

            [SetUp]
            public void SetUp()
            {
                Calls.Add("setup");
            }

            public void Run()
            {
                Calls.Add("run");
            }
        }

        private sealed class FailsToTearDown
        {
            [OneTimeTearDown]
            public void OneTimeTearDown()
            {
                throw new InvalidOperationException("the one-time teardown fails");
            }

            public void Run() { }
        }

        private sealed class Shapes
        {
            public IEnumerator Steps()
            {
                yield break;
            }

            public Task WaitsAsync()
            {
                return Task.CompletedTask;
            }

            [CountedRepeat]
            public void Repeated() { }

            [OuterAction]
            public void WithOuterAction() { }

            [PrebuildSetup(typeof(PrebuildSetupShape))]
            public void WithPrebuildSetup() { }

            [PostBuildCleanup(typeof(PostBuildCleanupShape))]
            public void WithPostBuildCleanup() { }

            [RecordsActions(ActionTargets.Suite)]
            public void WithSuiteAction() { }
        }

        private sealed class PostBuildCleanupShape : IPostBuildCleanup
        {
            public void Cleanup() { }

            public void Run() { }
        }

        [Timeout(Sleeps.TimeoutMilliseconds)]
        private sealed class SleepsUnderAClassTimeout
        {
            private int _runs;

            public void SleepsPastTheTimeoutOnTheSecondRun()
            {
                _runs++;
                Thread.Sleep(_runs == 2 ? 600 : 150);
            }
        }

        [RecordsActions(ActionTargets.Suite)]
        [RecordsActions(ActionTargets.Test)]
        private sealed class WithActions
        {
            public void Run()
            {
                Calls.Add("run");
            }
        }

        private sealed class BreaksTheContext : IDisposable
        {
            public BreaksTheContext()
            {
                Calls.Add("construct");
            }

            [OneTimeTearDown]
            public void OneTimeTearDown()
            {
                Calls.Add("one-time teardown");
            }

            [SetUp]
            public void SetUp()
            {
                Calls.Add("setup");
            }

            [ThrowsOnTheContext]
            public void Run()
            {
                Calls.Add("run");
            }

            public void Dispose()
            {
                Calls.Add("dispose");
            }
        }

        // As [SetCulture] with a culture that does not exist.
        [AttributeUsage(AttributeTargets.Method)]
        private sealed class ThrowsOnTheContextAttribute : NUnitAttribute, IApplyToContext
        {
            public const string Error = "the context cannot take this change";

            public void ApplyToContext(ITestExecutionContext context)
            {
                throw new InvalidOperationException(Error);
            }
        }

        [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
        private sealed class RecordsActionsAttribute : NUnitAttribute, ITestAction
        {
            public RecordsActionsAttribute(ActionTargets targets)
            {
                Targets = targets;
            }

            public ActionTargets Targets { get; }

            public void BeforeTest(ITest test)
            {
                Calls.Add("before " + Targets);
            }

            public void AfterTest(ITest test)
            {
                Calls.Add("after " + Targets);
            }
        }

        private sealed class UnitySetUpShape
        {
            [UnitySetUp]
            public IEnumerator UnitySetUp()
            {
                yield break;
            }

            public void Run() { }
        }

        private sealed class UnityTearDownShape
        {
            [UnityTearDown]
            public IEnumerator UnityTearDown()
            {
                yield break;
            }

            public void Run() { }
        }

        private sealed class UnityOneTimeSetUpShape
        {
            [UnityOneTimeSetUp]
            public IEnumerator UnityOneTimeSetUp()
            {
                yield break;
            }

            public void Run() { }
        }

        private sealed class UnityOneTimeTearDownShape
        {
            [UnityOneTimeTearDown]
            public IEnumerator UnityOneTimeTearDown()
            {
                yield break;
            }

            public void Run() { }
        }

        private sealed class TaskSetUpShape
        {
            [SetUp]
            public Task SetUpAsync()
            {
                return Task.CompletedTask;
            }

            public void Run() { }
        }

        [OuterAction]
        private sealed class OuterActionShape
        {
            public void Run() { }
        }

        private sealed class PrebuildSetupShape : IPrebuildSetup
        {
            public void Setup() { }

            public void Run() { }
        }

        private sealed class TakesArguments
        {
            public TakesArguments(int count) { }

            public void Run() { }
        }

        private sealed class SetsUpANamespace
        {
            [OneTimeSetUp]
            public void OneTimeSetUp() { }
        }

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
