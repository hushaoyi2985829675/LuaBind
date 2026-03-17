using System.Collections;
using System;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using System.Text.RegularExpressions;
using System.Linq;

[System.Serializable]
public class DataInfo<T>
{
    public string name;
    public List<T> valueList;
}


[System.Serializable]
public class LuaBehaviour
{
    private string path = "Assets/LuaScript/Lua/";
    private Regex regexStart = new Regex("自动绑定");
    private Regex regexEnd = new Regex("结束绑定");
    private string autoField = "{0} = nil";
    public string luaScript;

    public List<DataInfo<GameObject>> luaBindValueListGameObject = new List<DataInfo<GameObject>>();
    public List<DataInfo<Component>> luaBindValueListComponent = new List<DataInfo<Component>>();
    public void ClearBind()
    {
        luaBindValueListGameObject?.Clear();
        luaBindValueListComponent?.Clear();
        //清除文件
        Debug.Log("清除Lua变量成功");
    }
    LuaBindType luaBindType;

    public void AddData(string name, GameObject gameObject)
    {
        AddDataImpl(luaBindValueListGameObject, name, gameObject);
    }

    public void AddData(string name, Component component)
    {
        AddDataImpl(luaBindValueListComponent, name, component);
    }

    private static void AddDataImpl<T>(List<DataInfo<T>> list, string name, T value) where T : UnityEngine.Object
    {
        DataInfo<T> dataInfo = list.Find(x => x.name == name);
        if (dataInfo == null)
        {
            dataInfo = new DataInfo<T>();
            dataInfo.name = name;
            dataInfo.valueList = new List<T>();
            dataInfo.valueList.Add(value);
            list.Add(dataInfo);
        }
        else
        {
            dataInfo.valueList.Add(value);
        }
    }

    public void WriteLua()
    {
        int startIndex = 0;
        int endIndex = 0;
        string[] luaScriptLines = File.ReadAllLines(path + this.luaScript + ".lua");
        foreach(string line in luaScriptLines)
        {
            if(regexStart.IsMatch(line))
            {
                startIndex = Array.IndexOf(luaScriptLines, line);
                continue;
            }
            if(regexEnd.IsMatch(line))
            {
                endIndex = Array.IndexOf(luaScriptLines, line);
                break;
            }
        }
        var autoStrs = new List<string>();
        writeAutoField(luaBindValueListGameObject, autoStrs);
        writeAutoField(luaBindValueListComponent, autoStrs);
        // 保留 startIndex 之前（含标记行）、endIndex 及之后（含结束标记），中间整段替换为 autoStrs，多余行被删掉
        var newLines = luaScriptLines.Take(startIndex + 1).Concat(autoStrs).Concat(luaScriptLines.Skip(endIndex)).ToArray();
        File.WriteAllLines(path + this.luaScript + ".lua", newLines);
    }

    private void writeAutoField<T>(List<DataInfo<T>> gameObjectList, List<string> autoStrs) where T : UnityEngine.Object
    {
        for (int i = 0; i < gameObjectList.Count; i++)
        {
            if(gameObjectList[i].valueList.Count > 1 )
            {
                for (int j = 0; j < gameObjectList[i].valueList.Count; j++)
                {
                    autoStrs.Add(string.Format(autoField, luaScript + "." + gameObjectList[i].name + "[" + j + "]"));
                }
            }
            else
            {
                autoStrs.Add(string.Format(autoField, luaScript + "." + gameObjectList[i].name));
            }
        }
    }
}