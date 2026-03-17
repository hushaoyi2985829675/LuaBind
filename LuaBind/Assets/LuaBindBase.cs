using System;
using UnityEngine;
using UnityEngine.UI;

[Serializable]
public class LuaBindBaseData
{
    public string LuaScriptName;
}

public class LuaBindBase : MonoBehaviour
{
    public LuaBindBaseData bindData = new LuaBindBaseData();


    private LuaBindData luaBindData;

    private void Awake() 
    {
            
    }

    public void AutoBind()
    {
        Debug.Log("开始自动绑定");
        foreach(Transform child in transform)
        {
            bind(child);
        }
    }

    private void bind(Transform parent)
    {
        foreach(Transform child in parent)
        {
            if(child.name.EndsWith("Img") || child.name.EndsWith("Btn"))
            {
                // var cmp = child.gameObject.GetComponent<LoopScroll>();
                // if(cmp == null)
                // {
                //     continue;
                // }
                // Type type = cmp.GetType();
                // luaBindData.AddData(child.name,type);
            }
        }
    }
}