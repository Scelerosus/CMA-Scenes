using System;
using System.Collections.Generic;
using Assets;
using Assets._ReusableScripts.CuchiCuchi;
using UnityEngine;

namespace CMAScenes
{
    public sealed partial class ShakeController
    {
        private Vector3 m_heroHomeGroin;

        // The hero is moved for real. His body hangs from one root (the 'Anim' object of his puppet): the skeleton that is
        // drawn is its children and his physical body, the ragdoll with the colliders the game gave him, follows it. For
        // the ride that root is carried under her and turned onto its back, and the physical body is put on it with the
        // puppet's own teleport call, so he lies there with all of his physics and collision, as the game made them.
        // When the ride ends it is put back, with the physical body, exactly where he stood. His colliders, his puppet
        // and the camera are left alone.
        private Character m_hero;
        private float m_heroNextTry;
        private int m_heroCheckFrame;
        private bool m_heroReady;
        private RootMotion.Dynamics.PuppetMaster m_heroPuppet;
        private Transform m_heroRoot;
        private Vector3 m_heroRootPos;
        private Quaternion m_heroRootRot;
        private readonly List<Transform> m_heroBones = new List<Transform>();
        private readonly List<Vector3> m_heroHomePts = new List<Vector3>();
        private Transform m_heroPivot, m_heroHead;
        private Vector3 m_heroHomeHips, m_heroHomeHead, m_heroHomeFacing;
        private Vector3 m_heroAim;
        private Vector3 m_heroSetPos;
        private Quaternion m_heroSetRot;
        private bool m_heroSet;
        private int m_heroSetFrame = -1;
        private int m_heroFightLogs;
        private bool m_noHeroTold;
        private int m_heroWarns;

        // The game's own switch for the hero's movement controllers (Ootii's motion and actor controllers). It turns them
        // off in its scenes where he is placed by hand, such as the spa; left on, they treat a lying man as one in the air
        // and pull him down. A request to turn them off is added for the ride and taken away when it ends.
        private const string HeroFreezeId = "CMAScenes.Ride";
        private Assets._ReusableScripts.CuchiCuchi.Dependentes.Ootii.MotionAndActorControllerActivable m_heroActivable;

        private static Assets._ReusableScripts.CuchiCuchi.Dependentes.Ootii.MotionAndActorControllerActivable FindHeroActivable(Character hero)
        {
            try
            {
                var puppet = hero.GetComponent<Assets._ReusableScripts.CuchiCuchi.Dependentes.Ootii.Controllers.MotionControllerPuppet>();
                if (puppet != null && puppet.m_MotionAndActorControllerActivable != null)
                {
                    return puppet.m_MotionAndActorControllerActivable;
                }
            }
            catch (Exception)
            {
            }
            try
            {
                return hero.GetComponentInChildren<Assets._ReusableScripts.CuchiCuchi.Dependentes.Ootii.MotionAndActorControllerActivable>(true);
            }
            catch (Exception)
            {
                return null;
            }
        }

        private void FreezeHeroControllers(Character hero)
        {
            m_heroActivable = null;
            try
            {
                var act = FindHeroActivable(hero);
                if (act == null || act.estanDesactivadosModificable == null)
                {
                    Plugin.ModLog.LogInfo("Ride: the hero's movement controllers were not found, they are left on.");
                    return;
                }
                bool actorWas = act.actorController != null && act.actorController.enabled;
                bool motionWas = act.motionController != null && act.motionController.enabled;
                var request = act.estanDesactivadosModificable.ObtenerModificadorNotNull(HeroFreezeId);
                var value = request.valor;
                value.valor = true;
                request.valor = value;
                act.Actualizar();
                m_heroActivable = act;
                bool actorNow = act.actorController != null && act.actorController.enabled;
                bool motionNow = act.motionController != null && act.motionController.enabled;
                Plugin.ModLog.LogInfo($"Ride: hero movement controllers turned off for the ride (actor {actorWas}->{actorNow}, motion {motionWas}->{motionNow}).");
            }
            catch (Exception ex)
            {
                m_heroActivable = null;
                Plugin.ModLog.LogWarning("Hero controllers could not be turned off: " + ex.Message);
            }
        }

        // His own idle animation (the shifting of weight, the breathing) goes on under the lying pose, and with it he sways a
        // little from side to side. It is stopped for the ride, with the rest of his own movement, and let go with it.
        private Animator m_heroAnimator;
        private float m_heroAnimSpeed = 1f;

        private void FreezeHeroAnimation(Character hero)
        {
            m_heroAnimator = null;
            try
            {
                Animator anim = hero.bodyAnimator;
                if (anim != null)
                {
                    m_heroAnimSpeed = anim.speed;
                    anim.speed = 0f;
                    m_heroAnimator = anim;
                }
            }
            catch (Exception ex)
            {
                Plugin.ModLog.LogWarning("Hero animation could not be stopped: " + ex.Message);
            }
        }

