using System;
using UnityEngine;

namespace CMAScenes
{
    // Her legs when she is on her knees.
    //
    // What was wrong in the run of 0.2.0 (the bench, 63 cm wide, Legs "Knees": a knee off its edge and lower than its top,
    // her shins through its edge or in the air beside it, the heels of her shoes standing up into his thighs):
    //  - Her knee was set a fixed way out from the middle of her hips (FeetApart, 27 cm; not from the middle of the bench)
    //    and 14 cm ahead of them, and her ankle a shin's length behind that; the knee itself went wherever the lengths of
    //    her thigh and shin put it between her hip and her ankle. On him her hip joints are some 20 cm above the bench, and
    //    a thigh of 42 cm does not fold down to a knee that close to them: her knee went down through the bench, or off it.
    //  - Her foot was pushed out past his leg (his legs lie apart), off the bench, and left at the height of its top, in the
    //    air; or, out of reach of the floor, brought back in onto the bench, under his leg, where the heel of her shoe stood
    //    up into his thigh.
    //  - Once she was posed, the game's heel system turned her feet again as for standing in heels, and they were left so.
    // Now:
    //  - her knee is on the place (its middle 5.5 cm in from the edge) and outside his body, and as far ahead of her hip as
    //    her thigh reaches down to it, laid out for her hips at the middle of her stroke (at the top of a stroke her knee
    //    comes up a little, at the bottom it presses into the cushion);
    //  - her shin lies back along the place from her knee, turned out round his leg as far as it must; where the place has
    //    no room left for her foot beside his leg, her shin goes over the edge and her foot hangs down beside it;
    //  - her foot lies in the line of her shin, its top down and its heel turned out, away from him (the heel of a shoe
    //    stands up and out), or, hanging, points down;
    //  - kneeling, her feet are kept where they were put once the game's heel system has run (PoseFix.cs).
    // Whether she kneels at all on a place: only where a knee fits between his body and the edge on both sides of him.
    public sealed partial class ShakeController
    {
        // (How much she is on her knees, as she was posed this frame: PoseFix.cs keeps her feet then.)
        private float m_kneelPosed;

        // (The joint of her knee is some 5 cm above what she kneels on; her ankle, the top of her foot on it, 5.5.)
        private const float KneeUp = 0.05f, AnkleUp = 0.055f;

        // How far a spot may go outward (across the place, to the side that is given) before it is off the place. Infinite
        // on the floor and on a place without sides.
        private float PlaceRoom(Vector3 at, Vector3 outward)
        {
            Place p = m_place;
            if (p == null || !m_carryOn || p.open || p.halfWid <= 0f)
            {
                return float.PositiveInfinity;
            }
            Vector3 acrossP = Vector3.Cross(Vector3.up, p.along);
            acrossP.y = 0f;
            if (acrossP.sqrMagnitude < 1e-6f)
            {
                return float.PositiveInfinity;
            }
            acrossP.Normalize();
            float s = Vector3.Dot(acrossP, outward);
            if (Mathf.Abs(s) < 0.4f)
            {
                // (Outward runs along the place: its ends are what stop it, and she is not near them.)
                return float.PositiveInfinity;
            }
            if (s < 0f)
            {
                acrossP = -acrossP;
                s = -s;
            }
            Vector3 rel = at - p.centre;
            rel.y = 0f;
            return (p.halfWid - Vector3.Dot(rel, acrossP)) / s;
        }

        // How far out from "from" (to the side that is given) his body ends beside her knees: his hips and waist, some 16 cm
        // either side of the middle of his hip joints. Without him, 15 cm from her middle.
        private float HisSideOut(Vector3 from, Vector3 outward)
        {
            if (m_heroReady && m_heroThighL != null && m_heroThighR != null)
            {
                Vector3 hisMid = 0.5f * (m_heroThighL.position + m_heroThighR.position);
                Vector3 rel = hisMid - from;
                rel.y = 0f;
                return Vector3.Dot(rel, outward) + 0.16f;
            }
            return 0.15f;
        }

        // Whether a knee of hers fits on the place between his body and its edge, on both sides of him.
        private bool KneelRoom(Vector3 between, Vector3 across)
        {
            for (int i = 0; i < 2; i++)
            {
                Vector3 outward = i == 0 ? -across : across;
                float room = PlaceRoom(between, outward);
                if (!float.IsPositiveInfinity(room) && room - 0.055f < HisSideOut(between, outward) + 0.04f)
                {
                    return false;
                }
            }
            return true;
        }

