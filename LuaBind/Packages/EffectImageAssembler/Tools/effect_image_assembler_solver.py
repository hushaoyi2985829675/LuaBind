#!/usr/bin/env python3
"""Reconstruct static UI element placement from a reference PNG and cut PNGs."""

from __future__ import annotations

import argparse
import json
import math
import os
from dataclasses import dataclass
from pathlib import Path
from typing import Iterable, List, Sequence, Tuple

import cv2
import numpy as np


@dataclass
class AssetImage:
    path: Path
    asset_path: str
    sprite_name: str
    image: np.ndarray
    trim: Tuple[int, int, int, int]
    trimmed: np.ndarray


@dataclass
class Match:
    name: str
    asset: Path
    asset_path: str
    sprite_name: str
    x: float
    y: float
    width: float
    height: float
    scale: float
    confidence: float
    order: int = 0
    source_image: np.ndarray | None = None

    def rect(self) -> Tuple[int, int, int, int]:
        return (
            int(round(self.x)),
            int(round(self.y)),
            int(round(self.width)),
            int(round(self.height)),
        )


def read_bgra(path: Path) -> np.ndarray:
    data = np.fromfile(str(path), dtype=np.uint8)
    image = cv2.imdecode(data, cv2.IMREAD_UNCHANGED)
    if image is None:
        raise RuntimeError(f"Could not read image: {path}")
    if image.ndim == 2:
        image = cv2.cvtColor(image, cv2.COLOR_GRAY2BGRA)
    elif image.shape[2] == 3:
        alpha = np.full(image.shape[:2] + (1,), 255, dtype=np.uint8)
        image = np.concatenate([image, alpha], axis=2)
    return image


def write_image(path: Path, image: np.ndarray) -> None:
    extension = path.suffix or ".png"
    ok, encoded = cv2.imencode(extension, image)
    if not ok:
        raise RuntimeError(f"Could not encode image: {path}")
    encoded.tofile(str(path))


def trim_alpha(image: np.ndarray, alpha_threshold: int) -> Tuple[int, int, int, int]:
    alpha = image[:, :, 3]
    ys, xs = np.where(alpha > alpha_threshold)
    if len(xs) == 0 or len(ys) == 0:
        return 0, 0, image.shape[1], image.shape[0]
    left = int(xs.min())
    top = int(ys.min())
    right = int(xs.max()) + 1
    bottom = int(ys.max()) + 1
    return left, top, right, bottom


def iter_pngs(folder: Path) -> Iterable[Path]:
    for path in sorted(folder.rglob("*.png")):
        if path.is_file():
            yield path


def load_assets(folder: Path, alpha_threshold: int) -> List[AssetImage]:
    assets: List[AssetImage] = []
    for path in iter_pngs(folder):
        image = read_bgra(path)
        left, top, right, bottom = trim_alpha(image, alpha_threshold)
        trimmed = image[top:bottom, left:right].copy()
        if trimmed.size == 0:
            continue
        assets.append(
            AssetImage(
                path=path,
                asset_path=str(path),
                sprite_name=path.stem,
                image=image,
                trim=(left, top, right, bottom),
                trimmed=trimmed,
            )
        )
    return assets


def load_assets_from_index(index_path: Path, alpha_threshold: int) -> List[AssetImage]:
    data = json.loads(index_path.read_text(encoding="utf-8"))
    index_root = index_path.resolve().parent
    project_root = discover_project_root(index_root)
    assets: List[AssetImage] = []
    seen: set[tuple[str, str]] = set()

    for entry in data.get("sprites", []):
        asset_path = entry.get("assetPath") or entry.get("texturePath")
        texture_path = entry.get("texturePath") or asset_path
        sprite_name = entry.get("name") or Path(asset_path).stem
        if not texture_path or not asset_path:
            continue
        key = (asset_path, sprite_name)
        if key in seen:
            continue
        seen.add(key)

        source_path = resolve_index_path(texture_path, project_root)
        if source_path is None or not source_path.is_file():
            continue

        source = read_bgra(source_path)
        rect = entry.get("rect") or [0, 0, source.shape[1], source.shape[0]]
        cropped = crop_unity_rect(source, rect, entry.get("textureHeight") or source.shape[0])
        if cropped.size == 0:
            continue

        left, top, right, bottom = trim_alpha(cropped, alpha_threshold)
        trimmed = cropped[top:bottom, left:right].copy()
        if trimmed.size == 0:
            continue

        assets.append(
            AssetImage(
                path=source_path,
                asset_path=asset_path,
                sprite_name=sprite_name,
                image=cropped,
                trim=(left, top, right, bottom),
                trimmed=trimmed,
            )
        )

    return assets


