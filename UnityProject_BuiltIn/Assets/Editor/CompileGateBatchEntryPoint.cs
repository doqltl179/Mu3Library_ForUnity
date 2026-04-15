using UnityEditor;
using UnityEngine;

public static class CompileGateBatchEntryPoint
{
    public static void Run()
    {
        Debug.Log("COMPILE_GATE_START");
        EditorApplication.update += WaitForCompile;
        WaitForCompile();
    }

    private static void WaitForCompile()
    {
        if (EditorApplication.isCompiling) {
            return;
        }

        EditorApplication.update -= WaitForCompile;

        if (EditorUtility.scriptCompilationFailed) {
            Debug.LogError("COMPILE_FAILED");
            EditorApplication.Exit(1);
            return;
        }

        Debug.Log("COMPILE_OK");
        EditorApplication.Exit(0);
    }
}