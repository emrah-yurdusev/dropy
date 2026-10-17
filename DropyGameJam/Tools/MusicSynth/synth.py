"""Small offline synthesizer for the game's music (numpy only)."""
import math
import wave

import numpy as np

SR = 44100
NOTE = {"C": 0, "C#": 1, "Db": 1, "D": 2, "D#": 3, "Eb": 3, "E": 4, "F": 5, "F#": 6, "Gb": 6,
        "G": 7, "G#": 8, "Ab": 8, "A": 9, "A#": 10, "Bb": 10, "B": 11}


def midi(name):
    """'A3' -> 57, 'Bb2' -> 46."""
    i = 2 if len(name) > 2 and name[1] in "#b" else 1
    return 12 * (int(name[i:]) + 1) + NOTE[name[:i]]


def hz(n):
    return 440.0 * 2 ** ((n - 69) / 12)


def tarr(dur):
    return np.arange(int(dur * SR)) / SR


def fade(sig, a=0.004, r=0.08):
    n = len(sig)
    na, nr = min(n, int(a * SR)), min(n, int(r * SR))
    env = np.ones(n)
    if na:
        env[:na] = np.linspace(0, 1, na)
    if nr:
        env[-nr:] *= np.linspace(1, 0, nr)
    return sig * env


def lowpass(x, fc, order=2):
    spec = np.fft.rfft(x)
    f = np.fft.rfftfreq(len(x), 1 / SR)
    spec *= 1 / np.sqrt(1 + (f / fc) ** (2 * order))
    return np.fft.irfft(spec, len(x))


def highpass(x, fc, order=2):
    spec = np.fft.rfft(x)
    f = np.fft.rfftfreq(len(x), 1 / SR)
    spec *= 1 - 1 / np.sqrt(1 + (f / max(fc, 1)) ** (2 * order))
    return np.fft.irfft(spec, len(x))


# ------------------------------------------------------------------ instruments

def epiano(f, dur, vel=1.0, rng=None):
    t = tarr(dur + 1.2)
    index = 1.6 * np.exp(-t * 2.8) + 0.25
    s = np.sin(2 * np.pi * f * t + index * np.sin(2 * np.pi * f * t))
    s += 0.12 * np.sin(2 * np.pi * f * 14.0 * t) * np.exp(-t * 14)        # tine
    env = np.exp(-t * (0.9 + f / 900))
    rel = np.clip((dur + 1.2 - t) / 1.2, 0, 1)
    return fade(s * env * rel * vel * 0.5, 0.003, 0.05)


def felt_piano(f, dur, vel=1.0, rng=None):
    t = tarr(dur + 2.5)
    s = np.zeros_like(t)
    B = 0.0003
    for k in range(1, 9):
        fk = f * k * math.sqrt(1 + B * k * k)
        if fk > 9000:
            break
        s += (1 / k ** 1.4) * np.sin(2 * np.pi * fk * t + 0.3 * k) * np.exp(-t * (0.5 + 0.45 * k + f / 1500))
    rng = rng or np.random.default_rng(int(f))
    n = lowpass(rng.standard_normal(len(t)) * np.exp(-t * 90), 1800)
    s += 0.05 * n
    rel = np.clip((dur + 2.5 - t) / 0.9, 0, 1)
    return fade(s * rel * vel * 0.42, 0.006, 0.08)


def pad(f, dur, vel=1.0, attack=1.2, release=1.8, bright=1700.0, rng=None):
    rng = rng or np.random.default_rng(int(f * 10))
    t = tarr(dur + release)
    s = np.zeros_like(t)
    for cents in (-8.0, 0.0, 7.0):
        fd = f * 2 ** (cents / 1200)
        for k in range(1, 40):
            fk = fd * k
            if fk > 5000:
                break
            amp = (1 / k) * math.exp(-fk / bright)
            s += amp * np.sin(2 * np.pi * fk * t + rng.uniform(0, 2 * np.pi))
    env = np.minimum(1, t / attack)
    env *= np.clip((dur + release - t) / release, 0, 1)
    env *= 1 + 0.08 * np.sin(2 * np.pi * 0.23 * t + rng.uniform(0, 6))
    return s * env * vel * 0.12


