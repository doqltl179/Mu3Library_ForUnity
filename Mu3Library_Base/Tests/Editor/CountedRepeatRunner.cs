using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Threading.Tasks;
using NUnit.Framework;
using NUnit.Framework.Interfaces;
using NUnit.Framework.Internal;
using NUnit.Framework.Internal.Commands;
using NUnit.Framework.Internal.Execution;
using NUnit.Framework.Internal.Filters;
using UnityEngine.TestTools;

namespace Mu3Library.Tests
{
    /// <summary>
    /// Runs the test whose full name the environment variable <see cref="NameVariable"/> gives in one
    /// Unity as many times as <see cref="CountedRepeatAttribute.RunsVariable"/> says, once without it,
    /// as <see cref="CountedRepeatAttribute"/> runs the test it is on, so that a test's failure rate
    /// needs no <c>[Explicit]</c> test written to repeat it (#425).
    /// </summary>
    /// <remarks>
    /// <para>
    /// It finds the test among those the run loaded, every EditMode test of the project, and has the
    /// Unity Test Framework build that test's command as for a run of the test. Each run executes the
    /// part of the command that <c>[CountedRepeat]</c> wraps: the fixture's <c>[SetUp]</c> methods,
    /// base class first, the test's actions, the test with the framework's check for error logs it
    /// did not expect, and the <c>[TearDown]</c> methods, base class last. The runs share one instance
    /// of the fixture, which the runner makes with NUnit's commands, as the framework does: the
    /// constructor, the <c>[OneTimeSetUp]</c> methods, and the fixture's suite actions before the
    /// first run, and the actions, the <c>[OneTimeTearDown]</c> methods, and <c>Dispose</c> after the
    /// last, each checked for error logs as the framework checks them. The attributes of the test and
    /// of its fixture that change the context, such as <c>[Timeout]</c>, apply to every run, and the
    /// timeout counts from each run's start; a <c>[Retry]</c>, <c>[Repeat]</c>, or <c>[MaxTime]</c>
    /// of the test acts within each run, as it does around one run of the test.
    /// </para>
    /// <para>
    /// It goes on after a run that does not pass and logs the lines of <c>[CountedRepeat]</c> under the
    /// repeated test's full name, so test-unity.sh's summary gives that test's totals.
    /// </para>
    /// <para>
    /// Before any run it refuses, as an invalid test: an empty name, a name that no test or more than
    /// one test has, or that names a suite; itself and a test with <c>[CountedRepeat]</c>; a test that
    /// the framework would not run, ignored or invalid; and what it cannot run as the framework does:
    /// a test that returns <c>IEnumerator</c>, as a <c>[UnityTest]</c> does, or a <c>Task</c>; a
    /// fixture that takes arguments; a fixture with <c>[UnitySetUp]</c>, <c>[UnityTearDown]</c>,
    /// <c>[UnityOneTimeSetUp]</c>, or <c>[UnityOneTimeTearDown]</c>, or a <c>[SetUp]</c> or
    /// <c>[TearDown]</c> that returns a <c>Task</c>, which the framework runs over several frames
    /// where each run here takes one call; an <c>IOuterUnityTestAction</c>, which also takes frames;
    /// a <c>[PrebuildSetup]</c> or <c>[PostBuildCleanup]</c> or their interfaces, which the framework
    /// calls before and after the run only for the tests its filter selects; and a
    /// <c>[SetUpFixture]</c> above the test that is not above the runner, which the run never sets up.
    /// The value of <see cref="CountedRepeatAttribute.RunsVariable"/> is refused as for
    /// <c>[CountedRepeat]</c>.
    /// </para>
    /// <para>
    /// It reaches three internals of the Unity Test Framework by reflection, since nothing public
    /// builds a test's command or opens a log scope: <c>TestCommandBuilder.BuildTestCommand</c>, the
    /// command type <c>EnumerableSetUpTearDownCommand</c> under which a run's command sits, and
    /// <c>LogScope</c>. It was written against version 1.8.0 and refuses to run, naming what is
    /// missing, under a version without them.
    /// </para>
    /// </remarks>
    public sealed class CountedRepeatRunner
    {
        public const string NameVariable = "MU3LIBRARY_TEST_REPEAT_NAME";

