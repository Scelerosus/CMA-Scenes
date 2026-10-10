using System;
using System.Collections.Generic;
using Assets._ReusableScripts.CuchiCuchi;
using Assets._ReusableScripts.CuchiCuchi.Dependentes.Controllers.Interacciones;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using UnityEngine;

namespace CMAScenes
{
    public sealed partial class ShakeController
    {
        private readonly List<Woman> m_women = new List<Woman>();
        private readonly HashSet<long> m_known = new HashSet<long>();
        private readonly Dictionary<long, float> m_buildRetry = new Dictionary<long, float>();

        // A woman who is gone, or whose body was made anew (her bones are gone), is forgotten whole, key and all. The key
        // used to be kept: a woman made anew, or a new one at the same place in memory, was then never taken up again.
        // (Every frame, not only at the look for women every 2 s, which runs only while something is on: after a change of
        // scene with nothing running, the women of the scene before would still be gone through, and fail, in every frame.)
        private void PruneWomen()
        {
            for (int i = m_women.Count - 1; i >= 0; i--)
            {
                Woman old = m_women[i];
                if (old.character == null || old.hip == null)
                {
                    m_known.Remove(old.key);
                    m_women.RemoveAt(i);
                    // (Nothing keeps a woman who is gone: the ride takes the nearest one now, RunFrame.)
                    if (m_rideWoman == old)
                    {
                        m_rideWoman = null;
                        Plugin.ModLog.LogInfo("Ride: the woman she was is gone (the game made her anew, or the scene changed).");
                    }
                    if (m_lookWoman == old)
                    {
                        m_lookWoman = null;
                    }
                    if (m_heldWoman == old)
                    {
                        m_heldWoman = null;
                        m_heldBones.Clear();
                        m_holdValid = false;
                    }
                }
            }
        }

        private void FindWomen()
        {
            PruneWomen();
            var characters = Resources.FindObjectsOfTypeAll<Character>();
            if (characters == null)
            {
                return;
            }
            foreach (Character c in characters)
            {
                if (c == null || !c.gameObject.scene.IsValid() || !c.gameObject.activeInHierarchy)
                {
                    continue;
                }
                long key = c.Pointer.ToInt64();
                if (m_known.Contains(key))
                {
                    continue;
                }
                // (Looked for only on one not known yet: every character of the scene is gone through every 2 s.)
                if (c.GetComponentInChildren<InteraccionesBasicasDeFemale>(true) == null)
                {
                    continue;
                }
                // A body that could not be posed (bones missing) is tried again only now and then, not at every look.
                if (m_buildRetry.TryGetValue(key, out float retryAt) && Time.unscaledTime < retryAt)
                {
                    continue;
                }
                Woman w = Build(c);
                if (w != null)
                {
                    w.key = key;
                    m_buildRetry.Remove(key);
                    m_known.Add(key);
                    m_women.Add(w);
                }
                else
                {
                    m_buildRetry[key] = Time.unscaledTime + 20f;
                }
            }
            foreach (Woman w in m_women)
            {
                RefreshHair(w);
            }
        }

        // The hair's anchor is not a bone, so it does not move with the body by itself.
        private static void RefreshHair(Woman w)
        {
            var dead = new List<int>();
            foreach (var kv in w.hair)
            {
                if (kv.Value.pivot == null)
                {
                    dead.Add(kv.Key);
                }
            }
            foreach (int id in dead)
            {
                w.hair.Remove(id);
            }
            foreach (GPUTools.Hair.Scripts.HairSettings hs in w.character.GetComponentsInChildren<GPUTools.Hair.Scripts.HairSettings>(true))
            {
                if (hs == null || !hs.gameObject.activeInHierarchy)
                {
                    continue;
                }
                Transform pivot = null;
                try { pivot = hs.StandsSettings?.Provider != null ? hs.StandsSettings.Provider.transform : null; } catch (Exception) { }
                pivot = pivot ?? hs.transform;
                int id = pivot.GetInstanceID();
                if (!w.hair.ContainsKey(id))
                {
                    var rig = new HairRig { pivot = pivot, settings = hs };
                    foreach (var f in hs.GetComponentsInChildren<Assets._ReusableScripts.Miscellaneous.BaseFolowTransform>(true))
                    {
                        rig.followers.Add(f);
                    }
                    foreach (var f in hs.GetComponentsInChildren<Assets._ReusableScripts.Miscellaneous.MatrixFollowerBase>(true))
                    {
                        rig.matrixFollowers.Add(f);
                    }
                    foreach (var u in w.character.GetComponentsInChildren<Assets._ReusableScripts.CuchiCuchi.Dependentes.Hair.HairUpdater>(true))
                    {
                        if (u != null && u.m_HairSettings != null && u.m_HairSettings.Pointer == hs.Pointer)
                        {
                            rig.updaters.Add(u);
                        }
                    }
                    w.hair[id] = rig;
                    Plugin.ModLog.LogInfo($"Hair anchor tracked: {PathOf(pivot)} ({rig.followers.Count} followers, {rig.matrixFollowers.Count} matrix followers, {rig.updaters.Count} hair updaters)");
                }
            }
        }