def marimba(f, dur, vel=1.0, rng=None):
    t = tarr(min(dur, 1.2) + 0.6)
    s = (np.sin(2 * np.pi * f * t) * np.exp(-t * 3.2)
         + 0.25 * np.sin(2 * np.pi * f * 3.93 * t) * np.exp(-t * 9)
         + 0.07 * np.sin(2 * np.pi * f * 9.2 * t) * np.exp(-t * 16))
    return fade(s * vel * 0.45, 0.002, 0.08)


def pluck(f, dur, vel=1.0, rng=None):
    t = tarr(dur + 1.5)
    s = np.zeros_like(t)
    for k in range(1, 12):
        fk = f * k
        if fk > 8000:
            break
        s += (1 / k ** 1.6) * np.sin(2 * np.pi * fk * t) * np.exp(-t * (1.4 + 0.9 * k))
    return fade(s * vel * 0.4, 0.002, 0.1)


def bass(f, dur, vel=1.0, rng=None):
    t = tarr(dur + 0.15)
    s = np.sin(2 * np.pi * f * t) + 0.3 * np.sin(4 * np.pi * f * t) + 0.08 * np.sin(6 * np.pi * f * t)
    env = np.minimum(1, t / 0.012) * (0.75 + 0.25 * np.exp(-t * 5))
    env *= np.clip((dur + 0.15 - t) / 0.15, 0, 1)
    return np.tanh(1.3 * s * env) * vel * 0.38


def kick(vel=1.0):
    t = tarr(0.45)
    freq = 45 + 80 * np.exp(-t * 28)
    ph = 2 * np.pi * np.cumsum(freq) / SR
    return np.sin(ph) * np.exp(-t * 8) * vel * 0.8


def hat(vel=1.0, decay=45.0, rng=None):
    rng = rng or np.random.default_rng(7)
    t = tarr(0.12)
    n = highpass(rng.standard_normal(len(t)), 6500)
    return n * np.exp(-t * decay) * vel * 0.12


def snare(vel=1.0, rng=None):
    rng = rng or np.random.default_rng(9)
    t = tarr(0.25)
    body = np.sin(2 * np.pi * (185 + 40 * np.exp(-t * 40)) * t) * np.exp(-t * 22)
    noise = highpass(lowpass(rng.standard_normal(len(t)), 7000), 1200) * np.exp(-t * 18)
    return (0.6 * body + 0.55 * noise) * vel * 0.5


def clap(vel=1.0, rng=None):
    rng = rng or np.random.default_rng(13)
    t = tarr(0.3)
    env = np.zeros_like(t)
    for d in (0.0, 0.011, 0.022):
        env += np.where(t >= d, np.exp(-(t - d) * 60), 0)
    env += 0.5 * np.exp(-t * 12)
    n = highpass(lowpass(rng.standard_normal(len(t)), 5000), 900)
    return n * env * vel * 0.28


def tom(f, vel=1.0):
    t = tarr(0.6)
    ph = 2 * np.pi * np.cumsum(f * (1 + 0.6 * np.exp(-t * 25))) / SR
    return np.sin(ph) * np.exp(-t * 6) * vel * 0.7


def tick(vel=1.0, rng=None):
    t = tarr(0.05)
    s = np.sin(2 * np.pi * 2300 * t) * np.exp(-t * 160) + 0.4 * np.sin(2 * np.pi * 4100 * t) * np.exp(-t * 220)
    return s * vel * 0.18


def drone(f, dur, vel=1.0, rng=None):
    rng = rng or np.random.default_rng(int(f))
    t = tarr(dur)
    s = np.zeros_like(t)
    for ratio, amp in ((1.0, 1.0), (1.0035, 0.8), (2.0, 0.35), (2.996, 0.15), (0.5, 0.5)):
        s += amp * np.sin(2 * np.pi * f * ratio * t + rng.uniform(0, 6))
    s += 0.25 * lowpass(rng.standard_normal(len(t)), f * 2.5)
    return s * vel * 0.2


def rumble(dur, vel=1.0, rng=None):
    rng = rng or np.random.default_rng(3)
    t = tarr(dur)
    n = lowpass(rng.standard_normal(len(t)), 90, order=3)
    n /= np.max(np.abs(n)) + 1e-9
    return n * vel * 0.9


def boom(vel=1.0, rng=None):
    rng = rng or np.random.default_rng(5)
    t = tarr(3.0)
    freq = 32 + 50 * np.exp(-t * 12)
    s = np.sin(2 * np.pi * np.cumsum(freq) / SR) * np.exp(-t * 1.4)
    s += 0.5 * lowpass(rng.standard_normal(len(t)), 300) * np.exp(-t * 6)
    return s * vel * 0.9


