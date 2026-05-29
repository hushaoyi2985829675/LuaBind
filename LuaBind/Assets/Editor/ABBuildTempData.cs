using System.Collections.Generic;
using System.IO;

public class BundleInfo
{
    public string BundleName;
    public string AssetPath;
}

public static class ABBuildTempData
{
    // key: 资源文件名, value: 资源所属bundle信息
    public static readonly Dictionary<string, BundleInfo> ResourceBundleDict = new Dictionary<string, BundleInfo>();

    /// <summary>
    /// 相对上次打包时间有变更或新增的资源路径（由增量流程中 <c>CollectModifiedAssetPathsSinceBuild</c> 填充）。
    /// </summary>
    public static readonly List<string> ModifiedAssetPathsSinceBuild = new List<string>();

    /// <summary>
    /// 打包前初始化临时数据容器，清空资源与包映射。
    /// </summary>
    public static void InitForBuild()
    {
        ResourceBundleDict.Clear();
        ModifiedAssetPathsSinceBuild.Clear();
    }

    /// <summary>
    /// 递归扫描指定UI目录，重建“资源文件名 -> 包信息”字典。
    /// </summary>
    /// <param name="uiRootPath">Unity工程内的UI根目录，例如Assets/UI。</param>
    public static void ResetResourceBundleDictFromUI(string uiRootPath)
    {
        ResourceBundleDict.Clear();

        string uiAbsolutePath = Path.GetFullPath(uiRootPath);
        string[] allDirs = Directory.GetDirectories(uiAbsolutePath, "*", SearchOption.AllDirectories);

        for (int i = 0; i < allDirs.Length; i++)
        {
            string dir = allDirs[i];
            string folderName = Path.GetFileName(dir);
            if (string.IsNullOrEmpty(folderName))
            {
                continue;
            }

            string[] files = Directory.GetFiles(dir, "*", SearchOption.TopDirectoryOnly);
            string bundleName = folderName.ToLower() + ".ab";

            for (int j = 0; j < files.Length; j++)
            {
                string file = files[j];
                if (file.EndsWith(".meta"))
                {
                    continue;
                }

                string assetPath = file.Replace("\\", "/");
                int assetsIndex = assetPath.IndexOf("Assets/");
                if (assetsIndex < 0)
                {
                    continue;
                }

                assetPath = assetPath.Substring(assetsIndex);
                string assetName = Path.GetFileName(assetPath);
                ResourceBundleDict[assetName] = new BundleInfo
                {
                    BundleName = bundleName,
                    AssetPath = assetPath
                };
            }
        }
    }
}
