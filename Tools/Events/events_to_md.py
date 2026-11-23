"""events.json を検証し、確認用の Docs/イベント一覧.md を書き出す。

使い方: py Tools/Events/events_to_md.py
"""
import json
import sys
from collections import Counter
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
SRC = ROOT / "Assets/_Project/Resources/Events/events.json"
DST = ROOT / "Docs/イベント一覧.md"

CATEGORIES = {
    "battle": "戦闘",
    "hp": "HP回復/減少",
    "madness": "狂気増加/減少",
    "coins": "塔片獲得",
    "layer_buff": "この層限定の効果",
    "warp": "隠し通路（層ワープ）",
    "trust": "信頼増加/減少",
}

STANCES = {"aggressive": "攻め", "defensive": "守り"}
KNOWLEDGE = {"known": "既知", "unknown": "未知"}


def signed(v):
    return f"+{v}" if v > 0 else str(v)


EFFECTS = {
    "hp_percent": lambda e: f"HP {signed(e['value'])}%",
    "madness": lambda e: f"狂気 {signed(e['value'])}",
    "coins": lambda e: f"塔片 {signed(e['value'])}",
    "coins_percent": lambda e: f"塔片 {signed(e['value'])}%（所持の割合）",
    "battle": lambda e: "強敵と戦闘" if e.get("kind") == "elite" else "戦闘",
    "battle_enemy_hp_percent": lambda e: f"敵HP {signed(e['value'])}%",
    "battle_opening_seconds": lambda e: f"敵の最初の攻撃 {abs(e['value'])}秒{'遅い' if e['value'] > 0 else '早い'}",
    "layer_word_damage_percent": lambda e: f"この層：単語ダメージ {signed(e['value'])}%",
    "layer_damage_taken_percent": lambda e: f"この層：被ダメージ {signed(e['value'])}%",
    "layer_enemy_hp_percent": lambda e: f"この層：敵HP {signed(e['value'])}%",
    "layer_opening_seconds": lambda e: f"この層：敵の最初の攻撃 {abs(e['value'])}秒{'遅い' if e['value'] > 0 else '早い'}",
    "layer_battle_coin_percent": lambda e: f"この層：戦闘の塔片 {signed(e['value'])}%",
    "layer_rerolls": lambda e: f"この層：リロール {signed(e['value'])}",
    "layer_heal_after_battle_percent": lambda e: f"この層：戦闘後にHP {signed(e['value'])}%",
    "warp": lambda e: f"{e['value']}層先へワープ" if e["value"] > 0 else f"{-e['value']}層前へ戻る",
    "trust": lambda e: f"{'全員' if e.get('target') == 'all' else '{target}'}の信頼 {signed(e['value'])}",
    "card_choices": lambda e: f"次の刻印の選択肢 {signed(e['value'])}",
    "skill_upgrade_random": lambda e: "刻印をランダムに1つレベルアップ",
    "relic_gain": lambda e: "レリック入手",
}

CONDITIONS = {
    "min_coins": lambda c: f"塔片 {c['value']} 以上",
    "can_warp_up": lambda c: "上へワープできる層",
    "can_warp_down": lambda c: "下へワープできる層",
}

BATTLE_ONLY = {"battle_enemy_hp_percent", "battle_opening_seconds"}


def validate(data):
    errors = []
    ids = Counter(ev["id"] for ev in data["events"])
    for i, n in ids.items():
        if n > 1:
            errors.append(f"{i}: id が重複")
    for ev in data["events"]:
        eid = ev["id"]
        if ev["category"] not in CATEGORIES:
            errors.append(f"{eid}: 不明なカテゴリ {ev['category']}")
        for c in ev.get("conditions", []):
            if c["type"] not in CONDITIONS:
                errors.append(f"{eid}: 不明な条件 {c['type']}")
        if len(ev["options"]) != 2:
            errors.append(f"{eid}: 選択肢が2つではない")
        for opt in ev["options"]:
            if opt["stance"] not in STANCES or opt["knowledge"] not in KNOWLEDGE:
                errors.append(f"{eid}/{opt['label']}: 意見タグが不正")
            total = sum(o["chance"] for o in opt["outcomes"])
            if total != 100:
                errors.append(f"{eid}/{opt['label']}: 確率の合計が {total}")
            for o in opt["outcomes"]:
                types = [e["type"] for e in o["effects"]]
                for t in types:
                    if t not in EFFECTS:
                        errors.append(f"{eid}/{opt['label']}: 不明な効果 {t}")
                if BATTLE_ONLY & set(types) and "battle" not in types:
                    errors.append(f"{eid}/{opt['label']}: 戦闘なしで戦闘補正がある")
    return errors


def describe(effects):
    if not effects:
        return "変化なし"
    return "、".join(EFFECTS[e["type"]](e) for e in effects)


def render(data):
    events = data["events"]
    counts = Counter(ev["category"] for ev in events)
    out = [
        "# イベント一覧（出来事マス）",
        "",
        "`Assets/_Project/Resources/Events/events.json` から `Tools/Events/events_to_md.py` で生成。直接編集しない。",
        "数値はすべて1層基準。ルールと効果の定義は `イベント設計.md`。",
        "",
        f"全 {len(events)} 本。" + "、".join(f"{CATEGORIES[k]} {counts[k]}" for k in CATEGORIES) + "。",
        "",
    ]
    for key, name in CATEGORIES.items():
        out += [f"## {name}", ""]
        for ev in (e for e in events if e["category"] == key):
            out += [f"### {ev['title']}（`{ev['id']}`）", ""]
            cond = [CONDITIONS[c["type"]](c) for c in ev.get("conditions", [])]
            if ev.get("minLayer", 1) > 1:
                cond.insert(0, f"{ev['minLayer']}層以降")
            if cond:
                out += [f"条件：{'、'.join(cond)}", ""]
            out += [f"> {ev['body']}", ""]
            for opt in ev["options"]:
                tag = f"{STANCES[opt['stance']]}・{KNOWLEDGE[opt['knowledge']]}"
                out.append(f"- **{opt['label']}**（{tag}）")
                for o in opt["outcomes"]:
                    out.append(f"  - {o['chance']}%：{describe(o['effects'])}　「{o['text']}」")
            out.append("")
    return "\n".join(out)


def main():
    data = json.loads(SRC.read_text(encoding="utf-8"))
    errors = validate(data)
    if errors:
        print("\n".join(errors))
        sys.exit(1)
    DST.write_text(render(data), encoding="utf-8")
    counts = Counter(ev["category"] for ev in data["events"])
    print(f"OK: {len(data['events'])} events -> {DST}")
    for k in CATEGORIES:
        print(f"  {k}: {counts[k]}")


if __name__ == "__main__":
    main()