        private void ThawHeroAnimation()
        {
            try
            {
                if (m_heroAnimator != null)
                {
                    m_heroAnimator.speed = m_heroAnimSpeed;
                }
            }
            catch (Exception)
            {
            }
            m_heroAnimator = null;
        }

        private void ThawHeroControllers()
        {
            if (m_heroActivable == null)
            {
                return;
            }
            try
            {
                m_heroActivable.estanDesactivadosModificable.RemoverModificador(HeroFreezeId);
                m_heroActivable.Actualizar();
                bool actorNow = m_heroActivable.actorController != null && m_heroActivable.actorController.enabled;
                Plugin.ModLog.LogInfo($"Ride: hero movement controllers given back (actor enabled={actorNow}).");
            }
            catch (Exception ex)
            {
                Plugin.ModLog.LogWarning("Hero controllers could not be given back: " + ex.Message);
            }
            m_heroActivable = null;
        }

        private sealed class HeroMuscleHome { public Transform t; public Rigidbody rb; public Vector3 p; public Quaternion r; }
        private readonly List<HeroMuscleHome> m_heroMuscleHome = new List<HeroMuscleHome>();
        private int m_heroAfterFrame;
        private RootMotion.Dynamics.PuppetMaster m_heroAfterPuppet;
        private Transform m_heroAfterHips;

        // His penis as it is with nothing moving his hips: its two ends where his root is carried to, as they were found
        // when he stood. She sits down to its root, and what the player does with his hips then moves him in her.
        private bool m_penisRestValid;
        // Lying, its root is not quite where it was while he stood (the game lets his penis settle by its weight: in the
        // log her vagina was 3 to 4 cm toward his feet of it, and as much above it, in every pose). The difference is
        // measured on him as he lies, while the player is not moving his hips, and kept in the frame of his root.
        private Vector3 m_penisRestRaw;
        private Vector3 m_penisFix;

        private void MeasurePenisRest()
        {
            // Only while she is over him and not yet on him: sat on, his root is pressed lower, and that is not where it rests.
            if (!m_penisValid || m_heroW < 0.99f || m_mount >= 2 || !PenisNow(out Vector3 nowBase, out Vector3 nowTip))
            {
                return;
            }
            // (His thrust, the wheel's or his own, moves it on purpose: that is not where it rests.)
            Vector3 off = Quaternion.Inverse(m_heroSetRot) * (nowBase - m_thrustWorld - m_penisRestRaw);
            off = Vector3.ClampMagnitude(off, 0.25f);
            m_penisFix = Vector3.Lerp(m_penisFix, off, 1f - Mathf.Exp(-Mathf.Clamp(Time.deltaTime, 0.004f, 0.1f) / 0.3f));
        }

        // How far he is lying down (0 standing at home, 1 lying under her). It has a will of its own, so that however the
        // ride ends he is eased back home first and let go only when he is there.
        private float m_heroW;

        private int m_heroLowTries;

        // The way he lies follows the way she faces slowly, much more slowly than she moves, so that her rocking does not
        // turn him. The sway fields only measure how much he moves sideways while lying (for the log).
        private Vector3 m_heroFwd = Vector3.forward;
        private float m_swayFrom;
        private float m_swayMinX, m_swayMaxX, m_swayMinY, m_swayMaxY, m_herMinX, m_herMaxX;
        private Vector3 m_swayAlong, m_swaySide, m_swayOrigin, m_swayHerOrigin;

        // The hero is the male character the game's camera is attached to. When none is (a third-person scene), the
        // nearest man right next to the view is taken, and nobody if there is none: a bystander is never moved.
        private static Character FindHero(Vector3 near)
        {
            var all = Resources.FindObjectsOfTypeAll<Character>();
            if (all == null)
            {
                return null;
            }
            Character nearest = null;
            float nearestD = 2.5f * 2.5f;
            foreach (Character c in all)
            {
                if (c == null || !c.gameObject.scene.IsValid() || !c.gameObject.activeInHierarchy || c.sexo != Sexo.masculino)
                {
                    continue;
                }
                bool attached = false;
                try
                {
                    attached = c.cameraAtada != null;
                }
                catch (Exception)
                {
                }
                if (attached)
                {
                    return c;
                }
                float d = (c.transform.position - near).sqrMagnitude;
                if (d < nearestD)
                {
                    nearestD = d;
                    nearest = c;
                }
            }
            return nearest;
        }

        private static bool NameHas(string lower, params string[] words)
        {
            foreach (string w in words)
            {
                if (lower.Contains(w))
                {
                    return true;
                }
            }
            return false;
        }

        private static bool IsLeftName(string n)
        {
            return n.Contains("left") || n.Contains("_l_") || n.Contains(".l") || n.EndsWith("_l") || n.Contains("l_thigh");
        }

        private static bool IsRightName(string n)
        {
            return n.Contains("right") || n.Contains("_r_") || n.Contains(".r") || n.EndsWith("_r") || n.Contains("r_thigh");
        }