def discover_project_root(start: Path) -> Path:
    for path in [start, *start.parents]:
        if (path / "Assets").is_dir():
            return path
    return start


def resolve_index_path(path_value: str, project_root: Path) -> Path | None:
    normalized = path_value.replace("\\", "/")
    if normalized.startswith("Assets/") or normalized.startswith("Packages/"):
        return (project_root / normalized).resolve()
    path = Path(path_value)
    if path.is_absolute():
        return path
    return (project_root / path).resolve()


def crop_unity_rect(source: np.ndarray, rect: Sequence[float], texture_height: int) -> np.ndarray:
    if len(rect) < 4:
        return source
    x = int(round(rect[0]))
    unity_y = int(round(rect[1]))
    width = int(round(rect[2]))
    height = int(round(rect[3]))
    top = int(round(texture_height - unity_y - height))
    left = x
    right = min(source.shape[1], left + width)
    bottom = min(source.shape[0], top + height)
    left = max(0, left)
    top = max(0, top)
    if right <= left or bottom <= top:
        return source[0:0, 0:0].copy()
    return source[top:bottom, left:right].copy()


def scale_values(min_scale: float, max_scale: float, scale_step: float) -> List[float]:
    if scale_step <= 0:
        raise ValueError("--scale-step must be greater than zero")
    values = []
    value = min_scale
    while value <= max_scale + 1e-6:
        values.append(round(value, 4))
        value += scale_step
    if 1.0 not in values and min_scale <= 1.0 <= max_scale:
        values.append(1.0)
    return sorted(set(values), key=lambda v: (abs(v - 1.0), v))


def resize_bgra(image: np.ndarray, scale: float) -> np.ndarray:
    if abs(scale - 1.0) < 1e-6:
        return image
    height, width = image.shape[:2]
    new_width = max(1, int(round(width * scale)))
    new_height = max(1, int(round(height * scale)))
    interpolation = cv2.INTER_AREA if scale < 1.0 else cv2.INTER_LINEAR
    return cv2.resize(image, (new_width, new_height), interpolation=interpolation)


