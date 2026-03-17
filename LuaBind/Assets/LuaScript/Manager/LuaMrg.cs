using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using XLua;
using XLua.LuaDLL;

public class LuaMrg : Singleton<LuaMrg>
{
    /// <summary>Lua 脚本所在目录（相对 Assets）</summary>
    private static readonly string LuaFolder = "LuaScript/Lua";
    LuaEnv luaEnv;
    void Start()
    {
        luaEnv = new LuaEnv();
        luaEnv.AddLoader(CustomLoader);
        luaEnv.DoString("require 'Main'");
    }

    // Update is called once per frame
    void Update()
    {
        luaEnv?.Tick();
    }

    public byte[] CustomLoader(ref string filepath)
    {
        // 将 require 的路径（如 Main 或 module.sub）转成 Lua 目录下的文件路径
        string relativePath = filepath.Replace(".", "/") + ".lua";
        string path = Path.Combine(Application.dataPath, LuaFolder, relativePath);
        if (File.Exists(path))
        {
            filepath = path;
            return File.ReadAllBytes(path);
        }
        return null;
    }
}