        private static RootMotion.Dynamics.PuppetMaster FindHeroPuppet(Character hero)
        {
            try
            {
                var puppetChar = hero.GetComponent<Assets._ReusableScripts.CuchiCuchi.Dependentes.Characters.MalePuppetChar>();
                if (puppetChar != null)
                {
                    RootMotion.Dynamics.PuppetMaster pm = puppetChar.puppetMaster;
                    if (pm != null)
                    {
                        return pm;
                    }
                    pm = puppetChar.m_PuppetMaster;
                    if (pm != null)
                    {
                        return pm;
                    }
                }
            }
            catch (Exception)
            {
            }
            try
            {
                return hero.GetComponentInChildren<RootMotion.Dynamics.PuppetMaster>(true);
            }
            catch (Exception)
            {
                return null;
            }
        }

        // His body as it stands now, before the ride touches it: the bones that are drawn, which way he faces, and where his
        // root, his hips and his head are. The ride is planned from this alone, never from the body while it moves.
        private bool CaptureHeroRig(Character hero)
        {
            m_heroBones.Clear();
            m_heroHomePts.Clear();
            m_heroPivot = m_heroHead = null;
            var seen = new HashSet<int>();
            int meshCount = 0;
            Component[] places = { hero, hero.bodyAnimator };
            foreach (Component place in places)
            {
                if (m_heroBones.Count >= 10)
                {
                    break;
                }
                if (place == null)
                {
                    continue;
                }
                var meshes = place.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                if (meshes == null)
                {
                    continue;
                }
                foreach (SkinnedMeshRenderer smr in meshes)
                {
                    if (smr == null)
                    {
                        continue;
                    }
                    meshCount++;
                    var bones = smr.bones;
                    if (bones == null)
                    {
                        continue;
                    }
                    foreach (Transform b in bones)
                    {
                        if (b != null && seen.Add(b.GetInstanceID()))
                        {
                            m_heroBones.Add(b);
                        }
                    }
                }
            }
            if (m_heroBones.Count < 10)
            {
                Plugin.ModLog.LogWarning($"Ride: the hero's drawn skeleton was not found (meshes={meshCount} bones={m_heroBones.Count}), he is not moved.");
                return false;
            }

            Transform left = null, right = null, highest = null;
            foreach (Transform b in m_heroBones)
            {
                string n = b.name.ToLowerInvariant();
                if (m_heroPivot == null && (n.EndsWith("hip") || n.EndsWith("hips") || n.EndsWith("pelvis")))
                {
                    m_heroPivot = b;
                }
                if (m_heroHead == null && n.EndsWith("head"))
                {
                    m_heroHead = b;
                }
                if (NameHas(n, "thigh", "upleg", "upperleg", "upper_leg"))
                {
                    if (left == null && IsLeftName(n))
                    {
                        left = b;
                    }
                    else if (right == null && IsRightName(n))
                    {
                        right = b;
                    }
                }
                if (highest == null || b.position.y > highest.position.y)
                {
                    highest = b;
                }
            }
            if (m_heroPivot == null)
            {
                m_heroPivot = m_heroBones[0];
            }
            if (m_heroHead == null)
            {
                m_heroHead = highest;
            }

            foreach (Transform b in m_heroBones)
            {
                m_heroHomePts.Add(b.position);
            }
            m_heroHomeHips = m_heroPivot.position;
            m_heroHomeHead = m_heroHead != null ? m_heroHead.position : m_heroHomeHips + Vector3.up * 0.7f;

            // The ride is planned from his body as it stands. A body that is not standing (fallen, knocked out) would be a bad
            // plan to come back to, so it is waited for a little, and only then taken as it is.
            float lowY = float.MaxValue, highY = float.MinValue;
            foreach (Vector3 p in m_heroHomePts)
            {
                lowY = Mathf.Min(lowY, p.y);
                highY = Mathf.Max(highY, p.y);
            }
            if (highY - lowY < 1.0f && m_heroLowTries < 3)
            {
                m_heroLowTries++;
                Plugin.ModLog.LogWarning($"Ride: the hero is not standing (his bones span {highY - lowY:F2} m in height), waiting for him to get up ({m_heroLowTries}/3).");
                m_heroHomePts.Clear();
                m_heroNextTry = Time.unscaledTime + 2f;
                return false;
            }

            // He faces the way that is across his hips, turned a quarter, the same way her own front is found.
            Vector3 facing = Vector3.zero;
            if (left != null && right != null)
            {
                Vector3 across = Vector3.ProjectOnPlane(right.position - left.position, Vector3.up);
                if (across.sqrMagnitude > 1e-6f)
                {
                    facing = Vector3.Cross(across.normalized, Vector3.up);
                }
            }
            if (facing.sqrMagnitude < 1e-6f)
            {
                facing = Vector3.ProjectOnPlane(hero.transform.forward, Vector3.up);
            }
            m_heroHomeFacing = facing.sqrMagnitude < 1e-6f ? Vector3.forward : facing.normalized;

            BuildHeroSegs(hero);
            // (The places of him that rest on what he lies on, and the joints bent for them: HeroBody.cs.)
            BuildHeroRest(hero);
            // His crotch, as the middle of his hip joints: the point that is matched to hers.
            m_heroHomeGroin = m_heroThighL != null && m_heroThighR != null
                ? 0.5f * (m_heroThighL.position + m_heroThighR.position)
                : m_heroHomeHips + Vector3.down * 0.07f;
            Plugin.ModLog.LogInfo($"Ride: hero rig: meshes={meshCount} bones={m_heroBones.Count} hips='{m_heroPivot.name}' head='{(m_heroHead != null ? m_heroHead.name : "?")}' legs={(left != null && right != null ? "found" : "NOT FOUND")} hipsAt={m_heroHomeHips.ToString("F2")} rootAt={hero.transform.position.ToString("F2")}");
            return true;
        }

