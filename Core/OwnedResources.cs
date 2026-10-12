using System;
using System.Collections.Generic;

namespace CMAScenes
{
    // Tracks only resources allocated by this plugin, never borrowed game textures or fonts.
    internal sealed class OwnedResources<T> where T : class
    {
        private readonly HashSet<T> owned = new(ReferenceEqualityComparer.Instance);
        private readonly Action<T> release;
        internal int Count => owned.Count;
        internal OwnedResources(Action<T> release) { this.release = release; }
        internal T Add(T item) { if (item != null) owned.Add(item); return item; }
        internal void Clear()
        {
            var snapshot = new List<T>(owned); owned.Clear();
            List<Exception> failures = null;
            foreach (T item in snapshot)
                try { release(item); } catch (Exception ex) { (failures ??= new()).Add(ex); }
            if (failures != null) throw new AggregateException(failures);
        }
    }
}
