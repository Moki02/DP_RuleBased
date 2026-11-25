"""The real Salok maze (Maze2Layer_0deg.csv), loaded once.

Everything is stored in LOCAL (0 degree, CSV) tile indices, like the game's map
(MapReveal stores local tiles). Zone 2 (rings 28-44) turns 90 deg clockwise per turn;
only the adjacency across its borders changes, so each orientation k has its own
neighbour lists, again in local indices. idx = y * W + x.
"""
import csv
import math
import os
from collections import deque

W = 95
C = 47
STEPS = ((0, -1), (1, 0), (0, 1), (-1, 0))  # N E S W (sheet coords, y grows south)

HUB, Z1, Z2, OUTER = 0, 1, 2, 3


def ring_of(x, y):
    return max(abs(x - C), abs(y - C))


class Gate:
    __slots__ = ("id", "kind", "tiles", "inner", "basin", "ring", "outer_tile", "outer_ring")

    def __init__(self, gid, kind, tiles, inner, basin, ring, outer_tile, outer_ring):
        self.id, self.kind, self.tiles, self.inner, self.basin = gid, kind, tiles, inner, basin
        self.ring, self.outer_tile, self.outer_ring = ring, outer_tile, outer_ring


class Room:
    __slots__ = ("id", "tiles", "centre", "ring", "part", "rect")

    def __init__(self, rid, tiles, centre, ring, part, rect):
        self.id, self.tiles, self.centre, self.ring, self.part, self.rect = rid, tiles, centre, ring, part, rect