        [Explicit("Repeats the test that MU3LIBRARY_TEST_REPEAT_NAME names as many times as MU3LIBRARY_TEST_REPEAT says; run it by name.")]
        [Test]
        [RunsNamedTest]
        public void RepeatTheNamedTest()
        {
            Assert.Fail("[RunsNamedTest] runs the named test in place of this method, but the Unity Test Framework called the method.");
        }

        /// <summary>
        /// Finds the test whose full name is <paramref name="name"/> in the tree that holds
        /// <paramref name="from"/>: null, or why there is not exactly one.
        /// </summary>
        internal static string Find(ITest from, string name, out TestMethod target)
        {
            target = null;
            if (string.IsNullOrWhiteSpace(name))
            {
                return $"Set {NameVariable} to the full name of the test to repeat, as the fullname attribute of the result file gives it.";
            }

            name = name.Trim();
            ITest root = from;
            while (root.Parent != null)
            {
                root = root.Parent;
            }

            List<TestMethod> tests = new List<TestMethod>();
            List<ITest> suites = new List<ITest>();
            Collect(root, name, tests, suites);
            if (tests.Count == 1)
            {
                target = tests[0];
                return null;
            }

            if (tests.Count > 1)
            {
                return $"{tests.Count} tests are named '{name}', and the runner repeats one.";
            }

            if (suites.Count > 0)
            {
                return $"'{name}' names a suite of {suites[0].TestCaseCount} tests, not a test; give the full name of one of them.";
            }

            return $"No EditMode test of the project is named '{name}'; give the full name of the test, as the fullname attribute of the result file gives it.";
        }

        /// <summary>
        /// Why the runner <paramref name="runner"/> cannot repeat <paramref name="target"/> as the
        /// framework runs it, or null and the command of one run in <paramref name="run"/>.
        /// </summary>
        internal static string Prepare(ITest runner, TestMethod target, out TestCommand run)
        {
            run = null;
            return Refusal(runner, target) ?? Framework.RunCommand(target, out run);
        }

        /// <summary>
        /// Runs <paramref name="run"/>, the command <see cref="Prepare"/> gave for
        /// <paramref name="target"/>, <paramref name="runs"/> times in one instance of the target's
        /// fixture, with the fixture's one-time setup before and its one-time teardown after, and
        /// returns the result of <paramref name="runner"/> for them. Gives <paramref name="log"/> the
        /// lines of <c>[CountedRepeat]</c> and leaves the context as it found it.
        /// </summary>
        internal static TestResult Repeat(ITestExecutionContext context, Test runner, TestMethod target, TestCommand run, int runs, string source, Action<string> log)
        {
            TestFixture fixture = FixtureOf(target);
            Type type = fixture.TypeInfo.Type;
            List<SetUpTearDownItem> oneTimeSetUpTearDown = CommandBuilder.BuildSetUpTearDownList(type, typeof(OneTimeSetUpAttribute), typeof(OneTimeTearDownAttribute));
            List<TestActionItem> suiteActions = SuiteActions(type);
            TestCommand oneTimeSetUp = CommandBuilder.MakeOneTimeSetUpCommand(fixture, oneTimeSetUpTearDown, suiteActions);
            TestCommand oneTimeTearDown = CommandBuilder.MakeOneTimeTearDownCommand(fixture, oneTimeSetUpTearDown, suiteActions);

            ContextState saved = new ContextState(context);
            object savedFixture = fixture.Fixture;
            TestResult overall;
            try
            {
                // NUnit sets up the instance a suite holds, which the framework leaves there after the
                // fixture's own run, so clearing it gives the runs a new one.
                fixture.Fixture = null;
                context.TestObject = null;
                context.CurrentTest = fixture;
                TestResult fixtureResult = fixture.MakeTestResult();
                fixtureResult.SetResult(ResultState.Success);
                context.CurrentResult = fixtureResult;
                Framework.InLogScope(() => oneTimeSetUp.Execute(context), fixtureResult, FailureSite.SetUp);

                if (fixtureResult.ResultState.Status == TestStatus.Passed)
                {
                    foreach (IApplyToContext change in target.Method.GetCustomAttributes<IApplyToContext>(true))
                    {
                        change.ApplyToContext(context);
                    }

                    context.CurrentTest = target;
                    overall = CountedRepeatAttribute.RunCounted(context, target, runner, current => Framework.RunWithinOneCall(run, current), runs, source, log);
                }
                else
                {
                    // The framework skips the fixture's tests with this result.
                    overall = runner.MakeTestResult();
                    overall.SetResult(fixtureResult.ResultState.WithSite(FailureSite.Parent), "OneTimeSetUp: " + fixtureResult.Message, fixtureResult.StackTrace);
                }

                if (context.ExecutionStatus != TestExecutionStatus.AbortRequested)
                {
                    context.CurrentTest = fixture;
                    context.CurrentResult = fixtureResult;
                    Framework.InLogScope(() => oneTimeTearDown.Execute(context), fixtureResult, FailureSite.TearDown);
                    if (fixtureResult.ResultState.Site == FailureSite.TearDown)
                    {
                        // The framework fails the fixture; the runner's result is all there is to fail.
                        string message = string.IsNullOrEmpty(overall.Message) ? string.Empty : overall.Message + "; ";
                        ResultState state = overall.ResultState.Status == TestStatus.Failed ? overall.ResultState : fixtureResult.ResultState;
                        overall.SetResult(state, message + "OneTimeTearDown: " + fixtureResult.Message, overall.StackTrace ?? fixtureResult.StackTrace);
                    }
                }
            }
            finally
            {
                fixture.Fixture = savedFixture;
                saved.Restore(context);
            }

            return overall;
        }

