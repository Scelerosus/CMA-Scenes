using System.Collections.Generic;
using Assets;
using Assets._ReusableScripts.CuchiCuchi;
using Assets._ReusableScripts.CuchiCuchi.Chars.Ropa.Memorias;
using Assets._ReusableScripts.CuchiCuchi.Ropa;
using UnityEngine;

namespace CMAScenes
{
    // Clothing contexts do not require a skeleton that the posing system can drive.
    internal static class WardrobeCharacters
    {
        internal sealed class Context
        {
            internal long Key;
            internal Character Character;
            internal RopaAdminDeCharacter Wardrobe;
            internal float DistanceSquared;
        }
        private static readonly Dictionary<long, Context> contexts = new();
        private static readonly List<Context> nearby = new();
        private static readonly CharacterSelection selection = new();
        private static float nextScan;
        private static float lastRadius;
        internal static IReadOnlyList<Context> Nearby { get { Refresh(); return nearby; } }
        internal static Character Selected
        {
            get
            {
                Refresh();
                if (!selection.SelectedKey.HasValue || !contexts.TryGetValue(selection.SelectedKey.Value, out var context)) return null;
                return context.Character;
            }
        }
        internal static void Select(long key) { selection.Select(key); }
        internal static void Clear()
        {
            contexts.Clear(); nearby.Clear(); selection.Reconcile(new List<long>()); nextScan = 0f;
        }
        private static void Refresh()
        {
            float radius = Mathf.Clamp(Plugin.ClothingRadius.Value, 1f, 50f);
            if (Time.unscaledTime < nextScan && radius == lastRadius) return;
            nextScan = Time.unscaledTime + 1f; lastRadius = radius;
            nearby.Clear();
            Camera camera = Camera.main;
            if (camera == null) { contexts.Clear(); selection.Reconcile(new List<long>()); return; }
            var active = new HashSet<long>();
            foreach (Character character in Resources.FindObjectsOfTypeAll<Character>())
            {
                if (character == null || !character.gameObject.scene.IsValid() || !character.gameObject.activeInHierarchy || character.sexo == Sexo.masculino) continue;
                float distance = (character.transform.position - camera.transform.position).sqrMagnitude;
                if (distance > radius * radius) continue;
                long key = character.Pointer.ToInt64();
                if (!contexts.TryGetValue(key, out var context) || context.Character != character || context.Wardrobe == null || context.Wardrobe.manager == null)
                {
                    var wardrobe = character.GetComponentInChildren<RopaAdminDeCharacter>(true);
                    if (wardrobe == null || wardrobe.manager == null) continue;
                    context = new Context { Key = key, Character = character, Wardrobe = wardrobe };
                    contexts[key] = context;
                }
                active.Add(key); context.DistanceSquared = distance; nearby.Add(context);
            }
            var gone = new List<long>();
            foreach (long key in contexts.Keys) if (!active.Contains(key)) gone.Add(key);
            foreach (long key in gone) contexts.Remove(key);
            nearby.Sort((a, b) => { int d = a.DistanceSquared.CompareTo(b.DistanceSquared); return d != 0 ? d : a.Key.CompareTo(b.Key); });
            var keys = new List<long>(nearby.Count);
            foreach (Context context in nearby) keys.Add(context.Key);
            selection.Reconcile(keys);
        }
    }
}
