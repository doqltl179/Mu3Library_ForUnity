using System.Collections.Generic;
using Mu3Library.DI;
using Mu3Library.UI.MVP;
using UnityEditor;
using UnityEngine;

#if MU3LIBRARY_ADDRESSABLES_SUPPORT
using Mu3Library.Addressable;
#endif

namespace Mu3Library.Editor.Window.Drawer
{
    /// <summary>
    /// Shows what the running services hold right now: the presenters the MVP manager manages
    /// and the entries the Addressables cache keeps. Reading the state changes nothing.
    /// </summary>
    [CreateAssetMenu(fileName = FileName, menuName = MenuName, order = 0)]
    public class RuntimeDiagnosticsDrawer : Mu3WindowDrawer
    {
        public const string FileName = "RuntimeDiagnostics";
        private const string ItemName = "Runtime Diagnostics";
        private const string MenuName = MenuRoot + "/" + ItemName;



        public override void OnGUIHeader()
        {
            DrawFoldoutHeader1(ItemName, ref _foldout);
        }

        public override void OnGUIBody()
        {
            DrawStruct(() =>
            {
                if (!Application.isPlaying)
                {
                    EditorGUILayout.HelpBox("Runtime state exists while the game runs. Enter play mode to inspect.", MessageType.Info);
                    return;
                }

                CoreRoot coreRoot = Object.FindFirstObjectByType<CoreRoot>();
                if (coreRoot == null || coreRoot.RegisteredCores.Count == 0)
                {
                    EditorGUILayout.HelpBox("No registered core found.", MessageType.Info);
                    return;
                }

                foreach (CoreBase core in coreRoot.RegisteredCores)
                {
                    if (core == null)
                    {
                        continue;
                    }

                    DrawCoreDiagnostics(core);
                    GUILayout.Space(8);
                }
            }, 20, 20, 0, 0);
        }

        private void DrawCoreDiagnostics(CoreBase core)
        {
            DrawHeader3($"[ {core.GetType().Name} ]");

            bool anyDrawn = false;
            foreach (object instance in core.GetActiveSingletonInstances())
            {
                if (instance is MVPManager mvpManager)
                {
                    DrawMVPDiagnostics(mvpManager);
                    anyDrawn = true;
                }

#if MU3LIBRARY_ADDRESSABLES_SUPPORT
                if (instance is AddressablesManager addressablesManager)
                {
                    DrawAddressablesDiagnostics(addressablesManager);
                    anyDrawn = true;
                }
#endif
            }

            if (!anyDrawn)
            {
                EditorGUILayout.LabelField("No inspectable service in this core.");
            }
        }

        private void DrawMVPDiagnostics(MVPManager mvpManager)
        {
            List<string> lines = mvpManager.GetPresenterDiagnostics();
            EditorGUILayout.LabelField($"MVP presenters: {lines.Count}");

            foreach (string line in lines)
            {
                EditorGUILayout.LabelField($"- {line}");
            }
        }

#if MU3LIBRARY_ADDRESSABLES_SUPPORT
        private void DrawAddressablesDiagnostics(AddressablesManager addressablesManager)
        {
            EditorGUILayout.LabelField($"Addressables cached assets: {addressablesManager.CachedAssetCount}");
            EditorGUILayout.LabelField($"Addressables tracked handles: {addressablesManager.TrackedHandleCount}");

            foreach (object key in addressablesManager.CachedBaseKeys)
            {
                EditorGUILayout.LabelField($"- {key}");
            }
        }
#endif
    }
}