        private static void Collect(ITest test, string name, List<TestMethod> tests, List<ITest> suites)
        {
            if (string.Equals(test.FullName, name, StringComparison.Ordinal))
            {
                if (test is TestMethod method)
                {
                    tests.Add(method);
                }
                else
                {
                    suites.Add(test);
                }
            }

            if (test.HasChildren)
            {
                foreach (ITest child in test.Tests)
                {
                    Collect(child, name, tests, suites);
                }
            }
        }

        private static string Refusal(ITest runner, TestMethod target)
        {
            string name = target.FullName;
            if (ReferenceEquals(target, runner) || string.Equals(name, runner.FullName, StringComparison.Ordinal))
            {
                return $"{name} is this runner; give the full name of the test to repeat.";
            }

            if (target.Method.IsDefined<CountedRepeatAttribute>(true))
            {
                return $"{name} repeats a test with [CountedRepeat]; give the full name of the test it calls, or run {name} by its own name.";
            }

            for (ITest test = target; test != null; test = test.Parent)
            {
                if (test.RunState != RunState.Runnable && test.RunState != RunState.Explicit)
                {
                    string reason = test.Properties.Get(PropertyNames.SkipReason) as string;
                    return $"The framework would not run {name}: {test.FullName} is {test.RunState}" + (string.IsNullOrEmpty(reason) ? "." : $": {reason}");
                }
            }

            Type returnType = target.Method.ReturnType.Type;
            if (returnType == typeof(IEnumerator))
            {
                return $"{name} returns IEnumerator, as a [UnityTest] does, so the framework runs it over several frames; each run of the runner takes one call, as a run of [CountedRepeat] does.";
            }

            if (returnType == typeof(Task))
            {
                return $"{name} returns a Task, which the framework waits for over several frames; each run of the runner takes one call, as a run of [CountedRepeat] does.";
            }

            TestFixture fixture = FixtureOf(target);
            if (fixture == null)
            {
                return $"{name} is in no test fixture.";
            }

            if (fixture.Arguments != null && fixture.Arguments.Length > 0)
            {
                return $"{fixture.FullName} takes arguments, and the runner makes only a fixture that takes none; repeat {target.Name} with [CountedRepeat] on an [Explicit] test of that fixture.";
            }

            Type type = fixture.TypeInfo.Type;
            if (CountedRepeatAttribute.HasUnitySetUpOrTearDown(type))
            {
                return $"{type.Name} has [UnitySetUp] or [UnityTearDown], which the framework runs over several frames around each run of {target.Name}; each run of the runner takes one call.";
            }

            if (HasMethodWith(type, typeof(UnityOneTimeSetUpAttribute)) || HasMethodWith(type, typeof(UnityOneTimeTearDownAttribute)))
            {
                return $"{type.Name} has [UnityOneTimeSetUp] or [UnityOneTimeTearDown], which the framework runs over several frames; the runner sets up and tears down the fixture in one call each.";
            }

            MethodInfo waits = Reflect.GetMethodsWithAttribute(type, typeof(SetUpAttribute), true)
                .Concat(Reflect.GetMethodsWithAttribute(type, typeof(TearDownAttribute), true))
                .FirstOrDefault(method => method.ReturnType == typeof(Task));
            if (waits != null)
            {
                return $"{type.Name}.{waits.Name} is a [SetUp] or [TearDown] that returns a Task, which the framework waits for over several frames; each run of the runner takes one call, in which a task that continues on Unity's main thread never ends.";
            }

            if (CountedRepeatAttribute.HasOuterActions(target))
            {
                return $"{name} has IOuterUnityTestAction attributes, on its method or on a class above it, which the framework runs over several frames around each run; each run of the runner takes one call.";
            }

            if (HasBuildSteps(target.Method.MethodInfo, type))
            {
                return $"{name} has a [PrebuildSetup] or [PostBuildCleanup], or its fixture implements IPrebuildSetup or IPostBuildCleanup, which the framework calls before and after a run only for the tests its filter selects, and the runner's filter selects the runner.";
            }

            ITest setUpFixture = SetUpFixtureAbove(fixture, runner);
            if (setUpFixture != null)
            {
                return $"{setUpFixture.FullName} is a [SetUpFixture] above {name} but not above the runner, so a run of the runner never sets it up.";
            }

            return null;
        }

