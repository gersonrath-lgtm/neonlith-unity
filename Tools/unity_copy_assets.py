"""Kopiert die Unity-tauglichen Assets aus dem Godot-Projekt Neonlith (führend) in dieses Unity-Repository.
Aufruf: python3 -I Tools/unity_copy_assets.py <pfad-zum-godot-repo> [<pfad-zu-diesem-repo>]
Schreibt Assets/Neonlith/... und die Liste ASSET_MANIFEST.md. Unity-eigene Dateien (Skripte, Szenen, Shader, .meta)
bleiben unberührt.
"""
import os, re, shutil, sys, json

repo = os.path.abspath(sys.argv[1])
unity = os.path.abspath(sys.argv[2]) if len(sys.argv) > 2 else os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
if not os.path.isfile(os.path.join(repo, "project.godot")):
    sys.exit("Kein Godot-Projekt (project.godot fehlt): " + repo)
dst_root = os.path.join(unity, "Assets", "Neonlith")
SKIP_EXT = {".import", ".uid", ".DS_Store", ".tres", ".res", ".mesh", ".gdshader", ".gdshaderinc"}
copied, skipped = [], []

def copy_tree(src_rel, dst_rel, keep=None):
    src = os.path.join(repo, src_rel)
    for root, _, files in os.walk(src):
        for f in sorted(files):
            p = os.path.join(root, f)
            rel = os.path.relpath(p, src)
            ext = os.path.splitext(f)[1]
            if f == ".DS_Store" or ext in SKIP_EXT or (keep and ext not in keep):
                if ext in (".mesh", ".tres", ".res", ".gdshader", ".gdshaderinc"):
                    skipped.append((os.path.join(src_rel, rel), ext))
                continue
            out = os.path.join(dst_root, dst_rel, rel)
            os.makedirs(os.path.dirname(out), exist_ok=True)
            shutil.copy2(p, out)
            copied.append((os.path.join(src_rel, rel), os.path.join("Assets/Neonlith", dst_rel, rel), os.path.getsize(p)))

copy_tree("assets/models", "Art/Models")
copy_tree("tools/art/src", "Art/Models/Quellen", keep={".glb"})
copy_tree("tools/art/rig", "Art/Models/Quellen/rig", keep={".glb"})
copy_tree("assets/textures", "Art/Textures")
copy_tree("assets/worlds", "Art/Worlds")
copy_tree("assets/fonts", "Art/Fonts")
copy_tree("assets/audio/music", "Audio/Music")
copy_tree("assets/audio/sfx", "Audio/SFX")
copy_tree("assets/icons", "UI/Icons")
copy_tree("assets/thumbs", "UI/Thumbs")
copy_tree("assets/ui", "UI")
copy_tree("assets/android", "Android")
copy_tree("assets/encyclopedia", "Texte/Encyclopedia")
copy_tree("data/levels", "Resources/Data/levels", keep={".json"})
for f in ("campaign.json", "hangar.json"):
    shutil.copy2(os.path.join(repo, "data", f), os.path.join(dst_root, "Resources/Data", f))
    copied.append(("data/" + f, "Assets/Neonlith/Resources/Data/" + f, os.path.getsize(os.path.join(repo, "data", f))))
# Lizenzen und Credits gehören zu den Assets
os.makedirs(os.path.join(dst_root, "Texte/Lizenzen"), exist_ok=True)
for f in sorted(os.listdir(os.path.join(repo, "LICENSES"))):
    if f.startswith("GODOT"):
        continue
    shutil.copy2(os.path.join(repo, "LICENSES", f), os.path.join(dst_root, "Texte/Lizenzen", f))
shutil.copy2(os.path.join(repo, "CREDITS.txt"), os.path.join(dst_root, "Texte/CREDITS.txt"))

# --- Godot-Ressourcen (.tres mit script_class) nach JSON ---------------------------------
def parse_value(v):
    v = v.strip()
    if v in ("true", "false"):
        return v == "true"
    if re.fullmatch(r"-?\d+", v):
        return int(v)
    if re.fullmatch(r"-?\d*\.\d+(e-?\d+)?|-?\d+e-?\d+", v):
        return float(v)
    if v.startswith('"') and v.endswith('"'):
        return v[1:-1]
    m = re.fullmatch(r"(\w+)\((.*)\)", v)
    if m:
        kind, inner = m.group(1), m.group(2)
        nums = [float(x) for x in inner.split(",") if x.strip()] if inner.strip() else []
        if kind == "Color":
            return {"r": nums[0], "g": nums[1], "b": nums[2], "a": nums[3] if len(nums) > 3 else 1.0}
        if kind in ("Vector2", "Vector3", "Vector4"):
            return dict(zip("xyzw", nums))
        if kind.startswith("Packed"):
            step = 3 if "Vector3" in kind else 2 if "Vector2" in kind else 1
            return [nums[i:i + step] for i in range(0, len(nums), step)] if step > 1 else nums
        return {"_godot": kind, "values": nums}
    return v

def tres_to_json(path):
    out, cls = {}, ""
    in_res = False
    for line in open(path, encoding="utf-8"):
        line = line.strip()
        m = re.search(r'script_class="(\w+)"', line)
        if m:
            cls = m.group(1)
        if line == "[resource]":
            in_res = True
            continue
        if in_res and "=" in line and not line.startswith("script ="):
            k, v = line.split("=", 1)
            out[k.strip()] = parse_value(v)
    return {"_class": cls, "_source": os.path.relpath(path, repo), **out}

cfg_dir = os.path.join(dst_root, "Resources/Data/config")
os.makedirs(cfg_dir, exist_ok=True)
for f in sorted(os.listdir(os.path.join(repo, "data"))):
    if f.endswith(".tres"):
        d = tres_to_json(os.path.join(repo, "data", f))
        out = os.path.join(cfg_dir, f[:-5] + ".json")
        json.dump(d, open(out, "w", encoding="utf-8"), indent=1, ensure_ascii=False)
        copied.append(("data/" + f, "Assets/Neonlith/Resources/Data/config/" + f[:-5] + ".json (umgewandelt)", os.path.getsize(out)))

# --- Manifest ------------------------------------------------------------------------------
total = sum(s for _, _, s in copied)
with open(os.path.join(unity, "ASSET_MANIFEST.md"), "w", encoding="utf-8") as m:
    m.write("# Übernommene Assets\n\nErzeugt von `Tools/unity_copy_assets.py` aus dem Godot-Projekt (führend). %d Dateien, %.1f MB.\n\n" % (len(copied), total / 1e6))
    m.write("| Godot | Unity | Bytes |\n|---|---|---:|\n")
    for a, b, s in copied:
        m.write("| `%s` | `%s` | %d |\n" % (a, b, s))
    m.write("\n## Nicht übernommen (Godot-eigene Formate)\n\n")
    m.write("Diese Dateien sind Godot-Ressourcen oder Godot-Shader und müssen neu erzeugt bzw. neu geschrieben werden (siehe `Docs/UNITY_PORT.md`).\n\n")
    for a, ext in skipped:
        m.write("- `%s`\n" % a)
print("kopiert:", len(copied), "Dateien,", round(total / 1e6, 1), "MB; übersprungen:", len(skipped))
