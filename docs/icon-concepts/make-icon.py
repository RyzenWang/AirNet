#!/usr/bin/env python3
"""Regenerates src/AirNet.App/Assets/AirNet.ico from the 512px pulse concept.

The shape lives in 01-pulse.png; only the background colour is applied here, so changing the
brand colour is one constant away and the icon can never drift from the palette again.

    python make-icon.py                 # writes the .ico next to the app
    python make-icon.py --brand E3563B  # override the colour

Requires Pillow. Not part of the build: run it when the brand colour changes, commit the .ico.
"""

from __future__ import annotations

import argparse
import os
from collections import Counter

from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, "..", ".."))
SOURCE = os.path.join(HERE, "01-pulse.png")
TARGET = os.path.join(ROOT, "src", "AirNet.App", "Assets", "AirNet.ico")
MAC_TARGET = os.path.join(ROOT, "packaging", "assets", "AirNet.icns")

GLYPH = (251, 250, 247)                     # paper white, same as the glyph in the concept
SIZES = [16, 24, 32, 48, 64, 128, 256]


def luminance(pixel: tuple[int, int, int]) -> float:
    r, g, b = pixel
    return 0.299 * r + 0.587 * g + 0.114 * b


def dominant_colour(image: Image.Image) -> tuple[int, int, int]:
    counts = Counter(pixel[:3] for pixel in image.getdata() if pixel[3] > 200)
    return counts.most_common(1)[0][0]


def is_accent_dot(pixel: tuple[int, int, int], background: tuple[int, int, int]) -> bool:
    """The warm marker at the end of the pulse; it must stay a distinct element."""
    r, g, b = pixel
    if g <= b or r <= 190:
        return False
    # Clearly warmer and lighter than whatever the background happens to be.
    return luminance(pixel) > luminance(background) + 45 and b < 170


def recolour(source: Image.Image, brand: tuple[int, int, int]) -> Image.Image:
    background = dominant_colour(source)
    background_lum = luminance(background)
    white_lum = luminance(GLYPH)
    span = max(1.0, white_lum - background_lum)

    out = Image.new("RGBA", source.size)
    src, dst = source.load(), out.load()
    width, height = source.size

    for y in range(height):
        for x in range(width):
            pixel = src[x, y]
            if pixel[3] == 0:
                dst[x, y] = (0, 0, 0, 0)
                continue

            if is_accent_dot(pixel[:3], background):
                dst[x, y] = (*GLYPH, pixel[3])
                continue

            coverage = (luminance(pixel[:3]) - background_lum) / span
            coverage = 0.0 if coverage < 0 else (1.0 if coverage > 1 else coverage)
            dst[x, y] = (
                round(coverage * GLYPH[0] + (1 - coverage) * brand[0]),
                round(coverage * GLYPH[1] + (1 - coverage) * brand[1]),
                round(coverage * GLYPH[2] + (1 - coverage) * brand[2]),
                pixel[3],
            )

    return out


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--brand", default="E3563B", help="brand background colour, hex")
    args = parser.parse_args()

    brand = tuple(int(args.brand[i:i + 2], 16) for i in (0, 2, 4))

    source = Image.open(SOURCE).convert("RGBA")
    if source.size != (512, 512):
        raise SystemExit(f"expected a 512x512 source, found {source.size}")

    icon = recolour(source, brand)

    # Refresh the concept image too, so the docs show the colour that actually ships.
    icon.save(SOURCE)

    os.makedirs(os.path.dirname(TARGET), exist_ok=True)
    icon.save(TARGET, format="ICO", sizes=[(s, s) for s in SIZES])

    # macOS bundles its own icon; leaving it on the old colour was the same bug one platform over.
    os.makedirs(os.path.dirname(MAC_TARGET), exist_ok=True)
    icon.save(MAC_TARGET, format="ICNS")

    print(f"brand   {args.brand}")
    print(f"sizes   {', '.join(str(s) for s in SIZES)}")
    print(f"written {TARGET} ({os.path.getsize(TARGET)} bytes)")
    print(f"written {MAC_TARGET} ({os.path.getsize(MAC_TARGET)} bytes)")


if __name__ == "__main__":
    main()
