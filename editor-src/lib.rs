// v2.5 — Position + trait-driven recall AI with decision locking
use mod_api::*;
use std::sync::atomic::{AtomicBool, AtomicU64, Ordering};
use std::sync::{LazyLock, OnceLock, RwLock};
use std::collections::HashMap;
use std::fs;
use std::path::PathBuf;

// ── Globals ──
static THINK_COUNT: AtomicU64 = AtomicU64::new(0);
static T0_TICK: AtomicU64 = AtomicU64::new(0);
static T0_RING: AtomicU64 = AtomicU64::new(0);
static T1_TICK: AtomicU64 = AtomicU64::new(0);
static T1_RING: AtomicU64 = AtomicU64::new(0);
static CONFIG: OnceLock<Config> = OnceLock::new();

#[derive(Clone, Copy, Default)]
struct AthleteTraits { recall_off: i32, overstay_off: i32, danger_off: i32 }
static ATHLETE_TRAITS: LazyLock<RwLock<HashMap<usize, AthleteTraits>>> =
    LazyLock::new(|| RwLock::new(HashMap::new()));
static TRAITS_LOADED: AtomicBool = AtomicBool::new(false);
static DECISION_LOCK: LazyLock<RwLock<HashMap<usize, u64>>> =
    LazyLock::new(|| RwLock::new(HashMap::new()));
static LAST_HP: LazyLock<RwLock<HashMap<usize, usize>>> =
    LazyLock::new(|| RwLock::new(HashMap::new()));
static RESPAWN_TICK: LazyLock<RwLock<HashMap<usize, u64>>> =
    LazyLock::new(|| RwLock::new(HashMap::new()));

// ── Config ──
struct Config {
    recall_min: usize, recall_max: usize,
    overstay_min: usize, overstay_max: usize,
    danger_hp: usize, danger_floor: usize,
    threshold_reduce: usize, overstay_increase: usize,
    safe_threshold_increase: usize,
    danger_ring_window: u64,
    absolute_hp_panic: usize,
    decision_lock_ticks: usize,
    respawn_fight_ticks: usize,
    pos_recall: [i32; 5], pos_overstay: [i32; 5], pos_danger: [i32; 5],
}
impl Default for Config {
    fn default() -> Self {
        Config {
            recall_min: 22, recall_max: 33,
            overstay_min: 10, overstay_max: 18,
            danger_hp: 15, danger_floor: 3,
            threshold_reduce: 8, overstay_increase: 15,
            safe_threshold_increase: 5,
            danger_ring_window: 64,
            absolute_hp_panic: 150,
            decision_lock_ticks: 400,
            respawn_fight_ticks: 2000,
            pos_recall: [-8, 8, -2, 15, -3],
            pos_overstay: [15, -8, 3, -10, 10],
            pos_danger: [-6, 5, 0, 10, -3],
        }
    }
}

// ── Position helpers ──
fn pos_index(pos: Position) -> usize {
    match pos {
        Position::Top => 0, Position::Jungle => 1, Position::Mid => 2,
        Position::Bottom => 3, Position::Support => 4,
    }
}
fn traits_offset(agg: i32, jud: i32, men: i32) -> AthleteTraits {
    AthleteTraits {
        recall_off: (50 - agg) / 10 + (jud - 50) / 25,
        overstay_off: (agg - 50) / 8 + (men - 50) / 15,
        danger_off: (50 - jud + 50 - men) / 15,
    }
}
fn lookup_traits(id: usize) -> AthleteTraits {
    ATHLETE_TRAITS.read().ok().and_then(|m| m.get(&id).copied()).unwrap_or_default()
}

// ── Path ──
fn mod_dir() -> PathBuf {
    const WS: &str = "3763852408";
    if let Some(d) = std::env::current_exe().ok()
        .and_then(|p| p.parent()?.parent()?.parent()?.join("workshop").join("content").join("3009300").join(WS).canonicalize().ok())
        { return d; }
    if let Some(d) = std::env::current_exe().ok()
        .and_then(|p| p.parent()?.join("mods").join("tm2_bp_ai").canonicalize().ok())
        { return d; }
    PathBuf::from(".")
}

