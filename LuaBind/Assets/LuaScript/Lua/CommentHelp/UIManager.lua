--[[
    Lua 版 UIManager，逻辑对齐 Assets/Script/UIManager.cs（#if false 内原实现）。
    依赖 XLua：需能访问 CS.UnityEngine.*、CS.DG.Tweening、以及项目内的 PanelBase、MaskLayer、Ui 等类型。
    使用：require 后调用 UIManager.Init()（对应 Awake），再 UIManager.AddLayer(...) 等。
]]

UIManager = {}

local GameObject = CS.UnityEngine.GameObject
local Object = CS.UnityEngine.Object
local Vector3 = CS.UnityEngine.Vector3
local Debug = CS.UnityEngine.Debug
local DOTween = CS.DG.Tweening.DOTween
local Ease = CS.DG.Tweening.Ease

---@class LayerInfoLua
---@field name string
---@field Obj CS.UnityEngine.GameObject
---@field maskLayer CS.UnityEngine.GameObject|nil

local layerList ---@type LayerInfoLua[]
local layerStack ---@type LayerInfoLua[]
local maskLayerRef ---@type CS.UnityEngine.GameObject
local layerCanvas ---@type CS.UnityEngine.Transform
local popLayerCanvas ---@type CS.UnityEngine.Transform

local function new_layer_info(name, layer, maskLayer)
    return { name = name, layer = layer, maskLayer = maskLayer }
end

