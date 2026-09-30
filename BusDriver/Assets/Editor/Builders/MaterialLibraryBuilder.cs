using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace BusDriver.Editor.Builders {
    // BuildAll step 2 (§4.15): the flat-colour greybox materials (URP Lit, emissive variants) in
    // Generated/Materials, plus the bus hull's physics material. Later builders fetch them by name.
    public static class MaterialLibraryBuilder {
        public const string Folder = BuilderUtil.GeneratedRoot + "/Materials";
        public const string HullPhysicsPath = Folder + "/BusHull.asset";

        struct Spec {
            public string Name;
            public Color BaseColor;
            public Color? Emission;
        }

        // The MVP palette, unchanged (the art swap replaces views, not these)
        static readonly Spec[] Specs = {
            New("Ground", new Color(0.07f, 0.09f, 0.07f)),
            New("Asphalt", new Color(0.13f, 0.13f, 0.14f)),
            New("Kerb", new Color(0.5f, 0.5f, 0.5f)),
            New("LinePaint", new Color(0.9f, 0.9f, 0.85f), new Color(0.9f, 0.9f, 0.8f) * 0.6f),
            New("Post", new Color(0.15f, 0.15f, 0.15f)),
            New("Reflector", new Color(1f, 0.6f, 0.1f), new Color(1f, 0.55f, 0.1f) * 1.5f),
            New("StopPad", new Color(0.8f, 0.7f, 0.1f), new Color(0.8f, 0.7f, 0.1f) * 0.5f),
            New("Wall", new Color(0.3f, 0.28f, 0.27f)),
            New("Building", new Color(0.18f, 0.18f, 0.2f)),
            New("Obstacle", new Color(0.45f, 0.3f, 0.2f)),
            New("BusBody", new Color(0.6f, 0.58f, 0.5f)),
            New("BusInterior", new Color(0.35f, 0.36f, 0.38f)),
            New("Seat", new Color(0.15f, 0.2f, 0.4f)),
            New("Dash", new Color(0.08f, 0.08f, 0.09f)),
            New("Tyre", new Color(0.04f, 0.04f, 0.04f)),
            New("LampWhite", Color.white, new Color(1f, 0.95f, 0.8f) * 3f),
            New("LampRed", new Color(0.8f, 0.05f, 0.05f), new Color(1f, 0.05f, 0.05f) * 2f),
            New("Door", new Color(0.3f, 0.33f, 0.36f)),
            New("Passenger", new Color(0.65f, 0.55f, 0.5f)),
            New("PassengerOdd", new Color(0.25f, 0.2f, 0.22f)),
            New("Player", new Color(0.35f, 0.55f, 0.85f)),
            New("Face", new Color(0.04f, 0.04f, 0.04f)),
            // The PR #5 angel's face, kept for its greybox AngelWeep tell (§4.14)
            New("Face2", new Color(0.934f, 0f, 0f)),
            // Route 1 profiles (§3.4, T-M2-03)
            New("Gravel", new Color(0.3f, 0.29f, 0.27f)),
            New("Rock", new Color(0.24f, 0.23f, 0.22f)),
            New("Water", new Color(0.03f, 0.05f, 0.08f)),
            New("GuardRail", new Color(0.55f, 0.56f, 0.58f)),
        };

        static Spec New(string name, Color baseColor, Color? emission = null) {
            return new Spec { Name = name, BaseColor = baseColor, Emission = emission };
        }

        [MenuItem("Tools/Bus Driver/Builders/Material Library")]
        public static void Build() {
            BuilderUtil.EnsureFolder(Folder);
            foreach (Spec spec in Specs) {
                BuilderUtil.GetOrCreateMaterial(PathOf(spec.Name), spec.BaseColor, spec.Emission);
            }
            HullPhysics();
            AssetDatabase.SaveAssets();
        }

        public static string PathOf(string name) {
            return Folder + "/" + name + ".mat";
        }

        public static IEnumerable<string> Names {
            get {
                foreach (Spec spec in Specs) {
                    yield return spec.Name;
                }
            }
        }

        // Builders run this step first, so a missing material is a builder bug worth failing on
        public static Material Get(string name) {
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(PathOf(name));
            if (mat == null) {
                throw new System.InvalidOperationException($"no generated material '{name}'; run MaterialLibraryBuilder first");
            }
            return mat;
        }

        // Slippery, so the bus scrapes along walls instead of sticking to them
        public static PhysicsMaterial HullPhysics() {
            PhysicsMaterial material = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(HullPhysicsPath);
            if (material == null) {
                BuilderUtil.EnsureFolder(Folder);
                material = new PhysicsMaterial("BusHull");
                AssetDatabase.CreateAsset(material, HullPhysicsPath);
            }
            material.dynamicFriction = 0.25f;
            material.staticFriction = 0.25f;
            material.bounciness = 0f;
            material.frictionCombine = PhysicsMaterialCombine.Minimum;
            EditorUtility.SetDirty(material);
            return material;
        }
    }
}