fn cfg() -> &'static Config {
    CONFIG.get_or_init(|| {
        use std::io::Read;
        let mut b = Config::default();
        let d = mod_dir();
        let load = |name: &str, c: &mut Config| {
            if let Ok(mut f) = fs::File::open(d.join(name)) {
                let mut s = String::new();
                if f.read_to_string(&mut s).is_ok() {
                    for line in s.lines() {
                        let t = line.trim();
                        if t.is_empty() || t.starts_with('#') || t.starts_with('[') { continue; }
                        if let Some((k, v)) = t.split_once('=') {
                            let v = v.trim();
                            match k.trim().trim_matches('"') {
                                "recall_threshold_min" => c.recall_min = v.parse().unwrap_or(c.recall_min),
                                "recall_threshold_max" => c.recall_max = v.parse().unwrap_or(c.recall_max),
                                "overstay_min" => c.overstay_min = v.parse().unwrap_or(c.overstay_min),
                                "overstay_max" => c.overstay_max = v.parse().unwrap_or(c.overstay_max),
                                "danger_hp" => c.danger_hp = v.parse().unwrap_or(c.danger_hp),
                                "danger_floor" => c.danger_floor = v.parse().unwrap_or(c.danger_floor),
                                "danger_ring_window" => c.danger_ring_window = v.parse().unwrap_or(c.danger_ring_window),
                                "threshold_reduce" => c.threshold_reduce = v.parse().unwrap_or(c.threshold_reduce),
                                "overstay_increase" => c.overstay_increase = v.parse().unwrap_or(c.overstay_increase),
                                "safe_threshold_increase" => c.safe_threshold_increase = v.parse().unwrap_or(c.safe_threshold_increase),
                                "absolute_hp_panic" => c.absolute_hp_panic = v.parse().unwrap_or(c.absolute_hp_panic),
                                "decision_lock_ticks" => c.decision_lock_ticks = v.parse().unwrap_or(c.decision_lock_ticks),
                                "respawn_fight_ticks" => c.respawn_fight_ticks = v.parse().unwrap_or(c.respawn_fight_ticks),
                                "pos_top_recall" => c.pos_recall[0] = v.parse().unwrap_or(c.pos_recall[0]),
                                "pos_jungle_recall" => c.pos_recall[1] = v.parse().unwrap_or(c.pos_recall[1]),
                                "pos_mid_recall" => c.pos_recall[2] = v.parse().unwrap_or(c.pos_recall[2]),
                                "pos_bottom_recall" => c.pos_recall[3] = v.parse().unwrap_or(c.pos_recall[3]),
                                "pos_support_recall" => c.pos_recall[4] = v.parse().unwrap_or(c.pos_recall[4]),
                                "pos_top_overstay" => c.pos_overstay[0] = v.parse().unwrap_or(c.pos_overstay[0]),
                                "pos_jungle_overstay" => c.pos_overstay[1] = v.parse().unwrap_or(c.pos_overstay[1]),
                                "pos_mid_overstay" => c.pos_overstay[2] = v.parse().unwrap_or(c.pos_overstay[2]),
                                "pos_bottom_overstay" => c.pos_overstay[3] = v.parse().unwrap_or(c.pos_overstay[3]),
                                "pos_support_overstay" => c.pos_overstay[4] = v.parse().unwrap_or(c.pos_overstay[4]),
                                "pos_top_danger" => c.pos_danger[0] = v.parse().unwrap_or(c.pos_danger[0]),
                                "pos_jungle_danger" => c.pos_danger[1] = v.parse().unwrap_or(c.pos_danger[1]),
                                "pos_mid_danger" => c.pos_danger[2] = v.parse().unwrap_or(c.pos_danger[2]),
                                "pos_bottom_danger" => c.pos_danger[3] = v.parse().unwrap_or(c.pos_danger[3]),
                                "pos_support_danger" => c.pos_danger[4] = v.parse().unwrap_or(c.pos_danger[4]),
                                _ => {}
                            }
                        }
                    }
                }
            }
        };
        load("config.toml", &mut b);
        load("config.user.toml", &mut b);
        b
    })
}

fn log(msg: &str) {
    use std::io::Write;
    let _ = (|| -> std::io::Result<()> {
        let mut f = fs::OpenOptions::new().create(true).append(true).open(mod_dir().join("tm2_bp_ai.log"))?;
        writeln!(f, "{}", msg)
    })();
}

