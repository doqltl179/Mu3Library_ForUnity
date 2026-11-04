using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using Mu3Library.DI;
using StripHarness.Domain;
using StripHarness.Plugin;
using UnityEngine;

// The boot of the DI stripping harness (tools/README.md#di-stripping-harness). The scene is empty:
// HarnessBoot creates itself and then the cores at runtime, each check logs one HARNESS CHECK line,
// and the player quits with 0 only when every check passed and nothing logged an error or
// exception. mu3-cli unity di-strip-harness reads these lines from the player's log.
namespace StripHarness
{
    public sealed class HarnessOtherCore : CoreBase
    {
        protected override void ConfigureContainer()
        {
            RegisterClass<Remote>();
        }
    }

    public sealed class HarnessCore : CoreBase
    {
        [Inject] private IGreeter _greeter = null;
        public IGreeter InjectedGreeter => _greeter;
        [Inject] public IClock CoreClock { get; private set; }

        protected override void ConfigureContainer()
        {
            RegisterClass<Clock>();
            RegisterClass<Greeter>();
            RegisterClass<Installer>();
            RegisterClass<Consumer>();
        }

        public T Get<T>() where T : class => GetClass<T>();
        public T Get<T>(string key) where T : class => GetClass<T>(key);
    }

    public sealed class HarnessBoot : MonoBehaviour
    {
        private int _checks;
        private int _failures;
        private int _errorLogs;
        private readonly List<string> _errors = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void CreateBoot()
        {
            new GameObject(nameof(HarnessBoot)).AddComponent<HarnessBoot>();
        }

