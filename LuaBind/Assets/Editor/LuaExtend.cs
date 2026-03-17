using System.IO;
using UnityEditor;
using UnityEditor.ProjectWindowCallback;
using UnityEngine;

public class LuaExtend
{
    static string tempPath = "Assets/LuaScript/Manager/LuaScriptTemp.lua";
    [MenuItem("Assets/Create/创建Lua脚本", false, 5)]
    public static void CreateLuaScript()
    {
        string folder = GetSelectedFolder();
        string defaultPath = AssetDatabase.GenerateUniqueAssetPath(Path.Combine(folder, "NewLuaScript.lua").Replace("\\", "/"));
        Texture2D icon = EditorGUIUtility.IconContent("TextAsset Icon").image as Texture2D;

        ProjectWindowUtil.StartNameEditingIfProjectWindowExists(
            0,
            ScriptableObject.CreateInstance<CreateLuaEndAction>(),
            defaultPath,
            icon,
            null
        );
    }

    private static string GetSelectedFolder()
    {
        Object selected = Selection.activeObject;
        string path = selected == null ? "Assets" : AssetDatabase.GetAssetPath(selected);
        if (string.IsNullOrEmpty(path)) return "Assets";
        if (AssetDatabase.IsValidFolder(path)) return path;
        return Path.GetDirectoryName(path)?.Replace("\\", "/") ?? "Assets";
    }

    private class CreateLuaEndAction : EndNameEditAction
    {
        public override void Action(int instanceId, string pathName, string resourceFile)
        {
            string str = File.ReadAllText(tempPath);
            str = str.Replace("temp", Path.GetFileNameWithoutExtension(pathName));
            File.WriteAllText(pathName, str);
            AssetDatabase.ImportAsset(pathName);
            Object created = AssetDatabase.LoadAssetAtPath<Object>(pathName);
            ProjectWindowUtil.ShowCreatedAsset(created);
        }
    }
}
