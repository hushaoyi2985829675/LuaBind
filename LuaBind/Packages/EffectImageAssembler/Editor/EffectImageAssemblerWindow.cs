using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEngine;
using UnityEngine.UI;
using Debug = UnityEngine.Debug;

namespace EffectImageAssembler.Editor
{
    public sealed class EffectImageAssemblerWindow : EditorWindow
    {
        private enum AssetSearchScope
        {
            SelectedFolder,
            UiAndArtFolders,
            EntireAssets
        }

        private Texture2D referenceImage;
        private DefaultAsset spritesFolder;
        private AssetSearchScope searchScope = AssetSearchScope.SelectedFolder;
        private string outputPrefabPath = "Assets/AutoAssembledUI.prefab";
        private string pythonExecutable = "python";
        private float minScale = 0.5f;
        private float maxScale = 2.0f;
        private float scaleStep = 0.1f;
        private float matchThreshold = 0.92f;
        private int maxOccurrencesPerSprite = 64;
        private int maxLayerIterations = 5;
        private bool forceSpriteImportSettings = true;

        [MenuItem("Tools/Effect Image Assembler")]
        public static void Open()
        {
            GetWindow<EffectImageAssemblerWindow>("Effect Image Assembler");
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Inputs", EditorStyles.boldLabel);
            referenceImage = (Texture2D)EditorGUILayout.ObjectField("Reference Image", referenceImage, typeof(Texture2D), false);
            searchScope = (AssetSearchScope)EditorGUILayout.EnumPopup("Search Scope", searchScope);
            using (new EditorGUI.DisabledScope(searchScope != AssetSearchScope.SelectedFolder))
            {
                spritesFolder = (DefaultAsset)EditorGUILayout.ObjectField("Sprites Folder", spritesFolder, typeof(DefaultAsset), false);
            }
            outputPrefabPath = EditorGUILayout.TextField("Output Prefab Path", outputPrefabPath);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Solver", EditorStyles.boldLabel);
            pythonExecutable = EditorGUILayout.TextField("Python Executable", pythonExecutable);
            minScale = EditorGUILayout.FloatField("Min Scale", minScale);
            maxScale = EditorGUILayout.FloatField("Max Scale", maxScale);
            scaleStep = EditorGUILayout.FloatField("Scale Step", scaleStep);
            matchThreshold = EditorGUILayout.Slider("Match Threshold", matchThreshold, 0.5f, 0.999f);
            maxOccurrencesPerSprite = EditorGUILayout.IntField("Max Occurrences / Sprite", maxOccurrencesPerSprite);
            maxLayerIterations = EditorGUILayout.IntField("Max Layer Iterations", maxLayerIterations);
            forceSpriteImportSettings = EditorGUILayout.Toggle("Force Sprite Import Settings", forceSpriteImportSettings);

            EditorGUILayout.Space();
            using (new EditorGUI.DisabledScope(referenceImage == null || (searchScope == AssetSearchScope.SelectedFolder && spritesFolder == null)))
            {
                if (GUILayout.Button("Generate UGUI Prefab", GUILayout.Height(34)))
                {
                    Generate();
                }
            }
        }

        private void Generate()
        {
            string referenceAssetPath = AssetDatabase.GetAssetPath(referenceImage);
            string spritesAssetPath = spritesFolder == null ? string.Empty : AssetDatabase.GetAssetPath(spritesFolder);

            if (!ValidateInputs(referenceAssetPath, spritesAssetPath))
            {
                return;
            }

            if (forceSpriteImportSettings)
            {
                ConfigurePngImports(GetSearchRoots(spritesAssetPath), searchScope == AssetSearchScope.SelectedFolder);
            }

            string solverPath = FindSolverPath();
            if (string.IsNullOrEmpty(solverPath))
            {
                EditorUtility.DisplayDialog("Effect Image Assembler", "Could not find effect_image_assembler_solver.py.", "OK");
                return;
            }

            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
            string absoluteReferencePath = Path.GetFullPath(Path.Combine(projectRoot, referenceAssetPath));
            string reportDirectory = PrepareReportDirectory(projectRoot);
            string outputJsonPath = Path.Combine(reportDirectory, "matches.json");
            string assetIndexPath = Path.Combine(reportDirectory, "asset-index.json");
            ExportSpriteIndex(assetIndexPath, GetSearchRoots(spritesAssetPath), referenceAssetPath);

            try
            {
                RunSolver(solverPath, absoluteReferencePath, assetIndexPath, outputJsonPath, reportDirectory);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorUtility.DisplayDialog("Effect Image Assembler", exception.Message, "OK");
                return;
            }

            if (!File.Exists(outputJsonPath))
            {
                EditorUtility.DisplayDialog("Effect Image Assembler", "Solver finished but did not create matches.json.", "OK");
                return;
            }

            var result = JsonUtility.FromJson<SolverResult>(File.ReadAllText(outputJsonPath));
            if (result == null || result.elements == null)
            {
                EditorUtility.DisplayDialog("Effect Image Assembler", "Could not parse solver JSON.", "OK");
                return;
            }

            CreatePrefab(result, projectRoot);
            CopyReportFiles(reportDirectory);
            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog(
                "Effect Image Assembler",
                $"Generated {result.elements.Length} UI elements.\nPrefab: {outputPrefabPath}",
                "OK");
        }

