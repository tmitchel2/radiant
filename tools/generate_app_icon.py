# Generates Radiant's default application icon: a warm radiant burst — a bright core with
# tapering rays — on a deep indigo squircle.
#
# Run from the repo root (needs Pillow). Writes src/Radiant.Host/Resources/icon.png, which is
# embedded in Radiant.Host and is the Dock icon of any application that does not supply its own
# (RadiantAppIdentity.DockIcon), and src/Radiant.Host/Resources/Radiant.icns, the matching bundle
# icon for tools/bundle-macos-app.sh (macOS only: uses sips + iconutil).
import math
import os
import shutil
import subprocess
import sys
import tempfile
from PIL import Image, ImageDraw, ImageFilter

S = 1024          # final size
SS = 4            # supersample factor
W = S * SS

img = Image.new("RGBA", (W, W), (0, 0, 0, 0))

# ---- squircle background with a vertical indigo gradient ----
bg = Image.new("RGBA", (W, W), (0, 0, 0, 0))
top, bottom = (40, 34, 92), (12, 10, 34)
grad = Image.new("RGBA", (1, W))
for y in range(W):
    t = y / (W - 1)
    grad.putpixel((0, y), tuple(int(top[i] + (bottom[i] - top[i]) * t) for i in range(3)) + (255,))
grad = grad.resize((W, W))
mask = Image.new("L", (W, W), 0)
margin = int(W * 0.09)
ImageDraw.Draw(mask).rounded_rectangle(
    (margin, margin, W - margin, W - margin), radius=int(W * 0.2), fill=255)
bg.paste(grad, (0, 0), mask)
img = Image.alpha_composite(img, bg)

cx = cy = W / 2

# ---- rays: alternating long and short tapering wedges ----
rays = Image.new("RGBA", (W, W), (0, 0, 0, 0))
dr = ImageDraw.Draw(rays)
count = 16
for k in range(count):
    angle = 2 * math.pi * k / count - math.pi / 2
    length = W * (0.36 if k % 2 == 0 else 0.26)
    half_width = 2 * math.pi / count * (0.22 if k % 2 == 0 else 0.16)
    inner = W * 0.08
    tip = (cx + math.cos(angle) * length, cy + math.sin(angle) * length)
    left = (cx + math.cos(angle - half_width) * inner, cy + math.sin(angle - half_width) * inner)
    right = (cx + math.cos(angle + half_width) * inner, cy + math.sin(angle + half_width) * inner)
    colour = (255, 196, 92, 255) if k % 2 == 0 else (255, 150, 80, 230)
    dr.polygon([left, tip, right], fill=colour)
rays = rays.filter(ImageFilter.GaussianBlur(W * 0.002))

# ---- glow behind the core ----
glow = Image.new("RGBA", (W, W), (0, 0, 0, 0))
ImageDraw.Draw(glow).ellipse(
    (cx - W * 0.2, cy - W * 0.2, cx + W * 0.2, cy + W * 0.2), fill=(255, 170, 70, 150))
glow = glow.filter(ImageFilter.GaussianBlur(W * 0.06))

# ---- bright core ----
core = Image.new("RGBA", (W, W), (0, 0, 0, 0))
cd = ImageDraw.Draw(core)
cd.ellipse((cx - W * 0.11, cy - W * 0.11, cx + W * 0.11, cy + W * 0.11), fill=(255, 214, 120, 255))
cd.ellipse((cx - W * 0.07, cy - W * 0.07, cx + W * 0.07, cy + W * 0.07), fill=(255, 246, 220, 255))

clipped = Image.new("RGBA", (W, W), (0, 0, 0, 0))
for layer in (glow, rays, core):
    clipped = Image.alpha_composite(clipped, layer)
inside = Image.new("RGBA", (W, W), (0, 0, 0, 0))
inside.paste(clipped, (0, 0), mask)
img = Image.alpha_composite(img, inside)

img = img.resize((S, S), Image.LANCZOS)

root = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
resources = os.path.join(root, "src", "Radiant.Host", "Resources")
png_path = os.path.join(resources, "icon.png")
img.save(png_path, optimize=True)
print("wrote", png_path)

if sys.platform != "darwin":
    sys.exit(0)

# ---- .icns via an .iconset (the sizes iconutil expects) ----
workdir = tempfile.mkdtemp()
try:
    iconset = os.path.join(workdir, "Radiant.iconset")
    os.mkdir(iconset)
    for size in (16, 32, 128, 256, 512):
        for scale in (1, 2):
            px = size * scale
            name = f"icon_{size}x{size}{'@2x' if scale == 2 else ''}.png"
            subprocess.run(["sips", "-z", str(px), str(px), png_path, "--out", os.path.join(iconset, name)],
                           check=True, capture_output=True)
    icns_path = os.path.join(resources, "Radiant.icns")
    subprocess.run(["iconutil", "-c", "icns", iconset, "-o", icns_path], check=True)
    print("wrote", icns_path)
finally:
    shutil.rmtree(workdir)
