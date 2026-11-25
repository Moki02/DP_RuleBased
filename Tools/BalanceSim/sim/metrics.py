"""Turns a list of run summaries into the Step 4 metrics and checks the Step 5 targets."""
from collections import Counter

import numpy as np

INF_DAY = 99  # a run that never escaped counts as "later than any day" for medians


def _pct(xs, q):
    return float(np.percentile(xs, q)) if len(xs) else None


def aggregate(runs):
    n = len(runs)
    esc = [r for r in runs if r["result"] == "escaped"]
    days = [r["escape_day"] for r in esc]
    all_days = sorted([r["escape_day"] if r["result"] == "escaped" else INF_DAY for r in runs])
    go = [r for r in runs if r["result"] == "game_over"]
    win_rate = len(esc) / n
    collapsed = [r for r in runs if r["collapses"] > 0]
    win_after = (sum(1 for r in collapsed if r["result"] == "escaped") / len(collapsed)) if collapsed else None

    def frac_within(key, minutes):
        return sum(1 for r in runs if r.get(key) is not None and r[key] <= minutes) / n

    gate_room_10 = sum(1 for r in runs if r.get("first_gate_min") is not None and r["first_gate_min"] <= 10
                       and r.get("first_room_min") is not None and r["first_room_min"] <= 10) / n

    area_keys = [k for k in runs[0] if k.startswith("t_")]
    area = {k[2:]: float(np.mean([r[k] for r in runs])) for k in area_keys}
    src_keys = [k for k in runs[0] if k.startswith("src_")]
    src = {k[4:]: float(np.mean([r[k] for r in runs])) for k in src_keys}

    items = Counter()
    for r in runs:
        for u in (r["items"].split("|") if r["items"] else []):
            items[u] += 1

    coll_causes = Counter()
    for r in runs:
        for c in (r["collapse_causes"].split(";") if r["collapse_causes"] else []):
            coll_causes[c] += 1
    go_causes = Counter(r["game_over_cause"] for r in go)

    mg = {}
    for k in ("hub", "z1exit", "z2exit", "valve", "door"):
        t = sum(r[f"mg_{k}_tries"] for r in runs)
        w = sum(r[f"mg_{k}_wins"] for r in runs)
        mg[k] = {"tries_per_run": t / n, "win_rate": (w / t) if t else None}

    # water curve: median banked water and HYD per day
    by_day = {}
    for r in runs:
        for (d, clean, raw, jugs, hyd, can, _m) in r["_water_curve"]:
            by_day.setdefault(d, {"banked": [], "hyd": [], "clean": []})
            by_day[d]["banked"].append(clean + raw + jugs)
            by_day[d]["clean"].append(clean)
            by_day[d]["hyd"].append(hyd)
    curve = {d: {k: float(np.median(v)) for k, v in vals.items()} | {"n": len(vals["hyd"])}
             for d, vals in sorted(by_day.items())}

    def _score(r):
        return r["escape_day"] if r["result"] == "escaped" else (60 if r["result"] == "game_over" else 50)

    return {
        "n": n,
        "mean_score": float(np.mean([_score(r) for r in runs])),
        "escape_rate": win_rate,
        "game_over_rate": len(go) / n,
        "timeout_rate": sum(1 for r in runs if r["result"] == "timeout") / n,
        "median_escape_day_all": float(np.median(all_days)),
        "median_escape_day": float(np.median(days)) if days else None,
        "mean_escape_day": float(np.mean(days)) if days else None,
        "min_escape_day": min(days) if days else None,
        "escape_day_iqr": (_pct(days, 75) - _pct(days, 25)) if len(days) >= 4 else None,
        "escape_day_p25": _pct(days, 25), "escape_day_p75": _pct(days, 75),
        "real_min_median": float(np.median([r["real_min"] for r in esc])) if esc else None,
        "collapses_mean": float(np.mean([r["collapses"] for r in runs])),
        "close_calls_mean": float(np.mean([r["close_calls"] for r in runs])),
        "win_rate_after_collapse": win_after,
        "post_collapse_win_ratio": (win_after / win_rate) if (win_after is not None and win_rate > 0) else None,
        "reward_gap_median_min": float(np.median([r["reward_gap_median_min"] for r in runs])),
        "reward_gap_longest_min": float(np.median([r["reward_gap_longest_min"] for r in runs])),
        "reward_gap_longest_p90": _pct([r["reward_gap_longest_min"] for r in runs], 90),
        "dead_time": float(np.mean([r["dead_time_frac"] for r in runs])),
        "pump_frac": float(np.mean([r["pump_frac"] for r in runs])),
        "rewalk_frac": float(np.mean([r["rewalk_frac"] for r in runs])),
        "idle_hub_frac": float(np.mean([r["idle_hub_frac"] for r in runs])),
        "demo_gate_5": frac_within("first_gate_min", 5), "demo_gate_10": frac_within("first_gate_min", 10),
        "demo_room_5": frac_within("first_room_min", 5), "demo_room_10": frac_within("first_room_min", 10),
        "demo_return_5": frac_within("first_hub_return_min", 5), "demo_return_10": frac_within("first_hub_return_min", 10),
        "demo_gate_and_room_10": gate_room_10,
        "zone2_day_median": float(np.median([r["zone2_day"] for r in runs if r["zone2_day"]])) if any(r["zone2_day"] for r in runs) else None,
        "zone1_exit_min_median": float(np.median([r["first_gate_z1exit_min"] for r in runs if r["first_gate_z1exit_min"] is not None])) if any(r["first_gate_z1exit_min"] is not None for r in runs) else None,
        "map_pct_mean": float(np.mean([r["map_pct"] for r in runs])),
        "area_minutes": area,
        "sources_l": src,
        "items_bought_share": {k: v / n for k, v in items.most_common()},
        "collapse_causes": dict(coll_causes),
        "game_over_causes": dict(go_causes),
        "minigames": mg,
        "water_curve": curve,
    }


