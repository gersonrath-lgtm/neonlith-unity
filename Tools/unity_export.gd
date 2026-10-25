## Exportiert Godot-eigene Netze als GLB für den Unity-Port (Docs/UNITY_PORT.md).
##
## Was Unity nicht lesen kann: assets/models/*.mesh (ArrayMesh-Ressourcen der Gegner und des Laubs) und die
## Modelle, die im Spiel aus Code entstehen (scripts/view3d: TowerModels, WeaponModels, RobotModels, KlarBridge,
## FogBridge, ...). Dieses Werkzeug schreibt die .mesh-Dateien und eine Auswahl erzeugter Modelle als GLB in das
## Unity-Repository nach Assets/Neonlith/Art/Models/AusGodot/. Materialien kommen als einfache PBR-Werkstoffe mit
## (Farbe, Textur); die Godot-Shader (Lauf-Animation, Holo, Lowpoly-Facetten) müssen in Unity neu entstehen.
##
## Aufruf aus der Wurzel des Godot-Projekts (führend), Godot 4.7; das Skript liegt im Unity-Repository:
##   godot --headless --path . -s /pfad/zu/neonlith-unity/Tools/unity_export.gd -- --out=/pfad/zu/neonlith-unity
## Ohne --out wird das Unity-Repository neben dem Godot-Projekt erwartet (../neonlith-unity).
## Danach in Unity: glTFast importiert die GLB-Dateien beim nächsten Fokus des Editors.
extends SceneTree

const MESH_DIR := "res://assets/models"
var out_dir := ""


func _init() -> void:
	var unity := ProjectSettings.globalize_path("res://../neonlith-unity")
	for a in OS.get_cmdline_user_args():
		if a.begins_with("--out="):
			unity = a.substr(6)
	out_dir = unity.path_join("Assets/Neonlith/Art/Models/AusGodot")
	if DirAccess.make_dir_recursive_absolute(out_dir) != OK:
		push_error("unity_export: Zielordner nicht anlegbar: " + out_dir)
		quit(1)
		return
	var n := 0
	n += _export_mesh_files()
	n += _export_generated()
	print("unity_export: %d GLB-Dateien nach %s geschrieben" % [n, out_dir])
	quit()


## Alle .mesh-Dateien im Modellordner (Gegner, Laub)
func _export_mesh_files() -> int:
	var count := 0
	var dir := DirAccess.open(MESH_DIR)
	if dir == null:
		push_error("unity_export: %s nicht gefunden" % MESH_DIR)
		return 0
	for f in dir.get_files():
		if not f.ends_with(".mesh"):
			continue
		var mesh: Mesh = load(MESH_DIR + "/" + f)
		if mesh == null:
			push_warning("unity_export: %s nicht ladbar" % f)
			continue
		var root := Node3D.new()
		root.name = f.get_basename()
		var mi := MeshInstance3D.new()
		mi.name = root.name
		mi.mesh = mesh
		# Albedo-Textur gleichen Namens als Werkstoff mitgeben (enemy_scarab.mesh -> enemy_scarab_albedo.png)
		var tex_path := MESH_DIR + "/" + f.get_basename() + "_albedo.png"
		if ResourceLoader.exists(tex_path):
			var mat := StandardMaterial3D.new()
			mat.albedo_texture = load(tex_path)
			mat.roughness = 0.8
			for s in mesh.get_surface_count():
				mi.set_surface_override_material(s, mat)
		root.add_child(mi)
		if _write_glb(root, f.get_basename()):
			count += 1
		root.free()
	return count


## Modelle aus Code. Die Generatoren brauchen teils Spielkontext (Welt, Palette); hier nur die, die sich ohne
## Szene aufrufen lassen. Weitere Einträge nach demselben Muster ergänzen: Knoten bauen, _write_glb aufrufen.
func _export_generated() -> int:
	var count := 0
	var jobs := {}
	# Waffen des Arsenals (WeaponModels.build, 4.99.46): Pistole und Sniper aus dem Generator; das Sturmgewehr
	# ist das GLB gun_cranium.glb und schon kopiert
	var wm = load("res://scripts/view3d/weapon_models.gd")
	for kind in ["pistol", "sniper"]:
		jobs["weapon_" + kind] = wm.build(kind)
	# Spielerfigur Rook (RobotModels) und Türme (TowerModels) bauen ihre Teile je Knochen bzw. je Stufe mit
	# Spielkontext; dafür nach demselben Muster einen Node3D zusammensetzen und hier eintragen.
	for key in jobs:
		var node = jobs[key]
		if node is Node3D:
			if _write_glb(node, key):
				count += 1
			node.free()
		else:
			push_warning("unity_export: Generator %s lieferte keinen Node3D" % key)
	return count


func _write_glb(root: Node3D, name: String) -> bool:
	var doc := GLTFDocument.new()
	var state := GLTFState.new()
	var err := doc.append_from_scene(root, state)
	if err != OK:
		push_error("unity_export: %s: append_from_scene %d" % [name, err])
		return false
	var path := out_dir.path_join(name + ".glb")
	err = doc.write_to_filesystem(state, path)
	if err != OK:
		push_error("unity_export: %s: write_to_filesystem %d" % [name, err])
		return false
	print("unity_export: ", path)
	return true
