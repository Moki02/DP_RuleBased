"""Runs the current balance with all three bots (200 runs each), saves CSVs and a summary.

    python run_baseline.py            # 200 runs per bot
    python run_baseline.py --n 50     # quicker
"""
import argparse
import json
import os
import pickle
import time

from sim import batch, config, metrics

ROOT = os.path.dirname(os.path.abspath(__file__))


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--n", type=int, default=200)
    ap.add_argument("--name", default="baseline")
    ap.add_argument("--overrides", default=None, help="JSON file of balance overrides")
    args = ap.parse_args()
    ov = json.load(open(args.overrides)) if args.overrides else None
    out = os.path.join(ROOT, "results", args.name)
    os.makedirs(out, exist_ok=True)
    aggs, keep = {}, {}
    for bot in ("normal", "beginner", "skilled"):
        t = time.time()
        runs = batch.run(bot, n=args.n, overrides=ov)
        dt = time.time() - t
        batch.save_csv(runs, os.path.join(out, f"{bot}.csv"))
        aggs[bot] = metrics.aggregate(runs)
        keep[bot] = runs
        a = aggs[bot]
        print(f"{bot:9s} {args.n} runs in {dt:5.1f}s  escape {a['escape_rate']:.0%}  game over {a['game_over_rate']:.0%}  "
              f"still going {a['timeout_rate']:.0%}  median day {a['median_escape_day']}  mean {a['mean_escape_day'] and round(a['mean_escape_day'],1)}")
    batch.close()
    rows = metrics.check_targets(aggs, config.load_json("targets.json"))
    with open(os.path.join(out, "summary.json"), "w") as f:
        json.dump({"aggregates": aggs, "targets": rows, "overrides": ov}, f, indent=1, default=str)
    with open(os.path.join(out, "runs.pkl"), "wb") as f:
        pickle.dump(keep, f)
    print(f"targets passed: {metrics.score(rows)}/{len(rows)}")
    for r in rows:
        print(f"  {'PASS' if r[4] else 'fail'}  {r[0]:8s} {r[1]:50s} target {r[2]:10s} value {r[3]}")


if __name__ == "__main__":
    main()
