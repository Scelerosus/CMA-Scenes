using System;
using System.Text.RegularExpressions;
using Assets._ReusableScripts.CuchiCuchi;
using UnityEngine;

namespace CMAScenes
{
    // Where the hero's penis really is, and where her vagina is: what the ride lines up, and how deep he is in her.
    //
    // His: when the ride takes him up, his penis mesh is baked once as it is drawn at that moment, and its long axis is
    // found: the end nearer the bone all of its bones hang from is the base, the other the tip. Each end is then kept in
    // the frame of the bone nearest to it, so it follows him wherever he is carried and however the game bends him.
    // Hers: a bone of hers named for it, if her rig has one; otherwise a point a little below the middle of her hip joints.
    public sealed partial class ShakeController
    {
        private static readonly Regex PenisName = new Regex("penis|(?<![a-z])pene|(?<![a-z])dick|(?<![a-z])shaft", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex VulvaName = new Regex("vagin|vulv|pussy|labia|labio", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex VulvaBest = new Regex("hole|entr|open|orif|agujero", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        // The penis: its two ends in the frames of the bones that carry them, and the same ends where he stood when the
        // ride took him up (for laying him down so that it is under her).
        private Transform m_penisBaseBone, m_penisTipBone;
        private Vector3 m_penisBaseLocal, m_penisTipLocal;
        private Vector3 m_penisHomeBase, m_penisHomeTip;
        private bool m_penisValid;

        // Below the middle of her hip joints, where her vagina is when her rig has no bone for it.
        private const float VulvaBelowHips = 0.06f;

        private void PenisForget()
        {
            m_penisValid = false;
            m_penisBaseBone = m_penisTipBone = null;
        }

        // Finds his penis and measures it, as he stands now. Called once for each ride, before he is laid down.
        private void PenisCapture(Character hero)
        {
            PenisForget();
            try
            {
                SkinnedMeshRenderer best = null;
                int bestCount = 0;
                Component[] places = { hero, hero.bodyAnimator };
                foreach (Component place in places)
                {
                    if (place == null)
                    {
                        continue;
                    }
                    var all = place.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                    if (all == null)
                    {
                        continue;
                    }
                    foreach (SkinnedMeshRenderer r in all)
                    {
                        if (r == null || r.sharedMesh == null || !PenisName.IsMatch(r.name) || WetNotSkin.IsMatch(r.name) || r.name.StartsWith("CMAScenes"))
                        {
                            continue;
                        }
                        int count = r.sharedMesh.vertexCount;
                        if (count > bestCount)
                        {
                            best = r;
                            bestCount = count;
                        }
                    }
                }
                if (best == null)
                {
                    Plugin.ModLog.LogInfo("Ride: penis: no penis mesh found on him; he is lined up by his hips.");
                    return;
                }

                // The mesh as it is drawn now, in the world. Whether the baked points have the renderer's scale in them or
                // not is told by their size: the mesh's own size times its scale is how big it is in the world. (Its model
                // is in centimetres with a scale of 0.01: taken the wrong way, he came out 22 m long.)
                var baked = new Mesh();
                best.BakeMesh(baked, true);
                var vs = baked.vertices;
                int n = vs.Length;
                if (n < 8)
                {
                    UnityEngine.Object.Destroy(baked);
                    return;
                }
                Transform rt = best.transform;
                Vector3 scale = rt.lossyScale;
                float bakedSize = baked.bounds.size.magnitude;
                float worldSize = Vector3.Scale(best.sharedMesh.bounds.size, scale).magnitude;
                float scaleSize = (Mathf.Abs(scale.x) + Mathf.Abs(scale.y) + Mathf.Abs(scale.z)) / 3f;
                UnityEngine.Object.Destroy(baked);
                // As they are (scale already in them), or scaled by the renderer: whichever comes out nearer its true size.
                bool scaleIn = Mathf.Abs(Mathf.Log(Mathf.Max(1e-6f, bakedSize) / Mathf.Max(1e-6f, worldSize)))
                    <= Mathf.Abs(Mathf.Log(Mathf.Max(1e-6f, bakedSize * scaleSize) / Mathf.Max(1e-6f, worldSize)));
                var pts = new Vector3[n];
                for (int i = 0; i < n; i++)
                {
                    pts[i] = scaleIn ? rt.position + rt.rotation * vs[i] : rt.TransformPoint(vs[i]);
                }

                if (!LongAxis(pts, out Vector3 mean, out Vector3 axis, out float lo, out float hi))
                {
                    return;
                }
                var bones = best.bones;
                Transform root = null;
                int rootDepth = int.MaxValue;
                for (int b = 0; bones != null && b < bones.Length; b++)
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
                    if (depth < rootDepth)
                    {
                        rootDepth = depth;
                        root = bones[b];
                    }
                }
                if (root == null)
                {
                    Plugin.ModLog.LogInfo("Ride: penis: its mesh has no bones; he is lined up by his hips.");
                    return;
                }
                float pr = Vector3.Dot(root.position - mean, axis);
                bool baseLow = Mathf.Abs(pr - lo) <= Mathf.Abs(pr - hi);
                Vector3 a = mean + axis * lo, b2 = mean + axis * hi;
                Vector3 baseW = baseLow ? a : b2;
                Vector3 tipW = baseLow ? b2 : a;

                float length = (tipW - baseW).magnitude;
                // And it grows from his crotch: a base far from his hips is a measuring gone wrong. (His hips as they
                // are now: the place the ride remembers for them is there only during a ride, and without it every
                // measuring made outside the ride was thrown away.)
                Vector3 hipsNow = m_heroHomeHips;
                Animator heroAnim = hero.bodyAnimator;
                Transform hipsBone = heroAnim != null && heroAnim.isHuman ? heroAnim.GetBoneTransform(HumanBodyBones.Hips) : null;
                if (hipsBone != null)
                {
                    hipsNow = hipsBone.position;
                }
                if (length < 0.03f || length > 0.4f || (baseW - hipsNow).magnitude > 0.35f)
                {
                    Plugin.ModLog.LogInfo($"Ride: penis: '{best.name}' measured {length * 100f:F0} cm with its base {(baseW - hipsNow).magnitude * 100f:F0} cm from his hips, which cannot be right; he is lined up by his hips.");
                    return;
                }
                m_penisBaseBone = NearestBone(bones, baseW);
                m_penisTipBone = NearestBone(bones, tipW);
                if (m_penisBaseBone == null || m_penisTipBone == null)
                {
                    return;
                }
                m_penisBaseLocal = m_penisBaseBone.InverseTransformPoint(baseW);
                m_penisTipLocal = m_penisTipBone.InverseTransformPoint(tipW);
                m_penisHomeBase = baseW;
                m_penisHomeTip = tipW;
                m_penisValid = true;
                Plugin.ModLog.LogInfo($"Ride: penis: '{best.name}', {length * 100f:F0} cm from base to tip (base on '{m_penisBaseBone.name}', tip on '{m_penisTipBone.name}'; baked points {(scaleIn ? "already scaled" : "scaled here")}), base at {baseW.ToString("F2")}, tip at {tipW.ToString("F2")}.");
            }
            catch (Exception ex)
            {
                PenisForget();
                Plugin.ModLog.LogWarning("Ride: penis could not be measured: " + ex.Message);
            }
        }

        private static Transform NearestBone(Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppReferenceArray<Transform> bones, Vector3 p)
        {
            Transform best = null;
            float bestD = float.MaxValue;
            for (int i = 0; bones != null && i < bones.Length; i++)
            {
                Transform t = bones[i];
                if (t == null)
                {
                    continue;
                }
                float d = (t.position - p).sqrMagnitude;
                if (d < bestD)
                {
                    bestD = d;
                    best = t;
                }
            }
            return best;
        }

        // The main direction a set of points spreads along, and how far they reach along it either side of their middle.
        private static bool LongAxis(Vector3[] pts, out Vector3 mean, out Vector3 axis, out float lo, out float hi)
        {
            int n = pts.Length;
            mean = Vector3.zero;
            for (int i = 0; i < n; i++)
            {
                mean += pts[i];
            }
            mean /= n;
            float xx = 0f, xy = 0f, xz = 0f, yy = 0f, yz = 0f, zz = 0f;
            for (int i = 0; i < n; i++)
            {
                Vector3 d = pts[i] - mean;
                xx += d.x * d.x; xy += d.x * d.y; xz += d.x * d.z;
                yy += d.y * d.y; yz += d.y * d.z; zz += d.z * d.z;
            }
            axis = new Vector3(1f, 0.7f, 0.4f).normalized;
            lo = hi = 0f;
            for (int k = 0; k < 24; k++)
            {
                axis = new Vector3(xx * axis.x + xy * axis.y + xz * axis.z, xy * axis.x + yy * axis.y + yz * axis.z, xz * axis.x + yz * axis.y + zz * axis.z);
                if (axis.sqrMagnitude < 1e-20f)
                {
                    return false;
                }
                axis.Normalize();
            }
            lo = float.MaxValue;
            hi = float.MinValue;
            for (int i = 0; i < n; i++)
            {
                float p = Vector3.Dot(pts[i] - mean, axis);
                lo = Mathf.Min(lo, p);
                hi = Mathf.Max(hi, p);
            }
            return hi - lo > 1e-4f;
        }

        // His penis now: base and tip in the world.
        // His penis as it was at the end of the last frame, after the game had done all it does to him (its own posing of
        // his hips comes after this plugin's turn in the frame: read before it, his penis is where it is not drawn).
        private Vector3 m_penisSeenBase, m_penisSeenTip;
        private int m_penisSeenFrame = -10;

        private bool PenisNow(out Vector3 baseW, out Vector3 tipW)
        {
            if (Time.frameCount - m_penisSeenFrame <= 2)
            {
                baseW = m_penisSeenBase;
                tipW = m_penisSeenTip;
                return (tipW - baseW).sqrMagnitude > 1e-4f;
            }
            return PenisLive(out baseW, out tipW);
        }

        private void PenisSee()
        {
            if (PenisLive(out Vector3 b, out Vector3 t))
            {
                m_penisSeenBase = b;
                m_penisSeenTip = t;
                m_penisSeenFrame = Time.frameCount;
            }
        }

        private bool PenisLive(out Vector3 baseW, out Vector3 tipW)
        {
            baseW = tipW = Vector3.zero;
            if (!m_penisValid || m_penisBaseBone == null || m_penisTipBone == null)
            {
                return false;
            }
            baseW = m_penisBaseBone.TransformPoint(m_penisBaseLocal);
            tipW = m_penisTipBone.TransformPoint(m_penisTipLocal);
            return (tipW - baseW).sqrMagnitude > 1e-4f;
        }

        // Which of her holes the ride is set to.
        internal static bool AnalNow()
        {
            return string.Equals(Plugin.RideHole.Value, "Anus", StringComparison.OrdinalIgnoreCase);
        }

        // Her entrance (the hole the ride is set to): the place the game itself goes by when it was found; else her
        // vagina's bone, looked for once; else a little below the middle of her hip joints.
        private static Vector3 VulvaOf(Woman w)
        {
            if (!w.vulvaSearched)
            {
                w.vulvaSearched = true;
                try
                {
                    Transform best = null;
                    int bestScore = int.MinValue;
                    foreach (Transform t in w.character.GetComponentsInChildren<Transform>(true))
                    {
                        if (t == null || !VulvaName.IsMatch(t.name) || WetNotSkin.IsMatch(t.name) || t.GetComponent<Renderer>() != null)
                        {
                            continue;
                        }
                        // Only a point near her crotch will do: clothing and far-off helpers are not it.
                        Vector3 mid = 0.5f * (w.legs[0].upper.t.position + w.legs[1].upper.t.position);
                        if ((t.position - mid).magnitude > 0.2f)
                        {
                            continue;
                        }
                        int score = VulvaBest.IsMatch(t.name) ? 2 : 0;
                        if (score > bestScore)
                        {
                            bestScore = score;
                            best = t;
                        }
                    }
                    w.vulva = best;
                    Plugin.ModLog.LogInfo(best != null ? $"Ride: her vagina is taken from the bone '{best.name}'." : "Ride: her rig has no bone for her vagina; it is taken to be a little below the middle of her hip joints.");
                }
                catch (Exception ex)
                {
                    w.vulva = null;
                    Plugin.ModLog.LogWarning("Ride: her vagina could not be looked for: " + ex.Message);
                }
            }
            // (With the fine-tuning of where her entrance is taken to be, forward and up, as set for that hole.)
            bool anal = AnalNow();
            Vector3 fwd = Instance != null ? Instance.m_rideFwd : Vector3.forward;
            Vector3 aim = fwd * (anal ? Plugin.RideAnalAimForward.Value : Plugin.RideAimForward.Value) + Vector3.up * (anal ? Plugin.RideAnalAimUp.Value : Plugin.RideAimUp.Value);
            if (w.vulvaLocalSet && w.holeAnchor != null)
            {
                return w.holeAnchor.TransformPoint(w.vulvaLocal) + (Plugin.RideAutoAim.Value ? Vector3.zero : aim);
            }
            if (w.vulva != null)
            {
                return w.vulva.position + aim;
            }
            return 0.5f * (w.legs[0].upper.t.position + w.legs[1].upper.t.position) + Vector3.down * VulvaBelowHips;
        }

        // How deep he is in her, 0 (out) to 1 (all the way): how much of him, from the tip, is past her entrance, if it is
        // lined up with him at all.
        private float DepthIn(Vector3 vulva)
        {
            if (!PenisNow(out Vector3 b, out Vector3 t))
            {
                return -1f;
            }
            Vector3 shaft = t - b;
            float len = shaft.magnitude;
            Vector3 dir = shaft / len;
            float along = Vector3.Dot(vulva - b, dir);
            float off = (vulva - b - dir * along).magnitude;
            if (along > len + 0.01f)
            {
                return 0f;
            }
            // Beside him rather than over him: not in her.
            float lined = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((off - 0.035f) / 0.05f));
            return Mathf.Clamp01((len - along) / len) * lined;
        }
    }
}
