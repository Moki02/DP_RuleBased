"""Step 6: tuning sweep.

Phase A  sensitivity: every knob at its low and high value, one at a time (others at baseline).
Phase B  coarse search: random combinations of the knobs that mattered.
Phase C  refine: small moves around the best few setups, 200 runs per bot.
Then the build-order comparison for the baseline and the best setup.

Writes results/sweep.json. report.py reads it.

    python sweep.py                  # full sweep (about an hour on 20 cores)
    python sweep.py --quick          # smaller numbers, to try it out
"""
import argparse
import copy
import json
import os
import random
import time

from sim import batch, config, metrics

ROOT = os.path.dirname(os.path.abspath(__file__))

# name: (paths, kind, low, high, step choices for search). kind "set" = absolute value,
# "scale" = multiply the baseline value of every path.
KNOBS = {
    "day_length_s":        (["clock.day_length_s"], "set", 240, 360, [240, 270, 300, 330, 360, 420]),
    "night_lockdown_s":    (["clock.night_lockdown_s"], "set", 45, 90, [45, 60, 75, 90]),
    "pump_l_per_stroke":   (["sources.pump_liters_per_stroke"], "set", 0.25, 0.4, [0.25, 0.3, 0.35, 0.4, 0.5]),
    "lock_hub_l":          (["locks.lock_liters.hub"], "set", 3, 6, [2, 3, 4, 5, 6]),
    "lock_zone1_exit_l":   (["locks.lock_liters.z1exit"], "set", 7, 13, [6, 8, 10, 12]),
    "lock_zone2_exit_l":   (["locks.lock_liters.z2exit"], "set", 12, 24, [8, 10, 12, 14, 16, 18, 20]),
    "drain_walk":          (["hydration.drain_walk"], "set", 0.4, 0.6, [0.35, 0.4, 0.45, 0.5, 0.55, 0.6]),
    "drain_idle":          (["hydration.drain_idle"], "set", 0.2, 0.3, [0.15, 0.2, 0.25, 0.3]),
    "drain_crouch_idle":   (["hydration.drain_crouch_idle"], "set", 0.1, 0.25, [0.1, 0.15, 0.2, 0.25]),
    "lockdown_drain":      (["hydration.lockdown_drain"], "set", 2, 6, [2, 3, 4, 5, 6]),
    "upgrade_cost_mult":   (["upgrade_cost_mult"], "set", 0.75, 1.25, [0.6, 0.75, 0.85, 1.0, 1.15, 1.25]),
    "collapse_unbanked_loss": (["collapse.unbanked_loss"], "set", 0.25, 0.75, [0.25, 0.5, 0.75]),
    "collapse_clock_penalty_s": (["clock.collapse_clock_penalty_s"], "set", 0, 90, [0, 30, 60, 90]),
    "room_water_mult":     (["rooms.cache_base", "rooms.cache_per_d", "rooms.cistern_base", "rooms.cistern_per_d"], "scale", 0.5, 2.0, [0.5, 1.0, 1.5, 2.0, 3.0]),
    "exit_valve_hits":     (["minigame.exit_valve.hits"], "set", 3, 5, [3, 4]),
    "valve_band_shrink":   (["minigame.band_shrink"], "set", 10, 20, [5, 10, 15, 20]),
    "start_canteen_l":     (["storage.canteen_tiers.0", "start.canteen"], "set", 1.5, 3, [2, 3, 4]),
    "stage4_start_day":    (["stages.3.start_day"], "set", 10, 15, [10, 12, 15, 18]),
    "tile_reward_l":       (["rewards.tile_reward"], "set", 0.02, 0.05, [0.02, 0.03, 0.04, 0.05]),
    "stage4_water_table_m": (["stages.3.depth"], "set", 6.5, 7.5, [6.5, 7.0, 7.5]),
    "hydration_pack_cost_l": (["upgrades.S4.cost"], "set", 30, 40, [30, 35, 40]),
    "deep_set_cost_l":     (["upgrades.W4.cost"], "set", 10, 30, [10, 15, 20, 30]),
}
NOT_MODELLED = {
    "jump_hyd_cost": "Bots never jump (the maze is flat), so jump drain changes nothing in the sim.",
    "lockdown_warning_times": "Bots read the clock directly, so the warning toasts (60/40/20/10/5 s) don't change their timing. Modelled instead through each bot's home time margin.",
}