class Maze:
    def __init__(self, csv_path, rings):
        self.hub_max, self.z1_max, self.z2_max = rings
        with open(csv_path, newline="") as f:
            rows = [[c.strip() for c in r] for r in csv.reader(f)]
        assert len(rows) == W and all(len(r) == W for r in rows), "maze must be 95x95"
        n = W * W
        self.n = n
        self.code = [rows[i // W][i % W] for i in range(n)]
        self.x = [i % W for i in range(n)]
        self.y = [i // W for i in range(n)]
        self.ring = [ring_of(i % W, i // W) for i in range(n)]
        self.part = [self._part(r) for r in self.ring]
        self.walkable = [c != "W" for c in self.code]
        self.rotates = [self.z1_max < r <= self.z2_max for r in self.ring]
        self.centre = C * W + C

        # local -> world and world -> local for k = 0..3 turns
        self.l2w = []
        self.w2l = []
        for k in range(4):
            l2w = list(range(n))
            for i in range(n):
                if self.rotates[i]:
                    x, y = i % W, i // W
                    for _ in range(k):
                        x, y = 94 - y, x
                    l2w[i] = y * W + x
            w2l = [0] * n
            for i, j in enumerate(l2w):
                w2l[j] = i
            self.l2w.append(l2w)
            self.w2l.append(w2l)

        # neighbour lists per orientation (local indices), walkable tiles only
        self.nbrs = []
        for k in range(4):
            l2w, w2l = self.l2w[k], self.w2l[k]
            lst = [()] * n
            for i in range(n):
                if not self.walkable[i]:
                    continue
                wi = l2w[i]
                wx, wy = wi % W, wi // W
                out = []
                for dx, dy in STEPS:
                    nx, ny = wx + dx, wy + dy
                    if 0 <= nx < W and 0 <= ny < W:
                        j = w2l[ny * W + nx]
                        if self.walkable[j]:
                            out.append(j)
                lst[i] = tuple(out)
            self.nbrs.append(lst)

        self.hub_tiles = [i for i in range(n) if self.code[i] == "S"]
        self.is_hub = [c == "S" for c in self.code]
        self._find_gates()
        self._find_rooms()
        self._find_doors()
        self.valves = [i for i in range(n) if self.code[i] == "V"]
        self.map_tiles = [i for i in range(n) if self.walkable[i]]
        self.floor_like = [c in ("", "R") for c in self.code]  # pays the 0.03 L tile reward

    def _part(self, r):
        if r <= self.hub_max:
            return HUB
        if r <= self.z1_max:
            return Z1
        if r <= self.z2_max:
            return Z2
        return OUTER

    def area_of(self, i, room_of):
        """'hub', 'zone1', 'zone2', 'room<N>' for time-per-area stats."""
        r = room_of[i]
        if r >= 0:
            return r + 3
        p = self.part[i]
        return 0 if p == HUB else (1 if p == Z1 else 2)

    def _find_gates(self):
        seen = set()
        gates = []
        for i in range(self.n):
            if self.code[i] != "G" or i in seen:
                continue
            comp, q = [], deque([i])
            seen.add(i)
            while q:
                a = q.popleft()
                comp.append(a)
                ax, ay = a % W, a // W
                for dx, dy in STEPS:
                    nx, ny = ax + dx, ay + dy
                    if 0 <= nx < W and 0 <= ny < W:
                        b = ny * W + nx
                        if self.code[b] == "G" and b not in seen:
                            seen.add(b)
                            q.append(b)
            inner = min(comp, key=lambda t: self.ring[t])
            outer = max(comp, key=lambda t: self.ring[t])
            r = self.ring[inner]
            kind = "hub" if r <= self.hub_max else ("z1exit" if r <= self.z1_max else "z2exit")
            ix, iy = inner % W, inner // W
            dx, dy = C - ix, C - iy
            step = (1 if dx > 0 else -1, 0) if abs(dx) >= abs(dy) else (0, 1 if dy > 0 else -1)
            basin = (iy + step[1]) * W + (ix + step[0])
            gates.append(Gate(len(gates), kind, tuple(comp), inner, basin, r, outer, self.ring[outer]))
        self.gates = gates
        self.gate_of_tile = [-1] * self.n
        for g in gates:
            for t in g.tiles:
                self.gate_of_tile[t] = g.id

    def _find_rooms(self):
        seen = set()
        comps = []
        for i in range(self.n):
            if self.code[i] != "R" or i in seen:
                continue
            comp, q = [], deque([i])
            seen.add(i)
            while q:
                a = q.popleft()
                comp.append(a)
                ax, ay = a % W, a // W
                for dx, dy in STEPS:
                    nx, ny = ax + dx, ay + dy
                    if 0 <= nx < W and 0 <= ny < W:
                        b = ny * W + nx
                        if self.code[b] == "R" and b not in seen:
                            seen.add(b)
                            q.append(b)
            comps.append(comp)
        # Game order (RoomNotes.cs): top to bottom, then left to right
        comps.sort(key=lambda c: (min(t // W for t in c), min(t % W for t in c)))
        self.rooms = []
        self.room_of = [-1] * self.n
        for rid, comp in enumerate(comps):
            xs = [t % W for t in comp]
            ys = [t // W for t in comp]
            cx = (min(xs) + max(xs)) // 2
            cy = (min(ys) + max(ys)) // 2
            centre = cy * W + cx
            room = Room(rid, tuple(comp), centre, self.ring[centre], self._part(self.ring[centre]),
                        (min(xs), min(ys), max(xs), max(ys)))
            self.rooms.append(room)
            for t in comp:
                self.room_of[t] = rid

    def _find_doors(self):
        """RoomDoorLayout.Find: one door per edge between a room tile and a non-wall,
        non-room tile. Doors are stored as frozenset edges of local indices."""
        self.doors = []  # (room id, inside, outside, ring)
        self.door_of_edge = {}
        self.door_tiles = set()
        for room in self.rooms:
            rs = set(room.tiles)
            for t in room.tiles:
                tx, ty = t % W, t // W
                for dx, dy in STEPS:
                    nx, ny = tx + dx, ty + dy
                    if not (0 <= nx < W and 0 <= ny < W):
                        continue
                    o = ny * W + nx
                    if o in rs or self.code[o] in ("W", "R"):
                        continue
                    did = len(self.doors)
                    self.doors.append((room.id, t, o, self.ring[t]))
                    self.door_of_edge[(t, o)] = did
                    self.door_of_edge[(o, t)] = did
                    self.door_tiles.add(t)
                    self.door_tiles.add(o)

    def sections(self, z1_count, z2_count):
        """MazeSections.cs: zone 1 cut into z1_count angle slices, zone 2 into z2_count.
        Returns (section_of list, tiles per section, part per section)."""
        sec_of = [-1] * self.n
        total = z1_count + z2_count
        tiles = [[] for _ in range(total)]
        parts = [Z1] * z1_count + [Z2] * z2_count
        for i in range(self.n):
            p = self.part[i]
            if p not in (Z1, Z2):
                continue
            x, y = i % W, i // W
            ang = math.degrees(math.atan2(x - C, -(y - C)))
            if ang < 0:
                ang += 360
            cnt = z1_count if p == Z1 else z2_count
            s = min(cnt - 1, int(math.floor(ang / (360.0 / cnt))))
            if p == Z2:
                s += z1_count
            sec_of[i] = s
            if self.walkable[i]:
                tiles[s].append(i)
        return sec_of, tiles, parts


_CACHE = {}


def load(csv_path, rings):
    key = (os.path.abspath(csv_path), tuple(rings))
    if key not in _CACHE:
        _CACHE[key] = Maze(csv_path, rings)
    return _CACHE[key]
