"""Loads config/*.json and resolves the maze path."""
import copy
import json
import os

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))


def load_json(name):
    with open(os.path.join(ROOT, "config", name), encoding="utf8") as f:
        return json.load(f)


def balance(overrides=None):
    cfg = load_json("balance.json")
    cfg["_maze_path"] = os.path.normpath(os.path.join(ROOT, cfg["maze"]["csv"]))
    if overrides:
        cfg = apply(cfg, overrides)
    return cfg


def bots():
    b = load_json("bots.json")
    evolved = os.path.join(ROOT, "results", "skilled_genome.json")
    if os.path.exists(evolved):
        with open(evolved, encoding="utf8") as f:
            b["skilled"].update(json.load(f)["genome"])
    return b


def apply(cfg, overrides):
    """overrides: {"hydration.drain_walk": 0.45, "locks.lock_liters.z2exit": 14, "stages.*.heat": [..]}"""
    cfg = copy.deepcopy(cfg)
    for path, val in overrides.items():
        keys = path.split(".")
        node = cfg
        for k in keys[:-1]:
            node = node[int(k)] if isinstance(node, list) else node[k]
        last = keys[-1]
        if isinstance(node, list):
            node[int(last)] = val
        else:
            node[last] = val
    return cfg