        private IEnumerator Start()
        {
            Application.logMessageReceived += OnLog;
#if ENABLE_IL2CPP
            string backend = "il2cpp";
#elif ENABLE_MONO
            string backend = "mono";
#else
            string backend = "unknown";
#endif
            Debug.Log($"HARNESS START unity={Application.unityVersion} backend={backend} debugBuild={Debug.isDebugBuild}");

            bool initialized = false;
            bool prepared = false;
            CoreRoot.Instance.SubscribeOnCoreInitializedOnce<HarnessCore>(() => initialized = true);
            CoreRoot.Instance.SubscribeOnCorePreparedOnce<HarnessCore>(() => prepared = true);

            new GameObject(nameof(HarnessOtherCore)).AddComponent<HarnessOtherCore>();
            HarnessCore core = new GameObject(nameof(HarnessCore)).AddComponent<HarnessCore>();

            for (int i = 0; i < 10; i++)
            {
                yield return null;
            }

            Describe(typeof(Clock));
            Describe(typeof(Greeter));
            Describe(typeof(Installer));
            Describe(typeof(Score));
            Describe(typeof(PluginA));
            Describe(typeof(OnlyCtor));
            Describe(typeof(Consumer));
            Describe(typeof(ConsumerBase));
            Describe(typeof(PreMade));
            Describe(typeof(Manual));
            Describe(typeof(HarnessCore));
            Describe(typeof(ScoreService));
            Describe(typeof(PluginService));
            Describe(typeof(Outer.NestedService));
            DescribeContainerScope();
            string unusedName = string.Join(".", new[] { "StripHarness", "UnusedService" }) + ", Assembly-CSharp";
            Debug.Log($"HARNESS UNUSED UnusedService present={Type.GetType(unusedName) != null}");
            // Keeps Base's runtime assembly in the build without creating anything from it, so its
            // size shows what the link.xml adds to a referenced assembly.
            Debug.Log($"HARNESS REFERENCED {typeof(Mu3Library.UI.MVP.MVPManager).Name}");

            Check("core.initialized-event", () => initialized, () => $"initialized={initialized}");
            Check("core.prepared", () => prepared && core.IsPrepared, () => $"event={prepared} isPrepared={core.IsPrepared}");
            Check("core.inject-field", () => core.InjectedGreeter != null, () => $"{core.InjectedGreeter}");
            Check("core.inject-private-set-property", () => core.CoreClock != null, () => $"{core.CoreClock}");
            Check("ctor.di-free-asmdef", () => core.Get<IScoreService>()?.Repo?.Count == 3, () => $"{SafeDescribe(() => core.Get<IScoreService>()?.Repo)}");
            Check("ctor.precompiled-dll", () => core.Get<IPluginService>()?.Repo?.Count == 5, () => $"{SafeDescribe(() => core.Get<IPluginService>()?.Repo)}");
            Check("ctor.nested-type", () => core.Get<INested>()?.Clock != null, () => $"{SafeDescribe(() => core.Get<INested>()?.Clock)}");
            Check("ctor.player-only-type", () => core.Get<IPlayerOnly>()?.Clock != null, () => $"{SafeDescribe(() => core.Get<IPlayerOnly>()?.Clock)}");

            Check("register.default-ctor", () => core.Get<IClock>()?.Now == 42, () => $"{core.Get<IClock>()}");
            Check("ctor.longest-with-param", () => core.Get<IGreeter>()?.CtorUsed == "IClock",
                () => $"ctorUsed={core.Get<IGreeter>()?.CtorUsed} greet={core.Get<IGreeter>()?.Greet()}");
            Check("ctor.scope-param", () => core.Get<Installer>()?.Installed == true, () => $"installed={core.Get<Installer>()?.Installed}");
            Check("register.keyed-no-new-constraint", () => core.Get<IScore>("special")?.Value == 7, () => $"{SafeDescribe(() => core.Get<IScore>("special"))}");
            Check("ctor.only-non-default", () => core.Get<IOnlyCtor>()?.Clock != null, () => $"{SafeDescribe(() => core.Get<IOnlyCtor>())}");
            Check("register.factory+field-inject", () => core.Get<IFactoryMade>()?.Clock != null, () => $"{SafeDescribe(() => core.Get<IFactoryMade>()?.Clock)}");
            Check("register.instance+property-inject", () => core.Get<IPreMade>()?.Clock != null, () => $"{SafeDescribe(() => core.Get<IPreMade>()?.Clock)}");

            Consumer consumer = Safe(() => core.Get<Consumer>());
            Check("consumer.resolved", () => consumer != null, () => "null");
            Check("inject.field", () => consumer?.Greeter != null, () => $"{consumer?.Greeter}");
            Check("inject.base-private-field", () => consumer?.BaseClock != null, () => $"{consumer?.BaseClock}");
            Check("inject.auto-property-private-set", () => consumer?.AutoProperty != null, () => $"{consumer?.AutoProperty}");
            Check("inject.explicit-property", () => consumer?.ExplicitProperty != null, () => $"{consumer?.ExplicitProperty}");
            Check("inject.keyed-field", () => consumer?.Special?.Value == 7, () => $"{consumer?.Special}");
            Check("inject.other-core", () => consumer?.Remote?.Where == "other", () => $"{consumer?.Remote}");
            Check("inject.optional-missing", () => consumer != null && consumer.Missing == null, () => $"{consumer?.Missing}");
            Check("inject.enumerable", () => consumer?.Plugins != null && string.Join(",", consumer.Plugins.Select(p => p.Name)) == "A,B",
                () => consumer?.Plugins == null ? "null" : string.Join(",", consumer.Plugins.Select(p => p.Name)));
            Check("inject.func", () => consumer?.ClockFactory?.Invoke()?.Now == 42, () => $"{consumer?.ClockFactory}");
            Check("inject.lazy", () => consumer?.LazyGreeter?.Value?.CtorUsed == "IClock", () => $"{SafeDescribe(() => consumer?.LazyGreeter?.Value?.CtorUsed)}");
            Check("inject.object-injector", () =>
            {
                Manual manual = new Manual();
                consumer?.Injector?.Inject(manual);
                return manual.Clock != null;
            }, () => $"injector={consumer?.Injector}");

            // A required static [Inject] member makes the injection throw before it fills anything,
            // as in the Editor, only if the player still has the member for the container to find.
            string staticField = StaticInjectFailure(consumer?.Injector, new StaticFieldHolder(),
                "field is static: StripHarness.StaticFieldHolder._strippedStaticClock");
            Check("inject.required-static-field-throws", () => staticField == null, () => staticField);
            string staticProperty = StaticInjectFailure(consumer?.Injector, new StaticPropertyHolder(),
                "property is static: StripHarness.StaticPropertyHolder.StrippedStaticClock");
            Check("inject.required-static-property-throws", () => staticProperty == null, () => staticProperty);
            string baseStaticField = StaticInjectFailure(consumer?.Injector, new DerivedStaticFieldHolder(),
                "field is static: StripHarness.DerivedStaticFieldHolder._strippedBaseStaticClock");
            Check("inject.required-static-base-field-throws", () => baseStaticField == null, () => baseStaticField);

            // An instance [Inject] field that no code reads is filled only if the player kept its
            // attribute for the container to find; filling it creates one transient service.
            string unreadField = UnreadFieldInjectFailure(consumer?.Injector, new UnreadFieldHolder());
            Check("inject.unread-instance-field", () => unreadField == null, () => unreadField);

#if !UNITY_EDITOR
            Check("inject.player-only-member-with-preserve", () => consumer?.PlayerOnlyPreserved != null, () => $"{consumer?.PlayerOnlyPreserved}");
            Debug.Log($"HARNESS INFO player-only member without [Preserve] injected={consumer?.PlayerOnlyUnpreserved != null}");
#endif
            Check("lifecycle.initialize", () => Probe.Count("Clock.Initialize") == 1 && Probe.Count("Consumer.Initialize") == 1 && Probe.Count("OnlyCtor.Initialize") == 1,
                () => $"clock={Probe.Count("Clock.Initialize")} consumer={Probe.Count("Consumer.Initialize")} onlyCtor={Probe.Count("OnlyCtor.Initialize")}");
            Check("lifecycle.update", () => Probe.Count("Clock.Update") >= 5 && Probe.Count("Clock.LateUpdate") >= 5,
                () => $"update={Probe.Count("Clock.Update")} lateUpdate={Probe.Count("Clock.LateUpdate")}");

            Destroy(core.gameObject);
            yield return null;
            yield return null;

            Check("lifecycle.dispose", () => Probe.Count("Clock.Dispose") == 1, () => $"dispose={Probe.Count("Clock.Dispose")}");

            foreach (string error in _errors)
            {
                Debug.Log($"HARNESS ERRORLOG {error}");
            }

            bool pass = _failures == 0 && _errorLogs == 0;
            Debug.Log($"HARNESS RESULT {(pass ? "PASS" : "FAIL")} checks={_checks} failures={_failures} errorLogs={_errorLogs}");
            Application.logMessageReceived -= OnLog;
            Application.Quit(pass ? 0 : 3);
        }

