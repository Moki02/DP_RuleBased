"""Step 7: builds report.html (one self-contained file: charts are embedded PNGs, replays are
a small canvas script with the path data inline).

    python report.py
Reads results/baseline/runs.pkl, results/baseline/summary.json, results/sweep.json,
results/best_runs.pkl, results/skilled_genome.json (whatever exists).
"""
import base64
import html
import io
import json
import os
import pickle

import matplotlib

matplotlib.use("Agg")
import matplotlib.pyplot as plt  # noqa: E402
import numpy as np  # noqa: E402

from sim import config, metrics  # noqa: E402
from sim import maze as mz  # noqa: E402

ROOT = os.path.dirname(os.path.abspath(__file__))
RES = os.path.join(ROOT, "results")
BOTS = ["normal", "beginner", "skilled"]
COL = {"normal": "#2a78d6", "beginner": "#eb6834", "skilled": "#1baf7a"}  # validated slots 1-3
INK, INK2, GRID, SURF = "#0b0b0b", "#52514e", "#e4e3df", "#fcfcfb"

plt.rcParams.update({
    "font.family": "DejaVu Sans", "font.size": 9, "axes.edgecolor": GRID, "axes.labelcolor": INK2,
    "xtick.color": INK2, "ytick.color": INK2, "axes.grid": True, "grid.color": GRID, "grid.linewidth": 0.6,
    "axes.spines.top": False, "axes.spines.right": False, "figure.facecolor": SURF, "axes.facecolor": SURF,
    "legend.frameon": False,
})


def load(path, kind="json"):
    p = os.path.join(RES, path)
    if not os.path.exists(p):
        return None
    if kind == "pkl":
        with open(p, "rb") as f:
            return pickle.load(f)
    with open(p, encoding="utf8") as f:
        return json.load(f)


def png(fig):
    buf = io.BytesIO()
    fig.savefig(buf, format="png", dpi=110, bbox_inches="tight")
    plt.close(fig)
    return f'<img alt="" src="data:image/png;base64,{base64.b64encode(buf.getvalue()).decode()}">'


def fmt(v, kind=""):
    if v is None:
        return "-"
    if isinstance(v, bool):
        return "yes" if v else "no"
    if kind == "%":
        return f"{v:.0%}"
    if isinstance(v, float):
        return f"{v:.2f}" if abs(v) < 10 else f"{v:.1f}"
    return html.escape(str(v))


def target_table(rows, rows_after=None):
    h = ["<table><tr><th>Bot</th><th>Metric</th><th>Target</th><th>Current</th><th></th>"]
    if rows_after:
        h.append("<th>Recommended</th><th></th>")
    h.append("</tr>")
    after = {(r[0], r[1]): r for r in (rows_after or [])}
    for r in rows:
        pct = "rate" in r[1] or "dead" in r[1] or "within 10" in r[1] or "after 1st" in r[1]
        h.append(f"<tr><td>{r[0]}</td><td>{html.escape(r[1])}</td><td>{html.escape(r[2])}</td>"
                 f"<td>{fmt(r[3], '%' if pct else '')}</td><td>{badge(r[4])}</td>")
        if rows_after:
            a = after.get((r[0], r[1]))
            h.append(f"<td>{fmt(a[3], '%' if pct else '') if a else '-'}</td><td>{badge(a[4]) if a else ''}</td>")
        h.append("</tr>")
    for k, a in after.items():
        if not any((r[0], r[1]) == k for r in rows):
            h.append(f"<tr><td>{a[0]}</td><td>{html.escape(a[1])}</td><td>{html.escape(a[2])}</td><td>-</td><td></td>"
                     f"<td>{fmt(a[3])}</td><td>{badge(a[4])}</td></tr>")
    h.append("</table>")
    return "".join(h)


def badge(ok):
    return '<span class="pass">PASS</span>' if ok else '<span class="fail">FAIL</span>'


