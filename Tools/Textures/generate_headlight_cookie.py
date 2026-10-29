# Маска (cookie) фары машины игроков: широкое горизонтальное пятно (вдвое шире, чем выше) с ярким центром,
# мягким спадом к краям (круг конуса не виден) и лёгкими полосами от рассеивателя.
# Маска симметрична по вертикали: фара наклонена вниз (BuildHeadlights.cs), центр пятна ложится на дорогу в ~20 м,
# нижняя половина — ближе, верхняя добивает до машин впереди.
# Запуск из корня проекта: python Tools/Textures/generate_headlight_cookie.py (нужны numpy и Pillow).
import os
import numpy as np
from PIL import Image

N = 256
OUT = os.path.join(os.path.dirname(__file__), "..", "..", "Assets", "Content", "Car", "HeadlightCookie.png")

y, x = np.mgrid[0:N, 0:N].astype(float)
u = (x + 0.5) / N * 2 - 1
v = 1 - (y + 0.5) / N * 2

spot = np.exp(-((u / 0.5) ** 2 + (v / 0.26) ** 2))
fill = np.exp(-((u / 0.9) ** 2 + (v / 0.45) ** 2)) * 0.4
r = np.sqrt(u ** 2 + v ** 2)
edge = np.clip((1 - r) / 0.3, 0, 1) ** 2
rng = np.random.default_rng(7)
stripes = 1 + 0.025 * np.sin(u * 37 + rng.uniform(0, 6)) + 0.015 * np.sin(u * 83 + rng.uniform(0, 6))

# Свет гаснет с квадратом расстояния, поэтому верх пятна (дальняя дорога) ярче низа (асфальт у бампера),
# а выше светотени (v > 0.2) свет быстро гаснет
far = np.exp(np.clip(v, -1, 0.2) * 2.6)
cut = 1 / (1 + np.exp((v - 0.2) / 0.04))
img = np.clip((spot + fill) * far * cut * edge * stripes, 0, None)
img = np.clip(img / img.max(), 0, 1) ** 0.85
os.makedirs(os.path.dirname(OUT), exist_ok=True)
Image.fromarray((img * 255).astype(np.uint8), "L").convert("RGB").save(OUT)
print("saved", OUT)
