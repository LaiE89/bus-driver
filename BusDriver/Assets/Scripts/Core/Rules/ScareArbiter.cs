using System;
using BusDriver.Core.Data;

namespace BusDriver.Core.Rules {
    public enum ScareOutcome : int { Accepted = 0, Queued = 1, Dropped = 2 }

    public enum ScareReason : int {
        None = 0,
        // Accepted: a Kill that interrupts the scare that was running
        Preempts = 1,
        // Accepted: a queued Monster scare whose wait is over
        Released = 2,
        NotDriving = 10,
        Paused = 11,
        Telegraph = 12,
        // Another Startle-or-above scare is still playing
        Busy = 13,
        GlobalGap = 14,
        Cooldown = 15,
        // A Monster scare is already waiting in the one-slot queue
        QueueFull = 16,
        // A queued Monster scare waited its full time without a chance to play
        Expired = 17,
        // A Kill took over while it waited
        Cancelled = 18,
    }

    // One arbitration result, for the caller and for the F1 scare log (§4.18)
    public struct ScareDecision {
        public string Id;
        public ScareTier Tier;
        public ScareOutcome Outcome;
        public ScareReason Reason;
        public double Time;

        public bool Accepted { get { return Outcome == ScareOutcome.Accepted; } }

        public override string ToString() {
            return Reason == ScareReason.None ? $"{Id} {Outcome}" : $"{Id} {Outcome} ({Reason})";
        }
    }

    // The timing numbers of §2.17 (BalanceConfig.scareGlobalGap, …), in seconds
    public struct ScareArbiterSettings {
        public float GlobalGap;
        public float MonsterCooldown;
        public float StartleCooldown;
        public float MonsterQueueSeconds;

        public static ScareArbiterSettings Default {
            get { return new ScareArbiterSettings { GlobalGap = 6f, MonsterCooldown = 20f, StartleCooldown = 10f, MonsterQueueSeconds = 5f }; }
        }

        public static ScareArbiterSettings From(BalanceConfig balance) {
            if (balance == null) {
                return Default;
            }
            return new ScareArbiterSettings {
                GlobalGap = balance.scareGlobalGap,
                MonsterCooldown = balance.monsterScareCooldown,
                StartleCooldown = balance.startleCooldown,
                MonsterQueueSeconds = balance.monsterScareQueueSeconds,
            };
        }
    }

    // The rules that decide whether a scare may play (§2.17). ScareDirector wraps it; this core
    // only answers and keeps the bookkeeping:
    // - no scares outside Driving or while paused
    // - during a kill-sequence telegraph only a Kill is allowed
    // - a Kill always plays, interrupting whatever is running and ignoring gaps and cooldowns
    // - Startle and Monster scares keep a 6 s global gap and their tier cooldown (10 s / 20 s), and
    //   don't start over another one still playing
    // - a blocked Monster scare waits in a one-slot queue for up to 5 s, then is dropped; a
    //   blocked Startle is dropped at once; Ambient only obeys the telegraph
    // Time comes from the injected clock (scaled game seconds in play, a fake clock in tests), so
    // pausing, which stops scaled time, also stops a queued scare's wait.
    public sealed class ScareArbiter {
        public const int LogSize = 8;

        readonly ScareArbiterSettings settings;
        readonly Func<double> clock;
        readonly ScareDecision[] log = new ScareDecision[LogSize];
        int logCount;
        int logNext;

        double lastStartleOrAbove = double.NegativeInfinity;
        double lastStartle = double.NegativeInfinity;
        double lastMonster = double.NegativeInfinity;
        bool driving;

        bool hasQueued;
        string queuedId;
        object queuedPayload;
        double queuedDeadline;

        public ScareArbiter(ScareArbiterSettings settings, Func<double> clock) {
            this.settings = settings;
            this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
        }

        public ScareArbiterSettings Settings { get { return settings; } }
        public bool Paused { get; set; }
        public bool TelegraphActive { get; set; }
        // A Startle-or-above scare is playing (until NotifyFinished)
        public bool IsRunning { get; private set; }
        public ScareTier RunningTier { get; private set; }
        public bool HasQueued { get { return hasQueued; } }
        public string QueuedId { get { return hasQueued ? queuedId : null; } }
        public int LogCount { get { return logCount; } }

        // Leaving Driving drops a waiting scare: nothing carries over into a Summary or a death
        public bool Driving {
            get { return driving; }
            set {
                driving = value;
                if (!value && hasQueued) {
                    DropQueued(ScareReason.NotDriving);
                }
            }
        }

