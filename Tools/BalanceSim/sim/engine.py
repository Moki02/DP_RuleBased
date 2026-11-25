"""Headless Salok game + bot. One Game = one run (one seed, one bot).

Time step: 1 real second (long hub waits are fast-forwarded to the next clock event).
All tiles are LOCAL indices (see maze.py). Rules: RULES.md. Numbers: config/balance.json.
"""
import math
import random
from array import array

from . import maze as mz

AREA_NAMES = ["hub", "zone1", "zone2"] + [f"room{i}" for i in range(7)]


# ---------------------------------------------------------------- minigame maths
def _erf(x):
    return math.erf(x)


def erfinv(p):
    lo, hi = 0.0, 6.0
    for _ in range(80):
        mid = (lo + hi) / 2
        if math.erf(mid) < p:
            lo = mid
        else:
            hi = mid
    return (lo + hi) / 2


def sigma_for(hit_rate, ref_band, ref_speed):
    """Timing error (s) that gives `hit_rate` on the reference valve."""
    hit_rate = min(max(hit_rate, 0.01), 0.999)
    return (ref_band / 2) / (erfinv(hit_rate) * math.sqrt(2) * ref_speed)


def p_hit(band, speed, sigma):
    return _erf((band / 2) / (speed * sigma * math.sqrt(2)))


