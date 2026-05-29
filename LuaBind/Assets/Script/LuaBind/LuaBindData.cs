using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEngine;
using XLua;

[System.Serializable]
public class DataInfo<T>
{
    public string name;
    public string componentName;
    public List<T> valueList;
}
[Serializable]
public class LuaBindData 
{
    // Start is called before the first frame update
    public List<DataInfo<GameObject>> luaBindValueListGameObject = new List<DataInfo<GameObject>>();
    public List<DataInfo<Component>> luaBindValueListComponent = new List<DataInfo<Component>>();
    public string luaScript;
    private StringBuilder luaBindDataStr;
    public List<object> dataList = new List<object>();
    public LuaTable luaTable = null;
    public void AutoBind()
    {
        
    }

    public void ClearBind()
    {
        luaBindValueListGameObject?.Clear();
        luaBindValueListComponent?.Clear();
        //清除文件
        ClearLuaAutoBind();
    }

    public void AddData(string name, string componentName, GameObject gameObject)
    {
        AddDataImpl(luaBindValueListGameObject, name, componentName, gameObject);
    }

    public void AddData(string name, string componentName, Component component)
    {
        AddDataImpl(luaBindValueListComponent, name, componentName, component);
    }

    private static void AddDataImpl<T>(List<DataInfo<T>> list, string name, string componentName, T value) where T : UnityEngine.Object
    {
        DataInfo<T> dataInfo = list.Find(x => x.name == name);
        if (dataInfo == null)
        {
            dataInfo = new DataInfo<T>();
            dataInfo.name = name;
            dataInfo.componentName = componentName;
            dataInfo.valueList = new List<T>();
            list.Add(dataInfo);
        }
         dataInfo.valueList.Add(value);
    }

    public void WriteLua()
    {
        if (string.IsNullOrEmpty(luaScript)) return;
        var bindings = new List<(string componentName, string fieldName, bool isArray)>();
        CollectBindings(luaBindValueListGameObject, bindings);
        CollectBindings(luaBindValueListComponent, bindings);
        LuaScriptFileManager.WriteAutoBindSection(luaScript, bindings);
    }

    private static void CollectBindings<T>(List<DataInfo<T>> list, List<(string, string, bool)> bindings) where T : UnityEngine.Object
    {
        if (list == null) return;
        for (int i = 0; i < list.Count; i++)
        {
            bool isArray = list[i].valueList != null && list[i].valueList.Count > 1;
            bindings.Add((list[i].componentName, list[i].name, isArray));
        }
    }

    private void ClearLuaAutoBind()
    {
        if (string.IsNullOrEmpty(luaScript)) return;
        LuaScriptFileManager.ClearBetweenMarkers(luaScript);
    }

    /// <summary>
    /// 将两个绑定列表填入 dataList，供传给 Lua 前调用；避免 dataList 为空导致 Lua 索引抛异常。
    /// </summary>
    public void InitLuaTable()
    {
        luaTable = LuaMrg.Instance.luaEnv.NewTable();
        AddLuaTable(luaBindValueListGameObject);
        AddLuaTable(luaBindValueListComponent);
    }

    private void AddLuaTable<T>(List<DataInfo<T>> list)
    {
        // LuaTable luaTable = LuaMrg.Instance.luaEnv.NewTable();
        for(int i = 0; i < list.Count; i++)
        {
            DataInfo<T> dataInfo = list[i];
            if(dataInfo.valueList.Count > 1)
            {
                LuaTable luaTableList = LuaMrg.Instance.luaEnv.NewTable();
                for(int j = 0; j < dataInfo.valueList.Count; j++)
                {
                    luaTableList.Set(j + 1, dataInfo.valueList[j]);
                }
                luaTable.Set(dataInfo.name, luaTableList);
            }
            else
            {
                luaTable.Set(dataInfo.name, dataInfo.valueList[0]);
            }
        }
    }
}