        private static TestFixture FixtureOf(ITest test)
        {
            for (ITest suite = test.Parent; suite != null; suite = suite.Parent)
            {
                if (suite is TestFixture fixture)
                {
                    return fixture;
                }
            }

            return null;
        }

        private static bool HasMethodWith(Type type, Type attribute)
        {
            return Reflect.GetMethodsWithAttribute(type, attribute, true).Length > 0;
        }

        // The framework's finders take the attributes from the test's method and its fixture, and the
        // interfaces from the fixture.
        private static bool HasBuildSteps(MethodInfo method, Type fixture)
        {
            Type[] attributes =
            {
                typeof(PrebuildSetupAttribute),
                typeof(PostBuildCleanupAttribute),
                typeof(PrebuildSetupWithTestDataAttribute),
                typeof(PostBuildCleanupWithTestDataAttribute),
            };
            Type[] interfaces =
            {
                typeof(IPrebuildSetup),
                typeof(IPostBuildCleanup),
                typeof(IPrebuildSetupWithTestData),
                typeof(IPostbuildCleanupWithTestData),
            };
            return attributes.Any(attribute => method.IsDefined(attribute, true) || fixture.IsDefined(attribute, true))
                || interfaces.Any(contract => contract.IsAssignableFrom(fixture));
        }

        private static ITest SetUpFixtureAbove(ITest fixture, ITest runner)
        {
            HashSet<ITest> runnerSuites = new HashSet<ITest>();
            for (ITest suite = runner.Parent; suite != null; suite = suite.Parent)
            {
                runnerSuites.Add(suite);
            }

            for (ITest suite = fixture.Parent; suite != null; suite = suite.Parent)
            {
                if (suite is SetUpFixture && !runnerSuites.Contains(suite))
                {
                    return suite;
                }
            }

            return null;
        }

