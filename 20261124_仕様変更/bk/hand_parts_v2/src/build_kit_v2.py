"""hand_parts_v2 : v1 kit with the trigger gems moved to the card's top edge (right-aligned, straddling the border)
and the state tags moved to the top-left. Parts are copied unchanged from hand_parts_v1 (v1 is not modified/re-run).
Only layout (assembly + preview + docs) changes."""
import sys, os, json, shutil, zipfile
sys.path.insert(0, "/workspace/art/ui/battle_hand_v1/src")
import build_mocks as V1                      # text / font helpers only (no side effects on import)
from build_mocks import F, text, ART
from PIL import Image, ImageDraw, ImageChops

V1KIT = f"{ART}/ui/hand_parts_v1"
OUT = f"{ART}/ui/hand_parts_v2"
P = f"{OUT}/parts"
if os.path.exists(P): shutil.rmtree(P)
shutil.copytree(f"{V1KIT}/parts", P)          # same PNGs as v1

W, H, R, PAD = 313, 150, 10, 28
RAR = {"N_silver": (196, 210, 232), "R_gold": (232, 178, 64), "SR_rainbow": (210, 150, 255)}
GEM, GEM_GAP = 24, 4

# ---------------- new layout (relative to card top-left) ----------------
GEMS_RIGHT = W - 48          # backing right edge x=265 for every card -> gem columns line up; clears the ×N badge (x277..)
BACK_H = GEM + 8             # 32
BACK_Y = -16                 # backing y -16..16 : centred on the top border
def backing_x(n): return GEMS_RIGHT - (n * GEM + (n - 1) * GEM_GAP + 14)
def gem_x(n, i): return backing_x(n) + 7 + i * (GEM + GEM_GAP)
GEM_Y = BACK_Y + 4           # -12 .. 12
TAG_SPRITE = (-2, -54)       # tag sprite top-left (sprite has 10px glow padding) -> pill x 8.., y -44..-16
LAYOUT = {
    "name": {"font": "Shippori Mincho ExtraBold", "size": 28, "x": 20, "baseline_y": 48, "max_width": 270},
    "divider": {"x0": 20, "x1": 190, "y": 64},
    "effect": {"font": "Noto Sans JP Bold", "size": 20, "x": 20, "baseline_y": [102, 132], "max_lines": 2, "max_width": 262,
               "number_color": "#F2D58A", "color": "#F0ECF6"},
    "name_color": {"N_silver": "#ECF2FC", "R_gold": "#FFE9B0", "SR_rainbow": "#EAD8FF"},
    "stars": {"size": 22, "centers_x": [W - 70, W - 49, W - 28], "center_y": 66},
    "gem_backing": {"right_x": GEMS_RIGHT, "y": BACK_Y, "h": BACK_H, "x_for_n": {n: backing_x(n) for n in range(1, 6)},
                    "note": "上辺をまたぐ（カード上端 y=0 が楕円の中心）。右揃え"},
    "gems": {"size": GEM, "gap": GEM_GAP, "y": GEM_Y, "x_first_for_n": {n: gem_x(n, 0) for n in range(1, 6)}, "step": GEM + GEM_GAP},
    "stack_badge": {"pos_rel_card": [W - 42, -22], "pill_box_rel_card": [W - 36, -16, W + 15, 13]},
    "ghost_card": {"pos_rel_card": [6, -6]},
    "state_glow": {"pos_rel_card": [-PAD, -PAD]},
    "tag": {"sprite_pos_rel_card": list(TAG_SPRITE), "pill_box_rel_card": "x 8..(8+幅), y -44..-16", "align": "左揃え"},
}

