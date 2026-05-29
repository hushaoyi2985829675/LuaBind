--[[
    Lua 版 Ui，逻辑对齐 Assets/LuaScript/Lua/CommentHelp/Ui.cs。
    必须先加载 LuaEnum.lua（本目录 LuaEnum.lua），再通过本模块的 Init() 对应 Awake。
]]

local GameObject = CS.GameObject
local Object = CS.Object
local Vector3 = CS.Vector3
local Debug = CS.Debug
local Resources = CS.Resources
local Sprite = CS.Sprite
local Color = CS.Color
local Mathf = CS.Mathf
local Random = CS.Random
local NavMesh = CS.NavMesh
local mathm = CS.mathm

Ui = {}

local layerPath = "Layer/"
local heroObjNode ---@type CS.Transform
local spriteDict ---@type table<string, CS.Sprite>

local function init_sprite()
    Resources.Load(typeof(Sprite), layerPath)
    local sprites = Resources.LoadAll("UI", typeof(Sprite))
    if sprites == nil then return end
    local n = sprites.Length
    for i = 0, n - 1 do
        local sp = sprites[i]
        if sp == nil then goto continue end
        local name = sp.name
        if spriteDict[name] ~= nil then
            break
        end
        spriteDict[name] = sp
        ::continue::
    end
end

--- 对应 Ui.Awake：创建 HeroObjNode、加载 UI 图集字典
function Ui.Init()
    spriteDict = {}
    local heroGo = GameObject("HeroObjNode")
    heroObjNode = heroGo.transform
    init_sprite()
end

function Ui.GetHeroNode()
    return heroObjNode
end

function Ui.GetLayerPrefab(layerName)
    local path = layerPath .. layerName
    local layerPrefab = Resources.Load(typeof(GameObject), path)
    if layerPrefab == nil then
        Debug.LogError("Layer预制体找不到: " .. path)
    end
    return layerPrefab
end

function Ui.GetChild(parent, name)
    if parent == nil then return nil end
    local childCount = parent.childCount
    for i = 0, childCount - 1 do
        local child = parent:GetChild(i)
        if child.name == name then
            return child.gameObject
        end
        local obj = Ui.GetChild(child, name)
        if obj ~= nil then
            return obj
        end
    end
    return nil
end

---@param componentType CS.System.Type MonoBehaviour 子类型，如 typeof(CS.SomePanel)
function Ui.GetComponentByChild(parent, componentType)
    if parent == nil then return nil end
    local childCount = parent.childCount
    for i = 0, childCount - 1 do
        local child = parent:GetChild(i)
        local comp = child:GetComponent(componentType)
        if comp ~= nil then
            return comp
        end
        local script = Ui.GetComponentByChild(child, componentType)
        if script ~= nil then
            return script
        end
    end
    return nil
end

---@param componentType CS.System.Type Component 子类型
function Ui.GetComponentByParent(parent, componentType)
    if parent == nil then return nil end
    local comp = parent:GetComponent(componentType)
    if comp ~= nil then
        return comp
    end
    local grandParent = parent.parent
    if grandParent ~= nil then
        local gc = grandParent.childCount
        for i = 0, gc - 1 do
            local sibling = grandParent:GetChild(i)
            if sibling == parent then
                goto continue_sib
            end
            local scomp = sibling:GetComponent(componentType)
            if scomp ~= nil then
                return scomp
            end
            ::continue_sib::
        end
    end
    return Ui.GetComponentByParent(parent.parent, componentType)
end

function Ui.RemoveAllChildren(parent)
    if parent == nil then return end
    for i = parent.childCount - 1, 0, -1 do
        local child = parent:GetChild(i)
        Object.DestroyImmediate(child.gameObject)
    end
end

function Ui.GetSprite(spriteName)
    local sp = spriteDict[spriteName]
    if sp == nil then
        Debug.LogError("找不到图片: " .. tostring(spriteName))
        return nil
    end
    return sp
end

function Ui.GetQualityColor(quality, isBack)
    if isBack == nil then isBack = true end
    local normalize = 1 / 255
    if isBack then
        if quality == 1 then
            return Color(235 * normalize, 235 * normalize, 235 * normalize)
        elseif quality == 2 then
            return Color(41 * normalize, 182 * normalize, 246 * normalize)
        elseif quality == 3 then
            return Color(211 * normalize, 47 * normalize, 47 * normalize)
        end
        return Color.white
    else
        if quality == 1 then
            return Color(85 * normalize, 85 * normalize, 85 * normalize)
        elseif quality == 2 then
            return Color(13 * normalize, 71 * normalize, 161 * normalize)
        elseif quality == 3 then
            return Color(149 * normalize, 17 * normalize, 17 * normalize)
        end
        return Color.white
    end
end

function Ui.GetRandomPointInCircle(center, radius)
    for _ = 1, 30 do
        local randomAngle = Random.Range(0, Mathf.PI * 2)
        local x = radius * mathm.cos(randomAngle)
        local z = radius * mathm.sin(randomAngle)
        local pos = Vector3(x, center.y, z)
        local ok, hit = NavMesh.SamplePosition(pos, 1.0, NavMesh.AllAreas)
        if ok then
            return hit.position
        end
    end
    Debug.Log("随机移动坐标超出30次")
    return Vector3.zero
end

return Ui
