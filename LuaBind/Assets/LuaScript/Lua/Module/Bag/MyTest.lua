---@class MyTest
local MyTest = Class("MyTest",{})


------------自动绑定-------------
---@type GameObject[] @GameObj
MyTest.GameObj = nil
---@type GameObject @MyObj
MyTest.MyObj = nil
---@type Button[] @mBtn
MyTest.mBtn = nil
---@type Image @MyImg
MyTest.MyImg = nil
------------结束绑定-------------

function MyTest:Init(gameObject)
    self.gameObject = gameObject;
end

function MyTest:Awake()
    self.MyImg.gameObject.name = "Mg";
end

function MyTest:Start()
    print("MyTest:Start")
end

return MyTest