def _in(x, lo, hi):
    return x is not None and lo <= x <= hi


def check_targets(agg_by_bot, targets, variety=None):
    """Returns a list of (bot, name, target text, value, pass)."""
    rows = []
    t = targets["normal"]
    a = agg_by_bot.get("normal")
    if a:
        md = a["median_escape_day_all"] if a["median_escape_day_all"] < INF_DAY else None
        rows += [
            ("normal", "median escape day (non-escapes count as never)", f'{t["median_escape_day"][0]}-{t["median_escape_day"][1]}', md, _in(md, *t["median_escape_day"])),
            ("normal", "game over rate", f'<= {t["game_over_rate_max"]:.0%}', a["game_over_rate"], a["game_over_rate"] <= t["game_over_rate_max"]),
            ("normal", "collapses per run (mean)", f'{t["collapses_mean"][0]}-{t["collapses_mean"][1]}', a["collapses_mean"], _in(a["collapses_mean"], *t["collapses_mean"])),
            ("normal", "close calls per run (mean)", f'{t["close_calls_mean"][0]}-{t["close_calls_mean"][1]}', a["close_calls_mean"], _in(a["close_calls_mean"], *t["close_calls_mean"])),
            ("normal", "win rate after 1st collapse / overall", f'>= {t["post_collapse_win_ratio_min"]:.0%}', a["post_collapse_win_ratio"], a["post_collapse_win_ratio"] is not None and a["post_collapse_win_ratio"] >= t["post_collapse_win_ratio_min"]),
            ("normal", "reward gap, median (real min)", f'<= {t["reward_gap_median_max_min"]}', a["reward_gap_median_min"], a["reward_gap_median_min"] <= t["reward_gap_median_max_min"]),
            ("normal", "reward gap, longest (real min, median run)", f'<= {t["reward_gap_longest_max_min"]}', a["reward_gap_longest_min"], a["reward_gap_longest_min"] <= t["reward_gap_longest_max_min"]),
            ("normal", "dead time (pumping + re-walking)", f'<= {t["dead_time_max"]:.0%}', a["dead_time"], a["dead_time"] <= t["dead_time_max"]),
            ("normal", "demo: first gate AND first room within 10 min", f'>= {t["demo_gate_and_room_10min_min"]:.0%}', a["demo_gate_and_room_10"], a["demo_gate_and_room_10"] >= t["demo_gate_and_room_10min_min"]),
            ("normal", "escape day IQR (days)", f'<= {t["escape_day_iqr_max"]}', a["escape_day_iqr"], a["escape_day_iqr"] is not None and a["escape_day_iqr"] <= t["escape_day_iqr_max"]),
        ]
    t = targets["beginner"]
    a = agg_by_bot.get("beginner")
    if a:
        rows += [
            ("beginner", "escape rate", f'>= {t["escape_rate_min"]:.0%}', a["escape_rate"], a["escape_rate"] >= t["escape_rate_min"]),
            ("beginner", "game over rate", f'<= {t["game_over_rate_max"]:.0%}', a["game_over_rate"], a["game_over_rate"] <= t["game_over_rate_max"]),
            ("beginner", "median escape day (escapers)", f'<= {t["median_escape_day_max"]}', a["median_escape_day"], a["median_escape_day"] is not None and a["median_escape_day"] <= t["median_escape_day_max"]),
        ]
    t = targets["skilled"]
    a = agg_by_bot.get("skilled")
    if a:
        md = a["median_escape_day_all"] if a["median_escape_day_all"] < INF_DAY else None
        rows += [
            ("skilled", "median escape day", f'{t["median_escape_day"][0]}-{t["median_escape_day"][1]}', md, _in(md, *t["median_escape_day"])),
            ("skilled", "fastest escape day", f'>= {t["min_escape_day"]}', a["min_escape_day"], a["min_escape_day"] is not None and a["min_escape_day"] >= t["min_escape_day"]),
        ]
    if variety is not None:
        tv = targets["build_variety"]
        rows.append(("normal", "buy orders within 15% of the best", f'>= {tv["orders_needed"]}', variety, variety >= tv["orders_needed"]))
    return rows


def score(rows):
    """Higher is better: passes count most, then closeness."""
    return sum(1 for r in rows if r[4])
