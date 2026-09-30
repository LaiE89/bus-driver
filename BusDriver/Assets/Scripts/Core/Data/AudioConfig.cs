using System;
using UnityEngine;
using UnityEngine.Audio;

namespace BusDriver.Core.Data {
    // How AudioService reaches the mixer (GameRootConfig.mixer; §4.8, §4.12): group → mixer group,
    // snapshot → mixer snapshot, and the exposed volume parameters (in dB). Entries left empty fall back to the
    // mixer's Master group, which is what happens until the [HUMAN] mixer groups exist (T-M1-10).
    [CreateAssetMenu(menuName = "Bus Driver/Audio Config", fileName = "AudioConfig")]
    public sealed class AudioConfig : ScriptableObject {
        [Serializable]
        public struct GroupRoute {
            public AudioGroup group;
            public AudioMixerGroup mixerGroup;
            [Tooltip("Exposed volume parameter that controls this group, e.g. SfxVolume for every SFX child")]
            public string volumeParameter;
        }

        [Serializable]
        public struct SnapshotRoute {
            public AudioSnapshot snapshot;
            [Tooltip("The snapshot's name in the mixer")]
            public string snapshotName;
        }

        public string masterVolumeParameter = "MasterVolume";
        public GroupRoute[] groups = new GroupRoute[0];
        public SnapshotRoute[] snapshots = {
            new SnapshotRoute { snapshot = AudioSnapshot.Default, snapshotName = "Default" },
            new SnapshotRoute { snapshot = AudioSnapshot.Earplugs, snapshotName = "Earplugs" },
            new SnapshotRoute { snapshot = AudioSnapshot.Tunnel, snapshotName = "Tunnel" },
            new SnapshotRoute { snapshot = AudioSnapshot.Blackout, snapshotName = "Blackout" },
        };

        public bool TryGetGroup(AudioGroup group, out GroupRoute route) {
            for (int i = 0; i < groups.Length; i++) {
                if (groups[i].group == group) {
                    route = groups[i];
                    return true;
                }
            }
            route = default(GroupRoute);
            return false;
        }

        public string SnapshotName(AudioSnapshot snapshot) {
            for (int i = 0; i < snapshots.Length; i++) {
                if (snapshots[i].snapshot == snapshot) {
                    return snapshots[i].snapshotName;
                }
            }
            return snapshot.ToString();
        }
    }
}