fn log_clear(msg: &str) {
    use std::io::Write;
    let _ = (|| -> std::io::Result<()> {
        let mut f = fs::OpenOptions::new().create(true).write(true).truncate(true).open(mod_dir().join("tm2_bp_ai.log"))?;
        writeln!(f, "{}", msg)
    })();
}

// ── Init ──
fn init(_ctx: &GameCtx) -> ModRegistration {
    let mut reg = ModRegistration::new("tm2_bp_ai");
    reg.set_extension(Monitor);
    reg.add_player_input_ai(TeamARecall);
    reg.add_player_input_ai(TeamBRecall);
    reg
}
declare_mod!(init);

// ── Monitor ──
struct Monitor;
impl ModExtension for Monitor {
    fn post_update(&self, scene: &mut Scene, _u: &mut GameUI, _a: &mut Assets, _dt: f32) {
        if let Scene::InGame { data } = scene {
            if !TRAITS_LOADED.load(Ordering::Relaxed) {
                let ids: Vec<usize> = data.db().athletes.keys().copied().collect();
                if !ids.is_empty() {
                    let mut map = ATHLETE_TRAITS.write().unwrap();
                    for id in &ids {
                        if let Some(athlete) = data.athlete(*id) {
                            let agg = athlete.stat.aggressive as i32;
                            let jud = athlete.stat.judgement as i32;
                            let men = athlete.stat.mental as i32;
                            map.insert(*id, traits_offset(agg, jud, men));
                        }
                    }
                    TRAITS_LOADED.store(true, Ordering::Relaxed);
                    log_clear(&format!("traits_loaded athletes={} v2.5.1", ids.len()));
                }
            }
        } else {
            TRAITS_LOADED.store(false, Ordering::Relaxed);
            DECISION_LOCK.write().unwrap().clear();
            LAST_HP.write().unwrap().clear();
            RESPAWN_TICK.write().unwrap().clear();
        }

        static LAST_LOG: AtomicU64 = AtomicU64::new(0);
        let t = THINK_COUNT.load(Ordering::Relaxed);
        let last = LAST_LOG.load(Ordering::Relaxed);
        if t >= last + 50000 {
            LAST_LOG.store(t, Ordering::Relaxed);
            let d0 = T0_RING.load(Ordering::Relaxed).count_ones();
            let d1 = T1_RING.load(Ordering::Relaxed).count_ones();
            log(&format!("thinks={} danger_t0={} danger_t1={}", t, d0, d1));
        }
    }
}

// ── PRNG ──
fn gauss(s: u64) -> f32 {
    let mut x = (s as u32) ^ 0xDEADBEEF;
    let mut sum = 0.0f32;
    for _ in 0..12 {
        x ^= x << 13;
        x ^= x >> 17;
        x ^= x << 5;
        sum += (x as f32) / (u32::MAX as f32 + 1.0);
    }
    sum - 6.0
}

