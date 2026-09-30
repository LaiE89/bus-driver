using System;
using System.Collections.Generic;
using UnityEngine;

namespace BusDriver.Core.Data {
    [Serializable]
    public sealed class EnvironmentViewEntry {
        [Tooltip("An EnvironmentKinds key, e.g. Blocker.FallenTree.A")]
        public string kind = "";
        [Tooltip("The generated greybox view. Owned by the builder, which refreshes it on every Build All (D76)")]
        public GameObject greyboxView;
        [Tooltip("The artist's view (Phase B). Used instead of the greybox view when assigned")]
        public GameObject artView;
    }

    // Kind key → view (§4.8, §4.14, Data/Views/Environment). Every environment logic prefab
    // instantiates its view from here when the route or menu scene is built: art if assigned,
    // greybox otherwise, so the art swap is a data change plus a rebuild.
    public sealed class EnvironmentViewSet : ScriptableObject {
        public List<EnvironmentViewEntry> entries = new List<EnvironmentViewEntry>();

        public EnvironmentViewEntry Find(string kind) {
            for (int i = 0; i < entries.Count; i++) {
                if (entries[i] != null && entries[i].kind == kind) {
                    return entries[i];
                }
            }
            return null;
        }

        public GameObject Resolve(string kind, bool preferArt = true) {
            EnvironmentViewEntry entry = Find(kind);
            if (entry == null) {
                return null;
            }
            if (preferArt && entry.artView != null) {
                return entry.artView;
            }
            return entry.greyboxView;
        }
    }

    // The environment kind keys (§4.8). Dotted PascalCase, as the §4.8 examples (D53 allows dots).
    public static class EnvironmentKinds {
        public const string StreetLamp = "Lamp.Street";
        public const string TunnelLamp = "Lamp.Tunnel";
        public const string GuardrailSegment = "Guardrail.Segment";
        public const string GuardrailEndCap = "Guardrail.EndCap";
        public const string RockChunk = "Rock.Chunk";
        public const string DepotBuilding = "Building.Depot";
        public const string LodgeBuilding = "Building.Lodge";
        public const int TreeVariants = 2;

        public static string Blocker(BlockerKind kind, BlockerVariant variant) {
            return "Blocker." + kind + "." + variant;
        }

        public static string Stop(StopKind kind) {
            return "Stop." + kind;
        }

        public static string Sign(SignKind kind) {
            return "Sign." + kind;
        }

        // Variant 0 is A, 1 is B (ProfileBuilder's DressingSpot.Variant)
        public static string Tree(int variant) {
            return "Tree.Conifer." + (variant % TreeVariants == 0 ? "A" : "B");
        }

        // Every kind the builders place, in a fixed order
        public static List<string> All() {
            List<string> all = new List<string>();
            foreach (BlockerKind kind in Enum.GetValues(typeof(BlockerKind))) {
                foreach (BlockerVariant variant in Enum.GetValues(typeof(BlockerVariant))) {
                    all.Add(Blocker(kind, variant));
                }
            }
            foreach (StopKind kind in Enum.GetValues(typeof(StopKind))) {
                all.Add(Stop(kind));
            }
            all.Add(StreetLamp);
            all.Add(TunnelLamp);
            foreach (SignKind kind in Enum.GetValues(typeof(SignKind))) {
                all.Add(Sign(kind));
            }
            all.Add(GuardrailSegment);
            all.Add(GuardrailEndCap);
            for (int i = 0; i < TreeVariants; i++) {
                all.Add(Tree(i));
            }
            all.Add(RockChunk);
            all.Add(DepotBuilding);
            all.Add(LodgeBuilding);
            return all;
        }
    }
}
