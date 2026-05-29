using System.IO;
using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEditor;
using UnityEngine;

public class AeesetBundleTool : EditorWindow
{
    private const string UIRootPath = "Assets/UI";
    private const string OutputRootPath = "Assets/AB/ABFile/ABRes";
    private const string BuildTimeFilePath = "Assets/AB/ABFile/BuildTime.txt";
    private const string ResourceBundleMapFilePath = "Assets/AB/ABFile/ResourceBundleMap.txt";
    private const string ResourceBundleMapHeader = "# ResourceBundleMap v1";

    /// <summary>
    /// 打包入口：输出目录无产物时走全量，否则走增量（仅构建「改变资源」涉及的包）。
    /// </summary>
    [MenuItem("AssetsBundle/buildAB")]
    public static void BuildAB()
    {
        DeleteOrphanMetaFilesUnderDirectory(AssetPathToAbsoluteUnderProject(OutputRootPath));
        BuildTarget activeTarget = EditorUserBuildSettings.activeBuildTarget;
        string targetOutputPath = Path.Combine(OutputRootPath, activeTarget.ToString());
        bool isAll = !(Directory.Exists(targetOutputPath) && AbOutputDirectoryHasAnyContent(targetOutputPath));
        ABBuildTempData.InitForBuild();

        if (isAll)
        {
            BuildAllFromUI(targetOutputPath, activeTarget);
        }
        else
        {
            BuildIncrementalFromUI(targetOutputPath, activeTarget);
        }
    }

