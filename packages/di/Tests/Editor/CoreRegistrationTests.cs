using System;
using System.Collections.Generic;
using Mu3Library.DI;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Mu3Library.Tests
{
    public class CoreRegistrationTests
    {
        private sealed class EventLog
        {
            public readonly List<string> Entries = new();
        }

        private interface IGreeter
        {
            EventLog Log { get; }
        }

        // Needs its constructor parameter, so RegisterClass<T>() with its new() constraint cannot
        // register it.
        private sealed class Greeter : IGreeter, IInitializable, IUpdatable
        {
            public EventLog Log { get; }

            public Greeter(EventLog log)
            {
                Log = log;
                log.Entries.Add("created");
            }

            public void Initialize() => Log.Entries.Add("initialized");
            public void Update() => Log.Entries.Add("updated");
        }

        private interface IPreparedService { }

        // Stands for a manager that is created and set up before the core registers it.
        private sealed class PreparedService : IPreparedService, IUpdatable
        {
            [Inject] private EventLog _log = null;

            public bool IsReady { get; private set; }
            public EventLog InjectedLog => _log;

            public void Prepare() => IsReady = true;
            public void Update() => _log.Entries.Add("prepared updated");
        }

        private sealed class ConfigurableCore : CoreBase
        {
            public Action<ConfigurableCore> Configure;

            protected override void ConfigureContainer() => Configure?.Invoke(this);

            public void AddClass<TService, TImpl>(ServiceLifetime lifetime, string key = null)
                where TService : class
                where TImpl : class, TService
                => RegisterClass<TService, TImpl>(lifetime, key);

            public void AddInstance<TService>(TService instance) where TService : class
                => RegisterInstance(instance);

            public void AddFactory<TService>(Func<ContainerScope, TService> factory, ServiceLifetime lifetime, string key = null)
                where TService : class
                => RegisterFactory(factory, lifetime, key);

            public T Get<T>() where T : class => GetClass<T>();
            public T Get<T>(string key) where T : class => GetClass<T>(key);
        }

        private readonly List<GameObject> _objects = new();



        [TearDown]
        public void TearDown()
        {
            foreach (GameObject go in _objects)
            {
                if (go != null)
                {
                    Object.DestroyImmediate(go);
                }
            }

            _objects.Clear();
        }

        // Awake never runs on a component added in edit mode, so the test builds the container
        // the way Awake does.
        private ConfigurableCore CreateCore(Action<ConfigurableCore> configure)
        {
            GameObject go = new(nameof(ConfigurableCore));
            _objects.Add(go);
            ConfigurableCore core = go.AddComponent<ConfigurableCore>();
            core.Configure = configure;
            core.InitializeContainer();
            return core;
        }

        [Test]
        public void RegisterClass_SingletonWithConstructorDependency_IsCreatedAtOnceAndRunsItsLifecycle()
        {
            EventLog log = new();

            ConfigurableCore core = CreateCore(c =>
            {
                c.AddInstance(log);
                c.AddClass<IGreeter, Greeter>(ServiceLifetime.Singleton);
            });

            CollectionAssert.AreEqual(new[] { "created" }, log.Entries);
            IGreeter greeter = core.Get<IGreeter>();
            Assert.AreSame(greeter, core.Get<IGreeter>());
            Assert.AreSame(log, greeter.Log);
            // Only the service type maps to the implementation.
            Assert.IsNull(core.Get<Greeter>());

            core.InitializeCore();
            core.UpdateCore();

            CollectionAssert.AreEqual(new[] { "created", "initialized", "updated" }, log.Entries);
        }

        [Test]
        public void RegisterClass_Transient_IsCreatedOnEachResolveWithoutLifecycle()
        {
            EventLog log = new();

            ConfigurableCore core = CreateCore(c =>
            {
                c.AddInstance(log);
                c.AddClass<IGreeter, Greeter>(ServiceLifetime.Transient);
            });

            CollectionAssert.IsEmpty(log.Entries);
            Assert.AreNotSame(core.Get<IGreeter>(), core.Get<IGreeter>());

            core.InitializeCore();
            core.UpdateCore();

            CollectionAssert.AreEqual(new[] { "created", "created" }, log.Entries);
        }

        [Test]
        public void RegisterInstance_PreparedInstance_ResolvesWithItsMembersInjectedAndRunsItsLifecycle()
        {
            EventLog log = new();
            PreparedService prepared = new();
            prepared.Prepare();

            ConfigurableCore core = CreateCore(c =>
            {
                c.AddInstance(log);
                c.AddInstance(prepared);
            });

            Assert.AreSame(prepared, core.Get<PreparedService>());
            Assert.AreSame(prepared, core.Get<IPreparedService>());
            Assert.IsTrue(prepared.IsReady);
            Assert.AreSame(log, prepared.InjectedLog);

            core.InitializeCore();
            core.UpdateCore();

            CollectionAssert.AreEqual(new[] { "prepared updated" }, log.Entries);
        }

        [Test]
        public void RegisterFactory_KeyedSingleton_RunsTheFactoryOnceAtRegistration()
        {
            EventLog log = new();
            int factoryCalls = 0;

            ConfigurableCore core = CreateCore(c =>
            {
                c.AddInstance(log);
                c.AddFactory<IGreeter>(scope =>
                {
                    factoryCalls++;
                    return new Greeter(scope.Resolve<EventLog>());
                }, ServiceLifetime.Singleton, "main");
            });

            Assert.AreEqual(1, factoryCalls);
            IGreeter greeter = core.Get<IGreeter>("main");
            Assert.AreSame(greeter, core.Get<IGreeter>("main"));
            Assert.AreEqual(1, factoryCalls);
            Assert.AreSame(log, greeter.Log);

            core.InitializeCore();
            core.UpdateCore();

            CollectionAssert.AreEqual(new[] { "created", "initialized", "updated" }, log.Entries);
        }
    }
}