def outcome_table(aggs):
    h = ["<table><tr><th>Bot</th><th>Escaped</th><th>Game over</th><th>Still going (day 40)</th>"
         "<th>Escape day: median</th><th>mean</th><th>IQR</th><th>fastest</th><th>Real minutes (median escape)</th>"
         "<th>Collapses/run</th><th>Close calls/run</th><th>Zone 2 reached (median day)</th></tr>"]
    for b in BOTS:
        a = aggs.get(b)
        if not a:
            continue
        h.append(f"<tr><td><span class='sw' style='background:{COL[b]}'></span>{b}</td><td>{fmt(a['escape_rate'],'%')}</td>"
                 f"<td>{fmt(a['game_over_rate'],'%')}</td><td>{fmt(a['timeout_rate'],'%')}</td>"
                 f"<td>{fmt(a['median_escape_day'])}</td><td>{fmt(a['mean_escape_day'])}</td><td>{fmt(a['escape_day_iqr'])}</td>"
                 f"<td>{fmt(a['min_escape_day'])}</td><td>{fmt(a['real_min_median'])}</td><td>{fmt(a['collapses_mean'])}</td>"
                 f"<td>{fmt(a['close_calls_mean'])}</td><td>{fmt(a['zone2_day_median'])}</td></tr>")
    h.append("</table>")
    return "".join(h)


def heatmap(runs, m, title):
    v = np.zeros(m.n)
    for r in runs:
        if r.get("_visits"):
            v += np.array(r["_visits"])
    v /= max(1, len(runs))
    grid = v.reshape(mz.W, mz.W)
    walls = np.array([0 if m.walkable[i] else 1 for i in range(m.n)]).reshape(mz.W, mz.W)
    fig, ax = plt.subplots(figsize=(6.2, 6.2))
    ax.imshow(np.where(walls == 1, 1, np.nan), cmap="Greys", vmin=0, vmax=3.5, interpolation="nearest")
    show = np.where((walls == 0) & (grid > 0), np.log10(grid + 1), np.nan)
    im = ax.imshow(show, cmap="Blues", interpolation="nearest", vmin=0)
    ax.set_xticks([]); ax.set_yticks([]); ax.grid(False)
    ax.set_title(title, color=INK, fontsize=10, loc="left")
    cb = fig.colorbar(im, ax=ax, fraction=0.04, pad=0.02)
    cb.set_label("visits per run (log10 of visits+1)")
    return png(fig)


def curve_charts(aggs):
    out = []
    for key, label in (("banked", "Banked water at dawn (Clean + Raw + spare jugs, L), median run"),
                       ("hyd", "HYD at dawn, median run")):
        fig, ax = plt.subplots(figsize=(6.2, 2.8))
        for b in BOTS:
            a = aggs.get(b)
            if not a:
                continue
            c = a["water_curve"]
            days = sorted(int(d) for d in c)
            ys = [c[d if d in c else str(d)][key] for d in days]
            ax.plot(days, ys, color=COL[b], lw=2, label=b)
            ax.annotate(b, (days[-1], ys[-1]), xytext=(4, 0), textcoords="offset points", color=INK2, fontsize=8, va="center")
        ax.set_xlabel("day"); ax.set_title(label, color=INK, fontsize=10, loc="left")
        ax.legend(loc="upper left", fontsize=8)
        out.append(png(fig))
    return "".join(out)


def bar_h(labels, values, title, color="#2a78d6", xlabel="", target=None, fmt_s="{:.1f}"):
    fig, ax = plt.subplots(figsize=(6.2, 0.32 * len(labels) + 0.9))
    y = np.arange(len(labels))
    ax.barh(y, values, color=color, height=0.62)
    ax.set_yticks(y); ax.set_yticklabels(labels); ax.invert_yaxis()
    for yi, vi in zip(y, values):
        ax.text(vi, yi, " " + fmt_s.format(vi), va="center", fontsize=8, color=INK2)
    if target is not None:
        ax.axvline(target, color=INK2, lw=1, ls="--")
    ax.set_title(title, color=INK, fontsize=10, loc="left"); ax.set_xlabel(xlabel)
    ax.grid(axis="y", visible=False)
    return png(fig)


