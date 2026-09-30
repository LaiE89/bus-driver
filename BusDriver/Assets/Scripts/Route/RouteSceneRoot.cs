using System.Collections.Generic;
using BusDriver.Gameplay.World;
using UnityEngine;

namespace BusDriver.Gameplay.Route {
    // The root of a route scene (§4.3): the lists the night needs from the world. It is not an
    // ISceneRoot; ShiftContext receives it through AttachRoute. The route builder fills it in.
    public sealed class RouteSceneRoot : MonoBehaviour {
        [Tooltip("Every stop, in route order")]
        [SerializeField] BusStop[] stops = new BusStop[0];
        [Tooltip("Where the bus starts the night: position, and +Z along the route")]
        [SerializeField] Transform busSpawn;
        [Tooltip("The MVP's pooled waiting riders, until the manifest arrives (T-M2-07)")]
        [SerializeField] LegacyRiderSpawner riders;
        [Tooltip("Components in this scene that ShiftContext binds (IGameBindable / IShiftBindable)")]
        [SerializeField] MonoBehaviour[] bindables = new MonoBehaviour[0];

        public IReadOnlyList<BusStop> Stops { get { return stops; } }
        public Transform BusSpawn { get { return busSpawn; } }
        public LegacyRiderSpawner Riders { get { return riders; } }
        public IReadOnlyList<MonoBehaviour> Bindables { get { return bindables; } }
    }
}
