---将 C# 的 List（或支持 .Count 与 [i] 的集合）转成 Lua table，便于用 #、pairs、[1] 等
---@param csList userdata|nil C# List<object> 或 nil
---@return table 1-based 的 Lua 数组，空表表示 nil 或空列表
function csListToLuaTable(csList)
    local t = {}
    if not csList or not csList.Count then return t end
    for i = 0, csList.Count - 1 do
        t[i + 1] = csList[i]
    end
    return t
end

Class = function (className,super)
    local cls = {}
    setmetatable(cls,super);
    local bindTable = nil;
    cls.New = function (self,luaNodeBase,gameObject,transform,bindList) 
        self.luaNodeBase = luaNodeBase;
        self.gameObject = gameObject;
        self.transform = transform;
        bindTable  = bindList;
    end
    super.__index = function (t,k)
        if bindTable[k] then
            return bindTable[k]
        else
            return super[k]
        end
    end;
    return cls;
end

---打印简单表数据
---@param tb table @需要打印的表
---@param hintStr string @打印时的起始名，为了制裁打印日志信息太随意，hintStr 信息不能太短
function printTable(tb, hintStr)

    if type(hintStr) ~= "string" then
        hintStr = tostring(hintStr)
    end

    if tb == nil then
        print("<color=#FFAE1FFF>", hintStr, " = nil</color>")
        return
    elseif type(tb) ~= "table" then
        print("<color=#FFAE1FFF>", hintStr, " = " .. tostring(tb) .. "</color>")
        return
    end
    local strPrint = {}
    table.insert(strPrint, string.format("<color=#33F1ABFF>%s = {</color>", hintStr))
    getPrint(strPrint, tb, 1)
    table.insert(strPrint, "\n}")
    print(table.concat(strPrint, ""))
end

-- 把table表转化为字符串，out 为输出用的表
function getPrint(out, tb, step)
    local stepSS = ""
    for j = 1, step do
        stepSS = stepSS .. "   "
    end
    for i, v in pairs(tb) do
        --table.insert(strPrint, "\n" .. stepSS .. (type(i) == "number" and ("[" .. i .. "]") or i) .. " = ")
        -- FIXED: 对于某些map表，key值类型为table时，会导致凭借报错
        local typeiName = type(i)
        if typeiName == "number" then
            table.insert(out, "\n" .. stepSS .. "[" .. i .. "]" .. " = ")
        elseif typeiName == "string" then
            table.insert(out, "\n" .. stepSS .. i .. " = ")
        else
            table.insert(out, "\n" .. stepSS .. "[*]" .. " = ")
        end
        if type(v) == "table" then
            table.insert(out, "{")
            if step > 10 then
                table.insert(out, "\"ingone table:" .. tostring(v) .. "\"")
            else
                getPrint(out, v, step + 1)
            end
            table.insert(out, "\n" .. stepSS .. "},")
        elseif type(v) == "string" then
            table.insert(out, "\"" .. v .. "\",")
        else
            table.insert(out, tostring(v) .. ",")
        end
    end
end