    /// <summary>
    /// 输出目录下是否存在任意文件或子目录（仅看一层，用于区分「有产物」与空目录）。
    /// </summary>
    private static bool AbOutputDirectoryHasAnyContent(string directoryPath)
    {
        foreach (string _ in Directory.EnumerateFileSystemEntries(directoryPath))
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// 增量流程：对比上次 Map / 打包时间与当前 UI 扫描结果，划分「改变资源的包」「不在当前构建计划的包」及二者并集（重打前删旧产物用）。
    /// </summary>
    private static void BuildIncrementalFromUI(string outputPath, BuildTarget target)
    {
        if (!AssetDatabase.IsValidFolder(UIRootPath))
        {
            Debug.LogError($"未找到目录: {UIRootPath}");
            return;
        }

        if (!Directory.Exists(outputPath))
        {
            Directory.CreateDirectory(outputPath);
        }

        if (!TryGetLastBuildTime(out DateTime lastBuildTime))
        {
            Debug.LogWarning("未读到有效打包时间（BuildTime.txt 为空或不存在），请先执行一次全量打包。");
            return;
        }

        ABBuildTempData.ResetResourceBundleDictFromUI(UIRootPath);

        if (ABBuildTempData.ResourceBundleDict.Count == 0)
        {
            Debug.LogWarning($"{UIRootPath} 下没有可打包资源");
            return;
        }
        //遍历修改数量
        CollectModifiedAssetPathsSinceBuild(lastBuildTime);
        int changedCount = ABBuildTempData.ModifiedAssetPathsSinceBuild.Count;

        TryReadResourceBundleMap(out Dictionary<string, string> lastMapSnapshot);
        HashSet<string> currentAssetPaths = CollectCurrentAssetPathsFromDict();
        List<string> deletedAssetPaths = new List<string>();
        foreach (KeyValuePair<string, string> kv in lastMapSnapshot)
        {
            if (!currentAssetPaths.Contains(kv.Key))
            {
                deletedAssetPaths.Add(kv.Key);
            }
        }

        // ---------- 增量分类（与「删旧 .ab/.manifest → 再打」顺序一致）----------
        // 1) 改变资源列表：mtime 变更、Map 中已删资源旧所属包、Map 中无记录的新增资源所在包 → 与「需重打的包」一致
        HashSet<string> bundlesAffectedByResourceChanges = CollectBundlesAffectedByResourceChanges(
            deletedAssetPaths,
            lastMapSnapshot,
            ABBuildTempData.ModifiedAssetPathsSinceBuild);

        // 2) 当前构建计划以外的包：lastMap/磁盘上存在、但本次 UI 扫描不再产出的包名（含输出目录孤儿 .ab）
        HashSet<string> bundlesNotInCurrentBuildPlan = CollectBundlesNotInCurrentBuildPlan(lastMapSnapshot, outputPath);

        // 3) 重打前要从磁盘清掉的旧产物 = (2) ∪ (1)；重打的包 ⊆ (1)
        HashSet<string> bundlesDeleteOldArtifactsBeforeRebuild = CollectBundlesRequiringOldArtifactDeleteBeforeRebuild(
            bundlesNotInCurrentBuildPlan,
            bundlesAffectedByResourceChanges);

        Debug.Log(
            $"[增量AB] 上次打包时间: {lastBuildTime:yyyy-MM-dd HH:mm:ss}，mtime 变更路径数: {changedCount}，" +
            $"删除的资源数量: {deletedAssetPaths.Count}，" +
            $"改变资源涉及包数(=待重打): {bundlesAffectedByResourceChanges.Count}，" +
            $"重打前需删旧产物包数(并集): {bundlesDeleteOldArtifactsBeforeRebuild.Count}，平台: {target}，输出: {outputPath}");

        DeleteBuiltBundleAbAndManifestFiles(outputPath, bundlesNotInCurrentBuildPlan);

        if (bundlesAffectedByResourceChanges.Count > 0)
        {
            if (!BuildFromResourceBundleDict(outputPath, target, bundlesAffectedByResourceChanges))
            {
                AssetDatabase.Refresh();
                return;
            }
        }
        else if (bundlesDeleteOldArtifactsBeforeRebuild.Count == 0)
        {
            Debug.Log("[增量AB] 无资源变更且无计划外包，跳过构建与删包。");
        }

        WriteResourceBundleMapFile();
        WriteBuildTimeFile();
        AssetDatabase.Refresh();
        Debug.Log($"[增量AB] 完成。本次构建增量包数: {bundlesAffectedByResourceChanges.Count}，平台: {target}，输出: {outputPath}");
    }

    /// <summary>
    /// 遍历 <see cref="ABBuildTempData.ResourceBundleDict"/>，将磁盘最后修改时间晚于上次打包时间的资源路径写入 <see cref="ABBuildTempData.ModifiedAssetPathsSinceBuild"/>。
    /// </summary>
    private static void CollectModifiedAssetPathsSinceBuild(DateTime lastBuildTime)
    {
        ABBuildTempData.ModifiedAssetPathsSinceBuild.Clear();
        string projectRoot = Path.GetDirectoryName(Application.dataPath);

        foreach (KeyValuePair<string, BundleInfo> pair in ABBuildTempData.ResourceBundleDict)
        {
            BundleInfo info = pair.Value;
            if (info == null || string.IsNullOrEmpty(info.AssetPath))
            {
                continue;
            }

            string fullPath = Path.GetFullPath(
                Path.Combine(projectRoot, info.AssetPath.Replace('/', Path.DirectorySeparatorChar)));
            if (!File.Exists(fullPath))
            {
                continue;
            }

            if (File.GetLastWriteTime(fullPath) > lastBuildTime)
            {
                ABBuildTempData.ModifiedAssetPathsSinceBuild.Add(info.AssetPath);
            }
        }
    }

    private static HashSet<string> CollectCurrentAssetPathsFromDict()
    {
        HashSet<string> set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (KeyValuePair<string, BundleInfo> pair in ABBuildTempData.ResourceBundleDict)
        {
            BundleInfo info = pair.Value;
            if (info != null && !string.IsNullOrEmpty(info.AssetPath))
            {
                set.Add(info.AssetPath);
            }
        }

        return set;
    }

    /// <summary>
    /// 「改变资源」涉及的包名集合，与后续增量 <c>BuildPipeline</c> 需要重打的包一致。
    /// 含：① lastMap 中有、当前工程中已删的资源，其旧所属包；② 磁盘 mtime 晚于上次打包的资源所在包；
    /// ③ 当前工程有、但 lastMap 中无记录的资源（新增）所在包。
    /// </summary>
    private static HashSet<string> CollectBundlesAffectedByResourceChanges(
        List<string> deletedAssetPaths,
        Dictionary<string, string> lastMapSnapshot,
        List<string> modifiedAssetPathsSinceBuild)
    {
        HashSet<string> affected = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < deletedAssetPaths.Count; i++)
        {
            if (lastMapSnapshot.TryGetValue(deletedAssetPaths[i], out string bundle))
            {
                if (!string.IsNullOrEmpty(bundle))
                {
                    affected.Add(bundle);
                }
            }
        }

        Dictionary<string, string> pathToBundle = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (KeyValuePair<string, BundleInfo> pair in ABBuildTempData.ResourceBundleDict)
        {
            BundleInfo info = pair.Value;
            if (info != null && !string.IsNullOrEmpty(info.AssetPath) && !string.IsNullOrEmpty(info.BundleName))
            {
                pathToBundle[info.AssetPath] = info.BundleName;
            }
        }

        for (int i = 0; i < modifiedAssetPathsSinceBuild.Count; i++)
        {
            string path = modifiedAssetPathsSinceBuild[i];
            if (pathToBundle.TryGetValue(path, out string bundle))
            {
                affected.Add(bundle);
            }
        }

        foreach (KeyValuePair<string, BundleInfo> pair in ABBuildTempData.ResourceBundleDict)
        {
            BundleInfo info = pair.Value;
            if (info == null || string.IsNullOrEmpty(info.AssetPath) || string.IsNullOrEmpty(info.BundleName))
            {
                continue;
            }

            if (!lastMapSnapshot.ContainsKey(info.AssetPath))
            {
                affected.Add(info.BundleName);
            }
        }

        return affected;
    }

