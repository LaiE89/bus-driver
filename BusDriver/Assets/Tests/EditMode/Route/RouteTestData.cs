using BusDriver.Core.Data;
using BusDriver.Editor.Builders;
using NUnit.Framework;
using UnityEditor;

namespace BusDriver.Tests.EditMode.Route {
    // The committed Route01 asset: the tests check the data the game actually uses (§0.1)
    static class RouteTestData {
        public static RouteDefinition Route01 {
            get {
                RouteDefinition route = AssetDatabase.LoadAssetAtPath<RouteDefinition>(DataSeeder.DataRoot + "/" + RouteSeed.RelativePath);
                Assert.IsNotNull(route, "Data/" + RouteSeed.RelativePath + " is missing; run Build All");
                return route;
            }
        }
    }
}