        private void OnLog(string condition, string stackTrace, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
            {
                _errorLogs++;
                string firstFrame = stackTrace?.Split('\n').FirstOrDefault(line => line.Length > 0) ?? "";
                _errors.Add($"{type}: {condition} @ {firstFrame}");
            }
        }

        private void Check(string name, Func<bool> condition, Func<string> detail)
        {
            bool ok;
            string info;
            try
            {
                ok = condition();
                info = ok ? "" : detail();
            }
            catch (Exception ex)
            {
                ok = false;
                info = $"{ex.GetType().Name}: {ex.Message}";
            }

            _checks++;
            if (!ok)
            {
                _failures++;
            }

            Debug.Log($"HARNESS CHECK {name} {(ok ? "PASS" : "FAIL")} {info}");
        }

        private static T Safe<T>(Func<T> get) where T : class
        {
            try
            {
                return get();
            }
            catch (Exception ex)
            {
                Debug.Log($"HARNESS EXCEPTION {ex.GetType().Name}: {ex.Message}");
                return null;
            }
        }

        // Null when injecting target threw the exception whose message holds expected; otherwise
        // what happened instead.
        private static string StaticInjectFailure(IObjectInjector injector, object target, string expected)
        {
            if (injector == null)
            {
                return "no injector";
            }

            try
            {
                injector.Inject(target);
                Type type = target.GetType();
                return $"no exception (static [Inject] fields found={InjectableMembers.GetStaticFields(type).Count} properties found={InjectableMembers.GetStaticProperties(type).Count})";
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains(expected))
            {
                return null;
            }
            catch (Exception ex)
            {
                return $"{ex.GetType().Name}: {ex.Message}";
            }
        }

        // Null when injecting target created exactly one UnreadFieldService; otherwise what
        // happened instead. Reads the type through the object, as StaticInjectFailure does.
        private static string UnreadFieldInjectFailure(IObjectInjector injector, object target)
        {
            if (injector == null)
            {
                return "no injector";
            }

            int before = Probe.Count("UnreadFieldService.ctor");
            try
            {
                injector.Inject(target);
            }
            catch (Exception ex)
            {
                return $"{ex.GetType().Name}: {ex.Message}";
            }

            int created = Probe.Count("UnreadFieldService.ctor") - before;
            return created == 1
                ? null
                : $"services created={created} ([Inject] fields found={InjectableMembers.GetFields(target.GetType()).Count})";
        }

        private static string SafeDescribe(Func<object> get)
        {
            try
            {
                object value = get();
                return value == null ? "null" : value.ToString();
            }
            catch (Exception ex)
            {
                return $"{ex.GetType().Name}: {ex.Message}";
            }
        }

        // Reads the types through a parameter so that the linker sees no reflection on a known
        // type here and keeps exactly what it would keep without the harness.
        private static void Describe(Type type)
        {
            const BindingFlags all = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
            StringBuilder sb = new StringBuilder();
            sb.Append($"HARNESS TYPE {type.Name}: ctors=[");
            sb.Append(string.Join(",", type.GetConstructors(all).Select(c => $"({string.Join(",", c.GetParameters().Select(p => p.ParameterType.Name))})")));
            sb.Append("] fields=[");
            sb.Append(string.Join(",", type.GetFields(all).Select(f => $"{f.Name}{(f.GetCustomAttributes(false).Any(a => a is InjectAttribute) ? "@Inject" : "")}")));
            sb.Append("] props=[");
            sb.Append(string.Join(",", type.GetProperties(all).Select(p => $"{p.Name}(get={p.GetGetMethod(true) != null},set={p.GetSetMethod(true) != null}){(p.GetCustomAttributes(false).Any(a => a is InjectAttribute) ? "@Inject" : "")}")));
            sb.Append("] interfaces=[");
            sb.Append(string.Join(",", type.GetInterfaces().Select(i => i.Name)));
            sb.Append($"] injectableFields={InjectableMembers.GetFields(type).Count} injectableProps={InjectableMembers.GetProperties(type).Count}");
            Debug.Log(sb.ToString());
        }

        private static void DescribeContainerScope()
        {
            Type scope = typeof(CoreBase).Assembly.GetType("Mu3Library.DI.Container" + "Scope");
            const BindingFlags all = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
            string methods = scope == null ? "type missing" : string.Join(",", scope.GetMethods(all).Select(m => m.Name).Where(n => n.StartsWith("Create")));
            Debug.Log($"HARNESS TYPE ContainerScope Create* methods=[{methods}]");
        }
    }
}
