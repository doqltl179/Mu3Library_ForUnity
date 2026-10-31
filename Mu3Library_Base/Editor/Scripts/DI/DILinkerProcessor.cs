using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security;
using System.Text;
using Mu3Library.DI;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.Compilation;
using UnityEditor.UnityLinker;
using UnityEngine;
using Assembly = System.Reflection.Assembly;

namespace Mu3Library.Editor.DI
{
    /// <summary>
    /// Hands the Unity linker a link.xml that keeps what the DI container reaches only through
    /// reflection: the constructors it chooses from and the [Inject] fields and properties it fills.
    /// </summary>
    /// <remarks>
    /// <para>
    /// With Managed Stripping Level Medium or High the linker removes members that no code calls.
    /// For the container those are a constructor with parameters, every constructor of a class
    /// registered through <c>ContainerScope.Register</c>, which has no <c>new()</c> constraint,
    /// and the setter of an [Inject] property. Unity does not read a link.xml inside a package,
    /// so this writes one for each build.
    /// </para>
    /// <para>
    /// Constructors are kept in every assembly that can hold a registered class: the player script
    /// assemblies and loaded precompiled assemblies that reference Mu3Library.DI, and every player
    /// script or precompiled assembly they reference, directly or not, since a registration can name
    /// a class of an assembly that knows nothing of the container. A wildcard entry lets the linker
    /// match them against the assemblies it actually links, so a class that only the player
    /// compiles is covered as well. Precompiled assemblies are matched by file name: a DLL whose
    /// file name differs from its assembly name is skipped without a warning, and one the editor
    /// does not load is covered only when one of those assemblies references it.
    /// </para>
    /// <para>
    /// [Inject] members are listed by name, read from the assemblies the editor has loaded that
    /// reference Mu3Library.DI. A member that only the player compiles, inside
    /// <c>#if !UNITY_EDITOR</c> for example, or one in an assembly the editor does not load, is
    /// not listed. An entry for a member the player lacks is ignored.
    /// </para>
    /// <para>
    /// Every entry is conditional (<c>required="0"</c>, <c>ignoreIfUnreferenced="1"</c>): a class
    /// or assembly the build drops because nothing uses it stays dropped, and a class something
    /// else keeps also keeps its constructors and [Inject] members.
    /// </para>
    /// </remarks>
    internal sealed class DILinkerProcessor : IUnityLinkerProcessor
    {
        private const BindingFlags DeclaredMemberFlags =
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

        public int callbackOrder => 0;



        public string GenerateAdditionalLinkXmlFile(BuildReport report, UnityLinkerBuildPipelineData data)
        {
            string diAssemblyName = typeof(InjectAttribute).Assembly.GetName().Name;

#if UNITY_6000_5_OR_NEWER
            IEnumerable<Assembly> loaded = UnityEngine.Assemblies.CurrentAssemblies.GetLoadedAssemblies();
#else
            IEnumerable<Assembly> loaded = AppDomain.CurrentDomain.GetAssemblies();
#endif
            Dictionary<string, Assembly> loadedByName = new(StringComparer.Ordinal);
            foreach (Assembly assembly in loaded)
            {
                if (!assembly.IsDynamic)
                {
                    loadedByName[assembly.GetName().Name] = assembly;
                }
            }

            // The references of the script assemblies come from their player compilation, so an
            // assembly the editor does not compile still counts. A precompiled assembly's own
            // references are known only once the editor has loaded it.
            HashSet<string> precompiled = new(
                CompilationPipeline.GetPrecompiledAssemblyPaths(CompilationPipeline.PrecompiledAssemblySources.UserAssembly)
                    .Select(Path.GetFileNameWithoutExtension),
                StringComparer.Ordinal);
            Dictionary<string, IReadOnlyCollection<string>> references = new(StringComparer.Ordinal);
            foreach (string name in precompiled)
            {
                references[name] = loadedByName.TryGetValue(name, out Assembly assembly)
                    ? GetReferencedNames(assembly)
                    : Array.Empty<string>();
            }
            foreach (UnityEditor.Compilation.Assembly script in CompilationPipeline.GetAssemblies(AssembliesType.Player))
            {
                references[script.name] = script.assemblyReferences.Select(reference => reference.name)
                    .Concat(script.compiledAssemblyReferences.Select(Path.GetFileNameWithoutExtension).Where(precompiled.Contains))
                    .ToArray();
            }

            IReadOnlyCollection<string> constructorAssemblies = SelectAssemblies(references, diAssemblyName);
            IEnumerable<Assembly> injectAssemblies = constructorAssemblies
                .Where(name => references[name].Contains(diAssemblyName) && loadedByName.ContainsKey(name))
                .Select(name => loadedByName[name]);

            string path = Path.Combine(Path.GetDirectoryName(Application.dataPath), "Library", "Mu3Library", "DI.link.xml");
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, BuildLinkXml(constructorAssemblies, injectAssemblies));
            return path;
        }

