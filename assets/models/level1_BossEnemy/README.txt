Level 1 boss (Mixamo)

Place FBX files here and match names in BossLevel1 Inspector if needed:

  Dance.fbx   — with skin (dance animation, default clip name mixamo_com)
  FastRun.fbx — without skin (run animation, same skeleton)

If your files use other names, set DanceFbxPath and FastRunFbxPath on the BossLevel1 node.

Scale: Mixamo FBX is often tiny in Godot. On BossLevel1 use BossVisualUniformScale (e.g. 100) or scale only the Dancing mesh child — do not scale the whole CharacterBody3D root without resizing the capsule.

Playtest (F6): open scenes/enemies/BossLevel1_Playtest.tscn — not BossLevel1.tscn alone (no camera/light).

Level spawn: Level1BossDirector uses BossStandOffsetGlobal by default so the boss stays inside the arena; tune in Inspector if needed.
