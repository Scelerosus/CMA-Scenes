using System;
using System.Collections.Generic;
using UnityEngine;
using GameHole = Assets._ReusableScripts.CuchiCuchi.PhysicsAndBonesScripts.BoneStretchedChain;

namespace CMAScenes
{
    // His semen leaving her, in the X-ray through her body: a stream down her canal to her entrance and out of it.
    //
    // The game lets his semen out of her by itself (SemenParaHole, XRaySemen.cs): its count of it in her goes down, and
    // the drops it lets out at her entrance are its own, drawn outside her like anything else. Inside her it draws
    // nothing of that: its drawing of the semen in her only empties. So while the game says it lets it out ("expulsando"),
    // or while its count goes down, a stream of the plugin's own is drawn through her body: from the far end of her canal
    // (the neck of her womb, for her vagina; some centimetres up her rectum, for her anus) along the bones the game has
    // for her canal, to her entrance and a little way out and down. It is as thick as what leaves her each second, and
    // lumps run along it at the pace the game lets it out. When the game stops, it thins and is gone; how much is left
    // in her is the game's. Drawn "always", as the rest of what is in her.
    public sealed partial class ShakeController
    {
        private const int FlowPts = 30, FlowSides = 8;
        private GameObject m_xFlow;
        private Mesh m_xFlowMesh;
        private Material m_xFlowMat;
        private Vector3[] m_xFlowVerts, m_xFlowNorms;
        private readonly Vector3[] m_xFlowLine = new Vector3[FlowPts];
        private readonly float[] m_xFlowAt = new float[FlowPts];
        private static readonly List<Vector3> s_xFlowPath = new List<Vector3>(48);
        // How far the lumps have run along it (metres), and whether it was told of.
        private float m_xFlowRun;
        private bool m_xFlowTold;

        private void FlowMake(Material from)
        {
            FlowDrop();
            if (from == null || m_xLayer < 0)
            {
                return;
            }
            m_xFlowMat = GhostOf(from, SemenTint, 0.95f, XQueue + 3);
            if (m_xFlowMat.HasProperty("_Smoothness")) m_xFlowMat.SetFloat("_Smoothness", 0.9f);
            if (m_xFlowMat.HasProperty("_EmissiveColor")) m_xFlowMat.SetColor("_EmissiveColor", new Color(0.62f, 0.6f, 0.55f, 1f));
            m_xFlowMesh = new Mesh { name = "CMAScenes.XRayFlow", hideFlags = HideFlags.HideAndDontSave };
            m_xFlowMesh.MarkDynamic();
            m_xFlowVerts = new Vector3[FlowPts * FlowSides];
            m_xFlowNorms = new Vector3[FlowPts * FlowSides];
            var tris = new int[(FlowPts - 1) * FlowSides * 6];
            int k = 0;
            for (int j = 0; j + 1 < FlowPts; j++)
            {
                for (int s = 0; s < FlowSides; s++)
                {
                    int s1 = (s + 1) % FlowSides;
                    int a0 = j * FlowSides + s, a1 = j * FlowSides + s1, b0 = a0 + FlowSides, b1 = a1 + FlowSides;
                    tris[k++] = a0; tris[k++] = a1; tris[k++] = b0;
                    tris[k++] = a1; tris[k++] = b1; tris[k++] = b0;
                }
            }
            m_xFlowMesh.vertices = m_xFlowVerts;
            m_xFlowMesh.normals = m_xFlowNorms;
            m_xFlowMesh.triangles = tris;
            m_xFlow = new GameObject("CMAScenes.XRayFlow") { layer = m_xLayer, hideFlags = HideFlags.HideAndDontSave };
            m_xFlow.AddComponent<MeshFilter>().sharedMesh = m_xFlowMesh;
            MeshRenderer r = m_xFlow.AddComponent<MeshRenderer>();
            r.sharedMaterial = m_xFlowMat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            m_xFlow.SetActive(false);
        }

