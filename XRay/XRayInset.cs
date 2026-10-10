using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Assets._ReusableScripts.CuchiCuchi.Holes.Internals;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;
using GameHole = Assets._ReusableScripts.CuchiCuchi.PhysicsAndBonesScripts.BoneStretchedChain;

namespace CMAScenes
{
    // The X-ray inset: a cut through her from front to back, on the screen.
    //
    // Each frame the bones of her insides (her canal, the neck and the top of her womb, her rectum: where the game's own
    // springs have them, stretched and pushed by him) and the bones of his penis are laid flat onto the plane that cuts
    // her in two from front to back. That plane is fixed to her pelvis, so the picture stays put while she moves: "up" in
    // it is into her, the way her holes run, and her front is to the right; the entrance of the hole he is at stays in
    // one place. From that the picture is painted (XRayPaint.cs), on a thread of its own so that it costs the frame
    // nothing, and put on the screen the frame after.
    public sealed partial class ShakeController
    {
        private const int XW = 264, XH = 340;
        // (A millimetre of her to a pixel.)
        private const float XScale = 1000f, XEntranceRow = 72f;

        private Texture2D m_xTex;
        private Il2CppStructArray<Color32> m_xBuf;
        private XRayCanvas m_xCanvas;
        private XRayScene m_xSceneBuild = new XRayScene(), m_xScenePaint = new XRayScene();
        private byte[] m_xBack;
        private float m_xBackVx = float.NaN, m_xBackVy;
        // 0: nothing is being painted; 1: a picture is being painted; 2: one is painted and waits to be put up.
        private volatile int m_xPaint;
        private float m_xPaintAt;
        private bool m_xShown, m_xPaintFailed;

        // The plane of the cut, kept as directions on her pelvis: into her, and toward her front.
        private Vector3 m_xUpLocal, m_xFrontLocal;
        private bool m_xFrame;
        // The bones of her insides in the order they lie in from the entrance, and of his penis from its root to its tip.
        private Transform[] m_xVagBones, m_xAnusBones, m_xPenisChain;
        private Transform m_xCervix, m_xWombTop, m_xChainFor;
        private static readonly Vector3[] s_xPts = new Vector3[40];
        // For the words under the picture.
        private float m_xDepth, m_xLen, m_xWordsAt;
        private bool m_xIn, m_xAtCervix;
        private string m_xWords = "", m_xTitle = "";
        private GUIStyle m_xStyle, m_xStyleTitle;

        // Her insides were looked for anew: everything that was read off them is forgotten.
        private void InsetNew()
        {
            m_xFrame = false;
            m_xVagBones = null;
            m_xAnusBones = null;
            m_xCervix = null;
            m_xWombTop = null;
            m_xBackVx = float.NaN;
            m_xShown = false;
            try
            {
                var list = new List<Transform>();
                if (m_xVagIn != null)
                {
                    Section(m_xVagIn.m_canal, list);
                    m_xVagBones = Ordered(list, m_xVag);
                    m_xCervix = m_xVagIn.m_cervix != null ? m_xVagIn.m_cervix : m_xVagIn.m_cervixPivot;
                    m_xWombTop = m_xVagIn.m_uterusTip != null ? m_xVagIn.m_uterusTip : m_xVagIn.m_uterus;
                }
                list.Clear();
                if (m_xAnusIn != null)
                {
                    Section(m_xAnusIn.m_anus, list);
                    Section(m_xAnusIn.m_rectus, list);
                    Section(m_xAnusIn.m_intestines, list);
                    m_xAnusBones = Ordered(list, m_xAnus);
                }
                Plugin.ModLog.LogInfo($"X-ray: inset: her vagina is read from {Named(m_xVagBones)}{(m_xCervix != null ? ", the neck of her womb from '" + m_xCervix.name + "'" : "")}{(m_xWombTop != null ? ", its top from '" + m_xWombTop.name + "'" : "")}; her rectum from {Named(m_xAnusBones)}.");
            }
            catch (Exception ex)
            {
                m_xVagBones = null;
                m_xAnusBones = null;
                Plugin.ModLog.LogInfo("X-ray: inset: the bones of her insides could not be read (" + ex.Message + "); her holes' own entrance and bottom are painted.");
            }
        }

