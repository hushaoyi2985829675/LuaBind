using System;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class PbProtoImportEditor
{
    private const string MenuPath = "Tools/导Pb";

    [MenuItem(MenuPath, false, 100)]
    private static void ImportPbProtoFiles()
    {
        string unityProjectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        string sourceRoot = Path.GetFullPath(Path.Combine(unityProjectRoot, "..", "Backend", "proto"));
        string targetRoot = Path.GetFullPath(Path.Combine(unityProjectRoot, "Assets", "Pb", "proto"));

        if (!Directory.Exists(sourceRoot))
        {
            EditorUtility.DisplayDialog("导Pb失败", "源目录不存在:\n" + sourceRoot, "确定");
            Debug.LogError("[PbProtoImport] source directory not found: " + sourceRoot);
            return;
        }

        if (!Directory.Exists(targetRoot))
        {
            Directory.CreateDirectory(targetRoot);
        }

        string[] protoFiles = Directory.GetFiles(sourceRoot, "*.proto", SearchOption.AllDirectories);
        int copiedCount = 0;

        for (int i = 0; i < protoFiles.Length; i++)
        {
            string sourceFile = protoFiles[i];
            string relativePath = Path.GetRelativePath(sourceRoot, sourceFile);
            string targetFile = Path.Combine(targetRoot, relativePath);
            string targetDir = Path.GetDirectoryName(targetFile);

            if (!string.IsNullOrEmpty(targetDir) && !Directory.Exists(targetDir))
            {
                Directory.CreateDirectory(targetDir);
            }

            File.Copy(sourceFile, targetFile, true);
            copiedCount++;
        }

        AssetDatabase.Refresh();
        string message = $"导Pb完成\n来源: {sourceRoot}\n目标: {targetRoot}\n复制文件数: {copiedCount}";
        EditorUtility.DisplayDialog("导Pb成功", message, "确定");
        Debug.Log("[PbProtoImport] " + message.Replace("\n", " | "));
    }
}
