using System.Collections.Generic;
using BusDriver.Core.Data;
using BusDriver.Editor.Builders;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace BusDriver.Tests.EditMode.Builders {
    // §4.14 / A.0: views hold renderers, never a collider or rigidbody; gameplay collision lives on
    // the logic root. Covers every environment view (greybox and art) and every generated logic
    // prefab's View child.
    public class ViewRulesTests {
        static IEnumerable<GameObject> EnvironmentViews() {
            EnvironmentViewSet set = AssetDatabase.LoadAssetAtPath<EnvironmentViewSet>(EnvironmentSeed.AssetPath);
            Assert.IsNotNull(set, EnvironmentSeed.AssetPath + " is missing; run Build All");
            foreach (EnvironmentViewEntry entry in set.entries) {
                if (entry.greyboxView != null) {
                    yield return entry.greyboxView;
                }
                if (entry.artView != null) {
                    yield return entry.artView;
                }
            }
        }

        static IEnumerable<GameObject> LogicPrefabViews() {
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { PrefabBuilder.Folder })) {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
                Transform view = prefab.transform.Find("View");
                if (view != null) {
                    yield return view.gameObject;
                }
            }
        }

        static void AssertNoPhysics(GameObject view, string what, List<string> problems) {
            foreach (Collider collider in view.GetComponentsInChildren<Collider>(true)) {
                problems.Add($"{what}: collider on {collider.name}");
            }
            foreach (Rigidbody body in view.GetComponentsInChildren<Rigidbody>(true)) {
                problems.Add($"{what}: rigidbody on {body.name}");
            }
        }

        [Test]
        public void Views_HoldNoCollidersOrRigidbodies() {
            List<string> problems = new List<string>();
            int count = 0;
            foreach (GameObject view in EnvironmentViews()) {
                AssertNoPhysics(view, view.name, problems);
                count++;
            }
            foreach (GameObject view in LogicPrefabViews()) {
                AssertNoPhysics(view, view.transform.root.name + "/View", problems);
                count++;
            }
            Assert.Greater(count, 30, "found too few views to check");
            Assert.IsEmpty(problems, string.Join("\n", problems));
        }
    }
}
