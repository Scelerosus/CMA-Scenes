using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Assets._ReusableScripts.CuchiCuchi;
using UnityEngine;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using HerShine = Assets._ReusableScripts.CuchiCuchi.AI.Controladores.ControlladorBrilloPorPlacer;

namespace CMAScenes
{
    public sealed partial class ShakeController
    {
        private void AddWetParts(SkinnedMeshRenderer r)
        {
            Mesh mesh = r.sharedMesh;
            if (!ShaftOf(r, out float[] at, out Vector2[] uv, out float length, out float[] radius, out Vector3[] pts, out float toWorld, out Vector3 shaftDir))
            {
                return;
            }
            m_penisLength = length;
            Array.Copy(radius, m_penisRadius, RadiusBins);
            Plugin.ModLog.LogInfo($"Ride: wet: he is {length * 100f:F0} cm long by his mesh, and {radius[2] * 200f:F1} / {radius[RadiusBins / 2] * 200f:F1} / {radius[RadiusBins - 2] * 200f:F1} cm thick near his base, in his middle and near his tip.");
            var mats = r.sharedMaterials;
            int subs = Fast.Max(1, mesh.subMeshCount);
            for (int slot = 0; mats != null && slot < mats.Length && slot < subs; slot++)
            {
                Material m = mats[slot];
                if (m == null || !m.HasProperty(MaskMapId) || !m.HasProperty(SmoothMaxId))
                {
                    Plugin.ModLog.LogInfo($"Ride: wet: '{(m != null ? m.name : "?")}' has no mask of smoothness to draw on; it is left as it is.");
                    continue;
                }
                float[] along = AlongMap(mesh, slot, at, uv, pts, toWorld, out float density, out float[] pos);
                if (along == null)
                {
                    Plugin.ModLog.LogInfo($"Ride: wet: the UVs of '{m.name}' cover almost nothing; it is left as it is.");
                    continue;
                }
                var part = new WetPart { renderer = r, slot = slot, name = m.name, material = m, along = along, pos = pos, density = density };
                var live = new List<int>();
                for (int i = 0; i < along.Length; i++)
                {
                    if (along[i] >= 0f)
                    {
                        live.Add(i);
                    }
                }
                part.live = live.ToArray();
                part.aLo = 1f;
                part.aHi = 0f;
                foreach (int li in live)
                {
                    part.aLo = Fast.Min(part.aLo, along[li]);
                    part.aHi = Fast.Max(part.aHi, along[li]);
                }
                part.streak = Streaks(part, pts, toWorld, shaftDir, length);
                Plugin.ModLog.LogInfo($"Ride: wet: '{m.name}': its map has {density / 100f:F0} texels to a centimetre of his skin.");
                var own = new MaterialPropertyBlock();
                r.GetPropertyBlock(own, slot);
                part.hadBlock = !own.isEmpty;
                part.maskWas = m.GetTexture(MaskMapId);
                bool masked = part.maskWas != null && m.IsKeywordEnabled(MaskKeyword);
                string how;
                if (masked)
                {
                    part.min0 = m.GetFloat(SmoothMinId);
                    part.max0 = m.GetFloat(SmoothMaxId);
                    part.dry = CopyMask(part.maskWas, false);
                    how = part.dry != null ? "its own mask was copied" : "its own mask could not be copied, an even one stands for it";
                }
                else
                {
                    // No mask of its own: its smoothness is one number, and the mask that is drawn says that everywhere.
                    part.min0 = 0f;
                    part.max0 = 1f;
                    how = "it had no mask, an even one is made for it";
                }
                if (part.dry == null)
                {
                    float smooth = masked ? 0.5f : (m.HasProperty("_Smoothness") ? m.GetFloat("_Smoothness") : 0.5f);
                    float metal = !masked && m.HasProperty("_Metallic") ? m.GetFloat("_Metallic") : 0f;
                    var even = new Color32((byte)(Fast.Clamp01(metal) * 255f), 255, 0, (byte)(Fast.Clamp01(smooth) * 255f));
                    part.dry = new Color32[MaskSize * MaskSize];
                    for (int i = 0; i < part.dry.Length; i++)
                    {
                        part.dry[i] = even;
                    }
                }
                // The top of the range goes up to as smooth as skin gets; what is dry is scaled so that it stays as it was.
                float scale = (part.max0 - part.min0) / Fast.Max(0.01f, 1f - part.min0);
                for (int i = 0; i < part.dry.Length; i++)
                {
                    Color32 c = part.dry[i];
                    c.a = (byte)Fast.Clamp(c.a * scale, 0f, 255f);
                    part.dry[i] = c;
                }
                part.pixels = new Color32[MaskSize * MaskSize];
                Array.Copy(part.dry, part.pixels, part.dry.Length);
                part.mask = new Texture2D(MaskSize, MaskSize, TextureFormat.RGBA32, true, true)
                {
                    filterMode = FilterMode.Trilinear,
                    wrapMode = TextureWrapMode.Repeat,
                    anisoLevel = 4,
                    hideFlags = HideFlags.HideAndDontSave
                };
                // The colour of his skin, for what has a colour of its own (his semen, the condom).
                part.baseWas = m.HasProperty(BaseMapId) ? m.GetTexture(BaseMapId) : null;
                part.skin = part.baseWas != null ? CopyMask(part.baseWas, true) : null;
                if (part.skin != null)
                {
                    part.skinPixels = new Color32[MaskSize * MaskSize];
                    Array.Copy(part.skin, part.skinPixels, part.skin.Length);
                    part.colour = new Texture2D(MaskSize, MaskSize, TextureFormat.RGBA32, true, false)
                    {
                        filterMode = FilterMode.Trilinear,
                        wrapMode = TextureWrapMode.Repeat,
                        anisoLevel = 4,
                        hideFlags = HideFlags.HideAndDontSave
                    };
                }
                // The liquid in relief: its own normal map over his skin's, if this material can be given one.
                try
                {
                    CaptureRelief(part, m);
                }
                catch (Exception ex)
                {
                    part.reliefOk = false;
                    Plugin.ModLog.LogInfo($"Ride: wet: relief: '{m.name}' could not be prepared ({ex.Message}); only gloss is drawn on it.");
                }
                m_wetParts.Add(part);
                Plugin.ModLog.LogInfo($"Ride: wet: '{m.name}' (renderer '{r.name}', slot {slot}): smoothness {part.min0:F2} to {part.max0:F2}, {how}; {(part.skin != null ? "the colour of its skin was copied too" : "the colour of its skin could not be copied: only gloss is drawn on it")}.");
            }
        }

