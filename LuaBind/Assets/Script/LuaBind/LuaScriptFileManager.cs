using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;

/// <summary>
/// 专门管理对 Lua 脚本文件的读写，以及「自动绑定」区间的替换与清空。
/// </summary>
public class LuaScriptFileManager
{
    /// <summary>Lua 脚本根目录（相对项目，用于读写 .lua 文件）</summary>
    public const string LuaScriptRoot = "Assets/LuaScript/Lua/";

    private static readonly Regex RegexStart = new Regex("自动绑定");
    private static readonly Regex RegexEnd = new Regex("结束绑定");
    
    private const string AutoFieldFormat = "{0} = nil";
    private const string AutoNoteFormat = "---@type {0} {1}";


    /// <summary>
    /// 获取脚本的完整路径（相对项目，如 Assets/LuaScript/Lua/xxx.lua）。用于显示或配置。
    /// </summary>
    public static string GetScriptFilePath(string scriptName)
    {
        if (string.IsNullOrWhiteSpace(scriptName)) return null;
        string name = scriptName.Trim();
        if (!name.EndsWith(".lua", StringComparison.OrdinalIgnoreCase))
            name += ".lua";
        return Path.Combine(LuaScriptRoot, name).Replace('\\', '/');
    }

    /// <summary>
    /// 获取脚本在磁盘上的绝对路径，用于 File 读写。
    /// </summary>
    private static string GetScriptAbsolutePath(string scriptName)
    {
        if (string.IsNullOrWhiteSpace(scriptName)) return null;

        return FileIndex.GetLuaPath(scriptName);
    }

    /// <summary>
    /// 读取脚本所有行。
    /// </summary>
    public static string[] ReadAllLines(string scriptName)
    {
        string path = GetScriptAbsolutePath(scriptName);
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
        {
            Debug.LogWarning("[LuaScriptFileManager] 文件不存在: " + (path ?? scriptName));
            return Array.Empty<string>();
        }
        return File.ReadAllLines(path);
    }

    /// <summary>
    /// 在已有行数组中查找「自动绑定」「结束绑定」的行索引。
    /// </summary>
    /// <param name="lines">脚本所有行</param>
    /// <param name="startIndex">自动绑定所在行索引，未找到为 -1</param>
    /// <param name="endIndex">结束绑定所在行索引，未找到为 -1</param>
    public static void GetMarkerIndices(string[] lines, out int startIndex, out int endIndex)
    {
        startIndex = -1;
        endIndex = -1;
        if (lines == null) return;
        for (int i = 0; i < lines.Length; i++)
        {
            if (RegexStart.IsMatch(lines[i]))
            {
                startIndex = i;
                continue;
            }
            if (RegexEnd.IsMatch(lines[i]))
            {
                endIndex = i;
                break;
            }
        }
    }

    /// <summary>
    /// 将「自动绑定」与「结束绑定」之间的内容替换为 middleLines，写回文件并刷新资源。
    /// </summary>
    /// <param name="scriptName">脚本名（不含 .lua）</param>
    /// <param name="middleLines">中间段要写入的行（不含首尾标记行）</param>
    /// <returns>是否成功（未找到标记或文件不存在时返回 false）</returns>
    public static bool ReplaceBetweenMarkers(string scriptName, IEnumerable<string> middleLines)
    {
        if (string.IsNullOrWhiteSpace(scriptName))
        {
            Debug.LogWarning("[LuaScriptFileManager] 脚本名为空");
            return false;
        }
        string path = GetScriptAbsolutePath(scriptName);
        if (!File.Exists(path))
        {
            Debug.LogWarning("[LuaScriptFileManager] 文件不存在: " + scriptName);
            return false;
        }
        string[] lines = File.ReadAllLines(path);
        GetMarkerIndices(lines, out int startIndex, out int endIndex);
        if (startIndex < 0 || endIndex < 0)
        {
            Debug.LogWarning("[LuaScriptFileManager] 未找到「自动绑定」或「结束绑定」标记: " + scriptName);
            return false;
        }
        var middle = middleLines?.ToList() ?? new List<string>();
        var newLines = lines.Take(startIndex + 1).Concat(middle).Concat(lines.Skip(endIndex)).ToArray();
        File.WriteAllLines(path, newLines);
#if UNITY_EDITOR
        AssetDatabase.Refresh();
#endif
        return true;
    }

    /// <summary>
    /// 清空「自动绑定」与「结束绑定」之间的内容（保留首尾标记行），写回并刷新。
    /// </summary>
    public static bool ClearBetweenMarkers(string scriptName)
    {
        return ReplaceBetweenMarkers(scriptName, Array.Empty<string>());
    }

    /// <summary>
    /// 根据绑定数据生成「自动绑定」区内容并写入文件。由管理器负责格式与文件写入，LuaBehaviour 只传数据。
    /// </summary>
    /// <param name="scriptName">脚本名（不含 .lua）</param>
    /// <param name="moduleName">模块名前缀，用于生成字段名，如 MyTest</param>
    /// <param name="bindings">每个绑定项：(componentName, fieldName, isArray)</param>
    public static bool WriteAutoBindSection(string scriptName, IEnumerable<(string componentName, string fieldName, bool isArray)> bindings)
    {
        if (string.IsNullOrEmpty(scriptName)) return false;
        var lines = new List<string>();
        foreach (var (componentName, fieldName, isArray) in bindings ?? Array.Empty<(string, string, bool)>())
        {
            string typeStr = isArray ? componentName + "[]" : componentName;
            lines.Add(string.Format(AutoNoteFormat, typeStr, "@" + fieldName));
            lines.Add(string.Format(AutoFieldFormat, scriptName + "." + fieldName));
        }
        return ReplaceBetweenMarkers(scriptName, lines);
    }
}
