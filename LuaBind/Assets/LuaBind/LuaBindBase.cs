using System;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.UI;


public enum LuaBindType
{
    GameObject,
    Component,
}
public class LuaBindRule
{
    public string name;
    public string componentName;
    public LuaBindType bindype;
}

public class LuaBindBase : MonoBehaviour
{
    public LuaBehaviour luaBehaviour;
    private string rulePath = "Assets/LuaBind/LuaBindRule.txt";
    private void Awake() 
    {
            
    }

    public void AutoBind()
    {
        ClearBind();
        bind(transform);
        //写入lua
        luaBehaviour.WriteLua();
        Debug.Log("自动绑定成功");
    }

    public void ClearBind()
    {
        luaBehaviour?.ClearBind();
    }

    private void bind(Transform parent)
    {
        LuaBindRule rule = GetLuaBindRule(parent.name);
        if (rule != null)
        {
            switch (rule.bindype)
            {
                case LuaBindType.GameObject:
                    luaBehaviour.AddData(rule.name, parent.gameObject);
                    break;
                case LuaBindType.Component:      
                    Component component = parent.GetComponent(rule.componentName);
                    if (component != null)
                        luaBehaviour.AddData(rule.name, component);
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
}