        // Each frame the X-ray shows her through her body.
        private void FlowStep(float dt)
        {
            if (m_xFlow == null)
            {
                return;
            }
            bool anal = m_xFlowA > m_xFlowV;
            float strength = anal ? m_xFlowA : m_xFlowV;
            Transform[] canal = anal ? m_xAnusBones : m_xVagBones;
            GameHole hole = anal == m_holeAnal ? m_vagHole : m_otherHole;
            if (strength < 0.02f || canal == null || canal.Length < 2 || canal[0] == null || canal[1] == null || !FlowPath(anal, canal, hole, out float length))
            {
                if (m_xFlow.activeSelf)
                {
                    m_xFlow.SetActive(false);
                }
                return;
            }
            // The lumps run out at the pace it leaves her: some 4 to 10 cm a second.
            m_xFlowRun += dt * (0.04f + 0.06f * strength);
            float baseR = Mathf.Lerp(0.0011f, 0.0028f, strength);
            const float lumpLen = 0.016f;
            Vector3 up = Vector3.zero;
            for (int j = 0; j < FlowPts; j++)
            {
                Vector3 tan = m_xFlowLine[Mathf.Min(j + 1, FlowPts - 1)] - m_xFlowLine[Mathf.Max(j - 1, 0)];
                tan = tan.sqrMagnitude > 1e-12f ? tan.normalized : Vector3.up;
                // (The ring carried along the stream, not set afresh at each point, so that it does not twist.)
                if (j == 0)
                {
                    up = Vector3.Cross(tan, Mathf.Abs(tan.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
                }
                else
                {
                    up -= tan * Vector3.Dot(up, tan);
                    up = up.sqrMagnitude > 1e-12f ? up.normalized : Vector3.Cross(tan, Vector3.right).normalized;
                }
                Vector3 side = Vector3.Cross(tan, up);
                float s = m_xFlowAt[j];
                float wave = 0.5f + 0.5f * Mathf.Cos((s - m_xFlowRun) / lumpLen * Mathf.PI * 2f);
                float r = baseR * (0.55f + 0.7f * wave * wave);
                // (It comes from nothing at its far end, and ends in a drop that is about to fall.)
                r *= Soft(s / 0.008f);
                float toEnd = length - s;
                if (toEnd < 0.012f)
                {
                    r *= 1f + 0.5f * Soft((0.012f - toEnd) / 0.006f);
                }
                if (j == FlowPts - 1)
                {
                    r = 0f;
                }
                for (int k = 0; k < FlowSides; k++)
                {
                    float a = k * Mathf.PI * 2f / FlowSides;
                    Vector3 n = up * Mathf.Cos(a) + side * Mathf.Sin(a);
                    m_xFlowVerts[j * FlowSides + k] = m_xFlowLine[j] + n * r;
                    m_xFlowNorms[j * FlowSides + k] = n;
                }
            }
            m_xFlowMesh.vertices = m_xFlowVerts;
            m_xFlowMesh.normals = m_xFlowNorms;
            m_xFlowMesh.RecalculateBounds();
            if (!m_xFlow.activeSelf)
            {
                m_xFlow.SetActive(true);
            }
            if (!m_xFlowTold)
            {
                m_xFlowTold = true;
                Plugin.ModLog.LogInfo($"X-ray: his semen leaving her {(anal ? "anus" : "vagina")}: drawn as a stream {length * 100f:F1} cm long along {canal.Length} bone(s) of her canal to her entrance and out ({(anal ? m_xMlA : m_xMlV):F1} ml in her by the game, {(anal ? m_xOutA : m_xOutV):F2} ml a second leaving her{((anal ? m_xLetsA : m_xLetsV) ? ", the game lets it out" : "")}).");
            }
        }

        // The middle line of the stream (FlowPts points, evenly along it, and how far along each is): from the far end
        // of her canal to her entrance, then out and down. False when it cannot be had.
        private bool FlowPath(bool anal, Transform[] canal, GameHole hole, out float length)
        {
            length = 0f;
            List<Vector3> path = s_xFlowPath;
            path.Clear();
            Vector3 deep = canal[canal.Length - 1] != null ? canal[canal.Length - 1].position : canal[1].position;
            if (!anal && m_xCervix != null && (m_xCervix.position - deep).sqrMagnitude < 0.04f * 0.04f)
            {
                path.Add(m_xCervix.position);
            }
            for (int i = canal.Length - 1; i >= 0; i--)
            {
                if (canal[i] != null)
                {
                    path.Add(canal[i].position);
                }
            }
            Vector3 open = canal[0].position;
            try
            {
                if (hole != null && hole.entrada != null)
                {
                    open = HoleOpening(hole);
                }
            }
            catch (Exception)
            {
            }
            if ((open - path[path.Count - 1]).sqrMagnitude > 0.003f * 0.003f)
            {
                path.Add(open);
            }
            Vector3 outward = canal[0].position - canal[1].position;
            outward = outward.sqrMagnitude > 1e-8f ? outward.normalized : Vector3.down;
            path.Add(open + outward * 0.008f);
            path.Add(open + outward * 0.012f + Vector3.down * 0.012f);
            path.Add(open + outward * 0.014f + Vector3.down * 0.03f);
            // (Up her rectum it would run along her gut: from some seven centimetres in only.)
            float total = 0f;
            for (int i = 1; i < path.Count; i++)
            {
                total += (path[i] - path[i - 1]).magnitude;
            }
            float skip = 0f;
            if (anal)
            {
                float inside = 0f;
                int openAt = Math.Max(0, path.Count - 4);
                for (int i = 1; i <= openAt; i++)
                {
                    inside += (path[i] - path[i - 1]).magnitude;
                }
                skip = Mathf.Max(0f, inside - 0.07f);
            }
            length = total - skip;
            if (path.Count < 2 || length < 0.02f || length > 0.5f)
            {
                return false;
            }
            int seg = 1;
            float before = 0f, segLen = (path[1] - path[0]).magnitude;
            for (int j = 0; j < FlowPts; j++)
            {
                float d = skip + length * j / (FlowPts - 1f);
                while (seg < path.Count - 1 && d > before + segLen)
                {
                    before += segLen;
                    seg++;
                    segLen = (path[seg] - path[seg - 1]).magnitude;
                }
                m_xFlowLine[j] = Vector3.Lerp(path[seg - 1], path[seg], segLen > 1e-6f ? Mathf.Clamp01((d - before) / segLen) : 0f);
                m_xFlowAt[j] = d - skip;
            }
            return true;
        }

        private void FlowDrop()
        {
            try
            {
                if (m_xFlow != null) UnityEngine.Object.Destroy(m_xFlow);
                if (m_xFlowMesh != null) UnityEngine.Object.Destroy(m_xFlowMesh);
                if (m_xFlowMat != null) UnityEngine.Object.Destroy(m_xFlowMat);
            }
            catch (Exception)
            {
            }
            m_xFlow = null;
            m_xFlowMesh = null;
            m_xFlowMat = null;
        }
    }
}