        // The bone of her toes, looked for once.
        private static Transform FootToe(Limb leg)
        {
            if (!leg.toeSearched)
            {
                leg.toeSearched = true;
                Transform ft = leg.end.t;
                for (int ci = 0; ci < ft.childCount; ci++)
                {
                    Transform ch = ft.GetChild(ci);
                    if (ch != null && ch.name.IndexOf("toe", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        leg.toe = ch;
                        break;
                    }
                }
            }
            return leg.toe;
        }

        // One leg of hers on its knee (see the top of this file). "between": the middle of her hips, on the floor's plane;
        // "ahead", "outward": the way she faces, and out to this leg's side; "apart": how far out her knee is set; "risen":
        // how far her hips are risen in the stroke now; "footRot": her foot as the game's animation has it, standing. Gives
        // where her knee and her ankle go, and how her foot is turned (false: her toes were not found, it is not turned).
        private bool KneelLeg(Limb leg, Vector3 between, Vector3 ahead, Vector3 outward, float apart, float risen, Quaternion footRot, out Vector3 knee, out Vector3 ankle, out Quaternion footLay)
        {
            Vector3 hip = leg.upper.t.position;
            float thigh = (leg.fore.t.position - hip).magnitude;
            float shin = (leg.end.t.position - leg.fore.t.position).magnitude;
            float top = m_rideFloorY;
            float room = PlaceRoom(between, outward);
            float hisSide = HisSideOut(between, outward);
            // Her knee: out by as much as is set, but outside his body and on the place (when both cannot be had, on the
            // place: she was let kneel there only with room enough, and the cushion gives).
            float kneeOut = Mathf.Min(Mathf.Max(apart, hisSide + 0.05f), room - 0.055f);
            Vector3 hipRel = hip - between;
            hipRel.y = 0f;
            float latOff = kneeOut - Vector3.Dot(hipRel, outward);
            // As far ahead of her hip as her thigh reaches down to there, for her hips at the middle of her stroke.
            float hipY = hip.y - 0.5f * Mathf.Max(0f, risen);
            float kneeY = top + KneeUp;
            float dy = hipY - kneeY;
            float flat = thigh * thigh - dy * dy - latOff * latOff;
            float aheadOff = 0f;
            if (flat >= 0f)
            {
                aheadOff = Mathf.Sqrt(flat);
            }
            else
            {
                // (Her hips are too high for her knee to be down out there: it is lifted as far as it must be, beside her hip.)
                float hang = thigh * thigh - latOff * latOff;
                kneeY = hang > 0f ? hipY - Mathf.Sqrt(hang) : hipY;
            }
            knee = new Vector3(hip.x, kneeY, hip.z) + outward * latOff + ahead * aheadOff;

            // Her shin back along the place from her knee, turned out round his leg as far as it must be: her ankle outside his
            // thigh by its thickness and hers (his legs may lie apart).
            float ankleOut = kneeOut;
            if (m_heroReady && m_heroThighL != null && m_heroThighR != null && m_heroKneeL != null && m_heroKneeR != null)
            {
                Vector3 guess = knee - ahead * shin;
                bool leftIsIt = Vector3.Dot(m_heroKneeL.position - between, outward) > Vector3.Dot(m_heroKneeR.position - between, outward);
                Vector3 hisHip = (leftIsIt ? m_heroThighL : m_heroThighR).position;
                Vector3 hisKnee = (leftIsIt ? m_heroKneeL : m_heroKneeR).position;
                Vector3 hisLeg = hisKnee - hisHip;
                float along = hisLeg.sqrMagnitude > 1e-4f ? Mathf.Clamp(Vector3.Dot(guess - hisHip, hisLeg) / hisLeg.sqrMagnitude, 0f, 1.4f) : 0f;
                Vector3 hisAt = hisHip + hisLeg * along - between;
                hisAt.y = 0f;
                ankleOut = Mathf.Max(ankleOut, Vector3.Dot(hisAt, outward) + 0.12f);
            }
            float lat = Mathf.Min(ankleOut - kneeOut, shin * 0.6f);
            // No room left on the place for her ankle there: her shin goes over the edge and her foot hangs down beside it.
            bool hanging = kneeOut + lat > room - 0.04f;
            float ankleY = top + AnkleUp;
            if (hanging)
            {
                lat = Mathf.Min(Mathf.Max(lat, room + 0.035f - kneeOut), shin * 0.6f);
                ankleY = top - 0.01f;
            }
            float drop = kneeY - ankleY;
            float back = Mathf.Sqrt(Mathf.Max(0f, shin * shin - lat * lat - drop * drop));
            ankle = new Vector3(knee.x, ankleY, knee.z) + outward * lat - ahead * back;

            // Her foot in the line of her shin, its top down and turned out by 20 deg (the sole, and the heel of a shoe, face
            // up and away from him); hanging over the edge, pointing down.
            footLay = footRot;
            Transform toe = FootToe(leg);
            if (toe == null)
            {
                return false;
            }
            Vector3 shinRun = ankle - knee;
            Vector3 flatRun = Vector3.ProjectOnPlane(shinRun, Vector3.up);
            flatRun = flatRun.sqrMagnitude > 1e-6f ? flatRun.normalized : -ahead;
            Vector3 toeLying = (flatRun - Vector3.up * 0.15f).normalized;
            Vector3 downP = Vector3.ProjectOnPlane(Vector3.down, toeLying).normalized;
            Vector3 outP = Vector3.ProjectOnPlane(outward, toeLying).normalized;
            Vector3 dorsum = (downP * 0.94f - outP * 0.34f).normalized;
            Vector3 toeWant = toeLying;
            if (hanging)
            {
                toeWant = (shinRun.normalized + Vector3.down + outward * 0.1f).normalized;
                dorsum = Quaternion.FromToRotation(toeLying, toeWant) * dorsum;
            }
            Vector3 toeLocal = Quaternion.Inverse(leg.end.t.rotation) * (toe.position - leg.end.t.position);
            Vector3 toeStand = footRot * toeLocal;
            Vector3 dorsumStand = Vector3.ProjectOnPlane(Vector3.up, toeStand);
            if (toeLocal.sqrMagnitude < 1e-8f || dorsumStand.sqrMagnitude < 1e-6f)
            {
                return false;
            }
            // (The top of her foot as she stands: up, across the way her toes point.)
            Vector3 dorsumLocal = Quaternion.Inverse(footRot) * dorsumStand.normalized;
            footLay = Quaternion.LookRotation(toeWant, dorsum) * Quaternion.Inverse(Quaternion.LookRotation(toeLocal.normalized, dorsumLocal));
            return true;
        }
    }
}
