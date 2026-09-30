using System.Collections.Generic;
using BusDriver.Core.Data;
using BusDriver.Core.Util;
using BusDriver.Editor.Builders;
using BusDriver.Gameplay.Views;
using BusDriver.Gameplay.World;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace BusDriver.Tests.EditMode.Builders {
    // T-M2-06: every environment kind has a logic prefab and a view, and Place() puts them
    // together the way the route and menu builders will
    public class EnvironmentPrefabTests {
        readonly List<GameObject> placed = new List<GameObject>();

        [TearDown]
        public void TearDown() {
            foreach (GameObject go in placed) {
                if (go != null) {
                    Object.DestroyImmediate(go);
                }
            }
            placed.Clear();
        }

        GameObject Place(string kind) {
            GameObject instance = EnvironmentPrefabBuilder.Place(kind, null, Vector3.zero, Quaternion.identity, 99);
            placed.Add(instance);
            return instance;
        }

        [Test]
        public void EveryKind_HasItsLogicPrefabAndView() {
            EnvironmentViewSet set = EnvironmentPrefabBuilder.ViewSet;
            List<string> kinds = EnvironmentKinds.All();
            Assert.AreEqual(33, kinds.Count, "10 blockers, 8 stops, 2 lamps, 6 signs, 2 guardrail parts, 2 trees, a rock, 2 buildings");
            foreach (string kind in kinds) {
                Assert.IsNotNull(set.Resolve(kind), kind + " has no view");
                GameObject instance = Place(kind);
                EnvironmentPiece piece = instance.GetComponent<EnvironmentPiece>();
                Assert.IsNotNull(piece, kind);
                Assert.AreEqual(kind, piece.Kind);
                Assert.IsNotNull(piece.View, kind + ": no view was instantiated in the slot");
                Assert.IsNotNull(piece.View.GetComponentInChildren<Renderer>(), kind + ": the view renders nothing");
            }
        }

        [Test]
        public void Lamps_DriveTheirViewGlow() {
            List<string> lamps = new List<string> { EnvironmentKinds.StreetLamp, EnvironmentKinds.TunnelLamp };
            foreach (StopKind stop in System.Enum.GetValues(typeof(StopKind))) {
                lamps.Add(EnvironmentKinds.Stop(stop));
            }
            foreach (string kind in lamps) {
                GameObject instance = Place(kind);
                LightFlicker flicker = instance.GetComponent<LightFlicker>();
                Assert.IsNotNull(flicker, kind + " has no LightFlicker");
                Assert.AreEqual(1, flicker.LightCount, kind);
                SerializedProperty glows = new SerializedObject(flicker).FindProperty("emissiveViews");
                Assert.Greater(glows.arraySize, 0, kind + ": the flicker drives no emissive view");
                Assert.IsTrue(glows.GetArrayElementAtIndex(0).objectReferenceValue is IEmissiveView, kind);
                Light light = instance.GetComponentInChildren<Light>();
                Transform viewAnchor = FindDeep(instance.GetComponent<EnvironmentPiece>().View.transform, EnvironmentPrefabBuilder.LightAnchorName);
                Assert.IsNotNull(viewAnchor, kind + ": the view has no Anchor_Light");
                Assert.Less(Vector3.Distance(light.transform.position, viewAnchor.position), 1e-3f, kind + ": the light isn't at the view's Anchor_Light");
                Assert.IsFalse(light.transform.IsChildOf(instance.GetComponent<EnvironmentPiece>().ViewSlot), kind + ": the light is in the view");
            }
            Assert.AreEqual(FlickerMode.Unstable, Place(EnvironmentKinds.TunnelLamp).GetComponent<LightFlicker>().Mode);
            Assert.AreEqual(FlickerMode.Subtle, Place(EnvironmentKinds.Stop(StopKind.Church)).GetComponent<LightFlicker>().Mode);
        }

        [Test]
        public void Stops_HaveABusStopAndKeepTheBoardingZoneClear() {
            foreach (StopKind stop in System.Enum.GetValues(typeof(StopKind))) {
                string kind = EnvironmentKinds.Stop(stop);
                GameObject instance = Place(kind);
                Assert.IsNotNull(instance.GetComponent<BusStop>(), kind + " has no BusStop");
                // A.4: x 1.8–4.4 m, ±12 m along the road, clear of anything standing up
                Bounds zone = new Bounds();
                zone.SetMinMax(new Vector3(EnvironmentPrefabBuilder.StopClearMinX, 0.1f, -EnvironmentPrefabBuilder.StopClearHalfLength),
                               new Vector3(EnvironmentPrefabBuilder.StopClearMaxX, 3.5f, EnvironmentPrefabBuilder.StopClearHalfLength));
                foreach (Renderer renderer in instance.GetComponentsInChildren<Renderer>()) {
                    Assert.IsFalse(renderer.bounds.Intersects(zone), $"{kind}: {renderer.name} stands in the boarding zone ({renderer.bounds})");
                }
                foreach (Collider collider in instance.GetComponentsInChildren<Collider>()) {
                    Assert.IsFalse(collider.bounds.Intersects(zone), $"{kind}: {collider.name} blocks the boarding zone");
                }
            }
        }

        [Test]
        public void Blockers_CloseTheirStubWithAContainmentCollider() {
            foreach (BlockerKind blocker in System.Enum.GetValues(typeof(BlockerKind))) {
                foreach (BlockerVariant variant in System.Enum.GetValues(typeof(BlockerVariant))) {
                    string kind = EnvironmentKinds.Blocker(blocker, variant);
                    BoxCollider box = Place(kind).GetComponentInChildren<BoxCollider>();
                    Assert.IsNotNull(box, kind + " has no collider");
                    Assert.AreEqual(Tags.Containment, box.tag, kind);
                    Assert.AreEqual(Layers.World, box.gameObject.layer, kind);
                    Assert.GreaterOrEqual(box.bounds.size.x, EnvironmentPrefabBuilder.BlockerWidth, kind + " doesn't span the 8 m stub");
                }
            }
        }

        [Test]
        public void TreesAndRocks_HaveNoColliders_AndTreesCullAtThreePercent() {
            foreach (string kind in new[] { EnvironmentKinds.Tree(0), EnvironmentKinds.Tree(1), EnvironmentKinds.RockChunk }) {
                Assert.IsNull(Place(kind).GetComponentInChildren<Collider>(), kind + " has a collider");
            }
            for (int variant = 0; variant < EnvironmentKinds.TreeVariants; variant++) {
                GameObject tree = Place(EnvironmentKinds.Tree(variant));
                LODGroup group = tree.GetComponentInChildren<LODGroup>();
                Assert.IsNotNull(group);
                LOD[] lods = group.GetLODs();
                Assert.AreEqual(0.03f, lods[lods.Length - 1].screenRelativeTransitionHeight, 1e-4f, "the last LOD should cull at 3 %");
                foreach (Renderer renderer in tree.GetComponentsInChildren<Renderer>()) {
                    foreach (Material material in renderer.sharedMaterials) {
                        Assert.IsTrue(material.enableInstancing, $"{material.name} on a tree isn't GPU-instanced");
                    }
                    Assert.IsFalse(renderer.gameObject.isStatic, "trees are instanced, not static-batched");
                }
            }
        }

        // §4.17: static batching turns small emissive props magenta
        [Test]
        public void EmissiveParts_AreNeverStatic() {
            foreach (string kind in EnvironmentKinds.All()) {
                foreach (Renderer renderer in Place(kind).GetComponentsInChildren<Renderer>()) {
                    bool emissive = renderer.sharedMaterial != null && renderer.sharedMaterial.IsKeywordEnabled("_EMISSION");
                    if (emissive || renderer.GetComponent<TMP_Text>() != null) {
                        Assert.IsFalse(renderer.gameObject.isStatic, $"{kind}: {renderer.name} is emissive (or text) but static");
                    }
                }
            }
        }

        static Transform FindDeep(Transform root, string name) {
            if (root.name == name) {
                return root;
            }
            foreach (Transform child in root) {
                Transform found = FindDeep(child, name);
                if (found != null) {
                    return found;
                }
            }
            return null;
        }
    }
}
