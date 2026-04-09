# Level 2 — Rakennusopas & Projektin tila

> Tämä dokumentti on kirjoitettu Claude-tekoälyavustajalle kontekstikseen.
> Se kuvaa projektin nykytilan, level_1:n arkkitehtuurin referenssinä ja ohjeet level_2:n rakentamiseen.

---

## Projektin yleiskuva

**Nimi:** Game Over  
**Moottori:** Godot 4.x, C# / .NET (assembly: `GameOver`)  
**Fysiikka:** Jolt Physics  
**Tyyli:** 2.5D action — 3D-maailma + sivukamera  
**Kohdealusta:** Windows (Steam) + Raspberry Pi 5 (8 GB) — heikko integroitu GPU, optimointi tärkeää  
**Ohjaimen:** PS5 DualSense (button_index: Cross=0, Circle=1, Square=2, Triangle=3, L1=9, R1=10)

---

## Hakemistorakenne (tärkeimmät)

```
scripts/
  player/
    PlayerController.cs     ← pelaajan kaikki logiikka (CharacterBody3D)
    HealthComponent.cs      ← HP, kuolema, respawn
  enemies/
    BossLevel1.cs           ← level_1 boss (tanssi/syöksyfaasi)
  levels/
    Level1ArcadePhysicsSetup.cs  ← arcade-propsien fysiikka ajon aikana
    Level1BossDirector.cs        ← bossin spawnus ja intro-kamera
    Level1ExitHole.cs            ← bossin kuoltua avautuva lattiarei'kä → level_2
    LevelExit.cs                 ← yleinen Area3D-exit (NextScene-export)
    GameState.cs                 ← AutoLoad-singleton (HasJoystick ym. tila)
  CameraFollow.cs           ← kameran seurauslogiikka
  HUDController.cs          ← HP-palkki, boss-HP, R1-vihje

scenes/
  characters/
    player.tscn             ← pelaajan PackedScene
    gameover_character.tscn ← 3D-malli + Skeleton3D + BoneAttachments (SwordAttachment, ShieldAttachment)
  enemies/
    BossLevel1.tscn
    EnemyLevel1.tscn        ← perusvihollinen (susi), ryhmä "enemy"
    EnemySpawner.cs         ← spawnauslogiikka
  levels/
    level_1.tscn            ← valmis kenttä (referenssi level_2:lle)
    level_2.tscn            ← LUODAAN SEURAAVAKSI
  ui/
    hud.tscn                ← HUD (lisää kentän juureen)
    main_menu.tscn
    game_over.tscn

assets/
  models/animations/        ← Mixamo FBX -animaatiot
  audio/
    music/musiclevel1.mp3
    sfx/miekka.mp3, vahinko.mp3, enemybosshit.mp3, death.mp3, puraisu.mp3
```

---

## AutoLoad-singleton: GameState

`scripts/GameState.cs` on rekisteröity `project.godot`-tiedostossa autoloadiksi.

```csharp
GameState.Instance.HasJoystick  // bool — true jos pelaaja on poiminut joystickin level_1:ssä
```

Level_2:ssa tarvitset tätä erityisesti drone-moodin aktivoimiseen.

---

## Pelaajan InputMap (project.godot)

| Action | Joypad | Näppäimistö | Käyttö |
|--------|--------|-------------|--------|
| `move_left/right` | Akseli 0 | A/D | Liike |
| `move_forward/back` | Akseli 1 | W/S | Syvyysliike (DepthMovementEnabled) |
| `jump` | button 0 (Cross) | Space | Hyppy |
| `sit` | button 1 (Circle) | I | Istuminen (toggle) |
| `grab` | button 2 (Square) | E | Tarttuminen (grabbable-ryhmä) |
| `toggle_weapon` | button 3 (Triangle) | — | Asemoodi tai drone-moodi (jos HasJoystick) |
| `block` | Akseli 4 (L2) | — | Kilpi |
| `attack` | Akseli 5 (R2) | F | Perusmiekka |
| `attack_r1` | button 10 (R1) | — | Raskas latauslyönti |
| `cam_look_*` | Akseli 2/3 | — | Kamera |

---

## Törmäyskerrokset (Collision Layers)

| Kerros | Bitmask | Kuka käyttää |
|--------|---------|-------------|
| 1 | 1 | Maailma (lattia, seinät), pelaaja (oletus) |
| 2 | 2 | Viholliset (EnemyLevel1, BossLevel1) |
| 5 | 16 | Arcade-proppit (AirHockeyRigid, Level1ArcadePhysicsSetup) |

**Level_2 suositus:** Käytä samaa konventiota. Pelaaja on kerroksella 1, viholliset kerroksella 2.

---

## Level_1 rakenne referenssinä

Level_1.tscn juurinoodi on `Node3D ("Level 1")` jolla on nämä skriptit/lapset:

- **Juuri:** `Level1ArcadePhysicsSetup.cs` (skripti) — luo arcade-propsit koodissa
- **Player** (instance) — `PlayerController` + `HealthComponent`
- **Camera3D** — `CameraFollow.cs`
- **EnemySpawner** — `EnemySpawner.cs`
- **Floor** `StaticBody3D` — lattia (BoxShape 30×30)
- **Wall_N/S/E/W** `StaticBody3D` — areenan seinät
- **HUD** (instance `hud.tscn`)
- **JoystickLever** `Area3D` — bossia ennen aktivoitava vipu → poimitaan bossin kuoltua
- **ExitHole** `Node3D` — `Level1ExitHole.cs`: avautuu bossin kuoltua, vie level_2:een