        // As the framework's CompositeWorkItem takes them for a fixture.
        private static List<TestActionItem> SuiteActions(Type fixture)
        {
            List<TestActionItem> actions = new List<TestActionItem>();
            foreach (ITestAction action in ActionsHelper.GetActionsFromTypesAttributes(fixture))
            {
                if ((action.Targets & ActionTargets.Suite) == ActionTargets.Suite || action.Targets == ActionTargets.Default)
                {
                    actions.Add(new TestActionItem(action));
                }
            }

            return actions;
        }

        [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
        private sealed class RunsNamedTestAttribute : NUnitAttribute, IWrapSetUpTearDown
        {
            public TestCommand Wrap(TestCommand command)
            {
                return new RunsNamedTestCommand(command);
            }
        }

        // Takes the place of the runner's own setup, test, and teardown, which it never calls.
        private sealed class RunsNamedTestCommand : DelegatingTestCommand
        {
            public RunsNamedTestCommand(TestCommand innerCommand)
                : base(innerCommand) { }

            public override TestResult Execute(ITestExecutionContext context)
            {
                string refusal = Find(Test, Environment.GetEnvironmentVariable(NameVariable), out TestMethod target);
                int runs = 0;
                string source = null;
                TestCommand run = null;
                if (refusal == null)
                {
                    refusal = CountedRepeatAttribute.ReadRuns(out runs, out source);
                }

                if (refusal == null)
                {
                    refusal = Prepare(Test, target, out run);
                }

                if (refusal != null)
                {
                    context.CurrentResult.SetResult(ResultState.NotRunnable, refusal);
                    return context.CurrentResult;
                }

                TestResult overall = Repeat(context, Test, target, run, runs, source, CountedRepeatAttribute.Log);
                context.CurrentResult = overall;
                return overall;
            }
        }

        // What the runs and the fixture's setup change in the runner's context.
        private sealed class ContextState
        {
            private readonly Test _currentTest;
            private readonly TestResult _currentResult;
            private readonly object _testObject;
            private readonly DateTime _startTime;
            private readonly long _startTicks;
            private readonly int _testCaseTimeout;
            private readonly CultureInfo _currentCulture;
            private readonly CultureInfo _currentUICulture;
            private readonly ParallelScope _parallelScope;
            private readonly bool _isSingleThreaded;

            public ContextState(ITestExecutionContext context)
            {
                _currentTest = context.CurrentTest;
                _currentResult = context.CurrentResult;
                _testObject = context.TestObject;
                _startTime = context.StartTime;
                _startTicks = context.StartTicks;
                _testCaseTimeout = context.TestCaseTimeout;
                _currentCulture = context.CurrentCulture;
                _currentUICulture = context.CurrentUICulture;
                _parallelScope = context.ParallelScope;
                _isSingleThreaded = context.IsSingleThreaded;
            }

            public void Restore(ITestExecutionContext context)
            {
                context.CurrentTest = _currentTest;
                context.CurrentResult = _currentResult;
                context.TestObject = _testObject;
                context.StartTime = _startTime;
                context.StartTicks = _startTicks;
                context.TestCaseTimeout = _testCaseTimeout;
                context.CurrentCulture = _currentCulture;
                context.CurrentUICulture = _currentUICulture;
                context.ParallelScope = _parallelScope;
                context.IsSingleThreaded = _isSingleThreaded;
            }
        }

        // The internals of the Unity Test Framework that the runner reaches by reflection.
        private static class Framework
        {
            private const string RunnerNamespace = "UnityEngine.TestRunner.NUnitExtensions.Runner";

            // The framework puts this command around the part of a test's command that each
            // IWrapSetUpTearDown, [CountedRepeat] among them, wraps, and runs a test's [UnitySetUp]
            // and [UnityTearDown] in it.
            private const string RunBoundary = "UnityEngine.TestTools.EnumerableSetUpTearDownCommand";

            private static readonly Assembly TestRunner = typeof(UnitySetUpAttribute).Assembly;
            private static readonly MethodInfo BuildTestCommand = TestRunner.GetType(RunnerNamespace + ".TestCommandBuilder")
                ?.GetMethod("BuildTestCommand", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static, null, new[] { typeof(TestMethod), typeof(ITestFilter) }, null);
            private static readonly Type EnumerableCommand = TestRunner.GetType(RunnerNamespace + ".IEnumerableTestMethodCommand");
            private static readonly MethodInfo ExecuteEnumerable = EnumerableCommand?.GetMethod("ExecuteEnumerable", new[] { typeof(ITestExecutionContext) });
            private static readonly Type LogScope = TestRunner.GetType("UnityEngine.TestTools.Logging.LogScope");
            private static readonly MethodInfo EvaluateLogScope = LogScope?.GetMethod("EvaluateLogScope", new[] { typeof(bool) });
            private static readonly FieldInfo InnerCommand = typeof(DelegatingTestCommand).GetField("innerCommand", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

            public static string RunCommand(TestMethod test, out TestCommand run)
            {
                run = null;
                string missing = Missing();
                if (missing != null)
                {
                    return $"The runner runs a test through {missing} of the Unity Test Framework, which this version of it lacks; the runner was written against version 1.8.0.";
                }

                TestCommand command;
                try
                {
                    command = (TestCommand)BuildTestCommand.Invoke(null, new object[] { test, new FullNameFilter(test.FullName) });
                }
                catch (TargetInvocationException exception)
                {
                    return $"The Unity Test Framework could not build the command of {test.FullName}: {(exception.InnerException ?? exception).Message}";
                }

                for (TestCommand current = command; current is DelegatingTestCommand; current = (TestCommand)InnerCommand.GetValue(current))
                {
                    if (current.GetType().FullName == RunBoundary)
                    {
                        run = (TestCommand)InnerCommand.GetValue(current);
                        return null;
                    }
                }

                return $"The Unity Test Framework built a {command.GetType().Name} for {test.FullName}, with no {RunBoundary} in it to find the command of a run under.";
            }

            // As the framework's ImmediateEnumerableCommand runs what [CountedRepeat] wraps.
            public static TestResult RunWithinOneCall(TestCommand command, ITestExecutionContext context)
            {
                if (!EnumerableCommand.IsInstanceOfType(command))
                {
                    return command.Execute(context);
                }

                IEnumerable steps;
                try
                {
                    steps = (IEnumerable)ExecuteEnumerable.Invoke(command, new object[] { context });
                }
                catch (TargetInvocationException exception) when (exception.InnerException != null)
                {
                    ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
                    throw;
                }

                foreach (object step in steps)
                {
                    if (step != null)
                    {
                        throw new Exception("Only null can be yielded at this point.");
                    }
                }

                return context.CurrentResult;
            }

            // As the framework's CompositeWorkItem runs a fixture's one-time setup and teardown.
            public static void InLogScope(Action action, TestResult result, FailureSite site)
            {
                IDisposable scope = (IDisposable)Activator.CreateInstance(LogScope);
                try
                {
                    action();
                    EvaluateLogScope.Invoke(scope, new object[] { true });
                }
                catch (Exception exception)
                {
                    if ((exception is NUnitException || exception is TargetInvocationException) && exception.InnerException != null)
                    {
                        exception = exception.InnerException;
                    }

                    result.RecordException(exception, site);
                }
                finally
                {
                    scope.Dispose();
                }
            }

            private static string Missing()
            {
                if (BuildTestCommand == null || !typeof(TestCommand).IsAssignableFrom(BuildTestCommand.ReturnType))
                {
                    return "TestCommandBuilder.BuildTestCommand(TestMethod, ITestFilter)";
                }

                if (ExecuteEnumerable == null)
                {
                    return "IEnumerableTestMethodCommand.ExecuteEnumerable(ITestExecutionContext)";
                }

                if (LogScope == null || EvaluateLogScope == null || LogScope.GetConstructor(Type.EmptyTypes) == null || !typeof(IDisposable).IsAssignableFrom(LogScope))
                {
                    return "LogScope with EvaluateLogScope(bool)";
                }

                if (InnerCommand == null || !typeof(TestCommand).IsAssignableFrom(InnerCommand.FieldType))
                {
                    return "DelegatingTestCommand.innerCommand";
                }

                return null;
            }
        }
    }
}
