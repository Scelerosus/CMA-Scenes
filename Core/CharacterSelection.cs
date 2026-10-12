using System.Collections.Generic;

namespace CMAScenes
{
    // Runtime pointers are session keys, never persistent character identities.
    internal sealed class CharacterSelection
    {
        private readonly HashSet<long> active = new();
        internal long? SelectedKey { get; private set; }
        internal void Reconcile(IReadOnlyList<long> nearestFirst)
        {
            active.Clear();
            foreach (long key in nearestFirst) active.Add(key);
            if (SelectedKey.HasValue && active.Contains(SelectedKey.Value)) return;
            SelectedKey = nearestFirst.Count > 0 ? nearestFirst[0] : null;
        }
        internal bool Select(long key)
        {
            if (!active.Contains(key)) return false;
            SelectedKey = key; return true;
        }
    }
}