// ── Dual AI ──
macro_rules! team_recall {
    ($name:ident, $my_team:literal, $ring_self:ident, $tick_self:ident, $ring_other:ident) => {
        #[derive(Clone, Debug, Default)]
        struct $name;
        impl ModPlayerInputAi for $name {
            fn clone_box(&self) -> Box<dyn ModPlayerInputAi> { Box::new(self.clone()) }
            fn id(&self) -> &str { stringify!($name) }
            fn matches(&self, ctx: &PlayerAiInitContext) -> bool { ctx.team == $my_team }

            fn think(&mut self, ctx: &mut PlayerAiContext<'_, '_, '_>, _base_input: Option<Input>) -> PlayerInputDecision {
                let n = THINK_COUNT.fetch_add(1, Ordering::Relaxed);
                let c = cfg();
                let pi = pos_index(ctx.position());
                let tr = lookup_traits(ctx.athlete_id());
                let tick = ctx.tick() as u64;
                let aid = ctx.athlete_id();

                let recall_off = c.pos_recall[pi] + tr.recall_off;
                let overstay_off = c.pos_overstay[pi] + tr.overstay_off;
                let danger_off = c.pos_danger[pi] + tr.danger_off;

                // ── Respawn fight detection ──
                // prev_hp == 0 && current_hp > 0 = just respawned → fight to death
                let current_hp = ctx.hp().unwrap_or(0);
                let prev_hp = LAST_HP.read().unwrap().get(&aid).copied().unwrap_or(0);
                if prev_hp == 0 && current_hp > 0 {
                    RESPAWN_TICK.write().unwrap().insert(aid, tick);
                }
                LAST_HP.write().unwrap().insert(aid, current_hp);
                if let Some(&respawn) = RESPAWN_TICK.read().unwrap().get(&aid) {
                    if tick - respawn < c.respawn_fight_ticks as u64 {
                        return PlayerInputDecision::Pass;
                    }
                }

                // ── Decision lock check ──
                {
                    let lock = DECISION_LOCK.read().unwrap();
                    if let Some(&end) = lock.get(&aid) {
                        if tick < end {
                            return PlayerInputDecision::Pass;
                        }
                    }
                }
                DECISION_LOCK.write().unwrap().remove(&aid);

                let hp_pct = ctx.hp_ratio_percent().unwrap_or(100);

                // ── Absolute HP panic ──
                if let Some(hp) = ctx.hp() {
                    if hp < c.absolute_hp_panic {
                        if ctx.is_safe_to_recall() {
                            if let Some(i) = ctx.get_recall_input() {
                                DECISION_LOCK.write().unwrap().insert(aid, tick + c.decision_lock_ticks as u64);
                                return PlayerInputDecision::Replace(i);
                            }
                        }
                        if let Some(i) = ctx.get_run_away_without_skill_input() {
                            DECISION_LOCK.write().unwrap().insert(aid, tick + c.decision_lock_ticks as u64);
                            return PlayerInputDecision::Replace(i);
                        }
                        return PlayerInputDecision::Pass;
                    }
                }

                // ── Danger ring ──
                let last = $tick_self.load(Ordering::Relaxed);
                let mut ring = $ring_self.load(Ordering::Relaxed);
                if tick > last {
                    ring <<= 1;
                    if c.danger_ring_window < 64 {
                        ring &= (1u64 << c.danger_ring_window) - 1;
                    }
                    $tick_self.store(tick, Ordering::Relaxed);
                }
                if hp_pct < (c.danger_hp as i32 + danger_off).max(c.danger_floor as i32) as usize {
                    ring |= 1;
                }
                $ring_self.store(ring, Ordering::Relaxed);

                let self_danger = ring.count_ones() as usize;
                let other_danger = $ring_other.load(Ordering::Relaxed).count_ones() as usize;
                let losing = self_danger > other_danger;

                // ── Recall threshold ──
                let range = (c.recall_max - c.recall_min + 1) as u64;
                let base = (c.recall_min + ((n >> 3) % range) as usize) as i32;
                let threshold = if losing {
                    (base + recall_off - c.threshold_reduce as i32).max(1)
                } else {
                    (base + recall_off + c.safe_threshold_increase as i32).max(1)
                } as usize;

                if hp_pct >= threshold {
                    return PlayerInputDecision::Pass;
                }

                // ── Escape or recall ──
                if !ctx.is_safe_to_recall() {
                    if let Some(i) = ctx.get_run_away_input() {
                        DECISION_LOCK.write().unwrap().insert(aid, tick + c.decision_lock_ticks as u64);
                        return PlayerInputDecision::Replace(i);
                    }
                    return PlayerInputDecision::Pass;
                }

                // ── Overstay roll ──
                let o_range = (c.overstay_max - c.overstay_min + 1) as u64;
                let base_overstay = c.overstay_min + ((n >> 10) % o_range) as usize;
                let overstay = (base_overstay as i32 + overstay_off
                    + if losing { c.overstay_increase as i32 } else { 0 }).max(0) as usize;

                if gauss(n.wrapping_mul(0x9E3779B97F4A7C15)) < (overstay as f32 * 2.0 / 100.0) - 1.0 {
                    return PlayerInputDecision::Pass;
                }

                if let Some(i) = ctx.get_recall_input() {
                    DECISION_LOCK.write().unwrap().insert(aid, tick + c.decision_lock_ticks as u64);
                    return PlayerInputDecision::Replace(i);
                }
                PlayerInputDecision::Pass
            }
        }
    };
}
team_recall!(TeamARecall, 0, T0_RING, T0_TICK, T1_RING);
team_recall!(TeamBRecall, 1, T1_RING, T1_TICK, T0_RING);