        // A copy of a texture of the game's that can be read, at the size of the masks. Null when it cannot be made.
        private static Color32[] CopyMask(Texture source, bool colour)
        {
            RenderTexture rt = null;
            Texture2D copy = null;
            RenderTexture before = RenderTexture.active;
            try
            {
                rt = RenderTexture.GetTemporary(MaskSize, MaskSize, 0, RenderTextureFormat.ARGB32, colour ? RenderTextureReadWrite.sRGB : RenderTextureReadWrite.Linear);
                Graphics.Blit(source, rt);
                RenderTexture.active = rt;
                copy = new Texture2D(MaskSize, MaskSize, TextureFormat.RGBA32, false, !colour);
                copy.ReadPixels(new Rect(0f, 0f, MaskSize, MaskSize), 0, 0);
                copy.Apply(false, false);
                var read = copy.GetPixels32();
                if (read == null || read.Length != MaskSize * MaskSize)
                {
                    return null;
                }
                var result = new Color32[read.Length];
                for (int i = 0; i < result.Length; i++)
                {
                    result[i] = read[i];
                }
                return result;
            }
            catch (Exception ex)
            {
                Plugin.ModLog.LogInfo("Ride: wet: a map of the game's could not be read (" + ex.Message + ").");
                return null;
            }
            finally
            {
                RenderTexture.active = before;
                if (rt != null)
                {
                    RenderTexture.ReleaseTemporary(rt);
                }
                if (copy != null)
                {
                    UnityEngine.Object.Destroy(copy);
                }
            }
        }