        // Where his root and his physical bones are before the ride, to be put back exactly when it ends.
        private void CaptureHeroHome(Character hero)
        {
            m_heroMuscleHome.Clear();
            m_heroRoot = null;
            try
            {
                if (m_heroPuppet != null)
                {
                    m_heroRoot = m_heroPuppet.targetRoot;
                }
                if (m_heroRoot == null && hero.bodyAnimator != null)
                {
                    m_heroRoot = hero.bodyAnimator.transform;
                }
                if (m_heroRoot != null)
                {
                    m_heroRootPos = m_heroRoot.position;
                    m_heroRootRot = m_heroRoot.rotation;
                }
                var muscles = m_heroPuppet != null ? m_heroPuppet.muscles : null;
                for (int i = 0; muscles != null && i < muscles.Length; i++)
                {
                    if (muscles[i] != null && muscles[i].joint != null)
                    {
                        Transform t = muscles[i].joint.transform;
                        m_heroMuscleHome.Add(new HeroMuscleHome { t = t, rb = t.GetComponent<Rigidbody>(), p = t.position, r = t.rotation });
                    }
                }
                Plugin.ModLog.LogInfo($"Ride: hero home: root '{(m_heroRoot != null ? m_heroRoot.name : "?")}' at {m_heroRootPos.ToString("F2")}, puppet={(m_heroPuppet != null)}, {m_heroMuscleHome.Count} physical bone(s) saved.");
            }
            catch (Exception ex)
            {
                Plugin.ModLog.LogWarning("Hero home could not be saved: " + ex.Message);
            }
        }

        // When the ride ends his root and his physical bones go back exactly where they were before it.
        private void RestoreHeroHome()
        {
            try
            {
                if (m_heroRoot != null)
                {
                    m_heroRoot.SetPositionAndRotation(m_heroRootPos, m_heroRootRot);
                }
                foreach (HeroMuscleHome h in m_heroMuscleHome)
                {
                    if (h.t != null)
                    {
                        h.t.SetPositionAndRotation(h.p, h.r);
                    }
                }
                CalmHeroBodies();
                Physics.SyncTransforms();
            }
            catch (Exception ex)
            {
                Plugin.ModLog.LogWarning("Hero home could not be restored: " + ex.Message);
            }
        }

        // His physical bones keep no speed from the last step: whatever the joints were pulling or the floor pushing against is
        // forgotten, so that being put somewhere is not followed by a fall, or a throw, from the speed gathered meanwhile.
        private void CalmHeroBodies()
        {
            foreach (HeroMuscleHome h in m_heroMuscleHome)
            {
                // (Not of a body that is carried: it has no speed, and Unity complains when one is set on it.)
                if (h.rb != null && !h.rb.isKinematic)
                {
                    h.rb.velocity = Vector3.zero;
                    h.rb.angularVelocity = Vector3.zero;
                }
            }
        }

        private void ReleaseHero()
        {
            FreeHeroPuppet();
            ControlsForget();
            RestoreHeroHome();
            ThawHeroControllers();
            ThawHeroAnimation();
            // A moment later: where is he now, against where he was.
            m_heroAfterPuppet = m_heroPuppet;
            m_heroAfterHips = m_heroPivot;
            m_heroAfterFrame = Time.frameCount + 45;
            m_heroPuppet = null;
            m_heroRoot = null;
            m_heroSet = false;
            m_heroBones.Clear();
            m_heroHomePts.Clear();
            m_heroMuscleHome.Clear();
            m_heroSegs.Clear();
            m_heroChest = null;
            m_heroThighL = m_heroThighR = m_heroKneeL = m_heroKneeR = null;
            m_heroSolid = false;
            m_heroPush = 0f;
            m_pushVel = 0f;
            m_heroW = 0f;
            PenisForget();
            MountForget();
            ThrustRelease();
            CameraRelease();
            m_penisRestValid = false;
            m_hero = null;
            m_heroReady = false;
        }

