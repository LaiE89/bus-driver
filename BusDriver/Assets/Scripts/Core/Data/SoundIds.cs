namespace BusDriver.Core.Data {
    // Every sound id of ROADMAP Appendix A.3, so code never spells an id by hand. SoundIdsTests
    // checks this list against the appendix, and ContentValidator (T-M1-13) checks each one has a
    // SoundDefinition. Ids are never renamed; a new sound gets a new id (Appendix A.3).
    public static class SoundIds {
        public const string BusEngineLoop = "bus.engine_loop";
        public const string BusAccel = "bus.accel";
        public const string BusDecel = "bus.decel";
        public const string BusHandbrake = "bus.handbrake";
        public const string BusDoorOpen = "bus.door_open";
        public const string BusDoorClose = "bus.door_close";
        public const string BusFareTap = "bus.fare_tap";
        public const string BusStopRequest = "bus.stop_request";
        public const string BusRumbleStrip = "bus.rumble_strip";
        public const string BusCrashMinor = "bus.crash_minor";
        public const string BusCrashMajor = "bus.crash_major";
        public const string BusHorn = "bus.horn";
        public const string CctvSwitch = "cctv.switch";
        public const string CctvStaticLoop = "cctv.static_loop";
        public const string AmbWind = "amb.wind";
        public const string AmbForestNight = "amb.forest_night";
        public const string AmbTunnel = "amb.tunnel";
        public const string AmbCabinHum = "amb.cabin_hum";
        public const string AmbLampBuzz = "amb.lamp_buzz";
        public const string PlayerFootstepBus = "player.footstep_bus";
        public const string PlayerFootstepGravel = "player.footstep_gravel";
        public const string PaxFootstep = "pax.footstep";
        public const string PaxMutterLoop = "pax.mutter_loop";
        public const string PaxDeath = "pax.death";
        public const string PaxKickOut = "pax.kick_out";
        public const string MonTelegraphRumble = "mon.telegraph_rumble";
        public const string MonExpel = "mon.expel";
        public const string MonStarerSting = "mon.starer.sting";
        public const string MonStarerKill = "mon.starer.kill";
        public const string MonWhisperLoop = "mon.whisper_loop";
        public const string MonWhisperFeedLoop = "mon.whisper_feed_loop";
        public const string MonWhisperDriver = "mon.whisper.driver";
        public const string MonWhispererKill = "mon.whisperer.kill";
        public const string MonMimicSting = "mon.mimic.sting";
        public const string MonMimicKill = "mon.mimic.kill";
        public const string MonMimicReveal = "mon.mimic.reveal";
        public const string MonAngelScrape = "mon.angel.scrape";
        public const string MonAngelSting = "mon.angel.sting";
        public const string MonAngelKill = "mon.angel.kill";
        public const string ScareStartleSting = "scare.startle_sting";
        public const string ScareLightsOut = "scare.lights_out";
        public const string HalFootstepsBehind = "hal.footsteps_behind";
        public const string HalDoorChime = "hal.door_chime";
        public const string HalWindowKnock = "hal.window_knock";
        public const string HalStaticBurst = "hal.static_burst";
        public const string HalWhisperDriver = "hal.whisper_driver";
        public const string SanHeartbeatLoop = "san.heartbeat_loop";
        public const string DeathFallWind = "death.fall_wind";
        public const string DeathFallImpact = "death.fall_impact";
        public const string DeathBlackout = "death.blackout";
        public const string ItemCoffee = "item.coffee";
        public const string ItemEarplugs = "item.earplugs";
        public const string UiClick = "ui.click";
        public const string UiHover = "ui.hover";
        public const string UiPurchase = "ui.purchase";
        public const string UiError = "ui.error";
        public const string UiMoneyUp = "ui.money_up";
        public const string UiMoneyDown = "ui.money_down";
        public const string UiNightCard = "ui.night_card";
        public const string UiTypeTick = "ui.type_tick";
        public const string MenuDistantEngine = "menu.distant_engine";
        public const string MenuBusArrive = "menu.bus_arrive";
        public const string MusSummarySting = "mus.summary_sting";
        public const string MusGameoverSting = "mus.gameover_sting";

        public static readonly string[] All = {
            BusEngineLoop, BusAccel, BusDecel, BusHandbrake, BusDoorOpen, BusDoorClose, BusFareTap, BusStopRequest, BusRumbleStrip,
            BusCrashMinor, BusCrashMajor, BusHorn, CctvSwitch, CctvStaticLoop,
            AmbWind, AmbForestNight, AmbTunnel, AmbCabinHum, AmbLampBuzz,
            PlayerFootstepBus, PlayerFootstepGravel, PaxFootstep, PaxMutterLoop, PaxDeath, PaxKickOut,
            MonTelegraphRumble, MonExpel, MonStarerSting, MonStarerKill, MonWhisperLoop, MonWhisperFeedLoop,
            MonWhisperDriver, MonWhispererKill, MonMimicSting, MonMimicKill, MonMimicReveal,
            MonAngelScrape, MonAngelSting, MonAngelKill, ScareStartleSting, ScareLightsOut,
            HalFootstepsBehind, HalDoorChime, HalWindowKnock, HalStaticBurst, HalWhisperDriver,
            SanHeartbeatLoop, DeathFallWind, DeathFallImpact, DeathBlackout, ItemCoffee, ItemEarplugs,
            UiClick, UiHover, UiPurchase, UiError, UiMoneyUp, UiMoneyDown, UiNightCard, UiTypeTick,
            MenuDistantEngine, MenuBusArrive, MusSummarySting, MusGameoverSting,
        };
    }
}
