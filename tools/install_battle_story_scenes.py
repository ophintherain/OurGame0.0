"""Copy the existing exploration story UI into the three serialized fight scenes.

This preserves the source scene and all exploration scripts. Unity's editor menu
Spotlight/战斗/接入战斗剧情 UI performs the same operation on scene objects.
"""
from pathlib import Path
import re

ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / "Assets/Scenes/explore/first.unity"
DOC_START = re.compile(r"^--- !u!(\d+) &(\d+).*$", re.M)
REF = re.compile(r"\{fileID: (\d+)\}")


def parse(path):
    content = path.read_text(encoding="utf-8-sig")
    matches = list(DOC_START.finditer(content))
    header = content[: matches[0].start()]
    docs = {}
    for i, match in enumerate(matches):
        docs[int(match.group(2))] = content[match.start(): matches[i + 1].start() if i + 1 < len(matches) else len(content)]
    return header, docs


def name(d):
    hit = re.search(r"^  m_Name: (.*)$", d, re.M)
    return hit.group(1) if hit else None


def components(d):
    hit = re.search(r"^  m_Component:\n((?:  - component: \{fileID: \d+\}\n)+)", d, re.M)
    return [int(x) for x in re.findall(r"component: \{fileID: (\d+)\}", hit.group(1))] if hit else []


def children(d):
    hit = re.search(r"^  m_Children:.*(?:\n((?:  - \{fileID: \d+\}\n)*))?", d, re.M)
    return [int(x) for x in re.findall(r"fileID: (\d+)", hit.group(1) or "")] if hit else []


def field(d, key, value):
    result, count = re.subn(rf"^(  {re.escape(key)}: ).*$", rf"\g<1>{value}", d, count=1, flags=re.M)
    if count != 1:
        raise ValueError(f"Missing field {key}")
    return result


def transform_of(docs, go):
    for component in components(docs[go]):
        if docs[component].startswith("--- !u!4 ") or docs[component].startswith("--- !u!224 "):
            return component
    raise ValueError(f"No transform on {go}")


_, source = parse(SOURCE)
named = {name(doc): key for key, doc in source.items() if doc.startswith("--- !u!1 ")}
canvas_go = named["Canvas"]
system_go = named["StorySystem"]
overlay_go = named["StoryOverlay"]
map_go = named["Map"]
player_go = named["Player"]
transform_to_go = {transform_of(source, key): key for key, doc in source.items() if doc.startswith("--- !u!1 ")}
selected_go = {canvas_go, map_go, player_go}


def collect(transform):
    go = transform_to_go[transform]
    selected_go.add(go)
    for child in children(source[transform]):
        collect(child)


collect(transform_of(source, system_go))
collect(transform_of(source, overlay_go))
selected_components = set()
for go in selected_go:
    selected_components.update(components(source[go]))

# The invisible input proxy only needs these two exploration components.
map_components = components(source[map_go])
player_components = components(source[player_go])
map_script = next(x for x in map_components if "  m_Script:" in source[x])
player_script = next(x for x in player_components if "  m_Script:" in source[x])
proxy_keep = {map_go, player_go, transform_of(source, map_go), transform_of(source, player_go), map_script, player_script}
selected_components = {x for x in selected_components if x not in map_components + player_components or x in proxy_keep}
system_components = components(source[system_go])
story_scripts = {"fd30902ac9165cd40809cd305a434991", "d2b9d14998fb8154c9dac8a1992e74fc"}
system_keep = {x for x in system_components if x == transform_of(source, system_go) or
               source[x].startswith("--- !u!82 ") or any(g in source[x] for g in story_scripts)}
selected_components = {x for x in selected_components if x not in system_components or x in system_keep}
selected = selected_go | selected_components

for carriage in (1, 2, 3):
    path = ROOT / f"Assets/Scenes/fight/fight_{carriage}.unity"
    header, target = parse(path)
    if "BattleStoryCanvas" in path.read_text(encoding="utf-8"):
        print(f"fight_{carriage}: already installed")
        continue
    base = 7100000000000 + carriage * 10000000000
    ids = {old: base + old for old in selected}
    additions = []
    for old in sorted(selected):
        doc = source[old]
        if old == canvas_go:
            doc = field(doc, "m_Name", "BattleStoryCanvas")
        elif old == map_go:
            doc = field(doc, "m_Name", "StoryMapProxy")
            doc = re.sub(r"(^  m_Component:\n)(?:  - component: \{fileID: \d+\}\n)+",
                         lambda m: m.group(1) + "".join(f"  - component: {{fileID: {x}}}\n" for x in (transform_of(source, map_go), map_script)), doc, count=1, flags=re.M)
        elif old == player_go:
            doc = field(doc, "m_Name", "StoryPlayerProxy")
            doc = re.sub(r"(^  m_Component:\n)(?:  - component: \{fileID: \d+\}\n)+",
                         lambda m: m.group(1) + "".join(f"  - component: {{fileID: {x}}}\n" for x in (transform_of(source, player_go), player_script)), doc, count=1, flags=re.M)
        elif old == system_go:
            doc = re.sub(r"(^  m_Component:\n)(?:  - component: \{fileID: \d+\}\n)+",
                         lambda m: m.group(1) + "".join(f"  - component: {{fileID: {x}}}\n" for x in system_components if x in system_keep), doc, count=1, flags=re.M)
        elif old == transform_of(source, canvas_go):
            roots = [transform_of(source, overlay_go), transform_of(source, system_go)]
            doc = re.sub(r"^  m_Children:\n(?:  - \{fileID: \d+\}\n)*",
                         "  m_Children:\n" + "".join(f"  - {{fileID: {x}}}\n" for x in roots), doc, count=1, flags=re.M)
        elif old in (transform_of(source, map_go), transform_of(source, player_go)):
            doc = field(doc, "m_Father", "{fileID: 0}")
            doc = re.sub(r"^  m_Children:\n(?:  - \{fileID: \d+\}\n)*", "  m_Children: []\n", doc, count=1, flags=re.M)
        elif old == player_script:
            doc = field(doc, "controlEnabled", "0")
            for key in ("standingVisual", "proneVisual", "placeholderVisual"):
                doc = field(doc, key, "{fileID: 0}")
            for key in ("standingMoveFrames", "proneMoveFrames"):
                doc = re.sub(rf"^  {key}:.*\n(?:  - .*\n)*", f"  {key}: []\n", doc, flags=re.M)
        elif old in components(source[system_go]) and "  library:" in doc:
            for key in ("backpackPanel", "taskPanel", "interactables"):
                doc = field(doc, key, "{fileID: 0}")
            doc = field(doc, "player", f"{{fileID: {player_script}}}")
        if old in components(source[canvas_go]) and "  m_RenderMode:" in doc:
            doc = field(doc, "m_OverrideSorting", "1")
            doc = field(doc, "m_SortingOrder", "30000")
        doc = re.sub(rf"(?<=&)\b{old}\b", str(ids[old]), doc, count=1)
        doc = REF.sub(lambda m: f"{{fileID: {ids.get(int(m.group(1)), 0)}}}", doc)
        doc = re.sub(r"[ \t]+(?=\n)", "", doc)
        additions.append(doc)
    path.write_text(header + "".join(target.values()) + "".join(additions), encoding="utf-8")
    print(f"fight_{carriage}: installed {len(additions)} scene objects/components")
