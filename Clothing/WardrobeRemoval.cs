using System.Collections.Generic;
using Assets._ReusableScripts.CuchiCuchi.Ropa;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CMAScenes
{
    internal static class WardrobeRemoval
    {
        internal static List<SkinnedMeshRenderer> Renderers(PiezaDeRopaBase piece)
        {
            var result = new List<SkinnedMeshRenderer>();
            if (piece == null) return result;
            if (piece.skinnedMeshRenderer != null) result.Add(piece.skinnedMeshRenderer);
            if (piece.m_subSkins != null)
                for (int i = 0; i < piece.m_subSkins.Count; i++)
                {
                    var renderer = piece.m_subSkins[i]?.skinnedMeshRenderer;
                    if (renderer != null && !result.Contains(renderer)) result.Add(renderer);
                }
            return result;
        }

        internal static bool SynchronizeRemoved(IRopaManager manager, string id, bool addedByMod, Object by, List<SkinnedMeshRenderer> knownRenderers = null)
        {
            if (manager.Usando(id, true)) return false;
            // Native undress can leave an invisible registered garment in the Outfit menu.
            if (addedByMod && !Remove(manager, id, by, knownRenderers)) return false;
            return true;
        }

        internal static bool Remove(IRopaManager manager, string id, Object by, List<SkinnedMeshRenderer> knownRenderers = null)
        {
            var pieces = manager.piezasPuestasPorId;
            PiezaDeRopaBase piece = null;
            if (pieces != null && pieces.TryGetValue(id, out piece) && piece == null) return false;
            // Garment renderers can be reparented onto the armature and outlive the removed wardrobe object.
            var renderers = Renderers(piece);
            if (knownRenderers != null) foreach (var renderer in knownRenderers)
                if (renderer != null && !renderers.Contains(renderer)) renderers.Add(renderer);
            if (piece != null && !manager.RemovePieza(id, true, by)) return false;
            var retained = new HashSet<int>();
            pieces = manager.piezasPuestasPorId;
            if (pieces != null)
            {
                var ids = new Il2CppSystem.Collections.Generic.List<string>();
                manager.ObtenerPiezasIDs(ids.Cast<Il2CppSystem.Collections.Generic.ICollection<string>>(), false);
                for (int i = 0; i < ids.Count; i++)
                    if (pieces.TryGetValue(ids[i], out var remaining))
                        foreach (var renderer in Renderers(remaining)) retained.Add(renderer.GetInstanceID());
            }
            foreach (var renderer in renderers)
            {
                if (renderer == null || retained.Contains(renderer.GetInstanceID())) continue;
                renderer.enabled = false;
                Object.Destroy(renderer);
            }
            return pieces == null || !pieces.ContainsKey(id);
        }

        internal static bool Cleanup(IRopaManager manager, HashSet<string> owned, HashSet<string> loading, Object by,
            Dictionary<string, List<SkinnedMeshRenderer>> knownRenderers = null)
        {
            foreach (string id in new List<string>(owned))
            {
                List<SkinnedMeshRenderer> renderers = null;
                knownRenderers?.TryGetValue(id, out renderers);
                if (!loading.Contains(id) && Remove(manager, id, by, renderers)) owned.Remove(id);
            }
            return owned.Count == 0;
        }
    }
}
