using System.Collections.Generic;
using UnityEngine;

namespace BusDriver.Core.Data {
    // Every SoundDefinition the game can play (§4.8), looked up by id
    [CreateAssetMenu(menuName = "Bus Driver/Sound Library", fileName = "SoundLibrary")]
    public sealed class SoundLibrary : ScriptableObject {
        public List<SoundDefinition> sounds = new List<SoundDefinition>();

        Dictionary<string, SoundDefinition> byId;

        public bool TryGet(string id, out SoundDefinition definition) {
            if (byId == null || byId.Count != sounds.Count) {
                Index();
            }
            return byId.TryGetValue(id ?? "", out definition);
        }

        // Rebuilt when the list changes size (editing in the Inspector during play)
        public void Index() {
            byId = new Dictionary<string, SoundDefinition>();
            for (int i = 0; i < sounds.Count; i++) {
                SoundDefinition definition = sounds[i];
                if (definition != null && !string.IsNullOrEmpty(definition.id) && !byId.ContainsKey(definition.id)) {
                    byId.Add(definition.id, definition);
                }
            }
        }
    }
}
