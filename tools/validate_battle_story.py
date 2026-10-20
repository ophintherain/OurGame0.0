from pathlib import Path
import re

root = Path(__file__).resolve().parents[1]
for carriage in (1, 2, 3):
    scene = (root / f"Assets/Scenes/fight/fight_{carriage}.unity").read_text(encoding="utf-8")
    ids = re.findall(r"^--- !u!\d+ &(\d+)", scene, re.M)
    assert len(ids) == len(set(ids)), f"fight_{carriage}: duplicate object ID"
    added = re.search(rf"^--- !u!\d+ &71{carriage}\d+", scene, re.M)
    assert added, f"fight_{carriage}: no story objects"
    local_references = re.findall(r"\{fileID: (\d+)\}(?!,)", scene[added.start():])
    missing = [value for value in local_references if value != "0" and value not in ids]
    assert not missing, f"fight_{carriage}: broken local references {missing[:5]}"
    assert scene.count("m_Name: BattleStoryCanvas") == 1
    assert scene.count("m_Name: StoryPlayerProxy") == 1
    assert scene.count("fd30902ac9165cd40809cd305a434991") == 1
    assert "  m_SortingOrder: 30000" in scene[added.start():]
    print(f"fight_{carriage}: {len(ids)} unique scene objects, story references resolved")

library = (root / "Assets/Modules/Story/StoryLibrary.asset").read_text(encoding="utf-8")
for name in ("boss1_post", "boss2_pre", "boss2_post", "boss3_post"):
    asset = root / f"Assets/Modules/Story/Definitions/{name}.asset"
    meta = asset.with_suffix(".asset.meta")
    guid = re.search(r"^guid: ([a-f0-9]{32})$", meta.read_text(), re.M).group(1)
    assert guid in library, f"{name}: missing from StoryLibrary"
    assert f"storyId: {name}" in asset.read_text(encoding="utf-8")
    print(f"{name}: registered")