local function stack_push(t, v)
    t[#t + 1] = v
end

local function stack_pop(t)
    local n = #t
    if n < 1 then return nil end
    local v = t[n]
    t[n] = nil
    return v
end

local function stack_peek(t)
    local n = #t
    if n < 1 then return nil end
    return t[n]
end

local function getlayer(layerName)
    for _, layerInfo in ipairs(layerList) do
        if layerInfo.name == layerName then
            return layerInfo
        end
    end
    return nil
end

--- 对应 UIManager.Awake；需在首场景加载 UI 根节点后调用一次。
function UIManager.Init()
    layerList = {}
    layerStack = {}
    layerCanvas = GameObject.FindWithTag("LayerCanvas").transform
    popLayerCanvas = GameObject.FindWithTag("PopLayerCanvas").transform
    maskLayerRef = CS.Ui.Instance:GetLayerPrefab("Common/MaskLayer")

    -- local mainGo = GameObject.Find("MainLayer")
    -- if mainGo ~= nil then
    --     local panel = mainGo:GetComponent(typeof(CS.PanelBase))
    --     local layerInfo = new_layer_info("MainLayer", panel, nil)
    --     table.insert(layerList, layerInfo)
    --     stack_push(layerStack, layerInfo)
    -- end
end

---@param data table|nil 传给 onEnter/onShow 的参数表（XLua 会按绑定转为 C#）
local function pack_data(...)
    local n = select("#", ...)
    if n == 0 then return nil end
    if n == 1 then
        local a = select(1, ...)
        if type(a) == "table" then return a end
    end
    return { ... }
end

---@param layerRef CS.UnityEngine.GameObject
function UIManager.AddLayer(name, params)
    local newLayerInfo = getlayer(name)
    local layerScript = newLayerInfo and newLayerInfo.Obj.LuaTable or nil
    local maskLayer = newLayerInfo and newLayerInfo.maskLayer or nil

    if newLayerInfo == nil then
        --获取GameObject
        
        -- local layer = Object.Instantiate(layerRef, layerCanvas)
        -- layer:GetComponent(typeof(CS.UnityEngine.RectTransform)).anchoredPosition = Vector3.zero
        -- layer.name = layerRef.name
        -- layerScript = layer:GetComponent(typeof(CS.PanelBase))
        -- if layerScript == nil then
        --     Debug.Log("页面脚本没有继承PanelBase")
        --     return nil
        -- end
        -- layerScript:onEnter(data)
        -- if not layerScript.isFullScreen then
        --     maskLayer = Object.Instantiate(maskLayerRef, layerCanvas)
        --     maskLayer.name = layerRef.name .. "MaskLayer"
        --     maskLayer:GetComponent(typeof(CS.MaskLayer)):SetPanel(layerScript)
        -- end
        -- newLayerInfo = new_layer_info(layerRef.name, layerScript, maskLayer)
        -- table.insert(layerList, newLayerInfo)
    end

    -- if not layerScript.isFullScreen then
    --     maskLayer:SetActive(true)
    --     maskLayer.transform:SetSiblingIndex(layerCanvas.childCount)
    --     layerScript.transform.localScale = Vector3(0.01, 0.01, 1)
    --     layerScript.transform:DOScale(Vector3(1, 1, 1), 0.3):SetEase(Ease.OutCirc)
    --     layerScript.transform:SetSiblingIndex(layerCanvas.childCount)
    -- else
    --     stack_push(layerStack, newLayerInfo)
    -- end

    -- layerScript:SetActive(true)
    -- layerScript:onShow(data)

    -- if layerScript.isFullScreen then
    --     local layerIdxList = {}
    --     for index, layerInfo in ipairs(layerList) do
    --         local layer = layerInfo.layer
    --         if layerRef.name ~= layer.name then
    --             UIManager.CloseLayer(layer.name, false)
    --             if not layer.isFullScreen then
    --                 table.insert(layerIdxList, index)
    --             end
    --         end
    --     end
    --     for i = #layerIdxList, 1, -1 do
    --         local idx = layerIdxList[i]
    --         local layerInfo = layerList[idx]
    --         if not layerInfo.layer.isFullScreen then
    --             DOTween.Kill(layerInfo.layer.transform, false)
    --             Object.Destroy(layerInfo.maskLayer.gameObject)
    --         end
    --         Object.Destroy(layerInfo.layer.gameObject)
    --         table.remove(layerList, idx)
    --     end
    -- else
    --     layerScript.transform:DOScale(Vector3(1, 1, 1), 0.3):SetEase(Ease.OutCirc)
    -- end

    -- return newLayerInfo
end

local function enable_last_layer()
    stack_pop(layerStack)
    local lastLayerInfo = stack_peek(layerStack)
    if lastLayerInfo == nil then return end
    lastLayerInfo.layer:SetActive(true)
    lastLayerInfo.layer:onShow()
end

function UIManager.CloseLayer(layerName, isRestoreLastLayer)
    if isRestoreLastLayer == nil then isRestoreLastLayer = true end
    local layerInfo = getlayer(layerName)
    if layerInfo == nil or layerInfo.layer == nil then return end
    local layer = layerInfo.layer
    layer:Hide()
    if layer.isFullScreen then
        layer:onExit()
        layer:SetActive(false)
        if isRestoreLastLayer then
            enable_last_layer()
        end
    else
        local mask = layerInfo.maskLayer
        if mask ~= nil then
            mask:SetActive(false)
        end
        layer.transform:DOScale(Vector3(0.01, 0.01, 1), 0.15):SetEase(Ease.OutCirc):OnComplete(function()
            layer:SetActive(false)
            layer:onExit()
            layer.transform:DOKill()
            stack_pop(layerStack)
        end)
    end
end

function UIManager.AddUINode(layerRef, parent, ...)
    local data = pack_data(...)
    local layer = Object.Instantiate(layerRef, parent)
    layer:GetComponent(typeof(CS.UnityEngine.RectTransform)).anchoredPosition = Vector3.zero
    layer.name = layerRef.name
    local layerScript = layer:GetComponent(typeof(CS.PanelBase))
    layerScript:onEnter(data)
    layerScript.transform:SetSiblingIndex(parent.childCount)
    layerScript:onShow(data)
    return layerScript
end

function UIManager.ClosePopLayer(layer)
    UIManager.CloseUINode(layer)
end

function UIManager.CloseUINode(layer)
    if layer == nil then return end
    local panelBase = layer:GetComponent(typeof(CS.PanelBase))
    if panelBase == nil then return end
    panelBase.transform:SetSiblingIndex(1)
    panelBase.gameObject:SetActive(false)
    panelBase:onExit()
    panelBase:Hide()
end

function UIManager.GetCanvas()
    local go = GameObject.Find("InTurnCanvas")
    if go == nil then return nil end
    return go.transform
end

function UIManager.LoadMainScene()
end

return UIManager
