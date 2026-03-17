Class = function (cls,super)
    setmetatable(cls,super);
    cls.New = function (this,luaNodeBase,gameObject,transform) 
        local instance = {};
        instance.luaNodeBase = luaNodeBase;
        instance.gameObject = gameObject;
        instance.transform = transform;
        setmetatable(instance,cls)
        return instance;
    end
end