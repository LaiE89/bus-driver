using System.Collections.Generic;
using BusDriver.Core.Data;
using BusDriver.Gameplay.World;
using UnityEngine;

namespace BusDriver.Gameplay.Route {
    // The root of a route scene (§4.3): the lists the night needs from the world. It is not an
    // ISceneRoot; ShiftContext receives it through AttachRoute. RouteBuilder fills it in.
    public sealed class RouteSceneRoot : MonoBehaviour {
        [Tooltip("The data this scene was built from; RouteTracker builds its RoutePath from it")]
        [SerializeField] RouteDefinition route;
        [Tooltip("Every stop, in route order")]
        [SerializeField] BusStop[] stops = new BusStop[0];
        [Tooltip("Where the bus starts the night: position, and +Z along the route")]
        [SerializeField] Transform busSpawn;
        [Tooltip("Every zone volume (tunnel, bridge, rumble strips, the cliff's fall zone)")]
        [SerializeField] ZoneVolume[] zones = new ZoneVolume[0];
        [SerializeField] FallZone[] fallZones = new FallZone[0];
        [SerializeField] KillPlane killPlane;
        [Tooltip("Where the fall camera watches the bus go over (§2.14)")]
        [SerializeField] Transform fallCamAnchor;
        [Tooltip("Components in this scene that ShiftContext binds (IGameBindable / IShiftBindable)")]
        [SerializeField] MonoBehaviour[] bindables = new MonoBehaviour[0];

        public RouteDefinition Route { get { return route; } }
        public IReadOnlyList<BusStop> Stops { get { return stops; } }
        public Transform BusSpawn { get { return busSpawn; } }
        public IReadOnlyList<ZoneVolume> Zones { get { return zones; } }
        public IReadOnlyList<FallZone> FallZones { get { return fallZones; } }
        public KillPlane KillPlane { get { return killPlane; } }
        public Transform FallCamAnchor { get { return fallCamAnchor; } }
        public IReadOnlyList<MonoBehaviour> Bindables { get { return bindables; } }

        // The stop with this id, or null
        public BusStop Stop(string stopId) {
            for (int i = 0; i < stops.Length; i++) {
                if (stops[i] != null && stops[i].StopId == stopId) {
                    return stops[i];
                }
            }
            return null;
        }
    }
}