        // Where the pivot (his hips) has to go, and how the whole body has to turn, for him to lie on his back with his head at
        // the place the ride gives it, his body running back under her pelvis and his back on what he lies on.
        private void HeroGoal(out Vector3 pivotTarget, out Quaternion turn)
        {
            Vector3 up = Vector3.up;
            Quaternion standing = Quaternion.LookRotation(m_heroHomeFacing, up);
            // Facing him, his head is toward where she faces; reversed, it is behind her and he lies the other way round.
            bool away = string.Equals(Plugin.RideFacing.Value, "Away", StringComparison.OrdinalIgnoreCase);
            Vector3 headDir = m_heroFwd;
            Quaternion lying = Quaternion.LookRotation(up, headDir);
            turn = lying * Quaternion.Inverse(standing);

            // How high: his buttocks on what is under them, worked out this frame from what is under him (HeroRestStep,
            // HeroBody.cs; his back and legs are bent onto it there). Before that, a hand above the floor of the sex.
            float pivotY = m_restYValid ? m_restY : m_rideFloorY + 0.12f;
            if (m_herCrotchValid)
            {
                // Matching the crotches: he is laid so that his penis is under her vagina (or, if it was not found, the
                // middle of his hip joints under the middle of hers), whatever the pose, the lean and the facing, and then
                // moved along his body by the fine-tuning, if it is set.
                Vector3 groinRel = turn * (m_heroHomeGroin - m_heroHomeHips);
                // With his penis known he is laid with its root right under where her vagina is on average: at once, with
                // nothing to catch up with (that place does not move with her stroke).
                Vector3 target = m_penisValid && m_vulvaHomeValid ? new Vector3(m_vulvaHome.x, m_crotchS.y, m_vulvaHome.z) : m_crotchS + headDir * Plugin.RideHeroShift.Value;
                if (m_penisValid)
                {
                    // The point of the shaft that is under her: where it is at the height her vagina has halfway through her
                    // stroke, so that it goes into her along its length rather than past it.
                    Vector3 baseRel = turn * (m_penisHomeBase - m_heroHomeHips);
                    // Lying on his back it stands straight up from its root, whichever way it pointed while he stood (the game
                    // lets it hang by its weight), so it is the root that goes under her.
                    groinRel = baseRel + m_heroSetRot * m_penisFix;
                    if (m_vulvaOffValid)
                    {
                        target += m_vulvaOff;
                    }
                }
                pivotTarget = new Vector3(target.x - groinRel.x, pivotY, target.z - groinRel.z);
                return;
            }
            // Before her crotch has been measured (the first frame): his head goes to the view point, in front of her pelvis
            // (or behind it when she faces away).
            Vector3 headRel = turn * (m_heroHomeHead - m_heroHomeHips);
            Vector3 aim = away ? 2f * m_ridePelvis - m_heroAim : m_heroAim;
            Vector3 headAt = new Vector3(aim.x, 0f, aim.z) + headDir * Plugin.RideHeroShift.Value;
            pivotTarget = new Vector3(headAt.x - headRel.x, pivotY, headAt.z - headRel.z);
        }

        // Carries his root to where it has to be this frame (blended with the ride's pose, so he lies down as it begins and
        // gets up as it ends) and puts his physical body on it.
        private void MoveHeroRoot(float weight)
        {
            HeroGoal(out Vector3 pivotTarget, out Quaternion turn);
            Quaternion part = Quaternion.Slerp(Quaternion.identity, turn, weight);
            Vector3 pivotAt = Vector3.Lerp(m_heroHomeHips, pivotTarget, weight);
            m_heroSetPos = pivotAt + part * (m_heroRootPos - m_heroHomeHips);
            m_heroSetRot = part * m_heroRootRot;
            m_heroSet = true;
            m_heroSetFrame = Time.frameCount;
            m_penisRestValid = m_penisValid && weight > 0.9f;
            if (m_penisValid)
            {
                m_penisRestRaw = pivotAt + part * (m_penisHomeBase - m_heroHomeHips);
                // And where his root will rest once he lies: she comes over that place while he is still being laid down.
                m_penisGoalBase = pivotTarget + turn * (m_penisHomeBase - m_heroHomeHips) + (turn * m_heroRootRot) * m_penisFix;
            }
            m_penisGoalValid = m_penisValid;
            // Only his root now: his physical body is put on it at the end of the frame, once the game has posed him. (Put
            // on it here as well, it was carried to and fro every frame between where he is before the game's own posing
            // of his hips and after it, his penis with it, and nothing could hold on to that.)
            m_heroRoot.SetPositionAndRotation(m_heroSetPos, m_heroSetRot);
        }

        private void ApplyHeroRoot()
        {
            m_heroRoot.SetPositionAndRotation(m_heroSetPos, m_heroSetRot);
            if (m_heroPuppet != null)
            {
                // His physical body is put on his root, so it is never dragged there by forces (all of it but an arm
                // whose hand the player is using: that one is the physics' to move).
                TeleportHeroBodies();
            }
        }

