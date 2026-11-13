using System;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using NUnit.Framework;
using NUnit.Framework.Interfaces;
using NUnit.Framework.Internal;
using NUnit.Framework.Internal.Commands;
using UnityEngine;
using UnityEngine.TestTools;

namespace Mu3Library.Tests
{
    /// <summary>
    /// Runs an <c>[Explicit]</c> test in one Unity as many times as the environment variable
    /// <see cref="RunsVariable"/> says, once without it, and goes on after a run that does not
    /// pass, so that a failure rate over hundreds of runs of a short test needs neither a Unity
    /// for each run nor a loop written into the test. NUnit's <c>[Repeat]</c> and the Unity Test
    /// Framework's <c>-repeat</c> option stop at the first result that is not a success and report
    /// one result for all the runs.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Each run is the framework's own: the attribute wraps the command that calls the fixture's
    /// <c>[SetUp]</c> methods, base class first, the test's actions, the test with its log check,
    /// and the <c>[TearDown]</c> methods, base class last, in the instance the framework made for
    /// the fixture. The test's timeout counts from each run's start, and each run's result is
    /// its own.
    /// </para>
    /// <para>
    /// Each run logs a line that starts with <see cref="LinePrefix"/>, with its result, how long
    /// it took, and the message of a result that has one, and the last line gives the totals. The
    /// test fails when a run failed, with the totals and the first failure as its message.
    /// </para>
    /// <para>
    /// It holds a <c>[Test]</c> that returns nothing; the framework refuses it on a
    /// <c>[UnityTest]</c>. It refuses a test that is not explicit, which the default run would
    /// then run through it, and a fixture with <c>[UnitySetUp]</c> or <c>[UnityTearDown]</c> or
    /// a test with an <c>IOuterUnityTestAction</c>, which the framework calls outside it, once
    /// around all the runs.
    /// </para>
    /// <para>
    /// <see cref="CountedRepeatRunner"/> runs the same runs and logs the same lines for a test it
    /// finds by its full name, with no test written to repeat it.
    /// </para>
    /// </remarks>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
    internal sealed class CountedRepeatAttribute : NUnitAttribute, IWrapSetUpTearDown
    {
        public const string RunsVariable = "MU3LIBRARY_TEST_REPEAT";
        public const string LinePrefix = "[Repeat]";

        public TestCommand Wrap(TestCommand command)
        {
            return new CountedRepeatCommand(command);
        }

        /// <summary>
        /// Runs <paramref name="execute"/> <paramref name="runs"/> times as runs of
        /// <paramref name="repeated"/>, each with a result of its own and its timeout counted from
        /// its start, until the run of the tests stops, and goes on after a run that does not pass.
        /// Gives <paramref name="log"/> the line that starts the runs, a line for each run, and the
        /// totals, under the full name of <paramref name="repeated"/>, and returns a result of
        /// <paramref name="reported"/> for all the runs, which it also makes the context's.
        /// </summary>
        internal static TestResult RunCounted(ITestExecutionContext context, Test repeated, Test reported, Func<ITestExecutionContext, TestResult> execute, int runs, string source, Action<string> log)
        {
            string name = repeated.FullName;
            log($"{name}: start, runs {runs} ({source})");

            Tally tally = new Tally();
            DateTime startTime = context.StartTime;
            long startTicks = context.StartTicks;
            try
            {
                for (int run = 1; run <= runs && context.ExecutionStatus == TestExecutionStatus.Running; run++)
                {
                    // The framework times a test out from its start, which for a run is the run's.
                    context.StartTime = DateTime.UtcNow;
                    context.StartTicks = Stopwatch.GetTimestamp();
                    context.CurrentResult = repeated.MakeTestResult();
                    try
                    {
                        context.CurrentResult = execute(context);
                    }
                    catch (Exception exception)
                    {
                        context.CurrentResult.RecordException(exception);
                    }

                    double milliseconds = (Stopwatch.GetTimestamp() - context.StartTicks) * 1000.0 / Stopwatch.Frequency;
                    TestResult result = context.CurrentResult;
                    tally.Add(run, result);

                    string line = $"{name}: run {run} of {runs}: {result.ResultState} in {milliseconds.ToString("0.0", CultureInfo.InvariantCulture)} ms";
                    if (!string.IsNullOrEmpty(result.Message))
                    {
                        line += ": " + OneLine(result.Message);
                    }

                    log(line);
                }
            }
            finally
            {
                context.StartTime = startTime;
                context.StartTicks = startTicks;
            }

            string totals = tally.Totals(runs);
            log($"{name}: totals: {totals}");

            TestResult overall = reported.MakeTestResult();
            if (tally.Failed > 0)
            {
                overall.SetResult(ResultState.Failure, $"{totals}; first failure, run {tally.FirstFailureRun}: {tally.FirstFailure.Message}", tally.FirstFailure.StackTrace);
            }
            else if (tally.Ended < runs)
            {
                overall.SetResult(ResultState.Cancelled, totals);
            }
            else if (tally.Inconclusive > 0)
            {
                overall.SetResult(ResultState.Inconclusive, $"{totals}; first inconclusive, run {tally.FirstInconclusiveRun}: {tally.FirstInconclusive.Message}");
            }
            else if (tally.Passed == 0)
            {
                overall.SetResult(ResultState.Ignored, $"{totals}; first skipped, run {tally.FirstSkippedRun}: {tally.FirstSkipped.Message}");
            }
            else
            {
                overall.SetResult(ResultState.Success, totals);
            }

            context.CurrentResult = overall;
            return overall;
        }

