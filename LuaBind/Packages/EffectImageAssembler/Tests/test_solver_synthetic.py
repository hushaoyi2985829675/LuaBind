from pathlib import Path
import json
import subprocess
import sys

import cv2
import numpy as np


def write_png(path: Path, image: np.ndarray) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    assert cv2.imwrite(str(path), image)


def test_solver_synthetic(tmp_path: Path) -> None:
    sprites = tmp_path / "sprites"
    reference = np.zeros((128, 192, 4), dtype=np.uint8)
    reference[:, :, 3] = 255

    panel = np.zeros((80, 140, 4), dtype=np.uint8)
    panel[:, :, :] = (40, 90, 160, 255)
    panel[8:18, 12:128, :3] = (80, 140, 210)
    panel[50:68, 20:80, :3] = (20, 60, 120)
    panel[:, 0:4, :3] = (180, 210, 240)

    button = np.zeros((24, 60, 4), dtype=np.uint8)
    button[:, :, :] = (30, 220, 80, 255)
    button[6:18, 10:50, :3] = (10, 130, 50)
    button[:, 0:3, :3] = (180, 255, 190)

    write_png(sprites / "panel.png", panel)
    write_png(sprites / "button.png", button)

    reference[14:94, 10:150] = panel
    reference[98:122, 80:140] = button
    write_png(tmp_path / "reference.png", reference)

    output_json = tmp_path / "out" / "matches.json"
    report_dir = tmp_path / "out"
    solver = Path(__file__).parents[1] / "Tools" / "effect_image_assembler_solver.py"
    subprocess.check_call(
        [
            sys.executable,
            str(solver),
            "--reference",
            str(tmp_path / "reference.png"),
            "--sprites",
            str(sprites),
            "--output-json",
            str(output_json),
            "--report-dir",
            str(report_dir),
            "--threshold",
            "0.99",
        ]
    )

    data = json.loads(output_json.read_text(encoding="utf-8"))
    assert data["referenceSize"] == [192, 128]
    assert len(data["elements"]) >= 2
    rects = {
        (
            round(element["x"]),
            round(element["y"]),
            round(element["width"]),
            round(element["height"]),
        )
        for element in data["elements"]
    }
    assert (10, 14, 140, 80) in rects
    assert (80, 98, 60, 24) in rects
