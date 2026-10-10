using System;
using System.Collections.Generic;
using UnityEngine;
using GameHole = Assets._ReusableScripts.CuchiCuchi.PhysicsAndBonesScripts.BoneStretchedChain;

namespace CMAScenes
{
    // Threads of her fluid between her and his tip as she comes off him (the setting "WetStrings"): from her vagina in
    // the ride, or from her lips as she lets him out of her mouth.
    //
    // A liquid that wets two things that part is drawn out between them into a thread: it hangs a little under its own
    // weight, thins as it is drawn out (its volume stays, spread over more length) and most in its middle, swells into
    // a foot where it holds on, and snaps once it is drawn out too far, the two ends then running back into where they
    // hang from. Each thread is a chain of points (pinned at his tip and at her lips, sagging, kept to its length), drawn
    // as a thin tube of the plugin's own: a copy of his skin's material, without its maps, as smooth as liquid.
    //
    // Clear liquid has no colour of its own: what is seen of a thread of it is what is behind it (here, skin: his and
    // hers) with sharp highlights along it. So it is opaque (a see-through surface in this game's renderer came out
    // black or not at all), coloured as his skin near his tip only paler and greyer, and as smooth as can be: it reads
    // as a clear glossy thread by its highlights. (A dark thread read as a wire.) They were whitish once after he came in
    // her; no longer: nothing of what he came is put on him. It is never thinner than about half a millimetre across, or
    // it would flicker in and out between the pixels; it thins further only just before it snaps.
    public sealed partial class ShakeController
    {
        private sealed class Strand
        {
            public Transform tipBone, herBone;
            public Vector3 tipLocal, herLocal;
            public Vector3[] pts, prev;
            // How much of it there is (m^3), how long it may be drawn out before it snaps, and how long since it snapped
            // (below 0: whole).
            public float volume, breakAt, broken = -1f;
            // How long it has hung, how long it may hang before it drains thin enough to snap by itself (a liquid thread
            // does not hang for ever), and how far it is drawn out toward snapping (0..1: it necks before it goes).
            public float age, life, stretch;
            // Drawn out from his shaft (not his tip) to her lips while she rises: gone once her lips are back at it.
            public bool shaft;
        }

        private const int StrandPoints = 12, StrandSides = 6, MaxStrands = 3;
        private readonly List<Strand> m_strands = new List<Strand>();
        private GameObject m_strandObj;
        private Mesh m_strandMesh;
        private Material m_strandMat;
        private bool m_strandFailed;
        private int m_strandFrame = -1;
        // Her mouth on him lately (Mount.cs, MouthDepth), and whether her lips have been round his tip since the last threads.
        private GameHole m_mouthSeen;
        private float m_mouthSeenAt = -10f;
        private bool m_strandTouch;
        private int m_strandLogs;
        private readonly System.Random m_strandRnd = new System.Random(77);
        // The colour of clear threads: his skin's near his tip, paler and greyer (StrandsReady).
        private Color m_strandClear = new Color(0.93f, 0.91f, 0.9f, 1f);