        private static void ApplyHair(Woman w, float poseW, float shakeW)
        {
            if (w.head == null)
            {
                return;
            }
            bool idle = poseW <= 0.001f && shakeW <= 0.001f;
            foreach (HairRig rig in w.hair.Values)
            {
                if (rig.pivot == null)
                {
                    continue;
                }
                if (idle)
                {
                    // Her normal pose: the game drives the hair. Just remember where the anchor sits relative to the head.
                    rig.rel = w.head.worldToLocalMatrix * rig.pivot.localToWorldMatrix;
                    rig.has = true;
                }
                else if (rig.has)
                {
                    // The game updated its hair earlier this frame, from the unposed head. Redo it from the posed one,
                    // with the game's own code: anchor, followers, then the hair updater.
                    Matrix4x4 m = w.head.localToWorldMatrix * rig.rel;
                    rig.pivot.SetPositionAndRotation(m.GetColumn(3), m.rotation);
                    try
                    {
                        foreach (var f in rig.followers) { if (f != null && f.enabled) { f.Follow(); } }
                        foreach (var f in rig.matrixFollowers) { if (f != null && f.enabled) { f.Follow(); } }
                        foreach (var u in rig.updaters) { if (u != null && u.enabled) { u.GPUTools_Hair_Scripts_IHairUpdaterTValle_LateUpdate(); } }
                    }
                    catch (Exception ex)
                    {
                        if (!rig.warned)
                        {
                            rig.warned = true;
                            Plugin.ModLog.LogWarning("Re-running the game's hair update failed: " + ex.Message);
                        }
                    }
                }
            }
        }

