using UnityEngine;

public static class Logger
{
    [System.Diagnostics.Conditional("ENABLE_LOG")]
    public static void Log(string message)
    {
        Debug.Log(message);
    }

    [System.Diagnostics.Conditional("ENABLE_LOG")]
    public static void LogWarning(string message)
    {
        Debug.LogWarning(message);
    }

    [System.Diagnostics.Conditional("ENABLE_LOG")]
    public static void LogError(string message)
    {
        Debug.LogError(message);
    }

    [System.Diagnostics.Conditional("ENABLE_LOG")]
    public static void DrawRay(Vector3 start, Vector3 dir, Color color, float duration)
    {
        Debug.DrawRay(start, dir, color, duration);
    }
}