        // Runs at the end of every frame, once his skeleton and hers are as they will be drawn.
        private void StrandsStep()
        {
            if (m_strandFrame == Time.frameCount || m_strandFailed)
            {
                return;
            }
            m_strandFrame = Time.frameCount;
            try
            {
                if (!Plugin.RideWetStrings.Value || !m_penisValid || m_penisTipBone == null || m_wetParts.Count == 0)
                {
                    StrandsClear();
                    return;
                }
                // Whose lips: her vagina in the ride, else her mouth while she has had him in it lately.
                GameHole hole = null;
                bool mouth = false, touching = false;
                if (m_ride && m_heroReady && m_vagHole != null && !m_holeAnal)
                {
                    hole = m_vagHole;
                    touching = Time.frameCount - m_rideInFrame <= 2 && m_rideIn > 0.03f;
                }
                else if (m_mouthSeen != null && Time.unscaledTime - m_mouthSeenAt < 4f)
                {
                    hole = m_mouthSeen;
                    mouth = true;
                    touching = Time.unscaledTime - m_mouthSeenAt < 0.25f;
                }
                if (hole == null || hole.entrada == null || !PenisLive(out Vector3 b, out Vector3 t))
                {
                    StrandsClear();
                    return;
                }
                Vector3 lips = HoleOpening(hole);
                float gap = (lips - t).magnitude;
                if (touching || gap < 0.006f)
                {
                    // On him: what threads there were are back in her.
                    m_strandTouch = true;
                    if (m_strands.Count > 0 && gap < 0.006f)
                    {
                        StrandsClear();
                    }
                }
                else if (m_strandTouch && gap < 0.08f)
                {
                    // She has just come off his tip: threads, if he is wet enough there.
                    m_strandTouch = false;
                    float wet = mouth ? SpitAt(0.95f) : Mathf.Max(WetAt(0.95f), SpitAt(0.95f));
                    if (wet > 0.25f)
                    {
                        MakeStrands(hole.entrada, lips, b, t, gap, wet, mouth);
                    }
                }
                else if (gap >= 0.08f)
                {
                    m_strandTouch = false;
                }
                if (m_ride && !mouth && touching && !m_holeAnal)
                {
                    ShaftStrands(hole.entrada, lips, b, t);
                }
                if (m_strands.Count == 0)
                {
                    if (m_strandObj != null && m_strandObj.activeSelf)
                    {
                        m_strandObj.SetActive(false);
                    }
                    return;
                }
                float dt = Mathf.Clamp(Time.deltaTime, 0.004f, 0.05f);
                for (int n = m_strands.Count - 1; n >= 0; n--)
                {
                    if (!StrandMove(m_strands[n], dt))
                    {
                        m_strands.RemoveAt(n);
                    }
                }
                Prof.B("StrandsDraw");
                StrandsDraw();
                Prof.E("StrandsDraw");
            }
            catch (Exception ex)
            {
                m_strandFailed = true;
                StrandsDrop();
                Plugin.ModLog.LogWarning("Ride: wet: threads failed and are off for this session: " + ex.Message);
            }
        }