        // Where along the shaft each point of the mesh is, 0 at the base and 1 at the tip: its long axis is the shaft, and
        // the end nearer to the bone that all of its bones hang from is the base. False if the mesh cannot be read or is
        // not one long shape.
        private static bool ShaftOf(SkinnedMeshRenderer source, out float[] at, out Vector2[] uv, out float length, out float[] radius, out Vector3[] points, out float toWorldOut, out Vector3 shaftDir)
        {
            points = null;
            shaftDir = Vector3.up;
            toWorldOut = 1f;
            radius = new float[RadiusBins];
            for (int i = 0; i < RadiusBins; i++)
            {
                radius[i] = 0.019f;
            }
            at = null;
            uv = null;
            length = DefaultPenisLength;
            try
            {
                Mesh mesh = source.sharedMesh;
                if (mesh == null || !mesh.isReadable)
                {
                    Plugin.ModLog.LogInfo("Ride: wet: the mesh of his penis cannot be read; nothing is made wet.");
                    return false;
                }
                var vs = mesh.vertices;
                var uvs = mesh.uv;
                int n = vs.Length;
                if (n < 8 || uvs == null || uvs.Length != n)
                {
                    Plugin.ModLog.LogInfo("Ride: wet: the mesh of his penis has no UVs to draw by; nothing is made wet.");
                    return false;
                }
                var pts = new Vector3[n];
                uv = new Vector2[n];
                for (int i = 0; i < n; i++)
                {
                    pts[i] = vs[i];
                    uv[i] = uvs[i];
                }
                if (!LongAxis(pts, out Vector3 mean, out Vector3 axis, out float lo, out float hi))
                {
                    return false;
                }
                // Which end is the base: the one nearer the bone the others hang from.
                var bones = source.bones;
                var binds = mesh.bindposes;
                int baseBone = -1, baseDepth = int.MaxValue;
                for (int b = 0; bones != null && binds != null && b < bones.Length && b < binds.Length; b++)
                {
                    if (bones[b] == null)
                    {
                        continue;
                    }
                    int depth = 0;
                    for (Transform t = bones[b].parent; t != null; t = t.parent)
                    {
                        depth++;
                    }
                    if (depth < baseDepth)
                    {
                        baseDepth = depth;
                        baseBone = b;
                    }
                }
                if (baseBone < 0)
                {
                    Plugin.ModLog.LogInfo("Ride: wet: his penis has no bones to tell its base from its tip; nothing is made wet.");
                    return false;
                }
                Vector3 basePoint = binds[baseBone].inverse.MultiplyPoint3x4(Vector3.zero);
                float pb = Vector3.Dot(basePoint - mean, axis);
                bool baseLow = Fast.Abs(pb - lo) <= Fast.Abs(pb - hi);
                // (From his base to his tip, in the mesh's own frame: which way the film runs along him.)
                shaftDir = baseLow ? axis : -axis;
                float span = hi - lo;
                at = new float[n];
                for (int i = 0; i < n; i++)
                {
                    float p = Vector3.Dot(pts[i] - mean, axis);
                    at[i] = baseLow ? (p - lo) / span : (hi - p) / span;
                }
                Vector3 scale = source.transform.lossyScale;
                float measured = span * Fast.Max(Fast.Abs(scale.x), Fast.Abs(scale.y), Fast.Abs(scale.z));
                length = measured >= 0.06f && measured <= 0.35f ? measured : DefaultPenisLength;
                // How thick he is at each place along him: how far his skin is from his middle line there, on average.
                var sum = new float[RadiusBins];
                var cnt = new int[RadiusBins];
                for (int i = 0; i < n; i++)
                {
                    Vector3 d = pts[i] - mean;
                    float off = (d - axis * Vector3.Dot(d, axis)).magnitude;
                    int bin = Fast.Clamp((int)(at[i] * RadiusBins), 0, RadiusBins - 1);
                    sum[bin] += off;
                    cnt[bin]++;
                }
                float toWorld = length / Fast.Max(1e-5f, span);
                for (int i = 0; i < RadiusBins; i++)
                {
                    radius[i] = cnt[i] > 0 ? Fast.Clamp(sum[i] / cnt[i] * toWorld * 1.12f, 0.01f, 0.035f) : -1f;
                }
                for (int i = 0; i < RadiusBins; i++)
                {
                    if (radius[i] < 0f)
                    {
                        radius[i] = i > 0 ? radius[i - 1] : 0.019f;
                    }
                }
                // (Near his base the mesh has more than the shaft in it: no thicker there than he is further up.)
                for (int i = RadiusBins / 3; i >= 0; i--)
                {
                    radius[i] = Fast.Min(radius[i], radius[i + 1] * 1.05f);
                }
                points = pts;
                toWorldOut = toWorld;
                return true;
            }
            catch (Exception ex)
            {
                Plugin.ModLog.LogInfo("Ride: wet: where the wet goes could not be worked out (" + ex.Message + ").");
                return false;
            }
        }

