using System.Collections.Generic;
using BusDriver.Core.Data;
using BusDriver.Editor.Builders;
using BusDriver.Editor.Validation;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace BusDriver.Tests.EditMode.Builders {
    // The committed content passes the same validation BuildAll runs (§4.15, §4.19)
    public class ContentValidationTests {
        [Test]
        public void ContentIsValid() {
            List<string> problems = ContentValidator.Validate();
            Assert.IsEmpty(problems, string.Join("\n", problems));
        }

        static RouteDefinition Route01 {
            get {
                RouteDefinition route = AssetDatabase.LoadAssetAtPath<RouteDefinition>(DataSeeder.DataRoot + "/" + RouteSeed.RelativePath);
                Assert.IsNotNull(route, "Data/" + RouteSeed.RelativePath + " is missing; run Build All");
                return route;
            }
        }

        static List<string> Check(RouteDefinition route) {
            List<string> problems = new List<string>();
            ContentValidator.CheckRoute(route, problems);
            return problems;
        }

        [Test]
        public void Route01IsValid() {
            List<string> problems = Check(Route01);
            Assert.IsEmpty(problems, string.Join("\n", problems));
        }

        // The seeded asset holds the §3 tables (26 segments, 7 stops, 6 stubs, 5 zones, 15 signs)
        [Test]
        public void Route01HoldsTheSection3Tables() {
            RouteDefinition route = Route01;
            Assert.AreEqual("route01", route.id);
            Assert.AreEqual(26, route.segments.Length);
            Assert.AreEqual(7, route.stops.Length);
            Assert.AreEqual(6, route.stubs.Length);
            Assert.AreEqual(5, route.zones.Length);
            Assert.AreEqual(15, route.signs.Length);
            Assert.AreEqual("lodge", route.terminusStopId);
            string[] order = { "farm_gate", "gas_station", "campground", "church", "clinic", "trailhead", "lodge" };
            for (int i = 0; i < order.Length; i++) {
                Assert.AreEqual(order[i], route.stops[i].stopId);
            }
            int chevrons = 0;
            foreach (RouteSign sign in route.signs) {
                if (sign.kind == SignKind.Chevron) {
                    chevrons++;
                    Assert.AreEqual(RouteSide.Left, sign.side);
                }
            }
            Assert.AreEqual(11, chevrons);
            RouteSegment cliff = route.segments[14];
            Assert.AreEqual(SideProfile.CliffDrop, cliff.left);
            Assert.AreEqual(200f, cliff.Length, 0.01f);
        }

        [Test]
        public void AStopOnAnArcFails() {
            RouteDefinition route = Object.Instantiate(Route01);
            try {
                route.stops[0].distance = 460f;   // inside segment 4, an arc
                StringAssert.Contains("isn't on a straight", string.Join("\n", Check(route)));
            }finally {
                Object.DestroyImmediate(route);
            }
        }

        [Test]
        public void StopsOutOfOrderFail() {
            RouteDefinition route = Object.Instantiate(Route01);
            try {
                route.stops[1].distance = 300f;
                StringAssert.Contains("out of route order", string.Join("\n", Check(route)));
            }finally {
                Object.DestroyImmediate(route);
            }
        }

        [Test]
        public void DuplicateAndInvalidStopIdsFail() {
            RouteDefinition route = Object.Instantiate(Route01);
            try {
                route.stops[1].stopId = "farm_gate";
                route.stops[2].stopId = "Camp Ground";
                string all = string.Join("\n", Check(route));
                StringAssert.Contains("listed twice", all);
                StringAssert.Contains("invalid id", all);
            }finally {
                Object.DestroyImmediate(route);
            }
        }
    }
}
