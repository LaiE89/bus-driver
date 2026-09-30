using System;
using System.Collections.Generic;
using BusDriver.Core.Data;
using UnityEditor;
using UnityEngine;

namespace BusDriver.Editor.Builders {
    // Seed data for Data/Looks/look01..look12 (§2.6, §4.8, T-M3-01): twelve greybox looks that
    // differ in body value as well as hue, so they still read apart on the greyscale CCTV feed
    // (Appendix A.1), and in silhouette through the accessory and the height scale.
    public static class LookSeed {
        public const string Folder = "Looks";
        public const int Count = 12;

        struct Spec {
            public Color Body;
            public Color Head;
            public float Height;
            public LookAccessory Accessory;
        }

        static readonly Spec[] Specs = {
            new Spec { Body = new Color(0.20f, 0.28f, 0.45f), Head = new Color(0.85f, 0.72f, 0.60f), Height = 1.00f, Accessory = LookAccessory.Cap },
            new Spec { Body = new Color(0.55f, 0.15f, 0.12f), Head = new Color(0.55f, 0.40f, 0.30f), Height = 0.97f, Accessory = LookAccessory.None },
            new Spec { Body = new Color(0.75f, 0.72f, 0.62f), Head = new Color(0.90f, 0.78f, 0.66f), Height = 1.03f, Accessory = LookAccessory.Scarf },
            new Spec { Body = new Color(0.18f, 0.35f, 0.20f), Head = new Color(0.42f, 0.30f, 0.22f), Height = 1.02f, Accessory = LookAccessory.Backpack },
            new Spec { Body = new Color(0.35f, 0.35f, 0.37f), Head = new Color(0.80f, 0.65f, 0.52f), Height = 0.96f, Accessory = LookAccessory.Glasses },
            new Spec { Body = new Color(0.12f, 0.12f, 0.13f), Head = new Color(0.70f, 0.55f, 0.45f), Height = 1.05f, Accessory = LookAccessory.LongCoat },
            new Spec { Body = new Color(0.62f, 0.50f, 0.20f), Head = new Color(0.60f, 0.45f, 0.35f), Height = 0.98f, Accessory = LookAccessory.None },
            new Spec { Body = new Color(0.45f, 0.30f, 0.50f), Head = new Color(0.88f, 0.75f, 0.65f), Height = 1.01f, Accessory = LookAccessory.Cap },
            new Spec { Body = new Color(0.85f, 0.85f, 0.88f), Head = new Color(0.50f, 0.36f, 0.28f), Height = 0.99f, Accessory = LookAccessory.Backpack },
            new Spec { Body = new Color(0.30f, 0.20f, 0.12f), Head = new Color(0.78f, 0.62f, 0.50f), Height = 1.04f, Accessory = LookAccessory.Scarf },
            new Spec { Body = new Color(0.15f, 0.40f, 0.50f), Head = new Color(0.66f, 0.50f, 0.40f), Height = 0.95f, Accessory = LookAccessory.Glasses },
            new Spec { Body = new Color(0.50f, 0.50f, 0.30f), Head = new Color(0.92f, 0.80f, 0.70f), Height = 1.00f, Accessory = LookAccessory.LongCoat },
        };

        public static string Id(int index) {
            return "look" + (index + 1).ToString("00");
        }

        public static string RelativePath(int index) {
            return Folder + "/" + Id(index) + ".asset";
        }

        public static IEnumerable<Seed> Seeds() {
            for (int i = 0; i < Count; i++) {
                int index = i;
                yield return Seed.Of<PassengerLookDefinition>(RelativePath(index), look => Fill(look, index));
            }
        }

        static void Fill(PassengerLookDefinition look, int index) {
            Spec spec = Specs[index];
            look.id = Id(index);
            look.greybox = new GreyboxLook {
                bodyColor = spec.Body,
                headColor = spec.Head,
                heightScale = spec.Height,
                accessory = spec.Accessory,
            };
            // artView is the artists' (Phase B); the seed never sets it
        }

        // Appends every seeded look the config doesn't list yet, then keeps the list in id order;
        // never removes one
        public static void Adopt(string root, GameRootConfig config) {
            List<PassengerLookDefinition> looks = new List<PassengerLookDefinition>();
            if (config.looks != null) {
                foreach (PassengerLookDefinition look in config.looks) {
                    if (look != null) {
                        looks.Add(look);
                    }
                }
            }
            for (int i = 0; i < Count; i++) {
                PassengerLookDefinition look = AssetDatabase.LoadAssetAtPath<PassengerLookDefinition>(root + "/" + RelativePath(i));
                if (look != null && !looks.Contains(look)) {
                    looks.Add(look);
                }
            }
            looks.Sort((a, b) => string.CompareOrdinal(a.id, b.id));
            config.looks = looks.ToArray();
        }
    }
}