# ------------------------------------------------------------------ arrangement

def reverb_ir(seconds=2.6, rt60=2.2, seed=11, darkness=5000):
    rng = np.random.default_rng(seed)
    t = tarr(seconds)
    ir = np.zeros((2, len(t)))
    for c in range(2):
        n = rng.standard_normal(len(t)) * np.exp(-6.9 * t / rt60)
        ir[c] = lowpass(n, darkness)
    pre = int(0.018 * SR)
    ir = np.concatenate([np.zeros((2, pre)), ir], axis=1)
    return ir / np.sqrt(np.sum(ir ** 2, axis=1, keepdims=True)) * 0.6


def convolve(x, ir):
    n = x.shape[1] + ir.shape[1]
    size = 1 << (n - 1).bit_length()
    out = np.zeros((2, n))
    for c in range(2):
        out[c] = np.fft.irfft(np.fft.rfft(x[c], size) * np.fft.rfft(ir[c], size), size)[:n]
    return out


class Track:
    def __init__(self, bpm, bars, beats_per_bar=4, tail=4.0, seed=1):
        self.spb = 60.0 / bpm
        self.beats = bars * beats_per_bar
        self.length = self.beats * self.spb
        self.tail = tail
        n = int((self.length + tail) * SR)
        self.dry = np.zeros((2, n))
        self.send = np.zeros((2, n))
        self.rng = np.random.default_rng(seed)

    def beat_time(self, beat):
        return beat * self.spb

    def add(self, beat, sig, pan=0.0, gain=1.0, send=0.25, seconds=None):
        start = int((seconds if seconds is not None else beat * self.spb) * SR)
        if start >= self.dry.shape[1]:
            return
        sig = sig[: self.dry.shape[1] - start] * gain
        l = math.cos((pan + 1) * math.pi / 4)
        r = math.sin((pan + 1) * math.pi / 4)
        end = start + len(sig)
        self.dry[0, start:end] += sig * l
        self.dry[1, start:end] += sig * r
        self.send[0, start:end] += sig * l * send
        self.send[1, start:end] += sig * r * send

    def note(self, beat, inst, name, beats, vel=1.0, pan=0.0, send=0.25, **kw):
        f = hz(midi(name)) if isinstance(name, str) else hz(name)
        humanize = self.rng.uniform(-0.008, 0.008)
        sig = inst(f, beats * self.spb, vel=vel * self.rng.uniform(0.9, 1.05), **kw)
        self.add(beat, sig, pan=pan, send=send, seconds=max(0.0, beat * self.spb + humanize))

    def chord(self, beat, inst, names, beats, vel=1.0, spread=0.5, send=0.35, **kw):
        for i, nm in enumerate(names):
            pan = (i / max(1, len(names) - 1) - 0.5) * 2 * spread
            self.note(beat, inst, nm, beats, vel=vel / math.sqrt(len(names)), pan=pan, send=send, **kw)

    def render(self, path, loop=True, ir=None, wet=1.0, target_rms=0.11, fade_out=0.0):
        ir = ir if ir is not None else reverb_ir()
        mix = self.dry + wet * convolve(self.send, ir)[:, : self.dry.shape[1]]
        n = int(self.length * SR)
        if loop:
            out = mix[:, :n].copy()
            tail = mix[:, n:]
            k = min(tail.shape[1], n)
            out[:, :k] += tail[:, :k]
        else:
            out = mix
            if fade_out > 0:
                m = int(fade_out * SR)
                out[:, -m:] *= np.linspace(1, 0, m)
        rms = np.sqrt(np.mean(out ** 2)) + 1e-9
        out *= target_rms / rms
        out = np.tanh(out * 1.1) / np.tanh(1.1)
        peak = np.max(np.abs(out))
        if peak > 0.97:
            out *= 0.97 / peak
        write_wav(path, out)
        return len(out[0]) / SR


def write_wav(path, stereo):
    data = (np.clip(stereo.T, -1, 1) * 32767).astype("<i2")
    with wave.open(path, "wb") as w:
        w.setnchannels(2)
        w.setsampwidth(2)
        w.setframerate(SR)
        w.writeframes(data.tobytes())
