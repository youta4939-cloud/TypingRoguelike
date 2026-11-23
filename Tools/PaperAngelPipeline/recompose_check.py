"""全パーツ重ねが source_x4 と一致するか確認"""
from __future__ import annotations

from pathlib import Path

import numpy as np
from PIL import Image

ROOT = Path(__file__).resolve().parent
RES = ROOT.parent.parent / "Assets/_Project/Resources/Battle/Art/Enemies/PaperAngel"
ORDER = [
    "01_shadow",
    "02_wing_L_inner",
    "03_wing_L_outer",
    "04_wing_R_inner",
    "05_wing_R_outer",
    "06_feet",
    "07_face_inner",
    "08_body",
    "09_seal",
]

SOURCE = ROOT / "source_x4.png"
OUT = ROOT / "recompose_check.png"


def over(base: np.ndarray, layer: np.ndarray) -> np.ndarray:
    a = layer[:, :, 3:4].astype(np.float32) / 255.0
    return base * (1.0 - a) + layer[:, :, :3].astype(np.float32) * a


def main() -> None:
    src = np.array(Image.open(SOURCE).convert("RGBA"), dtype=np.float32)
    comp = np.zeros(src.shape[:2] + (3,), dtype=np.float32)
    for name in ORDER:
        path = RES / f"{name}.png"
        layer = np.array(Image.open(path).convert("RGBA"), dtype=np.float32)
        if layer.shape[:2] != src.shape[:2]:
            raise SystemExit(f"{name} size {layer.shape[:2]} != source {src.shape[:2]}")
        comp = over(comp, layer)

    diff = np.abs(comp - src[:, :, :3]) * (src[:, :, 3:4] / 255.0)
    mae = diff.mean()
    Image.fromarray(np.clip(comp, 0, 255).astype(np.uint8), "RGB").save(OUT)
    print(f"saved {OUT}")
    print(f"mean abs rgb diff (source alpha weighted): {mae:.4f}")


if __name__ == "__main__":
    main()