        private bool ValidateInputs(string referenceAssetPath, string spritesAssetPath)
        {
            if (string.IsNullOrEmpty(referenceAssetPath) || !referenceAssetPath.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
            {
                EditorUtility.DisplayDialog("Effect Image Assembler", "Reference Image must be a PNG asset.", "OK");
                return false;
            }

            if (searchScope == AssetSearchScope.SelectedFolder && (string.IsNullOrEmpty(spritesAssetPath) || !AssetDatabase.IsValidFolder(spritesAssetPath)))
            {
                EditorUtility.DisplayDialog("Effect Image Assembler", "Sprites Folder must be a Unity asset folder.", "OK");
                return false;
            }

            if (!outputPrefabPath.StartsWith("Assets/", StringComparison.Ordinal) || !outputPrefabPath.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
            {
                EditorUtility.DisplayDialog("Effect Image Assembler", "Output Prefab Path must be under Assets and end with .prefab.", "OK");
                return false;
            }

            return true;
        }

        private string[] GetSearchRoots(string selectedSpritesPath)
        {
            switch (searchScope)
            {
                case AssetSearchScope.SelectedFolder:
                    return new[] { selectedSpritesPath };
                case AssetSearchScope.UiAndArtFolders:
                    return new[] { "Assets/UI", "Assets/Art" }.Where(AssetDatabase.IsValidFolder).ToArray();
                case AssetSearchScope.EntireAssets:
                    return new[] { "Assets" };
                default:
                    return new[] { "Assets" };
            }
        }

        private static string FindSolverPath()
        {
            string[] guids = AssetDatabase.FindAssets("effect_image_assembler_solver");
            foreach (string guid in guids)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                if (assetPath.EndsWith("effect_image_assembler_solver.py", StringComparison.OrdinalIgnoreCase))
                {
                    if (assetPath.StartsWith("Packages/", StringComparison.Ordinal))
                    {
                        var packageInfo = UnityEditor.PackageManager.PackageInfo.FindForAssetPath(assetPath);
                        if (packageInfo != null)
                        {
                            string virtualRoot = "Packages/" + packageInfo.name + "/";
                            string relativePath = assetPath.Substring(virtualRoot.Length);
                            return Path.GetFullPath(Path.Combine(packageInfo.resolvedPath, relativePath));
                        }
                    }
                    else
                    {
                        string projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
                        return Path.GetFullPath(Path.Combine(projectRoot, assetPath));
                    }
                }
            }

            return null;
        }

        private void ConfigurePngImports(string[] searchRoots, bool convertTexturesToSprites)
        {
            if (searchRoots.Length == 0)
            {
                return;
            }

            string[] guids = AssetDatabase.FindAssets("t:Texture2D", searchRoots);
            foreach (string guid in guids)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                if (!assetPath.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
                if (importer == null)
                {
                    continue;
                }

                bool dirty = false;
                if (importer.textureType != TextureImporterType.Sprite)
                {
                    if (!convertTexturesToSprites)
                    {
                        continue;
                    }

                    importer.textureType = TextureImporterType.Sprite;
                    dirty = true;
                }
                dirty |= SetIfDifferent(importer.alphaIsTransparency, true, value => importer.alphaIsTransparency = value);
                dirty |= SetIfDifferent(importer.mipmapEnabled, false, value => importer.mipmapEnabled = value);
                dirty |= SetIfDifferent(importer.textureCompression, TextureImporterCompression.Uncompressed, value => importer.textureCompression = value);

                if (dirty)
                {
                    importer.SaveAndReimport();
                }
            }
        }

        private void ExportSpriteIndex(string outputPath, string[] searchRoots, string excludedAssetPath)
        {
            if (searchRoots.Length == 0)
            {
                throw new InvalidOperationException("No valid sprite search roots were found.");
            }

            var index = new SpriteIndex
            {
                searchScope = searchScope.ToString(),
                searchRoots = searchRoots,
                sprites = new List<SpriteIndexEntry>()
            };

            string[] guids = AssetDatabase.FindAssets("t:Texture2D", searchRoots);
            foreach (string guid in guids)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                if (!assetPath.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (string.Equals(assetPath, excludedAssetPath, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
                if (importer == null || importer.textureType != TextureImporterType.Sprite)
                {
                    continue;
                }

                var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
                if (texture == null)
                {
                    continue;
                }

                var sprites = AssetDatabase.LoadAllAssetsAtPath(assetPath).OfType<Sprite>().ToArray();
                if (sprites.Length == 0)
                {
                    var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
                    if (sprite != null)
                    {
                        sprites = new[] { sprite };
                    }
                }

                foreach (var sprite in sprites)
                {
                    Rect rect = sprite.rect;
                    Vector4 border = sprite.border;
                    index.sprites.Add(new SpriteIndexEntry
                    {
                        name = sprite.name,
                        assetPath = assetPath,
                        texturePath = assetPath,
                        textureWidth = texture.width,
                        textureHeight = texture.height,
                        rect = new[] { rect.x, rect.y, rect.width, rect.height },
                        pivot = new[] { sprite.pivot.x, sprite.pivot.y },
                        border = new[] { border.x, border.y, border.z, border.w },
                        packingTag = importer.spritePackingTag ?? string.Empty,
                        importMode = importer.spriteImportMode.ToString()
                    });
                }
            }

            Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? ".");
            File.WriteAllText(outputPath, JsonUtility.ToJson(index, true), Encoding.UTF8);
        }

        private static bool SetIfDifferent<T>(T current, T desired, Action<T> assign)
        {
            if (EqualityComparer<T>.Default.Equals(current, desired))
            {
                return false;
            }

            assign(desired);
            return true;
        }

        private string PrepareReportDirectory(string projectRoot)
        {
            string prefabName = Path.GetFileNameWithoutExtension(outputPrefabPath);
            string absoluteDirectory = Path.Combine(projectRoot, "Library", "EffectImageAssembler", prefabName);
            Directory.CreateDirectory(absoluteDirectory);
            foreach (string file in Directory.GetFiles(absoluteDirectory))
            {
                File.Delete(file);
            }

            return absoluteDirectory;
        }

        private void RunSolver(string solverPath, string referencePath, string assetIndexPath, string outputJsonPath, string reportDirectory)
        {
            var args = new List<string>
            {
                Quote(solverPath),
                "--reference", Quote(referencePath),
                "--asset-index", Quote(assetIndexPath),
                "--output-json", Quote(outputJsonPath),
                "--report-dir", Quote(reportDirectory),
                "--min-scale", minScale.ToString(CultureInfo.InvariantCulture),
                "--max-scale", maxScale.ToString(CultureInfo.InvariantCulture),
                "--scale-step", scaleStep.ToString(CultureInfo.InvariantCulture),
                "--threshold", matchThreshold.ToString(CultureInfo.InvariantCulture),
                "--max-occurrences", maxOccurrencesPerSprite.ToString(CultureInfo.InvariantCulture),
                "--layer-iterations", maxLayerIterations.ToString(CultureInfo.InvariantCulture)
            };

            var startInfo = new ProcessStartInfo
            {
                FileName = pythonExecutable,
                Arguments = string.Join(" ", args),
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            using var process = Process.Start(startInfo);
            if (process == null)
            {
                throw new InvalidOperationException("Failed to start Python solver.");
            }

            var stdoutBuilder = new StringBuilder();
            var stderrBuilder = new StringBuilder();
            process.OutputDataReceived += (_, eventArgs) =>
            {
                if (eventArgs.Data != null)
                {
                    stdoutBuilder.AppendLine(eventArgs.Data);
                }
            };
            process.ErrorDataReceived += (_, eventArgs) =>
            {
                if (eventArgs.Data != null)
                {
                    stderrBuilder.AppendLine(eventArgs.Data);
                }
            };
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            process.WaitForExit();

            string stdout = stdoutBuilder.ToString();
            string stderr = stderrBuilder.ToString();
            if (!string.IsNullOrWhiteSpace(stdout))
            {
                Debug.Log(stdout);
            }

            if (process.ExitCode != 0)
            {
                Debug.LogError(stderr);
                throw new InvalidOperationException($"Solver failed with exit code {process.ExitCode}.");
            }

            if (!string.IsNullOrWhiteSpace(stderr))
            {
                Debug.LogWarning(stderr);
            }
        }

        private void CreatePrefab(SolverResult result, string projectRoot)
        {
            string outputDirectory = Path.GetDirectoryName(outputPrefabPath);
            if (!string.IsNullOrEmpty(outputDirectory) && !Directory.Exists(outputDirectory))
            {
                Directory.CreateDirectory(outputDirectory);
            }

            var canvasObject = new GameObject("AutoAssembledCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(result.referenceSize[0], result.referenceSize[1]);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            var rootObject = new GameObject("AutoAssembledUI", typeof(RectTransform));
            rootObject.transform.SetParent(canvasObject.transform, false);
            var rootRect = rootObject.GetComponent<RectTransform>();
            ApplyTopLeftRect(rootRect, 0, 0, result.referenceSize[0], result.referenceSize[1]);

            foreach (var element in result.elements.OrderBy(e => e.order))
            {
                string assetPath = ToUnityAssetPath(element.asset, projectRoot);
                var sprite = LoadSprite(assetPath, element.spriteName);
                if (sprite == null)
                {
                    Debug.LogWarning($"Sprite not found for matched element: {element.asset} / {element.spriteName}");
                    continue;
                }

                var imageObject = new GameObject(element.name, typeof(RectTransform), typeof(Image));
                imageObject.transform.SetParent(rootObject.transform, false);

                var rect = imageObject.GetComponent<RectTransform>();
                ApplyTopLeftRect(rect, element.x, element.y, element.width, element.height);

                var image = imageObject.GetComponent<Image>();
                image.sprite = sprite;
                image.type = Image.Type.Simple;
                image.preserveAspect = false;
                image.raycastTarget = false;
            }

            PrefabUtility.SaveAsPrefabAsset(canvasObject, outputPrefabPath);
            DestroyImmediate(canvasObject);
        }

        private static Sprite LoadSprite(string assetPath, string spriteName)
        {
            if (!string.IsNullOrEmpty(spriteName))
            {
                foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(assetPath))
                {
                    if (asset is Sprite sprite && sprite.name == spriteName)
                    {
                        return sprite;
                    }
                }
            }

            return AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
        }

        private static void ApplyTopLeftRect(RectTransform rect, float x, float y, float width, float height)
        {
            rect.anchorMin = new Vector2(0, 1);
            rect.anchorMax = new Vector2(0, 1);
            rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(width, height);
            rect.localScale = Vector3.one;
        }

        private static string ToUnityAssetPath(string path, string projectRoot)
        {
            string normalized = path.Replace('\\', '/');
            if (normalized.StartsWith("Assets/", StringComparison.Ordinal) || normalized.StartsWith("Packages/", StringComparison.Ordinal))
            {
                return normalized;
            }

            string absoluteProject = Path.GetFullPath(projectRoot).Replace('\\', '/').TrimEnd('/');
            string absolutePath = Path.GetFullPath(path).Replace('\\', '/');
            if (absolutePath.StartsWith(absoluteProject + "/", StringComparison.OrdinalIgnoreCase))
            {
                return absolutePath.Substring(absoluteProject.Length + 1);
            }

            return normalized;
        }

        private void CopyReportFiles(string reportDirectory)
        {
            string outputDirectory = Path.GetDirectoryName(outputPrefabPath) ?? "Assets";
            Directory.CreateDirectory(outputDirectory);
            string baseName = Path.Combine(outputDirectory, Path.GetFileNameWithoutExtension(outputPrefabPath));

            CopyIfExists(Path.Combine(reportDirectory, "matches.json"), baseName + ".matches.json");
            CopyIfExists(Path.Combine(reportDirectory, "report.txt"), baseName + ".report.txt");
            CopyIfExists(Path.Combine(reportDirectory, "composite.png"), baseName + ".composite.png");
            CopyIfExists(Path.Combine(reportDirectory, "diff.png"), baseName + ".diff.png");
        }

        private static void CopyIfExists(string source, string destination)
        {
            if (File.Exists(source))
            {
                File.Copy(source, destination, true);
            }
        }

        private static string Quote(string value)
        {
            return "\"" + value.Replace("\"", "\\\"") + "\"";
        }

        [Serializable]
        private sealed class SolverResult
        {
            public int[] referenceSize;
            public Element[] elements;
            public UnmatchedRegion[] unmatchedRegions;
        }

        [Serializable]
        private sealed class Element
        {
            public string name;
            public string asset;
            public string spriteName;
            public float x;
            public float y;
            public float width;
            public float height;
            public float scale;
            public int order;
            public float confidence;
        }

        [Serializable]
        private sealed class UnmatchedRegion
        {
            public int x;
            public int y;
            public int width;
            public int height;
            public float meanError;
        }

        [Serializable]
        private sealed class SpriteIndex
        {
            public string searchScope;
            public string[] searchRoots;
            public List<SpriteIndexEntry> sprites;
        }

        [Serializable]
        private sealed class SpriteIndexEntry
        {
            public string name;
            public string assetPath;
            public string texturePath;
            public int textureWidth;
            public int textureHeight;
            public float[] rect;
            public float[] pivot;
            public float[] border;
            public string packingTag;
            public string importMode;
        }
    }
}
