using UnityEditor;
using UnityEngine;

namespace Mu3Library.Tests
{
    /// <summary>
    /// Puts back the time scale a test changed. Setting it outside play mode marks the project's
    /// TimeManager settings dirty, and the editor would save them when it exits, so the mark goes too.
    /// </summary>
    internal static class TestTimeScale
    {
        public static void Restore(float timeScale)
        {
            Time.timeScale = timeScale;

            foreach (Object settings in AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TimeManager.asset"))
            {
                EditorUtility.ClearDirty(settings);
            }
        }
    }
}
