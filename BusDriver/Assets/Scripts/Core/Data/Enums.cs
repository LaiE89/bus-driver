using System;

namespace BusDriver.Core.Data {
    // The shared enums of ROADMAP §4.8. Values are explicit and append-only: data assets store the
    // number, and save files store the name, so neither may ever be reused or renamed.
    public enum TellId : int {
        None = 0, HeadTrack = 10, Stillness = 11, EyesWide = 12, WhisperLean = 20, MouthWhisper = 21, JawStretch = 22,
        MimicFlicker = 30, MimicReveal = 31, TelegraphStand = 40, AngelWeep = 60, AngelReach = 61,
        DecoyNodOff = 50, DecoyPhoneGlow = 51, DecoyMutter = 52, DecoyFacingBackwards = 53, DecoyHoodUp = 54
    }
    public enum ReactionId : int { None = 0, Flinch = 1, LookAround = 2, TurnToCamera = 3 }
    public enum PassengerPose : int { Standing = 0, Seated = 1 }
    public enum DecoyKind : int { None = 0, NodOff = 1, PhoneGlow = 2, Mutter = 3, FacingBackwards = 4, HoodUp = 5 }
    public enum ThreatStage : int { Dormant = 0, Unsettled = 1, Aggressive = 2, Lethal = 3 }
    public enum ThreatCondition : int { Always = 0, Observed = 1, ObservedByCctv = 2, AttentionOnRoad = 3, PlayerOnFoot = 4 }
    [Flags] public enum ObserverKinds : int { None = 0, Cctv = 1, Driver = 2, OnFoot = 4, Mirror = 8 }
    public enum DeathCause : int { None = 0, MonsterKill = 1, SanityZero = 2, Fall = 3, Abandoned = 4 }
    public enum SanityTier : int { T0 = 0, T1 = 1, T2 = 2, T3 = 3, T4 = 4 }
    public enum ScareTier : int { Ambient = 0, Startle = 1, Monster = 2, Kill = 3 }
    public enum ScareStepKind : int {
        PlaySound = 1, ShowOverlay = 2, CameraShake = 3, FlickerCabinLights = 4, CctvStatic = 5, LockInput = 6,
        ForceHomeView = 7, CutToCctv = 8, ShowScareHead = 9, AllPassengersReact = 10, CabinLightsOff = 11,
        Blackout = 12, Wait = 13, HandsOverCamera = 14
    }
    public enum StopKind : int { Depot = 0, FarmGate = 1, GasStation = 2, Campground = 3, Church = 4, Clinic = 5, Trailhead = 6, Terminus = 7 }
    public enum SideProfile : int { Forest = 0, Rockface = 1, Drop = 2, CliffDrop = 3, Water = 4, TunnelWall = 5 }
    public enum BlockerKind : int { FallenTree = 0, FenceRoadClosed = 1, ConcreteBarriers = 2, CollapsedBridge = 3, Gate = 4 }
    public enum AudioGroup : int { Music = 0, Ambience = 1, SfxBus = 2, SfxCabin = 3, SfxWorld = 4, Voice = 5, Scares = 6, Ui = 7 }
    public enum AudioSnapshot : int { Default = 0, Earplugs = 1, Tunnel = 2, Blackout = 3 }
    public enum InputContext : int { None = 0, Menu = 1, Driving = 2, OnFoot = 3, Screen = 4, Cinematic = 5 }

    // Added with the save models (T-M1-03, D54)
    public enum ArrivalRating : int { None = 0, Early = 1, OnTime = 2, Late = 3, Missed = 4 }
    public enum ScareIntensity : int { Full = 0, Reduced = 1 }
    // A SoundDefinition's spatial blend (T-M1-09, D60)
    public enum SoundSpatial : int { TwoD = 0, ThreeD = 1 }
    // Mirrors UnityEngine.FullScreenMode's values; save models hold no Unity types (§4.9)
    public enum WindowMode : int { ExclusiveFullScreen = 0, FullScreenWindow = 1, MaximizedWindow = 2, Windowed = 3 }
}
