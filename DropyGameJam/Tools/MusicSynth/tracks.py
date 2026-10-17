"""Compositions. Usage: python tracks.py OUT_DIR name [name ...]"""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from synth import (SR, Track, bass, boom, clap, drone, epiano, felt_piano, hat, kick, marimba,  # noqa: E402
                   midi, pad, pluck, reverb_ir, rumble, snare, tick, tom)


def drums(tr, bar0, bar1, kicks=(0, 2.5), snares=(1, 3), steps=16, hat_vel=0.32, kick_vel=0.75,
          snare_vel=0.7, back=snare, swing=0.0):
    for bar in range(bar0, bar1):
        b = bar * 4
        for k in kicks:
            tr.add(b + k, kick(kick_vel), send=0.03)
        for s in snares:
            tr.add(b + s, back(snare_vel, rng=tr.rng), pan=0.05, send=0.18)
        for i in range(steps):
            pos = i * 4 / steps
            if swing and i % 2 == 1:
                pos += swing * 4 / steps
            v = hat_vel * (1.0 if i % 4 == 0 else 0.75 if i % 2 == 0 else 0.5)
            tr.add(b + pos, hat(v, rng=tr.rng), pan=0.3, send=0.06)


def bassline(tr, b0, root, pattern, vel=0.8):
    for off, semi, ln in pattern:
        tr.note(b0 + off, bass, midi(root) + semi, ln, vel=vel, send=0.04)


def melody(tr, b0, inst, notes, vel=0.7, pan=0.15, send=0.4, transpose=0):
    beat = b0
    for nm, ln in notes:
        if nm is not None:
            tr.note(beat, inst, midi(nm) + transpose, ln, vel=vel, pan=pan, send=send)
        beat += ln


GROOVE_8 = [(0, 0, 0.45), (0.5, 0, 0.45), (1, 12, 0.45), (1.5, 0, 0.45), (2, 7, 0.45), (2.5, 0, 0.45), (3, 12, 0.45), (3.5, 7, 0.45)]
FUNK = [(0, 0, 0.5), (0.75, 0, 0.25), (1.5, 12, 0.4), (2, 7, 0.5), (2.75, 0, 0.25), (3, 10, 0.4), (3.5, 7, 0.45)]
PULSE = [(i * 0.5, 0, 0.35) for i in range(8)]


def menu(out):
    tr = Track(bpm=96, bars=16, seed=2)
    prog = [(["D3", "F3", "A3", "C4", "E4"], ["D4", "F4", "A4"], "D2"),
            (["Bb2", "F3", "A3", "D4"], ["D4", "F4", "Bb4"], "Bb1"),
            (["A2", "E3", "F3", "C4"], ["C4", "F4", "A4"], "F2"),
            (["C3", "G3", "C4", "D4"], ["C4", "E4", "G4"], "C2")]
    tunes = [[("A4", 1.5), ("C5", 0.5), ("A4", 1), ("G4", 1), ("F4", 1.5), ("G4", 0.5), ("E4", 2)],
             [("D5", 1.5), ("C5", 0.5), ("Bb4", 1), ("A4", 1), ("F4", 2), ("A4", 2)],
             [("C5", 1), ("A4", 0.5), ("C5", 0.5), ("G4", 1), ("F4", 1), ("E4", 1.5), ("F4", 0.5), ("G4", 2)],
             [("E4", 0.5), ("F4", 0.5), ("G4", 1), ("C5", 1), ("Bb4", 1), ("A4", 2), ("G4", 2)]]
    for half in range(2):
        for i, (ep, pd, bs) in enumerate(prog):
            b0 = half * 32 + i * 8
            tr.chord(b0, pad, pd, 8, vel=0.6, spread=0.8, send=0.5)
            for bar in range(2):
                for st in (0, 1.5, 3):
                    tr.chord(b0 + bar * 4 + st, epiano, ep, 0.7, vel=0.75, send=0.3)
                if b0 >= 8 or half:
                    bassline(tr, b0 + bar * 4, bs, GROOVE_8, vel=0.75)
            if b0 >= 16:
                melody(tr, b0, epiano, tunes[i], vel=0.8, send=0.4)
                melody(tr, b0, pluck, tunes[i], vel=0.4, pan=-0.2, send=0.4, transpose=12)
            if half == 1:
                arp = [midi(n) + 12 for n in ep[1:4]]
                for k in range(32):
                    tr.note(b0 + k * 0.25, pluck, arp[k % 3] + (12 if k % 8 == 7 else 0), 0.25, vel=0.25, pan=-0.45, send=0.35)
    drums(tr, 0, 2, kicks=(0,), snares=(), steps=8, hat_vel=0.25)
    drums(tr, 2, 16, kicks=(0, 1.75, 2.5))
    return tr.render(out, loop=True, target_rms=0.12)


