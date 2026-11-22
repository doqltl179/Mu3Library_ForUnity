using System;
using System.Collections.Generic;
using System.Linq;
using Mu3Library.DebugConsole;
using NUnit.Framework;
using UnityEngine;

namespace Mu3Library.Tests
{
    public class DebugConsoleManagerTests
    {
        public enum Difficulty
        {
            Easy,
            Normal,
            Hard,
        }

        private readonly List<DebugConsoleManager> _consoles = new();



        [TearDown]
        public void TearDown()
        {
            foreach (DebugConsoleManager console in _consoles)
            {
                console.Dispose();
            }

            _consoles.Clear();
        }

        private DebugConsoleManager Create(bool development = true, bool captureUnityLog = false)
        {
            var console = new DebugConsoleManager(captureUnityLog, development);
            _consoles.Add(console);
            return console;
        }

        private static (int Item, int Count) _given;

        private static void Give(int item, int count = 1)
        {
            _given = (item, count);
        }

        [Test]
        public void TypedCommand_ReadsEachParameterType()
        {
            DebugConsoleManager console = Create();
            (int, float, bool) numbers = default;
            (string, Difficulty, Vector3) others = default;
            console.RegisterCommand<int, float, bool>("numbers", (i, f, b) => numbers = (i, f, b));
            console.RegisterCommand<string, Difficulty, Vector3>("others", (s, d, v) => others = (s, d, v));

            Assert.That(console.Execute("numbers 3 -1.5 on").IsSuccess);
            Assert.That(console.Execute("others \"two words\" hard 1,2.5,-3").IsSuccess);

            Assert.That(numbers, Is.EqualTo((3, -1.5f, true)));
            Assert.That(others.Item1, Is.EqualTo("two words"));
            Assert.That(others.Item2, Is.EqualTo(Difficulty.Hard));
            Assert.That(others.Item3, Is.EqualTo(new Vector3(1f, 2.5f, -3f)));
        }

        [Test]
        public void MethodWithDefaultParameter_LetsTheLineLeaveItOut()
        {
            DebugConsoleManager console = Create();
            console.RegisterCommand<int, int>("give", Give);

            Assert.That(console.Execute("give 7").IsSuccess);
            Assert.That(_given, Is.EqualTo((7, 1)));
            Assert.That(console.Execute("give 7 5").IsSuccess);
            Assert.That(_given, Is.EqualTo((7, 5)));
            Assert.That(console.TryGetCommand("give", out ConsoleCommand command));
            Assert.That(command.Usage, Is.EqualTo("give <item:int> [count:int=1]"));
        }

        [Test]
        public void WrongLines_FailWithTheirReason_AndPrintAnError()
        {
            DebugConsoleManager console = Create();
            console.RegisterCommand<int>("level", _ => { });

            Assert.That(console.Execute("nothing").Reason, Is.EqualTo(ConsoleFailureReason.UnknownCommand));
            Assert.That(console.Execute("level").Reason, Is.EqualTo(ConsoleFailureReason.InvalidArguments));
            Assert.That(console.Execute("level 1 2").Reason, Is.EqualTo(ConsoleFailureReason.InvalidArguments));
            Assert.That(console.Execute("level high").Reason, Is.EqualTo(ConsoleFailureReason.InvalidArguments));
            Assert.That(console.Execute("level \"1").Reason, Is.EqualTo(ConsoleFailureReason.SyntaxError));
            Assert.That(console.Execute("   ").Reason, Is.EqualTo(ConsoleFailureReason.Empty));

            Assert.That(console.Lines.Count(line => line.Kind == ConsoleLineKind.Error), Is.EqualTo(5));
            Assert.That(console.Lines.Last().Text, Does.Contain("quote"));
        }

        [Test]
        public void CommandThatThrows_FailsWithCommandFailed()
        {
            DebugConsoleManager console = Create();
            console.RegisterCommand("boom", () => throw new InvalidOperationException("broken"));

            ConsoleResult result = console.Execute("boom");

            Assert.That(result.Reason, Is.EqualTo(ConsoleFailureReason.CommandFailed));
            Assert.That(result.Message, Does.Contain("broken"));
        }

        [Test]
        public void RawCommand_GetsTheWordsAsTyped()
        {
            DebugConsoleManager console = Create();
            IReadOnlyList<string> words = null;
            console.RegisterCommand("say", call =>
            {
                words = call.Arguments;
                call.Print(string.Join("|", call.Arguments));
            });

            Assert.That(console.Execute("SAY hello \"big \\\"world\\\"\"").IsSuccess, "Names match without case.");

            Assert.That(words, Is.EqualTo(new[] { "hello", "big \"world\"" }));
            Assert.That(console.Lines.Last().Kind, Is.EqualTo(ConsoleLineKind.Output));
            Assert.That(console.Lines.Last().Text, Is.EqualTo("hello|big \"world\""));
        }

