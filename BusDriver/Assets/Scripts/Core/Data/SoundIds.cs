namespace BusDriver.Core.Data {
    // Every sound id of ROADMAP Appendix A.3, so code never spells an id by hand. SoundIdsTests
    // checks this list against the appendix, and ContentValidator (T-M1-13) checks each one has a
    // SoundDefinition. Ids are never renamed; a new sound gets a new id (Appendix A.3).
    public static class SoundIds {
        public const string BusEngineLoop = "bus.engine_loop";
        public const string BusHandbrake = "bus.handbrake";
        public const string BusDoorOpen = "bus.door_open";
        public const string BusDoorClose = "bus.door_close";
        public const string BusFareTap = "bus.fare_tap";
        public const string BusStopRequest = "bus.stop_request";
        public const string BusRumbleStrip = "bus.rumble_strip";
        public const string CctvSwitch = "cctv.switch";
        public const string AmbWind = "amb.wind";
        public const string AmbTunnel = "amb.tunnel";
        public const string AmbLampBuzz = "amb.lamp_buzz";
        public const string PaxMutterLoop = "pax.mutter_loop";
        public const string MonTelegraphRumble = "mon.telegraph_rumble";
        public const string MonExpel = "mon.expel";
        public const string MonStarerSting = "mon.starer.sting";
        public const string MonStarerKill = "mon.starer.kill";
        public const string MonGazerSprint = "mon.gazer.sprint";
        public const string MonGazerKill = "mon.gazer.kill";
        public const string MonGuestTweak = "mon.guest.tweak";
        public const string MonGuestSprint = "mon.guest.sprint";
        public const string MonGuestKill = "mon.guest.kill";
        public const string MonMimicTake = "mon.mimic.take";
        public const string MonMimicSprint = "mon.mimic.sprint";
        public const string MonMimicKill = "mon.mimic.kill";
        public const string MonWhisperLoop = "mon.whisper_loop";
        public const string MonWhisperFeedLoop = "mon.whisper_feed_loop";
        public const string ScareStartleSting = "scare.startle_sting";
        public const string HalFootstepsBehind = "hal.footsteps_behind";
        public const string HalDoorChime = "hal.door_chime";
        public const string HalWindowKnock = "hal.window_knock";
        public const string HalStaticBurst = "hal.static_burst";
        public const string HalWhisperDriver = "hal.whisper_driver";
        public const string SanHeartbeatLoop = "san.heartbeat_loop";
        public const string DeathFallWind = "death.fall_wind";
        public const string DeathFallImpact = "death.fall_impact";
        public const string DeathBlackout = "death.blackout";
        public const string UiClick = "ui.click";
        public const string UiMoneyUp = "ui.money_up";
        public const string UiMoneyDown = "ui.money_down";
        public const string UiTypeTick = "ui.type_tick";
        public const string MusGameoverSting = "mus.gameover_sting";

        public static readonly string[] All = {
            BusEngineLoop, BusHandbrake, BusDoorOpen, BusDoorClose, BusFareTap, BusStopRequest, BusRumbleStrip,
            CctvSwitch,
            AmbWind, AmbTunnel, AmbLampBuzz,
            PaxMutterLoop,
            MonTelegraphRumble, MonExpel, MonStarerSting, MonStarerKill,
            MonGazerSprint, MonGazerKill, MonGuestTweak, MonGuestSprint, MonGuestKill,
            MonMimicTake, MonMimicSprint, MonMimicKill,
            MonWhisperLoop, MonWhisperFeedLoop,
            ScareStartleSting,
            HalFootstepsBehind, HalDoorChime, HalWindowKnock, HalStaticBurst, HalWhisperDriver,
            SanHeartbeatLoop, DeathFallWind, DeathFallImpact, DeathBlackout,
            UiClick, UiMoneyUp, UiMoneyDown, UiTypeTick,
            MusGameoverSting,
        };
    }
}
