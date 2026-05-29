using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public class FileIndexEditor : AssetPostprocessor
{
    private static Dictionary<string, string> luaIndex = new Dictionary<string, string>();
    private static string luaPath = "Assets/LuaScript/Module";
   static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets,string[] movedAssets, string[] movedFromAssetPaths)
    {
        bool isReset = false;
        for(int i = 0; i < importedAssets.Length; i++)
        {
            string p = Path.GetDirectoryName(importedAssets[i].Replace(Application.dataPath, "")).Replace("\\", "/");
            if(importedAssets[i].EndsWith(".lua") && p == luaPath)
            {
                isReset = true;
                break;
            }
        }
        if(isReset)
        {
            FileIndex.ResetLuaIndex();
            return;
        }
        for(int i = 0; i < movedAssets.Length; i++)
        {
            if(movedAssets[i].EndsWith(".lua"))
            {
                string p = Path.GetDirectoryName(movedAssets[i].Replace(Application.dataPath, "")).Replace("\\", "/");
                string name = Path.GetFileNameWithoutExtension(movedAssets[i]);
                if(p == luaPath || FileIndex.GetLuaPath(name) != null)
                {
                    isReset = true;
                    break;
                }
            }
        }
        if(isReset)
        {
            FileIndex.ResetLuaIndex();
            return;
        }
        for(int i = 0; i < deletedAssets.Length; i++)
        {
            string p = Path.GetDirectoryName(deletedAssets[i].Replace(Application.dataPath, "")).Replace("\\", "/");
            if(deletedAssets[i].EndsWith(".lua") && p == luaPath)
            {
                isReset = true;
                break;
            }
        }
        if(isReset)
        {
            FileIndex.ResetLuaIndex();
            return;
        }
    }
    [MenuItem("XLua/ResetLuaIndex")]   
    public static void ResetLuaIndex()
    {
        FileIndex.ResetLuaIndex();
    }
}