        [Test]
        public void ReleaseBuild_RunsNothingUntilThePlayerConsoleIsOn()
        {
            DebugConsoleManager console = Create(development: false);
            int developer = 0;
            int player = 0;
            console.RegisterCommand("dev", () => developer++);
            console.RegisterCommand("perk", () => player++, new ConsoleCommandOptions("A perk.", ConsoleAccess.Player));

            Assert.That(console.CanOpen, Is.False);
            Assert.That(console.Open(), Is.False);
            Assert.That(console.Execute("perk").Reason, Is.EqualTo(ConsoleFailureReason.AccessDenied));

            console.PlayerConsoleEnabled = true;

            Assert.That(console.Open(), Is.True);
            Assert.That(console.Execute("perk").IsSuccess);
            Assert.That(console.Execute("dev").Reason, Is.EqualTo(ConsoleFailureReason.AccessDenied));
            Assert.That(player, Is.EqualTo(1));
            Assert.That(developer, Is.EqualTo(0));
        }

        [Test]
        public void DevelopmentBuild_RunsEveryCommand()
        {
            DebugConsoleManager console = Create(development: true);
            int runs = 0;
            console.RegisterCommand("dev", () => runs++);
            console.RegisterCommand("perk", () => runs++, new ConsoleCommandOptions("A perk.", ConsoleAccess.Player));

            Assert.That(console.CanOpen);
            Assert.That(console.Execute("dev").IsSuccess);
            Assert.That(console.Execute("perk").IsSuccess);
            Assert.That(runs, Is.EqualTo(2));
        }

        [Test]
        public void HelpAndCompletion_ListOnlyRunnableCommands()
        {
            DebugConsoleManager console = Create(development: false);
            console.PlayerConsoleEnabled = true;
            console.RegisterCommand("god", () => { }, new ConsoleCommandOptions("Developer only."));
            console.RegisterCommand("gold", () => { }, new ConsoleCommandOptions("Adds gold.", ConsoleAccess.Player));

            var completions = new List<string>();
            console.GetCompletions("go", completions);
            Assert.That(completions, Is.EqualTo(new[] { "gold" }));

            console.Clear();
            Assert.That(console.Execute("help").IsSuccess);
            string printed = string.Join("\n", console.Lines.Select(line => line.Text));
            Assert.That(printed, Does.Contain("gold - Adds gold."));
            Assert.That(printed, Does.Not.Contain("god -"));
            Assert.That(printed, Does.Contain("help"));
        }

        [Test]
        public void CommandThatAffectsProgress_MarksTheConsole()
        {
            DebugConsoleManager console = Create();
            var modified = new List<string>();
            console.OnProgressModified += modified.Add;
            console.RegisterCommand("look", () => { });
            console.RegisterCommand<int>("skip", _ => { }, new ConsoleCommandOptions("Skips stages.", affectsProgress: true));

            console.Execute("look");
            Assert.That(console.HasModifiedProgress, Is.False);
            console.Execute("skip x");
            Assert.That(console.HasModifiedProgress, Is.False, "A command that failed changed nothing.");

            console.Execute("skip 2");
            Assert.That(console.HasModifiedProgress);
            Assert.That(modified, Is.EqualTo(new[] { "skip" }));

            console.ResetProgressModified();
            Assert.That(console.HasModifiedProgress, Is.False);
        }

        [Test]
        public void Registration_RefusesBadNamesDuplicatesAndUnreadableTypes()
        {
            DebugConsoleManager console = Create();

            Assert.Throws<ArgumentException>(() => console.RegisterCommand("two words", () => { }));
            Assert.Throws<ArgumentException>(() => console.RegisterCommand("HELP", () => { }));
            Assert.Throws<ArgumentException>(() => console.RegisterCommand<List<int>>("list", _ => { }));

            console.RegisterCommand("once", () => { });
            Assert.That(console.UnregisterCommand("ONCE"));
            Assert.That(console.TryGetCommand("once", out _), Is.False);
        }

        [Test]
        public void UnityLog_IsCaptured_UntilDisposed()
        {
            DebugConsoleManager console = Create(captureUnityLog: true);

            Debug.Log("console test log");
            Debug.LogWarning("console test warning");

            Assert.That(console.Lines.Any(line => line.Kind == ConsoleLineKind.Log && line.Text == "console test log"));
            Assert.That(console.Lines.Any(line => line.Kind == ConsoleLineKind.Warning && line.Text == "console test warning"));

            console.Dispose();
            int count = console.Lines.Count;
            Debug.Log("after dispose");
            Assert.That(console.Lines.Count, Is.EqualTo(count));
        }

        [Test]
        public void Lines_KeepOnlyTheNewest_AndHistorySkipsRepeats()
        {
            DebugConsoleManager console = Create();
            console.MaxLines = 3;
            for (int i = 0; i < 5; i++)
            {
                console.Print($"line {i}");
            }

            Assert.That(console.Lines.Select(line => line.Text), Is.EqualTo(new[] { "line 2", "line 3", "line 4" }));

            console.Execute("help");
            console.Execute("help");
            console.Execute("clear");
            Assert.That(console.History, Is.EqualTo(new[] { "help", "clear" }));
            Assert.That(console.Lines, Is.Empty, "clear empties the console.");
        }

        [Test]
        public void OpenAndClose_RaiseOnlyOnChange()
        {
            DebugConsoleManager console = Create();
            var changes = new List<bool>();
            console.OnOpenChanged += changes.Add;

            console.Open();
            console.Open();
            console.Toggle();
            console.Close();

            Assert.That(changes, Is.EqualTo(new[] { true, false }));
        }
    }
}