def causes_chart(aggs, key, title):
    cats = sorted({c for b in BOTS if aggs.get(b) for c in aggs[b][key]})
    if not cats:
        return "<p>None.</p>"
    fig, ax = plt.subplots(figsize=(6.2, 0.45 * len(cats) + 1))
    y = np.arange(len(cats))
    hgt = 0.26
    for i, b in enumerate(BOTS):
        a = aggs.get(b)
        if not a:
            continue
        vals = [a[key].get(c, 0) / a["n"] for c in cats]
        ax.barh(y + (i - 1) * (hgt + 0.02), vals, height=hgt, color=COL[b], label=b)
    ax.set_yticks(y); ax.set_yticklabels(cats); ax.invert_yaxis()
    ax.set_xlabel("per run"); ax.set_title(title, color=INK, fontsize=10, loc="left"); ax.legend(fontsize=8)
    ax.grid(axis="y", visible=False)
    return png(fig)


def gap_chart(runs):
    longest = [r["reward_gap_longest_min"] for r in runs]
    med = [r["reward_gap_median_min"] for r in runs]
    fig, axs = plt.subplots(1, 2, figsize=(6.4, 2.6))
    axs[0].hist(med, bins=20, color="#2a78d6", rwidth=0.92)
    axs[0].axvline(5, color=INK2, ls="--", lw=1)
    axs[0].set_title("Median gap per run (target <= 5 min)", fontsize=9, loc="left", color=INK)
    axs[0].set_xlabel("real minutes")
    axs[1].hist(longest, bins=20, color="#2a78d6", rwidth=0.92)
    axs[1].axvline(12, color=INK2, ls="--", lw=1)
    axs[1].set_title("Longest gap per run (target <= 12 min)", fontsize=9, loc="left", color=INK)
    axs[1].set_xlabel("real minutes")
    for a in axs:
        a.set_ylabel("runs")
    fig.tight_layout()
    return png(fig)