def find_local_maxima(
    result: np.ndarray,
    threshold: float,
    max_occurrences: int,
    suppression_size: Tuple[int, int],
) -> List[Tuple[int, int, float]]:
    work = result.copy()
    work[~np.isfinite(work)] = -1.0
    matches: List[Tuple[int, int, float]] = []
    suppress_w = max(1, suppression_size[0] // 2)
    suppress_h = max(1, suppression_size[1] // 2)

    for _ in range(max_occurrences):
        _, max_value, _, max_location = cv2.minMaxLoc(work)
        if max_value < threshold:
            break
        x, y = max_location
        matches.append((x, y, float(max_value)))
        x0 = max(0, x - suppress_w)
        y0 = max(0, y - suppress_h)
        x1 = min(work.shape[1], x + suppress_w + 1)
        y1 = min(work.shape[0], y + suppress_h + 1)
        work[y0:y1, x0:x1] = -1.0

    return matches


def match_asset(
    reference: np.ndarray,
    asset: AssetImage,
    scales: Sequence[float],
    threshold: float,
    max_occurrences: int,
    alpha_threshold: int,
) -> List[Match]:
    reference_bgr = reference[:, :, :3]
    original_h, original_w = asset.image.shape[:2]
    trim_left, trim_top, _, _ = asset.trim
    candidates: List[Match] = []

    for scale in scales:
        template = resize_bgra(asset.trimmed, scale)
        template_h, template_w = template.shape[:2]
        if template_w > reference.shape[1] or template_h > reference.shape[0]:
            continue
        if template_w < 2 or template_h < 2:
            continue

        mask = (template[:, :, 3] > alpha_threshold).astype(np.uint8) * 255
        if cv2.countNonZero(mask) < 4:
            continue

        result = cv2.matchTemplate(reference_bgr, template[:, :, :3], cv2.TM_CCORR_NORMED, mask=mask)
        for match_x, match_y, score in find_local_maxima(result, threshold, max_occurrences, (template_w, template_h)):
            full_x = match_x - trim_left * scale
            full_y = match_y - trim_top * scale
            candidates.append(
                Match(
                    name="",
                    asset=asset.path,
                    asset_path=asset.asset_path,
                    sprite_name=asset.sprite_name,
                    x=float(full_x),
                    y=float(full_y),
                    width=float(original_w * scale),
                    height=float(original_h * scale),
                    scale=float(scale),
                    confidence=float(score),
                    source_image=asset.image,
                )
            )

    return nms_matches(candidates, iou_threshold=0.92, max_count=max_occurrences)


def nms_matches(matches: Sequence[Match], iou_threshold: float, max_count: int) -> List[Match]:
    kept: List[Match] = []
    for candidate in sorted(matches, key=lambda item: item.confidence, reverse=True):
        if all(iou(candidate.rect(), existing.rect()) < iou_threshold for existing in kept):
            kept.append(candidate)
        if len(kept) >= max_count:
            break
    return kept


def iou(a: Tuple[int, int, int, int], b: Tuple[int, int, int, int]) -> float:
    ax, ay, aw, ah = a
    bx, by, bw, bh = b
    ax2 = ax + aw
    ay2 = ay + ah
    bx2 = bx + bw
    by2 = by + bh
    ix1 = max(ax, bx)
    iy1 = max(ay, by)
    ix2 = min(ax2, bx2)
    iy2 = min(ay2, by2)
    if ix2 <= ix1 or iy2 <= iy1:
        return 0.0
    intersection = (ix2 - ix1) * (iy2 - iy1)
    union = aw * ah + bw * bh - intersection
    return intersection / union if union > 0 else 0.0


def assign_names(matches: Sequence[Match]) -> None:
    counts: dict[str, int] = {}
    for match in sorted(matches, key=lambda item: (item.asset_path, item.sprite_name, item.y, item.x)):
        stem = sanitize_name(match.sprite_name or match.asset.stem)
        counts[stem] = counts.get(stem, 0) + 1
        match.name = f"{stem}_{counts[stem]:03d}"


def sanitize_name(value: str) -> str:
    clean = []
    for char in value:
        clean.append(char if char.isalnum() or char == "_" else "_")
    name = "".join(clean).strip("_")
    return name or "element"


def initial_order(matches: Sequence[Match]) -> List[Match]:
    ordered = sorted(
        matches,
        key=lambda item: (
            -(item.width * item.height),
            item.y,
            item.x,
            -item.confidence,
        ),
    )
    for index, match in enumerate(ordered):
        match.order = index
    return ordered


def render_composite(reference_shape: Tuple[int, int, int], matches: Sequence[Match]) -> np.ndarray:
    canvas = np.zeros(reference_shape, dtype=np.uint8)
    for match in sorted(matches, key=lambda item: item.order):
        source = match.source_image.copy() if match.source_image is not None else read_bgra(match.asset)
        source = resize_bgra(source, match.scale)
        draw_image(canvas, source, int(round(match.x)), int(round(match.y)))
    return canvas


def draw_image(canvas: np.ndarray, source: np.ndarray, x: int, y: int) -> None:
    canvas_h, canvas_w = canvas.shape[:2]
    source_h, source_w = source.shape[:2]
    dst_x0 = max(0, x)
    dst_y0 = max(0, y)
    dst_x1 = min(canvas_w, x + source_w)
    dst_y1 = min(canvas_h, y + source_h)
    if dst_x1 <= dst_x0 or dst_y1 <= dst_y0:
        return

    src_x0 = dst_x0 - x
    src_y0 = dst_y0 - y
    src_x1 = src_x0 + (dst_x1 - dst_x0)
    src_y1 = src_y0 + (dst_y1 - dst_y0)

    dst = canvas[dst_y0:dst_y1, dst_x0:dst_x1].astype(np.float32)
    src = source[src_y0:src_y1, src_x0:src_x1].astype(np.float32)
    alpha = src[:, :, 3:4] / 255.0
    out_rgb = src[:, :, :3] * alpha + dst[:, :, :3] * (1.0 - alpha)
    out_alpha = src[:, :, 3:4] + dst[:, :, 3:4] * (1.0 - alpha)
    canvas[dst_y0:dst_y1, dst_x0:dst_x1, :3] = np.clip(out_rgb, 0, 255).astype(np.uint8)
    canvas[dst_y0:dst_y1, dst_x0:dst_x1, 3:4] = np.clip(out_alpha, 0, 255).astype(np.uint8)


def optimize_order(reference: np.ndarray, matches: List[Match], iterations: int) -> None:
    if len(matches) < 2 or iterations <= 0:
        return

    matches.sort(key=lambda item: item.order)
    current_error = composite_error(reference, matches)
    for _ in range(iterations):
        improved = False
        for left in range(len(matches) - 1):
            for right in range(left + 1, len(matches)):
                if iou(matches[left].rect(), matches[right].rect()) <= 0.0:
                    continue
                matches[left].order, matches[right].order = matches[right].order, matches[left].order
                new_error = composite_error(reference, matches)
                if new_error + 1e-6 < current_error:
                    current_error = new_error
                    improved = True
                else:
                    matches[left].order, matches[right].order = matches[right].order, matches[left].order
        if not improved:
            break

    for index, match in enumerate(sorted(matches, key=lambda item: item.order)):
        match.order = index


def composite_error(reference: np.ndarray, matches: Sequence[Match]) -> float:
    composite = render_composite(reference.shape, matches)
    diff = np.abs(reference.astype(np.int16)[:, :, :3] - composite.astype(np.int16)[:, :, :3])
    return float(np.mean(diff))


def diff_image(reference: np.ndarray, composite: np.ndarray) -> np.ndarray:
    diff = np.abs(reference.astype(np.int16)[:, :, :3] - composite.astype(np.int16)[:, :, :3])
    gray = np.mean(diff, axis=2).astype(np.uint8)
    heat = cv2.applyColorMap(gray, cv2.COLORMAP_JET)
    return heat


def unmatched_regions(reference: np.ndarray, composite: np.ndarray, threshold: int, min_area: int) -> List[dict]:
    diff = np.abs(reference.astype(np.int16)[:, :, :3] - composite.astype(np.int16)[:, :, :3])
    gray = np.mean(diff, axis=2).astype(np.uint8)
    mask = (gray > threshold).astype(np.uint8) * 255
    kernel = np.ones((3, 3), dtype=np.uint8)
    mask = cv2.morphologyEx(mask, cv2.MORPH_OPEN, kernel)
    contours, _ = cv2.findContours(mask, cv2.RETR_EXTERNAL, cv2.CHAIN_APPROX_SIMPLE)
    regions: List[dict] = []
    for contour in contours:
        area = int(cv2.contourArea(contour))
        if area < min_area:
            continue
        x, y, w, h = cv2.boundingRect(contour)
        local_error = float(np.mean(gray[y : y + h, x : x + w]))
        regions.append({"x": x, "y": y, "width": w, "height": h, "meanError": round(local_error, 4)})
    return sorted(regions, key=lambda item: item["width"] * item["height"], reverse=True)


def write_report(report_path: Path, matches: Sequence[Match], regions: Sequence[dict], error: float) -> None:
    lines = [
        "Effect Image Assembler Report",
        f"Matched elements: {len(matches)}",
        f"Composite mean RGB error: {error:.4f}",
        f"Unmatched regions: {len(regions)}",
        "",
        "Low confidence elements:",
    ]
    low = [match for match in matches if match.confidence < 0.95]
    if not low:
        lines.append("  none")
    else:
        for match in sorted(low, key=lambda item: item.confidence):
            lines.append(f"  {match.name}: {match.confidence:.4f} {match.asset}")
    report_path.write_text("\n".join(lines), encoding="utf-8")


def to_json(matches: Sequence[Match], reference: np.ndarray, regions: Sequence[dict]) -> dict:
    height, width = reference.shape[:2]
    return {
        "referenceSize": [width, height],
        "elements": [
            {
                "name": match.name,
                "asset": match.asset_path,
                "spriteName": match.sprite_name,
                "x": round(match.x, 4),
                "y": round(match.y, 4),
                "width": round(match.width, 4),
                "height": round(match.height, 4),
                "scale": round(match.scale, 6),
                "order": int(match.order),
                "confidence": round(match.confidence, 6),
            }
            for match in sorted(matches, key=lambda item: item.order)
        ],
        "unmatchedRegions": list(regions),
    }


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--reference", required=True, type=Path)
    parser.add_argument("--sprites", type=Path)
    parser.add_argument("--asset-index", type=Path)
    parser.add_argument("--output-json", required=True, type=Path)
    parser.add_argument("--report-dir", required=True, type=Path)
    parser.add_argument("--min-scale", type=float, default=0.5)
    parser.add_argument("--max-scale", type=float, default=2.0)
    parser.add_argument("--scale-step", type=float, default=0.1)
    parser.add_argument("--threshold", type=float, default=0.92)
    parser.add_argument("--max-occurrences", type=int, default=64)
    parser.add_argument("--layer-iterations", type=int, default=5)
    parser.add_argument("--alpha-threshold", type=int, default=8)
    parser.add_argument("--diff-threshold", type=int, default=30)
    parser.add_argument("--min-unmatched-area", type=int, default=32)
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    if not args.reference.is_file():
        raise RuntimeError(f"Reference PNG does not exist: {args.reference}")
    if args.asset_index is None and args.sprites is None:
        raise RuntimeError("Either --asset-index or --sprites is required.")
    if args.asset_index is not None and not args.asset_index.is_file():
        raise RuntimeError(f"Asset index does not exist: {args.asset_index}")
    if args.sprites is not None and not args.sprites.is_dir():
        raise RuntimeError(f"Sprites folder does not exist: {args.sprites}")

    args.output_json.parent.mkdir(parents=True, exist_ok=True)
    args.report_dir.mkdir(parents=True, exist_ok=True)

    reference = read_bgra(args.reference)
    if args.asset_index is not None:
        assets = load_assets_from_index(args.asset_index, args.alpha_threshold)
    else:
        assets = load_assets(args.sprites, args.alpha_threshold)
    if not assets:
        raise RuntimeError("No usable PNG sprites were found.")
    scales = scale_values(args.min_scale, args.max_scale, args.scale_step)

    all_matches: List[Match] = []
    for asset in assets:
        asset_matches = match_asset(
            reference=reference,
            asset=asset,
            scales=scales,
            threshold=args.threshold,
            max_occurrences=args.max_occurrences,
            alpha_threshold=args.alpha_threshold,
        )
        all_matches.extend(asset_matches)

    assign_names(all_matches)
    ordered = initial_order(all_matches)
    optimize_order(reference, ordered, args.layer_iterations)

    composite = render_composite(reference.shape, ordered)
    error = composite_error(reference, ordered)
    regions = unmatched_regions(reference, composite, args.diff_threshold, args.min_unmatched_area)

    write_image(args.report_dir / "composite.png", composite)
    write_image(args.report_dir / "diff.png", diff_image(reference, composite))
    write_report(args.report_dir / "report.txt", ordered, regions, error)
    args.output_json.write_text(json.dumps(to_json(ordered, reference, regions), ensure_ascii=False, indent=2), encoding="utf-8")

    print(f"Matched {len(ordered)} elements from {len(assets)} assets. Mean RGB error: {error:.4f}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
