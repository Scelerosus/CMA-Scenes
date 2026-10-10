using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Assets;
using Assets._ReusableScripts.CuchiCuchi;
using UnityEngine;

namespace CMAScenes
{
    // A place of the room for the sex: the top of a bench, of a table, a spot of the floor.
    internal sealed class Place
    {
        public string name;
        // The middle of its top, on it; the way its length runs across the floor (he lies along it, his head that way);
        // half its length and half its width (both 0: open, a floor); the floor beside it.
        public Vector3 centre;
        public Vector3 along = Vector3.forward;
        public float halfLen, halfWid, floorY;
        public bool open;
        // How far from its middle, along it, her hips are put so that he lies on it and not off its end.
        public float shift;
        public string how = "";
        // What the player has set by hand for this place (kept while the game runs: going to another place and coming
        // back, it is as he left it).
        public float handAlong, handAcross, handHeight, handTurn;
    }

    // Places.
    //
    // Up to here the sex was laid out from where the game has her standing: her feet stayed where they were, she sat down
    // between them and he was laid under her. So it was always on the floor, and always on that one spot of it.
    //
    // Now it can be put somewhere else. The whole of the sex is still laid out from her body as the game has it, but
    // that body is first carried, whole and rigid, to the place: her hips' bone (everything of her hangs on it) is moved
    // and turned about the upright before anything else reads her, in every frame, by one and the same move. So all that
    // follows (her squat, her feet, his place under her, the floor he lies on) finds her there and does there what it did
    // on the floor, and her own idle sway, the game's own posing of her and the puppet that is carried by her skeleton
    // (HeroExtra.cs) go with her. Her root stays where the game has it: the game goes on thinking she stands there.
    //
    // She is carried at once, not along a path (a path would go through the furniture): the picture is cut to black for
    // that moment and comes back.
    //
    // Places are found in three ways: the furniture of the room by its names and its colliders (PlacesScan), the spot the
    // player looks at (PlacePick), and "where she stands" (none: as it always was). What a place is like (how high, how
    // long and wide, which way it runs) is measured with rays from above; where the furniture has no colliders, from the
    // box of its mesh. What is measured wrong the player can put right by hand: along, across, height, turn.
    public sealed partial class ShakeController
    {
        private readonly List<Place> m_places = new List<Place>();
        private Place m_place;
        private bool m_placesScanned;
        private string m_placesScene = "";

        // The carrying: from where (under her crotch, on the floor she stands on, as the game has her when it begins) and
        // facing which way, and how far she is turned now.
        private bool m_carryOn;
        private Woman m_carryWoman;
        private Vector3 m_carryFrom, m_carryFwd0;
        private float m_carryYaw, m_carryTop;
        private readonly List<SkinnedMeshRenderer> m_carryDraws = new List<SkinnedMeshRenderer>();
        private int m_carryFails;