BUY_ORDERS = {
    "default (normal)": None,
    "carry first": ["S1", "S4", "E1", "M1", "S2", "S5", "W1", "P1", "E2", "S3", "W4", "M4", "E3", "S7"],
    "storage + purifier first": ["S2", "S5", "P1", "W1", "S1", "E1", "S3", "W4", "S4", "E2", "M1", "S8", "E3"],
    "endurance first": ["E1", "E2", "S1", "E3", "S2", "W1", "P1", "S5", "S4", "M1", "W4", "S3", "E5"],
    "speed + hands first": ["M1", "M4", "S1", "E1", "S2", "S5", "W1", "P1", "S4", "E2", "W4", "S3"],
    "scout first": ["V1", "V6", "S1", "V4", "E1", "S2", "S5", "W1", "P1", "S4", "W4", "V3", "S3"],
    "water sources first": ["W1", "S5", "P1", "S2", "W2", "S1", "W4", "W5", "E1", "S3", "S4", "W7"],
}


def overrides_for(setting, base_cfg):
    """setting: {knob: value} -> balance overrides {path: value}."""
    ov = {}
    for knob, val in setting.items():
        paths, kind = KNOBS[knob][0], KNOBS[knob][1]
        for p in paths:
            if kind == "scale":
                node = base_cfg
                for k in p.split("."):
                    node = node[int(k)] if isinstance(node, list) else node[k]
                ov[p] = round(node * val, 4)
            else:
                ov[p] = val
    return ov


def evaluate(setting, base_cfg, n_normal, n_other, targets, label=""):
    ov = overrides_for(setting, base_cfg)
    aggs = {}
    for bot, n in (("normal", n_normal), ("beginner", n_other), ("skilled", n_other)):
        runs = batch.run(bot, n=n, overrides=ov, record=0, keep_visits=False)
        aggs[bot] = metrics.aggregate(runs)
    rows = metrics.check_targets(aggs, targets)
    return {"setting": setting, "overrides": ov, "passed": metrics.score(rows), "of": len(rows),
            "closeness": closeness(aggs), "key": key_numbers(aggs), "rows": rows, "label": label}


def key_numbers(aggs):
    n, b, s = aggs["normal"], aggs["beginner"], aggs["skilled"]
    return {
        "normal_escape": n["escape_rate"], "normal_game_over": n["game_over_rate"],
        "normal_median_day": n["median_escape_day_all"], "normal_median_day_escapers": n["median_escape_day"],
        "normal_iqr": n["escape_day_iqr"], "normal_collapses": n["collapses_mean"], "normal_close_calls": n["close_calls_mean"],
        "normal_dead_time": n["dead_time"], "normal_gap_longest": n["reward_gap_longest_min"],
        "normal_real_min": n["real_min_median"], "normal_post_collapse": n["post_collapse_win_ratio"],
        "beginner_escape": b["escape_rate"], "beginner_game_over": b["game_over_rate"], "beginner_median_day": b["median_escape_day"],
        "skilled_median_day": s["median_escape_day_all"], "skilled_min_day": s["min_escape_day"], "skilled_escape": s["escape_rate"],
        "normal_score": n["mean_score"], "beginner_score": b["mean_score"], "skilled_score": s["mean_score"],
    }


def _dist(v, lo, hi, scale):
    if v is None:
        return 3.0
    return max(0.0, lo - v, v - hi) / scale


def closeness(aggs):
    """Lower is better: how far the main numbers are from their targets."""
    n, b, s = aggs["normal"], aggs["beginner"], aggs["skilled"]
    d = 0.0
    d += 2.0 * _dist(n["median_escape_day_all"], 20, 24, 4)
    d += _dist(n["game_over_rate"], 0, 0.15, 0.1)
    d += _dist(n["collapses_mean"], 1, 3, 1)
    d += _dist(n["close_calls_mean"], 3, 8, 3)
    d += _dist(n["escape_day_iqr"], 0, 6, 3)
    d += 0.5 * _dist(n["dead_time"], 0, 0.30, 0.1)
    d += 0.5 * _dist(n["reward_gap_longest_min"], 0, 12, 10)
    d += _dist(b["escape_rate"], 0.5, 1, 0.2)
    d += _dist(b["game_over_rate"], 0, 0.4, 0.2)
    d += _dist(b["median_escape_day"], 0, 32, 4)
    d += _dist(s["median_escape_day_all"], 12, 16, 4)
    d += _dist(s["min_escape_day"], 10, 99, 2)
    return round(d, 3)


