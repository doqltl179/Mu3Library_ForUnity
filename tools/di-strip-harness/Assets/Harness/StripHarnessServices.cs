using System;
using System.Collections.Generic;
using Mu3Library.DI;
using StripHarness.Domain;
using StripHarness.Plugin;
using UnityEngine.Scripting;

// The services of the DI stripping harness (tools/README.md#di-stripping-harness). No game code
// creates them with `new`: only the container does, through reflection, which the Unity linker
// cannot see, so a player built with Managed Stripping Level Medium or High keeps them only
// through the link.xml that Base's DILinkerProcessor writes.
namespace StripHarness
{
    public static class Probe
    {
        private static readonly Dictionary<string, int> _hits = new();

        public static void Hit(string name)
        {
            _hits.TryGetValue(name, out int count);
            _hits[name] = count + 1;
        }

        public static int Count(string name)
        {
            return _hits.TryGetValue(name, out int count) ? count : 0;
        }
    }

    public interface IClock
    {
        int Now { get; }
    }

    // Default constructor only; registered with RegisterClass<T>() (new() constraint).
    public sealed class Clock : IClock, IInitializable, IUpdatable, ILateUpdatable, IDisposable
    {
        public int Now => 42;

        public void Initialize() => Probe.Hit("Clock.Initialize");
        public void Update() => Probe.Hit("Clock.Update");
        public void LateUpdate() => Probe.Hit("Clock.LateUpdate");
        public void Dispose() => Probe.Hit("Clock.Dispose");
    }

    public interface IGreeter
    {
        string CtorUsed { get; }
        string Greet();
    }

    // RegisterClass<T>() needs the default constructor; the container prefers the longest
    // constructor it can fill, so it must call Greeter(IClock).
    public sealed class Greeter : IGreeter
    {
        private readonly IClock _clock;
        public string CtorUsed { get; }

        public Greeter()
        {
            CtorUsed = "default";
        }

        public Greeter(IClock clock)
        {
            _clock = clock;
            CtorUsed = "IClock";
        }

        public string Greet() => _clock == null ? "no clock" : "hello " + _clock.Now;
    }

    public interface IScore
    {
        int Value { get; }
    }

    // Registered with ContainerScope.Register<IScore, Score>(key), which has no new() constraint.
    public sealed class Score : IScore
    {
        public int Value => 7;
    }

    public interface IPlugin
    {
        string Name { get; }
    }

    public sealed class PluginA : IPlugin
    {
        public string Name => "A";
    }

    public sealed class PluginB : IPlugin
    {
        public string Name => "B";
    }

    public interface IOnlyCtor
    {
        IClock Clock { get; }
    }

    // Only a non-default constructor; registered with ContainerScope.Register<IOnlyCtor, OnlyCtor>().
    public sealed class OnlyCtor : IOnlyCtor, IInitializable
    {
        public IClock Clock { get; }

        public OnlyCtor(IClock clock)
        {
            Clock = clock;
        }

        public void Initialize() => Probe.Hit("OnlyCtor.Initialize");
    }

    public interface IFactoryMade
    {
        IClock Clock { get; }
    }

    public sealed class FactoryMade : IFactoryMade
    {
        [Inject] private IClock _clock = null;
        public IClock Clock => _clock;
    }

    public interface IPreMade
    {
        IClock Clock { get; }
    }

    public sealed class PreMade : IPreMade
    {
        [Inject] public IClock Clock { get; private set; }
    }

    public sealed class Manual
    {
        [Inject] public IClock Clock { get; set; }
    }

    // Required static [Inject] members, which the container never fills: it throws for each
    // before it injects anything, but only when it finds the member. No code reads or writes
    // them, and the fields have no initializer, so nothing but the link.xml keeps them in a
    // stripped player. Their names appear nowhere else but in the messages the boot expects.
#pragma warning disable CS0169 // never used: only reflection reaches them
    public sealed class StaticFieldHolder
    {
        [Inject] private static IClock _strippedStaticClock;
    }

    public sealed class StaticPropertyHolder
    {
        [Inject] public static IClock StrippedStaticClock { get; set; }
    }

    public abstract class StaticFieldHolderBase
    {
        [Inject] private static IClock _strippedBaseStaticClock;
    }
#pragma warning restore CS0169

    public sealed class DerivedStaticFieldHolder : StaticFieldHolderBase
    {
    }

    public interface IUnreadFieldService
    {
    }

    // Created only to fill UnreadFieldHolder's field, so its constructor counts the injections.
    public sealed class UnreadFieldService : IUnreadFieldService
    {
        public UnreadFieldService() => Probe.Hit("UnreadFieldService.ctor");
    }

    // An instance [Inject] field that no code reads, such as one that only makes the container
    // create a service and start its lifecycle. The linker keeps the [Inject] attribute of a
    // field that code reads, so no code reads or writes this one, and it has no initializer: only
    // the link.xml keeps its attribute in a stripped player. Its name appears nowhere else.
#pragma warning disable CS0169 // never used: only reflection reaches it
    public sealed class UnreadFieldHolder
    {
        [Inject] private IUnreadFieldService _injectedButUnread;
    }
#pragma warning restore CS0169

