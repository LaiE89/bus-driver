using System;
using System.IO;

namespace BusDriver.Tests.EditMode.Save {
    // A throwaway save root, so tests never touch the real persistentDataPath
    sealed class SaveTestFolder : IDisposable {
        public string Root { get; }

        public SaveTestFolder() {
            Root = Path.Combine(Path.GetTempPath(), "busdriver-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Root);
        }

        public string PathOf(string fileName) {
            return Path.Combine(Root, fileName);
        }

        public string[] Files() {
            string[] paths = Directory.GetFiles(Root);
            for (int i = 0; i < paths.Length; i++) {
                paths[i] = Path.GetFileName(paths[i]);
            }
            Array.Sort(paths, StringComparer.Ordinal);
            return paths;
        }

        public void Dispose() {
            if (Directory.Exists(Root)) {
                Directory.Delete(Root, true);
            }
        }
    }
}
