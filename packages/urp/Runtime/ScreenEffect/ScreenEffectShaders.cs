using System;
using System.Collections.Generic;
using UnityEngine;

namespace Mu3Library.URP.ScreenEffect
{
    /// <summary>
    /// Holds references to the shaders of the package's screen effects.
    /// <br/> The asset lives in a Resources folder of the package, so every player build includes it and,
    /// through its references, the shaders. <c>Shader.Find</c> alone only finds shaders that something
    /// in the build already references, which in a player left every screen effect without its shader.
    /// </summary>
    internal sealed class ScreenEffectShaders : ScriptableObject
    {
        internal const string ResourcePath = "Mu3Library.URP/ScreenEffectShaders";

        [SerializeField] private Shader[] _shaders = Array.Empty<Shader>();
        internal IReadOnlyList<Shader> Shaders => _shaders;

        private static ScreenEffectShaders s_loaded = null;
        private static readonly HashSet<string> s_reportedMissingShaders = new HashSet<string>();



        /// <summary>
        /// Returns the package's shader of that name, or the one <c>Shader.Find</c> finds for a name the
        /// package does not ship, such as the shader of a pass a project derives itself.
        /// </summary>
        internal static Shader Find(string shaderName)
        {
            if (string.IsNullOrEmpty(shaderName))
            {
                return null;
            }

            ScreenEffectShaders shaders = Load();
            Shader shader = shaders != null ? shaders.Get(shaderName) : null;
            if (shader != null)
            {
                return shader;
            }

            return Shader.Find(shaderName);
        }

        /// <summary>
        /// Logs that a shader could not be found, once per shader name until the next play session,
        /// however many passes, cameras, and frames ask for it.
        /// </summary>
        internal static void ReportMissing(string shaderName)
        {
            if (!s_reportedMissingShaders.Add(shaderName ?? string.Empty))
            {
                return;
            }

            Debug.LogError(
                $"Shader not found. path: {shaderName}\n" +
                "The screen effect that uses it draws nothing. A player finds only shaders that its build includes: " +
                "reference the shader from an asset the build includes, or add it to Always Included Shaders in Graphics settings.");
        }

        internal static ScreenEffectShaders Load()
        {
            if (s_loaded == null)
            {
                s_loaded = Resources.Load<ScreenEffectShaders>(ResourcePath);
            }

            return s_loaded;
        }

        internal Shader Get(string shaderName)
        {
            foreach (Shader shader in _shaders)
            {
                if (shader != null && shader.name == shaderName)
                {
                    return shader;
                }
            }

            return null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetReportedMissingShaders()
        {
            // Without a domain reload the set would survive into the next play session and hide its errors.
            s_reportedMissingShaders.Clear();
        }
    }
}