    public interface IUnreadPropertyService
    {
    }

    // Created only to fill UnreadPropertyHolder's property, so its constructor counts the injections.
    public sealed class UnreadPropertyService : IUnreadPropertyService
    {
        public UnreadPropertyService() => Probe.Hit("UnreadPropertyService.ctor");
    }

    // The same for an instance [Inject] auto-property with a private setter. No code calls its
    // getter or setter, so nothing but the link.xml keeps the property, its setter, and its
    // [Inject] attribute in a stripped player. Its name appears nowhere else.
    public sealed class UnreadPropertyHolder
    {
        [Inject] public IUnreadPropertyService InjectedButUnreadProperty { get; private set; }
    }

    // Registers through the ContainerScope a constructor receives. RegisterClass<T>() needs the
    // default constructor, and the container must call Installer(ContainerScope).
    public sealed class Installer
    {
        public bool Installed { get; }

        public Installer()
        {
        }

        public Installer(ContainerScope scope)
        {
            scope.Register<IScore, Score>(ServiceLifetime.Singleton, "special");
            scope.Register<IPlugin, PluginA>();
            scope.Register<IPlugin, PluginB>();
            scope.Register<IOnlyCtor, OnlyCtor>();
            scope.RegisterFactory<IFactoryMade>(_ => new FactoryMade());
            scope.RegisterInstance<IPreMade>(new PreMade());
            scope.Register<IRepo, Repo>();
            scope.Register<IScoreService, ScoreService>();
            scope.Register<IPluginRepo, PluginRepo>();
            scope.Register<IPluginService, PluginService>();
            scope.Register<INested, Outer.NestedService>();
            scope.Register<IUnreadFieldService, UnreadFieldService>(ServiceLifetime.Transient);
            scope.Register<IUnreadPropertyService, UnreadPropertyService>(ServiceLifetime.Transient);
#if !UNITY_EDITOR
            scope.Register<IPlayerOnly, PlayerOnlyService>();
#endif
            Installed = true;
        }
    }

    public interface IRemote
    {
        string Where { get; }
    }

    public sealed class Remote : IRemote
    {
        public string Where => "other";
    }

    public interface IMissing
    {
    }

    public interface INested
    {
        IClock Clock { get; }
    }

    public static class Outer
    {
        public sealed class NestedService : INested
        {
            public NestedService(IClock clock)
            {
                Clock = clock;
            }

            public IClock Clock { get; }
        }
    }

    public interface IPlayerOnly
    {
        IClock Clock { get; }
    }

#if !UNITY_EDITOR
    // Compiled only into the player: the editor domain never sees it.
    public sealed class PlayerOnlyService : IPlayerOnly
    {
        public PlayerOnlyService(IClock clock)
        {
            Clock = clock;
        }

        public IClock Clock { get; }
    }
#endif

#if UNITY_EDITOR
    // Compiled only into the editor: a link.xml entry for it names a type the player lacks.
    public sealed class EditorOnlyService
    {
        public EditorOnlyService(IClock clock)
        {
        }

        [Inject] public IClock Clock { get; private set; }
    }
#endif

    // Referenced by nothing: the build should still drop it, link.xml entries included.
    public sealed class UnusedService
    {
        public UnusedService(IClock clock)
        {
        }

        [Inject] public IClock Clock { get; private set; }
    }

    public abstract class ConsumerBase
    {
        [Inject] private IClock _baseClock = null;
        public IClock BaseClock => _baseClock;
    }

    public sealed class Consumer : ConsumerBase, IInitializable
    {
        [Inject] private IGreeter _greeter = null;
        [Inject] public IClock AutoProperty { get; private set; }
        private IClock _explicit;
        [Inject] public IClock ExplicitProperty { get => _explicit; set => _explicit = value; }
        [Inject(true, "special")] private IScore _special = null;
        [Inject(typeof(HarnessOtherCore))] private IRemote _remote = null;
        [Inject(false)] private IMissing _missing = null;
        [Inject] private IEnumerable<IPlugin> _plugins = null;
        [Inject] private Func<IClock> _clockFactory = null;
        [Inject] private Lazy<IGreeter> _lazyGreeter = null;
        [Inject] private IObjectInjector _injector = null;
#if !UNITY_EDITOR
        // Compiled only into the player, so the editor's link.xml cannot list them: the README
        // says such a member needs [Preserve]; the second one shows what happens without it.
        [Preserve, Inject] public IClock PlayerOnlyPreserved { get; private set; }
        [Inject(false)] public IClock PlayerOnlyUnpreserved { get; private set; }
#endif

        public IGreeter Greeter => _greeter;
        public IScore Special => _special;
        public IRemote Remote => _remote;
        public IMissing Missing => _missing;
        public IEnumerable<IPlugin> Plugins => _plugins;
        public Func<IClock> ClockFactory => _clockFactory;
        public Lazy<IGreeter> LazyGreeter => _lazyGreeter;
        public IObjectInjector Injector => _injector;

        public void Initialize() => Probe.Hit("Consumer.Initialize");
    }
}
