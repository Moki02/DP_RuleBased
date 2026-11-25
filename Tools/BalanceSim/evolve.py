"""Step 3: find the Skilled bot by evolutionary search over strategy genes.

(mu + lambda): each generation every genome plays the same set of seeds; the best are kept
and mutated. Fitness = mean escape day (a run that doesn't escape counts as day 50, a game
over as day 60), so lower is better. Writes results/skilled_genome.json (used by every later
run) and results/evolve_log.json (with the exploits / strategies found).

    python evolve.py                 # default: 24 genomes x 14 generations x 30 seeds
"""
import argparse
import json
import os
import random
import statistics

from sim import batch, config

ROOT = os.path.dirname(os.path.abspath(__file__))

POOL_ITEMS = ["S1", "S4", "S7", "S2", "S3", "S5", "S8", "W1", "W4", "W2", "W5", "W7", "P1",
              "E1", "E2", "E3", "E5", "M1", "M4", "V1", "V3", "V4", "V6"]

GENES = {  # name: (low, high, kind)
    "drink_at": (30, 90, "f"),
    "home_hyd_margin": (3, 30, "f"),
    "home_time_margin_s": (0, 40, "f"),
    "home_slack": (1.0, 1.4, "f"),
    "min_trip_s": (20, 150, "f"),
    "pump_target_l": (0, 60, "f"),
    "pour_keep_hyd": (5, 40, "f"),
    "reserve_l": (2, 15, "f"),
    "door_attempts": (1, 20, "i"),
    "gate_attempts": (2, 20, "i"),
    "valve_try": (0.0, 1.0, "f"),
    "room_detour": (5, 60, "i"),
    "explore_range": (40, 400, "i"),
    "room_shelter": (0, 1, "b"),
    "crouch_wait": (0, 1, "b"),
    "react_pump": (0, 1, "b"),
}


def random_genome(rng, base):
    g = {}
    for k, (lo, hi, kind) in GENES.items():
        if kind == "b":
            g[k] = rng.random() < 0.5
        elif kind == "i":
            g[k] = rng.randint(int(lo), int(hi))
        else:
            g[k] = round(rng.uniform(lo, hi), 3)
    order = POOL_ITEMS[:]
    rng.shuffle(order)
    g["buy_order"] = order[: rng.randint(8, len(order))]
    return g


def mutate(g, rng, strength=0.25):
    g = json.loads(json.dumps(g))
    for k, (lo, hi, kind) in GENES.items():
        if rng.random() > 0.35:
            continue
        if kind == "b":
            g[k] = not g[k]
        else:
            v = g[k] + rng.gauss(0, (hi - lo) * strength)
            v = min(hi, max(lo, v))
            g[k] = int(round(v)) if kind == "i" else round(v, 3)
    order = g["buy_order"]
    r = rng.random()
    if r < 0.4 and len(order) > 1:  # swap two
        i, j = rng.sample(range(len(order)), 2)
        order[i], order[j] = order[j], order[i]
    elif r < 0.6:  # insert a missing item
        missing = [u for u in POOL_ITEMS if u not in order]
        if missing:
            order.insert(rng.randrange(len(order) + 1), rng.choice(missing))
    elif r < 0.75 and len(order) > 4:  # drop one
        order.pop(rng.randrange(len(order)))
    g["buy_order"] = order
    return g


def fitness(results):
    vals = []
    for r in results:
        if r["result"] == "escaped":
            vals.append(r["escape_day"])
        elif r["result"] == "game_over":
            vals.append(60)
        else:
            vals.append(50)
    return statistics.mean(vals)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--pop", type=int, default=24)
    ap.add_argument("--gens", type=int, default=14)
    ap.add_argument("--seeds", type=int, default=30)
    ap.add_argument("--keep", type=int, default=6)
    ap.add_argument("--rng", type=int, default=7)
    args = ap.parse_args()
    rng = random.Random(args.rng)
    bots = config.load_json("bots.json")
    base = bots["skilled"]
    fixed = {k: base[k] for k in ("hit_rate", "exit_valve_hit_rate", "wrong_turn", "wrong_turn_len", "buy",
                                  "pump_fill_frac", "noise", "tap_keep_l")}
    start = {k: base.get(k, GENES[k][0]) for k in GENES}
    start["react_pump"] = True
    start["buy_order"] = list(base["buy_order"])
    pop = [start] + [mutate(start, rng, 0.15) for _ in range(args.pop // 3)] + \
          [random_genome(rng, base) for _ in range(args.pop - 1 - args.pop // 3)]
    log = []
    best_overall = None
    for gen in range(args.gens):
        seeds = list(range(10_000 + gen * 1000, 10_000 + gen * 1000 + args.seeds))
        jobs = [({**fixed, **g}, s, False) for g in pop for s in seeds]
        res = batch.run_jobs(jobs)
        scored = []
        for i, g in enumerate(pop):
            rr = res[i * len(seeds):(i + 1) * len(seeds)]
            days = [r["escape_day"] for r in rr if r["result"] == "escaped"]
            scored.append((fitness(rr), g, {
                "escape_rate": len(days) / len(rr), "median_day": statistics.median(days) if days else None,
                "min_day": min(days) if days else None,
                "game_over": sum(r["result"] == "game_over" for r in rr) / len(rr)}))
        scored.sort(key=lambda x: x[0])
        best = scored[0]
        log.append({"gen": gen, "best_fitness": best[0], "stats": best[2], "genome": best[1],
                    "mean_fitness": statistics.mean(s[0] for s in scored)})
        print(f"gen {gen:2d}  best {best[0]:5.2f}  {best[2]}  mean {log[-1]['mean_fitness']:.2f}", flush=True)
        if best_overall is None or best[0] < best_overall[0]:
            best_overall = best
        elite = [s[1] for s in scored[: args.keep]]
        children = []
        while len(children) < args.pop - len(elite):
            parent = rng.choice(elite[:3] if rng.random() < 0.6 else elite)
            children.append(mutate(parent, rng, 0.2 if gen < args.gens // 2 else 0.1))
        pop = elite + children
    # re-check the top 4 elites on 100 fresh seeds and keep the best
    final = []
    seeds = list(range(900_000, 900_100))
    for g in pop[:4]:
        rr = batch.run_jobs([({**fixed, **g}, s, False) for s in seeds])
        final.append((fitness(rr), g, rr))
    final.sort(key=lambda x: x[0])
    fit, genome, rr = final[0]
    days = sorted(r["escape_day"] for r in rr if r["result"] == "escaped")
    out = {"fitness": fit, "genome": genome, "escape_rate": len(days) / len(rr),
           "median_day": statistics.median(days) if days else None, "min_day": days[0] if days else None,
           "days": days}
    os.makedirs(os.path.join(ROOT, "results"), exist_ok=True)
    with open(os.path.join(ROOT, "results", "skilled_genome.json"), "w") as f:
        json.dump(out, f, indent=1)
    with open(os.path.join(ROOT, "results", "evolve_log.json"), "w") as f:
        json.dump(log, f, indent=1)
    print("FINAL", {k: v for k, v in out.items() if k != "days"})
    batch.close()


if __name__ == "__main__":
    main()