def replay_block(runs_by_bot, m):
    code = "".join({"": ".", "W": "#", "G": "G", "R": "r", "S": "S", "V": "V"}.get(c, ".") for c in m.code)
    data = {}
    for b in BOTS:
        rs = [r for r in runs_by_bot.get(b, []) if r.get("_path")][:3]
        data[b] = [{"seed": r["seed"], "result": r["result"], "day": r["end_day"],
                    "path": [t for _, t in r["_path"]], "time": [round(tt / 60, 2) for tt, _ in r["_path"]]} for r in rs]
    js = json.dumps({"code": code, "runs": data, "colors": COL})
    return f"""
<div class="replay">
  <div class="controls">
    <label>Run <select id="rsel"></select></label>
    <button id="rplay">Play</button>
    <input id="rpos" type="range" min="0" max="1000" value="1000" aria-label="replay position">
    <span id="rinfo"></span>
  </div>
  <canvas id="rcan" width="570" height="570" role="img" aria-label="maze replay"></canvas>
  <p class="note">Paths are drawn on the 0&deg; map (the game's own map stores zone 2 the same way, so a zone 2
  corridor keeps its place on this picture even after zone 2 turns). Older steps fade; the dot is the player.</p>
</div>
<script>
(function(){{
const D={js};
const W=95, cell=6, cv=document.getElementById('rcan'), ctx=cv.getContext('2d');
const sel=document.getElementById('rsel'), pos=document.getElementById('rpos'), info=document.getElementById('rinfo');
const list=[]; for(const b of ['normal','beginner','skilled']) for(const r of (D.runs[b]||[])) list.push([b,r]);
list.forEach(([b,r],i)=>{{const o=document.createElement('option');o.value=i;o.textContent=b+' seed '+r.seed+' ('+r.result+', day '+r.day+')';sel.appendChild(o);}});
const base=document.createElement('canvas'); base.width=cv.width; base.height=cv.height; const bx=base.getContext('2d');
const css=getComputedStyle(document.body);
const wall=css.getPropertyValue('--wall').trim()||'#3b3a37', floor=css.getPropertyValue('--surface').trim()||'#fcfcfb';
for(let i=0;i<W*W;i++){{const c=D.code[i];const x=i%W,y=Math.floor(i/W);
 bx.fillStyle=c==='#'?wall:c==='r'?'#f3e9c6':c==='S'?'#dfeee3':c==='G'?'#c98500':c==='V'?'#9085e9':floor; bx.fillRect(x*cell,y*cell,cell,cell);}}
function draw(){{const [b,r]=list[sel.value]||[]; ctx.drawImage(base,0,0); if(!r) return;
 const n=Math.max(1,Math.round(r.path.length*pos.value/1000)); ctx.strokeStyle=D.colors[b]; ctx.lineWidth=2; ctx.lineJoin='round';
 const start=Math.max(1,n-4000);
 for(let k=start;k<n;k++){{const a=r.path[k-1],c=r.path[k]; const ax=a%W,ay=Math.floor(a/W),cx=c%W,cy=Math.floor(c/W);
   if(Math.abs(ax-cx)+Math.abs(ay-cy)>1) continue; ctx.globalAlpha=0.15+0.85*(k-start)/(n-start);
   ctx.beginPath(); ctx.moveTo(ax*cell+cell/2,ay*cell+cell/2); ctx.lineTo(cx*cell+cell/2,cy*cell+cell/2); ctx.stroke();}}
 ctx.globalAlpha=1; const p=r.path[n-1]; ctx.fillStyle=D.colors[b]; ctx.strokeStyle=floor; ctx.lineWidth=2;
 ctx.beginPath(); ctx.arc((p%W)*cell+cell/2,Math.floor(p/W)*cell+cell/2,5,0,7); ctx.fill(); ctx.stroke();
 info.textContent='step '+n+' / '+r.path.length+'  -  '+r.time[n-1].toFixed(1)+' real min (day '+(Math.floor(r.time[n-1]/6)+1)+' approx.)';}}
let timer=null; document.getElementById('rplay').onclick=function(){{ if(timer){{clearInterval(timer);timer=null;this.textContent='Play';return;}}
 if(+pos.value>=1000) pos.value=0; this.textContent='Pause'; timer=setInterval(()=>{{pos.value=Math.min(1000,+pos.value+2); draw(); if(+pos.value>=1000){{clearInterval(timer);timer=null;document.getElementById('rplay').textContent='Play';}}}},30);}};
sel.onchange=()=>{{pos.value=1000; draw();}}; pos.oninput=draw; draw();
}})();
</script>"""


def variety_table(v, title):
    if not v:
        return ""
    best = v["best"]
    h = [f"<h3>{html.escape(title)}</h3><p>{v['within_15pct']} buy orders within 15% of the best median escape day "
         f"(best {fmt(best)}). Non-escapes count as 'never' (99) in the median.</p>"
         "<table><tr><th>Buy order</th><th>Median escape day (all runs)</th><th>Median (escapers)</th><th>Escaped</th><th>Game over</th><th>Within 15%</th><th>First items</th></tr>"]
    for name, r in sorted(v["orders"].items(), key=lambda kv: kv[1]["median_all"]):
        h.append(f"<tr><td>{html.escape(name)}</td><td>{fmt(r['median_all'])}</td><td>{fmt(r['median_escapers'])}</td>"
                 f"<td>{fmt(r['escape_rate'],'%')}</td><td>{fmt(r['game_over'],'%')}</td>"
                 f"<td>{badge(r['median_all'] <= best*1.15)}</td><td>{', '.join(r['order'][:6])}</td></tr>")
    h.append("</table>")
    return "".join(h)