        // The i-th most recent decision, 0 = newest
        public ScareDecision Recent(int i) {
            if (i < 0 || i >= logCount) {
                throw new ArgumentOutOfRangeException(nameof(i));
            }
            return log[(logNext - 1 - i + LogSize) % LogSize];
        }

        // Ask to play a scare now. The payload comes back from Update when a queued scare is released.
        public ScareDecision Request(string id, ScareTier tier, object payload = null) {
            double now = clock();
            if (!driving) {
                return Record(id, tier, ScareOutcome.Dropped, ScareReason.NotDriving, now);
            }
            if (Paused) {
                return Record(id, tier, ScareOutcome.Dropped, ScareReason.Paused, now);
            }
            if (tier == ScareTier.Kill) {
                ScareReason reason = IsRunning ? ScareReason.Preempts : ScareReason.None;
                if (hasQueued) {
                    DropQueued(ScareReason.Cancelled);
                }
                Start(tier, now);
                return Record(id, tier, ScareOutcome.Accepted, reason, now);
            }
            if (TelegraphActive) {
                return Blocked(id, tier, payload, ScareReason.Telegraph, now);
            }
            if (tier == ScareTier.Ambient) {
                return Record(id, tier, ScareOutcome.Accepted, ScareReason.None, now);
            }
            ScareReason block = BlockReason(tier, now);
            if (block != ScareReason.None) {
                return Blocked(id, tier, payload, block, now);
            }
            Start(tier, now);
            return Record(id, tier, ScareOutcome.Accepted, ScareReason.None, now);
        }

        // Once a frame: releases the queued Monster scare when it may play, or drops it when its
        // wait is over. Returns true when there's a decision about it this frame.
        public bool Update(out ScareDecision decision, out object payload) {
            decision = default;
            payload = null;
            if (!hasQueued) {
                return false;
            }
            double now = clock();
            if (now > queuedDeadline) {
                decision = DropQueued(ScareReason.Expired);
                return true;
            }
            if (!driving || Paused || TelegraphActive || BlockReason(ScareTier.Monster, now) != ScareReason.None) {
                return false;
            }
            payload = queuedPayload;
            string id = queuedId;
            ClearQueue();
            Start(ScareTier.Monster, now);
            decision = Record(id, ScareTier.Monster, ScareOutcome.Accepted, ScareReason.Released, now);
            return true;
        }

        // The running scare has finished (or was interrupted)
        public void NotifyFinished() {
            IsRunning = false;
        }

        ScareReason BlockReason(ScareTier tier, double now) {
            if (IsRunning) {
                return ScareReason.Busy;
            }
            if (now - lastStartleOrAbove < settings.GlobalGap) {
                return ScareReason.GlobalGap;
            }
            double last = tier == ScareTier.Monster ? lastMonster : lastStartle;
            float cooldown = tier == ScareTier.Monster ? settings.MonsterCooldown : settings.StartleCooldown;
            if (now - last < cooldown) {
                return ScareReason.Cooldown;
            }
            return ScareReason.None;
        }

        ScareDecision Blocked(string id, ScareTier tier, object payload, ScareReason reason, double now) {
            if (tier != ScareTier.Monster) {
                return Record(id, tier, ScareOutcome.Dropped, reason, now);
            }
            if (hasQueued) {
                return Record(id, tier, ScareOutcome.Dropped, ScareReason.QueueFull, now);
            }
            hasQueued = true;
            queuedId = id;
            queuedPayload = payload;
            queuedDeadline = now + settings.MonsterQueueSeconds;
            return Record(id, tier, ScareOutcome.Queued, reason, now);
        }

        void Start(ScareTier tier, double now) {
            IsRunning = true;
            RunningTier = tier;
            lastStartleOrAbove = now;
            if (tier == ScareTier.Monster) {
                lastMonster = now;
            }else if (tier == ScareTier.Startle) {
                lastStartle = now;
            }
        }

        ScareDecision DropQueued(ScareReason reason) {
            string id = queuedId;
            ClearQueue();
            return Record(id, ScareTier.Monster, ScareOutcome.Dropped, reason, clock());
        }

        void ClearQueue() {
            hasQueued = false;
            queuedId = null;
            queuedPayload = null;
        }

        ScareDecision Record(string id, ScareTier tier, ScareOutcome outcome, ScareReason reason, double now) {
            ScareDecision decision = new ScareDecision { Id = id, Tier = tier, Outcome = outcome, Reason = reason, Time = now };
            log[logNext] = decision;
            logNext = (logNext + 1) % LogSize;
            if (logCount < LogSize) {
                logCount++;
            }
            return decision;
        }
    }
}
