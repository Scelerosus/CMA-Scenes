using System;
using System.Runtime.CompilerServices;

namespace CMAScenes
{
    // Plain arithmetic for the loops that go through some hundred thousand texels. UnityEngine.Mathf here is the game's
    // own, called through the interop layer: every Clamp, Min or Max in such a loop is a call into the game, and they were
    // the better part of what drawing the wet look and the condom cost (one draw took up to 60 ms).
    internal static class Fast
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)] public static float Clamp(float v, float a, float b) { return v < a ? a : v > b ? b : v; }
        [MethodImpl(MethodImplOptions.AggressiveInlining)] public static int Clamp(int v, int a, int b) { return v < a ? a : v > b ? b : v; }
        [MethodImpl(MethodImplOptions.AggressiveInlining)] public static float Clamp01(float v) { return v < 0f ? 0f : v > 1f ? 1f : v; }
        [MethodImpl(MethodImplOptions.AggressiveInlining)] public static float Max(float a, float b) { return a > b ? a : b; }
        public static float Max(float a, float b, float c) { return Max(Max(a, b), c); }
        [MethodImpl(MethodImplOptions.AggressiveInlining)] public static int Max(int a, int b) { return a > b ? a : b; }
        [MethodImpl(MethodImplOptions.AggressiveInlining)] public static float Min(float a, float b) { return a < b ? a : b; }
        [MethodImpl(MethodImplOptions.AggressiveInlining)] public static int Min(int a, int b) { return a < b ? a : b; }
        [MethodImpl(MethodImplOptions.AggressiveInlining)] public static float Abs(float v) { return v < 0f ? -v : v; }
        [MethodImpl(MethodImplOptions.AggressiveInlining)] public static float Sqrt(float v) { return (float)Math.Sqrt(v); }
        [MethodImpl(MethodImplOptions.AggressiveInlining)] public static float Lerp(float a, float b, float t) { return a + (b - a) * Clamp01(t); }
    }
}
