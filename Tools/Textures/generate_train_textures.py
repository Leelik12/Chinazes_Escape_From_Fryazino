# Текстуры электрички в духе ЭД4М и железной дороги (Tools/Blender/train): светло-серый кузов, красная окраска
# кабины и полос, тёмный металл тележек и подвагонного оборудования, щебень балласта, стёкла фонарей.
# Шум, сохранение (цвет + гладкость в альфе, карта нормалей) и рецепт крашеного металла — из generate_building_textures.py.
# Запуск из корня проекта: python Tools/Textures/generate_train_textures.py (нужны numpy и Pillow).
import importlib.util
import os
import numpy as np

_spec = importlib.util.spec_from_file_location(
    "bt", os.path.join(os.path.dirname(os.path.abspath(__file__)), "generate_building_textures.py"))
bt = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(bt)
bt.OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "Assets", "Content", "Train", "Textures")
bt.rng = np.random.default_rng(1997)
S, fbm, smoothstep, mix, rgb, save, grime = bt.S, bt.fbm, bt.smoothstep, bt.mix, bt.rgb, bt.save, bt.grime
yy = bt.yy


def body(name, paint, rust):
    # Крашеный кузов: гладкий металл, к низу пыль и потёки с крыши; ржавчины почти нет
    bt.painted_metal(name, paint, rust=rust)


def ballast():
    # Щебень балласта: серые камни с ржавым налётом от тормозных колодок; плитка 2 м
    stones = fbm(220, 0.6)
    tone = np.clip(0.5 + fbm(140, 0.8) * 0.45, 0, 1)
    base = mix(rgb(78, 76, 72), rgb(150, 146, 138), tone)
    rusty = smoothstep(0.4, 1.6, fbm(6, 2.2))
    color = mix(base, base * rgb(190, 140, 110) / 0.75 * 0.75, rusty * 0.55)
    gaps = smoothstep(-0.2, -1.2, stones)
    color = color * (1 - 0.55 * gaps)[..., None]
    save("TR_Ballast", color, stones * 0.5, 0.15, strength=5.0)


def lamp(name, color):
    # Стекло фонаря: ровный цвет, светится в Unity (эмиссия задаётся в материале)
    glow = 0.85 + 0.15 * smoothstep(0.0, 1.0, 1 - np.abs(yy - 0.5) * 2)
    save(name, np.array(color)[None, None, :] * glow[..., None], np.zeros((S, S)), 0.9, strength=0.5)


if __name__ == "__main__":
    body("TR_BodyGrey", rgb(190, 192, 191), 2.2)
    body("TR_Red", rgb(172, 30, 36), 2.0)
    body("TR_DarkMetal", rgb(48, 49, 51), 1.2)
    ballast()
    lamp("TR_LampWhite", rgb(255, 246, 222))
    lamp("TR_LampRed", rgb(235, 40, 28))