def build_variety(overrides, n):
    bots = config.bots()
    res = {}
    for name, order in BUY_ORDERS.items():
        bo = {"normal": {"buy_order": order}} if order else None
        runs = batch.run("normal", n=n, overrides=overrides, bot_overrides=bo, record=0, keep_visits=False)
        a = metrics.aggregate(runs)
        res[name] = {"median_all": a["median_escape_day_all"], "median_escapers": a["median_escape_day"],
                     "escape_rate": a["escape_rate"], "game_over": a["game_over_rate"],
                     "order": order or bots["normal"]["buy_order"]}
    best = min(v["median_all"] for v in res.values())
    within = sum(1 for v in res.values() if v["median_all"] <= best * 1.15)
    return {"orders": res, "best": best, "within_15pct": within}


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--quick", action="store_true")
    ap.add_argument("--rng", type=int, default=3)
    ap.add_argument("--finalize", action="store_true",
                    help="keep the search results in results/sweep.json; only redo baseline, build variety and final runs")
    args = ap.parse_args()
    if args.finalize:
        return finalize()
    rng = random.Random(args.rng)
    q = args.quick
    nA, oA = (60, 40) if q else (200, 100)
    nB, oB, countB = (60, 40, 8) if q else (100, 100, 36)
    nC, countC = (80, 4) if q else (200, 16)
    base_cfg = config.balance()
    targets = config.load_json("targets.json")
    out = {"knobs": {k: {"paths": v[0], "kind": v[1], "low": v[2], "high": v[3], "choices": v[4]} for k, v in KNOBS.items()},
           "not_modelled": NOT_MODELLED}
    t0 = time.time()

    print("== baseline", flush=True)
    base = evaluate({}, base_cfg, 200, 200, targets, "baseline")
    out["baseline"] = base
    print(f"   passed {base['passed']}/{base['of']} closeness {base['closeness']} {base['key']}", flush=True)

    print("== phase A: sensitivity", flush=True)
    sens = {}
    for knob, (paths, kind, lo, hi, _) in KNOBS.items():
        res = {}
        for side, val in (("low", lo), ("high", hi)):
            r = evaluate({knob: val}, base_cfg, nA, oA, targets, f"{knob}={val}")
            res[side] = {"value": val, "key": r["key"], "passed": r["passed"], "closeness": r["closeness"]}
        bk = base["key"]
        H, L = res["high"]["key"], res["low"]["key"]
        eff_day = abs(H["normal_score"] - L["normal_score"])
        eff_esc = abs(H["normal_escape"] - L["normal_escape"])
        eff_go = abs(H["normal_game_over"] - L["normal_game_over"])
        eff_sk = abs(H["skilled_score"] - L["skilled_score"])
        eff_bg = abs(H["beginner_score"] - L["beginner_score"])
        res["effect"] = {"normal_score": eff_day, "normal_escape": eff_esc, "normal_game_over": eff_go,
                         "skilled_score": eff_sk, "beginner_score": eff_bg,
                         "score": round(eff_day + 0.5 * eff_sk + 0.5 * eff_bg, 2)}
        sens[knob] = res
        print(f"   {knob:26s} effect {res['effect']['score']:6.2f}  normal score {eff_day:5.1f}  escape {eff_esc:.2f}  "
              f"gameover {eff_go:.2f}  ({time.time()-t0:.0f}s)", flush=True)
    out["sensitivity"] = sens
    ranked = sorted(sens, key=lambda k: -sens[k]["effect"]["score"])
    out["sensitivity_rank"] = ranked
    active = [k for k in ranked if sens[k]["effect"]["score"] > 1.0][:10] or ranked[:6]

    print("== phase B: coarse search over", active, flush=True)
    tried = []
    for i in range(countB):
        setting = {k: rng.choice(KNOBS[k][4]) for k in active if rng.random() < 0.7}
        r = evaluate(setting, base_cfg, nB, oB, targets, f"B{i}")
        tried.append(r)
        print(f"   B{i:02d} passed {r['passed']}/{r['of']} close {r['closeness']:6.2f}  {setting}", flush=True)
    tried.sort(key=lambda r: (-r["passed"], r["closeness"]))

    print("== phase C: refine", flush=True)
    seeds_c = [t["setting"] for t in tried[:4]]
    refined = []
    for s in seeds_c:
        r = evaluate(s, base_cfg, nC, nC, targets, "C-parent")
        refined.append(r)
    for i in range(countC):
        parent = rng.choice(seeds_c)
        child = dict(parent)
        for _ in range(rng.randint(1, 2)):
            k = rng.choice(active)
            ch = KNOBS[k][4]
            cur = child.get(k, None)
            if cur in ch and rng.random() < 0.7:
                j = ch.index(cur) + rng.choice((-1, 1))
                child[k] = ch[max(0, min(len(ch) - 1, j))]
            else:
                child[k] = rng.choice(ch)
        r = evaluate(child, base_cfg, nC, nC, targets, f"C{i}")
        refined.append(r)
        print(f"   C{i:02d} passed {r['passed']}/{r['of']} close {r['closeness']:6.2f}  {child}", flush=True)
    refined.sort(key=lambda r: (-r["passed"], r["closeness"]))
    out["coarse"] = tried
    out["refined"] = refined
    best = refined[0]
    out["best"] = best
    all_pass = [r for r in refined + tried if r["passed"] == r["of"]]
    out["all_pass"] = all_pass

    print("== build variety", flush=True)
    out["variety_baseline"] = build_variety(None, 100 if q else 200)
    out["variety_best"] = build_variety(best["overrides"], 100 if q else 200)
    print("   baseline", out["variety_baseline"]["within_15pct"], "best", out["variety_best"]["within_15pct"], flush=True)

    # the best setup again with 200 runs per bot, keeping paths for the report
    print("== final runs for the best setup", flush=True)
    finals = {}
    for bot in ("normal", "beginner", "skilled"):
        finals[bot] = batch.run(bot, n=200, overrides=best["overrides"], record=3)
    import pickle
    with open(os.path.join(ROOT, "results", "best_runs.pkl"), "wb") as f:
        pickle.dump(finals, f)
    out["best_final"] = {b: metrics.aggregate(r) for b, r in finals.items()}
    out["best_final_rows"] = metrics.check_targets(out["best_final"], targets, out["variety_best"]["within_15pct"])
    with open(os.path.join(ROOT, "results", "sweep.json"), "w") as f:
        json.dump(out, f, indent=1, default=str)
    print(f"done in {time.time()-t0:.0f}s. best: passed {sum(1 for r in out['best_final_rows'] if r[4])}/{len(out['best_final_rows'])} {best['setting']}")
    batch.close()


