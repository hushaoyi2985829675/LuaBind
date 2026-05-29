using System.Collections;
using System.Collections.Generic;
using System.IO;
using Unity.VisualScripting;
using UnityEngine;
using XLua;
using XLua.LuaDLL;

public class LuaMrg : Singleton<LuaMrg>
{
    /// <summary>Lua 脚本所在目录（相对 Assets）</summary>
    private static readonly string LuaFolder = "LuaScript/Lua";
    public LuaEnv luaEnv;
    void Start()
    {
      
    }
    protected override void Init()
    {
        luaEnv = new LuaEnv();
        luaEnv.AddLoader(CustomLoader);
        string s = @"
                    require ""Assets/LuaScript/Lua/CommentHelp/LuaIndex.lua"";
                    local lua_require = require;
                    require = function (name)
                        local path = LuaIndex[name]
                        if path == nil then
                            name = string.gsub(name, ""%."", ""/"")   
                            path = CS.UnityEngine.Application.dataPath .. ""/LuaScript/"" .. name
                        end
                        return lua_require(path.."".lua"");
                    end;
         ";
        luaEnv.DoString(s);
        luaEnv.DoString("require 'Main'");
    }

    // Update is called once per frame
    void Update()
    {
        luaEnv?.Tick();
    }

    private byte[] CustomLoader(ref string filepath)
    {
        return File.ReadAllBytes(filepath);
    }

    public LuaTable DoString(string luaScript)
    {
        // 必须用 return require 'xxx'，否则 chunk 可能不把 require 的返回值留在栈上，DoString 会得到 null
        object[] result = luaEnv.DoString("return require '" + luaScript + "'");
        if (result == null || result.Length == 0)
        {
            Debug.LogError("Lua脚本没有返回值: " + luaScript);
            return null;
        }
        return result[0] as LuaTable;
    }
}