def colorise(im, col):
    r, g, b, a = im.split()
    return Image.merge("RGBA", [ch.point(lambda v, k=k: v * k // 255) for ch, k in zip((r, g, b), col)] + [a])
def rmask():
    m = Image.new("L", (W, H)); ImageDraw.Draw(m).rounded_rectangle((0, 0, W - 1, H - 1), R, fill=255); return m
def hexrgb(h): return tuple(int(h[i:i + 2], 16) for i in (1, 3, 5))

def card_body(c, art):
    """layers 1-6 + stars (everything inside the card rectangle)"""
    rk = c["rar"]; card = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    card.alpha_composite(Image.open(f"{P}/card/card_base_313x150.png"))
    a = art.resize((W, H), Image.LANCZOS); a.putalpha(ImageChops.multiply(a.getchannel("A"), rmask())); card.alpha_composite(a)
    card.alpha_composite(Image.open(f"{P}/card/text_gradient_313x150.png"))
    card.alpha_composite(Image.open(f"{P}/frame/frame_{rk}.png"))
    card.alpha_composite(Image.open(f"{P}/edge/edge_left_{c['trig'][0]}.png")); card.alpha_composite(Image.open(f"{P}/edge/edge_right_{c['trig'][-1]}.png"))
    L = LAYOUT
    text(card, (L["name"]["x"], L["name"]["baseline_y"]), c["name"], F("name", 28), hexrgb(L["name_color"][rk]))
    ImageDraw.Draw(card).line((20, L["divider"]["y"], 190, L["divider"]["y"]), fill=(*RAR[rk], 140), width=1)
    for i, ln in enumerate(c["eff"]): V1.effect_line(card, 20, L["effect"]["baseline_y"][i], ln, F("sans", 20))
    for i, cx in enumerate(L["stars"]["centers_x"]):
        card.alpha_composite(Image.open(f"{P}/badge/star_{'lit' if i < c['lv'] else 'unlit'}_22.png"), (cx - 11, 66 - 11))
    return card

def gems_on_edge(im, c, x, y):
    n = len(c["trig"])
    im.alpha_composite(colorise(Image.open(f"{P}/gem/gem_backing_n{n}.png"), RAR[c["rar"]]), (x + backing_x(n), y + BACK_Y))
    for i, col in enumerate(c["trig"]):
        im.alpha_composite(Image.open(f"{P}/gem/gem_{col}_{'on' if i < c.get('lit', 0) else 'dim'}_24.png"), (x + gem_x(n, i), y + GEM_Y))

HAND = [
    dict(name="一ノ太刀・紫電", rar="R_gold", trig="RBP", lit=3, eff=["敵に120ダメージ", "「集中」+1"], lv=1, state="ready", art="slash"),
    dict(name="燕返し・紅蓮", rar="N_silver", trig="BPR", lit=2, eff=["敵に60ダメージ", "重なり1枚ごとに+20"], lv=2, state="near", stack=2, art="moon"),
    dict(name="二ノ太刀・蒼氷", rar="N_silver", trig="BG", lit=0, eff=["敵に80ダメージ", "敵の攻撃を1秒遅らせる"], lv=1, art="slash"),
    dict(name="明鏡止水・金光", rar="SR_rainbow", trig="YPB", lit=0, eff=["「集中」+3", "次のスキル威力1.5倍"], lv=1, state="chain", art="moon"),
    dict(name="大薙ぎ・翠嵐", rar="R_gold", trig="RBPG", lit=3, eff=["敵に150ダメージ", "「集中」を2消費"], lv=3, state="near", stack=3, art="slash"),
]
LIFT = {"ready": 28, "near": 14, "chain": 0, None: 0}

def preview():
    im = Image.new("RGBA", (1920, 1080), (0, 0, 0, 255))
    bg = Image.open(f"{ART}/bg/layer1_angle/upscaled_2x.png").convert("RGB").resize((1920, 1080), Image.LANCZOS)
    im.paste(Image.blend(bg, Image.new("RGB", bg.size, (6, 3, 5)), 0.55))
    arts = {k: Image.open(f"{P}/art/placeholder_{k}_626x300.png").convert("RGBA") for k in ("slash", "moon")}
    pos = []
    for i, c in enumerate(HAND):
        x = 272 + i * (W + 16); y = 916 - LIFT[c.get("state")]; pos.append((x, y)); st = c.get("state")
        g = {"ready": "glow_ready_369x206", "chain": "glow_chain_Y_369x206", "near": "glow_near_369x206"}.get(st)
        if g: im.alpha_composite(Image.open(f"{P}/state/{g}.png"), (x - PAD, y - PAD))
        if c.get("stack"): im.alpha_composite(colorise(Image.open(f"{P}/badge/stack_ghost_card.png"), RAR[c["rar"]]), (x + 6, y - 6))
        im.alpha_composite(card_body(c, arts[c["art"]]), (x, y))
        if st == "near": im.alpha_composite(Image.open(f"{P}/state/near_top_edge_313x8.png"), (x, y))
    for c, (x, y) in zip(HAND, pos):                       # on top: gems on the edge, badge, tag
        gems_on_edge(im, c, x, y)
        if c.get("stack"): im.alpha_composite(Image.open(f"{P}/badge/stack_x{c['stack']}.png"), (x + W - 42, y - 22))
        st = c.get("state")
        t = {"ready": "tag_ready", "chain": "tag_chain_Y"}.get(st) or (f"tag_near_{c['trig'][-1]}" if st == "near" else None)
        if t: im.alpha_composite(Image.open(f"{P}/state/{t}.png"), (x + TAG_SPRITE[0], y + TAG_SPRITE[1]))
    text(im, (272, 776), "hand_parts_v2 preview  ／  発動条件の宝石＝カード上辺・右揃え（右端 x+265）  ／  状態タグ＝左上", F("sans", 22), (230, 220, 210))
    text(im, (272, 806), "通常 y916・あと1色 y902(+14)・条件成立 y888(+28)  ／  STOCK：赤・青・紫  ／  直前の色：黄", F("sans", 19), (200, 192, 186))
    rgb = im.convert("RGB"); rgb.save(f"{OUT}/preview.png")
    rgb.crop((240, 750, 1920, 1080)).save(f"{OUT}/preview_zoom_hand.png")
    x, y = pos[0]
    rgb.crop((x - 30, y - 70, x + W + 30, y + H + 30)).resize(((W + 60) * 2, (H + 100) * 2), Image.LANCZOS).save(f"{OUT}/card_ichinotachi_2x.png")
    return pos

if __name__ == "__main__":
    pos = preview()
    v1 = json.load(open(f"{V1KIT}/layout.json"))
    json.dump(dict(card=[W, H], layout=LAYOUT, hand_positions=pos, lift=dict(rest=0, near=14, ready=28), files=v1["files"]),
              open(f"{OUT}/layout.json", "w"), ensure_ascii=False, indent=1)
    z = f"{OUT}/hand_parts_v2.zip"
    with zipfile.ZipFile(z, "w", zipfile.ZIP_DEFLATED) as zf:
        for dp, dn, fn in os.walk(OUT):
            for f in fn:
                p = os.path.join(dp, f)
                if p == z or "__pycache__" in p: continue
                zf.write(p, os.path.join("hand_parts_v2", os.path.relpath(p, OUT)))
    print(pos, {n: backing_x(n) for n in range(1, 6)}, "zip", len(zipfile.ZipFile(z).namelist()))
