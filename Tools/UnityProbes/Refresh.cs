using UnityEditor;

// Editor probe only: picks up script edits made outside Unity.
public static class Refresh
{
    public static string Execute() { AssetDatabase.Refresh(); return "refreshed, compiling=" + EditorApplication.isCompiling; }
}
