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
        // (Kept from frame to frame instead of made anew in every one.)
        private static readonly Vector3[] s_footPos = new Vector3[2];
        private static readonly Quaternion[] s_footRot = new Quaternion[2];

        private static void Apply(Woman w, float poseW, float shakeW, Motion mo)
        {
            float phase = mo.phase;
            BeginAll(w);

            if (poseW <= 0.0005f && shakeW <= 0.0005f)
            {
                ReleaseAll(w);
                return;
            }

            // The character's own frame, from where the thighs are.
            Vector3 right = Vector3.ProjectOnPlane(w.legs[1].upper.t.position - w.legs[0].upper.t.position, Vector3.up).normalized;
            Vector3 fwd = Vector3.Cross(right, Vector3.up);

            Vector3[] footPos = s_footPos;
            Quaternion[] footRot = s_footRot;
            for (int i = 0; i < 2; i++)
            {
                footPos[i] = w.legs[i].end.t.position;
                footRot[i] = w.legs[i].end.t.rotation;
            }

            // The bounce is not a pure sine: it drops quicker than it rises, and the pelvis tilts a quarter ahead. The
            // smoother the motion is set to be, the less lopsided that is.
            float skew = Mathf.Lerp(0.6f, 0.3f, Plugin.Smoothness.Value);
            float s = Mathf.Sin(phase);
            float c = Mathf.Cos(phase);
            float sB = Skew(phase, skew);
            float cB = Skew(phase + Mathf.PI * 0.5f, skew);
            float dir = mo.circleDir;
            float depth = mo.jitter * mo.amp;

            // Drop and push back the hips, then move them: up and down (bounce), side to side (sway) and round (circle), and
            // let her weight sit on one leg and then the other.
            Vector3 hipMove =
                Vector3.up * (sB * Plugin.BounceHeight.Value * depth * (mo.bounce + 0.6f * mo.circle))
                + right * ((s * Plugin.SwayWidth.Value * mo.sway + c * Plugin.CircleRadius.Value * mo.circle) * mo.amp)
                + fwd * (s * dir * Plugin.CircleRadius.Value * mo.circle * mo.amp)
                + right * (mo.weight * 0.022f);
            Vector3 hipOffset = Vector3.down * (Glide(w, G.Squat, Plugin.SquatDepth.Value) * poseW) - fwd * (Glide(w, G.HipBack, Plugin.HipBack.Value) * poseW) + hipMove * shakeW;

            // Hinge forward at the pelvis and let the spine follow. The pelvis rocks with each move and the rock travels up the
            // spine as a wave: each level answers the one below a moment later and against it, and shows less of it.
            float lean = (Glide(w, G.Lean, Plugin.LeanDegrees.Value) + mo.leanExtra) * poseW;
            float pitch = Plugin.TiltDegrees.Value * depth * (cB * mo.bounce + s * dir * mo.circle) * shakeW;
            float roll = Plugin.RollDegrees.Value * (s * mo.sway + c * mo.circle) * shakeW + mo.weight * 2.4f * shakeW;
            float wave = Plugin.TiltDegrees.Value * depth * mo.bounce * shakeW;
            float waistPitch = -wave * 0.34f * Skew(phase + Mathf.PI * 0.5f - 0.75f, skew);
            float sp1Pitch = -wave * 0.16f * Skew(phase + Mathf.PI * 0.5f - 1.5f, skew);
            float sp2Pitch = -wave * 0.06f * Skew(phase + Mathf.PI * 0.5f - 2.2f, skew);

            Ease(w, ref hipOffset, ref pitch, ref roll, ref lean);
            w.sHip.Offset(hipOffset);

            // The arch: the pelvis tips forward and the spine curves back the same amount in total, so the chest stays at
            // the angle the lean gives it while the bottom is pushed up and back.
            float arch = Glide(w, G.Arch, Plugin.Arch.Value) * poseW;
            w.sPelvis.t.rotation = Quaternion.AngleAxis(roll, fwd) * Quaternion.AngleAxis(lean * 0.5f + pitch + arch, right) * w.sPelvis.t.rotation;
            w.sWaist.t.rotation = Quaternion.AngleAxis(-roll * 0.5f, fwd) * Quaternion.AngleAxis(lean * 0.2f + waistPitch - arch * 0.55f, right) * w.sWaist.t.rotation;
            w.sSp1.t.rotation = Quaternion.AngleAxis(lean * 0.15f + sp1Pitch - arch * 0.3f, right) * w.sSp1.t.rotation;
            w.sSp2.t.rotation = Quaternion.AngleAxis(lean * 0.15f + sp2Pitch - arch * 0.15f, right) * w.sSp2.t.rotation;
            w.sPelvis.Commit();
            w.sWaist.Commit();
            w.sSp1.Commit();
            w.sSp2.Commit();

            // The feet go wide apart with the toes turned out, and the knees are pushed out over them: the stance of a
            // twerk. The feet stay planted there while the hips drop and move.
            float legW = Mathf.Max(poseW, shakeW);
            float stance = Glide(w, G.Stance, Plugin.Stance.Value) * poseW;
            float toes = Glide(w, G.Toes, Plugin.ToesOut.Value) * poseW;
            for (int i = 0; i < 2; i++)
            {
                Limb leg = w.legs[i];
                Vector3 outward = i == 0 ? -right : right;
                Ik(leg, footPos[i] + outward * stance, fwd * 0.6f + outward, legW);
                leg.end.t.rotation = Quaternion.AngleAxis((i == 1 ? 1f : -1f) * toes, Vector3.up) * footRot[i];
                leg.end.Commit();
            }

            // Hands: on her knees, on her hips, or left to the game. Where they are is a blend that glides, so that moving them
            // from one place to the other is a movement of the arms and not a jump.
            string hands = Plugin.HandsOn.Value ?? "Knees";
            bool handsNone = string.Equals(hands, "None", StringComparison.OrdinalIgnoreCase);
            bool handsHips = string.Equals(hands, "Hips", StringComparison.OrdinalIgnoreCase);
            float armW = Glide(w, G.HandsAny, handsNone ? 0f : 1f);
            float onKnees = Glide(w, G.HandsKnees, !handsNone && !handsHips ? 1f : 0f);
            float onHips = Glide(w, G.HandsHips, handsHips ? 1f : 0f);
            float onAny = onKnees + onHips;
            if (armW > 0.001f && onAny > 0.001f)
            {
                for (int i = 0; i < 2; i++)
                {
                    Limb arm = w.arms[i];
                    if (arm == null)
                    {
                        continue;
                    }
                    Vector3 outward = i == 0 ? -right : right;
                    // The side of the hip: out from the hip joint and up the flank, tilted with the pelvis as she leans.
                    Vector3 pelvisUp = Quaternion.AngleAxis(lean * 0.5f, right) * Vector3.up;
                    Vector3 hipSpot = w.legs[i].upper.t.position + outward * Plugin.HipWidth.Value + pelvisUp * Plugin.HipHeight.Value - fwd * Plugin.HipForward.Value;
                    Vector3 kneeSpot = w.legs[i].fore.t.position + Vector3.up * 0.06f + fwd * 0.03f;
                    Vector3 target = (hipSpot * onHips + kneeSpot * onKnees) / onAny;
                    Ik(arm, target, outward - fwd * Plugin.ElbowBack.Value, poseW * armW);
                }
            }

            // The soft bones bounce on top of all that, shaken by how the hips really moved.
            Wobble(w, hipOffset, shakeW);
        }
    }
}
