--[[
    C# / XLua 常用类型短引用。请在其他 Lua 脚本之前加载本文件。
    表名 CS 会覆盖 XLua 默认全局 CS，仅保留此处列出的键；需要未列出的类型时请在此补充，或使用 xlua 原始全局（若加载顺序允许）。
]]

CS = {
    GameObject = CS.UnityEngine.GameObject,
    Object = CS.UnityEngine.Object,
    Vector3 = CS.UnityEngine.Vector3,
    Debug = CS.UnityEngine.Debug,
    DOTween = CS.DG.Tweening.DOTween,
    Ease = CS.DG.Tweening.Ease,
    -- Ui.cs 等 UI / 资源
    Transform = CS.UnityEngine.Transform,
    Resources = CS.UnityEngine.Resources,
    Sprite = CS.UnityEngine.Sprite,
    MonoBehaviour = CS.UnityEngine.MonoBehaviour,
    Component = CS.UnityEngine.Component,
    Color = CS.UnityEngine.Color,
    Mathf = CS.UnityEngine.Mathf,
    Random = CS.UnityEngine.Random,
    RectTransform = CS.UnityEngine.RectTransform,
    -- NavMesh（GetRandomPointInCircle）
    NavMesh = CS.UnityEngine.AI.NavMesh,
    NavMeshHit = CS.UnityEngine.AI.NavMeshHit,
    -- Unity.Mathematics（与 Ui.cs 中 math.cos / math.sin 一致）
    mathm = CS.Unity.Mathematics.math,
    -- 项目内单例（按需）
    Ui = CS.Ui,
}
