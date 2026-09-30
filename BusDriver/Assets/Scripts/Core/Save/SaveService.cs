using System;
using System.Globalization;
using System.IO;
using System.Text;
using BusDriver.Core.Util;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace BusDriver.Core.Save {
    // JSON save files with envelopes, atomic writes and a .bak (§4.9). The root folder is
    // injected: Application.persistentDataPath in the game, a temporary folder in tests.
    public sealed class SaveService : ISaveStore {
        public const string TempSuffix = ".tmp";
        public const string BackupSuffix = ".bak";
        public const string CorruptInfix = ".corrupt-";

        enum ReadResult { Ok, Corrupt, Refused }

        static readonly UTF8Encoding Utf8NoBom = new UTF8Encoding(false);

        readonly SaveMigrations migrations;
        readonly JsonSerializer serializer;
        readonly JsonSerializerSettings settings;
        readonly Func<DateTime> utcNow;

        // Test hook: runs after the .tmp is on disk and before it replaces the file, so a test
        // can throw here to simulate a crash between the two
        internal Action<string> AfterTempWritten;

        public string Root { get; }
        public string BuildLabel { get; set; }

        public SaveService(string root, SaveMigrations migrations, string buildLabel, Func<DateTime> utcNow = null) {
            if (string.IsNullOrEmpty(root)) {
                throw new ArgumentException("save root is empty", nameof(root));
            }
            Root = root;
            this.migrations = migrations ?? SaveMigrations.CreateDefault();
            BuildLabel = buildLabel;
            this.utcNow = utcNow ?? (() => DateTime.UtcNow);
            settings = SaveJson.CreateSettings();
            serializer = JsonSerializer.Create(settings);
        }

        public string PathOf(SaveSlot slot) {
            return Path.Combine(Root, SaveSlots.FileName(slot));
        }

        public bool Exists(SaveSlot slot) {
            return File.Exists(PathOf(slot));
        }

        // The .bak goes too: a deleted run must not come back from its backup
        public void Delete(SaveSlot slot) {
            string path = PathOf(slot);
            DeleteIfExists(path);
            DeleteIfExists(path + BackupSuffix);
            DeleteIfExists(path + TempSuffix);
            Log.Info(LogCat.Save, "deleted " + SaveSlots.FileName(slot));
        }

        public void Save<T>(SaveSlot slot, T data) {
            SaveEnvelope envelope = new SaveEnvelope {
                saveVersion = migrations.Current(slot),
                kind = SaveSlots.Kind(slot),
                writtenUtc = utcNow().ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture),
                build = BuildLabel,
                data = JObject.FromObject(data, serializer),
            };
            string json = JsonConvert.SerializeObject(envelope, settings);
            Directory.CreateDirectory(Root);
            string path = PathOf(slot);
            string tmp = path + TempSuffix;
            using (FileStream stream = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None)) {
                byte[] bytes = Utf8NoBom.GetBytes(json);
                stream.Write(bytes, 0, bytes.Length);
                // Through the OS cache to the disk, so a power cut can't leave a half-written file
                stream.Flush(true);
            }
            if (AfterTempWritten != null) {
                AfterTempWritten(tmp);
            }
            if (File.Exists(path)) {
                File.Replace(tmp, path, path + BackupSuffix);
            }else {
                File.Move(tmp, path);
            }
        }

        public bool TryLoad<T>(SaveSlot slot, out T data) {
            data = default(T);
            string path = PathOf(slot);
            if (!File.Exists(path)) {
                return false;
            }
            string problem;
            ReadResult result = TryRead(slot, path, out data, out problem);
            if (result == ReadResult.Ok) {
                return true;
            }
            if (result == ReadResult.Refused) {
                // A newer build wrote it, or a migration is missing: leave the file exactly as it is
                Log.Warn(LogCat.Save, $"{SaveSlots.FileName(slot)} not loaded: {problem}");
                return false;
            }
            string backup = path + BackupSuffix;
            string backupProblem = "no backup";
            if (File.Exists(backup) && TryRead(slot, backup, out data, out backupProblem) == ReadResult.Ok) {
                Log.Warn(LogCat.Save, $"{SaveSlots.FileName(slot)} is unreadable ({problem}); loaded its backup");
                return true;
            }
            data = default(T);
            string corrupt = CorruptPath(path);
            File.Move(path, corrupt);
            Log.Error(LogCat.Save, $"{SaveSlots.FileName(slot)} is unreadable ({problem}; backup: {backupProblem}). "
                + $"Kept it as {Path.GetFileName(corrupt)} and fell back to defaults");
            return false;
        }

        ReadResult TryRead<T>(SaveSlot slot, string path, out T data, out string problem) {
            data = default(T);
            problem = null;
            try {
                SaveEnvelope envelope;
                using (JsonTextReader reader = new JsonTextReader(new StringReader(File.ReadAllText(path, Encoding.UTF8)))) {
                    reader.DateParseHandling = DateParseHandling.None;
                    envelope = serializer.Deserialize<SaveEnvelope>(reader);
                }
                if (envelope == null || envelope.data == null) {
                    problem = "no envelope data";
                    return ReadResult.Corrupt;
                }
                if (envelope.kind != SaveSlots.Kind(slot)) {
                    problem = $"kind '{envelope.kind}' in a {SaveSlots.Kind(slot)} file";
                    return ReadResult.Corrupt;
                }
                int current = migrations.Current(slot);
                if (envelope.saveVersion > current) {
                    problem = $"saveVersion {envelope.saveVersion} is newer than this build's {current}";
                    return ReadResult.Refused;
                }
                if (envelope.saveVersion < current) {
                    if (!migrations.CanMigrate(slot, envelope.saveVersion)) {
                        problem = $"no migration path from v{envelope.saveVersion} to v{current}";
                        return ReadResult.Refused;
                    }
                    migrations.Migrate(slot, envelope.data, envelope.saveVersion);
                }
                data = envelope.data.ToObject<T>(serializer);
                return ReadResult.Ok;
            }catch (JsonException e) {
                problem = e.GetType().Name + " - " + e.Message;
                return ReadResult.Corrupt;
            }catch (FormatException e) {
                problem = e.GetType().Name + " - " + e.Message;
                return ReadResult.Corrupt;
            }catch (InvalidCastException e) {
                problem = e.GetType().Name + " - " + e.Message;
                return ReadResult.Corrupt;
            }catch (ArgumentException e) {
                problem = e.GetType().Name + " - " + e.Message;
                return ReadResult.Corrupt;
            }
        }

        string CorruptPath(string path) {
            string stamp = utcNow().ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture);
            string candidate = path + CorruptInfix + stamp;
            for (int n = 2; File.Exists(candidate); n++) {
                candidate = path + CorruptInfix + stamp + "-" + n;
            }
            return candidate;
        }

        static void DeleteIfExists(string path) {
            if (File.Exists(path)) {
                File.Delete(path);
            }
        }
    }
}