def finalize():
    """Re-runs the parts that depend on the bot after a bot fix, keeping the knob search."""
    import pickle
    path = os.path.join(ROOT, "results", "sweep.json")
    with open(path, encoding="utf8") as f:
        out = json.load(f)
    base_cfg = config.balance()
    targets = config.load_json("targets.json")
    out["baseline"] = evaluate({}, base_cfg, 200, 200, targets, "baseline")
    best = evaluate(out["best"]["setting"], base_cfg, 200, 200, targets, "best")
    out["best"] = best
    out["variety_baseline"] = build_variety(None, 200)
    out["variety_best"] = build_variety(best["overrides"], 200)
    print("variety baseline", out["variety_baseline"]["within_15pct"], "best", out["variety_best"]["within_15pct"], flush=True)
    finals = {b: batch.run(b, n=200, overrides=best["overrides"], record=3) for b in ("normal", "beginner", "skilled")}
    with open(os.path.join(ROOT, "results", "best_runs.pkl"), "wb") as f:
        pickle.dump(finals, f)
    out["best_final"] = {b: metrics.aggregate(r) for b, r in finals.items()}
    out["best_final_rows"] = metrics.check_targets(out["best_final"], targets, out["variety_best"]["within_15pct"])
    out["finalized_after_bot_fix"] = True
    with open(path, "w") as f:
        json.dump(out, f, indent=1, default=str)
    print("final:", sum(1 for r in out["best_final_rows"] if r[4]), "/", len(out["best_final_rows"]))
    batch.close()


if __name__ == "__main__":
    main()
