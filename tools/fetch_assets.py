"""Fetch verifiable, version-pinned League assets. No generated artwork.

Run with Python 3 + requests + Pillow. The desktop application needs neither.
"""
from concurrent.futures import ThreadPoolExecutor
from hashlib import sha256
from pathlib import Path
from io import BytesIO
import json
import sys
import requests
from PIL import Image, ImageOps

ROOT = Path(__file__).resolve().parents[1]
CD = "https://raw.communitydragon.org/12.21/"
UPSTREAM = "https://raw.githubusercontent.com/darrenprx/lolPing/c98806ae96a4dd83ca9c12be224878057f3d3ae7/"
RECORDS = []


def download(item):
    relative, url = item
    target = ROOT / relative
    target.parent.mkdir(parents=True, exist_ok=True)
    if target.exists():
        content = target.read_bytes()
    else:
        response = requests.get(url, timeout=60)
        response.raise_for_status()
        content = response.content
        if relative.endswith(".png"):
            Image.open(BytesIO(content)).verify()
        elif relative.endswith(".json"):
            json.loads(content)
        elif relative.endswith(".wav"):
            assert content[:4] == b"RIFF" and content[8:12] == b"WAVE", "Invalid WAV: " + relative
        target.write_bytes(content)
    return {"file": relative, "url": url, "sha256": sha256(content).hexdigest(), "bytes": len(content)}


def fetch(items):
    with ThreadPoolExecutor(max_workers=6) as pool:
        for record in pool.map(download, items):
            RECORDS.append(record)
            print(record["file"], record["bytes"])


def build_wheel(kind):
    layout = json.loads((ROOT / "references" / (kind + "-layout.json")).read_text(encoding="utf-8"))
    atlas = Image.open(ROOT / "assets/original/contextwheel_atlas.png").convert("RGBA")
    out = ROOT / "assets/wheel" / kind
    out.mkdir(parents=True, exist_ok=True)
    prefix = "PingWheel" if kind == "ping" else "ContextWheel"
    sprites = []
    for element in layout.values():
        name = element["mName"].rsplit("/", 1)[-1]
        if "Mini" in element["mName"] or "mAtlas" not in element or "mRect" not in element:
            continue
        if "SelectionLine" in name or name.endswith("_BG"):
            continue
        uv = tuple(int(x) for x in element["mAtlas"]["mTextureUV"])
        sprite = atlas.crop(uv)
        if element.get("mFlipX"):
            sprite = ImageOps.mirror(sprite)
        if element.get("mFlipY"):
            sprite = ImageOps.flip(sprite)
        rect = [int(x) for x in element["mRect"]]
        sprite = sprite.resize((rect[2] - rect[0], rect[3] - rect[1]), Image.Resampling.LANCZOS)
        sprite.save(out / (name + ".png"))
        sprites.append({"name": name, "rect": rect, "enabled": element.get("mEnabled", False), "layer": element.get("mLayer", 0)})
    base = Image.new("RGBA", (286, 286))
    for element in sorted(sprites, key=lambda x: x["layer"]):
        name = element["name"]
        if element["enabled"] and ("Frame" in name or "BG_0" in name):
            base.alpha_composite(Image.open(out / (name + ".png")), tuple(element["rect"][:2]))
    base.save(out / "base.png")
    (out / "elements.json").write_text(json.dumps(sprites, indent=2), encoding="utf-8")


def main():
    sys.stdout.reconfigure(encoding="utf-8")
    fetch([
        ("assets/original/contextwheel_atlas.png", CD + "game/assets/ux/menu/contextwheel_atlas.png"),
        ("references/ping-layout.json", CD + "game/gameplay/lolpingradial.uibase.bin.json"),
        ("references/emote-layout.json", CD + "game/gameplay/lolemoteradial.uibase.bin.json"),
        ("references/default-emotes.json", CD + "plugins/rcp-be-lol-game-data/global/default/v1/summoner-emotes.json"),
        ("references/zh-emotes.json", CD + "plugins/rcp-be-lol-game-data/global/zh_cn/v1/summoner-emotes.json"),
        ("references/upstream-license.txt", UPSTREAM + "LICENSE"),
        ("assets/fonts/BeaufortForLoL-Bold.otf", CD + "plugins/rcp-fe-lol-typekit/global/default/fonts/beaufort/beaufortforlol-bold.otf"),
        ("assets/fonts/license.json", CD + "plugins/rcp-fe-lol-typekit/global/default/license.json"),
    ])
    textures = [
        "ping-danger", "ping-danger-base", "ping-danger-base-color",
        "ping-comehere_02", "ping-comehere-base", "ping-comehere-base-color",
        "ping-mia_02", "ping-mia-base", "ping-mia-base-color", "ping-mia-base-outer", "ping-mia-base-outer-color",
        "ping-omw_02", "ping-omw-arrow", "ping-omw-base", "ping-omw-base-color",
        "ping-caution_02", "ping-caution-base", "ping-caution-base-color",
    ]
    fetch([("assets/pings/" + t + ".png", CD + "game/assets/shared/particles/" + t + ".png") for t in textures])
    fetch([("assets/sounds/" + name + ".wav", UPSTREAM + "assets/sounds/" + name + ".wav") for name in ["Danger", "OnMyWay", "ComeHere", "MIA", "Base", "button"]])
    catalog = json.loads((ROOT / "references/default-emotes.json").read_text(encoding="utf-8"))
    zh = {x["id"]: x["name"] for x in json.loads((ROOT / "references/zh-emotes.json").read_text(encoding="utf-8"))}
    ids = [1459, 1492, 1030, 1445, 1457, 1499, 1501, 1505, 1506, 3153, 3154, 3155, 3159, 3166, 3167]
    chosen = []
    items = []
    for emote_id in ids:
        emote = next(x for x in catalog if x["id"] == emote_id)
        path = emote["inventoryIcon"].replace("/lol-game-data/assets/", "").lower()
        url = CD + "plugins/rcp-be-lol-game-data/global/default/" + path
        items.append(("assets/emotes/" + str(emote_id) + ".png", url))
        chosen.append({"id": emote_id, "name": emote["name"], "nameZh": zh.get(emote_id, emote["name"]), "url": url})
    fetch(items)
    (ROOT / "assets/emotes/catalog.json").write_text(json.dumps(chosen, ensure_ascii=False, indent=2), encoding="utf-8")
    for kind in ["ping", "emote"]:
        build_wheel(kind)
    derived = []
    for target in sorted((ROOT / "assets/wheel").rglob("*")):
        if target.is_file():
            content = target.read_bytes()
            derived.append({"file": target.relative_to(ROOT).as_posix(), "sha256": sha256(content).hexdigest(), "bytes": len(content)})
    (ROOT / "assets/manifest.json").write_text(json.dumps({"gameVersion": "12.21", "records": RECORDS, "derived": derived}, indent=2), encoding="utf-8")
    print("All assets verified and wheel sprites cropped from the 12.21 atlas.")


if __name__ == "__main__":
    main()
