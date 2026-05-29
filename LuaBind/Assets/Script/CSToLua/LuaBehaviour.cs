using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using XLua;

public enum LuaBindType
{
    GameObject,
    Component,
}
public class LuaBehaviour : MonoBehaviour
{
    public LuaBindData luaBindData = new LuaBindData();
    private bool isLoadLua = false;
    private string rulePath = "Assets/Script/LuaBind/LuaBindRule.txt";
    public LuaTable luaTable;
    private Action<LuaTable> onLuaAwake;
    private Action<LuaTable> onLuaStart;
    private Action<LuaTable> onLuaUpdate;

    /// <summary>
    /// 供 XLua 代码生成扫描：CSharpCallLua 需要 List&lt;Type&gt; 或 Type，不能是委托实例。生成后需执行菜单 XLua → Generate Code。
    /// </summary>
   

    void Awake()
    {
        if (isLoadLua) return;
        if (string.IsNullOrEmpty(luaBindData.luaScript))
        {
            return;
        } 
        luaTable = LuaMrg.Instance.DoString(luaBindData.luaScript);
        if (luaTable == null) 
        {
            return;
        }
        //初始化LuaTable
        luaBindData.InitLuaTable();
        var newFunc = luaTable.Get<Action<LuaTable, LuaBehaviour, GameObject, Transform, LuaTable>>("New");
        if (newFunc != null)
        {
            newFunc.Invoke(luaTable, this, gameObject, transform, luaBindData.luaTable);
        }
        isLoadLua = true;
        onLuaAwake = luaTable.Get<Action<LuaTable>>("Awake");
        onLuaStart = luaTable.Get<Action<LuaTable>>("Start");
        onLuaUpdate = luaTable.Get<Action<LuaTable>>("Update");
        onLuaAwake?.Invoke(luaTable);
    }
    void Start()
    {
        onLuaStart?.Invoke(luaTable);
    }
    void Update()
    {
        onLuaUpdate?.Invoke(luaTable);
    }
    
    private void bind(Transform parent)
    {
        LuaBindRule rule = GetLuaBindRule(parent.name);
        if (rule != null)
        {
            switch (rule.bindype)
            {
                case LuaBindType.GameObject:
                    luaBindData.AddData(rule.name, rule.componentName, parent.gameObject);
                    break;
                case LuaBindType.Component:      
                    Component component = parent.GetComponent(rule.componentName);
                    if (component != null)
                        luaBindData.AddData(rule.name, rule.componentName, component);
                    break;
            }
        }
        foreach (Transform child in parent)
        {
            bind(child);
        }
    }

    private LuaBindRule GetLuaBindRule(string name)
    {
        LuaBindRule luaBindRule = new LuaBindRule();
        string[] lins = File.ReadAllLines(rulePath);
        foreach(string line in lins)
        {
            string[] parts = line.Split(' ');
            if(name.EndsWith(parts[0]))
            {
                luaBindRule.name = name;
                luaBindRule.componentName = parts[1];
                LuaBindType bindype = LuaBindType.GameObject;
                switch(parts[2])
                {
                    case "GameObject":
                        bindype = LuaBindType.GameObject;
                    break;
                    case "Component":
                        bindype = LuaBindType.Component;
                    break;
                }
                luaBindRule.bindype = bindype;
                return luaBindRule;
            }
        }
        return null;
    }
    public void AutoBind()
    {
        ClearBind();
        bind(transform);
        luaBindData.WriteLua();
    }
    public void ClearBind()
    {
        luaBindData.ClearBind();
    }
}