    private static HashSet<string> CollectCurrentBundleNamesFromDict()
    {
        HashSet<string> set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (KeyValuePair<string, BundleInfo> pair in ABBuildTempData.ResourceBundleDict)
        {
            BundleInfo info = pair.Value;
            if (info != null && !string.IsNullOrEmpty(info.BundleName))
            {
                set.Add(info.BundleName);
            }
        }

        return set;
    }

    /// <summary>
    /// 当前 UI 扫描得到的「本次要打」的包名集合以外的包：lastMap 曾出现且已整包退出计划，以及输出目录中磁盘孤儿 *.ab。
    /// </summary>
    private static HashSet<string> CollectBundlesNotInCurrentBuildPlan(
        Dictionary<string, string> lastMapSnapshot,
        string abOutputDirectory)
    {
        HashSet<string> currentBundleNames = CollectCurrentBundleNamesFromDict();
        HashSet<string> notInPlan = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        HashSet<string> bundlesInLastMap = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (KeyValuePair<string, string> kv in lastMapSnapshot)
        {
            if (!string.IsNullOrEmpty(kv.Value))
            {
                bundlesInLastMap.Add(kv.Value);
            }
        }

        foreach (string bundleName in bundlesInLastMap)
        {
            if (!currentBundleNames.Contains(bundleName))
            {
                notInPlan.Add(bundleName);
            }
        }

        AddDiskBuiltAbNotInCurrentBuild(abOutputDirectory, currentBundleNames, notInPlan);

        return notInPlan;
    }

    /// <summary>
    /// 重打前需删除旧 <c>.ab</c> / <c>.manifest</c> 的包名 = 「不在当前构建计划」∪「改变资源涉及的包」。
    /// </summary>
    private static HashSet<string> CollectBundlesRequiringOldArtifactDeleteBeforeRebuild(
        HashSet<string> bundlesNotInCurrentBuildPlan,
        HashSet<string> bundlesAffectedByResourceChanges)
    {
        HashSet<string> union = new HashSet<string>(bundlesNotInCurrentBuildPlan, StringComparer.OrdinalIgnoreCase);
        foreach (string name in bundlesAffectedByResourceChanges)
        {
            union.Add(name);
        }

        return union;
    }

    /// <summary>
    /// 输出目录中已存在的 *.ab 文件名，若不在本次构建包名集合中，则加入 <paramref name="notInPlan"/>（物理层对比）。
    /// </summary>
    private static void AddDiskBuiltAbNotInCurrentBuild(
        string abOutputDirectory,
        HashSet<string> currentBundleNames,
        HashSet<string> notInPlan)
    {
        if (string.IsNullOrEmpty(abOutputDirectory) || !Directory.Exists(abOutputDirectory))
        {
            return;
        }

        string abs = Path.GetFullPath(abOutputDirectory);
        foreach (string file in Directory.GetFiles(abs, "*.ab", SearchOption.TopDirectoryOnly))
        {
            string fileName = Path.GetFileName(file);
            if (string.IsNullOrEmpty(fileName))
            {
                continue;
            }

            if (!currentBundleNames.Contains(fileName))
            {
                notInPlan.Add(fileName);
            }
        }
    }