def office_calm(out):
    tr = Track(bpm=112, bars=16, seed=3)
    prog = [(["F3", "A3", "C4", "E4"], ["F3", "C4"], "F1"),
            (["E3", "G3", "A3", "C4"], ["A2", "E3"], "A1"),
            (["D3", "F3", "A3", "Bb3"], ["Bb2", "F3"], "Bb1"),
            (["E3", "G3", "Bb3", "C4"], ["C3", "G3"], "C2")]
    lead = [[("C6", 0.5), ("A5", 0.5), ("G5", 0.5), ("F5", 1), (None, 0.5), ("G5", 0.5), ("A5", 0.5), ("C6", 1.5), (None, 2.5)],
            [("E6", 0.5), ("C6", 0.5), ("A5", 1), ("G5", 0.5), ("E5", 1), (None, 0.5), ("A5", 1), (None, 3)],
            [("D6", 0.5), ("C6", 0.5), ("Bb5", 0.5), ("A5", 1), (None, 0.5), ("F5", 0.5), ("G5", 0.5), ("A5", 2), (None, 2)],
            [("G5", 0.5), ("A5", 0.5), ("Bb5", 0.5), ("C6", 1.5), ("Bb5", 0.5), ("A5", 0.5), ("G5", 2), (None, 2)]]
    for rep in range(2):
        for i, (tones, pd, bs) in enumerate(prog):
            b0 = rep * 32 + i * 8
            up = [midi(n) + 12 for n in tones]
            pattern = [0, 2, 1, 3, 2, 1, 3, 2]
            for bar in range(2):
                for k, idx in enumerate(pattern):
                    tr.note(b0 + bar * 4 + k * 0.5, marimba, up[idx], 0.5, vel=0.6 if k % 2 == 0 else 0.42, pan=0.25, send=0.22)
                tr.note(b0 + bar * 4 + 3.75, marimba, up[3] + 12, 0.25, vel=0.35, pan=0.3, send=0.22)
                bassline(tr, b0 + bar * 4, bs, FUNK, vel=0.75)
            tr.chord(b0, pad, [midi(n) + 12 for n in pd], 8, vel=0.4, spread=0.7, send=0.5, bright=1200.0)
            if b0 >= 16:
                melody(tr, b0, pluck, lead[i], vel=0.5, pan=-0.25, send=0.35, transpose=-12)
    drums(tr, 0, 2, kicks=(0, 2), snares=(), steps=8, hat_vel=0.22, kick_vel=0.55)
    drums(tr, 2, 16, kicks=(0, 1.75, 2.5), back=clap, snare_vel=0.75, hat_vel=0.28, kick_vel=0.65)
    for bar in range(16):
        for b in range(4):
            tr.add(bar * 4 + b + 0.5, tick(0.3), pan=-0.35, send=0.08)
    return tr.render(out, loop=True, ir=reverb_ir(rt60=1.3, seconds=1.8), target_rms=0.12)


def office_tense(out):
    tr = Track(bpm=118, bars=16, seed=4)
    prog = [(["D3", "F3", "A3"], "D2"), (["Bb2", "D3", "F3"], "Bb1"), (["G2", "Bb2", "D3"], "G1"), (["A2", "C#3", "E3"], "A1")]
    riff = [[("D5", 0.5), ("C#5", 0.5), ("D5", 0.5), ("E5", 0.5), ("F5", 1), ("E5", 0.5), ("D5", 0.5), ("A4", 2), (None, 2)],
            [("F5", 0.5), ("E5", 0.5), ("F5", 0.5), ("G5", 0.5), ("A5", 1), ("G5", 0.5), ("F5", 0.5), ("D5", 2), (None, 2)],
            [("G5", 0.5), ("F5", 0.5), ("E5", 0.5), ("D5", 0.5), ("Bb4", 1), ("D5", 1), ("G4", 2), (None, 2)],
            [("A4", 0.5), ("C#5", 0.5), ("E5", 0.5), ("G5", 0.5), ("F5", 1), ("E5", 1), ("C#5", 2), (None, 2)]]
    for rep in range(2):
        for i, (tones, bs) in enumerate(prog):
            b0 = rep * 32 + i * 8
            up = [midi(n) + 12 for n in tones]
            for bar in range(2):
                for k in range(16):
                    n = up[[0, 1, 2, 1][k % 4]] + (12 if k in (6, 14) else 0)
                    tr.note(b0 + bar * 4 + k * 0.25, pluck, n, 0.25, vel=0.42 if k % 4 == 0 else 0.28, pan=0.3, send=0.2)
                bassline(tr, b0 + bar * 4, bs, PULSE, vel=0.8)
            tr.chord(b0, pad, [midi(n) + 12 for n in tones], 8, vel=0.55, spread=0.8, send=0.5, bright=1000.0)
            if rep == 1 or i >= 2:
                melody(tr, b0, epiano, riff[i], vel=0.7, pan=-0.15, send=0.35, transpose=-12)
    drums(tr, 0, 2, kicks=(0, 1, 2, 3), snares=(), steps=8, hat_vel=0.25, kick_vel=0.6)
    drums(tr, 2, 16, kicks=(0, 1, 2, 3), snares=(1, 3), hat_vel=0.3, kick_vel=0.7)
    for bar in range(16):
        for e in range(8):
            tr.add(bar * 4 + e * 0.5 + 0.25, tick(0.35), pan=-0.4, send=0.06)
    return tr.render(out, loop=True, ir=reverb_ir(rt60=1.6), target_rms=0.12)


