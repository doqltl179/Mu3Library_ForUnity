using System;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Mu3Library.DI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Mu3Library.Tests
{
    public class ContainerTests
    {
        private interface IServiceA { }

        private class ServiceA : IServiceA { }

        private class ServiceB
        {
            public ServiceA A { get; }

            public ServiceB(ServiceA a)
            {
                A = a;
            }
        }

        private class CircularA
        {
            public CircularA(CircularB b) { }
        }

        private class CircularB
        {
            public CircularB(CircularA a) { }
        }

        private class DisposableService : IDisposable
        {
            public bool IsDisposed { get; private set; }

            public void Dispose() => IsDisposed = true;
        }

        private class InjectTarget
        {
            [Inject(required: false)] private IServiceA _service = null;

            public IServiceA Service => _service;
        }

        private abstract class InjectBase
        {
            [Inject(required: false)] private IServiceA _basePrivate = null;
            [Inject(required: false)] protected IServiceA _baseProtected = null;
            [Inject(required: false)] private IServiceA BasePrivateProperty { get; set; }

            public IServiceA BasePrivate => _basePrivate;
            public IServiceA BaseProtected => _baseProtected;
            public IServiceA BasePrivatePropertyValue => BasePrivateProperty;
        }

        private class InjectMiddle : InjectBase { }

        private class InjectLeaf : InjectMiddle
        {
            [Inject(required: false)] private IServiceA _leaf = null;

            public IServiceA Leaf => _leaf;
        }

        private abstract class RequiredInjectBase
        {
            [Inject] private ServiceB _required = null;

            public ServiceB Required => _required;
        }

        private class RequiredInjectLeaf : RequiredInjectBase { }

        private class VirtualInjectBase
        {
            private IServiceA _value;

            public int SetCount { get; private set; }

            [Inject(required: false)]
            protected virtual IServiceA Value
            {
                get => _value;
                set
                {
                    _value = value;
                    SetCount++;
                }
            }
        }

        private class VirtualInjectLeaf : VirtualInjectBase
        {
            [Inject(required: false)]
            protected override IServiceA Value
            {
                get => base.Value;
                set => base.Value = value;
            }
        }

        private abstract class GameCoreBase : CoreBase
        {
            [Inject(required: false)] private IServiceA _service = null;

            public IServiceA Service => _service;
        }

        private sealed class StageCore : GameCoreBase { }

        private class CountingService : IServiceA, IInitializable, IUpdatable
        {
            public int InitializeCount { get; private set; }
            public int UpdateCount { get; private set; }

            public void Initialize() => InitializeCount++;
            public void Update() => UpdateCount++;
        }

        private class RecordingService : IInitializable, IUpdatable, ILateUpdatable, IDisposable
        {
            public int InitializeCount { get; private set; }
            public int UpdateCount { get; private set; }
            public int LateUpdateCount { get; private set; }
            public int DisposeCount { get; private set; }

            public void Initialize() => InitializeCount++;
            public void Update() => UpdateCount++;
            public void LateUpdate() => LateUpdateCount++;
            public void Dispose() => DisposeCount++;
        }

        private sealed class FirstRecordingService : RecordingService { }

        private sealed class LastRecordingService : RecordingService { }

        private sealed class ThrowsOnInitialize : IInitializable
        {
            public void Initialize() => throw new InvalidOperationException("initialize boom");
        }

        private sealed class ThrowsOnUpdate : IUpdatable, ILateUpdatable
        {
            public void Update() => throw new InvalidOperationException("update boom");
            public void LateUpdate() => throw new InvalidOperationException("late update boom");
        }

        private sealed class ThrowsOnDispose : IDisposable
        {
            public void Dispose() => throw new InvalidOperationException("dispose boom");
        }

        private Container _container;
        private ContainerScope _scope;



        [SetUp]
        public void SetUp()
        {
            _container = new Container(null);
            _scope = _container.CreateScope();
        }

        [TearDown]
        public void TearDown()
        {
            _scope.Dispose();
        }

        [Test]
        public void Resolve_Singleton_ReturnsSameInstance()
        {
            _scope.Register<ServiceA>(ServiceLifetime.Singleton);

            ServiceA first = _scope.Resolve<ServiceA>();
            ServiceA second = _scope.Resolve<ServiceA>();

            Assert.IsNotNull(first);
            Assert.AreSame(first, second);
        }

        [Test]
        public void Resolve_Transient_ReturnsNewInstances()
        {
            _scope.Register<ServiceA>(ServiceLifetime.Transient);

            Assert.AreNotSame(_scope.Resolve<ServiceA>(), _scope.Resolve<ServiceA>());
        }

        [Test]
        public void Resolve_InterfaceAndSelf_ShareSingletonInstance()
        {
            _scope.Register<ServiceA>(ServiceLifetime.Singleton);

            ServiceA self = _scope.Resolve<ServiceA>();
            IServiceA viaInterface = _scope.Resolve<IServiceA>();

            Assert.AreSame(self, viaInterface);
        }

        [Test]
        public void Resolve_ConstructorInjection_ResolvesDependencies()
        {
            _scope.Register<ServiceA>(ServiceLifetime.Singleton);
            _scope.Register<ServiceB>(ServiceLifetime.Singleton);

            ServiceB serviceB = _scope.Resolve<ServiceB>();

            Assert.IsNotNull(serviceB.A);
            Assert.AreSame(_scope.Resolve<ServiceA>(), serviceB.A);
        }

        [Test]
        public void Resolve_MissingService_Throws()
        {
            Assert.Throws<InvalidOperationException>(() => _scope.Resolve<ServiceB>());
        }

        [Test]
        public void TryResolve_MissingService_ReturnsFalse()
        {
            Assert.IsFalse(_scope.TryResolve(out ServiceB instance));
            Assert.IsNull(instance);
        }

        [Test]
        public void Resolve_CircularDependency_Throws()
        {
            _scope.Register<CircularA>(ServiceLifetime.Singleton);
            _scope.Register<CircularB>(ServiceLifetime.Singleton);

            Assert.Throws<InvalidOperationException>(() => _scope.Resolve<CircularA>());
        }

        [Test]
        public void Register_SameServiceWithDifferentLifetime_LastRegistrationWins()
        {
            _scope.Register<ServiceA>(ServiceLifetime.Singleton);
            ServiceA singleton = _scope.Resolve<ServiceA>();
            Assert.AreSame(singleton, _scope.Resolve<ServiceA>());

            _scope.Register<ServiceA>(ServiceLifetime.Transient);

            Assert.AreNotSame(_scope.Resolve<ServiceA>(), _scope.Resolve<ServiceA>());
        }

        [Test]
        public void RegisterInstance_ResolvesRegisteredInstance()
        {
            ServiceA instance = new();
            _scope.RegisterInstance(instance);

            Assert.AreSame(instance, _scope.Resolve<ServiceA>());
            Assert.AreSame(instance, _scope.Resolve<IServiceA>());
        }

        [Test]
        public void RegisterFactory_UsesFactoryResult()
        {
            ServiceA fromFactory = new();
            _scope.RegisterFactory(_ => fromFactory, ServiceLifetime.Singleton);

            Assert.AreSame(fromFactory, _scope.Resolve<ServiceA>());
        }

        [Test]
        public void Resolve_KeyedRegistrations_AreIndependent()
        {
            _scope.Register<ServiceA>(ServiceLifetime.Singleton, "one");
            _scope.Register<ServiceA>(ServiceLifetime.Singleton, "two");

            ServiceA one = _scope.Resolve<ServiceA>("one");
            ServiceA two = _scope.Resolve<ServiceA>("two");

            Assert.IsNotNull(one);
            Assert.IsNotNull(two);
            Assert.AreNotSame(one, two);
        }

        [Test]
        public void Resolve_FuncAndLazyWrappers_ResolveElement()
        {
            _scope.Register<ServiceA>(ServiceLifetime.Singleton);

            Func<ServiceA> factory = _scope.Resolve<Func<ServiceA>>();
            Lazy<ServiceA> lazy = _scope.Resolve<Lazy<ServiceA>>();

            Assert.AreSame(_scope.Resolve<ServiceA>(), factory());
            Assert.AreSame(_scope.Resolve<ServiceA>(), lazy.Value);
        }

        [Test]
        public void ResolveAll_ReturnsEveryRegistration()
        {
            _scope.Register<IServiceA, ServiceA>(ServiceLifetime.Transient);

            Assert.AreEqual(1, _scope.ResolveAll<IServiceA>().Count());
        }

        [Test]
        public void Dispose_DisposesTrackedSingleton()
        {
            _scope.Register<DisposableService>(ServiceLifetime.Singleton);
            DisposableService service = _scope.Resolve<DisposableService>();

            _scope.Dispose();

            Assert.IsTrue(service.IsDisposed);
        }

        [Test]
        public void InjectInto_OptionalMissingService_LeavesMemberNull()
        {
            InjectTarget target = new();

            _scope.InjectInto(target);

            Assert.IsNull(target.Service);
        }

        [Test]
        public void InjectInto_RegisteredService_FillsMember()
        {
            _scope.Register<ServiceA>(ServiceLifetime.Singleton);

            InjectTarget target = new();
            _scope.InjectInto(target);

            Assert.IsNotNull(target.Service);
        }

        [Test]
        public void IsRegistered_ReportsWithoutResolving()
        {
            Assert.IsFalse(_container.IsRegistered(typeof(ServiceA)));

            _scope.Register<ServiceA>(ServiceLifetime.Singleton);

            Assert.IsTrue(_container.IsRegistered(typeof(ServiceA)));
            Assert.AreEqual(0, _container.GetActiveSingletonInstances().Count);
        }

        #region Lifecycle isolation
        private (RecordingService first, RecordingService last) ResolveAround<TThrowing>() where TThrowing : class
        {
            _scope.Register<FirstRecordingService>(ServiceLifetime.Singleton);
            _scope.Register<TThrowing>(ServiceLifetime.Singleton);
            _scope.Register<LastRecordingService>(ServiceLifetime.Singleton);

            RecordingService first = _scope.Resolve<FirstRecordingService>();
            _scope.Resolve<TThrowing>();
            RecordingService last = _scope.Resolve<LastRecordingService>();
            return (first, last);
        }

        [Test]
        public void Initialize_ThrowingService_StillInitializesTheOthers()
        {
            (RecordingService first, RecordingService last) = ResolveAround<ThrowsOnInitialize>();

            LogAssert.Expect(LogType.Exception, new Regex("initialize boom"));
            _scope.Initialize();

            Assert.AreEqual(1, first.InitializeCount);
            Assert.AreEqual(1, last.InitializeCount);
        }

        [Test]
        public void Update_ThrowingService_KeepsUpdatingTheOthersEveryFrame()
        {
            (RecordingService first, RecordingService last) = ResolveAround<ThrowsOnUpdate>();

            LogAssert.Expect(LogType.Exception, new Regex("update boom"));
            _scope.Update();
            LogAssert.Expect(LogType.Exception, new Regex("update boom"));
            _scope.Update();

            Assert.AreEqual(2, first.UpdateCount);
            Assert.AreEqual(2, last.UpdateCount);
        }

        [Test]
        public void LateUpdate_ThrowingService_StillLateUpdatesTheOthers()
        {
            (RecordingService first, RecordingService last) = ResolveAround<ThrowsOnUpdate>();

            LogAssert.Expect(LogType.Exception, new Regex("late update boom"));
            _scope.LateUpdate();

            Assert.AreEqual(1, first.LateUpdateCount);
            Assert.AreEqual(1, last.LateUpdateCount);
        }

        [Test]
        public void Dispose_ThrowingService_StillDisposesTheOthers()
        {
            (RecordingService first, RecordingService last) = ResolveAround<ThrowsOnDispose>();

            LogAssert.Expect(LogType.Exception, new Regex("dispose boom"));
            _scope.Dispose();

            Assert.AreEqual(1, first.DisposeCount);
            Assert.AreEqual(1, last.DisposeCount);
        }

        [Test]
        public void Resolve_AfterInitialize_ThrowingInitializeIsLoggedAndInstanceReturned()
        {
            _scope.Initialize();
            _scope.Register<ThrowsOnInitialize>(ServiceLifetime.Singleton);

            LogAssert.Expect(LogType.Exception, new Regex("initialize boom"));
            ThrowsOnInitialize instance = _scope.Resolve<ThrowsOnInitialize>();

            Assert.IsNotNull(instance);
        }
        #endregion

        #region Base class members
        [Test]
        public void InjectInto_BaseClassMembers_AreInjected()
        {
            _scope.Register<ServiceA>(ServiceLifetime.Singleton);
            IServiceA service = _scope.Resolve<IServiceA>();

            InjectLeaf target = new();
            _scope.InjectInto(target);

            Assert.AreSame(service, target.Leaf);
            Assert.AreSame(service, target.BasePrivate);
            Assert.AreSame(service, target.BaseProtected);
            Assert.AreSame(service, target.BasePrivatePropertyValue);
        }

        [Test]
        public void InjectInto_MissingRequiredBaseClassMember_Throws()
        {
            Assert.Throws<InvalidOperationException>(() => _scope.InjectInto(new RequiredInjectLeaf()));
        }

        [Test]
        public void InjectInto_OverriddenProperty_IsInjectedOnce()
        {
            _scope.Register<ServiceA>(ServiceLifetime.Singleton);

            VirtualInjectLeaf target = new();
            _scope.InjectInto(target);

            Assert.AreEqual(1, target.SetCount);
            Assert.AreEqual(1, InjectableMembers.GetProperties(typeof(VirtualInjectLeaf)).Count);
        }

        [Test]
        public void InjectableMembers_ListsBaseClassMembersLeafFirst()
        {
            FieldInfo[] fields = InjectableMembers.GetFields(typeof(InjectLeaf)).ToArray();

            CollectionAssert.AreEqual(
                new[] { "_leaf", "_basePrivate", "_baseProtected" },
                fields.Select(field => field.Name).ToArray());
            Assert.AreEqual(1, InjectableMembers.GetProperties(typeof(InjectLeaf)).Count);
        }

        [Test]
        public void InjectInto_CoreDerivedThroughBaseCore_InjectsBaseCoreMember()
        {
            _scope.Register<ServiceA>(ServiceLifetime.Singleton);

            GameObject go = new(nameof(StageCore));
            try
            {
                StageCore core = go.AddComponent<StageCore>();
                _scope.InjectInto(core);

                Assert.AreSame(_scope.Resolve<IServiceA>(), core.Service);
                Assert.IsTrue(InjectableMembers.GetFields(typeof(StageCore)).All(field => field.DeclaringType == typeof(GameCoreBase)));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }
        #endregion

        #region Instance identity
        [Test]
        public void Resolve_KeyedSingleton_SharesInstanceAndLifecycleAcrossServiceTypes()
        {
            _scope.Register<CountingService>(ServiceLifetime.Singleton, "one");

            CountingService self = _scope.Resolve<CountingService>("one");
            IServiceA viaInterface = _scope.Resolve<IServiceA>("one");
            _scope.Initialize();
            _scope.Update();

            Assert.AreSame(self, viaInterface);
            Assert.AreEqual(1, self.InitializeCount);
            Assert.AreEqual(1, self.UpdateCount);
        }

        [Test]
        public void Resolve_Scoped_SharesInstanceAcrossServiceTypesWithinOneScope()
        {
            _scope.Register<ServiceA>(ServiceLifetime.Scoped);
            ContainerScope other = _container.CreateScope();

            try
            {
                ServiceA self = _scope.Resolve<ServiceA>();

                Assert.AreSame(self, _scope.Resolve<IServiceA>());
                Assert.AreSame(other.Resolve<ServiceA>(), other.Resolve<IServiceA>());
                Assert.AreNotSame(self, other.Resolve<IServiceA>());
            }
            finally
            {
                other.Dispose();
            }
        }

        [Test]
        public void ResolveAll_SeveralFactories_EachCreatesItsOwnInstance()
        {
            int firstCalls = 0;
            int secondCalls = 0;
            _scope.RegisterFactory<IServiceA>(_ =>
            {
                firstCalls++;
                return new ServiceA();
            });
            _scope.RegisterFactory<IServiceA>(_ =>
            {
                secondCalls++;
                return new ServiceA();
            });

            IServiceA[] all = _scope.ResolveAll<IServiceA>().ToArray();

            Assert.AreEqual(2, all.Length);
            Assert.AreNotSame(all[0], all[1]);
            Assert.AreEqual(1, firstCalls);
            Assert.AreEqual(1, secondCalls);
        }

        [Test]
        public void Resolve_FactoryRegisteredAfterFirstResolve_Applies()
        {
            ServiceA first = new();
            ServiceA second = new();

            _scope.RegisterFactory<IServiceA>(_ => first);
            Assert.AreSame(first, _scope.Resolve<IServiceA>());

            _scope.RegisterFactory<IServiceA>(_ => second);
            Assert.AreSame(second, _scope.Resolve<IServiceA>());
        }
        #endregion
    }
}
