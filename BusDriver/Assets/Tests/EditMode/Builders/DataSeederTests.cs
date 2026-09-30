using System.Collections.Generic;
using BusDriver.Core.Data;
using BusDriver.Editor.Builders;
using BusDriver.UI.Theme;
using NUnit.Framework;
using UnityEditor;

namespace BusDriver.Tests.EditMode.Builders {
    // DataSeeder creates missing Data assets and never overwrites one (§4.15, T-M1-13). Seeds into
    // a scratch folder, without adopting into GameRootConfig, so the real Data/ is never touched.
    public class DataSeederTests {
        const string Root = "Assets/_DataSeederTest";

        [SetUp]
        public void SetUp() {
            AssetDatabase.DeleteAsset(Root);
        }

        [TearDown]
        public void TearDown() {
            AssetDatabase.DeleteAsset(Root);
        }

        [Test]
        public void SeedsEveryMissingAsset() {
            List<string> created = DataSeeder.SeedMissing(Root, false);
            foreach (Seed seed in DataSeeder.All()) {
                string path = Root + "/" + seed.RelativePath;
                CollectionAssert.Contains(created, path);
                Assert.IsNotNull(AssetDatabase.LoadAssetAtPath(path, seed.Type), path);
            }
            CollectionAssert.IsEmpty(DataSeeder.SeedMissing(Root, false), "a second run creates nothing");
        }

        [Test]
        public void AHandModifiedAssetSurvivesReseeding() {
            DataSeeder.SeedMissing(Root, false);
            UITheme theme = AssetDatabase.LoadAssetAtPath<UITheme>(Root + "/" + UIThemeSeed.RelativePath);
            theme.roles[0].size = 321f;
            AudioConfig audio = AssetDatabase.LoadAssetAtPath<AudioConfig>(Root + "/" + AudioSeed.ConfigRelativePath);
            audio.masterVolumeParameter = "HandEdited";
            EditorUtility.SetDirty(theme);
            EditorUtility.SetDirty(audio);
            AssetDatabase.SaveAssets();

            DataSeeder.SeedMissing(Root, false);

            Assert.AreEqual(321f, AssetDatabase.LoadAssetAtPath<UITheme>(Root + "/" + UIThemeSeed.RelativePath).roles[0].size);
            Assert.AreEqual("HandEdited", AssetDatabase.LoadAssetAtPath<AudioConfig>(Root + "/" + AudioSeed.ConfigRelativePath).masterVolumeParameter);
        }

        [Test]
        public void TheRealConfigHasAdoptedEverySeed() {
            GameRootConfig config = AssetDatabase.LoadAssetAtPath<GameRootConfig>(GameRootConfig.AssetPath);
            Assert.IsNotNull(config);
            Assert.IsNotNull(config.soundLibrary, "soundLibrary");
            Assert.IsNotNull(config.audioConfig, "audioConfig");
            Assert.IsNotNull(config.uiTheme, "uiTheme");
            Assert.IsNotNull(config.mixer, "mixer");
            Assert.IsNotNull(config.inputActions, "inputActions");
            Assert.IsNotNull(config.Route(RouteSeed.RouteId), "routes: " + RouteSeed.RouteId);
        }
    }
}
