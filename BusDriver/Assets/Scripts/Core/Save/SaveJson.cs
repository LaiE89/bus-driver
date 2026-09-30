using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace BusDriver.Core.Save {
    // The one JSON configuration for save files (§4.9): no type names, unknown members ignored,
    // enums by name (so renaming a saved enum member needs a migration), indented.
    public static class SaveJson {
        public static JsonSerializerSettings CreateSettings() {
            JsonSerializerSettings settings = new JsonSerializerSettings {
                TypeNameHandling = TypeNameHandling.None,
                MissingMemberHandling = MissingMemberHandling.Ignore,
                Formatting = Formatting.Indented,
                // writtenUtc stays the string it was written as
                DateParseHandling = DateParseHandling.None,
            };
            settings.Converters.Add(new StringEnumConverter());
            return settings;
        }

        public static JsonSerializer CreateSerializer() {
            return JsonSerializer.Create(CreateSettings());
        }
    }
}