        // While she rises on him, what has just come out of her is wet and her lips still hold on to it: threads are drawn
        // out between the wet shaft just below her lips and her lips, longer as she goes on up, and gone as she comes down.
        private float m_strandPrevIn, m_strandNext;
        private void ShaftStrands(Transform herBone, Vector3 lips, Vector3 b, Vector3 t)
        {
            float dt = Mathf.Max(0.004f, Time.deltaTime);
            float inNow = m_rideIn;
            float rising = (m_strandPrevIn - inNow) / dt;
            m_strandPrevIn = inNow;
            if (rising < 0.2f || Time.time < m_strandNext || inNow < 0.15f || inNow > 0.97f || m_strands.Count >= MaxStrands || !StrandsReady())
            {
                return;
            }
            float length = (t - b).magnitude;
            if (length < 0.05f)
            {
                return;
            }
            Vector3 dir = (t - b) / length;
            float f = Mathf.Clamp01(Vector3.Dot(lips - b, dir) / length);
            float wet = WetAt(Mathf.Max(0f, f - 0.06f));
            if (wet < 0.2f)
            {
                return;
            }
            m_strandNext = Time.time + Mathf.Lerp(0.45f, 0.9f, (float)m_strandRnd.NextDouble());
            Vector3 across = Vector3.Cross(dir, Mathf.Abs(dir.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
            Vector3 across2 = Vector3.Cross(dir, across);
            int count = Mathf.Min(MaxStrands - m_strands.Count, 1 + (m_strandRnd.NextDouble() < 0.5 ? 1 : 0));
            for (int k = 0; k < count; k++)
            {
                float ang = (float)(m_strandRnd.NextDouble() * Math.PI * 2.0);
                Vector3 radial = across * Mathf.Cos(ang) + across2 * Mathf.Sin(ang);
                float below = 0.008f + 0.014f * (float)m_strandRnd.NextDouble();
                // (From his skin just below her up to the rim of her, on the same side of him and just clear of his skin:
                // ending at the middle of her hole, as it did, most of it lay inside him and was never seen.)
                float girth = PenisRadiusAt(Mathf.Max(0f, f - 0.03f));
                Vector3 a = b + dir * Mathf.Max(0.005f, f * length - below) + radial * girth * 1.04f;
                Vector3 c = lips + radial * (girth * 1.02f + 0.002f * (float)m_strandRnd.NextDouble());
                // (A little thicker than the ones off his tip, to be seen at the distance he is seen from.)
                float r0 = Mathf.Lerp(0.0011f, 0.0019f, (float)m_strandRnd.NextDouble()) * (0.8f + 0.4f * wet);
                var s = new Strand
                {
                    tipBone = m_penisTipBone,
                    herBone = herBone,
                    tipLocal = m_penisTipBone.InverseTransformPoint(a),
                    herLocal = herBone.InverseTransformPoint(c),
                    pts = new Vector3[StrandPoints],
                    prev = new Vector3[StrandPoints],
                    volume = Mathf.PI * r0 * r0 * Mathf.Max(below, 0.01f),
                    breakAt = Mathf.Lerp(0.03f, 0.065f, (float)m_strandRnd.NextDouble()) * (0.7f + 0.6f * m_herWet),
                    life = Mathf.Lerp(0.9f, 2f, (float)m_strandRnd.NextDouble()),
                    shaft = true
                };
                for (int j = 0; j < StrandPoints; j++)
                {
                    s.pts[j] = s.prev[j] = Vector3.Lerp(a, c, j / (float)(StrandPoints - 1));
                }
                m_strands.Add(s);
            }
            if (m_strandLogs < 12)
            {
                m_strandLogs++;
                Plugin.ModLog.LogInfo($"Ride: wet: threads: {count} drawn from his shaft ({f * 100f:F0} % up him, he is {wet:F2} wet there) to her lips as she rises.");
            }
        }

        private void MakeStrands(Transform herBone, Vector3 lips, Vector3 b, Vector3 t, float gap, float wet, bool mouth)
        {
            if (!StrandsReady())
            {
                return;
            }
            Vector3 dir = (t - b).normalized;
            Vector3 across = Vector3.Cross(dir, Mathf.Abs(dir.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
            Vector3 across2 = Vector3.Cross(dir, across);
            // (Her fluids and her saliva are clear.)
            int count = 1 + (wet > 0.6f ? 1 : 0) + (m_strandRnd.NextDouble() < 0.4 + 0.4 * m_herWet ? 1 : 0);
            count = Mathf.Min(count, MaxStrands);
            float tipR = PenisRadiusAt(0.97f) * 0.5f;
            for (int k = 0; k < count; k++)
            {
                float ang = (float)(m_strandRnd.NextDouble() * Math.PI * 2.0);
                float off = (float)m_strandRnd.NextDouble();
                Vector3 radial = across * Mathf.Cos(ang) + across2 * Mathf.Sin(ang);
                Vector3 a = t - dir * 0.003f + radial * tipR * off;
                float ang2 = ang + (float)(m_strandRnd.NextDouble() - 0.5);
                Vector3 radial2 = across * Mathf.Cos(ang2) + across2 * Mathf.Sin(ang2);
                Vector3 c = lips + radial2 * (mouth ? 0.012f : 0.007f) * (float)m_strandRnd.NextDouble();
                float u = (float)m_strandRnd.NextDouble();
                float r0 = Mathf.Lerp(0.0009f, 0.0016f, u) * (mouth ? 1.15f : 1f) * (0.8f + 0.4f * wet);
                float len0 = Mathf.Max(gap, 0.006f);
                var s = new Strand
                {
                    tipBone = m_penisTipBone,
                    herBone = herBone,
                    tipLocal = m_penisTipBone.InverseTransformPoint(a),
                    herLocal = herBone.InverseTransformPoint(c),
                    pts = new Vector3[StrandPoints],
                    prev = new Vector3[StrandPoints],
                    volume = Mathf.PI * r0 * r0 * len0,
                    // (Thicker stuff, and a wetter her, draws out further before it snaps.)
                    breakAt = Mathf.Max(gap + 0.01f, Mathf.Lerp(0.025f, 0.07f, (float)m_strandRnd.NextDouble()) * (0.6f + 0.8f * Mathf.Max(m_herWet, mouth ? 0.6f : 0f))),
                    life = Mathf.Lerp(1.5f, 4f, (float)m_strandRnd.NextDouble()) * (mouth ? 1.3f : 1f)
                };
                for (int j = 0; j < StrandPoints; j++)
                {
                    s.pts[j] = s.prev[j] = Vector3.Lerp(a, c, j / (float)(StrandPoints - 1));
                }
                m_strands.Add(s);
                if (m_strands.Count >= MaxStrands)
                {
                    break;
                }
            }
            if (m_strandLogs < 8)
            {
                m_strandLogs++;
                Plugin.ModLog.LogInfo($"Ride: wet: threads: {count} drawn out between his tip and her {(mouth ? "lips" : "vagina")} ({gap * 100f:F1} cm apart, he is {wet:F2} wet there; {StrandSummary()}).");
            }
        }

        // One thread for one frame. False when it is gone.
        private bool StrandMove(Strand s, float dt)
        {
            if (s.tipBone == null || s.herBone == null)
            {
                return false;
            }
            Vector3 a = s.tipBone.TransformPoint(s.tipLocal);
            Vector3 c = s.herBone.TransformPoint(s.herLocal);
            float len = (c - a).magnitude;
            if (s.shaft && s.age > 0.12f)
            {
                // Her lips are back down at where it held on to him (or past it): it is in her again.
                if (len < 0.004f || (PenisNow(out Vector3 pb, out Vector3 pt) && Vector3.Dot(c - a, (pt - pb).normalized) < 0.003f))
                {
                    return false;
                }
            }
            const int last = StrandPoints - 1;
            s.age += dt;
            // Drawn out toward snapping by its length and, as it drains while it hangs, by its age.
            s.stretch = Mathf.Clamp01(Mathf.Max(len / Mathf.Max(0.001f, s.breakAt), s.age / Mathf.Max(0.2f, s.life)));
            if (s.broken < 0f && (len > s.breakAt || s.age > s.life))
            {
                s.broken = 0f;
                if (m_strandLogs < 16)
                {
                    m_strandLogs++;
                    Plugin.ModLog.LogInfo($"Ride: wet: threads: one snapped after {s.age:F1} s, drawn out to {len * 100f:F1} cm ({(len > s.breakAt ? "too far" : "drained thin")}).");
                }
            }
            else if (s.broken >= 0f)
            {
                s.broken += dt;
                if (s.broken > 0.45f)
                {
                    return false;
                }
            }
            if ((s.pts[0] - a).sqrMagnitude > 0.01f || (s.pts[last] - c).sqrMagnitude > 0.01f)
            {
                // Something jumped (a cut of the camera, her put elsewhere): laid out afresh.
                for (int j = 0; j <= last; j++)
                {
                    s.pts[j] = s.prev[j] = Vector3.Lerp(a, c, j / (float)last);
                }
            }
            // Its points fall and swing (a little damped: it is a viscous thing), held to its length.
            Vector3 fall = Vector3.down * 9.81f * dt * dt;
            for (int j = 1; j < last; j++)
            {
                Vector3 now = s.pts[j];
                s.pts[j] = now + (now - s.prev[j]) * 0.94f + fall;
                s.prev[j] = now;
            }
            s.pts[0] = a;
            s.pts[last] = c;
            // (A little longer than the way between its ends: it hangs a little. Snapped, the two halves run back.)
            float rest = len / last * 1.04f;
            int cut = -1;
            if (s.broken >= 0f)
            {
                rest *= Mathf.Max(0.04f, 1f - s.broken / 0.3f);
                cut = last / 2;
            }
            for (int it = 0; it < 6; it++)
            {
                for (int j = 0; j < last; j++)
                {
                    if (j == cut)
                    {
                        continue;
                    }
                    Vector3 d = s.pts[j + 1] - s.pts[j];
                    float m = d.magnitude;
                    if (m < 1e-6f)
                    {
                        continue;
                    }
                    float wa = j == 0 ? 0f : 0.5f, wb = j + 1 == last ? 0f : 0.5f;
                    if (wa + wb <= 0f)
                    {
                        continue;
                    }
                    Vector3 fix = d * ((m - rest) / m / (wa + wb));
                    s.pts[j] += fix * wa;
                    s.pts[j + 1] -= fix * wb;
                }
            }
            return true;
        }

        // The material and the object the threads are drawn with, made once.
        private bool StrandsReady()
        {
            if (m_strandObj != null && m_strandMesh != null && m_strandMat != null)
            {
                return true;
            }
            StrandsDrop();
            WetPart from = m_wetParts.Count > 0 ? m_wetParts[0] : null;
            Material m = from != null && from.material != null ? new Material(from.material) : (Shader.Find("HDRP/Lit") != null ? new Material(Shader.Find("HDRP/Lit")) : null);
            if (m == null)
            {
                Plugin.ModLog.LogInfo("Ride: wet: threads: no material to draw them with.");
                return false;
            }
            m.name = "CMAScenes.Threads";
            m.hideFlags = HideFlags.HideAndDontSave;
            foreach (string map in new[] { "_BaseColorMap", "_MainTex", "_MaskMap", "_NormalMap", "_DetailMap", "_ThicknessMap", "_SubsurfaceMaskMap", "_CoatMaskMap" })
            {
                if (m.HasProperty(map)) m.SetTexture(map, null);
            }
            m.DisableKeyword("_MASKMAP");
            m.DisableKeyword("_NORMALMAP");
            m.DisableKeyword("_DETAIL_MAP");
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.97f);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0f);
            m_strandClear = ClearThreadColour();
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", m_strandClear);
            // (Drawn after his skin and hers where they are drawn as see-through things too, so that it is not painted over by
            // what lies behind it.)
            if (m.renderQueue >= 2500)
            {
                m.renderQueue += 15;
            }
            m_strandMat = m;
            m_strandMesh = new Mesh { name = "CMAScenes.Threads", hideFlags = HideFlags.HideAndDontSave };
            m_strandMesh.MarkDynamic();
            m_strandObj = new GameObject("CMAScenes.Threads") { hideFlags = HideFlags.HideAndDontSave };
            if (from != null && from.renderer != null)
            {
                m_strandObj.layer = from.renderer.gameObject.layer;
            }
            m_strandObj.AddComponent<MeshFilter>().sharedMesh = m_strandMesh;
            var mr = m_strandObj.AddComponent<MeshRenderer>();
            mr.sharedMaterial = m;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            Plugin.ModLog.LogInfo($"Ride: wet: threads: drawn with '{(m.shader != null ? m.shader.name : "?")}' (from '{(from != null ? from.name : "-")}') on layer {m_strandObj.layer}, clear colour {m_strandClear.r:F2},{m_strandClear.g:F2},{m_strandClear.b:F2}, smoothness 0.97.");
            return true;
        }

        // The colour of a clear thread: the average colour of his skin near his tip (what it mostly lies against, and
        // her skin is near enough to it), made paler and greyer, as clear liquid looks against skin. A plain warm pale
        // colour when his skin could not be read.
        private Color ClearThreadColour()
        {
            float r = 0f, g = 0f, b = 0f;
            int n = 0;
            foreach (WetPart p in m_wetParts)
            {
                if (p.skin == null || p.live == null)
                {
                    continue;
                }
                for (int k = 0; k < p.live.Length; k += 7)
                {
                    int i = p.live[k];
                    if (p.along[i] < 0.8f)
                    {
                        continue;
                    }
                    Color32 c = p.skin[i];
                    r += c.r;
                    g += c.g;
                    b += c.b;
                    n++;
                }
            }
            if (n < 20)
            {
                return new Color(0.93f, 0.91f, 0.9f, 1f);
            }
            var skin = new Color(r / n / 255f, g / n / 255f, b / n / 255f, 1f);
            // (Mostly the pale of clear liquid catching the light, a little of his skin showing through it: the colour
            // of his skin made a little paler, as it was, could not be told from the skin behind it.)
            Color col = Color.Lerp(skin, new Color(0.97f, 0.96f, 0.96f, 1f), 0.8f);
            col.a = 1f;
            return col;
        }

        // For the log: how thick the threads are now, at their middle (mm across).
        private string StrandSummary()
        {
            float r = 0f;
            foreach (Strand s in m_strands)
            {
                float len = (s.pts[StrandPoints - 1] - s.pts[0]).magnitude;
                r = Mathf.Max(r, Mathf.Clamp(Mathf.Sqrt(s.volume / (Mathf.PI * Mathf.Max(len, 0.004f))), 0.0005f, 0.0022f) * 0.7f);
            }
            return $"{m_strands.Count} hanging, about {r * 2000f:F1} mm across in the middle";
        }

        // All threads as one mesh of thin tubes, in world space.
        private void StrandsDraw()
        {
            if (!StrandsReady())
            {
                m_strands.Clear();
                return;
            }
            const int last = StrandPoints - 1;
            int nv = m_strands.Count * StrandPoints * StrandSides;
            var vs = new Vector3[nv];
            var ns = new Vector3[nv];
            var tris = new List<int>(m_strands.Count * last * StrandSides * 6);
            int v0 = 0;
            foreach (Strand s in m_strands)
            {
                float curve = 0f;
                for (int j = 0; j < last; j++)
                {
                    curve += (s.pts[j + 1] - s.pts[j]).magnitude;
                }
                // Its volume spread over its length; and as it snaps, gone.
                // (Never thinner than some 0.3 mm in radius where it is whole: thinner, it falls between the pixels.)
                float r = Mathf.Clamp(Mathf.Sqrt(s.volume / (Mathf.PI * Mathf.Max(curve, 0.004f))), 0.0005f, 0.0022f);
                // As it nears snapping it necks in its middle.
                float neck = Soft((s.stretch - 0.7f) / 0.3f);
                if (s.broken >= 0f)
                {
                    r *= Mathf.Max(0f, 1f - s.broken / 0.45f);
                }
                Vector3 up = Vector3.zero;
                for (int j = 0; j <= last; j++)
                {
                    Vector3 tan = s.pts[Mathf.Min(j + 1, last)] - s.pts[Mathf.Max(j - 1, 0)];
                    tan = tan.sqrMagnitude > 1e-12f ? tan.normalized : Vector3.up;
                    // (The ring is turned along with the thread, not set afresh at each point, so that it does not twist.)
                    if (j == 0)
                    {
                        up = Vector3.Cross(tan, Mathf.Abs(tan.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
                    }
                    else
                    {
                        up = (up - tan * Vector3.Dot(up, tan));
                        up = up.sqrMagnitude > 1e-12f ? up.normalized : Vector3.Cross(tan, Vector3.right).normalized;
                    }
                    Vector3 side = Vector3.Cross(tan, up);
                    float f = j / (float)last;
                    float mid = 2f * f - 1f;
                    // Thinnest in its middle; a foot where it holds on.
                    float end = Mathf.Min(f, 1f - f);
                    float rr = r * (0.7f + 0.3f * mid * mid) * (1f - 0.55f * neck * (1f - mid * mid)) * (end < 0.1f ? 1f + 0.8f * (1f - end / 0.1f) : 1f);
                    for (int k = 0; k < StrandSides; k++)
                    {
                        float ang = k * Mathf.PI * 2f / StrandSides;
                        Vector3 nrm = up * Mathf.Cos(ang) + side * Mathf.Sin(ang);
                        vs[v0 + j * StrandSides + k] = s.pts[j] + nrm * rr;
                        ns[v0 + j * StrandSides + k] = nrm;
                    }
                }
                int cut = s.broken >= 0f ? last / 2 : -1;
                for (int j = 0; j < last; j++)
                {
                    if (j == cut)
                    {
                        continue;
                    }
                    for (int k = 0; k < StrandSides; k++)
                    {
                        int k1 = (k + 1) % StrandSides;
                        int a0 = v0 + j * StrandSides + k, a1 = v0 + j * StrandSides + k1;
                        int b0 = a0 + StrandSides, b1 = a1 + StrandSides;
                        // (Wound so that their outside is their front: the other way round only the far wall of each
                        // tube was drawn, lit as if from behind, and a thread was a dim line that could not be seen.)
                        tris.Add(a0); tris.Add(a1); tris.Add(b0);
                        tris.Add(a1); tris.Add(b1); tris.Add(b0);
                    }
                }
                v0 += StrandPoints * StrandSides;
            }
            // Clear fluid: the colour of what is behind it, paler, and as smooth as can be (StrandsReady).
            if (m_strandMat.HasProperty("_BaseColor"))
            {
                Color col = m_strandClear;
                col.a = 1f;
                m_strandMat.SetColor("_BaseColor", col);
            }
            m_strandMesh.Clear();
            m_strandMesh.vertices = vs;
            m_strandMesh.normals = ns;
            m_strandMesh.triangles = tris.ToArray();
            m_strandMesh.RecalculateBounds();
            if (!m_strandObj.activeSelf)
            {
                m_strandObj.SetActive(true);
            }
        }

        // No threads now (the object is kept, hidden).
        private void StrandsClear()
        {
            m_strands.Clear();
            if (m_strandObj != null && m_strandObj.activeSelf)
            {
                m_strandObj.SetActive(false);
            }
        }

        // No threads, and nothing of them kept.
        private void StrandsDrop()
        {
            m_strands.Clear();
            try
            {
                if (m_strandObj != null) UnityEngine.Object.Destroy(m_strandObj);
                if (m_strandMesh != null) UnityEngine.Object.Destroy(m_strandMesh);
                if (m_strandMat != null) UnityEngine.Object.Destroy(m_strandMat);
            }
            catch (Exception)
            {
            }
            m_strandObj = null;
            m_strandMesh = null;
            m_strandMat = null;
        }
    }
}