        private void LogHeroPuppet(string when)
        {
            try
            {
                if (m_heroPuppet == null)
                {
                    Plugin.ModLog.LogInfo($"Ride: hero puppet ({when}): none found");
                    return;
                }
                var muscles = m_heroPuppet.muscles;
                string at = "?";
                if (muscles != null && muscles.Length > 0 && muscles[0] != null && muscles[0].joint != null && muscles[0].target != null)
                {
                    at = $"puppet {muscles[0].joint.transform.position.ToString("F2")} target {muscles[0].target.position.ToString("F2")} ({muscles[0].name})";
                }
                Plugin.ModLog.LogInfo($"Ride: hero puppet ({when}): mode={m_heroPuppet.mode} state={m_heroPuppet.state} pin={m_heroPuppet.pinWeight:F2} muscle={m_heroPuppet.muscleWeight:F2} muscles={(muscles != null ? muscles.Length : 0)} {at}");
            }
            catch (Exception ex)
            {
                Plugin.ModLog.LogWarning("Hero puppet report failed: " + ex.Message);
            }
        }

        // Runs early in the frame, before the game poses its puppets. He lies down and gets up with the ride, on his own ramp.
        private void PoseHero()
        {
            m_heroSolid = false;
            if (m_heroAfterFrame != 0 && Time.frameCount >= m_heroAfterFrame)
            {
                m_heroAfterFrame = 0;
                try
                {
                    var keep = m_heroPuppet;
                    m_heroPuppet = m_heroAfterPuppet;
                    LogHeroPuppet("after");
                    m_heroPuppet = keep;
                    if (m_heroAfterHips != null)
                    {
                        Plugin.ModLog.LogInfo($"Ride: hero after: hips={m_heroAfterHips.position.ToString("F2")} before the ride={m_heroHomeHips.ToString("F2")} (off by {(m_heroAfterHips.position - m_heroHomeHips).magnitude:F2} m)");
                    }
                }
                catch (Exception)
                {
                }
                m_heroAfterPuppet = null;
                m_heroAfterHips = null;
            }
            try
            {
                // He lies down while the ride is on and gets up when it is off, each at his own
                // pace. He is let go only once he is standing at home again, never in the middle of a step.
                bool lieDown = m_ride && m_toggled && !m_switching;
                if (!m_heroReady)
                {
                    m_heroW = 0f;
                    if (!lieDown || !m_camValid || m_rideWoman == null)
                    {
                        return;
                    }
                    if (Time.unscaledTime < m_heroNextTry)
                    {
                        return;
                    }
                    Character hero = FindHero(m_camPos);
                    if (hero == null)
                    {
                        m_heroNextTry = Time.unscaledTime + 3f;
                        // (Looked for again every 3 s: said once a ride, and once on the screen.)
                        if (!m_noHeroTold)
                        {
                            m_noHeroTold = true;
                            Plugin.ModLog.LogInfo("Ride: no hero found (no male character has the camera), he is not moved.");
                            Notice(Loc.T("No hero found next to the view: she rides without him.", "Рядом с камерой не найден герой: она двигается без него."));
                        }
                        return;
                    }
                    m_hero = hero;
                    if (!CaptureHeroRig(hero))
                    {
                        m_hero = null;
                        if (m_heroNextTry <= Time.unscaledTime)
                        {
                            m_heroNextTry = Time.unscaledTime + 10f;
                        }
                        return;
                    }
                    m_heroPuppet = FindHeroPuppet(hero);
                    CaptureHeroHome(hero);
                    if (m_heroRoot == null)
                    {
                        Plugin.ModLog.LogWarning("Ride: the hero has no root to carry, he is not moved.");
                        m_hero = null;
                        m_heroPuppet = null;
                        m_heroNextTry = Time.unscaledTime + 10f;
                        return;
                    }
                    LogHeroPuppet("before");
                    ControlsCapture(hero);
                    ControlsReport("before the ride");
                    FreezeHeroControllers(hero);
                    FreezeHeroAnimation(hero);
                    HoldHeroPuppet();
                    PenisCapture(hero);
                    m_penisFix = Vector3.zero;
                    WetCapture(hero);
                    MountCapture(hero);
                    ThrustCapture(hero);
                    m_heroFwd = HeroHeadGoal();
                    m_swayFrom = 0f;
                    DriftReset();
                    m_heroAim = m_camPos;
                    m_crotchS = m_herCrotchValid ? m_herCrotch : m_camPos;
                    m_heroFightLogs = 0;
                    m_heroReady = true;
                    m_heroW = 0f;
                    m_heroCheckFrame = Time.frameCount + 30;
                    Plugin.ModLog.LogInfo($"Ride: '{hero.name}' is carried under her.");
                }
                if (m_hero == null || m_heroRoot == null || m_heroPivot == null)
                {
                    ReleaseHero();
                    return;
                }

                KeepHeroPuppet();
                float ramp = Mathf.Max(0.8f, Plugin.PoseRamp.Value);
                m_heroW = Mathf.MoveTowards(m_heroW, lieDown ? 1f : 0f, Mathf.Min(Time.deltaTime, 0.1f) / ramp);
                float weight = Mathf.SmoothStep(0f, 1f, m_heroW);

                // The place he lies at follows the ride's view point, a little slowly so that her bounce does not move him.
                // (When the ride has just been switched, the view point is not known yet: the last one is kept.)
                if (m_camValid)
                {
                    float dtU = Mathf.Min(Time.unscaledDeltaTime, 0.1f);
                    float k = 1f - Mathf.Exp(-dtU / 0.9f);
                    m_heroAim = Vector3.Lerp(m_heroAim, m_camPos, k);
                    if (m_herCrotchValid)
                    {
                        m_crotchS = Vector3.Lerp(m_crotchS, m_herCrotch, k);
                    }
                    float kf = 1f - Mathf.Exp(-dtU / 1.2f);
                    // He is turned about the upright only, by a part of the angle that is left: turning her round on him (the
                    // reverse) swings him round under her, flat on the floor, instead of flipping him over at once.
                    float turnLeft = Vector3.SignedAngle(m_heroFwd, HeroHeadGoal(), Vector3.up);
                    m_heroFwd = (Quaternion.AngleAxis(turnLeft * kf, Vector3.up) * m_heroFwd).normalized;
                }
                if (m_heroAnimator != null && m_heroAnimator.speed != 0f)
                {
                    // Whatever of the game's gave it speed again is told to wait.
                    m_heroAnimator.speed = 0f;
                }
                // How far his hips are to be lifted; from what is under him, how high he lies and how he is bent onto it; his
                // root carried there; then his skeleton posed on it (in that order, so that what is posed is posed where he
                // is this frame, and his hands find her from there), and his arms.
                ThrustStep(weight);
                HeroRestStep(weight);
                MoveHeroRoot(weight);
                HeroPoseLying(weight);
                HeroArmsOut(weight);
                if (!lieDown && m_heroW <= 0.001f)
                {
                    ReleaseHero();
                    Plugin.ModLog.LogInfo("Ride: the hero is back where he was.");
                    return;
                }

                // Where his body lies now, for her to be kept out of.
                bool solid = m_heroSegs.Count > 0;
                foreach (HeroSeg s in m_heroSegs)
                {
                    if (s.a == null || s.b == null)
                    {
                        solid = false;
                        break;
                    }
                    s.pa = s.a.position;
                    s.pb = s.b.position;
                }
                m_heroSolid = solid;

                if (m_heroCheckFrame != 0 && Time.frameCount >= m_heroCheckFrame && weight > 0.99f)
                {
                    m_heroCheckFrame = 0;
                    HeroGoal(out Vector3 wanted, out Quaternion unused);
                    float lowestNow = float.MaxValue;
                    foreach (Transform b in m_heroBones)
                    {
                        if (b != null)
                        {
                            lowestNow = Mathf.Min(lowestNow, b.position.y);
                        }
                    }
                    Plugin.ModLog.LogInfo($"Ride: hero lying: root={m_heroRoot.position.ToString("F2")} (set {m_heroSetPos.ToString("F2")}) hips={m_heroPivot.position.ToString("F2")} wanted hips={wanted.ToString("F2")} (lifted {m_thrustNow * 100f:F1} cm by his thrust) head={(m_heroHead != null ? m_heroHead.position.ToString("F2") : "?")} floor of the sex={m_rideFloorY.ToString("F2")} (her ankles {m_rideGroundY.ToString("F2")}) lowest bone y={lowestNow:F2}");
                    LogHeroPuppet("lying");
                    ControlsReport("lying");
                    if (m_herCrotchValid && m_heroThighL != null && m_heroThighR != null)
                    {
                        // How well the crotches match: the distance between the two points across the floor.
                        Vector3 his = 0.5f * (m_heroThighL.position + m_heroThighR.position);
                        float off = new Vector2(his.x - m_herCrotch.x, his.z - m_herCrotch.z).magnitude;
                        Plugin.ModLog.LogInfo($"Ride: crotch match: hers {m_herCrotch.ToString("F2")} his {his.ToString("F2")}, off by {off * 100f:F1} cm across the floor.");
                    }
                    if (m_rideWoman != null && PenisNow(out Vector3 pb, out Vector3 pt))
                    {
                        // How well his penis meets her: where it is, where her vagina is, and how far off its line she is.
                        Vector3 v = VulvaOf(m_rideWoman);
                        Vector3 dir = (pt - pb).normalized;
                        float along = Vector3.Dot(v - pb, dir);
                        float side = (v - pb - dir * along).magnitude;
                        Plugin.ModLog.LogInfo($"Ride: penis match: base {pb.ToString("F2")} tip {pt.ToString("F2")} (pointing {dir.ToString("F2")}), her vagina {v.ToString("F2")}: {along * 100f:F1} cm along it, {side * 100f:F1} cm off its line; depth {DepthIn(v):F2}.");
                    }
                }
            }
            catch (Exception ex)
            {
                m_heroNextTry = Time.unscaledTime + 5f;
                m_heroWarns++;
                if (m_heroWarns <= 6)
                {
                    Plugin.ModLog.LogWarning("Hero failed: " + (m_heroWarns <= 2 ? ex.ToString() : ex.Message));
                }
                try
                {
                    ReleaseHero();
                }
                catch (Exception)
                {
                    m_heroReady = false;
                    m_hero = null;
                }
            }
        }