        /// <summary>
        /// The assemblies that reference <paramref name="diAssemblyName"/> directly and every
        /// assembly of <paramref name="references"/> they reach through their references, in name order.
        /// </summary>
        internal static IReadOnlyCollection<string> SelectAssemblies(
            IReadOnlyDictionary<string, IReadOnlyCollection<string>> references, string diAssemblyName)
        {
            SortedSet<string> selected = new(StringComparer.Ordinal);
            Queue<string> pending = new(references
                .Where(pair => pair.Value.Contains(diAssemblyName))
                .Select(pair => pair.Key));

            while (pending.Count > 0)
            {
                string name = pending.Dequeue();
                if (!selected.Add(name))
                {
                    continue;
                }

                foreach (string reference in references[name])
                {
                    if (references.ContainsKey(reference))
                    {
                        pending.Enqueue(reference);
                    }
                }
            }

            return selected;
        }

        /// <summary>
        /// The link.xml that keeps the constructors of every class in
        /// <paramref name="constructorAssemblies"/> and the [Inject] members declared in
        /// <paramref name="injectAssemblies"/>.
        /// </summary>
        internal static string BuildLinkXml(IEnumerable<string> constructorAssemblies, IEnumerable<Assembly> injectAssemblies)
        {
            SortedDictionary<string, StringBuilder> entries = new(StringComparer.Ordinal);
            foreach (string name in constructorAssemblies)
            {
                Entries(entries, name)
                    .AppendLine("    <type fullname=\"*\" preserve=\"nothing\" required=\"0\">")
                    .AppendLine("      <method name=\".ctor\" />")
                    .AppendLine("    </type>");
            }

            foreach (Assembly assembly in injectAssemblies)
            {
                foreach (Type type in GetTypes(assembly).OrderBy(type => type.FullName, StringComparer.Ordinal))
                {
                    string entry = BuildInjectEntry(type);
                    if (entry != null)
                    {
                        Entries(entries, assembly.GetName().Name).Append(entry);
                    }
                }
            }

            StringBuilder xml = new StringBuilder();
            xml.AppendLine("<linker>");
            foreach (KeyValuePair<string, StringBuilder> pair in entries)
            {
                xml.AppendLine($"  <assembly fullname=\"{SecurityElement.Escape(pair.Key)}\" ignoreIfMissing=\"1\" ignoreIfUnreferenced=\"1\">");
                xml.Append(pair.Value);
                xml.AppendLine("  </assembly>");
            }
            xml.AppendLine("</linker>");
            return xml.ToString();
        }

        private static StringBuilder Entries(SortedDictionary<string, StringBuilder> entries, string assemblyName)
        {
            if (!entries.TryGetValue(assemblyName, out StringBuilder builder))
            {
                builder = new StringBuilder();
                entries.Add(assemblyName, builder);
            }

            return builder;
        }

        private static string BuildInjectEntry(Type type)
        {
            if (type == null || type.FullName == null)
            {
                return null;
            }

            List<string> fields = type.GetFields(DeclaredMemberFlags)
                .Where(field => field.IsDefined(typeof(InjectAttribute), false))
                .Select(field => field.Name)
                .ToList();
            List<string> properties = type.GetProperties(DeclaredMemberFlags)
                .Where(property => property.IsDefined(typeof(InjectAttribute), false))
                .Select(property => property.Name)
                .ToList();

            if (fields.Count == 0 && properties.Count == 0)
            {
                return null;
            }

            StringBuilder entry = new StringBuilder();
            entry.AppendLine($"    <type fullname=\"{SecurityElement.Escape(type.FullName.Replace('+', '/'))}\" preserve=\"nothing\" required=\"0\">");
            foreach (string field in fields)
            {
                entry.AppendLine($"      <field name=\"{SecurityElement.Escape(field)}\" />");
            }
            foreach (string property in properties)
            {
                entry.AppendLine($"      <property name=\"{SecurityElement.Escape(property)}\" />");
            }
            entry.AppendLine("    </type>");
            return entry.ToString();
        }

        private static IReadOnlyCollection<string> GetReferencedNames(Assembly assembly)
        {
            try
            {
                return assembly.GetReferencedAssemblies().Select(reference => reference.Name).ToArray();
            }
            catch (Exception)
            {
                return Array.Empty<string>();
            }
        }

        private static IEnumerable<Type> GetTypes(Assembly assembly)
        {
            try
            {
                return assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                return ex.Types.Where(type => type != null);
            }
        }
    }
}