        private static readonly Regex s_furniture = new Regex("bench|sofa|couch|settee|ottoman|pouf|puff|table|desk|\\bbed\\b|mattress|mesa|escritorio|sillon|cama\\b|banco|camilla|stretcher|counter|divan", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex s_notFurniture = new Regex("leg\\b|legs\\b|lamp|vase|cloth|book|paper|monitor|keyboard|mouse|drawer|handle|glass|plate|cup|bottle|tablet|pose|goto|pivot|target|shadow|light|collider|trigger|decal|text|canvas|label", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        internal bool PlaceActive => m_place != null && m_carryOn;
        internal int PlaceCount => m_places.Count;

        // ---- which place ----

        // The place that is set, by its name; none when it is "where she stands" or is not in this room.
        private Place PlaceResolve()
        {
            string want = Plugin.PlaceUse.Value ?? "";
            if (want.Length == 0)
            {
                return null;
            }
            PlacesFresh();
            if (!m_placesScanned)
            {
                PlacesScan();
            }
            foreach (Place p in m_places)
            {
                if (string.Equals(p.name, want, StringComparison.OrdinalIgnoreCase))
                {
                    return p;
                }
            }
            Plugin.ModLog.LogInfo($"Place: '{want}' is not in this room ({m_places.Count} place(s) known here); the sex is where she stands.");
            return null;
        }

        // (Another room: what was found in the last one is not here.)
        private void PlacesFresh()
        {
            string scene = "";
            try
            {
                scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name ?? "";
            }
            catch (Exception)
            {
            }
            if (scene != m_placesScene)
            {
                m_placesScene = scene;
                m_places.Clear();
                m_placesScanned = false;
            }
        }

        internal string PlaceLabel()
        {
            string want = Plugin.PlaceUse.Value ?? "";
            if (want.Length == 0)
            {
                return Loc.T("Where she stands", "Там, где она стоит");
            }
            foreach (Place p in m_places)
            {
                if (string.Equals(p.name, want, StringComparison.OrdinalIgnoreCase))
                {
                    return p.name + (p.open ? Loc.T("  (floor)", "  (пол)") : $"  ({p.halfLen * 200f:F0} x {p.halfWid * 200f:F0} {Loc.T("cm", "см")}, {Loc.T("height", "высота")} {(p.centre.y - p.floorY) * 100f:F0})");
                }
            }
            return want + Loc.T("  (not in this room)", "  (в этой комнате нет)");
        }

        // The next place of the list (or the one before); "where she stands" is one of them.
        internal void PlaceCycle(int dir)
        {
            PlacesFresh();
            if (!m_placesScanned)
            {
                PlacesScan();
            }
            int at = -1;
            string want = Plugin.PlaceUse.Value ?? "";
            for (int i = 0; i < m_places.Count; i++)
            {
                if (string.Equals(m_places[i].name, want, StringComparison.OrdinalIgnoreCase))
                {
                    at = i;
                }
            }
            int n = m_places.Count + 1;
            int next = ((at + 1 + dir) % n + n) % n - 1;
            PlaceSet(next < 0 ? "" : m_places[next].name);
        }

        internal void PlaceSet(string name)
        {
            if (string.Equals(Plugin.PlaceUse.Value ?? "", name ?? "", StringComparison.Ordinal))
            {
                return;
            }
            // (What was set by hand stays with the place it was set for.)
            foreach (Place p in m_places)
            {
                if (string.Equals(p.name, Plugin.PlaceUse.Value ?? "", StringComparison.OrdinalIgnoreCase))
                {
                    p.handAlong = Plugin.PlaceAlong.Value;
                    p.handAcross = Plugin.PlaceAcross.Value;
                    p.handHeight = Plugin.PlaceHeight.Value;
                    p.handTurn = Plugin.PlaceTurn.Value;
                }
            }
            Place next = null;
            foreach (Place p in m_places)
            {
                if (string.Equals(p.name, name ?? "", StringComparison.OrdinalIgnoreCase))
                {
                    next = p;
                }
            }
            Plugin.PlaceUse.Value = name ?? "";
            Plugin.PlaceAlong.Value = next != null ? next.handAlong : 0f;
            Plugin.PlaceAcross.Value = next != null ? next.handAcross : 0f;
            Plugin.PlaceHeight.Value = next != null ? next.handHeight : 0f;
            Plugin.PlaceTurn.Value = next != null ? next.handTurn : 0f;
            Plugin.Cfg.Save();
            Plugin.ModLog.LogInfo("Place: set to " + (string.IsNullOrEmpty(name) ? "where she stands" : "'" + name + "'") + ".");
            if (Riding)
            {
                // (During the sex: she gets off him and gets on him again there, the way one mode is left for another.)
                m_switching = true;
                m_switchRide = true;
            }
        }

        // Where on the place her hips go and which way he lies, with what the player has set by hand.
        private void PlaceFrame(Place p, out Vector3 to, out Vector3 along)
        {
            along = Quaternion.AngleAxis(Plugin.PlaceTurn.Value, Vector3.up) * p.along;
            Vector3 across = Vector3.Cross(Vector3.up, along);
            to = p.centre + along * (p.shift + Plugin.PlaceAlong.Value) + across * Plugin.PlaceAcross.Value + Vector3.up * Plugin.PlaceHeight.Value;
        }

        // ---- the carrying ----

        // First thing once her bones are the game's again this frame: she is carried to the place.
        private void PlaceCarry(Woman w)
        {
            Place p = m_place;
            if (p == null || m_carryFails > 3 || w == null || w.hip == null || w.sHipRot == null)
            {
                if (m_carryOn)
                {
                    PlaceCarryOff();
                }
                return;
            }
            try
            {
                Vector3 hipL = w.legs[0].upper.t.position, hipR = w.legs[1].upper.t.position;
                bool away = string.Equals(Plugin.RideFacing.Value, "Away", StringComparison.OrdinalIgnoreCase);
                PlaceFrame(p, out Vector3 to, out Vector3 along);
                // (She faces his head; with her back to him it is she who is turned round, and he stays on the place.)
                Vector3 herFwd = away ? -along : along;
                float dt = Mathf.Clamp(Time.deltaTime, 0.004f, 0.1f);
                if (!m_carryOn || m_carryWoman != w)
                {
                    Vector3 right0 = Vector3.ProjectOnPlane(hipR - hipL, Vector3.up);
                    if (right0.sqrMagnitude < 1e-6f)
                    {
                        return;
                    }
                    Vector3 fwd0 = Vector3.Cross(right0.normalized, Vector3.up);
                    Vector3 crotch = 0.5f * (hipL + hipR);
                    float ankle = Mathf.Min(w.legs[0].end.t.position.y, w.legs[1].end.t.position.y);
                    bool found = FloorUnder(new Vector3(crotch.x, 0f, crotch.z), ankle, out float fy);
                    float floor = found ? Mathf.Clamp(fy, ankle - 0.25f, ankle - 0.02f) : ankle - 0.09f;
                    m_carryFrom = new Vector3(crotch.x, floor, crotch.z);
                    m_carryFwd0 = fwd0;
                    m_carryYaw = Vector3.SignedAngle(fwd0, herFwd, Vector3.up);
                    m_carryOn = true;
                    m_carryWoman = w;
                    m_carryJumpFrame = Time.frameCount;
                    CarryDraws(w);
                    FadeCut();
                    Plugin.ModLog.LogInfo($"Place: she is carried to '{p.name}' ({p.how}): {(to - m_carryFrom).magnitude:F2} m from where she stands, {to.y - floor:F2} m up, turned {m_carryYaw:F0} deg; the floor under her was {(found ? "found" : "taken from her ankles")} at {floor:F2}.");
                }
                else
                {
                    // (Turned round on him, or the place turned by hand: she turns there, she does not jump.)
                    float goal = Vector3.SignedAngle(m_carryFwd0, herFwd, Vector3.up);
                    float left = Mathf.DeltaAngle(m_carryYaw, goal);
                    m_carryYaw += left * (1f - Mathf.Exp(-dt / 0.35f));
                }
                m_carryTop = to.y;
                Quaternion turn = Quaternion.AngleAxis(m_carryYaw, Vector3.up);
                Vector3 hip = w.hip.position;
                Vector3 hipTo = to + turn * (hip - m_carryFrom);
                w.hip.rotation = turn * w.hip.rotation;
                w.sHipRot.Commit();
                w.sHip.Carry(hipTo - hip);
            }
            catch (Exception ex)
            {
                if (++m_carryFails > 3)
                {
                    Plugin.ModLog.LogWarning("Place: she could not be carried there (" + ex.Message + "); the sex is where she stands.");
                    PlaceCarryOff();
                    m_place = null;
                }
            }
        }

        // She is the game's again where it has her.
        private void PlaceCarryOff()
        {
            if (!m_carryOn)
            {
                return;
            }
            m_carryOn = false;
            m_carryWoman = null;
            m_carryJumpFrame = Time.frameCount;
            foreach (SkinnedMeshRenderer r in m_carryDraws)
            {
                try
                {
                    if (r != null)
                    {
                        r.updateWhenOffscreen = false;
                    }
                }
                catch (Exception)
                {
                }
            }
            m_carryDraws.Clear();
            FadeCut();
            Plugin.ModLog.LogInfo("Place: she is back where the game has her.");
        }

        // Her meshes are told to keep track of where they really are: the game goes by where their root is to decide
        // whether they are in view, and that is where she stood. (Unity then works their bounds out anew every frame.)
        private void CarryDraws(Woman w)
        {
            try
            {
                Transform top = w.character != null ? w.character.transform : w.hip.root;
                var all = top.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                for (int i = 0; all != null && i < all.Length; i++)
                {
                    SkinnedMeshRenderer r = all[i];
                    if (r != null && !r.updateWhenOffscreen)
                    {
                        r.updateWhenOffscreen = true;
                        m_carryDraws.Add(r);
                    }
                }
            }
            catch (Exception ex)
            {
                Plugin.ModLog.LogInfo("Place: her meshes could not be told to follow her (" + ex.Message + "); should she vanish at the place, that is why.");
            }
        }

        // The top of the place, for where the floor of the sex is.
        private bool PlaceFloor(out float y)
        {
            y = m_carryTop;
            return m_place != null && m_carryOn;
        }

        // How far below the top of the place a foot (or a knee) put at that spot finds something to stand on: 0 on the
        // place, and by its height beside it.
        private float PlaceDrop(Vector3 at)
        {
            Place p = m_place;
            if (p == null || !m_carryOn || p.open || p.halfWid <= 0f)
            {
                return 0f;
            }
            Vector3 rel = at - p.centre;
            Vector3 across = Vector3.Cross(Vector3.up, p.along);
            if (Mathf.Abs(Vector3.Dot(rel, p.along)) <= p.halfLen + 0.02f && Mathf.Abs(Vector3.Dot(rel, across)) <= p.halfWid + 0.02f)
            {
                return 0f;
            }
            return Mathf.Max(0f, m_carryTop - p.floorY);
        }

        // The same spot brought in onto the place, a little way from its edge.
        private Vector3 PlaceOnto(Vector3 at)
        {
            Place p = m_place;
            if (p == null || p.open || p.halfWid <= 0f)
            {
                return at;
            }
            Vector3 rel = at - p.centre;
            Vector3 across = Vector3.Cross(Vector3.up, p.along);
            float a = Mathf.Clamp(Vector3.Dot(rel, p.along), -Mathf.Max(0.02f, p.halfLen - 0.06f), Mathf.Max(0.02f, p.halfLen - 0.06f));
            float c = Mathf.Clamp(Vector3.Dot(rel, across), -Mathf.Max(0.02f, p.halfWid - 0.06f), Mathf.Max(0.02f, p.halfWid - 0.06f));
            Vector3 on = p.centre + p.along * a + across * c;
            on.y = at.y;
            return on;
        }

        // ---- the tour ----
        // After an orgasm of hers she takes him on to the next place of the room: she gets off him, and gets on him
        // again there. Only with the tour set, with more places than one known, and not more often than her orgasms come.
        private float m_tourAt = -100f;

        private void PlaceTourStep()
        {
            if (!Plugin.PlaceTour.Value || !m_climaxHers || m_places.Count == 0 || m_switching || !Riding)
            {
                return;
            }
            float now = Time.unscaledTime;
            if (m_climaxAt <= m_tourAt || now - m_climaxAt < m_climaxLasts + 4f || now - m_climaxAt > m_climaxLasts + 30f)
            {
                return;
            }
            m_tourAt = now;
            Plugin.ModLog.LogInfo("Place: the tour: after her orgasm she takes him to the next place.");
            PlaceCycle(1);
        }

        // ---- the cut to black ----
        private float m_fadeFrom = -10f;

        private void FadeCut()
        {
            if (Plugin.PlaceFade.Value)
            {
                m_fadeFrom = Time.unscaledTime;
            }
        }

        private void FadeGui()
        {
            float t = Time.unscaledTime - m_fadeFrom;
            if (t < 0f || t > 0.8f)
            {
                return;
            }
            float alpha = t < 0.2f ? 1f : 1f - (t - 0.2f) / 0.6f;
            Color keep = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, Mathf.Clamp01(alpha));
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = keep;
        }

        // ---- finding places ----
        private readonly Dictionary<IntPtr, bool> m_surveySkip = new Dictionary<IntPtr, bool>();

        // (Not a part of a character, and not something loose that physics moves about.)
        private bool SurveySkips(Collider c)
        {
            if (c == null)
            {
                return true;
            }
            if (m_surveySkip.TryGetValue(c.Pointer, out bool skip))
            {
                return skip;
            }
            skip = false;
            try
            {
                Rigidbody body = c.attachedRigidbody;
                skip = (body != null && !body.isKinematic) || c.GetComponentInParent<Character>() != null;
            }
            catch (Exception)
            {
                skip = true;
            }
            m_surveySkip[c.Pointer] = skip;
            return skip;
        }

        // The top of what is under a point, looked for from a height down. NaN: nothing there.
        private float TopUnder(float x, float z, float from, float depth)
        {
            var hits = Physics.RaycastAll(new Vector3(x, from, z), Vector3.down, depth, ~0, QueryTriggerInteraction.Ignore);
            float best = float.NaN;
            for (int i = 0; hits != null && i < hits.Length; i++)
            {
                if (SurveySkips(hits[i].collider))
                {
                    continue;
                }
                float y = hits[i].point.y;
                if (float.IsNaN(best) || y > best)
                {
                    best = y;
                }
            }
            return best;
        }

        // The flat top that holds the middle of a square of the room, measured with rays from above: its height is
        // "level" (or, with level NaN, that of the largest flat top there is between the two heights given). False:
        // none found.
        private bool Survey(Vector3 mid, float half, float step, float from, float depth, float level, float lowest, float highest, string name, string how, out Place place)
        {
            place = null;
            int r = Mathf.Clamp(Mathf.CeilToInt(half / step), 4, 24);
            int n = 2 * r + 1;
            var ys = new float[n * n];
            int some = 0;
            for (int iz = 0; iz < n; iz++)
            {
                for (int ix = 0; ix < n; ix++)
                {
                    float y = TopUnder(mid.x + (ix - r) * step, mid.z + (iz - r) * step, from, depth);
                    ys[iz * n + ix] = y;
                    if (!float.IsNaN(y))
                    {
                        some++;
                    }
                }
            }
            if (some == 0)
            {
                return false;
            }
            var mark = new int[n * n];
            var queue = new List<int>();
            int bestId = 0, bestCount = 0, id = 0;
            float bestLevel = 0f;
            // (Either the one flat top that holds the middle, or every flat top there is, the largest of them.)
            for (int seed = 0; seed < n * n; seed++)
            {
                if (!float.IsNaN(level))
                {
                    seed = r * n + r;
                    if (float.IsNaN(ys[seed]) || Mathf.Abs(ys[seed] - level) > 0.05f)
                    {
                        // (The middle itself missed it: the nearest point that is at that height.)
                        int near = -1, nearD = int.MaxValue;
                        for (int k = 0; k < n * n; k++)
                        {
                            if (!float.IsNaN(ys[k]) && Mathf.Abs(ys[k] - level) <= 0.035f)
                            {
                                int dx = k % n - r, dz = k / n - r;
                                if (dx * dx + dz * dz < nearD)
                                {
                                    nearD = dx * dx + dz * dz;
                                    near = k;
                                }
                            }
                        }
                        if (near < 0 || nearD > 9)
                        {
                            return false;
                        }
                        seed = near;
                    }
                }
                if (mark[seed] != 0 || float.IsNaN(ys[seed]) || ys[seed] < lowest || ys[seed] > highest)
                {
                    if (!float.IsNaN(level))
                    {
                        return false;
                    }
                    continue;
                }
                id++;
                float h = float.IsNaN(level) ? ys[seed] : level;
                queue.Clear();
                queue.Add(seed);
                mark[seed] = id;
                int count = 0;
                for (int q = 0; q < queue.Count; q++)
                {
                    int cell = queue[q];
                    count++;
                    int cx = cell % n, cz = cell / n;
                    for (int d = 0; d < 4; d++)
                    {
                        int nx = cx + (d == 0 ? 1 : d == 1 ? -1 : 0), nz = cz + (d == 2 ? 1 : d == 3 ? -1 : 0);
                        if (nx < 0 || nz < 0 || nx >= n || nz >= n)
                        {
                            continue;
                        }
                        int next = nz * n + nx;
                        if (mark[next] == 0 && !float.IsNaN(ys[next]) && Mathf.Abs(ys[next] - h) <= 0.035f)
                        {
                            mark[next] = id;
                            queue.Add(next);
                        }
                    }
                }
                if (count > bestCount)
                {
                    bestCount = count;
                    bestId = id;
                    bestLevel = h;
                }
                if (!float.IsNaN(level))
                {
                    break;
                }
            }
            if (bestCount < 12)
            {
                return false;
            }
            // Where it lies and which way it runs: the middle of its points and the line they are spread along most.
            double mx = 0, mz = 0, my = 0;
            int edge = 0;
            for (int k = 0; k < n * n; k++)
            {
                if (mark[k] == bestId)
                {
                    mx += k % n - r;
                    mz += k / n - r;
                    my += ys[k];
                    int cx = k % n, cz = k / n;
                    if (cx == 0 || cz == 0 || cx == n - 1 || cz == n - 1)
                    {
                        edge++;
                    }
                }
            }
            mx /= bestCount;
            mz /= bestCount;
            my /= bestCount;
            double xx = 0, xz = 0, zz = 0;
            for (int k = 0; k < n * n; k++)
            {
                if (mark[k] == bestId)
                {
                    double dx = k % n - r - mx, dz = k / n - r - mz;
                    xx += dx * dx;
                    xz += dx * dz;
                    zz += dz * dz;
                }
            }
            float angle = 0.5f * Mathf.Atan2((float)(2.0 * xz), (float)(xx - zz));
            Vector3 along = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
            Vector3 across = Vector3.Cross(Vector3.up, along);
            float aLo = float.MaxValue, aHi = float.MinValue, cLo = float.MaxValue, cHi = float.MinValue;
            var lower = new List<float>();
            for (int k = 0; k < n * n; k++)
            {
                Vector3 rel = new Vector3((k % n - r) * step, 0f, (k / n - r) * step);
                if (mark[k] == bestId)
                {
                    float a = Vector3.Dot(rel, along), c = Vector3.Dot(rel, across);
                    aLo = Mathf.Min(aLo, a);
                    aHi = Mathf.Max(aHi, a);
                    cLo = Mathf.Min(cLo, c);
                    cHi = Mathf.Max(cHi, c);
                }
                else if (!float.IsNaN(ys[k]) && ys[k] < bestLevel - 0.15f)
                {
                    lower.Add(ys[k]);
                }
            }
            float area = bestCount * step * step;
            float halfLen = 0.5f * (aHi - aLo) + 0.5f * step, halfWid = 0.5f * (cHi - cLo) + 0.5f * step;
            // (A floor: it runs on past the square that was looked at, or it is simply large.)
            bool open = area > 3.2f || edge > 2 * n || lower.Count < 6;
            place = new Place { name = name, how = how, open = open };
            if (open)
            {
                place.centre = new Vector3(mid.x, (float)my, mid.z);
                place.floorY = (float)my;
                place.along = Vector3.forward;
            }
            else
            {
                place.centre = new Vector3(mid.x, (float)my, mid.z) + along * (0.5f * (aLo + aHi)) + across * (0.5f * (cLo + cHi));
                place.along = along;
                place.halfLen = halfLen;
                place.halfWid = halfWid;
                lower.Sort();
                place.floorY = lower[lower.Count / 4];
                // (He is as long as a man: his head is at the end of it, a hand from the edge, his hips 62 cm from his head, the rest of him along it.)
                place.shift = Mathf.Clamp(halfLen - 0.74f, -0.3f, 1.3f);
            }
            place.how += $", {some} of {n * n} rays met something, its top {bestCount} of them";
            return true;
        }

        // The furniture of the room, by the names of its meshes; measured by its colliders, and where it has none, taken
        // from the box of its mesh.
        internal void PlacesScan()
        {
            PlacesFresh();
            m_placesScanned = true;
            Place picked = null;
            foreach (Place p in m_places)
            {
                if (p.how.StartsWith("looked at", StringComparison.Ordinal))
                {
                    picked = p;
                }
            }
            m_places.Clear();
            m_surveySkip.Clear();
            int seen = 0, named = 0;
            try
            {
                var all = UnityEngine.Object.FindObjectsOfType<MeshRenderer>();
                var done = new HashSet<IntPtr>();
                for (int i = 0; all != null && i < all.Length && m_places.Count < 14; i++)
                {
                    MeshRenderer r = all[i];
                    if (r == null || !r.enabled || !r.gameObject.activeInHierarchy)
                    {
                        continue;
                    }
                    seen++;
                    // (Named so itself, or the thing it is a part of.)
                    Transform owner = null;
                    Transform t = r.transform;
                    for (int up = 0; t != null && up < 3 && owner == null; up++)
                    {
                        string tn = t.name ?? "";
                        if (s_furniture.IsMatch(tn) && !s_notFurniture.IsMatch(tn))
                        {
                            owner = t;
                        }
                        t = t.parent;
                    }
                    if (owner == null || s_notFurniture.IsMatch(r.name ?? "") || !done.Add(owner.Pointer))
                    {
                        continue;
                    }
                    if (r.GetComponentInParent<Character>() != null)
                    {
                        continue;
                    }
                    named++;
                    // The box of the whole thing: of all its meshes.
                    Bounds b = r.bounds;
                    var parts = owner.GetComponentsInChildren<MeshRenderer>(false);
                    for (int k = 0; parts != null && k < parts.Length; k++)
                    {
                        if (parts[k] != null && parts[k].enabled)
                        {
                            b.Encapsulate(parts[k].bounds);
                        }
                    }
                    float wide = Mathf.Min(b.size.x, b.size.z), long_ = Mathf.Max(b.size.x, b.size.z);
                    if (long_ < 0.6f || wide < 0.28f || long_ > 4.5f || b.size.y > 1.6f || b.size.y < 0.12f)
                    {
                        continue;
                    }
                    string name = PlaceName(owner.name);
                    Place place;
                    bool byRays = Survey(b.center, Mathf.Min(1.9f, 0.5f * long_ + 0.35f), 0.09f, b.max.y + 0.3f, b.size.y + 0.9f, float.NaN, b.min.y + 0.15f, b.max.y + 0.05f, name, "furniture, measured by rays", out place) && !place.open;
                    if (!byRays)
                    {
                        // (No colliders to meet the rays, or none that are a top: the box of its mesh, its top as its top.)
                        bool alongX = b.size.x >= b.size.z;
                        place = new Place
                        {
                            name = name,
                            how = "furniture, taken from the box of its mesh (no collider was met)",
                            centre = new Vector3(b.center.x, b.max.y, b.center.z),
                            along = alongX ? Vector3.right : Vector3.forward,
                            halfLen = 0.5f * long_,
                            halfWid = 0.5f * wide,
                            floorY = b.min.y,
                        };
                        place.shift = Mathf.Clamp(place.halfLen - 0.74f, -0.3f, 1.3f);
                    }
                    if (place.halfLen < 0.3f || place.halfWid < 0.14f)
                    {
                        continue;
                    }
                    bool twin = false;
                    foreach (Place other in m_places)
                    {
                        if ((other.centre - place.centre).sqrMagnitude < 0.12f)
                        {
                            twin = true;
                        }
                    }
                    if (twin)
                    {
                        continue;
                    }
                    int same = 1;
                    string baseName = place.name;
                    while (m_places.Exists(o => string.Equals(o.name, place.name, StringComparison.OrdinalIgnoreCase)))
                    {
                        place.name = baseName + " " + (++same);
                    }
                    m_places.Add(place);
                    Plugin.ModLog.LogInfo($"Place: found '{place.name}' ('{owner.name}'): {place.how}; top at {place.centre.y:F2}, {place.centre.y - place.floorY:F2} m above the floor beside it, {place.halfLen * 2f:F2} x {place.halfWid * 2f:F2} m, running {Mathf.Atan2(place.along.x, place.along.z) * Mathf.Rad2Deg:F0} deg.");
                }
            }
            catch (Exception ex)
            {
                Plugin.ModLog.LogWarning("Place: the furniture could not be looked for (" + ex.GetType().Name + ": " + ex.Message + ").");
            }
            if (picked != null)
            {
                m_places.Add(picked);
            }
            Plugin.ModLog.LogInfo($"Place: {m_places.Count} place(s) in this room ('{m_placesScene}'), of {named} named piece(s) of furniture among {seen} meshes.");
        }

        private static string PlaceName(string raw)
        {
            string s = Regex.Replace(raw ?? "", "\\(Clone\\)|\\(\\d+\\)|[_.]+", " ").Trim();
            s = Regex.Replace(s, "\\s+", " ");
            if (s.Length > 22)
            {
                s = s.Substring(0, 22).Trim();
            }
            return s.Length > 0 ? char.ToUpperInvariant(s[0]) + s.Substring(1) : "Place";
        }

        // The spot the player looks at: the top of whatever is there. Kept in the list as "Where I looked".
        internal void PlacePick()
        {
            PlacesFresh();
            m_surveySkip.Clear();
            string picked = Loc.T("Where I looked", "Куда я смотрел");
            try
            {
                Camera cam = Camera.main;
                if (cam == null)
                {
                    Notice(Loc.T("No view to look along.", "Нет камеры."));
                    return;
                }
                Vector3 from = cam.transform.position, dir = cam.transform.forward;
                var hits = Physics.RaycastAll(from, dir, 9f, ~0, QueryTriggerInteraction.Ignore);
                float near = float.MaxValue;
                Vector3 point = Vector3.zero, normal = Vector3.up;
                for (int i = 0; hits != null && i < hits.Length; i++)
                {
                    if (!SurveySkips(hits[i].collider) && hits[i].distance < near && hits[i].distance > 0.15f)
                    {
                        near = hits[i].distance;
                        point = hits[i].point;
                        normal = hits[i].normal;
                    }
                }
                if (near == float.MaxValue)
                {
                    Notice(Loc.T("Nothing there to put the sex on: look at the top of a bench, a table, or at the floor.", "Там не на что положить: посмотри на верх скамьи, стола или на пол."));
                    return;
                }
                if (normal.y < 0.6f)
                {
                    // (Its side: a little way in from it, and down from above.)
                    Vector3 into = Vector3.ProjectOnPlane(-normal, Vector3.up);
                    into = into.sqrMagnitude > 1e-4f ? into.normalized : Vector3.ProjectOnPlane(dir, Vector3.up).normalized;
                    Vector3 probe = point + into * 0.18f;
                    float top = TopUnder(probe.x, probe.z, point.y + 1.1f, 1.3f);
                    if (float.IsNaN(top) || top > point.y + 1.0f)
                    {
                        Notice(Loc.T("That is a wall or the side of something tall: look at the top of a bench, a table, or at the floor.", "Это стена или бок чего-то высокого: посмотри на верх скамьи, стола или на пол."));
                        return;
                    }
                    point = new Vector3(probe.x, top, probe.z);
                }
                Place place;
                if (!Survey(point, 1.6f, 0.07f, point.y + 0.3f, 1.7f, point.y, point.y - 0.06f, point.y + 0.06f, picked, "looked at, measured by rays", out place))
                {
                    place = new Place { name = picked, how = "looked at, a single point (its top could not be measured)", centre = point, floorY = point.y, open = true };
                }
                // Which way he lies: along it, his head to the far end as the player sees it; on a floor, away from him.
                Vector3 view = Vector3.ProjectOnPlane(dir, Vector3.up);
                view = view.sqrMagnitude > 1e-4f ? view.normalized : Vector3.forward;
                if (place.open)
                {
                    place.along = view;
                    place.centre = new Vector3(point.x, place.centre.y, point.z);
                }
                else if (Vector3.Dot(place.along, view) < -0.05f || (Mathf.Abs(Vector3.Dot(place.along, view)) <= 0.05f && Vector3.Dot(place.along, cam.transform.right) < 0f))
                {
                    place.along = -place.along;
                }
                m_places.RemoveAll(o => o.how.StartsWith("looked at", StringComparison.Ordinal));
                m_places.Add(place);
                Plugin.ModLog.LogInfo($"Place: picked by looking: {place.how}; {(place.open ? "a floor" : $"a top of {place.halfLen * 2f:F2} x {place.halfWid * 2f:F2} m, {place.centre.y - place.floorY:F2} m above the floor beside it")}, at ({place.centre.x:F2}, {place.centre.y:F2}, {place.centre.z:F2}).");
                Notice(place.open ? Loc.T("The sex is on that spot of the floor.", "Секс будет на этом месте пола.") : Loc.T("The sex is there, on top of it.", "Секс будет там, на нём."));
                // (Set even when it was set already: it is another spot now.)
                Plugin.PlaceUse.Value = "";
                PlaceSet(picked);
            }
            catch (Exception ex)
            {
                Plugin.ModLog.LogWarning("Place: the spot could not be picked (" + ex.GetType().Name + ": " + ex.Message + ").");
            }
        }
    }
}
