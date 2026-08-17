using System;
using System.Linq;
using Mu3Library.DI;
using NUnit.Framework;

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
    }
}