    /// <summary>
    /// 读取 BuildTime.txt 中记录的上次打包时间（与 WriteBuildTimeFile 格式一致）。
    /// </summary>
    private static bool TryGetLastBuildTime(out DateTime buildTime)
    {
        buildTime = default;
        string absolutePath = Path.GetFullPath(BuildTimeFilePath);
        if (!File.Exists(absolutePath))
        {
            return false;
        }

        string text = File.ReadAllText(absolutePath).Trim();
        if (string.IsNullOrEmpty(text))
        {
            return false;
        }

        return DateTime.TryParseExact(
            text,
            "yyyy-MM-dd HH:mm:ss",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out buildTime);
    }
   
    /// <summary>
    /// 全量打包前：删除磁盘上全部 AB 产物；删除持久化记录；清空内存临时数据并移除 Editor 内 AssetBundle 名注册。
    /// </summary>
    private static void PrepareFullBuildCleanArtifactsAndState()
    {
        // 非持久化：内存中的映射与增量列表
        ABBuildTempData.InitForBuild();

        // 持久化：上次打包时间、资源路径与包名映射快照
        TryDeleteProjectAssetOrDiskFile(BuildTimeFilePath);
        TryDeleteProjectAssetOrDiskFile(ResourceBundleMapFilePath);

        // 磁盘上所有已打出的包（含各平台子目录及 manifest 等）——在 Assets 下须走 AssetDatabase，避免残留 .meta
        if (AssetDatabase.IsValidFolder(OutputRootPath))
        {
            AssetDatabase.DeleteAsset(OutputRootPath);
        }
        else
        {
            string abResAbsolute = Path.GetFullPath(OutputRootPath);
            if (Directory.Exists(abResAbsolute))
            {
                Directory.Delete(abResAbsolute, true);
            }

            string abResMeta = abResAbsolute + ".meta";
            if (File.Exists(abResMeta))
            {
                File.Delete(abResMeta);
            }
        }

        // 非持久化：Unity 工程内登记的 AssetBundle 名（与资源 .meta 上的 bundle 引用解绑）
        string[] oldBundleNames = AssetDatabase.GetAllAssetBundleNames();
        for (int i = 0; i < oldBundleNames.Length; i++)
        {
            AssetDatabase.RemoveAssetBundleName(oldBundleNames[i], true);
        }

        AssetDatabase.Refresh();
    }

