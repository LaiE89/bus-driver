namespace BusDriver.Core.Save {
    // Saves by slot (§4.6). SaveService is the real one; tests can pass a fake.
    public interface ISaveStore {
        // False when the file is missing, from a newer build, or unreadable (and so is its .bak)
        bool TryLoad<T>(SaveSlot slot, out T data);
        void Save<T>(SaveSlot slot, T data);
        void Delete(SaveSlot slot);
        bool Exists(SaveSlot slot);
    }

    public static class SaveStoreExtensions {
        // The stored value, or a fresh default when there's nothing usable
        public static T LoadOrNew<T>(this ISaveStore store, SaveSlot slot) where T : class, new() {
            T data;
            if (store.TryLoad(slot, out data) && data != null) {
                return data;
            }
            return new T();
        }
    }
}