def evening(out):
    tr = Track(bpm=92, bars=16, seed=5)
    prog = [(["Ab3", "C4", "Eb4", "G4"], "Ab1"), (["F3", "Ab3", "C4", "Eb4"], "F1"),
            (["Db3", "F3", "Ab3", "C4"], "Db2"), (["Eb3", "Ab3", "Bb3", "Db4"], "Eb2")]
    tune = [[("C5", 1), ("Bb4", 0.5), ("Ab4", 0.5), ("Eb4", 2), (None, 1), ("F4", 0.5), ("G4", 0.5), ("Ab4", 2)],
            [("C5", 1), ("Eb5", 1), ("C5", 0.5), ("Bb4", 0.5), ("Ab4", 1), ("F4", 2), (None, 2)],
            [("F4", 0.5), ("Ab4", 0.5), ("C5", 1), ("Db5", 1), ("C5", 0.5), ("Ab4", 0.5), ("F4", 2), (None, 2)],
            [("Eb4", 1), ("F4", 0.5), ("G4", 0.5), ("Ab4", 1), ("Bb4", 1), ("C5", 2), ("Bb4", 2)]]
    for rep in range(2):
        for i, (ch, bs) in enumerate(prog):
            b0 = rep * 32 + i * 8
            for bar in range(2):
                tr.chord(b0 + bar * 4, epiano, ch, 1.4, vel=0.75, send=0.35)
                tr.chord(b0 + bar * 4 + 1.5 + 0.1, epiano, ch, 1.2, vel=0.55, send=0.35)
                bassline(tr, b0 + bar * 4, bs, [(0, 0, 1.4), (2, 7, 0.9), (3, 12, 0.4), (3.5, 7, 0.45)], vel=0.75)
            tr.chord(b0, pad, [midi(n) + 12 for n in ch[:3]], 8, vel=0.4, spread=0.8, send=0.55, bright=1300.0)
            if b0 >= 16:
                melody(tr, b0, felt_piano, tune[i], vel=0.85, pan=0.15, send=0.45)
    drums(tr, 0, 1, kicks=(0,), snares=(), steps=8, hat_vel=0.2, kick_vel=0.5, swing=0.18)
    drums(tr, 1, 16, kicks=(0, 2.75), snares=(1, 3), steps=8, hat_vel=0.3, kick_vel=0.7, snare_vel=0.5, swing=0.18)
    return tr.render(out, loop=True, ir=reverb_ir(rt60=1.9), target_rms=0.115)


def final(out):
    tr = Track(bpm=88, bars=16, seed=6)
    prog = [("A2", ["A3", "E4", "A4", "C5"]), ("F2", ["F3", "C4", "F4", "A4"]),
            ("C2", ["C3", "G3", "C4", "E4"]), ("G2", ["G3", "D4", "G4", "B4"])]
    tune = [[("E5", 1.5), ("D5", 0.5), ("C5", 1), ("B4", 1), ("A4", 2), ("C5", 2)],
            [("A5", 1.5), ("G5", 0.5), ("F5", 1), ("E5", 1), ("C5", 4)],
            [("G5", 1.5), ("E5", 0.5), ("D5", 1), ("C5", 1), ("E5", 2), ("G5", 2)],
            [("B4", 1), ("C5", 1), ("D5", 2), ("G4", 2), ("B4", 2)]]
    for rep in range(2):
        for i, (bs, ar) in enumerate(prog):
            b0 = rep * 32 + i * 8
            for bar in range(2):
                for k in range(8):
                    tr.note(b0 + bar * 4 + k * 0.5, felt_piano, ar[[0, 1, 2, 3, 2, 1, 2, 3][k]], 0.5, vel=0.5, pan=-0.2, send=0.35)
                bassline(tr, b0 + bar * 4, bs, [(0, 0, 1.8), (2, 7, 1.4), (3.5, 12, 0.45)], vel=0.7)
            tr.chord(b0, pad, [midi(n) for n in ar[1:4]], 8, vel=0.45, spread=0.8, send=0.55, bright=1200.0)
            if b0 >= 16:
                melody(tr, b0, felt_piano, tune[i], vel=0.85, pan=0.2, send=0.45)
    drums(tr, 2, 16, kicks=(0, 2.5), snares=(1, 3), steps=8, hat_vel=0.24, kick_vel=0.6, snare_vel=0.45, back=clap)
    return tr.render(out, loop=True, ir=reverb_ir(rt60=2.3), target_rms=0.11)


