using System.Collections.Generic;
using UnityEngine;

namespace BusDriver.Gameplay.World {
    [System.Serializable]
    public class ObjectPool {
        public string id;
        public GameObject prefab;
        [Tooltip("How many instances to create up front")]
        public int prewarm = 8;
    }

    // Generic GameObject pool. Configure named pools in the inspector, then Spawn/Despawn by id.
    public class ObjectPooling : MonoBehaviour {
        [SerializeField] ObjectPool[] pools;

        readonly Dictionary<string, ObjectPool> poolById = new Dictionary<string, ObjectPool>();
        readonly Dictionary<string, Queue<GameObject>> available = new Dictionary<string, Queue<GameObject>>();
        readonly Dictionary<GameObject, string> instancePoolId = new Dictionary<GameObject, string>();

        void Awake() {
            InitializePools();
        }

        void InitializePools() {
            poolById.Clear();
            available.Clear();
            instancePoolId.Clear();
            if (pools == null) {
                return;
            }
            for (int i = 0; i < pools.Length; i++) {
                ObjectPool pool = pools[i];
                if (pool == null || string.IsNullOrEmpty(pool.id) || pool.prefab == null) {
                    continue;
                }
                if (poolById.ContainsKey(pool.id)) {
                    Debug.LogWarning($"ObjectPooling: duplicate pool id '{pool.id}' on {name}");
                    continue;
                }
                poolById[pool.id] = pool;
                available[pool.id] = new Queue<GameObject>();
                int count = Mathf.Max(0, pool.prewarm);
                for (int n = 0; n < count; n++) {
                    available[pool.id].Enqueue(CreateInstance(pool));
                }
            }
        }

        GameObject CreateInstance(ObjectPool pool) {
            GameObject spawned = Instantiate(pool.prefab, transform);
            spawned.name = pool.prefab.name;
            spawned.SetActive(false);
            instancePoolId[spawned] = pool.id;
            return spawned;
        }

        public bool HasPool(string id) {
            return !string.IsNullOrEmpty(id) && poolById.ContainsKey(id);
        }

        public IEnumerable<string> PoolIds {
            get { return poolById.Keys; }
        }

        public GameObject Spawn(string id, Vector3 position, Quaternion rotation, Transform parent = null) {
            if (!HasPool(id)) {
                Debug.LogWarning($"ObjectPooling: no pool named '{id}'");
                return null;
            }
            Queue<GameObject> queue = available[id];
            GameObject instance = queue.Count > 0 ? queue.Dequeue() : CreateInstance(poolById[id]);
            instance.transform.SetParent(parent, false);
            instance.transform.SetPositionAndRotation(position, rotation);
            instance.SetActive(true);
            return instance;
        }

        public bool Despawn(GameObject instance) {
            if (instance == null) {
                return false;
            }
            if (!instancePoolId.TryGetValue(instance, out string id) || !available.ContainsKey(id)) {
                return false;
            }
            instance.SetActive(false);
            instance.transform.SetParent(transform, false);
            available[id].Enqueue(instance);
            return true;
        }

    #if UNITY_EDITOR
        void Update() {
            if (!Application.isPlaying || pools == null) {
                return;
            }
            for (int i = 0; i < pools.Length; i++) {
                ObjectPool pool = pools[i];
                if (pool == null || string.IsNullOrEmpty(pool.id) || !available.ContainsKey(pool.id)) {
                    continue;
                }
                Queue<GameObject> queue = available[pool.id];
                while (queue.Count < pool.prewarm) {
                    queue.Enqueue(CreateInstance(pool));
                }
            }
        }
    #endif
    }
}