        /// <summary>
        /// Reads the number of runs from <see cref="RunsVariable"/>: null, or why its value is not one.
        /// </summary>
        internal static string ReadRuns(out int runs, out string source)
        {
            return ParseRuns(Environment.GetEnvironmentVariable(RunsVariable), out runs, out source);
        }

        /// <summary>
        /// The number of runs that <paramref name="value"/>, the value of <see cref="RunsVariable"/>,
        /// gives, one when it is empty: null, or why it is not one.
        /// </summary>
        internal static string ParseRuns(string value, out int runs, out string source)
        {
            runs = 1;
            source = RunsVariable + " not set";
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            source = RunsVariable + "=" + value;
            if (!int.TryParse(value.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out runs) || runs < 1)
            {
                return $"{RunsVariable} gives the number of runs, a whole number from 1, not '{value}'.";
            }

            return null;
        }

        internal static bool HasUnitySetUpOrTearDown(Type fixture)
        {
            return Reflect.GetMethodsWithAttribute(fixture, typeof(UnitySetUpAttribute), true).Length > 0
                || Reflect.GetMethodsWithAttribute(fixture, typeof(UnityTearDownAttribute), true).Length > 0;
        }

        // The framework takes them from the method and from each suite above it that has a type,
        // as its OuterUnityTestActionCommand does.
        internal static bool HasOuterActions(ITest test)
        {
            if (test.Method != null && test.Method.MethodInfo.GetCustomAttributes(true).OfType<IOuterUnityTestAction>().Any())
            {
                return true;
            }

            for (ITest parent = test.Parent; parent != null; parent = parent.Parent)
            {
                if (parent.TypeInfo != null && parent.TypeInfo.GetCustomAttributes<IOuterUnityTestAction>(true).Length > 0)
                {
                    return true;
                }
            }

            return false;
        }

        internal static void Log(string line)
        {
            UnityEngine.Debug.LogFormat(LogType.Log, LogOption.NoStacktrace, null, "{0} {1}", LinePrefix, line);
        }

        private sealed class CountedRepeatCommand : DelegatingTestCommand
        {
            public CountedRepeatCommand(TestCommand innerCommand)
                : base(innerCommand) { }

            public override TestResult Execute(ITestExecutionContext context)
            {
                string refusal = Refusal(out int runs, out string source);
                if (refusal != null)
                {
                    context.CurrentResult.SetResult(ResultState.NotRunnable, refusal);
                    return context.CurrentResult;
                }

                return RunCounted(context, Test, Test, innerCommand.Execute, runs, source, Log);
            }

            private string Refusal(out int runs, out string source)
            {
                runs = 1;
                source = RunsVariable + " not set";
                if (!IsExplicit(Test))
                {
                    return "[CountedRepeat] holds only an [Explicit] test, which the default run leaves out; mark the test [Explicit].";
                }

                Type fixture = Test.TypeInfo?.Type;
                if (fixture != null && HasUnitySetUpOrTearDown(fixture))
                {
                    return $"[CountedRepeat] calls [SetUp] and [TearDown] in each run, but the framework calls the [UnitySetUp] and [UnityTearDown] of {fixture.Name} once around all the runs.";
                }

                if (HasOuterActions(Test))
                {
                    return $"[CountedRepeat] calls [SetUp] and [TearDown] in each run, but the framework calls the IOuterUnityTestAction attributes of {Test.FullName} once around all the runs.";
                }

                return ReadRuns(out runs, out source);
            }

            private static bool IsExplicit(ITest test)
            {
                for (; test != null; test = test.Parent)
                {
                    if (test.RunState == RunState.Explicit)
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        private sealed class Tally
        {
            public int Ended { get; private set; }
            public int Passed { get; private set; }
            public int Failed { get; private set; }
            public int Inconclusive { get; private set; }
            public int Skipped { get; private set; }
            public int FirstFailureRun { get; private set; }
            public TestResult FirstFailure { get; private set; }
            public int FirstInconclusiveRun { get; private set; }
            public TestResult FirstInconclusive { get; private set; }
            public int FirstSkippedRun { get; private set; }
            public TestResult FirstSkipped { get; private set; }

            public void Add(int run, TestResult result)
            {
                Ended++;
                switch (result.ResultState.Status)
                {
                    case TestStatus.Passed:
                        Passed++;
                        break;
                    case TestStatus.Inconclusive:
                        Inconclusive++;
                        if (FirstInconclusive == null)
                        {
                            FirstInconclusiveRun = run;
                            FirstInconclusive = result;
                        }

                        break;
                    case TestStatus.Skipped:
                        Skipped++;
                        if (FirstSkipped == null)
                        {
                            FirstSkippedRun = run;
                            FirstSkipped = result;
                        }

                        break;
                    default:
                        Failed++;
                        if (FirstFailure == null)
                        {
                            FirstFailureRun = run;
                            FirstFailure = result;
                        }

                        break;
                }
            }

            public string Totals(int runs)
            {
                string rate = Ended > 0
                    ? (100.0 * Failed / Ended).ToString("0.00", CultureInfo.InvariantCulture) + "%"
                    : "none, no run ended";
                return $"runs {Ended} of {runs}, passed {Passed}, failed {Failed}, inconclusive {Inconclusive}, skipped {Skipped}, failure rate {rate}";
            }
        }

        private static string OneLine(string message)
        {
            return string.Join(" | ", message
                .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(part => part.Trim())
                .Where(part => part.Length > 0));
        }
    }
}