        private static string Named(Transform[] bones)
        {
            if (bones == null || bones.Length == 0)
            {
                return "no bones (its entrance and bottom, as her hole has them)";
            }
            return bones.Length + " bone(s), '" + bones[0].name + "' to '" + bones[bones.Length - 1].name + "'";
        }

        private static void Section(HoleInternal.Seccion sec, List<Transform> into)
        {
            if (sec == null)
            {
                return;
            }
            var bones = sec.bones;
            for (int i = 0; bones != null && i < bones.Count; i++)
            {
                Transform t = bones[i];
                if (t != null && !into.Contains(t))
                {
                    into.Add(t);
                }
            }
        }

        // The bones of a canal in the order they lie in from its entrance: as the game lists them, turned round if that
        // begins at the far end.
        private static Transform[] Ordered(List<Transform> list, HoleInternal hi)
        {
            if (list.Count == 0)
            {
                return null;
            }
            GameHole hole = hi != null ? hi.m_hole : null;
            if (hole != null && hole.entrada != null && list.Count > 1)
            {
                Vector3 e = hole.entrada.position;
                if ((list[0].position - e).sqrMagnitude > (list[list.Count - 1].position - e).sqrMagnitude)
                {
                    list.Reverse();
                }
            }
            if (list.Count > XRayScene.Most - 2)
            {
                list.RemoveRange(XRayScene.Most - 2, list.Count - (XRayScene.Most - 2));
            }
            return list.ToArray();
        }

        // Which way a hole of hers runs in, from its entrance to its bottom.
        private static bool HoleRuns(GameHole hole, out Vector3 dir)
        {
            dir = Vector3.up;
            try
            {
                if (hole == null || hole.entrada == null)
                {
                    return false;
                }
                if (hole.fondo != null)
                {
                    Vector3 d = hole.fondo.position - hole.entrada.position;
                    if (d.sqrMagnitude > 1e-4f)
                    {
                        dir = d.normalized;
                        return true;
                    }
                }
                Vector3 o = -hole.worldOutHoleDirection;
                if (o.sqrMagnitude > 1e-6f)
                {
                    dir = o.normalized;
                    return true;
                }
            }
            catch (Exception)
            {
            }
            return false;
        }

        private void InsetOff()
        {
            m_xShown = false;
        }