### Level_1 pelattavuuskaari:
1. Pelaaja astuu kentälle
2. Painaa Neliötä JoystickLeverin vieressä → viholliset spawnautuvat
3. Viholliset tapettu → Level1BossDirector spawnaa bossin
4. Boss kuolee → JoystickLever hohtaa (poimi Neliöllä) + lattiarei'kä avautuu
5. Pelaaja poimii joystickin (GameState.HasJoystick = true)
6. Pelaaja hyppää reikään → Area3D → `ChangeSceneToFile("res://scenes/levels/level_2.tscn")`

---

## Drone-moodi (level_2:sta eteenpäin)

Kun `GameState.Instance.HasJoystick == true`:
- **Kolmio (toggle_weapon)** → `EnterDroneMode()` / `ExitDroneMode()` `PlayerController.cs`:ssä
- Drone-moodi: pelaaja istuu (`mixamo_com_004`), joystick-mesh tulee oikeaan käteen (`SwordAttachment`-bone)
- `_isDroneMode = true` flag PlayerControllerissa
- **Drone-ohjauslogiikka on PLACEHOLDER** — toteutus puuttuu vielä
- Lisää drone-entiteetti ja ohjauslogiikka `_isDroneMode`-ehdon taakse

---

## Level_2 rakentamisohjeet

### 1. Luo scene
`scenes/levels/level_2.tscn` — juuri `Node3D` nimeltä `"Level 2"`.

### 2. Lisää pakolliset nodet

```
Level 2 (Node3D)
├── Player (instance: scenes/characters/player.tscn)
├── Camera3D (script: scripts/CameraFollow.cs)
├── HUD (instance: scenes/ui/hud.tscn)
├── Floor (StaticBody3D + CollisionShape3D + MeshInstance3D)
└── [Kentän sisältö]
```

### 3. PlayerController tärkeät exportit (aseta Inspectorissa)

| Export | Suositus | Selitys |
|--------|----------|---------|
| `RunSpeed` | 6.0 | Juoksunopeus |
| `DepthMovementEnabled` | true/false | Syvyysliike käytössä |
| `DepthClampMin/Max` | ±10–14 | Kentän Z-rajat |
| `SwordHitReachExtraMeters` | 0.05 | Miekan kantama (level_1:ssä 0.05) |
| `LightMeleeProximityMax` | 0.85 | R2-osuman läheisyysraja |
| `HeavyMeleeProximityMax` | 0.9 | R1-osuman läheisyysraja |

### 4. CameraFollow
Liitä `CameraFollow.cs` `Camera3D`-nodeen. Aseta `TargetPath` → Player-noodin polku.

### 5. Vihollisten spawnus
Instansoi `EnemySpawner.cs` tai käytä `ArenaWaveDirector.cs` aaltoihin. Viholliset lisätään ryhmään `"enemy"`.

### 6. Boss level_2:lle
- Kopioi `BossLevel1.cs` / `BossLevel1.tscn` pohjaksi tai luo uusi
- Jos bossi on erilainen, aloita uudesta skriptistä `CharacterBody3D`:n pohjalta
- Boss lisätään ryhmään `"level2_boss"` (tai sopiva nimi)
- Bossin kuoltua: aktivoi drone tai jokin seuraava mekaniikka

### 7. Level exit
Lisää `LevelExit.cs` + `Area3D` kentän loppuun. Aseta `NextScene`-export Inspectorissa.

```csharp
// LevelExit.cs — yksinkertainen exit:
[Export] public string NextScene = "res://scenes/levels/level_3.tscn";
```

### 8. Drone-toiminnallisuus
Pelaajalla on nyt joystick (`GameState.Instance.HasJoystick == true`). Drone-entiteetti tulisi:
- Spawnautua kun `EnterDroneMode()` kutsutaan (tai kentän alusta)
- Reagoida kameran akseli-inputeihin kun `_isDroneMode == true`
- Lisää `DroneController.cs` + `RigidBody3D` tai `CharacterBody3D`

---

## Tärkeät koodivihjeet level_2:lle

### Pelaajan terveys
```csharp
var hc = player.GetNodeOrNull<HealthComponent>("HealthComponent");
hc?.TakeDamage(amount);
```

### Vihollisen kuolema tunnistus
```csharp
// EnemyLevel1-tyylinen rakenne:
private void Die() {
    _isDead = true;
    RemoveFromGroup("enemy");
    QueueFree();
}
```

### Kenttävaihto
```csharp
GetTree().ChangeSceneToFile("res://scenes/levels/level_3.tscn");
```

### Äänitehosteet (sama patterni kuin BossLevel1)
```csharp
var stream = GD.Load<AudioStream>("res://assets/audio/sfx/miekka.mp3");
var sfx = new AudioStreamPlayer();
GetTree().Root.AddChild(sfx);
sfx.Stream = stream;
sfx.Play();
sfx.Finished += () => sfx.QueueFree();
```

### Pelaajan haku
```csharp
var player = GetTree().GetFirstNodeInGroup("player") as PlayerController;
```

---

## Raspberry Pi 5 -muistutukset

- Älä lisää tuhansia erillisiä 3D-propseja — käytä `MultiMesh` tai pienennä lukumäärää
- Vältä reaaliaikaisia varjoja ja raskaita shader-efektejä
- Arcade-proppit kerroksella 5 (bitmask 16) jotta ne eivät hidasta fysiikkalaskentaa
- Testaa ARM64-buildillä ennen julkaisua

---

## Changelog (tänään 9.4.2026)

Katso `docs/DEVLOG_2026-04-09.md` täydellisestä kehityspäiväkirjasta.
