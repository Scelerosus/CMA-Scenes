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
        // (With "roll", for her arms: her upper arm is rolled about its own length as well, so that her elbow bends about
        // its own axis. Without it the limb is only swung to where it has to be and keeps whatever roll the game's
        // animation had: with her hand put far from where the animation has it, her arm came out twisted, her elbow
        // bending sideways and her wrist wrung round to make up for it.)
        internal static void Ik(Limb limb, Vector3 target, Vector3 pole, float weight, bool roll = false)
        {
            Transform upper = limb.upper.t, fore = limb.fore.t, end = limb.end.t;
            Quaternion baseU = upper.localRotation;
            Quaternion baseF = fore.localRotation;
            Vector3 s = upper.position, e = fore.position, wrist = end.position;
            float a = (e - s).magnitude;
            float b = (wrist - e).magnitude;
            Vector3 st = target - s;
            float d = Mathf.Clamp(st.magnitude, Mathf.Abs(a - b) + 0.001f, a + b - 0.001f);
            Vector3 u = st.normalized;
            Vector3 reach = s + u * d;
            float x = (a * a - b * b + d * d) / (2f * d);
            float h = Mathf.Sqrt(Mathf.Max(0f, a * a - x * x));
            Vector3 p = pole - u * Vector3.Dot(pole, u);
            p = p.sqrMagnitude < 1e-6f ? Vector3.forward : p.normalized;
            Vector3 elbow = s + u * x + p * h;

            if (roll && !limb.hingeOk)
            {
                // (Only from an arm that is clearly bent: of a straight one the axis cannot be told.)
                Vector3 bent = Vector3.Cross(e - s, wrist - e);
                if (bent.magnitude > a * b * 0.34f)
                {
                    limb.hinge = Quaternion.Inverse(upper.rotation) * bent.normalized;
                    limb.hingeOk = true;
                    Plugin.ModLog.LogInfo($"Ride: {limb.who} {(limb.side == 0 ? "left" : "right")} arm: the axis the elbow bends about was found (the arm bent {Mathf.Asin(Mathf.Clamp01(bent.magnitude / (a * b))) * Mathf.Rad2Deg:F0} deg in the game's animation); the upper arm is rolled so that it bends about it.");
                }
            }
            upper.rotation = Quaternion.FromToRotation(e - s, elbow - s) * upper.rotation;
            if (roll && limb.hingeOk && h > 0.02f)
            {
                Vector3 along = (elbow - s).normalized;
                Vector3 have = Vector3.ProjectOnPlane(upper.rotation * limb.hinge, along);
                Vector3 want = Vector3.ProjectOnPlane(Vector3.Cross(elbow - s, reach - elbow), along);
                if (have.sqrMagnitude > 1e-6f && want.sqrMagnitude > 1e-10f)
                {
                    upper.rotation = Quaternion.AngleAxis(Vector3.SignedAngle(have, want, along), along) * upper.rotation;
                }
            }
            fore.rotation = Quaternion.FromToRotation(end.position - fore.position, reach - fore.position) * fore.rotation;

            upper.localRotation = Quaternion.Slerp(baseU, upper.localRotation, weight);
            fore.localRotation = Quaternion.Slerp(baseF, fore.localRotation, weight);
            limb.upper.Commit();
            limb.fore.Commit();
        }

        // A sine that is not symmetric: with amount 0 it is a plain sine, and the more it is raised the quicker the wave
        // falls and the slower it climbs back, like a body dropping onto its knees and coming up again.
        internal static float Skew(float phase, float amount)
        {
            return Mathf.Sin(phase + amount * Mathf.Sin(phase));
        }

        // The glutes and, if asked for, the chest wobble as soft tissue. Each is a mass on a spring that is shaken by the
        // acceleration of the hips, so it lags behind every bounce and rings on after it, a little differently on each side.
        internal static void Wobble(Woman w, Vector3 hipMotion, float shakeW)
        {
            if (shakeW <= 0.0005f)
            {
                w.jglL.Reset();
                w.jglR.Reset();
                w.jpeL.Reset();
                w.jpeR.Reset();
                w.hipSamples = 0;
                w.hipAcc = Vector3.zero;
                return;
            }
            float dt = Mathf.Clamp(Time.deltaTime, 0.004f, 0.05f);
            if (w.hipSamples >= 1)
            {
                Vector3 vel = (hipMotion - w.lastHip) / dt;
                if (w.hipSamples >= 2)
                {
                    Vector3 acc = (vel - w.lastHipVel) / dt;
                    w.hipAcc = Vector3.Lerp(w.hipAcc, acc, 0.6f);
                }
                w.lastHipVel = vel;
            }
            w.lastHip = hipMotion;
            if (w.hipSamples < 2)
            {
                w.hipSamples++;
            }
            Vector3 shake = Vector3.ClampMagnitude(w.hipAcc, 80f);

            float gain = Plugin.ButtAmplitude.Value / 0.03f * 2.4f;
            w.jglL.Step(shake, gain, dt);
            w.jglR.Step(shake, gain, dt);
            w.glL?.Offset(w.jglL.x);
            w.glR?.Offset(w.jglR.x);
            if (Plugin.ShakeChest.Value)
            {
                // The breasts move a little, and never far from where they are on the body: the setting is how far they may
                // go at most (a fraction of it), the push comes from a half of the hips' shaking, and what goes sideways or
                // forward is cut down, so they bounce on the chest and do not slide off it.
                float reach = ChestReach();
                float chestGain = reach * 68f;
                Vector3 chestShake = Vector3.ClampMagnitude(shake * 0.5f, 25f);
                w.jpeL.Step(chestShake, chestGain, dt);
                w.jpeR.Step(chestShake, chestGain, dt);
                w.peL?.Offset(Hold(w.jpeL.x, reach));
                w.peR?.Offset(Hold(w.jpeR.x, reach));
            }
        }

        // The most the chest may move from where it is on the body, in metres: 40 % of the setting, which is at most 5 cm.
        internal static float ChestReach()
        {
            return Mathf.Min(Plugin.ChestAmplitude.Value, 0.05f) * 0.4f;
        }

        // A displacement held to a limit softly (it slows as it nears it), with little of it sideways or forward.
        private static Vector3 Hold(Vector3 d, float limit)
        {
            d = new Vector3(d.x * 0.35f, d.y, d.z * 0.35f);
            float m = d.magnitude;
            if (m < 1e-6f || limit < 1e-6f)
            {
                return Vector3.zero;
            }
            return d / m * (limit * (float)Math.Tanh(m / limit));
        }

        // Put everything back to what the game animated this frame.
        internal static void BeginAll(Woman w)
        {
            w.sHip.Begin();
            w.sHipRot?.Begin();
            w.sPelvis.Begin();
            w.sWaist.Begin();
            w.sSp1.Begin();
            w.sSp2.Begin();
            w.sHead?.Begin();
            foreach (Limb l in w.legs) { l.upper.Begin(); l.fore.Begin(); l.end.Begin(); }
            foreach (Limb l in w.arms) { if (l != null) { l.upper.Begin(); l.fore.Begin(); l.end.Begin(); BeginExtras(l); } }
            w.glL?.Begin();
            w.glR?.Begin();
            w.peL?.Begin();
            w.peR?.Begin();
        }

        // A setting of the pose, gliding: it follows what it is set to over about the time of a transition, starting and
        // ending gently, so a change of pose, of hands or of a slider is a movement and never a jump.
        internal static float Glide(Woman w, int slot, float goal)
        {
            if (!w.gHas[slot])
            {
                w.gA[slot] = w.gB[slot] = goal;
                w.gHas[slot] = true;
                return goal;
            }
            float tau = Mathf.Max(0.03f, Plugin.PoseRamp.Value * 0.3f);
            float k = 1f - Mathf.Exp(-Mathf.Clamp(Time.deltaTime, 0.004f, 0.1f) / tau);
            w.gA[slot] = Mathf.Lerp(w.gA[slot], goal, k);
            w.gB[slot] = Mathf.Lerp(w.gB[slot], w.gA[slot], k);
            return w.gB[slot];
        }

        // A value brought to its goal the way a well damped spring would bring it: it sets off gently, arrives gently, and if
        // the goal changes on the way (the key pressed again in the middle of a transition) it turns round without a corner.
        // The time is roughly a third of how long the whole move takes. Within a hair of the goal it is the goal.
        internal static float Damp(float current, float goal, ref float velocity, float time, float dt)
        {
            float omega = 2f / Mathf.Max(0.0001f, time);
            float x = omega * dt;
            float decay = 1f / (1f + x + 0.48f * x * x + 0.235f * x * x * x);
            float change = current - goal;
            float temp = (velocity + omega * change) * dt;
            velocity = (velocity - omega * temp) * decay;
            float result = goal + (change + temp) * decay;
            if (Mathf.Abs(result - goal) < 0.002f)
            {
                velocity = 0f;
                return goal;
            }
            return result;
        }

        internal static void ReleaseAll(Woman w)
        {
            w.fHas = false;
            Array.Clear(w.gHas, 0, w.gHas.Length);
            w.sHip.Release();
            w.sHipRot?.Release();
            w.sPelvis.Release();
            w.sWaist.Release();
            w.sSp1.Release();
            w.sSp2.Release();
            w.sHead?.Release();
            foreach (Limb l in w.legs) { l.upper.Release(); l.fore.Release(); l.end.Release(); }
            foreach (Limb l in w.arms) { if (l != null) { l.upper.Release(); l.fore.Release(); l.end.Release(); l.swivelHas = false; l.reachFall = 0f; ReleaseExtras(l); } }
            w.glL?.Release();
            w.glR?.Release();
            w.peL?.Release();
            w.peR?.Release();
        }

        // The bones of an arm besides its three (along its forearm, of its fingers): back to what the game animated this
        // frame, or let go.
        internal static void BeginExtras(Limb l)
        {
            if (l.twists != null)
            {
                foreach (RotSlot tw in l.twists)
                {
                    tw.Begin();
                }
            }
            if (l.fingers != null)
            {
                foreach (RotSlot[] f in l.fingers)
                {
                    foreach (RotSlot b in f)
                    {
                        b.Begin();
                    }
                }
            }
        }

        internal static void ReleaseExtras(Limb l)
        {
            if (l.twists != null)
            {
                foreach (RotSlot tw in l.twists)
                {
                    tw.Release();
                }
            }
            if (l.fingers != null)
            {
                foreach (RotSlot[] f in l.fingers)
                {
                    foreach (RotSlot b in f)
                    {
                        b.Release();
                    }
                }
            }
        }

        // Smoothing: the values that pose her (the lift of the hips, the rock and the roll of the pelvis, the lean) are eased
        // every frame, in two stages, toward what the motion asks for, so that nothing she does moves in steps and the knock of
        // a bounce is rounded off. Smoothness 0 leaves them as they are.
        internal static void Ease(Woman w, ref Vector3 hip, ref float pitch, ref float roll, ref float lean)
        {
            float tau = Plugin.Smoothness.Value * 0.1f;
            if (!w.fHas || tau < 0.002f)
            {
                w.fHipA = w.fHipB = hip;
                w.fPitchA = w.fPitchB = pitch;
                w.fRollA = w.fRollB = roll;
                w.fLeanA = w.fLeanB = lean;
                w.fHas = true;
                return;
            }
            float k = 1f - Mathf.Exp(-Mathf.Clamp(Time.deltaTime, 0.004f, 0.1f) / (tau * 0.5f));
            w.fHipA = Vector3.Lerp(w.fHipA, hip, k);
            w.fHipB = Vector3.Lerp(w.fHipB, w.fHipA, k);
            w.fPitchA = Mathf.Lerp(w.fPitchA, pitch, k);
            w.fPitchB = Mathf.Lerp(w.fPitchB, w.fPitchA, k);
            w.fRollA = Mathf.Lerp(w.fRollA, roll, k);
            w.fRollB = Mathf.Lerp(w.fRollB, w.fRollA, k);
            w.fLeanA = Mathf.Lerp(w.fLeanA, lean, k);
            w.fLeanB = Mathf.Lerp(w.fLeanB, w.fLeanA, k);
            hip = w.fHipB;
            pitch = w.fPitchB;
            roll = w.fRollB;
            lean = w.fLeanB;
        }
    }
}