def earthquake(out):
    """13 s one-shot. 0 s = cinematic start (fade in); quake begins at 2.0 s; the building falls at ~4.1 s."""
    tr = Track(bpm=60, bars=13, beats_per_bar=1, tail=2.5, seed=7)
    S = lambda s: s                                                     # 1 beat == 1 second here
    tr.add(0, drone(36.7, 13.0, vel=1.0), send=0.25)                    # D1 drone through the whole cue
    tr.add(0, drone(73.4, 13.0, vel=0.5), pan=0.2, send=0.3)
    hits = [2.0, 2.5, 3.0, 3.25, 3.5, 3.625, 3.75, 3.875, 3.9375, 4.0]
    for k, s in enumerate(hits):
        tr.add(S(s), tom(70 + 6 * k, vel=0.5 + 0.05 * k), pan=(-0.3 if k % 2 else 0.3), send=0.3)
    for k in range(16):
        s = 3.0 + k * (1.05 / 16)
        tr.add(S(s), snare(0.25 + 0.04 * k, rng=tr.rng), send=0.3)
    tr.add(S(3.4), rumble(4.5, vel=0.9), send=0.2)
    tr.add(S(4.1), boom(1.0), send=0.45)
    tr.add(S(4.1), hat(1.0, decay=3.0, rng=tr.rng), send=0.6)          # crash-like wash
    for name in ("D3", "F3", "A3", "Eb4"):
        tr.note(4.1, pad, name, 6.5, vel=0.5, send=0.6, attack=0.4, release=2.0, bright=1100.0)
    for k in range(int((10.5 - 4.6) / 0.25)):
        s = 4.6 + k * 0.25
        tr.note(s, bass, "D2" if (k // 8) % 2 == 0 else "Bb1", 0.2, vel=0.7, send=0.05)
        if k % 4 == 0:
            tr.add(S(s), tom(55, vel=0.6), send=0.3)
            tr.add(S(s), kick(0.7), send=0.03)
    return tr.render(out, loop=False, ir=reverb_ir(rt60=2.8, seconds=3.2), target_rms=0.13, fade_out=2.0)


def prison(out):
    """8 s one-shot. Door slams at ~3.0 s after the cinematic starts."""
    tr = Track(bpm=60, bars=8, beats_per_bar=1, tail=2.5, seed=8)
    for s in (0.6, 1.6, 2.6, 3.6, 4.7, 5.8, 6.9):                       # heartbeat, slowing after the slam
        tr.add(s, kick(0.55), send=0.08)
        tr.add(s + 0.24, kick(0.38), send=0.08)
    tr.add(0, drone(55.0, 8.0, vel=0.6), send=0.3)
    for s, nm in ((0.8, "A3"), (1.8, "E4"), (2.3, "C4")):
        tr.note(s, felt_piano, nm, 1.0, vel=0.6, send=0.5)
    tr.add(3.03, boom(0.75), send=0.4)
    tr.chord(3.03, felt_piano, ["A1", "E2", "A2", "C3"], 3.0, vel=1.1, send=0.5)
    tr.chord(3.03, pad, ["A3", "C4", "E4"], 4.5, vel=0.6, send=0.6, attack=0.6, release=2.0, bright=1000.0)
    for s, nm in ((4.3, "C5"), (4.8, "B4"), (5.3, "A4"), (6.3, "E4"), (6.8, "A4")):
        tr.note(s, felt_piano, nm, 0.9, vel=0.55, send=0.55)
    return tr.render(out, loop=False, ir=reverb_ir(rt60=2.6, seconds=3.0), target_rms=0.1, fade_out=1.2)


TRACKS = {"Menu": menu, "Ofis_Sakin": office_calm, "Ofis_Gergin": office_tense, "Aksam": evening,
          "Final": final, "Deprem": earthquake, "Hapishane": prison}

if __name__ == "__main__":
    out_dir = sys.argv[1]
    os.makedirs(out_dir, exist_ok=True)
    for name in (sys.argv[2:] or list(TRACKS)):
        secs = TRACKS[name](os.path.join(out_dir, name + ".wav"))
        print(name, round(secs, 1), "s")