        // For one part of the mesh (one material): where on the shaft each texel of its mask lies. Every triangle of the
        // part is drawn into the map at its UVs with that value. Null if its UVs cover almost nothing.
        private static float[] AlongMap(Mesh mesh, int sub, float[] at, Vector2[] uv, Vector3[] pts, float toWorld, out float density, out float[] pos)
        {
            const int size = MaskSize;
            // How many texels of the map there are to a metre of his skin: from how big its triangles are in both.
            density = 3000f;
            pos = null;
            var where = new float[size * size * 3];
            float inMap = 0f, onSkin = 0f;
            var tris = mesh.GetTriangles(sub);
            if (tris == null || tris.Length < 3)
            {
                return null;
            }
            int n = at.Length;
            var map = new float[size * size];
            for (int i = 0; i < map.Length; i++)
            {
                map[i] = -1f;
            }
            int filled = 0;
            for (int t = 0; t + 2 < tris.Length; t += 3)
            {
                int i0 = tris[t], i1 = tris[t + 1], i2 = tris[t + 2];
                if (i0 >= n || i1 >= n || i2 >= n)
                {
                    continue;
                }
                Vector2 q0 = uv[i0] * size, q1 = uv[i1] * size, q2 = uv[i2] * size;
                float area = (q1.x - q0.x) * (q2.y - q0.y) - (q2.x - q0.x) * (q1.y - q0.y);
                if (Fast.Abs(area) < 1e-8f)
                {
                    continue;
                }
                inMap += Fast.Abs(area) * 0.5f;
                onSkin += 0.5f * Vector3.Cross(pts[i1] - pts[i0], pts[i2] - pts[i0]).magnitude * toWorld * toWorld;
                int x0 = Mathf.FloorToInt(Fast.Min(q0.x, Fast.Min(q1.x, q2.x)));
                int x1 = Mathf.CeilToInt(Fast.Max(q0.x, Fast.Max(q1.x, q2.x)));
                int y0 = Mathf.FloorToInt(Fast.Min(q0.y, Fast.Min(q1.y, q2.y)));
                int y1 = Mathf.CeilToInt(Fast.Max(q0.y, Fast.Max(q1.y, q2.y)));
                if (x1 - x0 > size || y1 - y0 > size)
                {
                    continue;
                }
                for (int y = y0; y <= y1; y++)
                {
                    for (int x = x0; x <= x1; x++)
                    {
                        float px = x + 0.5f, py = y + 0.5f;
                        float w1 = ((px - q0.x) * (q2.y - q0.y) - (q2.x - q0.x) * (py - q0.y)) / area;
                        float w2 = ((q1.x - q0.x) * (py - q0.y) - (px - q0.x) * (q1.y - q0.y)) / area;
                        float w0 = 1f - w1 - w2;
                        if (w0 < -0.01f || w1 < -0.01f || w2 < -0.01f)
                        {
                            continue;
                        }
                        int idx = ((y % size + size) % size) * size + ((x % size + size) % size);
                        if (map[idx] < 0f)
                        {
                            filled++;
                        }
                        map[idx] = w0 * at[i0] + w1 * at[i1] + w2 * at[i2];
                        Vector3 p3 = (pts[i0] * w0 + pts[i1] * w1 + pts[i2] * w2) * toWorld;
                        where[idx * 3] = p3.x;
                        where[idx * 3 + 1] = p3.y;
                        where[idx * 3 + 2] = p3.z;
                    }
                }
            }
            if (filled < 64)
            {
                return null;
            }
            if (onSkin > 1e-9f)
            {
                density = Fast.Clamp(Fast.Sqrt(inMap / onSkin), 300f, 30000f);
            }
            // Grown a few texels past the edges of the layout, so no dry seam shows where the texture is filtered.
            for (int pass = 0; pass < 6; pass++)
            {
                var grown = (float[])map.Clone();
                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        int idx = y * size + x;
                        if (map[idx] >= 0f)
                        {
                            continue;
                        }
                        float sum = 0f, sx = 0f, sy = 0f, sz = 0f;
                        int cnt = 0;
                        for (int k = 0; k < 4; k++)
                        {
                            int nx = (x + (k == 0 ? 1 : k == 1 ? size - 1 : 0)) % size;
                            int ny = (y + (k == 2 ? 1 : k == 3 ? size - 1 : 0)) % size;
                            int ni = ny * size + nx;
                            float m = map[ni];
                            if (m >= 0f)
                            {
                                sum += m;
                                sx += where[ni * 3];
                                sy += where[ni * 3 + 1];
                                sz += where[ni * 3 + 2];
                                cnt++;
                            }
                        }
                        if (cnt > 0)
                        {
                            grown[idx] = sum / cnt;
                            where[idx * 3] = sx / cnt;
                            where[idx * 3 + 1] = sy / cnt;
                            where[idx * 3 + 2] = sz / cnt;
                        }
                    }
                }
                map = grown;
            }
            pos = where;
            return map;
        }

        // The smooth bands of the film along the shaft (WetPart.streak), for one part: a noise that goes round him
        // (seamless, as a circle in the noise) in some twelve bands and changes along him only every few centimetres,
        // so that it is long and soft, never a dot.
        private static float[] Streaks(WetPart p, Vector3[] pts, float toWorld, Vector3 shaftDir, float length)
        {
            var streak = new float[MaskSize * MaskSize];
            if (p.pos == null || pts == null || pts.Length == 0)
            {
                return streak;
            }
            Vector3 centre = Vector3.zero;
            for (int i = 0; i < pts.Length; i++)
            {
                centre += pts[i];
            }
            centre *= toWorld / pts.Length;
            Vector3 axis = shaftDir.sqrMagnitude > 1e-8f ? shaftDir.normalized : Vector3.up;
            Vector3 u = Vector3.Cross(axis, Fast.Abs(axis.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
            Vector3 v = Vector3.Cross(axis, u);
            foreach (int i in p.live)
            {
                Vector3 d = new Vector3(p.pos[i * 3], p.pos[i * 3 + 1], p.pos[i * 3 + 2]) - centre;
                float ang = Mathf.Atan2(Vector3.Dot(d, v), Vector3.Dot(d, u));
                float along = Fast.Max(0f, p.along[i]) * length / 0.04f;
                float n = Mathf.PerlinNoise(Mathf.Cos(ang) * 2f + 31.7f, Mathf.Sin(ang) * 2f + 11.3f + along);
                streak[i] = Soft((n - 0.38f) / 0.3f);
            }
            return streak;
        }
    }
}