    private static void TryDeleteProjectAssetOrDiskFile(string assetPath)
    {
        if (string.IsNullOrEmpty(assetPath))
        {
            return;
        }

        if (AssetDatabase.DeleteAsset(assetPath))
        {
            return;
        }

        string fullPath = Path.GetFullPath(assetPath);
        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);
        }

        string metaPath = fullPath + ".meta";
        if (File.Exists(metaPath))
        {
            File.Delete(metaPath);
        }
    }

    /// <summary>
    /// 全量流程：重建资源映射、写索引文件、执行AB构建并记录打包时间。
    /// </summary>
    /// <param name="outputPath">当前平台AB输出目录。</param>
    /// <param name="target">当前构建平台。</param>
    private static void BuildAllFromUI(string outputPath, BuildTarget target)
    {
        if (!AssetDatabase.IsValidFolder(UIRootPath))
        {
            Debug.LogError($"未找到目录: {UIRootPath}");
            return;
        }

        PrepareFullBuildCleanArtifactsAndState();

        if (!Directory.Exists(outputPath))
        {
            Directory.CreateDirectory(outputPath);
        }

        ABBuildTempData.ResetResourceBundleDictFromUI(UIRootPath);

        if (ABBuildTempData.ResourceBundleDict.Count == 0)
        {
            Debug.LogWarning($"{UIRootPath} 下没有可打包资源");
            return;
        }

        if (!BuildFromResourceBundleDict(outputPath, target, null))
        {
            return;
        }

        WriteResourceBundleMapFile();
        WriteBuildTimeFile();
        AssetDatabase.Refresh();
        Debug.Log($"AB打包完成，平台: {target}，输出: {outputPath}，资源映射数量: {ABBuildTempData.ResourceBundleDict.Count}");
    }

    /// <summary>
    /// 根据临时资源字典聚合出“包名 -> 资源列表”，并调用 Unity 打包接口。
    /// </summary>
    /// <param name="outputPath">AB输出目录。</param>
    /// <param name="target">目标构建平台。</param>
    /// <param name="onlyTheseBundleNames">非 null 时仅构建这些包名（增量）；null 时构建字典中全部包（全量）。</param>
    /// <returns>全量且无数据时为 false；增量无当前资源可打进列表时跳过 BuildPipeline 并返回 true。</returns>
    private static bool BuildFromResourceBundleDict(
        string outputPath,
        BuildTarget target,
        HashSet<string> onlyTheseBundleNames)
    {
        Dictionary<string, List<string>> bundleAssetsMap = new Dictionary<string, List<string>>();
        foreach (KeyValuePair<string, BundleInfo> pair in ABBuildTempData.ResourceBundleDict)
        {
            BundleInfo info = pair.Value;
            if (info == null || string.IsNullOrEmpty(info.BundleName) || string.IsNullOrEmpty(info.AssetPath))
            {
                continue;
            }

            if (onlyTheseBundleNames != null && !onlyTheseBundleNames.Contains(info.BundleName))
            {
                continue;
            }

            List<string> assetNames;
            if (!bundleAssetsMap.TryGetValue(info.BundleName, out assetNames))
            {
                assetNames = new List<string>();
                bundleAssetsMap.Add(info.BundleName, assetNames);
            }

            if (!assetNames.Contains(info.AssetPath))
            {
                assetNames.Add(info.AssetPath);
            }
        }

        if (bundleAssetsMap.Count == 0)
        {
            if (onlyTheseBundleNames != null)
            {
                Debug.Log("[增量AB] 变更涉及的包在当前资源映射中无条目，跳过 BuildPipeline（计划外 .ab 若存在已在上游删除）。");
                return true;
            }

            Debug.LogWarning("没有可打包的Bundle数据");
            return false;
        }

        // 重打前先删本批次的旧 .ab 与同名 .manifest，避免残留 manifest 与 Unity 新生成不一致
        DeleteBuiltBundleAbAndManifestFiles(outputPath, bundleAssetsMap.Keys);

        List<AssetBundleBuild> buildList = new List<AssetBundleBuild>(bundleAssetsMap.Count);
        foreach (KeyValuePair<string, List<string>> pair in bundleAssetsMap)
        {
            buildList.Add(new AssetBundleBuild
            {
                assetBundleName = pair.Key,
                assetNames = pair.Value.ToArray()
            });
        }

        BuildPipeline.BuildAssetBundles(
            outputPath,
            buildList.ToArray(),
            BuildAssetBundleOptions.None,
            target
        );

        // BuildPipeline 写磁盘后偶发与 .meta 不同步；扫一遍 AB 输出根目录下孤儿 .meta
        DeleteOrphanMetaFilesUnderDirectory(AssetPathToAbsoluteUnderProject(OutputRootPath));
        AssetDatabase.Refresh();
        return true;
    }

    /// <summary>
    /// 删除输出目录下指定包名的旧 <c>*.ab</c> 与同名的 <c>*.ab.manifest</c>（Unity BuildPipeline 对单个 AB 的配套 manifest）。
    /// </summary>
    private static void DeleteBuiltBundleAbAndManifestFiles(string outputDirectory, IEnumerable<string> bundleFileNames)
    {
        if (string.IsNullOrEmpty(outputDirectory) || bundleFileNames == null)
        {
            return;
        }

        string absDir = Path.GetFullPath(outputDirectory);
        if (!Directory.Exists(absDir))
        {
            return;
        }

        foreach (string bundleFileName in bundleFileNames)
        {
            if (string.IsNullOrEmpty(bundleFileName))
            {
                continue;
            }

            DeleteAssetOrDiskFileByAbsolutePath(Path.Combine(absDir, bundleFileName));
            DeleteAssetOrDiskFileByAbsolutePath(Path.Combine(absDir, bundleFileName + ".manifest"));
        }

        // 删包后清理「有 .meta、无对应资源文件」的残留（含历史在 Unity 外删文件导致的情况）
        DeleteOrphanMetaFilesUnderDirectory(AssetPathToAbsoluteUnderProject(OutputRootPath));
        AssetDatabase.Refresh();
    }

    /// <summary>
    /// 将 <c>Assets/...</c> 路径转为工程内绝对路径（用于磁盘扫描）。
    /// </summary>
    private static string AssetPathToAbsoluteUnderProject(string assetPath)
    {
        if (string.IsNullOrEmpty(assetPath))
        {
            return string.Empty;
        }

        string normalized = assetPath.Replace('/', Path.DirectorySeparatorChar);
        if (normalized.StartsWith("Assets" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            string tail = normalized.Substring(7);
            return Path.GetFullPath(Path.Combine(Application.dataPath, tail));
        }

        if (normalized.Equals("Assets", StringComparison.OrdinalIgnoreCase))
        {
            return Path.GetFullPath(Application.dataPath);
        }

        return Path.GetFullPath(assetPath);
    }

    /// <summary>
    /// 删除目录下「磁盘上存在 .meta，但对应主资源（去掉 .meta 后缀）既不是文件也不是文件夹」的孤儿 meta，且位于 <c>Assets/</c> 时用 <see cref="AssetDatabase.DeleteAsset"/>。
    /// </summary>
    private static void DeleteOrphanMetaFilesUnderDirectory(string directoryAbsolute)
    {
        if (string.IsNullOrEmpty(directoryAbsolute) || !Directory.Exists(directoryAbsolute))
        {
            return;
        }

        string dirAbs = Path.GetFullPath(directoryAbsolute);
        string assetsRoot = Path.GetFullPath(Application.dataPath);
        if (dirAbs.Length < assetsRoot.Length || !dirAbs.StartsWith(assetsRoot, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        string[] metaFiles = Directory.GetFiles(dirAbs, "*.meta", SearchOption.AllDirectories);
        for (int i = 0; i < metaFiles.Length; i++)
        {
            string metaAbs = Path.GetFullPath(metaFiles[i]);
            const string metaSuffix = ".meta";
            if (!metaAbs.EndsWith(metaSuffix, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string assetAbs = metaAbs.Substring(0, metaAbs.Length - metaSuffix.Length);
            if (File.Exists(assetAbs) || Directory.Exists(assetAbs))
            {
                continue;
            }

            string metaAssetPath = AbsolutePathUnderAssetsToAssetPath(metaAbs);
            if (!string.IsNullOrEmpty(metaAssetPath))
            {
                AssetDatabase.DeleteAsset(metaAssetPath);
            }
            else if (File.Exists(metaAbs))
            {
                File.Delete(metaAbs);
            }
        }
    }

    /// <summary>
    /// 绝对路径若在 <c>Assets</c> 下则转为 <c>Assets/...</c>，否则返回 null。
    /// </summary>
    private static string AbsolutePathUnderAssetsToAssetPath(string absolutePath)
    {
        if (string.IsNullOrEmpty(absolutePath))
        {
            return null;
        }

        string full = Path.GetFullPath(absolutePath);
        string assetsRoot = Path.GetFullPath(Application.dataPath);

        if (full.Length <= assetsRoot.Length || !full.StartsWith(assetsRoot, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        char sepAfterAssets = full[assetsRoot.Length];
        if (sepAfterAssets != Path.DirectorySeparatorChar && sepAfterAssets != Path.AltDirectorySeparatorChar)
        {
            return null;
        }

        string suffix = full.Substring(assetsRoot.Length + 1).Replace('\\', '/');
        return suffix.Length > 0 ? "Assets/" + suffix : null;
    }

    /// <summary>
    /// 位于 <c>Assets/</c> 下的文件优先用 <see cref="AssetDatabase.DeleteAsset"/> 删除（同时移除 .meta）；否则删磁盘并尝试删 .meta。
    /// </summary>
    private static void DeleteAssetOrDiskFileByAbsolutePath(string absolutePath)
    {
        if (string.IsNullOrEmpty(absolutePath))
        {
            return;
        }

        string full = Path.GetFullPath(absolutePath);
        string assetPath = AbsolutePathUnderAssetsToAssetPath(full);
        if (!string.IsNullOrEmpty(assetPath))
        {
            if (AssetDatabase.DeleteAsset(assetPath))
            {
                return;
            }
        }

        if (File.Exists(full))
        {
            File.Delete(full);
        }

        string metaPath = full + ".meta";
        if (!string.IsNullOrEmpty(AbsolutePathUnderAssetsToAssetPath(metaPath)))
        {
            string metaAssetPath = AbsolutePathUnderAssetsToAssetPath(metaPath);
            if (!string.IsNullOrEmpty(metaAssetPath) && AssetDatabase.DeleteAsset(metaAssetPath))
            {
                return;
            }
        }

        if (File.Exists(metaPath))
        {
            File.Delete(metaPath);
        }
    }

    /// <summary>
    /// 写入本次打包的全局时间戳到BuildTime文件。
    /// </summary>
    private static void WriteBuildTimeFile()
    {
        string buildTimeFileAbsolutePath = Path.GetFullPath(BuildTimeFilePath);
        string buildTimeDirectory = Path.GetDirectoryName(buildTimeFileAbsolutePath);
        if (!string.IsNullOrEmpty(buildTimeDirectory) && !Directory.Exists(buildTimeDirectory))
        {
            Directory.CreateDirectory(buildTimeDirectory);
        }

        string now = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        File.WriteAllText(buildTimeFileAbsolutePath, now);
    }

    /// <summary>
    /// 将「上次成功打 AB」的资源快照落盘：首行为版本头，每行 AssetPath + Tab + BundleName。
    /// 全量在整包 BuildPipeline 成功后调用；增量仅在增量 BuildPipeline 成功后调用。
    /// </summary>
    private static void WriteResourceBundleMapFile()
    {
        string mapFileAbsolutePath = Path.GetFullPath(ResourceBundleMapFilePath);
        string mapDirectory = Path.GetDirectoryName(mapFileAbsolutePath);
        if (!string.IsNullOrEmpty(mapDirectory) && !Directory.Exists(mapDirectory))
        {
            Directory.CreateDirectory(mapDirectory);
        }

        List<string> lines = new List<string>(1 + ABBuildTempData.ResourceBundleDict.Count)
        {
            ResourceBundleMapHeader
        };

        foreach (KeyValuePair<string, BundleInfo> pair in ABBuildTempData.ResourceBundleDict)
        {
            BundleInfo info = pair.Value;
            if (info == null || string.IsNullOrEmpty(info.AssetPath) || string.IsNullOrEmpty(info.BundleName))
            {
                continue;
            }

            lines.Add(info.AssetPath + "\t" + info.BundleName);
        }

        File.WriteAllLines(mapFileAbsolutePath, lines);
    }

    /// <summary>
    /// 读取持久化 Map。支持 v1（Tab）；兼容旧版「文件名 + 双空格 + 包名」（无法可靠做路径级删除对比，建议全量重打后换新格式）。
    /// </summary>
    private static bool TryReadResourceBundleMap(out Dictionary<string, string> assetPathToBundle)
    {
        assetPathToBundle = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        string mapFileAbsolutePath = Path.GetFullPath(ResourceBundleMapFilePath);
        if (!File.Exists(mapFileAbsolutePath))
        {
            return false;
        }

        string[] allLines = File.ReadAllLines(mapFileAbsolutePath);
        for (int i = 0; i < allLines.Length; i++)
        {
            string line = allLines[i].Trim();
            if (line.Length == 0 || line[0] == '#')
            {
                continue;
            }

            int tabIndex = line.IndexOf('\t');
            if (tabIndex >= 0)
            {
                string assetPath = line.Substring(0, tabIndex).Trim();
                string bundleName = line.Substring(tabIndex + 1).Trim();
                if (assetPath.Length > 0 && bundleName.Length > 0)
                {
                    assetPathToBundle[assetPath] = bundleName;
                }

                continue;
            }

            int doubleSpace = line.IndexOf("  ", StringComparison.Ordinal);
            if (doubleSpace >= 0)
            {
                string fileName = line.Substring(0, doubleSpace).Trim();
                string bundleName = line.Substring(doubleSpace + 2).Trim();
                if (fileName.Length > 0 && bundleName.Length > 0)
                {
                    assetPathToBundle[fileName] = bundleName;
                }
            }
        }

        return true;
    }
}
