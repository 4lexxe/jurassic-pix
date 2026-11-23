"""Convierte los sonidos CC0 de Tools/sonidos_cc0 a los .wav de Assets/JurassicPix/Audio.

Uso: python Tools/import_sounds.py   (requiere numpy, scipy y soundfile)
Unity no normaliza al importar, así que cada clip sale ya con el nivel con el que tiene que sonar.
"""
import os
import numpy as np
import soundfile as sf
from scipy.signal import resample_poly

HERE = os.path.dirname(os.path.abspath(__file__))
SOURCE = os.path.join(HERE, "sonidos_cc0")
TARGET = os.path.join(HERE, "..", "Assets", "JurassicPix", "Audio")


def load(name, mono=True, start=0.0, end=None, rate=32000):
    data, source_rate = sf.read(os.path.join(SOURCE, name), always_2d=True)
    data = data[int(start * source_rate):int(end * source_rate) if end else None]
    if mono:
        data = data.mean(axis=1, keepdims=True)
    if source_rate != rate:
        data = resample_poly(data, rate, source_rate, axis=0)
    return data, rate


def fade(data, rate, head, tail):
    head, tail = int(head * rate), int(tail * rate)
    if head:
        data[:head] *= np.linspace(0, 1, head)[:, None]
    if tail:
        data[-tail:] *= np.linspace(1, 0, tail)[:, None] ** 2
    return data


def trim(data, rate, floor=.02, lead=.004):
    loud = np.abs(data).max(axis=1)
    hits = np.nonzero(loud > floor * loud.max())[0]
    return data[max(0, hits[0] - int(lead * rate)):hits[-1] + int(.03 * rate)]


def save(name, data, rate, peak=None, rms=None):
    if rms is not None:
        data = data * (10 ** (rms / 20) / np.sqrt((data ** 2).mean()))
    top = np.abs(data).max()
    limit = 10 ** ((peak if peak is not None else -1) / 20)
    if peak is not None and rms is None or top > limit:
        data = data * (limit / top)
    sf.write(os.path.join(TARGET, name + ".wav"), data, rate, subtype="PCM_16")
    level = 20 * np.log10(np.sqrt((data ** 2).mean()))
    print(f"{name}: {len(data) / rate:.2f} s, pico {20 * np.log10(np.abs(data).max()):.1f} dB, RMS {level:.1f} dB")


# Aguacero: bucle estéreo que se suma a la lluvia sobre hojas cuando la tormenta arrecia.
rain, rate = load("lluvia_ylmir_2.ogg", mono=False)
blend = int(1.5 * rate)
tail = rain[-blend:] * np.linspace(1, 0, blend)[:, None] + rain[:blend] * np.linspace(0, 1, blend)[:, None]
save("Ambiente_Aguacero", np.concatenate([tail, rain[blend:-blend]]), rate, rms=-20)

# Trueno largo: el trueno que retumba 23 s en la grabación de lluvia.
roll, rate = load("rain-thunder_wuxiascrub.ogg", start=20.9, end=44.3)
save("Trueno_Largo", fade(roll, rate, .03, 5), rate, peak=-1)

# Trueno lejano: retumbo grave y corto.
far, rate = load("sfx100v2_thunder_01.ogg")
save("Trueno_Lejano", fade(far, rate, .01, 1.2), rate, peak=-1.5)

# Pisadas en agua y bocados.
for index in range(3):
    step, rate = load(f"sfx100v2_footstep_wet_0{index + 1}.ogg")
    save(f"Pisada_Agua_{index + 1}", fade(trim(step, rate), rate, .002, .05), rate, peak=-3)
for index, source in enumerate((2, 4, 5, 7)):
    bite, rate = load(f"crunch.{source}.ogg")
    save(f"Bocado_{index + 1}", fade(trim(np.clip(bite, -1, 1), rate), rate, .002, .06), rate, peak=-3)


def loud(name, rms=-12.5, peak=-.5):
    """Sube el volumen percibido de un .wav ya importado con un limitador suave, hasta el RMS pedido."""
    path = os.path.join(TARGET, name + ".wav")
    data, rate = sf.read(path, always_2d=True)
    data = data / np.abs(data).max()
    goal, low, high = 10 ** (rms / 20), 1.0, 60.0
    for _ in range(40):
        drive = (low + high) / 2
        shaped = np.tanh(data * drive) / np.tanh(drive) * 10 ** (peak / 20)
        low, high = (drive, high) if np.sqrt((shaped ** 2).mean()) < goal else (low, drive)
    sf.write(path, shaped, rate, subtype="PCM_16")
    print(f"{name}: RMS {20 * np.log10(np.sqrt((shaped ** 2).mean())):.1f} dB")


# Los truenos tienen que imponerse sobre la lluvia: el limitador les sube el cuerpo sin saturar los picos.
# Trueno_Tormenta y Trueno_Valle no salen de sonidos_cc0; volver a pasarlos por acá los deja en el mismo nivel.
for thunder in ("Trueno_Tormenta", "Trueno_Largo", "Trueno_Valle", "Trueno_Lejano"):
    loud(thunder)
