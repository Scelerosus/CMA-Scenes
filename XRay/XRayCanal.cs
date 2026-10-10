using System;
using UnityEngine;

namespace CMAScenes
{
    // Her canal in the X-ray through her body, made round him.
    //
    // What the game has for her canal in there is short (a few centimetres of bones) and does not follow his penis: it
    // stays where it is while he goes in, so that half of him showed in a canal that did not fit him and the rest of him
    // ran into the womb. So while he is in her a tube of the plugin's own is drawn instead (CanalStep): from her entrance
    // along him, as wide as he is and a little more, to his tip, closed round it; and her womb (WombStep) is put at its end,
    // where his tip pushes it. The canal stretches round him by as much as he goes in, and ends on the womb.
    // Out of her, the game's own drawing is the one that is shown.
    public sealed partial class ShakeController
    {
        private const int CanalRings = 18, CanalSides = 16;
        private GameObject m_xCanal;
        private Mesh m_xCanalMesh;
        private Material m_xCanalMat;
        private Vector3[] m_xCanalVerts, m_xCanalNorms;
        private bool m_xCanalOn, m_xCanalTold;
        // Where his tip is in the picture, and the way he points, for the womb to rest on.
        private Vector3 m_xCanalTip, m_xCanalAxis;
        private static readonly Color CanalTint = new Color(0.96f, 0.5f, 0.58f);

        private void CanalMake(Material from, float alpha)
        {
            CanalDrop();
            if (from == null || m_xLayer < 0 || !Plugin.XRayOwnCanal.Value)
            {
                return;
            }
            m_xCanalMat = GhostOf(from, CanalTint, Mathf.Min(1f, alpha + 0.1f), XQueue + 1);
            m_xCanalMesh = new Mesh { name = "CMAScenes.XRayCanal", hideFlags = HideFlags.HideAndDontSave };
            m_xCanalMesh.MarkDynamic();
            m_xCanalVerts = new Vector3[CanalRings * CanalSides];
            m_xCanalNorms = new Vector3[CanalRings * CanalSides];
            var tris = new int[(CanalRings - 1) * CanalSides * 6];
            int k = 0;
            for (int j = 0; j + 1 < CanalRings; j++)
            {
                for (int s = 0; s < CanalSides; s++)
                {
                    int s1 = (s + 1) % CanalSides;
                    int a0 = j * CanalSides + s, a1 = j * CanalSides + s1, b0 = a0 + CanalSides, b1 = a1 + CanalSides;
                    tris[k++] = a0; tris[k++] = a1; tris[k++] = b0;
                    tris[k++] = a1; tris[k++] = b1; tris[k++] = b0;
                }
            }
            m_xCanalMesh.vertices = m_xCanalVerts;
            m_xCanalMesh.normals = m_xCanalNorms;
            m_xCanalMesh.triangles = tris;
            m_xCanal = new GameObject("CMAScenes.XRayCanal") { layer = m_xLayer, hideFlags = HideFlags.HideAndDontSave };
            m_xCanal.AddComponent<MeshFilter>().sharedMesh = m_xCanalMesh;
            MeshRenderer r = m_xCanal.AddComponent<MeshRenderer>();
            r.sharedMaterial = m_xCanalMat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            m_xCanal.SetActive(false);
        }

        // Every frame the X-ray shows her through her body. Sets m_xCanalOn (the game's own drawing of her canal is hidden
        // while this one is shown).
        private void CanalStep(float alpha)
        {
            m_xCanalOn = false;
            if (m_xCanal == null)
            {
                return;
            }
            Vector3 root = Vector3.zero, tip = Vector3.zero;
            bool ok = m_in && m_mount >= 2 && !m_holeAnal && m_vagHole != null && m_vagHole.entrada != null && PenisNow(out root, out tip);
            Vector3 entrance = Vector3.zero, axis = Vector3.up;
            float len = 0f, depth = 0f, entranceAt = 0f;
            if (ok)
            {
                axis = tip - root;
                len = axis.magnitude;
                ok = len > 0.05f;
                if (ok)
                {
                    axis /= len;
                    entrance = HoleOpening(m_vagHole);
                    entranceAt = Vector3.Dot(entrance - root, axis);
                    depth = len - entranceAt;
                    ok = depth > 0.012f && depth < 0.3f;
                }
            }
            if (!ok)
            {
                if (m_xCanal.activeSelf)
                {
                    m_xCanal.SetActive(false);
                }
                return;
            }
            m_xCanalOn = true;
            m_xCanalTip = tip;
            m_xCanalAxis = axis;
            // (The middle line: from her entrance, drawn on to his own line within three centimetres.)
            Vector3 onHim = root + axis * entranceAt;
            Vector3 off = entrance - onHim;
            Vector3 side = Vector3.Cross(axis, Mathf.Abs(axis.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
            Vector3 up2 = Vector3.Cross(axis, side);
            float wall = 0.0045f;
            for (int j = 0; j < CanalRings; j++)
            {
                float f = j / (CanalRings - 1f);
                // (Past his tip it closes in a dome: the end of the canal on the neck of her womb.)
                float s = f * (depth + 0.012f);
                float along = entranceAt + s;
                Vector3 centre = root + axis * along + off * (1f - Soft(s / 0.03f));
                float a = Mathf.Clamp01(along / len);
                float r = PenisRadiusAt(a) * 1.1f + wall;
                float beyond = Mathf.Max(0f, s - depth);
                if (beyond > 0f)
                {
                    r *= Mathf.Sqrt(Mathf.Max(0f, 1f - Sq(beyond / 0.012f)));
                }
                else
                {
                    // (Her entrance grips him: narrower there, giving way over the first centimetres.)
                    r *= 0.82f + 0.18f * Soft(s / 0.03f);
                }
                for (int k = 0; k < CanalSides; k++)
                {
                    float ang = k * Mathf.PI * 2f / CanalSides;
                    Vector3 n = side * Mathf.Cos(ang) + up2 * Mathf.Sin(ang);
                    m_xCanalVerts[j * CanalSides + k] = centre + n * r;
                    m_xCanalNorms[j * CanalSides + k] = n;
                }
            }
            m_xCanalMesh.vertices = m_xCanalVerts;
            m_xCanalMesh.normals = m_xCanalNorms;
            m_xCanalMesh.RecalculateBounds();
            if (m_xCanalMat != null && m_xCanalMat.HasProperty("_BaseColor"))
            {
                m_xCanalMat.SetColor("_BaseColor", new Color(CanalTint.r, CanalTint.g, CanalTint.b, Mathf.Min(1f, alpha * 0.9f)));
            }
            if (!m_xCanal.activeSelf)
            {
                m_xCanal.SetActive(true);
            }
            if (!m_xCanalTold)
            {
                m_xCanalTold = true;
                Plugin.ModLog.LogInfo($"X-ray: through her body: her canal is drawn round him (the plugin's own tube from her entrance along him to his tip, {depth * 100f:F1} cm of him in her, his length {len * 100f:F1} cm), and her womb rests on his tip; the game's own drawing of her canal is hidden while he is in her.");
            }
        }

        private void CanalDrop()
        {
            try
            {
                if (m_xCanal != null) UnityEngine.Object.Destroy(m_xCanal);
                if (m_xCanalMesh != null) UnityEngine.Object.Destroy(m_xCanalMesh);
                if (m_xCanalMat != null) UnityEngine.Object.Destroy(m_xCanalMat);
            }
            catch (Exception)
            {
            }
            m_xCanal = null;
            m_xCanalMesh = null;
            m_xCanalMat = null;
            m_xCanalOn = false;
        }
    }
}
