// <summary>
// 职责：迁移现有工程到 URP，并核验场景、材质和渲染输出。
// 模块：Editor；创建日期：2026-10-18。
// 仅在用户确认迁移并完成工程备份后执行；不改变游戏玩法或数值。
// </summary>
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEditor.Rendering.Universal;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace MyTowerDefense.Editor
{
    /// <summary>已备份工程的 URP 配置迁移及实际相机渲染核验入口。</summary>
    public static class ProjectConfigurationMigration
    {
        private const string Folder = "Assets/Settings/Rendering";
        private const string PipelinePath = Folder + "/MyTowerDefenseURP.asset";
        private static readonly List<string> Changes = new List<string>();
        private static int _validationFrames;

        [Serializable]
        private sealed class VerificationResult
        {
            public bool passed;
            public string unityVersion;
            public string urpVersion;
            public string apiCompatibility;
            public string scriptingBackend;
            public string graphicsDevice;
            public string pipeline;
            public int qualityLevels;
            public string[] changes;
            public string[] screenshots;
            public string error;
        }

        /// <summary>应用已授权的迁移，保存配置后在编辑器更新中核验并退出批处理。</summary>
        [MenuItem("Tools/MyTowerDefense/Apply Approved URP Migration")]
        public static void Apply()
        {
            try
            {
                Changes.Clear();
                if (!AssetDatabase.IsValidFolder("Assets/Settings"))
                    AssetDatabase.CreateFolder("Assets", "Settings");
                if (!AssetDatabase.IsValidFolder(Folder))
                    AssetDatabase.CreateFolder("Assets/Settings", "Rendering");

                var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelinePath);
                if (pipeline == null)
                {
                    var renderer = ScriptableObject.CreateInstance<UniversalRendererData>();
                    ResourceReloader.ReloadAllNullIn(renderer, "Packages/com.unity.render-pipelines.universal");
                    AssetDatabase.CreateAsset(renderer, Folder + "/MyTowerDefenseRenderer.asset");
                    pipeline = UniversalRenderPipelineAsset.Create(renderer);
                    AssetDatabase.CreateAsset(pipeline, PipelinePath);
                }

                var graphics = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset")[0]);
                graphics.FindProperty("m_CustomRenderPipeline").objectReferenceValue = pipeline;
                graphics.ApplyModifiedPropertiesWithoutUndo();
                var quality = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/QualitySettings.asset")[0]);
                SerializedProperty levels = quality.FindProperty("m_QualitySettings");
                for (int i = 0; i < levels.arraySize; i++)
                    levels.GetArrayElementAtIndex(i).FindPropertyRelative("customRenderPipeline").objectReferenceValue = pipeline;
                quality.ApplyModifiedPropertiesWithoutUndo();

                foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { "Assets" }))
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    // 模型内嵌材质由导入器持有，不能直接写回导入缓存。
                    if (!path.EndsWith(".mat", StringComparison.OrdinalIgnoreCase)) continue;
                    Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
                    if (material == null || material.shader == null) throw new InvalidOperationException("Missing material shader: " + path);
                    string oldShader = material.shader.name;
                    if (oldShader == "Standard" || oldShader == "Standard (Specular setup)")
                        new StandardUpgrader(oldShader).Upgrade(material, MaterialUpgrader.UpgradeFlags.None);
                    else if (oldShader.StartsWith("Particles/Standard", StringComparison.Ordinal))
                        new ParticleUpgrader(oldShader).Upgrade(material, MaterialUpgrader.UpgradeFlags.None);
                    else if (oldShader.Contains("Particles/") && !oldShader.StartsWith("Universal Render Pipeline/", StringComparison.Ordinal))
                        ConvertLegacyParticle(material, oldShader);
                    else continue; // TMP、天空盒及现有 URP Shader Graph 保持原配置。
                    EditorUtility.SetDirty(material);
                    Changes.Add(path + ": " + oldShader + " -> " + material.shader.name);
                }

                string terrainPath = Folder + "/MyTowerDefenseTerrain.mat";
                Material terrainMaterial = AssetDatabase.LoadAssetAtPath<Material>(terrainPath);
                if (terrainMaterial == null)
                {
                    Shader terrainShader = Shader.Find("Universal Render Pipeline/Terrain/Lit");
                    if (terrainShader == null) throw new InvalidOperationException("URP terrain shader is unavailable.");
                    terrainMaterial = new Material(terrainShader);
                    AssetDatabase.CreateAsset(terrainMaterial, terrainPath);
                }

                foreach (EditorBuildSettingsScene entry in EditorBuildSettings.scenes)
                {
                    if (!entry.enabled) continue;
                    Scene scene = EditorSceneManager.OpenScene(entry.path, OpenSceneMode.Single);
                    foreach (GameObject root in scene.GetRootGameObjects())
                    {
                        foreach (Terrain terrain in root.GetComponentsInChildren<Terrain>(true))
                            terrain.materialTemplate = terrainMaterial;
                        foreach (Camera camera in root.GetComponentsInChildren<Camera>(true))
                            camera.GetUniversalAdditionalCameraData();
                    }
                    EditorSceneManager.MarkSceneDirty(scene);
                    if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Scene save failed: " + entry.path);
                    Changes.Add("Scene migrated: " + entry.path);
                }
                AssetDatabase.SaveAssets();
                _validationFrames = 0;
                EditorApplication.update += WaitForPipeline;
            }
            catch (Exception exception) { Finish(null, exception); }
        }

        private static void ConvertLegacyParticle(Material material, string oldShader)
        {
            Texture texture = material.GetTexture("_MainTex");
            Vector2 scale = material.GetTextureScale("_MainTex");
            Vector2 offset = material.GetTextureOffset("_MainTex");
            Color tint = material.HasProperty("_TintColor") ? material.GetColor("_TintColor") : Color.white;
            Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null) throw new InvalidOperationException("URP particle shader is unavailable.");
            material.shader = shader;
            material.shaderKeywords = Array.Empty<string>();
            material.SetTexture("_BaseMap", texture);
            material.SetTextureScale("_BaseMap", scale);
            material.SetTextureOffset("_BaseMap", offset);
            // 旧粒子 Shader 对顶点色和 TintColor 乘二，保留对应基础色强度。
            material.SetColor("_BaseColor", tint * 2f);
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", oldShader.Contains("Additive") ? 2f : 0f);
            material.SetFloat("_Cull", (float)CullMode.Off);
            BaseShaderGUI.SetupMaterialBlendMode(material);
        }

        private static void WaitForPipeline()
        {
            if (++_validationFrames < 20) return;
            EditorApplication.update -= WaitForPipeline;
            try { Finish(Verify(), null); }
            catch (Exception exception) { Finish(null, exception); }
        }

        private static VerificationResult Verify()
        {
            var pipeline = GraphicsSettings.renderPipelineAsset as UniversalRenderPipelineAsset;
            if (pipeline == null) throw new InvalidOperationException("Graphics pipeline is not URP.");
            for (int i = 0; i < QualitySettings.names.Length; i++)
                if (!(QualitySettings.GetRenderPipelineAssetAt(i) is UniversalRenderPipelineAsset))
                    throw new InvalidOperationException("Quality pipeline is not URP: " + i);

            var screenshots = new List<string>();
            string output = OutputDirectory();
            foreach (EditorBuildSettingsScene entry in EditorBuildSettings.scenes)
            {
                if (!entry.enabled) continue;
                Scene scene = EditorSceneManager.OpenScene(entry.path, OpenSceneMode.Single);
                if (scene.name == "LevelScene")
                {
                    // 只在未保存的核验场景实例化，避免向关卡写入测试角色。
                    AddPreview("Assets/Resourse/Prefabs/Hero/knight.prefab", new Vector3(2f, 0f, 4f));
                    AddPreview("Assets/Resourse/Prefabs/Hero/Mage.prefab", new Vector3(4f, 0f, 6f));
                    AddPreview("Assets/Resourse/Prefabs/Enemy/PinkGhost.prefab", new Vector3(8f, 0f, 4f));
                    AddPreview("Assets/Resourse/Prefabs/Enemy/BlueGhost.prefab", new Vector3(10f, 0f, 6f));
                }
                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
                    {
                        foreach (Material material in renderer.sharedMaterials)
                        {
                            if (material == null || material.shader == null || ShaderUtil.ShaderHasError(material.shader))
                                throw new InvalidOperationException("Invalid renderer material: " + renderer.name);
                            string shader = material.shader.name;
                            if (shader == "Standard" || shader.StartsWith("Legacy Shaders/", StringComparison.Ordinal))
                                throw new InvalidOperationException("Unconverted scene material: " + renderer.name + " / " + shader);
                        }
                    }
                }
                Camera camera = null;
                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    Camera candidate = root.GetComponentInChildren<Camera>();
                    if (candidate != null && candidate.enabled) { camera = candidate; break; }
                }
                if (camera != null && SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null)
                {
                    string screenshot = Path.Combine(output, scene.name + "_URP.png");
                    Capture(camera, screenshot);
                    screenshots.Add(screenshot);
                }
            }
            // 关闭未保存的测试场景，最终回到正式入口。
            EditorSceneManager.OpenScene("Assets/Scenes/Core.unity", OpenSceneMode.Single);
            return new VerificationResult
            {
                passed = true,
                unityVersion = Application.unityVersion,
                urpVersion = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(UniversalRenderPipeline).Assembly).version,
                apiCompatibility = PlayerSettings.GetApiCompatibilityLevel(BuildTargetGroup.Standalone).ToString(),
                scriptingBackend = PlayerSettings.GetScriptingBackend(BuildTargetGroup.Standalone).ToString(),
                graphicsDevice = SystemInfo.graphicsDeviceType.ToString(),
                pipeline = AssetDatabase.GetAssetPath(pipeline),
                qualityLevels = QualitySettings.names.Length,
                changes = Changes.ToArray(),
                screenshots = screenshots.ToArray()
            };
        }

        private static void AddPreview(string path, Vector3 position)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) throw new InvalidOperationException("Missing preview prefab: " + path);
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            foreach (MonoBehaviour behaviour in instance.GetComponentsInChildren<MonoBehaviour>(true)) behaviour.enabled = false;
            instance.transform.position = position;
        }

        private static void Capture(Camera camera, string path)
        {
            var target = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32);
            var pixels = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            RenderTexture previous = RenderTexture.active;
            try
            {
                target.Create();
                RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
                RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
                pixels.Apply();
                File.WriteAllBytes(path, pixels.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previous;
                target.Release();
                UnityEngine.Object.DestroyImmediate(target);
                UnityEngine.Object.DestroyImmediate(pixels);
            }
        }

        private static string OutputDirectory()
        {
            string directory = Environment.GetEnvironmentVariable("MYTD_VALIDATION_OUTPUT");
            if (string.IsNullOrWhiteSpace(directory)) directory = Path.GetFullPath("Logs/UrpMigration");
            Directory.CreateDirectory(directory);
            return directory;
        }

        private static void Finish(VerificationResult result, Exception exception)
        {
            EditorApplication.update -= WaitForPipeline;
            if (result == null) result = new VerificationResult { passed = false, changes = Changes.ToArray(), error = exception == null ? "Unknown failure" : exception.ToString() };
            File.WriteAllText(Path.Combine(OutputDirectory(), "urp-verification.json"), JsonUtility.ToJson(result, true));
            if (exception != null) Debug.LogException(exception);
            Debug.Log("MYTD_URP_VERIFICATION=" + result.passed);
            if (Application.isBatchMode) EditorApplication.Exit(result.passed ? 0 : 1);
        }
    }
}