        // Once a frame while the X-ray is on: the picture that was painted is put up, and the next one is begun from
        // where everything is now.
        private void InsetStep(Woman w)
        {
            if (m_xPaintFailed || w.sPelvis == null || w.legs[0] == null || w.legs[1] == null)
            {
                return;
            }
            if (m_xTex == null)
            {
                m_xTex = new Texture2D(XW, XH, TextureFormat.RGBA32, false) { name = "CMAScenes.XRay", hideFlags = HideFlags.HideAndDontSave, filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
                m_xBuf = new Il2CppStructArray<Color32>(XW * XH);
                m_xCanvas = new XRayCanvas(XW, XH);
                m_xShown = false;
            }
            if (m_xPaint == 2)
            {
                MemoryMarshal.Cast<byte, Color32>(m_xCanvas.px.AsSpan()).CopyTo(m_xBuf.AsSpan());
                m_xTex.SetPixels32(m_xBuf);
                m_xTex.Apply(false, false);
                m_xShown = true;
                m_xPaint = 0;
            }
            float now = Time.unscaledTime;
            if (m_xPaint != 0 || now < m_xPaintAt)
            {
                return;
            }
            m_xPaintAt = now + 1f / 45f;

            // The plane of the cut, fixed to her pelvis the first time.
            Transform pelvis = w.sPelvis.t;
            if (!m_xFrame)
            {
                Vector3 across = (w.legs[1].upper.t.position - w.legs[0].upper.t.position).normalized;
                Vector3 runs = Vector3.zero;
                if (HoleRuns(m_vagHole, out Vector3 a))
                {
                    runs += a;
                }
                if (HoleRuns(m_otherHole, out Vector3 b))
                {
                    runs += b;
                }
                if (runs.sqrMagnitude < 1e-4f)
                {
                    runs = pelvis.up;
                }
                Vector3 into = Vector3.ProjectOnPlane(runs, across);
                into = into.sqrMagnitude > 1e-6f ? into.normalized : Vector3.up;
                m_xUpLocal = pelvis.InverseTransformDirection(into);
                m_xFrontLocal = pelvis.InverseTransformDirection(Vector3.Cross(across, into));
                m_xFrame = true;
            }
            Vector3 up = pelvis.TransformDirection(m_xUpLocal), front = pelvis.TransformDirection(m_xFrontLocal);
            Vector3 at = VulvaOf(w);

            XRayScene s = m_xSceneBuild;
            s.anal = m_holeAnal;
            // The canal he is at, from its entrance (the origin); and her other one, from where it opens.
            float[] cx = s.anal ? s.anX : s.vagX, cy = s.anal ? s.anY : s.vagY;
            float[] ox = s.anal ? s.vagX : s.anX, oy = s.anal ? s.vagY : s.anY;
            Transform[] mine = s.anal ? m_xAnusBones : m_xVagBones, others = s.anal ? m_xVagBones : m_xAnusBones;
            int cn = Lay(cx, cy, 0f, 0f, true, mine, m_vagHole, at, front, up);
            int on = 0;
            if (m_otherHole != null && m_otherHole.entrada != null)
            {
                Vector3 open = HoleOpening(m_otherHole) - at;
                on = Lay(ox, oy, Vector3.Dot(open, front), Vector3.Dot(open, up), true, others, m_otherHole, at, front, up);
            }
            if (s.anal)
            {
                s.anN = cn;
                s.vagN = on;
            }
            else
            {
                s.vagN = cn;
                s.anN = on;
            }
            // Her womb.
            s.hasUterus = false;
            if (m_xCervix != null && m_xWombTop != null)
            {
                Vector3 c = m_xCervix.position - at, t = m_xWombTop.position - at;
                if (c.sqrMagnitude < 0.12f && t.sqrMagnitude < 0.16f && (c - t).sqrMagnitude > 1e-4f)
                {
                    s.hasUterus = true;
                    s.cervixX = Vector3.Dot(c, front);
                    s.cervixY = Vector3.Dot(c, up);
                    s.wombX = Vector3.Dot(t, front);
                    s.wombY = Vector3.Dot(t, up);
                }
            }
            // Him. (Not after the sex: he stands somewhere else then, and is nothing of the picture.)
            float len = m_xAfter ? 0f : LayHim(s, at, front, up);
            if (m_xAfter)
            {
                s.pN = 0;
            }
            s.inFrom = -1f;
            m_xIn = false;
            if (s.pN >= 2 && m_in && m_mount >= 2)
            {
                // (Where along him her entrance is: the place on him nearest to it.)
                float best = float.MaxValue, gone = 0f, where = 0f;
                for (int i = 0; i < s.pN - 1; i++)
                {
                    float dx = s.pX[i + 1] - s.pX[i], dy = s.pY[i + 1] - s.pY[i];
                    float l2 = dx * dx + dy * dy;
                    float seg = Mathf.Sqrt(l2);
                    float h = l2 > 1e-9f ? Mathf.Clamp01(-(s.pX[i] * dx + s.pY[i] * dy) / l2) : 0f;
                    float ex = s.pX[i] + dx * h, ey = s.pY[i] + dy * h;
                    float d = ex * ex + ey * ey;
                    if (d < best)
                    {
                        best = d;
                        where = gone + seg * h;
                    }
                    gone += seg;
                }
                if (best < 0.06f * 0.06f)
                {
                    s.inFrom = Mathf.Clamp(where, 0f, Mathf.Max(0f, gone - 0.004f));
                    m_xIn = true;
                    // (To his very tip: his middle line ends a little short of it, where his glans is round.)
                    m_xDepth = Mathf.Max(0f, len - s.inFrom);
                }
            }
            m_xLen = len;
            m_xAtCervix = false;
            if (m_xIn && !s.anal && s.hasUterus && s.pN >= 2)
            {
                float tx = s.pX[s.pN - 1] - s.cervixX, ty = s.pY[s.pN - 1] - s.cervixY;
                m_xAtCervix = Mathf.Sqrt(tx * tx + ty * ty) < s.pR[s.pN - 1] + 0.012f;
            }
            s.squeeze = m_squeezeNow;
            s.wet = m_herShine != null ? Mathf.Clamp01(m_herWet) : 0.5f;
            // His semen in her as the game has it (how full each hole is drawn, XRaySemen.cs), and what of it leaves her.
            s.semenV = m_xFillV;
            s.semenA = m_xFillA;
            s.semen = s.anal ? m_xFillA : m_xFillV;
            s.flowV = m_xFlowV;
            s.flowA = m_xFlowA;
            s.flowRun = m_xFlowRun;
            if (s.flowV > 0.02f || s.flowA > 0.02f)
            {
                // (Run on here too when nothing is drawn through her body: the inset alone.)
                if (!XRayThrough)
                {
                    m_xFlowRun += 0.02f * (0.04f + 0.06f * Mathf.Max(s.flowV, s.flowA));
                }
            }
            s.condom = m_condom == 4 && !m_xAfter;
            if (s.condom)
            {
                CondomTint(out Color32 tint, out float _);
                s.condomRgb = (tint.r << 16) | (tint.g << 8) | tint.b;
                // (The teat as round as what he came in it: a millilitre is a cubic centimetre.)
                s.condomFill = Soft(m_condomMl / 1.2f);
                s.teatR = Mathf.Max(0.0042f, Mathf.Pow(3f * Mathf.Max(0f, m_condomMl) * 1e-6f / (4f * Mathf.PI), 1f / 3f) * 1.08f);
            }

            // Painted on a thread of its own (nothing of the game's is touched there), and put up the frame after.
            float vx = s.anal ? (s.vagN > 0 ? s.vagX[0] : 0.04f) : 0f, vy = s.anal ? (s.vagN > 0 ? s.vagY[0] : 0f) : 0f;
            bool newBack = m_xBack == null || float.IsNaN(m_xBackVx) || Mathf.Abs(vx - m_xBackVx) > 0.004f || Mathf.Abs(vy - m_xBackVy) > 0.004f;
            if (newBack)
            {
                m_xBackVx = vx;
                m_xBackVy = vy;
            }
            // (The middle of her body in the middle of the picture, whichever hole is at the origin.)
            float px = XW * 0.5f - (m_xBackVx - 0.015f) * XScale, py = XEntranceRow;
            float bvx = m_xBackVx, bvy = m_xBackVy;
            m_xSceneBuild = m_xScenePaint;
            m_xScenePaint = s;
            XRayCanvas canvas = m_xCanvas;
            m_xPaint = 1;
            Task.Run(() =>
            {
                try
                {
                    if (newBack)
                    {
                        m_xBack = XRayPaint.Background(XW, XH, XScale, px, py, bvx, bvy);
                    }
                    XRayPaint.Paint(canvas, m_xBack, s, XScale, px, py);
                    m_xPaint = 2;
                }
                catch (Exception)
                {
                    m_xPaintFailed = true;
                    m_xPaint = 0;
                }
            });
        }

        // A canal laid flat: from where it begins, along its bones (or, with no bones, to the bottom of the hole).
        private static int Lay(float[] xs, float[] ys, float x0, float y0, bool start, Transform[] bones, GameHole hole, Vector3 at, Vector3 front, Vector3 up)
        {
            int n = 0;
            if (start)
            {
                xs[n] = x0;
                ys[n] = y0;
                n++;
            }
            if (bones != null)
            {
                for (int i = 0; i < bones.Length && n < XRayScene.Most; i++)
                {
                    if (bones[i] == null)
                    {
                        continue;
                    }
                    Vector3 p = bones[i].position - at;
                    float x = Vector3.Dot(p, front), y = Vector3.Dot(p, up);
                    // (Not a point right at the last one, nor one that is nowhere near her.)
                    float dx = x - xs[n - 1], dy = y - ys[n - 1];
                    if (dx * dx + dy * dy < 0.004f * 0.004f || p.sqrMagnitude > 0.25f)
                    {
                        continue;
                    }
                    xs[n] = x;
                    ys[n] = y;
                    n++;
                }
            }
            if (n < 2 && hole != null && hole.fondo != null)
            {
                Vector3 p = hole.fondo.position - at;
                xs[n] = Vector3.Dot(p, front);
                ys[n] = Vector3.Dot(p, up);
                n++;
            }
            return n;
        }

        // His penis laid flat, from its root to its tip, with how thick he is: along its bones where they are a chain,
        // straight from root to tip otherwise. Gives back his length.
        private float LayHim(XRayScene s, Vector3 at, Vector3 front, Vector3 up)
        {
            s.pN = 0;
            if (!PenisLive(out Vector3 root, out Vector3 tip))
            {
                return 0f;
            }
            if (m_xChainFor != m_penisTipBone)
            {
                m_xChainFor = m_penisTipBone;
                m_xPenisChain = null;
                var chain = new List<Transform>();
                Transform t = m_penisTipBone != null ? m_penisTipBone.parent : null;
                for (int guard = 0; t != null && t != m_penisBaseBone && guard < 24; guard++)
                {
                    chain.Add(t);
                    t = t.parent;
                }
                if (t != null && t == m_penisBaseBone && chain.Count > 0)
                {
                    chain.Reverse();
                    m_xPenisChain = chain.ToArray();
                }
                Plugin.ModLog.LogInfo($"X-ray: inset: his penis is read from {(m_xPenisChain != null ? m_xPenisChain.Length + " bone(s) between its root and its tip" : "its root and its tip alone (drawn straight)")}.");
            }
            // The line of him in the world: root, the bones between that lie in order between the two, tip.
            Vector3[] pts = s_xPts;
            int m = 0;
            pts[m++] = root;
            Vector3 axis = tip - root;
            float axis2 = axis.sqrMagnitude;
            float last = 0.03f;
            for (int i = 0; m_xPenisChain != null && i < m_xPenisChain.Length && m < pts.Length - 1; i++)
            {
                if (m_xPenisChain[i] == null)
                {
                    continue;
                }
                Vector3 p = m_xPenisChain[i].position;
                float along = axis2 > 1e-6f ? Vector3.Dot(p - root, axis) / axis2 : 0f;
                if (along > last + 0.04f && along < 0.96f)
                {
                    pts[m++] = p;
                    last = along;
                }
            }
            pts[m++] = tip;
            float len = 0f;
            for (int i = 1; i < m; i++)
            {
                len += (pts[i] - pts[i - 1]).magnitude;
            }
            if (len < 0.03f)
            {
                return 0f;
            }
            // Sixteen places along him, the last where his glans is round (so that the round end of the stroke that is
            // painted there ends at his very tip).
            float tipR = Mathf.Clamp(PenisRadiusAt(0.9f), 0.008f, 0.022f);
            if (tipR <= 0.0081f)
            {
                tipR = 0.016f;
            }
            const int count = 16;
            float end = Mathf.Max(0.02f, len - tipR);
            int seg = 1;
            float before = 0f, segLen = (pts[1] - pts[0]).magnitude;
            for (int i = 0; i < count; i++)
            {
                float d = end * i / (count - 1f);
                while (seg < m - 1 && d > before + segLen)
                {
                    before += segLen;
                    seg++;
                    segLen = (pts[seg] - pts[seg - 1]).magnitude;
                }
                Vector3 p = Vector3.Lerp(pts[seg - 1], pts[seg], segLen > 1e-6f ? Mathf.Clamp01((d - before) / segLen) : 0f) - at;
                s.pX[i] = Vector3.Dot(p, front);
                s.pY[i] = Vector3.Dot(p, up);
                float r = PenisRadiusAt(d / len);
                s.pR[i] = i == count - 1 ? tipR : r > 0.004f ? r : 0.02f;
            }
            s.pN = count;
            return len;
        }

        // On the screen (OnGUI): the picture at the side, with what it shows in words under it.
        private void XRayGui()
        {
            if (m_xW <= 0.01f || !m_xShown || m_xTex == null || !XRayInsetWanted || Event.current == null || Event.current.type != EventType.Repaint)
            {
                return;
            }
            float h = Screen.height * Mathf.Clamp(Plugin.XRayInsetSize.Value, 0.2f, 0.7f);
            float w = h * XW / XH;
            bool left = string.Equals(Plugin.XRayInsetSide.Value, "Left", StringComparison.OrdinalIgnoreCase);
            Rect r = new Rect(left ? 24f : Screen.width - w - 24f, (Screen.height - h) * 0.5f, w, h);
            // (The game's tone mapping makes what is drawn here brighter than it is: darkened like the window.)
            float dim = Mathf.Clamp(Plugin.UiDim.Value, 0.3f, 1f);
            Color keep = GUI.color;
            GUI.color = new Color(dim, dim, dim, m_xW);
            GUI.DrawTexture(r, m_xTex);
            if (m_xStyle == null)
            {
                m_xStyle = new GUIStyle(GUI.skin.label) { fontSize = 17, fontStyle = FontStyle.Bold, alignment = TextAnchor.LowerCenter, wordWrap = false };
                m_xStyleTitle = new GUIStyle(GUI.skin.label) { fontSize = 13, fontStyle = FontStyle.Bold, alignment = TextAnchor.UpperLeft, wordWrap = false };
                if (Menu.UiFont != null)
                {
                    m_xStyle.font = Menu.UiFont;
                    m_xStyleTitle.font = Menu.UiFont;
                }
            }
            if (Time.unscaledTime >= m_xWordsAt)
            {
                m_xWordsAt = Time.unscaledTime + 0.2f;
                m_xTitle = "X-RAY  ·  " + (m_holeAnal ? Loc.T("anus", "анус") : Loc.T("vagina", "вагина"));
                if (m_xAfter)
                {
                    m_xWords = Loc.T("after the sex", "после секса");
                }
                else if (!m_xIn)
                {
                    m_xWords = Loc.T("he is not in her", "он не в ней");
                }
                else
                {
                    m_xWords = Loc.T("in her: ", "в ней: ") + (m_xDepth * 100f).ToString("0.0") + Loc.T(" of ", " из ") + (m_xLen * 100f).ToString("0") + Loc.T(" cm", " см")
                        + (m_squeezeNow > 0.25f ? Loc.T("  ·  she squeezes him", "  ·  сжимает его") : m_xAtCervix ? Loc.T("  ·  at her cervix", "  ·  упёрся в шейку матки") : "");
                }
                // A second line: his semen, in her as the game counts it (and whether it is leaving her), or in the condom.
                float ml = m_holeAnal ? m_xMlA : m_xMlV;
                bool leaving = (m_holeAnal ? m_xFlowA : m_xFlowV) > 0.05f;
                if (m_condom == 4 && m_condomMl > 0.05f && !m_xAfter)
                {
                    m_xWords += "\n" + Loc.T("in the condom: ", "в презервативе: ") + m_condomMl.ToString("0.0") + Loc.T(" ml", " мл");
                }
                else if (ml > 0.05f || leaving)
                {
                    m_xWords += "\n" + Loc.T("his semen in her: ", "его сперма в ней: ") + ml.ToString("0.0") + Loc.T(" ml", " мл") + (leaving ? Loc.T("  ·  flowing out", "  ·  вытекает") : "");
                }
            }
            float k = h / 389f;
            m_xStyleTitle.fontSize = Mathf.Clamp(Mathf.RoundToInt(13f * k), 10, 20);
            // (The words are made smaller until the longest line fits the frame: they were cut off at its sides.)
            int size = Mathf.Clamp(Mathf.RoundToInt(17f * k), 11, 26);
            float room = r.width - 14f * k;
            m_xStyle.fontSize = size;
            m_xStyle.wordWrap = false;
            while (size > 9 && m_xStyle.CalcSize(new GUIContent(m_xWords)).x > room)
            {
                size--;
                m_xStyle.fontSize = size;
            }
            m_xStyle.wordWrap = true;
            int lines = m_xWords.IndexOf('\n') >= 0 ? 2 : 1;
            float wordsH = Mathf.Max(28f * k, m_xStyle.CalcHeight(new GUIContent(m_xWords), room) + 4f);
            Rect words = new Rect(r.x + 7f * k, r.yMax - wordsH - 6f * k * lines, room, wordsH);
            Rect title = new Rect(r.x + 12f * k, r.y + 8f * k, r.width - 20f * k, 22f * k);
            GUI.color = new Color(0f, 0f, 0f, 0.85f * m_xW);
            GUI.Label(new Rect(words.x + 1f, words.y + 1f, words.width, words.height), m_xWords, m_xStyle);
            GUI.Label(new Rect(title.x + 1f, title.y + 1f, title.width, title.height), m_xTitle, m_xStyleTitle);
            GUI.color = new Color(dim, dim * 0.93f, dim * 0.95f, m_xW);
            GUI.Label(words, m_xWords, m_xStyle);
            GUI.color = new Color(dim, dim * 0.75f, dim * 0.85f, 0.9f * m_xW);
            GUI.Label(title, m_xTitle, m_xStyleTitle);
            GUI.color = keep;
        }
    }
}
