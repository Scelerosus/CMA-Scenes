using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Assets.TValle.Tools.Runtime.Moddding.Clothing.Maps;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace CMAScenes
{
    // Only shared garment meshes are copied. No prefab scripts, bodies or live wardrobe are instantiated.
    internal static class ClothingPreviews
    {
        private sealed class Request { internal ClothingItemMap Map; internal Dictionary<int, string> Variants; internal Color Tint; internal bool ApplyTint, Solid; }
        private static readonly PreviewJobs<Request> jobs = new();
        private static readonly Dictionary<string, Texture2D> cache = new();
        private static readonly List<Material> materials = new();
        private static readonly Queue<string> order = new();
        private static readonly string directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CMAScenes", "clothing-previews");
        private static AsyncOperationHandle<GameObject> handle;
        private static readonly Dictionary<int, AsyncOperationHandle<Material>> materialHandles = new();
        private static Dictionary<int, Material> loadedMaterials = new();
        private static Dictionary<int, string> currentVariants;
        private static Color currentTint = Color.white;
        private static bool applyTint;
        private static bool currentSolid;
        private static bool loading;
        private static int rendered;
        private static string current;
        private static float started;
        private static GameObject stage, cameraObject;
        private static Camera camera;
        private static RenderTexture target;
        private static VolumeProfile profile;
        private static Exposure exposure;
        private static Il2CppSystem.Action<ScriptableRenderContext, Camera> callback;

        internal static string Key(ClothingItemMap map, Dictionary<int, string> variants = null, Color? tint = null, bool solid = false)
        {
            if (map == null || map.address == null || !map.address.RuntimeKeyIsValid()) return null;
            string materialsKey = string.Join(";", DefaultVariants(map, variants).OrderBy(x => x.Key).Select(x => x.Key + "=" + x.Value));
            Color color = tint ?? Color.white;
            return ClothingCardInfo.CacheKey(map.id, map.address.RuntimeKey.ToString(), (map.version?.ToString() ?? "") + "|preview2|" + materialsKey + "|" + (tint.HasValue ? ColorUtility.ToHtmlStringRGBA(color) : "native") + "|" + (solid ? "solid" : "pattern"));
        }
        internal static Texture2D Get(string key, ClothingItemMap map, Dictionary<int, string> variants = null, Color? tint = null, bool solid = false)
        {
            if (key == null) return null;
            if (cache.TryGetValue(key, out var texture)) return texture;
            jobs.Request(key, new Request { Map = map, Variants = DefaultVariants(map, variants), Tint = tint ?? Color.white, ApplyTint = tint.HasValue, Solid = solid });
            return null;
        }

        private static Dictionary<int, string> DefaultVariants(ClothingItemMap map, Dictionary<int, string> selected = null)
        {
            var result = new Dictionary<int, string>();
            if (map?.materialsPerIndex != null) foreach (var group in map.materialsPerIndex)
            {
                if (group?.materials == null || group.materialIndex < 0) continue;
                string requested = null;
                selected?.TryGetValue(group.materialIndex, out requested);
                for (int i = 0; i < group.materials.Count; i++)
                {
                    string id = group.materials[i]?.id;
                    if (string.IsNullOrWhiteSpace(id)) continue;
                    if (!result.ContainsKey(group.materialIndex)) result[group.materialIndex] = id;
                    if (id == requested) { result[group.materialIndex] = id; break; }
                }
            }
            return result;
        }

        internal static void Step()
        {
            if (!Menu.IsOpen) { Release(); return; }
            try
            {
                if (current != null)
                {
                    if (Time.realtimeSinceStartup - started > 20f) { Finish(); return; }
                    if (loading)
                    {
                        if (!handle.IsDone || materialHandles.Values.Any(h => !h.IsDone)) return;
                        loading = false;
                        if (handle.Status != AsyncOperationStatus.Succeeded || handle.Result == null) { Finish(); return; }
                        foreach (var pair in materialHandles) if (pair.Value.Status == AsyncOperationStatus.Succeeded && pair.Value.Result != null) loadedMaterials[pair.Key] = pair.Value.Result;
                        CreateStage(handle.Result);
                        return;
                    }
                    if (rendered < 2) return;
                    camera.enabled = false;
                    var previous = RenderTexture.active;
                    Texture2D texture = null;
                    try
                    {
                        RenderTexture.active = target;
                        texture = new Texture2D(384, 384, TextureFormat.RGBA32, false);
                        texture.ReadPixels(new Rect(0, 0, 384, 384), 0, 0);
                        texture.Apply();
                        Add(current, texture); texture = null;
                        try { Directory.CreateDirectory(directory); File.WriteAllBytes(Path.Combine(directory, current + ".png"), ImageConversion.EncodeToPNG(cache[current]).ToArray()); }
                        catch (Exception ex) { Plugin.ModLog.LogWarning("Preview cache: " + ex.Message); }
                    }
                    finally { RenderTexture.active = previous; if (texture != null) UnityEngine.Object.Destroy(texture); }
                    Finish(); return;
                }
                if (!jobs.Take(out string key, out var request)) return;
                if (LoadCached(key)) return;
                var map = request?.Map;
                if (map == null || map.address == null || !map.address.RuntimeKeyIsValid()) return;
                current = key; started = Time.realtimeSinceStartup;
                currentVariants = request.Variants ?? new Dictionary<int, string>(); currentTint = request.Tint; applyTint = request.ApplyTint; currentSolid = request.Solid; loadedMaterials = new Dictionary<int, Material>();
                handle = Addressables.LoadAssetAsync<GameObject>(map.address.RuntimeKey);
                if (map.materialsPerIndex != null) foreach (var group in map.materialsPerIndex)
                {
                    if (group?.materials == null || !currentVariants.TryGetValue(group.materialIndex, out string id)) continue;
                    for (int i = 0; i < group.materials.Count; i++)
                    {
                        var option = group.materials[i];
                        if (option == null || option.id != id || option.materialAddress == null || !option.materialAddress.RuntimeKeyIsValid()) continue;
                        materialHandles[group.materialIndex] = Addressables.LoadAssetAsync<Material>(option.materialAddress.RuntimeKey); break;
                    }
                }
                loading = true;
            }
            catch (Exception ex) { Plugin.ModLog.LogWarning("Clothing preview [" + current + "]: " + ex); Finish(); }
        }

        private static bool LoadCached(string key)
        {
            Texture2D texture = null;
            try
            {
                string path = Path.Combine(directory, key + ".png");
                if (!File.Exists(path) || new FileInfo(path).Length > 8 * 1024 * 1024) return false;
                byte[] data = File.ReadAllBytes(path);
                if (!ClothingCardInfo.SafePng(data)) return false;
                texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (!ImageConversion.LoadImage(texture, new Il2CppStructArray<byte>(data), false) || texture.width != 384 || texture.height != 384) return false;
                Add(key, texture); texture = null; return true;
            }
            catch (Exception ex) { Plugin.ModLog.LogWarning("Preview read: " + ex.Message); return false; }
            finally { if (texture != null) UnityEngine.Object.Destroy(texture); }
        }

        private static void Add(string key, Texture2D texture)
        {
            cache.Add(key, texture); order.Enqueue(key);
            while (order.Count > 96)
            {
                string oldest = order.Dequeue();
                UnityEngine.Object.Destroy(cache[oldest]); cache.Remove(oldest); jobs.Forget(oldest);
            }
        }

        private static int FreeLayer()
        {
            int used = 0;
            foreach (var renderer in UnityEngine.Object.FindObjectsOfType<Renderer>()) used |= 1 << renderer.gameObject.layer;
            for (int i = 31; i >= 24; i--) if ((used & (1 << i)) == 0) return i;
            throw new InvalidOperationException("No unused preview layer available.");
        }

        private static Material PreviewMaterial(Material source, Shader shader, int slot)
        {
            if (loadedMaterials.TryGetValue(slot, out var selected)) source = selected;
            var material = new Material(shader); materials.Add(material);
            if (source == null) return material;
            if (!currentSolid) foreach (string property in new[] { "_BaseColorMap", "_BaseMap", "_MainTex" })
                if (source.HasProperty(property) && source.GetTexture(property) != null)
                {
                    material.SetTexture("_UnlitColorMap", source.GetTexture(property));
                    material.SetTextureScale("_UnlitColorMap", source.GetTextureScale(property));
                    material.SetTextureOffset("_UnlitColorMap", source.GetTextureOffset(property)); break;
                }
            Color color = source.HasProperty("_BaseColor") ? source.GetColor("_BaseColor") : source.HasProperty("_Color") ? source.GetColor("_Color") : Color.white;
            material.SetColor("_UnlitColor", applyTint ? currentTint : color);
            material.SetFloat("_CullMode", 0); material.SetFloat("_CullModeForward", 0);
            material.SetFloat("_AlphaCutoffEnable", 1); material.SetFloat("_AlphaCutoff", source.HasProperty("_AlphaCutoff") ? source.GetFloat("_AlphaCutoff") : 0.1f);
            material.EnableKeyword("_ALPHATEST_ON");
            return material;
        }

        private static void CreateStage(GameObject prefab)
        {
            var shader = Shader.Find("HDRP/Unlit");
            if (shader == null) throw new InvalidOperationException("HDRP preview shader unavailable.");
            int layer = FreeLayer();
            stage = new GameObject("CMAScenes garment preview"); stage.layer = layer;
            stage.transform.position = new Vector3(0, -10000, 0);
            bool haveBounds = false; Bounds bounds = default;
            // GetLODs is stripped from this game's interop API; do not flatten all LODs into one cached image.
            // Some players strip the LODGroup class itself. If absent, their prefabs cannot contain it.
            if (Il2CppClassPointerStore<LODGroup>.NativeClassPtr != IntPtr.Zero &&
                prefab.GetComponentsInChildren(Il2CppType.Of<LODGroup>(), true).Length > 0)
                throw new InvalidOperationException("LOD clothing preview is unsupported by the installed API.");
            // IL2CPP player strips uninstantiated generic query methods. Use the native Type overload.
            foreach (var component in prefab.GetComponentsInChildren(Il2CppType.Of<Renderer>(), true))
            {
                var source = component.Cast<Renderer>();
                if (!source.enabled) continue;
                bool active = true;
                for (Transform parent = source.transform; parent != null && parent != prefab.transform; parent = parent.parent)
                    if (!parent.gameObject.activeSelf) { active = false; break; }
                if (!active) continue;
                Mesh mesh = null;
                var skinned = source.TryCast<SkinnedMeshRenderer>();
                if (skinned != null) mesh = skinned.sharedMesh;
                else { var filter = source.gameObject.GetComponent(Il2CppType.Of<MeshFilter>())?.TryCast<MeshFilter>(); if (filter != null) mesh = filter.sharedMesh; }
                if (mesh == null || mesh.vertexCount == 0) continue;
                var obj = new GameObject("Garment mesh"); obj.layer = layer; obj.transform.SetParent(stage.transform, false);
                obj.transform.localPosition = prefab.transform.InverseTransformPoint(source.transform.position);
                obj.transform.localRotation = Quaternion.Inverse(prefab.transform.rotation) * source.transform.rotation;
                Vector3 rootScale = prefab.transform.lossyScale, scale = source.transform.lossyScale;
                if (rootScale.x == 0 || rootScale.y == 0 || rootScale.z == 0) throw new InvalidOperationException("Garment root scale is zero.");
                obj.transform.localScale = new Vector3(scale.x / rootScale.x, scale.y / rootScale.y, scale.z / rootScale.z);
                obj.AddComponent(Il2CppType.Of<MeshFilter>()).Cast<MeshFilter>().sharedMesh = mesh;
                var renderer = obj.AddComponent(Il2CppType.Of<MeshRenderer>()).Cast<MeshRenderer>();
                var original = source.sharedMaterials;
                var copies = new Il2CppReferenceArray<Material>(original.Length);
                for (int i = 0; i < copies.Length; i++) copies[i] = PreviewMaterial(original[i], shader, i);
                renderer.sharedMaterials = copies;
                renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
                if (!haveBounds) { bounds = renderer.bounds; haveBounds = true; } else bounds.Encapsulate(renderer.bounds);
            }
            if (!haveBounds || !float.IsFinite(bounds.size.magnitude) || bounds.size.magnitude <= 0 || bounds.size.magnitude > 100)
                throw new InvalidOperationException("Garment mesh bounds unavailable.");
            cameraObject = new GameObject("CMAScenes preview camera"); cameraObject.layer = layer;
            camera = cameraObject.AddComponent(Il2CppType.Of<Camera>()).Cast<Camera>(); camera.enabled = false;
            camera.cullingMask = 1 << layer; camera.orthographic = true; camera.orthographicSize = bounds.extents.magnitude * 1.12f;
            camera.nearClipPlane = 0.01f; camera.farClipPlane = 200;
            camera.transform.position = bounds.center + new Vector3(0, 0, Mathf.Max(2, bounds.size.magnitude * 2));
            camera.transform.LookAt(bounds.center);
            var hd = cameraObject.AddComponent(Il2CppType.Of<HDAdditionalCameraData>()).Cast<HDAdditionalCameraData>();
            hd.clearColorMode = HDAdditionalCameraData.ClearColorMode.Color; hd.backgroundColorHDR = new Color(0.12f, 0.12f, 0.15f, 1);
            hd.volumeLayerMask = 1 << layer; hd.volumeAnchorOverride = camera.transform;
            var volume = cameraObject.AddComponent(Il2CppType.Of<Volume>()).Cast<Volume>(); volume.isGlobal = false; volume.weight = 1; volume.priority = 1000;
            var collider = cameraObject.AddComponent(Il2CppType.Of<BoxCollider>()).Cast<BoxCollider>(); collider.isTrigger = true; collider.size = Vector3.one * 2;
            profile = ScriptableObject.CreateInstance(Il2CppType.Of<VolumeProfile>()).Cast<VolumeProfile>();
            exposure = profile.Add(Il2CppType.Of<Exposure>(), true).Cast<Exposure>(); exposure.mode.value = ExposureMode.Fixed; exposure.fixedExposure.value = 0;
            volume.sharedProfile = profile;
            target = new RenderTexture(384, 384, 24, RenderTextureFormat.ARGB32);
            if (!target.Create()) throw new InvalidOperationException("Preview render target unavailable.");
            camera.targetTexture = target;
            callback = DelegateSupport.ConvertDelegate<Il2CppSystem.Action<ScriptableRenderContext, Camera>>(new Action<ScriptableRenderContext, Camera>((context, cam) => { if (camera != null && cam == camera) rendered++; }));
            RenderPipelineManager.add_endCameraRendering(callback);
            camera.enabled = true;
        }

        private static void Finish()
        {
            if (camera != null) { camera.enabled = false; camera.targetTexture = null; }
            if (callback != null) { RenderPipelineManager.remove_endCameraRendering(callback); callback = null; }
            if (cameraObject != null) UnityEngine.Object.Destroy(cameraObject);
            if (stage != null) UnityEngine.Object.Destroy(stage);
            if (target != null) { target.Release(); UnityEngine.Object.Destroy(target); }
            if (exposure != null) UnityEngine.Object.Destroy(exposure);
            if (profile != null) UnityEngine.Object.Destroy(profile);
            foreach (var material in materials) UnityEngine.Object.Destroy(material); materials.Clear();
            if (current != null && handle.IsValid()) Addressables.Release(handle);
            foreach (var mh in materialHandles.Values) if (mh.IsValid()) Addressables.Release(mh); materialHandles.Clear(); loadedMaterials.Clear(); currentVariants = null;
            camera = null; cameraObject = null; stage = null; target = null; profile = null; exposure = null; applyTint = false; currentSolid = false;
            handle = default; current = null; loading = false; rendered = 0;
        }

        internal static void Release()
        {
            Finish(); jobs.Clear();
            foreach (var texture in cache.Values) UnityEngine.Object.Destroy(texture);
            cache.Clear(); order.Clear();
        }
    }
}
