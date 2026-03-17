using System.Collections;
using System;
using System.Collections.Generic;
using UnityEngine;

public class DataInfo
{
    public string name;
    public List<Type> valueList;
    public string path;
}

public class LuaBindData
{
   public List<DataInfo> luaBindValueList = new List<DataInfo>();

   public void AddData(string name,Type type)
   {
        DataInfo dataInfo = luaBindValueList.Find(x => x.name == name);
        if(dataInfo == null)
        {
            dataInfo = new DataInfo();
            dataInfo.name = name;
            dataInfo.valueList = new List<Type>();
            dataInfo.valueList.Add(type);
            luaBindValueList.Add(dataInfo);
        }
        else
        {
            dataInfo.valueList.Add(type);
        }
   }
}