# ---------------------------------------------------------------- the game
class Game:
    def __init__(self, cfg, bot, seed, record_path=False):
        self.cfg = cfg
        self.bot = bot
        self.rng = random.Random(seed)
        self.seed = seed
        mc = cfg["maze"]
        self.m = m = mz.load(cfg["_maze_path"], (mc["hub_max_ring"], mc["zone1_max_ring"], mc["zone2_max_ring"]))
        self.record_path = record_path

        c = cfg
        self.day_len = c["clock"]["day_length_s"]
        self.night_len = c["clock"]["night_lockdown_s"]
        self.max_day = c["clock"]["max_day"]
        H = c["hydration"]
        self.H = H

        # clock
        self.day = 1
        self.tod = 0.0
        self.real = 0.0
        self.turns = 0
        self.k = 0
        self.night = False

        # player
        self.pos = m.centre
        self.hyd = float(H["hyd_max"])
        self.canteen = float(c["start"]["canteen"])
        self.unbanked = 0.0
        self.sick = 0.0
        self.last_dir = (0, -1)

        # hub
        self.clean = float(c["start"]["clean"])
        self.raw = float(c["start"]["raw"])
        self.jugs = 0.0
        self.owned = []
        self.fx = {}
        self.filter_day = None
        self.wrong_mult = 1.0
        self.evap_mult_tonight = 1.0

        # maze objects
        self.gate_l = [0.0] * len(m.gates)
        self.gate_open = [False] * len(m.gates)
        self.gate_lock_until = [-1.0] * len(m.gates)
        self.gate_cap = [float(c["locks"]["lock_liters"][g.kind]) for g in m.gates]
        self.valve_open = [False] * len(m.valves)
        self.valve_lock_until = [-1.0] * len(m.valves)
        self.valve_idx = {v: i for i, v in enumerate(m.valves)}
        self.door_open = [False] * len(m.doors)
        self._setup_rooms()

        # map knowledge
        self.known = bytearray(m.n)
        self.revealed_count = 0
        self.map_total = len(m.map_tiles)
        self.sec_of, self.sec_tiles, self.sec_parts = m.sections(mc["zone1_sections"], mc["zone2_sections"])
        self.sec_seen = [0] * len(self.sec_tiles)
        self.sec_paid = [False] * len(self.sec_tiles)
        self.milestone_paid = [False] * len(c["rewards"]["milestone_percents"])

        # events
        self.event = "calm"
        self.event_start = 0.0
        self.event_started = False
        self.phase = None        # None / "warn" / "rain" / "heat"
        self.phase_left = 0.0

        # metrics
        self.visits = array("i", [0]) * m.n
        self.path_log = [] if record_path else None
        self.area_time = [0.0] * len(AREA_NAMES)
        self.collapses = []      # (real_s, day, cause)
        self.close_calls = 0
        self._dip = False
        self.reward_times = []   # (real_s, what)
        self.pump_s = 0.0
        self.rewalk_s = 0.0
        self.idle_hub_s = 0.0
        self.minigame_s = 0.0
        self.water_curve = []    # per dawn
        self.src = {k: 0.0 for k in ("pump", "rain", "still", "well", "tile", "section", "milestone",
                                     "valve", "room", "storm", "safe_return")}
        self.bought = []         # (id, day, real_s)
        self.first = {}          # first-time milestones in real seconds
        self.zone_first_day = {}
        self.result = None
        self.end_cause = None
        self.valve_fail_real = -999.0
        self.minigame_log = {"hub": [0, 0], "z1exit": [0, 0], "z2exit": [0, 0], "valve": [0, 0], "door": [0, 0]}

        # bot state
        b = bot
        mg = c["minigame"]
        self.sigma = sigma_for(b["hit_rate"], mg["reference_band"], mg["reference_speed"])
        self.path = []           # reversed: pop() gives the next tile
        self.goal = None         # (purpose, target)
        self.mode = "hub"
        self.busy = None         # dict for multi-second actions
        self.move_budget = 0.0
        self.home_dist = None
        self.home_dirty = True
        self.since_home_refresh = 0
        self.trip_failed = set() # doors/valves/gates given up on this trip
        self.trip_valve_roll = {}
        self.bought_this_visit = False
        self.left_hub_once = False
        self.trips = []
        self.trip = None
        self.home_reason = ""

        if c["map"].get("hub_known_at_start", True):
            for t in m.hub_tiles:
                self._know(t, pay=False)
            for g in m.gates:
                if g.kind == "hub":
                    for t in g.tiles:
                        self._know(t, pay=False)
        self._roll_event()

    # ------------------------------------------------------------ setup
    def _setup_rooms(self):
        m, rc = self.m, self.cfg["rooms"]
        rings = [r.ring for r in m.rooms]
        near, far = min(rings), max(rings)
        self.room_d, self.room_kind, self.room_cap, self.room_cur = [], [], [], []
        self.room_found = [False] * len(m.rooms)
        self.room_note = [False] * len(m.rooms)
        self.room_known = [False] * len(m.rooms)   # location known (seen on map / marker)
        for r in m.rooms:
            d = 0.0 if far <= near else min(1.0, max(0.0, (r.ring - near) / (far - near)))
            ov = rc.get("overrides", {}).get(str(r.id))
            if ov:
                kind = ov
            else:
                h = _hash01(rc["seed"], r.id)
                if h < rc["empty_chance"]:
                    kind = "empty"
                elif h < rc["empty_chance"] + (rc["cistern_near"] + (rc["cistern_far"] - rc["cistern_near"]) * d):
                    kind = "cistern"
                else:
                    kind = "cache"
            cap = (rc["cistern_base"] + rc["cistern_per_d"] * d if kind == "cistern"
                   else rc["cache_base"] + rc["cache_per_d"] * d if kind == "cache" else 0.0)
            self.room_d.append(d)
            self.room_kind.append(kind)
            self.room_cap.append(cap)
            self.room_cur.append(cap)

    # ------------------------------------------------------------ effects
    def f(self, key, default):
        return self.fx.get(key, default)

    def hyd_per_l(self):
        return self.f("hyd_per_liter", self.H["hyd_per_liter"])

    def canteen_cap(self):
        return self.f("canteen_cap", self.cfg["storage"]["canteen_tiers"][0])

    def clean_cap(self):
        return self.f("clean_cap", self.cfg["storage"]["clean_tiers"][0][0])

    def raw_cap(self):
        return self.f("raw_cap", self.cfg["storage"]["raw_tiers"][0])

    def stage_idx(self, day=None):
        d = self.day if day is None else day
        idx = 0
        for i, s in enumerate(self.cfg["stages"]):
            if d >= s["start_day"]:
                idx = i
        return idx

    def stage(self):
        return self.cfg["stages"][self.stage_idx()]

    def speed_tiles(self):
        sp = self.f("walk_speed", self.cfg["player"]["walk_speed"])
        if self.hyd < self.H["dehydrated_at"] + 1 and not self.f("no_dehydrated_slow", 0):
            sp *= self.cfg["player"]["dehydrated_speed_mult"]
        if self.f("heavy", 0) and self.canteen > self.cfg["player"]["heavy_carry_threshold_l"]:
            sp *= self.cfg["player"]["heavy_carry_speed_mult"]
        return sp / self.cfg["maze"]["cell_size_m"]

    def in_hub(self, t=None):
        return self.m.is_hub[self.pos if t is None else t]

    def sheltered(self, t=None):
        t = self.pos if t is None else t
        return self.m.is_hub[t] or self.m.room_of[t] >= 0

    def drain_mult(self, moving, in_hub=None):
        mult = self.f("drain_mult_all", 1.0)
        if moving:
            mult *= self.f("drain_mult_move", 1.0)
        mult *= self.stage()["heat"]
        if self.phase == "rain":
            mult *= self.cfg["events"]["storm_drain_mult"]
        elif self.phase == "heat":
            ev = self.cfg["events"]
            mult *= ev["heatwave_mult_stage4"] if self.stage_idx() == len(self.cfg["stages"]) - 1 else ev["heatwave_mult"]
        if self.sick > 0:
            mult *= self.H["sick_mult"]
        if (self.in_hub() if in_hub is None else in_hub):
            mult *= self.H["hub_shade_mult"]
        return mult

    def pump_rate(self):
        s = self.cfg["sources"]
        depth = self.stage()["depth"]
        if self.f("deep_pump", 0):
            fac = _piecewise(s["deep_pump_by_depth"], depth)
        else:
            fac = 0.0 if depth > s["suction_limit"] else _piecewise(s["pump_by_depth"], depth)
        interval = self.f("pump_stroke_interval", s["pump_stroke_interval"])
        return s["pump_liters_per_stroke"] / interval * fac

    def hand_mult(self):
        return self.f("hand_rate_mult", 1.0)

    def basin_of(self, g):
        """The basin stands on a fixed WORLD tile next to its gate (GateManager parents it to
        LockBasins, not to the zone 2 pivot). g.basin is that world tile (= local at 0 deg);
        this returns the local tile that is there now."""
        return self.m.w2l[self.k][g.basin]

    # ------------------------------------------------------------ passability / BFS
    def tile_ok(self, j):
        code = self.m.code[j]
        if code == "G":
            return self.gate_open[self.m.gate_of_tile[j]] and not self.night
        if code == "V":
            return self.valve_open[self.valve_idx[j]] and not self.night
        return code != "W"

    def edge_ok(self, a, b, allow_doors):
        d = self.m.door_of_edge.get((a, b))
        if d is None:
            return True
        if self.door_open[d]:
            return True
        return allow_doors and ("door", d) not in self.trip_failed

    def bfs(self, start, goal, known_only=True, allow_doors=False, max_nodes=100000, through_goal=False):
        """Shortest path from start to the first tile where goal(j) is True.
        Returns reversed path (pop() = next step) or None."""
        m = self.m
        nb = m.nbrs[self.k]
        known = self.known
        door_tiles = m.door_tiles
        prev = {start: -1}
        frontier = [start]
        n = 0
        while frontier:
            nxt = []
            for a in frontier:
                for j in nb[a]:
                    if j in prev:
                        continue
                    if known_only and not known[j]:
                        continue
                    if a in door_tiles and not self.edge_ok(a, j, allow_doors):
                        continue
                    is_goal = goal(j)
                    if not is_goal and not self.tile_ok(j):
                        continue
                    if is_goal and not through_goal and not self.tile_ok(j):
                        continue
                    prev[j] = a
                    if is_goal:
                        path = []
                        t = j
                        while t != start:
                            path.append(t)
                            t = prev[t]
                        return path
                    nxt.append(j)
                    n += 1
            if n > max_nodes:
                return None
            frontier = nxt
        return None

    def bfs_tree(self, start, allow_doors=True):
        """BFS over known, passable tiles from start. Returns (tiles in BFS order, prev map)."""
        m = self.m
        nb = m.nbrs[self.k]
        known = self.known
        door_tiles = m.door_tiles
        code = m.code
        prev = {start: -1}
        order = [start]
        i = 0
        while i < len(order):
            a = order[i]
            i += 1
            check_door = a in door_tiles
            for j in nb[a]:
                if j in prev or not known[j]:
                    continue
                if check_door and not self.edge_ok(a, j, allow_doors):
                    continue
                c = code[j]
                if (c == "G" or c == "V") and not self.tile_ok(j):
                    continue
                prev[j] = a
                order.append(j)
        return order, prev

    def refresh_home(self):
        """Distance (tiles) from every known passable tile to the hub base, by known routes."""
        m = self.m
        nb = m.nbrs[self.k]
        dist = [-1] * m.n
        frontier = list(m.hub_tiles)
        for t in frontier:
            dist[t] = 0
        known = self.known
        door_tiles = m.door_tiles
        d = 0
        while frontier:
            d += 1
            nxt = []
            for a in frontier:
                for j in nb[a]:
                    if dist[j] >= 0 or not known[j]:
                        continue
                    if a in door_tiles and not self.edge_ok(a, j, False):
                        continue
                    code = m.code[j]
                    if code == "G":
                        if not self.gate_open[m.gate_of_tile[j]]:
                            continue
                    elif code == "V":
                        if not self.valve_open[self.valve_idx[j]]:
                            continue
                    dist[j] = d
                    nxt.append(j)
            frontier = nxt
        self.home_dist = dist
        self.home_dirty = False
        self.since_home_refresh = 0

    def dist_home(self):
        if self.home_dirty or self.home_dist is None or self.since_home_refresh > 90:
            self.refresh_home()
        d = self.home_dist[self.pos]
        if d < 0:
            self.refresh_home()
            d = self.home_dist[self.pos]
            if d < 0:
                d = 60  # unknown way back: guess
        return d

    # ------------------------------------------------------------ map reveal / rewards
    def _know(self, t, pay=True):
        if self.known[t]:
            return
        self.known[t] = 1
        m = self.m
        if not m.walkable[t]:
            return
        self.revealed_count += 1
        s = self.sec_of[t]
        if s >= 0:
            self.sec_seen[s] += 1
        if not pay:
            return
        rw = self.cfg["rewards"]
        if m.floor_like[t]:
            self.unbanked += rw["tile_reward"]
            self.src["tile"] += rw["tile_reward"]
        r = m.room_of[t]
        if r >= 0:
            self.room_known[r] = True
        if s >= 0 and not self.sec_paid[s]:
            pct = self.f("section_pct", rw["section_mapped_pct"])
            if self.sec_seen[s] >= pct * len(self.sec_tiles[s]):
                self.sec_paid[s] = True
                amt = rw["section_reward"][0 if self.sec_parts[s] == mz.Z1 else 1] * self.f("section_mult", 1.0)
                self.unbanked += amt
                self.src["section"] += amt
        frac = self.revealed_count / self.map_total
        for i, p in enumerate(rw["milestone_percents"]):
            if not self.milestone_paid[i] and frac >= p - 1e-9:
                self.milestone_paid[i] = True
                self.unbanked += rw["milestone_rewards"][i]
                self.src["milestone"] += rw["milestone_rewards"][i]

    def reveal(self):
        """R8.1a-c: the tile, its 4 (8 with V3) neighbours, and up to N tiles ahead."""
        m, k = self.m, self.k
        l2w, w2l = m.l2w[k], m.w2l[k]
        p = self.pos
        self._know(p)
        wi = l2w[p]
        wx, wy = wi % mz.W, wi // mz.W
        offs = ((0, -1), (1, 0), (0, 1), (-1, 0))
        if self.f("diag_reveal", 0):
            offs = offs + ((1, 1), (1, -1), (-1, 1), (-1, -1))
        for dx, dy in offs:
            nx, ny = wx + dx, wy + dy
            if 0 <= nx < mz.W and 0 <= ny < mz.W:
                self._know(w2l[ny * mz.W + nx])
        dx, dy = self.last_dir
        ahead = int(self.f("look_ahead", self.cfg["map"]["look_ahead"]))
        x, y = wx, wy
        for _ in range(ahead):
            x, y = x + dx, y + dy
            if not (0 <= x < mz.W and 0 <= y < mz.W):
                break
            j = w2l[y * mz.W + x]
            self._know(j)
            if not m.walkable[j]:
                break

    def new_thing(self, what):
        self.reward_times.append((self.real, what))

    # ------------------------------------------------------------ entering a tile
    def on_enter(self, t):
        m = self.m
        if self.visits[t] > 0 and not m.is_hub[t]:
            self.rewalk_s += 1.0 / max(self.speed_tiles(), 0.1)
        self.visits[t] += 1
        if self.path_log is not None:
            self.path_log.append((round(self.real, 1), t))
        self.reveal()
        if self.trip is not None and m.ring[t] > self.trip["maxring"]:
            self.trip["maxring"] = m.ring[t]
        part = m.part[t]
        if part in (mz.Z1, mz.Z2) and m.room_of[t] < 0:
            zn = "zone1" if part == mz.Z1 else "zone2"
            if zn not in self.zone_first_day:
                self.zone_first_day[zn] = self.day
                self.first[zn] = self.real
                self.new_thing("zone:" + zn)
        r = m.room_of[t]
        if r >= 0 and not self.room_found[r]:
            self.room_found[r] = True
            self.first.setdefault("room", self.real)
            self.new_thing(f"room{r}")
        if m.is_hub[t]:
            if self.mode == "field":
                self.arrive_hub()
        elif self.mode in ("hub", "field_leaving"):
            self.mode = "field"
            self.left_hub_once = True
            self.trip = {"start": self.real, "day": self.day, "tod": self.tod, "maxring": 0, "known0": self.revealed_count,
                         "hyd0": self.hyd, "can0": self.canteen}
            self.trip_failed = set()
            self.trip_valve_roll = {}
        if part == mz.OUTER and m.code[t] == "G":
            g = m.gates[m.gate_of_tile[t]]
            if g.kind == "z2exit" and self.gate_open[g.id] and m.ring[t] >= g.outer_ring:
                self.result = "escaped"

    def arrive_hub(self):
        if self.trip is not None:
            tr = self.trip
            self.trips.append((self.day, round(tr["tod"]), round(self.real - tr["start"]), tr["maxring"],
                               self.revealed_count - tr["known0"], round(self.hyd), self.home_reason))
            self.trip = None
        self.mode = "hub"
        self.bought_this_visit = False
        if self.left_hub_once:
            self.first.setdefault("hub_return", self.real)
        self.bank(bonus=True)
        self.path = []
        self.goal = None

    def bank(self, bonus):
        if self.unbanked <= 1e-4:
            return
        carried = self.unbanked
        b = carried * self.cfg["rewards"]["safe_return_bonus"] if (bonus and not self.night) else 0.0
        self.src["safe_return"] += b
        self.unbanked = 0.0
        self.store_home(carried + b)

    def store_home(self, amt):
        to_clean = min(amt, max(0.0, self.clean_cap() - self.clean))
        self.clean += to_clean
        amt -= to_clean
        to_raw = min(amt, max(0.0, self.raw_cap() - self.raw))
        self.raw += to_raw
        amt -= to_raw
        self.jugs += amt

    # ------------------------------------------------------------ clock
    def _roll_event(self):
        st = self.stage()
        r = self.rng.random() * 100
        if r < st["storm"]:
            self.event = "storm"
        elif r < st["storm"] + st["heatwave"]:
            self.event = "heatwave"
        else:
            self.event = "calm"
        ev = self.cfg["events"]
        self.event_start = self.rng.uniform(ev["window_min"], min(ev["window_max"], self.day_len + self.night_len))
        self.event_started = False

    def _start_event(self):
        self.event_started = True
        if self.phase in ("warn", "rain"):
            return  # one at a time (R10.7): a running storm swallows today's roll
        if self.event == "storm":
            self.phase = "warn"
            self.phase_left = self.cfg["events"]["storm_warn"]
        elif self.event == "heatwave":
            self.phase = "heat"
            self.evap_mult_tonight = self.cfg["events"]["heatwave_evap_mult"]

    def _night_start(self):
        self.night = True
        m = self.m
        # GateManager: a player standing in a gate doorway is moved to the hub side
        if m.code[self.pos] == "G":
            self.pos = self.basin_of(m.gates[m.gate_of_tile[self.pos]])
        elif m.code[self.pos] == "V":
            best = None
            for j in m.nbrs[self.k][self.pos]:
                if m.code[j] not in ("W", "V") and (best is None or m.ring[j] < m.ring[best]):
                    best = j
            if best is not None:
                self.pos = best
        # Zone 2 turns on the nights of every Nth day (R8.24)
        n = max(1, self.cfg["maze"]["zone2_days_between_turns"])
        if self.day % n == 0:
            self.turns += 1
            self.k = self.turns % 4
        self.path = []
        self.goal = None
        self.home_dirty = True

    def _dawn(self):
        # evaporation uses the stage of the day that just ended
        st = self.stage()
        rate = st["evap"] * self.evap_mult_tonight
        self.evap_mult_tonight = 1.0
        stor = self.cfg["storage"]
        self.raw -= self.raw * min(1.0, rate * stor["raw_evap_factor"])
        self.clean -= self.clean * min(1.0, rate * self.f("clean_cover", stor["clean_tiers"][0][1]))
        self.jugs -= self.jugs * min(1.0, rate * stor["jug_evap_factor"])
        self.day += 1
        self.tod = 0.0
        self.night = False
        if self.phase == "heat":
            self.phase = None
        fac = self.cfg["stages"][self.stage_idx()]["cistern_refill"]
        for r, kind in enumerate(self.room_kind):
            if kind == "cistern":
                self.room_cur[r] = max(self.room_cur[r], self.room_cap[r] * fac)
        self.home_dirty = True
        self.path = []
        self.water_curve.append((self.day, round(self.clean, 2), round(self.raw, 2), round(self.jugs, 2),
                                 round(self.hyd, 1), round(self.canteen, 2), round(self.real / 60, 2)))
        self._roll_event()

    def advance_clock(self, dt):
        """Moves the clock dt seconds, firing night/dawn/event boundaries in order."""
        while dt > 1e-9:
            if not self.night:
                to_b = self.day_len - self.tod
            else:
                to_b = self.day_len + self.night_len - self.tod
            if not self.event_started and self.tod < self.event_start:
                to_b = min(to_b, self.event_start - self.tod)
            step = min(dt, to_b)
            self.tod += step
            dt -= step
            if not self.event_started and self.tod >= self.event_start - 1e-9:
                self._start_event()
            if not self.night and self.tod >= self.day_len - 1e-9:
                self._night_start()
            elif self.night and self.tod >= self.day_len + self.night_len - 1e-9:
                self._dawn()
                if self.day > self.max_day:
                    return

    def next_boundary(self):
        if not self.night:
            b = self.day_len - self.tod
        else:
            b = self.day_len + self.night_len - self.tod
        if not self.event_started and self.tod < self.event_start:
            b = min(b, self.event_start - self.tod)
        if self.phase in ("warn", "rain"):
            b = min(b, self.phase_left)
        return max(b, 0.01)

    # ------------------------------------------------------------ world step
    def world_step(self, dt, activity, moving):
        H = self.H
        base = {"walk": H["drain_walk"], "idle": H["drain_idle"], "pump": H["drain_pump"],
                "minigame": H["drain_minigame"], "crouch": H["drain_crouch_idle"]}[activity]
        drain = base * self.drain_mult(moving)
        if self.night and not self.sheltered():
            drain += H["lockdown_drain"]
        self.hyd -= drain * dt
        if self.sick > 0:
            self.sick = max(0.0, self.sick - dt)

        s = self.cfg["sources"]
        # purifier (pot or Biosand filter)
        if self.f("filter", 0):
            rip = s["filter_ripening_by_day"]
            idx = min(len(rip) - 1, max(0, self.day - self.filter_day))
            rate = s["filter_rate"] * rip[idx]
        else:
            rate = s["boil_rate"]
        amt = min(rate * dt, self.raw, max(0.0, self.clean_cap() - self.clean))
        self.raw -= amt
        self.clean += amt
        if self.jugs > 0:
            amt = min(self.jugs, max(0.0, self.clean_cap() - self.clean), s["spare_jug_pour_rate"] * dt)
            self.jugs -= amt
            self.clean += amt
        st = self.stage()
        raining = self.phase == "rain"
        if raining and self.f("rain_catcher", 0):
            got = self.f("catch_area", s["catch_area"]) * s["rain_l_per_m2s"] * s["runoff"] * dt
            add = min(got, max(0.0, self.raw_cap() - self.raw))
            self.raw += add
            self.src["rain"] += add
        if not raining and self.f("stills", 0):
            got = s["still_rate"] * st["sun"] * self.f("stills", 0) * dt
            if self.phase == "heat":
                got *= self.cfg["events"]["heatwave_still_mult"]
            add = min(got, max(0.0, self.clean_cap() - self.clean))
            self.clean += add
            self.src["still"] += add
        if self.f("well", 0):
            got = s["well_rate"] * st["well"] * dt
            add = min(got, max(0.0, self.raw_cap() - self.raw))
            self.raw += add
            self.src["well"] += add
        if raining and self.m.room_of[self.pos] < 0:
            add = min(self.cfg["events"]["storm_canteen_refill"] * dt, max(0.0, self.canteen_cap() - self.canteen))
            self.canteen += add
            self.src["storm"] += add
        # storm phases
        if self.phase in ("warn", "rain"):
            self.phase_left -= dt
            if self.phase_left <= 1e-9:
                if self.phase == "warn":
                    self.phase = "rain"
                    self.phase_left = self.cfg["events"]["storm_seconds"]
                else:
                    self.phase = None
        # metrics
        self.area_time[self.m.area_of(self.pos, self.m.room_of)] += dt
        self.real += dt
        if activity == "pump":
            self.pump_s += dt
        elif activity == "minigame":
            self.minigame_s += dt
        elif activity in ("idle", "crouch") and self.in_hub():
            self.idle_hub_s += dt
        self.advance_clock(dt)
        # close calls (HYD below 20 and back to 30+ without collapsing)
        if self.hyd < 0.2 * H["hyd_max"] and self.hyd > 0:
            self._dip = True
        elif self._dip and self.hyd >= 0.3 * H["hyd_max"]:
            self._dip = False
            self.close_calls += 1

    # ------------------------------------------------------------ collapse
    def collapse(self):
        m = self.m
        if self.night and not self.sheltered():
            cause = "night_lockdown"
        elif self.real - self.valve_fail_real < 30:
            cause = "valve_fails"
        else:
            p = m.part[self.pos]
            cause = "day_" + ("hub" if p == mz.HUB else "zone1" if p == mz.Z1 else "zone2")
        self._dip = False
        c = self.cfg["collapse"]
        self.collapses.append((round(self.real / 60, 2), self.day, cause))
        if not self.f("keep_canteen", 0):
            self.canteen = 0.0
        lost = math.floor(self.unbanked * c["unbanked_loss"] * 10) / 10
        self.unbanked = max(0.0, self.unbanked - lost)
        self.bank(bonus=False)
        self.real += self.cfg["clock"]["collapse_fade_s"]
        if self.clean < c["game_over_below"] - 1e-4:
            self.result = "game_over"
            self.end_cause = cause
            return
        self.clean -= min(self.clean, c["wake_liters"])
        self.hyd = max(1.0, min(self.H["hyd_max"], self.f("wake_hyd", c["wake_liters"] * self.H["hyd_per_liter"])))
        self.sick = 0.0
        self.pos = m.centre
        self.mode = "hub"
        self.bought_this_visit = False
        self.path = []
        self.goal = None
        self.busy = None
        self.home_dirty = True
        self.advance_clock(self.cfg["clock"]["collapse_clock_penalty_s"])

    # ------------------------------------------------------------ buying
    def available(self, uid):
        u = self.cfg["upgrades"][uid]
        if uid in self.owned:
            return False
        for need in u["needs"]:
            if not any(n in self.owned for n in need.split("|")):
                return False
        return True

    def cost(self, uid):
        return math.ceil(self.cfg["upgrades"][uid]["cost"] * self.cfg.get("upgrade_cost_mult", 1.0) - 1e-4)

    def buy(self, uid):
        c = self.cost(uid)
        if not self.available(uid) or c > self.clean_cap() + 1e-4 or c > self.clean + 1e-4:
            return False
        self.clean -= c
        self.owned.append(uid)
        for k2, v in self.cfg["upgrades"][uid]["fx"].items():
            if k2 in ("canteen_cap", "clean_cap", "raw_cap", "catch_area", "stills"):
                self.fx[k2] = max(self.fx.get(k2, 0), v)
            else:
                self.fx[k2] = v
        if uid == "P1":
            self.filter_day = self.day
        if "clean_cover" in self.cfg["upgrades"][uid]["fx"]:
            # cover factor of the biggest storage owned
            best = max((u for u in self.owned if "clean_cap" in self.cfg["upgrades"][u]["fx"]),
                       key=lambda u: self.cfg["upgrades"][u]["fx"]["clean_cap"])
            self.fx["clean_cover"] = self.cfg["upgrades"][best]["fx"]["clean_cover"]
        self.wrong_mult = 1.0
        for u in self.owned:
            self.wrong_mult *= self.cfg["upgrades"][u]["fx"].get("wrong_turn_mult", 1.0)
        self.bought.append((uid, self.day, round(self.real / 60, 2)))
        self.new_thing("buy:" + uid)
        self.home_dirty = True
        return True

    # ------------------------------------------------------------ minigame
    def play_minigame(self, kind, ring):
        """Returns (won, seconds). kind: hub / z1exit / z2exit / valve / door."""
        mg = self.cfg["minigame"]
        b = self.bot
        if kind == "z2exit":
            ev = mg["exit_valve"]
            hits_n, miss_n = ev["hits"], ev["misses"]
            p_fn = lambda h: b["exit_valve_hit_rate"]
        else:
            t = min(1.0, max(0.0, (ring - mg["ring_start"]) / max(1, mg["ring_span"])))
            band = mg["band_at_hub"] - mg["band_shrink"] * t
            speed = mg["speed_at_hub"] + mg["speed_gain"] * t
            hits_n, miss_n = mg["hits_to_win"], mg["misses_to_lose"]
            sig = self.sigma
            mult, ups = mg["speed_up_mult"], mg["speed_ups"]
            p_fn = lambda h: p_hit(band, speed * mult ** min(h, ups), sig)
        hits = misses = presses = 0
        while hits < hits_n and misses < miss_n:
            presses += 1
            if self.rng.random() < p_fn(hits):
                hits += 1
            else:
                misses += 1
        won = hits >= hits_n
        log = self.minigame_log[kind]
        log[0] += 1
        log[1] += 1 if won else 0
        return won, presses * mg["press_interval_s"] + mg["end_delay_s"]

    # ------------------------------------------------------------ main loop
    def run(self):
        steps = 0
        while self.result is None:
            if self.day > self.max_day:
                self.result = "timeout"
                break
            dt, activity, moving = self.tick()
            self.world_step(dt, activity, moving)
            if self.hyd <= 0:
                self.collapse()
            steps += 1
            if steps > 2_000_000:
                self.result = "timeout"
        return self.summary()

    # ------------------------------------------------------------ BOT
    def tick(self):
        """One decision step. Returns (dt, activity, moving)."""
        b = self.bot
        # passive drinking from the canteen (works while walking)
        hpl = self.hyd_per_l()
        if self.hyd < b["drink_at"] and self.canteen > 0 and not self.in_hub():
            room = (self.H["hyd_max"] - self.hyd) / hpl
            keep = self.pour_reserve()
            amt = min(self.H["drink_rate"] * self.hand_mult(), self.canteen - keep if self.hyd > 15 else self.canteen, room)
            if amt > 0:
                self.canteen -= amt
                self.hyd += amt * hpl

        if self.busy is not None:
            return self.do_busy()
        if self.mode == "hub" and self.in_hub():
            return self.hub_tick()
        return self.field_tick()

    def pour_reserve(self):
        """Canteen litres kept for pouring when heading to a basin."""
        if self.goal and self.goal[0] == "pour" and self.hyd > 30:
            return min(self.canteen, 99.0)
        return 0.0

    # ---------------- busy actions
    def do_busy(self):
        bz = self.busy
        bz["left"] -= 1
        if bz["left"] <= 0:
            self.busy = None
            fn = bz.get("done")
            if fn:
                fn()
        act = bz.get("act", "idle")
        return 1.0, act, False

    def start_busy(self, seconds, act, done=None):
        self.busy = {"left": max(1, int(round(seconds))), "act": act, "done": done}

    # ---------------- hub
    def hub_tick(self):
        b = self.bot
        m = self.m
        if m.ring[self.pos] > 3:
            # walk to the middle of the hub (pump, tap, storage, Build menu)
            if not self.path:
                self.path = self.bfs(self.pos, lambda j: m.ring[j] <= 2 and m.is_hub[j]) or []
            return self.walk_tick()
        hpl = self.hyd_per_l()
        keep = b.get("tap_keep_l", 2.5)
        if not self.bought_this_visit:
            self.bought_this_visit = True
            self.do_buys()
        # drink at the tap
        if self.hyd < self.H["hyd_max"] - 1 and self.clean > keep:
            amt = min(self.H["drink_rate"] * self.hand_mult(), (self.H["hyd_max"] - self.hyd) / hpl, self.clean - keep)
            self.clean -= amt
            self.hyd += amt * hpl
            return 1.0, "idle", False
        # refill canteen
        if self.canteen < self.canteen_cap() - 0.01 and self.clean > keep:
            amt = min(self.H["refill_rate"], self.canteen_cap() - self.canteen, self.clean - keep)
            self.clean -= amt
            self.canteen += amt
            return 1.0, "idle", False
        # pump
        pr = self.pump_rate()
        if self.night:
            want_raw = self.raw_cap()
        else:
            want_raw = min(self.raw_cap() * b["pump_fill_frac"], b.get("pump_target_l", 15))
        if self.clean >= self.clean_cap() - 1.0:
            want_raw = 0.0  # storage full: pumping makes nothing
        secs_left = self.day_len - self.tod if not self.night else 0
        leaving = (not self.night) and secs_left >= b["min_trip_s"] and self.hyd >= self.H["hyd_max"] - 3
        if pr > 0 and self.raw < want_raw - max(0.5, pr):
            add = min(pr, self.raw_cap() - self.raw)
            self.raw += add
            self.src["pump"] += add
            return 1.0, "pump", False
        if leaving:
            self.trip_failed = set()
            self.trip_valve_roll = {}
            self.mode = "field_leaving"
            self.goal = None
            self.path = []
            return self.field_tick()
        # wait for dawn / the right time (fast-forward)
        dt = min(self.next_boundary(), 20.0)
        if self.hyd < self.H["hyd_max"] - 1 and self.clean > keep:
            dt = 1.0
        return dt, ("crouch" if b.get("crouch_wait") else "idle"), False

    def do_buys(self):
        b = self.bot
        reserve = b["reserve_l"]
        if b["buy"] == "random":
            if self.rng.random() > b.get("buy_chance", 1.0):
                return
            for _ in range(3):
                opts = [u for u in self.cfg["upgrades"] if self.available(u)
                        and self.cost(u) <= self.clean - reserve and self.cost(u) <= self.clean_cap()]
                if not opts:
                    return
                self.buy(self.rng.choice(opts))
        else:
            order = list(b["buy_order"])
            if b.get("react_pump", True) and not self.f("deep_pump", 0):
                s = self.cfg["sources"]
                for st in self.cfg["stages"]:
                    if st["depth"] > s["suction_limit"] and self.day >= st["start_day"] - 2:
                        order = ["W1", "W4"] + [u for u in order if u not in ("W1", "W4")]
                        break
            for uid in order:
                if uid in self.owned:
                    continue
                if not self.available(uid):
                    continue
                c = self.cost(uid)
                if c > self.clean_cap():
                    # needs bigger storage first: skip ahead only to storage items
                    continue
                # keep the reserve, unless the item + reserve can never fit in storage
                r = min(reserve, max(0.0, self.clean_cap() - c - 0.5))
                if c <= self.clean - r:
                    self.buy(uid)
                    continue
                break  # save up for the next item in order

    # ---------------- field
    def must_go_home(self):
        b = self.bot
        dh = self.dist_home()
        sp = max(self.speed_tiles(), 0.1)
        t_home = dh / sp * b["home_slack"]
        secs_left = self.day_len - self.tod
        if secs_left < t_home + b["home_time_margin_s"]:
            return "time"
        need = self.H["drain_walk"] * self.drain_mult(True, False) * t_home + b["home_hyd_margin"]
        avail = self.hyd + self.canteen * self.hyd_per_l()
        if avail < need:
            return "hyd"
        return None

    def need_home_hyd(self):
        b = self.bot
        dh = self.dist_home()
        sp = max(self.speed_tiles(), 0.1)
        t_home = dh / sp * b["home_slack"]
        return self.H["drain_walk"] * self.drain_mult(True, False) * t_home + b["home_hyd_margin"]

    def stage_level(self):
        m = self.m
        if any(self.gate_open[g.id] for g in m.gates if g.kind == "z1exit"):
            return 2
        if any(self.gate_open[g.id] for g in m.gates if g.kind == "hub"):
            return 1
        return 0

    def field_tick(self):
        if self.mode == "field_leaving":
            self.mode = "field" if not self.in_hub() else "field_leaving"
        b = self.bot
        m = self.m
        self.since_home_refresh += 1
        # night: get to shelter
        if self.night:
            if self.sheltered():
                if self.in_hub():
                    self.mode = "hub"
                    return self.hub_tick()
                return 1.0, ("crouch" if b.get("crouch_wait") else "idle"), False
            if not self.goal or self.goal[0] != "shelter" or not self.path:
                p = self.bfs(self.pos, lambda j: m.is_hub[j] or m.room_of[j] >= 0, allow_doors=True)
                if p is None:
                    p = self.bfs(self.pos, lambda j: m.is_hub[j] or m.room_of[j] >= 0, known_only=False, allow_doors=True)
                self.path = p or []
                self.goal = ("shelter", None)
            return self.walk_tick()

        # in a room during the day with a goal "wait for dawn"? no: daytime goes on
        if self.goal is None or self.goal[0] not in ("home", "shelter_room", "escape"):
            why = self.must_go_home()
            if why:
                self.home_reason = why
                sp = max(self.speed_tiles(), 0.1)
                home_ok = self.dist_home() / sp <= (self.day_len - self.tod) - 3
                if why == "time" and b.get("room_shelter") and not home_ok:
                    rp = self.room_shelter_path()
                    if rp is not None:
                        self.goal = ("shelter_room", None)
                        self.path = rp
                        return self.walk_tick()
                self.go_home()
        if self.goal and self.goal[0] == "shelter_room" and m.room_of[self.pos] >= 0 and not self.path:
            # wait in the room for the lockdown and dawn
            if self.night or (self.day_len - self.tod) < 60:
                return min(self.next_boundary(), 10.0), ("crouch" if b.get("crouch_wait") else "idle"), False
            self.goal = None
        if not self.path:
            self.plan()
        if not self.path and self.busy is None:
            # nothing to do outside: go home
            if not self.in_hub():
                self.go_home()
            else:
                self.mode = "hub"
                return 1.0, "idle", False
        if self.busy is not None:
            return self.do_busy()
        return self.walk_tick()

    def room_shelter_path(self):
        """A known room that is nearer than home by time, for the night."""
        m = self.m
        b = self.bot
        p = self.bfs(self.pos, lambda j: m.room_of[j] >= 0, allow_doors=True, max_nodes=600)
        if p is None:
            return None
        sp = max(self.speed_tiles(), 0.1)
        if len(p) / sp * b["home_slack"] + 10 < self.day_len - self.tod:
            return p
        return None

    def go_home(self):
        m = self.m
        if self.in_hub():
            self.path = []
            self.goal = None
            self.mode = "hub"
            return
        self.goal = ("home", None)
        p = self.bfs(self.pos, lambda j: m.is_hub[j])
        if p is None:
            p = self.bfs(self.pos, lambda j: m.is_hub[j], known_only=False)
        self.path = p or []

    def pourable(self):
        floor = max(self.cfg["locks"]["pour_hyd_floor"], self.need_home_hyd() + self.bot["pour_keep_hyd"])
        from_hyd = max(0.0, self.hyd - floor) / self.hyd_per_l() if self.cfg["locks"]["pour_from_hyd"] else 0.0
        return self.canteen + from_hyd

    def plan(self):
        """Pick the next goal and path. Order: basin of the next gate, room water, new rooms,
        exploration, valve grates, home."""
        b = self.bot
        m = self.m
        # standing in a room: read the note / take the water first
        if m.room_of[self.pos] >= 0 and self.in_room_actions():
            return
        # 0. an exit is open: walk out
        for g in m.gates:
            if g.kind == "z2exit" and self.gate_open[g.id] and not self.night:
                p = self.bfs(self.pos, lambda j, t=g.outer_tile: j == t, known_only=False)
                if p:
                    self.goal = ("escape", g.id)
                    self.path = p
                    return
        lvl = self.stage_level()
        kind_needed = ("hub", "z1exit", "z2exit")[lvl]
        known = self.known
        nb = m.nbrs[self.k]
        order, prev = self.bfs_tree(self.pos)

        def path_to(t):
            if t not in prev:
                return None
            p = []
            while t != self.pos:
                p.append(t)
                t = prev[t]
            return p

        # 1. the next gate's basin
        cand = [g for g in m.gates if g.kind == kind_needed and known[self.basin_of(g)]
                and not self.gate_open[g.id] and ("gate", g.id) not in self.trip_failed]
        if cand:
            cand.sort(key=lambda g: -self.gate_l[g.id])
            full = [g for g in cand if self.gate_l[g.id] >= self.gate_cap[g.id] - 1e-6]
            pick = full[0] if full else cand[0]
            is_full = self.gate_l[pick.id] >= self.gate_cap[pick.id] - 1e-6
            if is_full or self.pourable() >= 0.5:
                if self.pos == self.basin_of(pick):
                    self.goal = ("pour", pick.id)
                    self.at_basin(pick)
                    return
                p = path_to(self.basin_of(pick))
                if p:
                    self.goal = ("pour", pick.id)
                    self.path = p
                    return
        # 2./3. rooms: water into the canteen, or a room not entered yet (note, water, shelter)
        want_water = self.canteen < self.canteen_cap() - 0.8
        room_of = m.room_of
        for j in order:
            r = room_of[j]
            if r < 0:
                continue
            if (want_water and self.room_found[r] and self.room_cur[r] > 0.3) or                (self.room_known[r] and not self.room_found[r]):
                p = path_to(j)
                if p is not None and len(p) <= b["room_detour"]:
                    self.goal = ("room", r)
                    self.path = p
                    return
                break  # nearest room tile is too far: the rest are farther
        # 4. explore (prefer the zone we need to cross)
        want_part = (mz.HUB, mz.Z1, mz.Z2)[lvl]
        part = m.part
        first_any = None
        target = None
        for j in order:
            if j == self.pos:
                continue
            fr = False
            pref = part[j] == want_part
            for q in nb[j]:
                if not known[q]:
                    fr = True
                    if part[q] == want_part:
                        pref = True
            if not fr:
                continue
            if pref:
                target = j
                break
            if first_any is None:
                first_any = j
        if target is None:
            target = first_any
        if target is not None:
            p = path_to(target)
            if p is not None and len(p) <= b["explore_range"]:
                self.goal = ("explore", target)
                self.path = p
                return
        # 5. valve grates
        for vi, v in enumerate(m.valves):
            if self.valve_open[vi] or ("valve", vi) in self.trip_failed or not known[v]:
                continue
            roll = self.trip_valve_roll.setdefault(vi, self.rng.random() < b["valve_try"])
            if not roll:
                continue
            if self.pos in nb[v]:
                self.goal = ("valve", vi)
                self.path = []
                self.at_valve(vi)
                return
            best = None
            for j in nb[v]:
                if j in prev and m.code[j] != "V":
                    p = path_to(j)
                    if best is None or len(p) < len(best):
                        best = p
            if best:
                self.goal = ("valve", vi)
                self.path = best
                return
        # 6. home
        self.home_reason = "nothing"
        self.go_home()

    # ---------------- interactions
    def at_basin(self, g):
        gid = g.id
        b = self.bot
        cap = self.gate_cap[gid]
        if self.night:
            self.go_home()
            return
        if self.gate_l[gid] < cap - 1e-6:
            # pour one second's worth (canteen first, then HYD down to the floor)
            rate = self.cfg["locks"]["pour_rate"] * self.hand_mult()
            want = min(rate, cap - self.gate_l[gid])
            take = min(want, self.canteen)
            self.canteen -= take
            got = take
            if got < want and self.cfg["locks"]["pour_from_hyd"]:
                floor = max(self.cfg["locks"]["pour_hyd_floor"], self.need_home_hyd() + b["pour_keep_hyd"])
                from_hyd = min(want - got, max(0.0, self.hyd - floor) / self.hyd_per_l())
                self.hyd -= from_hyd * self.hyd_per_l()
                got += from_hyd
            self.gate_l[gid] += got
            if got <= 1e-6:
                self.trip_failed.add(("gate", gid))
                self.go_home()
                return
            self.goal = ("pour", gid)
            self.start_busy(1, "idle", lambda: self.after_pour(g))
            return
        # full: valve minigame
        if self.real < self.gate_lock_until[gid]:
            self.start_busy(self.gate_lock_until[gid] - self.real, "idle", lambda: self.after_pour(g))
            return
        tries = self.goal[2] if (self.goal and self.goal[0] == "play" and self.goal[1] == gid) else 0
        budget = self.hyd + self.canteen * self.hyd_per_l() - self.cfg["minigame"]["fail_hyd"]
        if tries >= b["gate_attempts"] or budget < self.need_home_hyd() + 5:
            self.trip_failed.add(("gate", gid))
            self.go_home()
            return
        won, secs = self.play_minigame(g.kind, g.ring)

        def done(g=g, won=won, tries=tries):
            if won:
                self.gate_open[g.id] = True
                self.home_dirty = True
                self.first.setdefault("gate", self.real)
                self.first.setdefault("gate_" + g.kind, self.real)
                self.new_thing("gate:" + g.kind)
                self.goal = None
                self.path = []
                if g.kind == "z2exit":
                    # walk out through it
                    self.goal = ("escape", g.id)
                    p = self.bfs(self.pos, lambda j, t=g.outer_tile: j == t, known_only=False)
                    self.path = p or []
            else:
                self.hyd -= self.cfg["minigame"]["fail_hyd"]
                self.valve_fail_real = self.real
                self.gate_lock_until[g.id] = self.real + self.cfg["minigame"]["lockout_s"]
                self.goal = ("play", g.id, tries + 1)
                self.path = []
        self.goal = ("play", gid, tries)
        self.start_busy(secs, "minigame", done)

    def after_pour(self, g):
        self.path = []
        self.at_basin(g)

    def at_valve(self, vi):
        b = self.bot
        v = self.m.valves[vi]
        if self.night or self.real < self.valve_lock_until[vi]:
            self.trip_failed.add(("valve", vi))
            self.goal = None
            return
        if self.hyd + self.canteen * self.hyd_per_l() - self.cfg["minigame"]["fail_hyd"] < self.need_home_hyd() + 10:
            self.trip_failed.add(("valve", vi))
            self.goal = None
            return
        won, secs = self.play_minigame("valve", self.m.ring[v])

        def done():
            if won:
                self.valve_open[vi] = True
                self.home_dirty = True
                rz = 1 if self.m.part[v] == mz.Z2 else 0
                amt = self.cfg["rewards"]["valve_reward"][rz]
                self.unbanked += amt
                self.src["valve"] += amt
                self.first.setdefault("gate", self.real)
                self.new_thing("valve")
            else:
                self.hyd -= self.cfg["minigame"]["fail_hyd"]
                self.valve_fail_real = self.real
                self.valve_lock_until[vi] = self.real + self.cfg["minigame"]["lockout_s"]
                self.trip_failed.add(("valve", vi))
            self.goal = None
            self.path = []
        self.start_busy(secs, "minigame", done)

    def try_door(self, d, nxt):
        b = self.bot
        tries_key = ("door_tries", d)
        tries = self.trip_valve_roll.get(tries_key, 0)
        if tries >= b["door_attempts"] and not self.night:
            self.trip_failed.add(("door", d))
            self.path = []
            self.goal = None
            return
        won, secs = self.play_minigame("door", self.m.doors[d][3])
        self.trip_valve_roll[tries_key] = tries + 1

        def done():
            if won:
                self.door_open[d] = True
                self.home_dirty = True
            # path kept: the next tick steps through if open, or tries again
        self.start_busy(secs, "minigame", done)

    def in_room_actions(self):
        """Read the note and take water when standing in a room."""
        r = self.m.room_of[self.pos]
        if r < 0:
            return False
        if not self.room_note[r]:
            self.room_note[r] = True
            self.new_thing(f"note{r}")
            self.apply_note(r)
            self.start_busy(self.cfg["rooms"]["note_read_s"], "idle")
            return True
        if self.room_cur[r] > 0.01 and self.canteen < self.canteen_cap() - 0.01:
            rate = self.cfg["rooms"]["fill_rate"] * self.hand_mult()
            amt = min(rate, self.room_cur[r], self.canteen_cap() - self.canteen)
            self.room_cur[r] -= amt
            if self.room_kind[r] == "cache" and self.room_cur[r] < 0.01:
                self.room_cur[r] = 0.0
            self.canteen += amt
            self.src["room"] += amt
            self.start_busy(1, "idle")
            return True
        return False

    def apply_note(self, r):
        pay = self.cfg["rooms"]["note_payloads"][r] if r < len(self.cfg["rooms"]["note_payloads"]) else "clue"
        m = self.m
        if pay.startswith("reveal:"):
            tr = int(pay.split(":")[1])
            for t in m.rooms[tr].tiles:
                self._know(t, pay=True)
            self.room_known[tr] = True
        elif pay == "marker":
            # pins the nearest cistern room the bot hasn't located yet
            cands = [i for i in range(len(m.rooms)) if self.room_kind[i] == "cistern" and not self.room_known[i]]
            if cands:
                tr = min(cands, key=lambda i: abs(m.rooms[i].ring - m.rooms[r].ring))
                self.room_known[tr] = True
                for t in m.rooms[tr].tiles:
                    self._know(t, pay=True)

    # ---------------- walking
    def walk_tick(self):
        m = self.m
        b = self.bot
        # room actions first when standing in a room on purpose
        if m.room_of[self.pos] >= 0 and self.goal and self.goal[0] in ("room", "shelter_room") and not self.path:
            if self.in_room_actions():
                return self.do_busy()
            self.goal = None
        if not self.path:
            if self.goal and self.goal[0] == "pour":
                g = m.gates[self.goal[1]]
                if self.pos == self.basin_of(g):
                    self.at_basin(g)
                    if self.busy:
                        return self.do_busy()
            elif self.goal and self.goal[0] == "valve":
                self.at_valve(self.goal[1])
                if self.busy:
                    return self.do_busy()
            elif self.goal and self.goal[0] == "room" and m.room_of[self.pos] >= 0:
                if self.in_room_actions():
                    return self.do_busy()
            return 1.0, "idle", False
        self.move_budget += self.speed_tiles()
        moved = False
        while self.move_budget >= 1.0 and self.path:
            nxt = self.path[-1]
            # door on the way
            d = m.door_of_edge.get((self.pos, nxt))
            if d is not None and not self.door_open[d]:
                if ("door", d) in self.trip_failed:
                    self.path = []
                    break
                self.move_budget = 0.0
                self.try_door(d, nxt)
                return self.do_busy() if self.busy else (1.0, "idle", moved)
            code = m.code[nxt]
            if code == "V" and not self.tile_ok(nxt):
                vi = self.valve_idx[nxt]
                self.path = []
                self.goal = ("valve", vi)
                self.at_valve(vi)
                return self.do_busy() if self.busy else (1.0, "idle", moved)
            if not self.tile_ok(nxt):
                self.path = []
                self.goal = None if (not self.goal or self.goal[0] != "home") else self.goal
                break
            # wrong turn at a junction
            if (self.goal is None or self.goal[0] != "shelter") and len(m.nbrs[self.k][self.pos]) >= 3:
                p_wrong = b["wrong_turn"] * self.wrong_mult
                if self.rng.random() < p_wrong:
                    alt = [j for j in m.nbrs[self.k][self.pos] if j != nxt and self.tile_ok(j)
                           and (self.pos not in m.door_tiles or self.edge_ok(self.pos, j, False))]
                    if alt:
                        self.make_detour(self.rng.choice(alt))
                        nxt = self.path[-1]
            self.path.pop()
            wa, wb = m.l2w[self.k][self.pos], m.l2w[self.k][nxt]
            self.last_dir = (max(-1, min(1, wb % mz.W - wa % mz.W)), max(-1, min(1, wb // mz.W - wa // mz.W)))
            hd = self.home_dist
            if hd is not None and hd[nxt] < 0 and hd[self.pos] >= 0:
                hd[nxt] = hd[self.pos] + 1
            self.pos = nxt
            self.move_budget -= 1.0
            moved = True
            self.on_enter(nxt)
            if self.result is not None:
                break
            if self.mode == "hub":
                self.move_budget = 0.0
                break
        if not self.path:
            self.move_budget = 0.0
        return 1.0, "walk", True

    def make_detour(self, first):
        """A wrong turn: walk up to N tiles down another branch, then come back."""
        m = self.m
        lo, hi = self.bot["wrong_turn_len"]
        n = self.rng.randint(lo, hi)
        out = [first]
        prev, cur = self.pos, first
        for _ in range(n - 1):
            opts = [j for j in m.nbrs[self.k][cur] if j != prev and self.tile_ok(j)
                    and (cur not in m.door_tiles or self.edge_ok(cur, j, False))]
            if not opts:
                break
            prev, cur = cur, self.rng.choice(opts)
            out.append(cur)
        back = list(reversed(out[:-1])) + [self.pos]
        # path is reversed: after the detour and the way back, the old path continues
        self.path.extend(reversed(out + back))

    # ------------------------------------------------------------ results
    def summary(self):
        m = self.m
        rt = sorted(self.reward_times)
        times = [0.0] + [t for t, _ in rt] + [self.real]
        gaps = [(times[i + 1] - times[i]) / 60 for i in range(len(times) - 1)]
        gaps = [g for g in gaps if g > 1e-6] or [self.real / 60]
        tot = max(self.real, 1.0)
        res = {
            "seed": self.seed,
            "result": self.result,
            "escape_day": self.day if self.result == "escaped" else None,
            "end_day": self.day,
            "real_min": round(self.real / 60, 2),
            "collapses": len(self.collapses),
            "collapse_causes": ";".join(c[2] for c in self.collapses),
            "game_over_cause": self.end_cause or "",
            "first_collapse_day": self.collapses[0][1] if self.collapses else None,
            "close_calls": self.close_calls,
            "reward_gap_median_min": round(sorted(gaps)[len(gaps) // 2], 2),
            "reward_gap_longest_min": round(max(gaps), 2),
            "dead_time_frac": round((self.pump_s + self.rewalk_s) / tot, 3),
            "pump_frac": round(self.pump_s / tot, 3),
            "rewalk_frac": round(self.rewalk_s / tot, 3),
            "idle_hub_frac": round(self.idle_hub_s / tot, 3),
            "minigame_frac": round(self.minigame_s / tot, 3),
            "map_pct": round(self.revealed_count / self.map_total, 3),
            "zone1_day": self.zone_first_day.get("zone1"),
            "zone2_day": self.zone_first_day.get("zone2"),
            "items": "|".join(u for u, _, _ in self.bought),
            "n_items": len(self.bought),
            "rooms_found": sum(self.room_found),
        }
        for i, name in enumerate(AREA_NAMES):
            res["t_" + name] = round(self.area_time[i] / 60, 2)
        for k, v in self.src.items():
            res["src_" + k] = round(v, 2)
        for k, (n, w) in self.minigame_log.items():
            res[f"mg_{k}_tries"] = n
            res[f"mg_{k}_wins"] = w
        for key in ("gate", "room", "hub_return", "gate_hub", "gate_z1exit", "zone1", "zone2"):
            v = self.first.get(key)
            res["first_" + key + "_min"] = round(v / 60, 2) if v is not None else None
        res["_trips"] = self.trips
        res["_water_curve"] = self.water_curve
        res["_bought"] = self.bought
        res["_reward_times"] = [(round(t / 60, 2), w) for t, w in rt]
        res["_collapses"] = self.collapses
        res["_visits"] = self.visits
        res["_area_time"] = self.area_time
        if self.path_log is not None:
            res["_path"] = self.path_log
        return res


def _hash01(seed, index):
    """RoomRules.Hash01, bit for bit (uint32 maths)."""
    M = 0xFFFFFFFF
    h = ((seed * 2654435761) & M) ^ (((index + 1) * 2246822519) & M)
    h ^= h >> 15
    h = (h * 2246822519) & M
    h ^= h >> 13
    h = (h * 3266489917) & M
    h ^= h >> 16
    return (h & 0xFFFFFF) / 16777216.0


def _piecewise(points, x):
    if x <= points[0][0]:
        return points[0][1]
    for i in range(1, len(points)):
        if x <= points[i][0]:
            a, b = points[i - 1], points[i]
            t = (x - a[0]) / (b[0] - a[0])
            return a[1] + (b[1] - a[1]) * t
    return points[-1][1]
