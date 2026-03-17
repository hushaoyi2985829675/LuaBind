using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using UnityEngine;




public class LuaBindMrg : Singleton<LuaBindMrg>
{
    public Dictionary<string,LuaBindRule> luaBindRuleDict = new Dictionary<string,LuaBindRule>();
    private string rulePath = "Assets/LuaBind/LuaBindRule.txt";
    // Start is called before the first frame update
    void Awake()
    {
        string[] lins = File.ReadAllLines(rulePath);
        foreach(string line in lins)
        {
            string[] parts = line.Split(' ');
            LuaBindRule luaBindRule = new LuaBindRule();
            luaBindRule.name = parts[1];
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
            luaBindRuleDict.Add(parts[0],luaBindRule);
        }
    }

    public LuaBindRule GetLuaBindRule(string name)
    {
        if(luaBindRuleDict.ContainsKey(name))
        {
            return luaBindRuleDict[name];
        }
        else
        {
            return null;
        }
    }
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
