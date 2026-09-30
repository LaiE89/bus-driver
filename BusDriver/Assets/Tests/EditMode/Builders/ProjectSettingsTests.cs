using BusDriver.Core.Util;
using BusDriver.Editor.Builders;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEditorInternal;

namespace BusDriver.Tests.EditMode.Builders {
    // ROADMAP §4.16 as ProjectSettingsBuilder writes it (T-M1-13). The expectations are spelled
    // out here from the table, not taken from the builder.
    public class ProjectSettingsTests {
        static readonly string[] ExpectedNames = {
            "Bus", "BusInterior", "Passenger", "World", "Occluder", "Zone", "Player", "ScareFx",
            "MimicHideCam1", "MimicHideCam2", "MimicHideCam3", "PlayerAvatar",
        };

        [Test]
        public void Layers8To19HaveTheRoadmapNames() {
            for (int i = 0; i < ExpectedNames.Length; i++) {
                Assert.AreEqual(ExpectedNames[i], LayerMask.LayerToName(8 + i), "layer " + (8 + i));
                Assert.AreEqual(8 + i, LayerMask.NameToLayer(ExpectedNames[i]));
                Assert.AreEqual(ExpectedNames[i], Layers.NameOf(8 + i));
            }
        }

        [Test]
        public void TheCollisionMatrixMatchesTheRoadmap() {
            // The only pairs among 8–19 that collide
            int[,] pairs = {
                { Layers.Bus, Layers.World },
                { Layers.Player, Layers.World },
                { Layers.Player, Layers.BusInterior },
                { Layers.Zone, Layers.Bus },
            };
            for (int a = Layers.First; a <= Layers.Last; a++) {
                for (int b = Layers.First; b <= Layers.Last; b++) {
                    bool expected = false;
                    for (int p = 0; p < pairs.GetLength(0); p++) {
                        expected |= (pairs[p, 0] == a && pairs[p, 1] == b) || (pairs[p, 0] == b && pairs[p, 1] == a);
                    }
                    Assert.AreEqual(expected, !Physics.GetIgnoreLayerCollision(a, b),
                        $"{LayerMask.LayerToName(a)} ↔ {LayerMask.LayerToName(b)}");
                }
            }
        }

        [Test]
        public void NonCollidingLayersCollideWithNothingAtAll() {
            int[] silent = {
                Layers.Passenger, Layers.Occluder, Layers.ScareFx, Layers.MimicHideCam1, Layers.MimicHideCam2,
                Layers.MimicHideCam3, Layers.PlayerAvatar,
            };
            foreach (int layer in silent) {
                for (int other = 0; other < 32; other++) {
                    Assert.IsTrue(Physics.GetIgnoreLayerCollision(layer, other), $"{LayerMask.LayerToName(layer)} collides with layer {other}");
                }
            }
            // Zone only ever meets the bus
            for (int other = 0; other < 32; other++) {
                Assert.AreEqual(other != Layers.Bus, Physics.GetIgnoreLayerCollision(Layers.Zone, other), "Zone ↔ layer " + other);
            }
        }

        [Test]
        public void ContainmentTagAndPlayerSettings() {
            CollectionAssert.Contains(InternalEditorUtility.tags, Tags.Containment);
            // Unity 6 reads the options enum: None reloads both the domain and the scene (D24, D62)
            Assert.IsFalse(EditorSettings.enterPlayModeOptionsEnabled, "Enter Play Mode Options must be off (D24)");
            Assert.AreEqual(EnterPlayModeOptions.None, EditorSettings.enterPlayModeOptions, "domain and scene must reload on Play (D24)");
            Assert.AreEqual(ProjectSettingsBuilder.CompanyName, PlayerSettings.companyName);
            Assert.AreEqual(ProjectSettingsBuilder.ProductName, PlayerSettings.productName);
            Assert.AreEqual(ProjectSettingsBuilder.Version, PlayerSettings.bundleVersion);
        }

        [Test]
        public void TheBuildListIsTheGeneratedScenes() {
            EditorBuildSettingsScene[] actual = EditorBuildSettings.scenes;
            Assert.AreEqual(SceneIds.BuildList.Count, actual.Length);
            for (int i = 0; i < actual.Length; i++) {
                Assert.AreEqual(SceneIds.BuildList[i], actual[i].path);
                Assert.IsTrue(actual[i].enabled);
            }
        }
    }
}
