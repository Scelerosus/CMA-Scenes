using System;
using Assets._ReusableScripts.CuchiCuchi.Ropa;
using UnityEngine;

namespace CMAScenes
{
    internal static class ClothingMaterials
    {
        internal static Il2CppSystem.Collections.Generic.List<SlotDeMaterialDeRopa> Snapshot(PiezaDeRopaBase piece)
        {
            var copy = new Il2CppSystem.Collections.Generic.List<SlotDeMaterialDeRopa>();
            var source = piece?.materialesData;
            if (source == null) return copy;
            int count = source.Cast<Il2CppSystem.Collections.Generic.IReadOnlyCollection<SlotDeMaterialDeRopa>>().Count;
            for (int i = 0; i < count; i++)
            {
                var slot = source[i]?.Clone();
                if (slot == null) continue;
                slot.field_Public_Boolean_0 = true;
                copy.Add(slot);
            }
            return copy;
        }

        internal static SlotDeMaterialDeRopa Create(string id, int index, Color color)
        {
            if (string.IsNullOrWhiteSpace(id) || index < 0) throw new ArgumentException("Invalid clothing material slot.");
            var slot = new SlotDeMaterialDeRopa();
            slot.SetMaterialID(id);
            slot.materialSlot = index;
            // The native loader rejects slots that do not request material instances.
            slot.field_Public_Boolean_0 = true;
            slot.color = color;
            return slot;
        }
    }
}