        private static Woman Build(Character c)
        {
            Animator anim = c.bodyAnimator;
            Transform hip = null;
            if (anim != null && anim.isHuman)
            {
                Transform mapped = anim.GetBoneTransform(HumanBodyBones.Hips);
                for (Transform t = mapped; t != null && hip == null; t = t.parent)
                {
                    if (t.name == "CC_Base_Hip")
                    {
                        hip = t;
                    }
                }
                if (hip == null)
                {
                    hip = mapped;
                }
            }
            if (hip == null)
            {
                foreach (Transform t in c.GetComponentsInChildren<Transform>(true))
                {
                    if (t.name == "CC_Base_Hip")
                    {
                        hip = t;
                        break;
                    }
                }
            }
            if (hip == null)
            {
                return null;
            }

            var byName = new Dictionary<string, Transform>();
            foreach (Transform t in hip.GetComponentsInChildren<Transform>(true))
            {
                string n = t.name;
                if (!byName.ContainsKey(n))
                {
                    byName[n] = t;
                }
            }

            Transform Get(string n) => byName.TryGetValue(n, out Transform t) ? t : null;

            Transform pelvis = Get("CC_Base_Pelvis");
            Transform waist = Get("CC_Base_Waist");
            Transform sp1 = Get("CC_Base_Spine01");
            Transform sp2 = Get("CC_Base_Spine02");
            Transform[] up = new Transform[2], fo = new Transform[2], en = new Transform[2];
            Transform[] aup = new Transform[2], afo = new Transform[2], aen = new Transform[2];
            for (int i = 0; i < 2; i++)
            {
                string s = i == 0 ? ".L" : ".R";
                up[i] = Get("CC_Base_Thigh" + s);
                fo[i] = Get("CC_Base_Calf" + s);
                en[i] = Get("CC_Base_Foot" + s);
                aup[i] = Get("CC_Base_Upperarm" + s);
                afo[i] = Get("CC_Base_Forearm" + s);
                aen[i] = Get("CC_Base_Hand" + s);
            }
            if (anim != null && anim.isHuman)
            {
                for (int i = 0; i < 2; i++)
                {
                    bool l = i == 0;
                    up[i] = up[i] ?? anim.GetBoneTransform(l ? HumanBodyBones.LeftUpperLeg : HumanBodyBones.RightUpperLeg);
                    fo[i] = fo[i] ?? anim.GetBoneTransform(l ? HumanBodyBones.LeftLowerLeg : HumanBodyBones.RightLowerLeg);
                    en[i] = en[i] ?? anim.GetBoneTransform(l ? HumanBodyBones.LeftFoot : HumanBodyBones.RightFoot);
                    aup[i] = aup[i] ?? anim.GetBoneTransform(l ? HumanBodyBones.LeftUpperArm : HumanBodyBones.RightUpperArm);
                    afo[i] = afo[i] ?? anim.GetBoneTransform(l ? HumanBodyBones.LeftLowerArm : HumanBodyBones.RightLowerArm);
                    aen[i] = aen[i] ?? anim.GetBoneTransform(l ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand);
                }
            }

            string missing = "";
            if (pelvis == null) missing += " pelvis";
            if (waist == null) missing += " waist";
            if (sp1 == null) missing += " spine01";
            if (sp2 == null) missing += " spine02";
            for (int i = 0; i < 2; i++)
            {
                if (up[i] == null || fo[i] == null || en[i] == null) missing += " leg" + i;
                if (aup[i] == null || afo[i] == null || aen[i] == null) missing += " arm" + i;
            }
            Plugin.ModLog.LogInfo($"Woman '{c.name}': hip='{hip.name}' humanoid={(anim != null && anim.isHuman)} missing:[{missing}]");
            if (pelvis == null || waist == null || sp1 == null || sp2 == null || up[0] == null || up[1] == null || fo[0] == null || fo[1] == null || en[0] == null || en[1] == null)
            {
                return null;
            }

            Transform headBone = anim != null && anim.isHuman ? anim.GetBoneTransform(HumanBodyBones.Head) : null;
            headBone = headBone ?? Get("CC_Base_Head");
            var w = new Woman { character = c, name = c.name, hip = hip, sHip = new PosSlot(hip), head = headBone };
            w.sHipRot = new RotSlot(hip);
            w.sPelvis = new RotSlot(pelvis);
            w.sHead = headBone != null ? new RotSlot(headBone) : null;
            w.sWaist = new RotSlot(waist);
            w.sSp1 = new RotSlot(sp1);
            w.sSp2 = new RotSlot(sp2);
            for (int i = 0; i < 2; i++)
            {
                w.legs[i] = new Limb { side = i, upper = new RotSlot(up[i]), fore = new RotSlot(fo[i]), end = new RotSlot(en[i]) };
                if (aup[i] != null && afo[i] != null && aen[i] != null)
                {
                    w.arms[i] = new Limb { side = i, upper = new RotSlot(aup[i]), fore = new RotSlot(afo[i]), end = new RotSlot(aen[i]) };
                }
            }
            Transform gl = Get("DEF_Glute.L"), gr = Get("DEF_Glute.R"), pl = Get("DEF_Pecho.L"), pr = Get("DEF_Pecho.R");
            w.glL = gl != null ? new PosSlot(gl) : null;
            w.glR = gr != null ? new PosSlot(gr) : null;
            w.peL = pl != null ? new PosSlot(pl) : null;
            w.peR = pr != null ? new PosSlot(pr) : null;
            return w;
        }

        private static string PathOf(Transform t)
        {
            if (t == null)
            {
                return "(null)";
            }
            string path = t.name;
            for (int i = 0; t.parent != null && i < 30; i++)
            {
                t = t.parent;
                path = t.name + "/" + path;
            }
            return path;
        }
    }
}
