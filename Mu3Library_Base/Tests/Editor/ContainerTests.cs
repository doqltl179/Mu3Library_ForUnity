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

        // Required [Inject] properties the container cannot write: get-only here, as one is in a
        // player whose linker removed the setter.
        private class GetOnlyInjectTarget
        {
            [Inject] public IServiceA Service { get; }
        }

        private abstract class GetOnlyInjectBase
        {
            [Inject] private IServiceA BaseService => null;
        }

        private class GetOnlyInjectLeaf : GetOnlyInjectBase { }

        private abstract class AbstractGetOnlyInjectBase
        {
            [Inject] public abstract IServiceA Service { get; }
        }

        private class OverriddenGetOnlyInjectLeaf : AbstractGetOnlyInjectBase
        {
            public override IServiceA Service => null;
        }

        private class OptionalGetOnlyInjectTarget
        {
            [Inject(required: false)] public IServiceA Service { get; }
        }

        // The base declaration has a setter, so the container still writes the property through it.
        private class WritableVirtualInjectBase
        {
            [Inject] public virtual IServiceA Service { get; set; }
        }

        private class GetterOnlyOverrideInjectLeaf : WritableVirtualInjectBase
        {
            public override IServiceA Service => base.Service;
        }

        private interface IServiceAHolder
        {
            IServiceA Service { get; }
        }

        // An explicit interface implementation is a private member of the class.
        private class ExplicitGetOnlyInjectTarget : IServiceAHolder
        {
            [Inject] IServiceA IServiceAHolder.Service => null;
        }

        // Required readonly [Inject] fields: only an initializer or a constructor sets them.
        private class ReadOnlyInjectTarget
        {
            [Inject(required: false)] private IServiceA _writable = null;
            [Inject] private readonly IServiceA _service = null;

            public IServiceA Writable => _writable;
            public IServiceA Service => _service;
        }

        private abstract class ReadOnlyInjectBase
        {
            [Inject] private readonly IServiceA _baseService = null;

            public IServiceA BaseService => _baseService;
        }

        private class ReadOnlyInjectLeaf : ReadOnlyInjectBase { }

        private class OptionalReadOnlyInjectTarget
        {
            [Inject(required: false)] private readonly IServiceA _service = null;

            public IServiceA Service => _service;
        }

        // Required static [Inject] members: a static belongs to the class, not to the injected object.
        private class StaticFieldInjectTarget
        {
            [Inject(required: false)] private IServiceA _writable = null;
            [Inject] private static IServiceA _service = null;

            public IServiceA Writable => _writable;
            public static IServiceA Service => _service;
        }

        private class StaticPropertyInjectTarget
        {
            [Inject] public static IServiceA Service { get; set; }
        }

        private abstract class StaticReadOnlyInjectBase
        {
            [Inject] private static readonly IServiceA _baseService = null;

            public static IServiceA BaseService => _baseService;
        }

        private class StaticReadOnlyInjectLeaf : StaticReadOnlyInjectBase { }

        private class OptionalStaticInjectTarget
        {
            [Inject(required: false)] private static IServiceA _field = null;

            [Inject(required: false)] public static IServiceA Property { get; set; }

            public static IServiceA Field => _field;
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

        // Registered instances whose injection fails: no service is registered for this one.
        private interface IUnregisteredService { }

        private interface IRollbackTarget { }

        private sealed class FailingInjectRecordingService : RecordingService, IServiceA
        {
            [Inject] public IUnregisteredService Missing { get; set; }
        }

        private sealed class DependsOnRollbackTarget
        {
            public DependsOnRollbackTarget(IRollbackTarget target) => Target = target;

            public IRollbackTarget Target { get; }
        }

        // The field resolves a service whose constructor resolves this instance through its
        // registration, which starts its lifecycle tracking; then the property fails, since
        // fields are injected before properties.
        private sealed class SelfResolvingFailingInjectService : RecordingService, IRollbackTarget
        {
            [Inject(required: false)] private DependsOnRollbackTarget _dependent = null;

            [Inject] public IUnregisteredService Missing { get; set; }

            public DependsOnRollbackTarget Dependent => _dependent;
        }

        private sealed class ValidRollbackTarget : IRollbackTarget { }

        private sealed class DependsOnDependent
        {
            public DependsOnDependent(DependsOnRollbackTarget dependent) => Dependent = dependent;

            public DependsOnRollbackTarget Dependent { get; }
        }

        // Receives a service that has nothing to do with it, then one that holds it through
        // another service, and then fails.
        private sealed class PartlyInjectedFailingInjectService : IRollbackTarget
        {
            [Inject(required: false)] private ServiceA _independent = null;
            [Inject(required: false)] private DependsOnDependent _transitive = null;

            [Inject] public IUnregisteredService Missing { get; set; }

            public ServiceA Independent => _independent;
            public DependsOnDependent Transitive => _transitive;
        }

        private sealed class RecordingRollbackDependent : RecordingService
        {
            public RecordingRollbackDependent(IRollbackTarget target) => Target = target;

            public IRollbackTarget Target { get; }
        }

        private sealed class RecordingDependentFailingInjectService : RecordingService, IRollbackTarget
        {
            [Inject(required: false)] private RecordingRollbackDependent _dependent = null;

            [Inject] public IUnregisteredService Missing { get; set; }

            public RecordingRollbackDependent Dependent => _dependent;
        }

        private sealed class ThrowsOnDisposeRollbackDependent : IDisposable
        {
            public ThrowsOnDisposeRollbackDependent(IRollbackTarget target) { }

            public void Dispose() => throw new InvalidOperationException("rollback dispose boom");
        }

        private sealed class ThrowingDependentFailingInjectService : IRollbackTarget
        {
            [Inject(required: false)] private ThrowsOnDisposeRollbackDependent _dependent = null;

            [Inject] public IUnregisteredService Missing { get; set; }

            public ThrowsOnDisposeRollbackDependent Dependent => _dependent;
        }

        // Equal to every object, with one hash code for all: by Equals, any of them would look
        // like a service that received the instance once one of them did.
        private abstract class EqualToAll
        {
            public override bool Equals(object obj) => true;

            public override int GetHashCode() => 42;
        }

        private sealed class EqualToAllDependent : EqualToAll
        {
            public EqualToAllDependent(IRollbackTarget target) { }
        }

        private sealed class EqualToAllService : EqualToAll { }

        private sealed class ConsumesEqualToAll
        {
            public ConsumesEqualToAll(EqualToAllService service) { }
        }

        private sealed class EqualityConfusedFailingInjectService : IRollbackTarget
        {
            [Inject(required: false)] private EqualToAllDependent _dependent = null;
            [Inject(required: false)] private ConsumesEqualToAll _unrelated = null;

            [Inject] public IUnregisteredService Missing { get; set; }

            public EqualToAllDependent Dependent => _dependent;
            public ConsumesEqualToAll Unrelated => _unrelated;
        }

        // A RegisterInstance call inside another one's injection: the holder of the outer
        // instance registers the inner one from its constructor.
        private interface IOuterTarget { }

        private interface IInnerTarget { }

        private sealed class InnerSupply
        {
            public InnerSupply(IInnerTarget instance) => Instance = instance;

            public IInnerTarget Instance { get; }
        }

        private sealed class DependsOnInner : RecordingService
        {
            public DependsOnInner(IInnerTarget target) => Target = target;

            public IInnerTarget Target { get; }
        }

        private sealed class FailingInnerTarget : IInnerTarget
        {
            [Inject(required: false)] private DependsOnInner _dependent = null;

            [Inject] public IUnregisteredService Missing { get; set; }

            public DependsOnInner Dependent => _dependent;
        }

        private sealed class ValidInnerTarget : IInnerTarget
        {
            [Inject(required: false)] private DependsOnInner _dependent = null;

            public DependsOnInner Dependent => _dependent;
        }

        private sealed class NestingHolder : RecordingService
        {
            public NestingHolder(ContainerScope scope, IOuterTarget outer, InnerSupply supply)
            {
                Outer = outer;
                try
                {
                    scope.RegisterInstance(supply.Instance);
                }
                catch (InvalidOperationException)
                {
                    InnerFailed = true;
                }
            }

            public IOuterTarget Outer { get; }
            public bool InnerFailed { get; }
        }

        private sealed class NestingOuterTarget : IOuterTarget
        {
            [Inject(required: false)] private NestingHolder _holder = null;

            public NestingHolder Holder => _holder;
        }

        private sealed class FailingNestingOuterTarget : IOuterTarget
        {
            [Inject(required: false)] private NestingHolder _holder = null;

            [Inject] public IUnregisteredService Missing { get; set; }

            public NestingHolder Holder => _holder;
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

        #region Unwritable [Inject] properties
        [Test]
        public void InjectInto_RequiredGetOnlyProperty_ThrowsEvenWhenItsServiceIsRegistered()
        {
            _scope.Register<ServiceA>(ServiceLifetime.Singleton);

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                () => _scope.InjectInto(new GetOnlyInjectTarget()));

            StringAssert.Contains($"{typeof(GetOnlyInjectTarget).FullName}.{nameof(GetOnlyInjectTarget.Service)}", exception.Message);
            StringAssert.Contains("no setter", exception.Message);
        }

        [Test]
        public void InjectInto_RequiredGetOnlyBaseClassProperty_Throws()
        {
            _scope.Register<ServiceA>(ServiceLifetime.Singleton);

            Assert.Throws<InvalidOperationException>(() => _scope.InjectInto(new GetOnlyInjectLeaf()));
        }

        [Test]
        public void InjectInto_RequiredGetOnlyOverriddenProperty_Throws()
        {
            _scope.Register<ServiceA>(ServiceLifetime.Singleton);

            Assert.Throws<InvalidOperationException>(() => _scope.InjectInto(new OverriddenGetOnlyInjectLeaf()));
        }

        [Test]
        public void InjectInto_RequiredGetOnlyExplicitInterfaceProperty_Throws()
        {
            _scope.Register<ServiceA>(ServiceLifetime.Singleton);

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                () => _scope.InjectInto(new ExplicitGetOnlyInjectTarget()));

            StringAssert.Contains($"{typeof(ExplicitGetOnlyInjectTarget).FullName}.", exception.Message);
            StringAssert.Contains($"{nameof(IServiceAHolder)}.{nameof(IServiceAHolder.Service)}", exception.Message);
            StringAssert.Contains("no setter", exception.Message);
        }

        [Test]
        public void Resolve_ServiceWithRequiredGetOnlyProperty_Throws()
        {
            _scope.Register<ServiceA>(ServiceLifetime.Singleton);
            _scope.Register<GetOnlyInjectTarget>(ServiceLifetime.Transient);

            Assert.Throws<InvalidOperationException>(() => _scope.Resolve<GetOnlyInjectTarget>());
        }

        [Test]
        public void InjectInto_OptionalGetOnlyProperty_IsSkipped()
        {
            _scope.Register<ServiceA>(ServiceLifetime.Singleton);
            OptionalGetOnlyInjectTarget target = new();

            Assert.DoesNotThrow(() => _scope.InjectInto(target));
            Assert.IsNull(target.Service);
        }

        [Test]
        public void InjectInto_GetterOnlyOverrideOfWritableProperty_InjectsThroughTheBaseSetter()
        {
            _scope.Register<ServiceA>(ServiceLifetime.Singleton);
            GetterOnlyOverrideInjectLeaf target = new();

            _scope.InjectInto(target);

            Assert.AreSame(_scope.Resolve<IServiceA>(), target.Service);
            Assert.AreEqual(0, InjectableMembers.GetUnwritableProperties(typeof(GetterOnlyOverrideInjectLeaf)).Count);
        }

        [Test]
        public void InjectableMembers_UnwritableProperties_ListsEachGetOnlyPropertyOnce()
        {
            CollectionAssert.AreEqual(
                new[] { nameof(GetOnlyInjectTarget.Service) },
                InjectableMembers.GetUnwritableProperties(typeof(GetOnlyInjectTarget)).Select(property => property.Name).ToArray());
            CollectionAssert.AreEqual(
                new[] { "BaseService" },
                InjectableMembers.GetUnwritableProperties(typeof(GetOnlyInjectLeaf)).Select(property => property.Name).ToArray());
            Assert.AreEqual(1, InjectableMembers.GetUnwritableProperties(typeof(OverriddenGetOnlyInjectLeaf)).Count);
            Assert.AreEqual(0, InjectableMembers.GetUnwritableProperties(typeof(InjectLeaf)).Count);
            Assert.AreEqual(0, InjectableMembers.GetProperties(typeof(GetOnlyInjectTarget)).Count);
        }
        #endregion

        #region Readonly [Inject] fields
        [Test]
        public void InjectInto_RequiredReadOnlyField_ThrowsBeforeInjectingAnyMember()
        {
            _scope.Register<ServiceA>(ServiceLifetime.Singleton);
            ReadOnlyInjectTarget target = new();

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => _scope.InjectInto(target));

            StringAssert.Contains($"{typeof(ReadOnlyInjectTarget).FullName}._service", exception.Message);
            StringAssert.Contains("readonly", exception.Message);
            Assert.IsNull(target.Writable);
        }

        [Test]
        public void InjectInto_RequiredReadOnlyBaseClassField_Throws()
        {
            _scope.Register<ServiceA>(ServiceLifetime.Singleton);

            Assert.Throws<InvalidOperationException>(() => _scope.InjectInto(new ReadOnlyInjectLeaf()));
        }

        [Test]
        public void Resolve_ServiceWithRequiredReadOnlyField_Throws()
        {
            _scope.Register<ServiceA>(ServiceLifetime.Singleton);
            _scope.Register<ReadOnlyInjectTarget>(ServiceLifetime.Transient);

            Assert.Throws<InvalidOperationException>(() => _scope.Resolve<ReadOnlyInjectTarget>());
        }

        [Test]
        public void InjectInto_OptionalReadOnlyField_IsSkipped()
        {
            _scope.Register<ServiceA>(ServiceLifetime.Singleton);
            OptionalReadOnlyInjectTarget target = new();

            Assert.DoesNotThrow(() => _scope.InjectInto(target));
            Assert.IsNull(target.Service);
        }

        [Test]
        public void InjectableMembers_ReadOnlyFields_ListsReadOnlyFieldsOnly()
        {
            CollectionAssert.AreEqual(
                new[] { "_service" },
                InjectableMembers.GetReadOnlyFields(typeof(ReadOnlyInjectTarget)).Select(field => field.Name).ToArray());
            CollectionAssert.AreEqual(
                new[] { "_baseService" },
                InjectableMembers.GetReadOnlyFields(typeof(ReadOnlyInjectLeaf)).Select(field => field.Name).ToArray());
            Assert.AreEqual(0, InjectableMembers.GetReadOnlyFields(typeof(InjectLeaf)).Count);
            CollectionAssert.AreEqual(
                new[] { "_writable" },
                InjectableMembers.GetFields(typeof(ReadOnlyInjectTarget)).Select(field => field.Name).ToArray());
        }
        #endregion

        #region Static [Inject] members
        [Test]
        public void InjectInto_RequiredStaticField_ThrowsBeforeInjectingAnyMember()
        {
            _scope.Register<ServiceA>(ServiceLifetime.Singleton);
            StaticFieldInjectTarget target = new();

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => _scope.InjectInto(target));

            StringAssert.Contains($"{typeof(StaticFieldInjectTarget).FullName}._service", exception.Message);
            StringAssert.Contains("static", exception.Message);
            Assert.IsNull(target.Writable);
            Assert.IsNull(StaticFieldInjectTarget.Service);
        }

        [Test]
        public void InjectInto_RequiredStaticProperty_ThrowsEvenWhenItsServiceIsRegistered()
        {
            _scope.Register<ServiceA>(ServiceLifetime.Singleton);

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                () => _scope.InjectInto(new StaticPropertyInjectTarget()));

            StringAssert.Contains($"{typeof(StaticPropertyInjectTarget).FullName}.{nameof(StaticPropertyInjectTarget.Service)}", exception.Message);
            StringAssert.Contains("static", exception.Message);
            Assert.IsNull(StaticPropertyInjectTarget.Service);
        }

        [Test]
        public void InjectInto_RequiredStaticReadOnlyBaseClassField_Throws()
        {
            _scope.Register<ServiceA>(ServiceLifetime.Singleton);

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                () => _scope.InjectInto(new StaticReadOnlyInjectLeaf()));

            StringAssert.Contains("._baseService", exception.Message);
            StringAssert.Contains("static", exception.Message);
        }

        [Test]
        public void Resolve_ServiceWithRequiredStaticMember_Throws()
        {
            _scope.Register<ServiceA>(ServiceLifetime.Singleton);
            _scope.Register<StaticPropertyInjectTarget>(ServiceLifetime.Transient);

            Assert.Throws<InvalidOperationException>(() => _scope.Resolve<StaticPropertyInjectTarget>());
        }

        [Test]
        public void InjectInto_OptionalStaticMembers_AreSkipped()
        {
            _scope.Register<ServiceA>(ServiceLifetime.Singleton);

            Assert.DoesNotThrow(() => _scope.InjectInto(new OptionalStaticInjectTarget()));
            Assert.IsNull(OptionalStaticInjectTarget.Field);
            Assert.IsNull(OptionalStaticInjectTarget.Property);
        }

        [Test]
        public void InjectableMembers_StaticMembers_ListsStaticMembersOnly()
        {
            CollectionAssert.AreEqual(
                new[] { "_service" },
                InjectableMembers.GetStaticFields(typeof(StaticFieldInjectTarget)).Select(field => field.Name).ToArray());
            CollectionAssert.AreEqual(
                new[] { "_baseService" },
                InjectableMembers.GetStaticFields(typeof(StaticReadOnlyInjectLeaf)).Select(field => field.Name).ToArray());
            CollectionAssert.AreEqual(
                new[] { nameof(StaticPropertyInjectTarget.Service) },
                InjectableMembers.GetStaticProperties(typeof(StaticPropertyInjectTarget)).Select(property => property.Name).ToArray());
            CollectionAssert.AreEqual(
                new[] { "_writable" },
                InjectableMembers.GetFields(typeof(StaticFieldInjectTarget)).Select(field => field.Name).ToArray());
            Assert.AreEqual(0, InjectableMembers.GetReadOnlyFields(typeof(StaticReadOnlyInjectLeaf)).Count);
            Assert.AreEqual(0, InjectableMembers.GetProperties(typeof(StaticPropertyInjectTarget)).Count);
            Assert.AreEqual(0, InjectableMembers.GetUnwritableProperties(typeof(StaticPropertyInjectTarget)).Count);
            Assert.AreEqual(0, InjectableMembers.GetStaticFields(typeof(InjectLeaf)).Count);
            Assert.AreEqual(0, InjectableMembers.GetStaticProperties(typeof(InjectLeaf)).Count);
        }
        #endregion

        #region Failed instance registration
        [Test]
        public void RegisterInstance_InjectionFails_InstanceDoesNotResolve()
        {
            FailingInjectRecordingService instance = new();

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => _scope.RegisterInstance(instance));

            StringAssert.Contains($"{typeof(FailingInjectRecordingService).FullName}.{nameof(FailingInjectRecordingService.Missing)}", exception.Message);
            Assert.IsFalse(_container.IsRegistered(typeof(FailingInjectRecordingService)));
            Assert.IsFalse(_container.IsRegistered(typeof(IServiceA)));
            Assert.IsFalse(_scope.TryResolve(out FailingInjectRecordingService _));
            Assert.IsFalse(_scope.TryResolve(out IServiceA _));
            Assert.AreEqual(0, _scope.ResolveAll<IServiceA>().Count());
            Assert.IsFalse(_container.GetActiveSingletonInstances().Contains(instance));
        }

        [Test]
        public void RegisterInstance_InjectionFails_LifecycleIsNotTracked()
        {
            FailingInjectRecordingService instance = new();
            Assert.Throws<InvalidOperationException>(() => _scope.RegisterInstance(instance));

            // A resolve started the tracking that the registration skipped.
            _scope.TryResolve(out FailingInjectRecordingService _);
            _scope.Initialize();
            _scope.Update();
            _scope.LateUpdate();
            _scope.Dispose();

            Assert.AreEqual(0, instance.InitializeCount);
            Assert.AreEqual(0, instance.UpdateCount);
            Assert.AreEqual(0, instance.LateUpdateCount);
            Assert.AreEqual(0, instance.DisposeCount);
        }

        [Test]
        public void RegisterInstance_InjectionFailsAfterTheInstanceResolved_LifecycleIsNotTracked()
        {
            _scope.Register<DependsOnRollbackTarget>(ServiceLifetime.Transient);
            SelfResolvingFailingInjectService instance = new();

            Assert.Throws<InvalidOperationException>(() => _scope.RegisterInstance(instance));
            _scope.Initialize();
            _scope.Update();
            _scope.LateUpdate();
            _scope.Dispose();

            Assert.AreSame(instance, instance.Dependent.Target);
            Assert.IsFalse(_container.IsRegistered(typeof(IRollbackTarget)));
            Assert.AreEqual(0, instance.InitializeCount);
            Assert.AreEqual(0, instance.UpdateCount);
            Assert.AreEqual(0, instance.LateUpdateCount);
            Assert.AreEqual(0, instance.DisposeCount);
        }

        [Test]
        public void RegisterInstance_UnfillableRequiredMember_UndoesTheRegistration()
        {
            _scope.Register<ServiceA>(ServiceLifetime.Singleton);

            Assert.Throws<InvalidOperationException>(() => _scope.RegisterInstance(new ReadOnlyInjectTarget()));

            Assert.IsFalse(_container.IsRegistered(typeof(ReadOnlyInjectTarget)));
        }

        [Test]
        public void RegisterInstance_InjectionFails_KeepsTheOtherRegistrations()
        {
            _scope.Register<ServiceA>(ServiceLifetime.Singleton);
            ServiceA registered = _scope.Resolve<ServiceA>();

            Assert.Throws<InvalidOperationException>(() => _scope.RegisterInstance(new FailingInjectRecordingService()));
            _scope.Register<ServiceB>(ServiceLifetime.Transient);

            Assert.AreSame(registered, _scope.Resolve<IServiceA>());
            Assert.AreEqual(1, _scope.ResolveAll<IServiceA>().Count());
            Assert.AreSame(registered, _scope.Resolve<ServiceB>().A);
        }

        [Test]
        public void RegisterInstance_InjectionFailsForAnInstanceRegisteredBefore_KeepsTheEarlierRegistration()
        {
            FailingInjectRecordingService instance = new();
            _container.RegisterInstance(instance, registerInterfaces: false);

            Assert.Throws<InvalidOperationException>(() => _scope.RegisterInstance(instance));

            Assert.AreSame(instance, _scope.Resolve<FailingInjectRecordingService>());
            Assert.IsFalse(_container.IsRegistered(typeof(IServiceA)));
        }

        [Test]
        public void RegisterInstance_InjectionFailsForAnInstanceAlreadyTracked_KeepsItsLifecycle()
        {
            FailingInjectRecordingService instance = new();
            _container.RegisterInstance(instance, registerInterfaces: false);
            // The resolve through the earlier registration starts the tracking.
            Assert.AreSame(instance, _scope.Resolve<FailingInjectRecordingService>());

            Assert.Throws<InvalidOperationException>(() => _scope.RegisterInstance(instance));
            _scope.Initialize();
            _scope.Update();
            _scope.LateUpdate();
            _scope.Dispose();

            Assert.AreEqual(1, instance.InitializeCount);
            Assert.AreEqual(1, instance.UpdateCount);
            Assert.AreEqual(1, instance.LateUpdateCount);
            Assert.AreEqual(1, instance.DisposeCount);
        }

        [TestCase(ServiceLifetime.Singleton)]
        [TestCase(ServiceLifetime.Scoped)]
        public void RegisterInstance_InjectionFails_EvictsTheServiceThatReceivedTheInstance(ServiceLifetime lifetime)
        {
            _scope.Register<DependsOnRollbackTarget>(lifetime);
            SelfResolvingFailingInjectService instance = new();

            Assert.Throws<InvalidOperationException>(() => _scope.RegisterInstance(instance));
            ValidRollbackTarget replacement = new();
            _scope.RegisterInstance<IRollbackTarget>(replacement);
            DependsOnRollbackTarget resolved = _scope.Resolve<DependsOnRollbackTarget>();

            Assert.AreSame(instance, instance.Dependent.Target);
            Assert.AreNotSame(instance.Dependent, resolved);
            Assert.AreSame(replacement, resolved.Target);
            Assert.IsFalse(_container.GetActiveSingletonInstances().Contains(instance.Dependent));
        }

        [TestCase(ServiceLifetime.Singleton)]
        [TestCase(ServiceLifetime.Transient)]
        public void RegisterInstance_InjectionFails_EvictsWhatHoldsTheInstanceThroughAnotherService(ServiceLifetime throughLifetime)
        {
            _scope.Register<DependsOnRollbackTarget>(throughLifetime);
            _scope.Register<DependsOnDependent>(ServiceLifetime.Singleton);
            PartlyInjectedFailingInjectService instance = new();

            Assert.Throws<InvalidOperationException>(() => _scope.RegisterInstance(instance));
            _scope.RegisterInstance<IRollbackTarget>(new ValidRollbackTarget());
            DependsOnDependent resolved = _scope.Resolve<DependsOnDependent>();

            Assert.AreSame(instance, instance.Transitive.Dependent.Target);
            Assert.AreNotSame(instance.Transitive, resolved);
            Assert.AreNotSame(instance.Transitive.Dependent, resolved.Dependent);
            Assert.IsInstanceOf<ValidRollbackTarget>(resolved.Dependent.Target);
        }

        [Test]
        public void RegisterInstance_InjectionFails_KeepsTheServicesThatDidNotReceiveTheInstance()
        {
            _scope.Register<ServiceA>(ServiceLifetime.Singleton);
            _scope.Register<DependsOnRollbackTarget>(ServiceLifetime.Singleton);
            _scope.Register<DependsOnDependent>(ServiceLifetime.Singleton);
            PartlyInjectedFailingInjectService instance = new();

            Assert.Throws<InvalidOperationException>(() => _scope.RegisterInstance(instance));

            Assert.IsNotNull(instance.Independent);
            Assert.AreSame(instance.Independent, _scope.Resolve<ServiceA>());
            Assert.IsTrue(_container.GetActiveSingletonInstances().Contains(instance.Independent));
        }

        [Test]
        public void RegisterInstance_InjectionFails_DisposesTheEvictedServiceAndEndsItsLifecycle()
        {
            _scope.Register<RecordingRollbackDependent>(ServiceLifetime.Singleton);
            _scope.Initialize();
            RecordingDependentFailingInjectService instance = new();

            Assert.Throws<InvalidOperationException>(() => _scope.RegisterInstance(instance));
            RecordingRollbackDependent dependent = instance.Dependent;
            Assert.AreEqual(1, dependent.InitializeCount);
            Assert.AreEqual(1, dependent.DisposeCount);

            _scope.Update();
            _scope.LateUpdate();
            _scope.Dispose();

            Assert.AreEqual(0, dependent.UpdateCount);
            Assert.AreEqual(0, dependent.LateUpdateCount);
            Assert.AreEqual(1, dependent.DisposeCount);
        }

        [Test]
        public void RegisterInstance_InjectionFailsBeforeTheScopeInitializes_DisposesTheEvictedServiceButNotTheInstance()
        {
            _scope.Register<RecordingRollbackDependent>(ServiceLifetime.Singleton);
            RecordingDependentFailingInjectService instance = new();

            Assert.Throws<InvalidOperationException>(() => _scope.RegisterInstance(instance));
            RecordingRollbackDependent dependent = instance.Dependent;
            Assert.AreEqual(1, dependent.DisposeCount);

            _scope.Initialize();
            _scope.Dispose();

            Assert.AreEqual(0, dependent.InitializeCount);
            Assert.AreEqual(1, dependent.DisposeCount);
            Assert.AreEqual(0, instance.InitializeCount);
            Assert.AreEqual(0, instance.DisposeCount);
        }

        [Test]
        public void RegisterInstance_InjectionFailsAfterTheScopeInitialized_DisposesTheInstanceItInitialized()
        {
            _scope.Register<DependsOnRollbackTarget>(ServiceLifetime.Transient);
            _scope.Initialize();
            SelfResolvingFailingInjectService instance = new();

            Assert.Throws<InvalidOperationException>(() => _scope.RegisterInstance(instance));
            Assert.AreEqual(1, instance.InitializeCount);
            Assert.AreEqual(1, instance.DisposeCount);

            _scope.Update();
            _scope.LateUpdate();
            _scope.Dispose();

            Assert.AreEqual(0, instance.UpdateCount);
            Assert.AreEqual(0, instance.LateUpdateCount);
            Assert.AreEqual(1, instance.DisposeCount);
        }

        [Test]
        public void RegisterInstance_InjectionFailsAfterTheScopeInitialized_DoesNotDisposeAnInstanceItDidNotInitialize()
        {
            _scope.Initialize();
            FailingInjectRecordingService instance = new();

            Assert.Throws<InvalidOperationException>(() => _scope.RegisterInstance(instance));
            _scope.Dispose();

            Assert.AreEqual(0, instance.InitializeCount);
            Assert.AreEqual(0, instance.DisposeCount);
        }

        [Test]
        public void RegisterInstance_InjectionFailsForAnInstanceRegisteredBefore_KeepsTheTrackingItStarted()
        {
            SelfResolvingFailingInjectService instance = new();
            _container.RegisterInstance(instance, registerInterfaces: false);
            _scope.Register<DependsOnRollbackTarget>(ServiceLifetime.Transient);
            _scope.Initialize();

            // The failed call's own IRollbackTarget registration hands the instance to the
            // dependent, which starts its tracking and runs its Initialize.
            Assert.Throws<InvalidOperationException>(() => _scope.RegisterInstance(instance));
            Assert.AreSame(instance, _scope.Resolve<SelfResolvingFailingInjectService>());
            _scope.Update();
            _scope.Dispose();

            Assert.AreEqual(1, instance.InitializeCount);
            Assert.AreEqual(1, instance.UpdateCount);
            Assert.AreEqual(1, instance.DisposeCount);
        }

        [Test]
        public void RegisterInstance_InjectionFails_LogsEachRegistrationAsRolledBack()
        {
            LogAssert.Expect(LogType.Log, "[DI Registration] Rolled back | Core: Unknown Core | Service: IServiceA | Implementation: FailingInjectRecordingService | Lifetime: Singleton");
            LogAssert.Expect(LogType.Log, "[DI Registration] Rolled back | Core: Unknown Core | Service: FailingInjectRecordingService | Implementation: FailingInjectRecordingService | Lifetime: Singleton");

            Assert.Throws<InvalidOperationException>(() => _scope.RegisterInstance(new FailingInjectRecordingService()));
        }

        [Test]
        public void RegisterInstance_InjectionFails_DisposeThatThrowsIsLoggedAndTheFailureStillReachesTheCaller()
        {
            _scope.Register<ThrowsOnDisposeRollbackDependent>(ServiceLifetime.Singleton);
            LogAssert.Expect(LogType.Exception, new Regex("rollback dispose boom"));

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => _scope.RegisterInstance(new ThrowingDependentFailingInjectService()));

            StringAssert.Contains($"{typeof(ThrowingDependentFailingInjectService).FullName}.{nameof(ThrowingDependentFailingInjectService.Missing)}", exception.Message);
        }

        [Test]
        public void RegisterInstance_InjectionFails_TellsServicesThatReceivedTheInstanceByReference()
        {
            _scope.Register<EqualToAllDependent>(ServiceLifetime.Singleton);
            _scope.Register<EqualToAllService>(ServiceLifetime.Singleton);
            _scope.Register<ConsumesEqualToAll>(ServiceLifetime.Singleton);
            EqualityConfusedFailingInjectService instance = new();

            Assert.Throws<InvalidOperationException>(() => _scope.RegisterInstance(instance));

            Assert.IsNotNull(instance.Dependent);
            Assert.IsNotNull(instance.Unrelated);
            Assert.AreSame(instance.Unrelated, _scope.Resolve<ConsumesEqualToAll>());
        }

        [Test]
        public void RegisterInstance_NestedRegistrationFailsInsideOneThatSucceeds_UndoesOnlyTheNestedOne()
        {
            _scope.Register<NestingHolder>(ServiceLifetime.Singleton);
            _scope.Register<DependsOnInner>(ServiceLifetime.Singleton);
            FailingInnerTarget inner = new();
            _container.RegisterInstance(new InnerSupply(inner));
            NestingOuterTarget outer = new();

            _scope.RegisterInstance(outer);

            NestingHolder holder = outer.Holder;
            Assert.IsTrue(holder.InnerFailed);
            Assert.AreSame(outer, holder.Outer);
            Assert.AreSame(outer, _scope.Resolve<IOuterTarget>());
            Assert.AreSame(holder, _scope.Resolve<NestingHolder>());
            Assert.AreEqual(0, holder.DisposeCount);
            Assert.IsFalse(_container.IsRegistered(typeof(IInnerTarget)));
            Assert.AreSame(inner, inner.Dependent.Target);
            Assert.AreEqual(1, inner.Dependent.DisposeCount);
            Assert.IsFalse(_container.GetActiveSingletonInstances().Contains(inner.Dependent));

            ValidInnerTarget replacement = new();
            _scope.RegisterInstance<IInnerTarget>(replacement);
            DependsOnInner resolved = _scope.Resolve<DependsOnInner>();
            Assert.AreNotSame(inner.Dependent, resolved);
            Assert.AreSame(replacement, resolved.Target);
            AssertAFailedRegistrationStillEvictsWhatReceivedTheInstance();
        }

        [Test]
        public void RegisterInstance_NestedRegistrationSucceedsInsideOneThatFails_UndoesOnlyTheOuterOne()
        {
            _scope.Register<NestingHolder>(ServiceLifetime.Singleton);
            _scope.Register<DependsOnInner>(ServiceLifetime.Singleton);
            ValidInnerTarget inner = new();
            _container.RegisterInstance(new InnerSupply(inner));
            FailingNestingOuterTarget outer = new();

            Assert.Throws<InvalidOperationException>(() => _scope.RegisterInstance(outer));

            NestingHolder holder = outer.Holder;
            Assert.IsFalse(holder.InnerFailed);
            Assert.AreSame(outer, holder.Outer);
            Assert.AreEqual(1, holder.DisposeCount);
            Assert.IsFalse(_container.IsRegistered(typeof(IOuterTarget)));
            Assert.IsFalse(_container.GetActiveSingletonInstances().Contains(holder));
            Assert.AreSame(inner, _scope.Resolve<IInnerTarget>());
            Assert.AreSame(inner.Dependent, _scope.Resolve<DependsOnInner>());
            Assert.AreSame(inner, inner.Dependent.Target);
            Assert.AreEqual(0, inner.Dependent.DisposeCount);

            NestingOuterTarget replacement = new();
            _scope.RegisterInstance<IOuterTarget>(replacement);
            Assert.AreNotSame(holder, replacement.Holder);
            Assert.AreSame(replacement, replacement.Holder.Outer);
            Assert.AreSame(replacement.Holder, _scope.Resolve<NestingHolder>());
            Assert.AreSame(inner.Dependent, _scope.Resolve<DependsOnInner>());
            AssertAFailedRegistrationStillEvictsWhatReceivedTheInstance();
        }

        // After nested registrations, a later failed one still takes back what received its instance.
        private void AssertAFailedRegistrationStillEvictsWhatReceivedTheInstance()
        {
            _scope.Register<DependsOnRollbackTarget>(ServiceLifetime.Singleton);
            SelfResolvingFailingInjectService instance = new();

            Assert.Throws<InvalidOperationException>(() => _scope.RegisterInstance(instance));

            Assert.AreSame(instance, instance.Dependent.Target);
            Assert.IsFalse(_container.IsRegistered(typeof(IRollbackTarget)));
            Assert.IsFalse(_container.GetActiveSingletonInstances().Contains(instance.Dependent));
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
