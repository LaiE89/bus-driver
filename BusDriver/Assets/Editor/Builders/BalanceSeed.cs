using BusDriver.Core.Data;
using UnityEditor;
using UnityEngine;

namespace BusDriver.Editor.Builders {
    // Seed data for Data/Balance/Balance (§4.8, T-M3-05). BalanceConfig's field initialisers are
    // the §2 first-pass numbers, so a fresh instance is the seed; Fill copies one over the asset so
    // a Reseed puts every field back.
    public static class BalanceSeed {
        public const string RelativePath = "Balance/Balance.asset";

        public static void Fill(BalanceConfig asset) {
            BalanceConfig fresh = ScriptableObject.CreateInstance<BalanceConfig>();
            string name = asset.name;
            EditorJsonUtility.FromJsonOverwrite(EditorJsonUtility.ToJson(fresh), asset);
            asset.name = name;
            Object.DestroyImmediate(fresh);
        }

        public static void Adopt(string root, GameRootConfig config) {
            if (config.balance == null) {
                config.balance = AssetDatabase.LoadAssetAtPath<BalanceConfig>(root + "/" + RelativePath);
            }
        }
    }
}
