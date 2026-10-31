using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security;
using System.Text;
using Mu3Library.DI;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.Compilation;
using UnityEditor.UnityLinker;
using Assembly = System.Reflection.Assembly;

namespace Mu3Library.Editor.DI
{
    /// <summary>
    /// Hands the Unity linker a link.xml that keeps what the DI container reaches only through
    /// reflection: the constructors it chooses from and the [Inject] fields and properties it fills.
    /// </summary>
    /// <remarks>
    /// With Managed Stripping Level Medium or High the linker removes members that no code calls.
    /// For the container those are a constructor with parameters, every constructor of a class
    /// registered through <c>ContainerScope.Register</c>, which has no <c>new()</c> constraint,
    /// and the setter of an [Inject] property. Unity does not read a link.xml inside a package,
    /// so this writes one for each build.
    /// It lists the classes of the player's script assemblies that reference Mu3Library.DI, since
    /// only they can register a service or declare an [Inject] member. Every entry is conditional
    /// (<c>required="0"</c>, <c>ignoreIfUnreferenced="1"</c>): a class or assembly the build drops
    /// because nothing uses it stays dropped, and a class something else keeps also keeps its
    /// constructors and [Inject] members.
    /// </remarks>
    internal sealed class DILinkerProcessor : IUnityLinkerProcessor
    {
        private const string OutputPath = "Library/Mu3Library/DI.link.xml";

        private const BindingFlags DeclaredMemberFlags =
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

        public int callbackOrder => 0;



        public string GenerateAdditionalLinkXmlFile(BuildReport report, UnityLinkerBuildPipelineData data)
        {
            // The editor holds the same script assemblies the player build compiles; listing only
            // those leaves out editor assemblies that reference Mu3Library.DI.
            HashSet<string> playerAssemblies = new HashSet<string>(
                CompilationPipeline.GetAssemblies(AssembliesType.Player).Select(assembly => assembly.name),
                StringComparer.Ordinal);
#if UNITY_6000_5_OR_NEWER
            IEnumerable<Assembly> loadedAssemblies = UnityEngine.Assemblies.CurrentAssemblies.GetLoadedAssemblies();
#else
            IEnumerable<Assembly> loadedAssemblies = AppDomain.CurrentDomain.GetAssemblies();
#endif
            IEnumerable<Assembly> assemblies = loadedAssemblies
                .Where(assembly => playerAssemblies.Contains(assembly.GetName().Name));

            string path = Path.GetFullPath(OutputPath);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, BuildLinkXml(assemblies));
            return path;
        }

        /// <summary>
        /// The link.xml for the given assemblies; only those that reference Mu3Library.DI add entries.
        /// </summary>
        internal static string BuildLinkXml(IEnumerable<Assembly> assemblies)
        {
            string diAssemblyName = typeof(InjectAttribute).Assembly.GetName().Name;

            StringBuilder xml = new StringBuilder();
            xml.AppendLine("<linker>");

            foreach (Assembly assembly in assemblies
                .Where(assembly => !assembly.IsDynamic && ReferencesAssembly(assembly, diAssemblyName))
                .OrderBy(assembly => assembly.GetName().Name, StringComparer.Ordinal))
            {
                List<string> types = new();
                foreach (Type type in GetTypes(assembly).OrderBy(type => type.FullName, StringComparer.Ordinal))
                {
                    string entry = BuildTypeEntry(type);
                    if (entry != null)
                    {
                        types.Add(entry);
                    }
                }

                if (types.Count == 0)
                {
                    continue;
                }

                xml.AppendLine($"  <assembly fullname=\"{SecurityElement.Escape(assembly.GetName().Name)}\" ignoreIfMissing=\"1\" ignoreIfUnreferenced=\"1\">");
                foreach (string entry in types)
                {
                    xml.Append(entry);
                }
                xml.AppendLine("  </assembly>");
            }

            xml.AppendLine("</linker>");
            return xml.ToString();
        }

        private static string BuildTypeEntry(Type type)
        {
            if (type == null || !type.IsClass || type.FullName == null ||
                type.IsDefined(typeof(CompilerGeneratedAttribute), false) ||
                typeof(Delegate).IsAssignableFrom(type) ||
                typeof(UnityEngine.Object).IsAssignableFrom(type))
            {
                return null;
            }

            bool keepsConstructors = !type.IsAbstract && type.GetConstructors().Length > 0;
            List<string> fields = type.GetFields(DeclaredMemberFlags)
                .Where(field => field.IsDefined(typeof(InjectAttribute), false))
                .Select(field => field.Name)
                .ToList();
            List<string> properties = type.GetProperties(DeclaredMemberFlags)
                .Where(property => property.IsDefined(typeof(InjectAttribute), false))
                .Select(property => property.Name)
                .ToList();

            if (!keepsConstructors && fields.Count == 0 && properties.Count == 0)
            {
                return null;
            }

            StringBuilder entry = new StringBuilder();
            entry.AppendLine($"    <type fullname=\"{SecurityElement.Escape(type.FullName.Replace('+', '/'))}\" preserve=\"nothing\" required=\"0\">");
            if (keepsConstructors)
            {
                entry.AppendLine("      <method name=\".ctor\" />");
            }
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

        private static bool ReferencesAssembly(Assembly assembly, string name)
        {
            try
            {
                return assembly.GetReferencedAssemblies().Any(reference => reference.Name == name);
            }
            catch (Exception)
            {
                return false;
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
