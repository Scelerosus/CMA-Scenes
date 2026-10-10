using System;
using System.Collections.Generic;
using Assets;
using Assets._ReusableScripts.CuchiCuchi;
using UnityEngine;

namespace CMAScenes
{
    public sealed partial class ShakeController
    {
        // The real floor under her, found by a ray down (her ankles are not on it: heels lift them), how high her ankles
        // stand above it as the game has her standing (about 7 cm barefoot), and where the ground was when the ride began.
        private float m_rideFloorY, m_heelUp, m_groundStart;
        private bool m_floorKnown;
        private float m_floorAt;

        // The height of the floor under a place, found by a ray down: the highest of what is under it that is no part of any
        // character (their bodies are not the floor) and no trigger. False when there is nothing.
        private static bool FloorUnder(Vector3 at, float ankleY, out float y)
        {
            y = 0f;
            try
            {
                var hits = Physics.RaycastAll(new Vector3(at.x, ankleY + 0.15f, at.z), Vector3.down, 1.2f, ~0, QueryTriggerInteraction.Ignore);
                if (hits == null)
                {
                    return false;
                }
                bool any = false;
                for (int i = 0; i < hits.Length; i++)
                {
                    Collider c = hits[i].collider;
                    if (c == null || c.attachedRigidbody != null)
                    {
                        continue;
                    }
                    Character owner = c.GetComponentInParent<Character>();
                    if (owner != null)
                    {
                        continue;
                    }
                    float h = hits[i].point.y;
                    if (!any || h > y)
                    {
                        y = h;
                        any = true;
                    }
                }
                return any;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private readonly float[] m_footGy = { float.NaN, float.NaN };

        // The ground under a spot a foot is put at: how high it is and which way it faces. Looked for with a ray from a
        // little above the spot (so a table over her head is not hit); what is no part of a character, no body of
        // physics and no trigger counts, and the highest of it that is not a wall.
        private bool GroundAt(Vector3 at, float refY, out float y, out Vector3 normal)
        {
            y = 0f;
            normal = Vector3.up;
            try
            {
                var hits = Physics.RaycastAll(new Vector3(at.x, refY + 0.3f, at.z), Vector3.down, 0.95f, ~0, QueryTriggerInteraction.Ignore);
                if (hits == null)
                {
                    return false;
                }
                bool any = false;
                for (int i = 0; i < hits.Length; i++)
                {
                    Collider c = hits[i].collider;
                    if (c == null || c.attachedRigidbody != null || hits[i].normal.y < 0.55f || hits[i].distance < 0.01f)
                    {
                        continue;
                    }
                    if (c.GetComponentInParent<Character>() != null)
                    {
                        continue;
                    }
                    if (!any || hits[i].point.y > y)
                    {
                        y = hits[i].point.y;
                        normal = hits[i].normal;
                        any = true;
                    }
                }
                return any;
            }
            catch (Exception)
            {
                return false;
            }
        }

        // Where the floor is and whether she wears heels: looked for at the start of the ride and now and then, never
        // trusted further than what her ankles allow (the ground is no more than 25 cm below them, and no higher than 2).
        private void FloorLook(Vector3 between, float groundY, bool first)
        {
            // (On a place: its top is the floor of the sex, as it was measured when the place was found. Places.cs)
            if (PlaceFloor(out float placeTop))
            {
                m_rideFloorY = placeTop;
                m_heelUp = Mathf.Max(0f, groundY - placeTop);
                if (!m_floorKnown || first)
                {
                    m_floorKnown = true;
                    Plugin.ModLog.LogInfo($"Ride: floor: the top of the place, at {m_rideFloorY:F2}; her ankles are {m_heelUp * 100f:F0} cm above it.");
                }
                return;
            }
            if (!first && Time.unscaledTime < m_floorAt)
            {
                return;
            }
            m_floorAt = Time.unscaledTime + 2f;
            bool found = FloorUnder(between, groundY, out float y);
            float barefoot = groundY - 0.07f;
            float floorY = found ? Mathf.Clamp(y, groundY - 0.25f, groundY - 0.02f) : barefoot;
            if (!m_floorKnown || first)
            {
                m_rideFloorY = floorY;
            }
            else
            {
                m_rideFloorY = Mathf.Lerp(m_rideFloorY, floorY, 0.3f);
            }
            m_heelUp = Mathf.Max(0f, groundY - m_rideFloorY);
            if (!m_floorKnown || first)
            {
                m_floorKnown = true;
                Plugin.ModLog.LogInfo($"Ride: floor: {(found ? "found by a ray" : "not found by a ray, taken as her bare ankle height")} at {m_rideFloorY:F2}; her ankles are {m_heelUp * 100f:F0} cm above it as the game's animation has her (before its heel system lifts her: heels show in \"posed and drawn\").");
            }
        }
    }
}
