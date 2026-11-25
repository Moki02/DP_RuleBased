"""Runs many seeds in parallel on ONE persistent worker pool. Each job carries its balance
overrides; workers cache the built config per override set."""
import csv
import json
import os
from multiprocessing import Pool

from . import config
from .engine import Game

_CFG_CACHE = {}
_BOTS = None


def _cfg(ov_json):
    if ov_json not in _CFG_CACHE:
        if len(_CFG_CACHE) > 8:
            _CFG_CACHE.clear()
        _CFG_CACHE[ov_json] = config.balance(json.loads(ov_json) or None)
    return _CFG_CACHE[ov_json]


def _job(args):
    global _BOTS
    ov_json, bot, seed, record, keep_visits = args
    if isinstance(bot, str):
        if _BOTS is None:
            _BOTS = config.bots()
        bot = _BOTS[bot]
    r = Game(_cfg(ov_json), bot, seed, record_path=record).run()
    r["_visits"] = list(r["_visits"]) if keep_visits else None
    return r


_POOL = None


def pool():
    global _POOL
    if _POOL is None:
        _POOL = Pool(max(1, os.cpu_count() - 1), maxtasksperchild=4000)
    return _POOL


def run(bot_name, n=200, seed0=1, overrides=None, bot_overrides=None, record=3, procs=None, keep_visits=True):
    """n runs of one bot. bot_overrides: {bot_name: {field: value}} merged into that bot."""
    ov = json.dumps(overrides or {}, sort_keys=True)
    bot = bot_name
    if bot_overrides and bot_name in bot_overrides:
        bot = {**config.bots()[bot_name], **bot_overrides[bot_name]}
    jobs = [(ov, bot, seed0 + i, i < record, keep_visits) for i in range(n)]
    res = pool().map(_job, jobs, chunksize=max(1, n // 76))
    for r in res:
        r["bot"] = bot_name
    return res


def run_jobs(jobs, overrides=None, procs=None):
    """jobs: list of (bot_dict, seed, record)."""
    ov = json.dumps(overrides or {}, sort_keys=True)
    return pool().map(_job, [(ov, b, s, rec, False) for b, s, rec in jobs], chunksize=max(1, len(jobs) // 160))


def close():
    global _POOL
    if _POOL is not None:
        _POOL.close()
        _POOL.join()
        _POOL = None


def save_csv(runs, path):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    cols = [k for k in runs[0].keys() if not k.startswith("_")]
    with open(path, "w", newline="", encoding="utf8") as f:
        w = csv.DictWriter(f, fieldnames=cols, extrasaction="ignore")
        w.writeheader()
        for r in runs:
            w.writerow(r)