def main():
    cfg = config.balance()
    m = mz.load(cfg["_maze_path"], (cfg["maze"]["hub_max_ring"], cfg["maze"]["zone1_max_ring"], cfg["maze"]["zone2_max_ring"]))
    targets = config.load_json("targets.json")
    base_runs = load("baseline/runs.pkl", "pkl") or {}
    base_aggs = {b: metrics.aggregate(r) for b, r in base_runs.items()}
    sweep = load("sweep.json")
    best_runs = load("best_runs.pkl", "pkl")
    best_aggs = {b: metrics.aggregate(r) for b, r in best_runs.items()} if best_runs else None
    genome = load("skilled_genome.json")
    variety_base = sweep["variety_baseline"]["within_15pct"] if sweep else None
    rows_base = metrics.check_targets(base_aggs, targets, variety_base)
    rows_best = sweep["best_final_rows"] if sweep else None

    S = []
    S.append("<h1>Salok balance sim</h1>")
    S.append(f"<p class='lede'>200 headless runs per bot on the real maze (Maze2Layer_0deg.csv with its zone 2 turns). "
             f"Rules and sources: <code>RULES.md</code>. Every number: <code>config/balance.json</code>. "
             f"Targets: <code>config/targets.json</code>. A game day is {cfg['clock']['day_length_s']}+{cfg['clock']['night_lockdown_s']} s "
             f"= {(cfg['clock']['day_length_s']+cfg['clock']['night_lockdown_s'])/60:g} real minutes.</p>")

    S.append("<h2>Targets</h2>")
    if rows_best:
        S.append("<p>Current values vs the recommended setup (see RECOMMENDATIONS.md).</p>")
    S.append(target_table(rows_base, rows_best))

    S.append("<h2>Outcomes: current values</h2>" + outcome_table(base_aggs))
    if best_aggs:
        S.append("<h2>Outcomes: recommended setup</h2>" + outcome_table(best_aggs))
        S.append("<p>Changed: " + ", ".join(f"<code>{html.escape(k)} = {html.escape(str(v))}</code>"
                                            for k, v in sweep["best"]["setting"].items()) + "</p>")

    if base_aggs.get("normal"):
        n = base_aggs["normal"]
        S.append("<h2>Sanity check: days 1 to 3 (Normal bot, current values)</h2>")
        S.append(f"<table><tr><th>Event</th><th>Median</th></tr>"
                 f"<tr><td>First gate opened (hub gate)</td><td>{fmt(np.median([r['first_gate_hub_min'] for r in base_runs['normal'] if r['first_gate_hub_min'] is not None]))} real min</td></tr>"
                 f"<tr><td>First room entered</td><td>{fmt(np.median([r['first_room_min'] for r in base_runs['normal'] if r['first_room_min'] is not None]))} real min</td></tr>"
                 f"<tr><td>Zone 1 exit opened</td><td>{fmt(n['zone1_exit_min_median'])} real min (day {int(n['zone1_exit_min_median']//6)+1 if n['zone1_exit_min_median'] else '-'})</td></tr>"
                 f"<tr><td>Zone 2 reached</td><td>day {fmt(n['zone2_day_median'])}</td></tr></table>"
                 "<p>Designer playthrough: reached zone 2 on day 3 (knowing the map).</p>")

    S.append("<h2>Where the time goes (Normal, current values)</h2>")
    if base_runs.get("normal"):
        S.append(heatmap(base_runs["normal"], m, "Normal bot: time spent per tile (200 runs)"))
        area = base_aggs["normal"]["area_minutes"]
        S.append(bar_h(list(area.keys()), list(area.values()), "Real minutes per run in each area (mean)", xlabel="minutes"))
        a = base_aggs["normal"]
        S.append(f"<p>Dead time {fmt(a['dead_time'],'%')} = pumping {fmt(a['pump_frac'],'%')} + walking known tiles "
                 f"{fmt(a['rewalk_frac'],'%')}. Idle in the hub (waiting for dawn / water): {fmt(a['idle_hub_frac'],'%')}.</p>")

    S.append("<h2>Replays (3 runs per bot)</h2>")
    S.append(replay_block(base_runs, m))

    S.append("<h2>Water curve</h2>" + curve_charts(base_aggs))
    if base_aggs.get("normal"):
        src = base_aggs["normal"]["sources_l"]
        S.append(bar_h(list(src.keys()), list(src.values()), "Where the Normal bot's water came from (L per run, mean)", xlabel="litres"))

    S.append("<h2>Collapses and game overs</h2>")
    S.append(causes_chart(base_aggs, "collapse_causes", "Collapse causes (per run)"))
    S.append(causes_chart(base_aggs, "game_over_causes", "Game-over causes (share of runs)"))
    if base_aggs.get("normal"):
        a = base_aggs["normal"]
        S.append(f"<p>Death spiral check (Normal): win rate after a collapse {fmt(a['win_rate_after_collapse'],'%')} "
                 f"vs overall {fmt(a['escape_rate'],'%')}.</p>")

    S.append("<h2>Reward cadence (Normal)</h2>")
    if base_runs.get("normal"):
        S.append(gap_chart(base_runs["normal"]))
        S.append("<p>A 'new thing' = upgrade bought, new room, note read, new zone, gate or valve opened.</p>")

    S.append("<h2>Demo window (first 5 and 10 real minutes)</h2><table><tr><th>Bot</th><th>First gate &le;5</th><th>&le;10</th>"
             "<th>First room &le;5</th><th>&le;10</th><th>First hub return &le;5</th><th>&le;10</th></tr>")
    for b in BOTS:
        a = base_aggs.get(b)
        if a:
            S.append(f"<tr><td>{b}</td>" + "".join(f"<td>{fmt(a[k],'%')}</td>" for k in
                     ("demo_gate_5", "demo_gate_10", "demo_room_5", "demo_room_10", "demo_return_5", "demo_return_10")) + "</tr>")
    S.append("</table>")

    S.append("<h2>Minigames</h2><table><tr><th>Bot</th>" + "".join(f"<th>{k}: tries/run</th><th>win %</th>" for k in ("hub", "z1exit", "z2exit", "valve", "door")) + "</tr>")
    for b in BOTS:
        a = base_aggs.get(b)
        if a:
            S.append(f"<tr><td>{b}</td>" + "".join(f"<td>{fmt(a['minigames'][k]['tries_per_run'])}</td><td>{fmt(a['minigames'][k]['win_rate'],'%')}</td>"
                                                   for k in ("hub", "z1exit", "z2exit", "valve", "door")) + "</tr>")
    S.append("</table>")

    S.append("<h2>Build and Skills</h2>")
    if base_aggs.get("normal"):
        allu = list(cfg["upgrades"].keys())
        share = base_aggs["beginner"]["items_bought_share"] if base_aggs.get("beginner") else {}
        never = [u for u in allu if all(u not in base_aggs[b]["items_bought_share"] for b in BOTS if base_aggs.get(b))]
        S.append(f"<p>Never bought by any bot: {', '.join(never) or 'none'}. "
                 f"No effect in the sim or the game: P2 SODIS and P3 Chlorine (not in the game code), M2/M3 (bots don't sprint), "
                 f"V2/V6 only reduce wrong turns.</p>")
        items = sorted(share.items(), key=lambda kv: -kv[1])
        if items:
            S.append(bar_h([f"{k} {cfg['upgrades'][k]['name']}" for k, _ in items], [v for _, v in items],
                           "Beginner (random buys): share of runs that bought each item", color=COL["beginner"], fmt_s="{:.0%}"))
    if sweep:
        S.append(variety_table(sweep.get("variety_baseline"), "Build variety: current values"))
        S.append(variety_table(sweep.get("variety_best"), "Build variety: recommended setup"))

    if genome:
        S.append("<h2>Skilled bot (evolved)</h2>")
        S.append(f"<p>Fastest strategy found: escape rate {fmt(genome['escape_rate'],'%')}, median day {fmt(genome['median_day'])}, "
                 f"fastest day {fmt(genome['min_day'])} on 100 fresh seeds.</p>"
                 f"<p>Escape days found: {html.escape(', '.join(f'day {d}: {c}' for d, c in sorted(__import__('collections').Counter(genome['days']).items())))}.</p>"
                 f"<pre>{html.escape(json.dumps(genome['genome'], indent=1))}</pre>")

    fp = os.path.join(RES, "findings.html")
    if os.path.exists(fp):
        with open(fp, encoding="utf8") as f:
            S.append("<h2>Findings (read these first)</h2>" + f.read())

    if sweep:
        S.append("<h2>Sensitivity ranking</h2>")
        rank = sweep["sensitivity_rank"]
        sens = sweep["sensitivity"]
        S.append(bar_h(rank, [sens[k]["effect"]["score"] for k in rank],
                       "How much each knob moves the results (low vs high value)", xlabel="effect score"))
        S.append("<table><tr><th>Knob</th><th>Low</th><th>High</th><th>Normal median day low &rarr; high</th>"
                 "<th>Normal escape low &rarr; high</th><th>Normal game over low &rarr; high</th><th>Skilled median low &rarr; high</th></tr>")
        for k in rank:
            s = sens[k]
            lo, hi = s["low"]["key"], s["high"]["key"]
            S.append(f"<tr><td>{k}</td><td>{s['low']['value']}</td><td>{s['high']['value']}</td>"
                     f"<td>{fmt(lo['normal_median_day'])} &rarr; {fmt(hi['normal_median_day'])}</td>"
                     f"<td>{fmt(lo['normal_escape'],'%')} &rarr; {fmt(hi['normal_escape'],'%')}</td>"
                     f"<td>{fmt(lo['normal_game_over'],'%')} &rarr; {fmt(hi['normal_game_over'],'%')}</td>"
                     f"<td>{fmt(lo['skilled_median_day'])} &rarr; {fmt(hi['skilled_median_day'])}</td></tr>")
        S.append("</table><p>Median days count a run that never escapes as 99, so a big jump means the median slipped past day 40.</p>")
        S.append("<h3>Not modelled</h3><ul>" + "".join(f"<li><b>{k}</b>: {html.escape(v)}</li>" for k, v in sweep["not_modelled"].items()) + "</ul>")
        S.append("<h3>Closest setups</h3><table><tr><th>Passed</th><th>Distance</th><th>Setting</th></tr>")
        for r in (sweep["refined"] + sweep["coarse"])[:0] or sorted(sweep["refined"] + sweep["coarse"], key=lambda r: (-r["passed"], r["closeness"]))[:8]:
            S.append(f"<tr><td>{r['passed']}/{r['of']}</td><td>{r['closeness']}</td><td><code>{html.escape(json.dumps(r['setting']))}</code></td></tr>")
        S.append("</table>")

    body = "\n".join(S)
    page = f"""<!doctype html><html lang="en"><head><meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1"><title>Salok Balance Report</title>
<style>
:root{{--surface:#fcfcfb;--ink:#0b0b0b;--ink2:#52514e;--rule:#e4e3df;--wall:#3b3a37;--pass:#0a7d32;--fail:#b3261e;color-scheme:light}}
body{{background:var(--surface);color:var(--ink);font:14px/1.5 system-ui,Segoe UI,sans-serif;max-width:1000px;margin:0 auto;padding:24px 16px}}
h1{{font-size:24px;margin:0 0 4px}} h2{{font-size:18px;margin:32px 0 8px;border-top:1px solid var(--rule);padding-top:16px}} h3{{font-size:15px}}
.lede,.note{{color:var(--ink2)}} img{{max-width:100%;height:auto;display:block;margin:8px 0}}
table{{border-collapse:collapse;margin:8px 0;font-size:13px;display:block;overflow-x:auto;max-width:100%}}
th,td{{border-bottom:1px solid var(--rule);padding:4px 8px;text-align:left;vertical-align:top}} th{{color:var(--ink2);font-weight:600}}
.pass{{color:var(--pass);font-weight:700}} .fail{{color:var(--fail);font-weight:700}}
.sw{{display:inline-block;width:10px;height:10px;border-radius:2px;margin-right:6px}}
code,pre{{font-family:Consolas,monospace;font-size:12px}} pre{{background:#f3f2ee;padding:8px;overflow:auto;max-height:260px}}
.replay canvas{{max-width:100%;height:auto;border:1px solid var(--rule)}} .controls{{display:flex;gap:8px;flex-wrap:wrap;align-items:center;margin:6px 0}}
#rpos{{flex:1;min-width:160px}}
</style></head><body>{body}</body></html>"""
    with open(os.path.join(ROOT, "report.html"), "w", encoding="utf8") as f:
        f.write(page)
    print("wrote report.html", len(page) // 1024, "KB")


if __name__ == "__main__":
    main()
