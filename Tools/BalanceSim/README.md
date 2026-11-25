# Salok balance sim

Headless Python model of Salok (the real 2-layer maze, hub, zone 1, zone 2 that turns,
lock basins, valve minigame, doors, rooms, day/night lockdown, collapse, Build & Skills,
events) with three player bots. Nothing here touches Unity.

Python 3.12 + numpy, pandas, matplotlib. Python lives at
`%LOCALAPPDATA%\Programs\Python\Python312\python.exe` (installed with winget; not on PATH).

| File | What it is |
|---|---|
| `RULES.md` | Every rule and number, with the Unity file/line it came from |
| `config/balance.json` | All game numbers (edit these, not the code) |
| `config/bots.json` | Beginner / Normal / Skilled strategy settings, hit rates |
| `config/targets.json` | Pass/fail targets |
| `sim/maze.py` | Loads `Maze2Layer_0deg.csv`; zone 2 rotation, gates, rooms, doors, sections |
| `sim/engine.py` | The game rules + the bot (1 s steps) |
| `run_baseline.py` | 200 runs per bot on the current values → `results/baseline/*.csv` |
| `evolve.py` | Evolves the Skilled bot → `results/skilled_genome.json` |
| `sweep.py` | Sensitivity + search + build-order comparison → `results/sweep.json` |
| `recommend.py` | Writes `RECOMMENDATIONS.md` from the sweep |
| `report.py` | Writes `report.html` (open it in a browser) |

Typical run (from this folder):

```powershell
$py = "$env:LOCALAPPDATA\Programs\Python\Python312\python.exe"
& $py run_baseline.py      # ~1 min
& $py evolve.py            # ~10 min, only when rules change a lot
& $py sweep.py             # ~1 h (sweep.py --quick to try it)
& $py recommend.py
& $py report.py
```

Same seed + same config = same result. CSV columns are one run per row (escape day, real
minutes, collapses and causes, close calls, reward gaps, dead time, time per area and
room, water sources, minigame tries/wins, first-time milestones in real minutes).
