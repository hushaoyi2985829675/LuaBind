using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AssetsBundleMrg : Singleton<AssetsBundleMrg>
{
    public static T LoadAssets<T>(string path, string assetName) where T : Object
    {
        AssetBundle ab = AssetBundle.LoadFromFile(path);
        if (ab == null)
        {
            Debug.LogError("加载AssetBundle失败: " + path);
            return default;
        }

        T asset = ab.LoadAsset<T>(assetName);
        ab.Unload(false);

        if (asset == null)
            Debug.LogError("资源未找到: " + assetName);

        return asset;
    }
}
