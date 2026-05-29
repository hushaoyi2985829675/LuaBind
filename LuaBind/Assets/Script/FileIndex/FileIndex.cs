using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using System.Text;

public class FileIndex
{
    private static Dictionary<string, string> luaIndex = new Dictionary<string, string>();
    static string path = "LuaScript/Lua/Module";
    static string luaIndexPath = Application.dataPath + "/LuaScript/Lua/CommentHelp/LuaIndex.lua";
    public static void ResetLuaIndex()
    {
        luaIndex.Clear();
        string[] files = Directory.GetFiles(Application.dataPath + "/" + path, "*.lua", SearchOption.AllDirectories);
        StringBuilder sb = new StringBuilder();
        foreach(string file in files)
        {
            string name = Path.GetFileNameWithoutExtension(file);
            string path = Path.ChangeExtension(file.Replace("\\", "/"), null);
            luaIndex.Add(name, path);
            sb.AppendFormat("\t{0} = \"{1}\",\n", name, path);
        }
        CreateLuaIndex(sb);
        Debug.Log("重置lua索引成功");
    }   

    private static void CreateLuaIndex(StringBuilder sb)
    {
        StringBuilder str = new StringBuilder();
        str.Append("LuaIndex = {\n");
        str.Append(sb.ToString());
        str.Append("}\n");
        File.WriteAllText(luaIndexPath, str.ToString());
#if UNITY_EDITOR
        UnityEditor.AssetDatabase.Refresh();
#endif
    }

    public static string GetLuaPath(string name)
    {
        string[] files = Directory.GetFiles(Application.dataPath + "/" + path, "*.lua", SearchOption.AllDirectories);
        foreach(string file in files)
        {
            string s = Path.GetFileNameWithoutExtension(file);
            if(s.Equals(name))
            {
                return file.Replace("\\", "/");
            }
        }
        return null;
    }
}