import sys, wave, numpy as np
for p in sys.argv[1:]:
    with wave.open(p) as w:
        x = np.frombuffer(w.readframes(w.getnframes()), "<i2").reshape(-1, 2).astype(float) / 32768
    peak = np.abs(x).max(); rms = np.sqrt((x ** 2).mean())
    clip = (np.abs(x) > 0.995).mean() * 100
    seam = np.abs(x[0] - x[-1]).max()
    typical = np.abs(np.diff(x, axis=0)).mean()
    spec = np.abs(np.fft.rfft(x[:, 0])); f = np.fft.rfftfreq(len(x), 1 / 44100)
    centroid = (spec * f).sum() / spec.sum()
    low = spec[f < 150].sum() / spec.sum() * 100
    segs = np.array_split(np.sqrt((x ** 2).mean(axis=1)), 16)
    dyn = " ".join(f"{20*np.log10(np.sqrt(s.mean())+1e-9):.0f}" for s in segs)
    print(f"{p.split('/')[-1]}: peak {20*np.log10(peak):.1f} dBFS, rms {20*np.log10(rms):.1f} dBFS, clipped {clip:.3f}%, "
          f"seam jump {seam:.4f} (typical step {typical:.4f}), centroid {centroid:.0f} Hz, <150Hz {low:.0f}%\n  loudness per 1/16: {dyn}")