        // How much he moves sideways and along his body once he lies, over five seconds, against how much her crotch does:
        // the first thing to read in the log when he seems to sway.
        private void MeasureSway()
        {
            if (m_swayFrom < 0f || m_heroW < 0.99f || m_heroPivot == null)
            {
                return;
            }
            if (m_swayFrom == 0f)
            {
                m_swayFrom = Time.time;
                m_swayMinX = float.NaN;
                return;
            }
            // The first three seconds are left out: he and she are still settling into the pose then.
            if (Time.time - m_swayFrom < 3f)
            {
                return;
            }
            if (float.IsNaN(m_swayMinX))
            {
                // Measured from where he is now and along the way he lies now, both kept for the whole of the measuring. (Before,
                // the axes were the ones of each frame and the positions were taken from the origin of the world: the least
                // turn of the axes, a third of a degree, showed as 8 cm of sway that was not there.)
                m_swayAlong = m_heroFwd;
                m_swaySide = Vector3.Cross(Vector3.up, m_swayAlong);
                m_swayOrigin = m_heroPivot.position;
                m_swayHerOrigin = m_herCrotch;
                m_swayMinX = m_swayMaxX = 0f;
                m_swayMinY = m_swayMaxY = 0f;
                m_herMinX = m_herMaxX = 0f;
                return;
            }
            float x = Vector3.Dot(m_heroPivot.position - m_swayOrigin, m_swaySide);
            float y = Vector3.Dot(m_heroPivot.position - m_swayOrigin, m_swayAlong);
            float hx = Vector3.Dot(m_herCrotch - m_swayHerOrigin, m_swaySide);
            m_swayMinX = Mathf.Min(m_swayMinX, x);
            m_swayMaxX = Mathf.Max(m_swayMaxX, x);
            m_swayMinY = Mathf.Min(m_swayMinY, y);
            m_swayMaxY = Mathf.Max(m_swayMaxY, y);
            m_herMinX = Mathf.Min(m_herMinX, hx);
            m_herMaxX = Mathf.Max(m_herMaxX, hx);
            if (Time.time - m_swayFrom >= 8f)
            {
                Plugin.ModLog.LogInfo($"Ride: hero sway over 5 s: sideways {(m_swayMaxX - m_swayMinX) * 100f:F1} cm, along his body {(m_swayMaxY - m_swayMinY) * 100f:F1} cm; her crotch sideways {(m_herMaxX - m_herMinX) * 100f:F1} cm.");
                m_swayFrom = -1f;
            }
        }

        // Runs last in the frame, with the camera: whatever the game did to his root since it was set (its own scripts follow
        // his hips with it) is undone, and his physical body is put on it again.
        private void HeroFinal()
        {
            if (!m_heroReady || m_heroRoot == null || !m_heroSet || m_heroSetFrame != Time.frameCount)
            {
                return;
            }
            try
            {
                float moved = (m_heroRoot.position - m_heroSetPos).magnitude;
                if (moved > 0.05f && m_heroFightLogs < 4)
                {
                    m_heroFightLogs++;
                    Plugin.ModLog.LogInfo($"Ride: the game moved the hero's root {moved:F2} m after it was set; put back.");
                }
                MeasureDrift();
                ApplyHeroRoot();
                PenisSee();
                CondomStep(Time.unscaledDeltaTime);
                MeasurePenisRest();
                CameraFovStep();
                // The physics scene is told now, not at its next step, so that contact checks made before then see him lying.
                Physics.SyncTransforms();
                MeasureSway();
                // (How thick he is under the places he rests on, by his colliders now on his skeleton, and how he lies:
                // HeroBody.cs.)
                HeroRestMeasure();
                HeroRestReport();
            }
            catch (Exception ex)
            {
                if (WarnOnce(ref m_finalWarns, ref m_finalQuiet))
                {
                    Plugin.ModLog.LogWarning("Hero final pose failed: " + ex.Message);
                }
            }
        }
        // Which way his head points as he lies: the way she faces, or the other way when she rides him reversed.
        private Vector3 HeroHeadGoal()
        {
            // (On a place he lies along it, whichever way she faces: turned round, it is she who turns. Places.cs)
            if (m_carryOn && m_place != null)
            {
                PlaceFrame(m_place, out Vector3 _, out Vector3 along);
                return along;
            }
            bool away = string.Equals(Plugin.RideFacing.Value, "Away", StringComparison.OrdinalIgnoreCase);
            return away ? -m_rideFwd : m_rideFwd;
        }
    }
}
