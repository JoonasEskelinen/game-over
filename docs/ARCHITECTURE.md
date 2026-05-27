# Tekninen arkkitehtuuridokumentti — Game Over

**Päivitetty:** 2026-05  
**Pelimoottori:** Godot 4.6, **C# / .NET 8**  
**Renderöinti:** Forward Plus (`project.godot`)  
**Fysiikka 3D:** Jolt Physics

Tekninen kuvaus reposta — päivitän tätä kun rakenne tai vastuut muuttuvat merkittävästi.

Alkuperäinen visio: [GDD.md](GDD.md). Toteutus on 3D + C#, ei 2D SubViewport -putki.

---

## 1. Sovelluksen käynnistys

| Asetus | Arvo |
|--------|------|
| **Main scene** | `scenes/ui/main_menu.tscn` |
| **Autoload: GameState** | `scripts/GameState.cs` — joystick-lippu, scene-lataus, debug-näppäimet |
| **Autoload: LoadingOverlay** | `scripts/ui/LoadingOverlay.cs` — spinner kenttävaihdoissa |

**Scene-kulku:** `main_menu` → `loading_screen` → kampanjakentät → `game_over` (elämät 0).

---

## 2. Hakemistot ja roolit

| Polku | Rooli |
|-------|--------|
| `scripts/player/` | `PlayerController`, `HealthComponent`, tangenttikorjaukset |
| `scripts/enemies/` | `BossLevel1`–`BossLevel3`, spawnerit, playtest-kamera |
| `scripts/levels/` | Boss-directorit, arcade-fysiikka, scatter, exit, musiikki, L2/L3 erikoisaseet |
| `scripts/hazards/` | Kattohämähäkki (L2), vierivä kivi ja drone-pommi (L3) |
| `scripts/ui/` | `LoadingOverlay`, `HudJoystickPreview` |
| `scripts/` (juuri) | `CameraFollow`, `GameState`, `HUDController`, `LeverTrigger` |
| `scripts/util/` | Mesh-apuskriptit |
| `scenes/enemies/` | Vihollisten scenet; `EnemyLevel1.cs` scenen vieressä, muut vastaavasti |
| `scenes/levels/` | Kentät + `EnemySpawner.cs`, `JoystickLever.cs` |
| `scenes/ui/` | Valikot, HUD, latausruutu |
| `assets/models/` | Mixamo/FBX, bossit, viholliset, arcade, luonto |

---

## 3. Pelaaja

**Scene:** `scenes/characters/player.tscn`

- **`CharacterBody3D`** + `PlayerController.cs` — ryhmä **`player`**
- **`HealthComponent`** — HP ja elämät (`user://savegame.cfg`)
- **`gameover_character`** (PackedScene) — mesh + Mixamo-animaatiot

**Keskeiset mekaniikat (`PlayerController.cs`):**

| Ominaisuus | Kuvaus |
|------------|--------|
| Liike | XZ + valinnainen syvyysakseli (`SyvyysliikeKäytössä`) |
| R2 | Kevyt miekan isku, vahinko 1 |
| R1 | Raskas isku, vahinko 3, cooldown-HUD, cleave (max 2 kohdetta / swing) |
| Kilpi | L2, vain miekka+kilpi -tilassa; `IsBlockingEffectiveAgainst()` |
| Asemodet | `Normal` ↔ `SwordShield` (`toggle_weapon`) |
| Tarttuminen | `grab` → lähin `grabbable`-ryhmän `RigidBody3D` |
| Drone (L1) | Kun `HasJoystick` + level_1: kolmio → istu + drone-konteksti |
| Respawn | `Checkpoint.LastPosition` (oletus jos checkpointia ei ole) |

---

## 4. Kamera

**`CameraFollow.cs`** — seuraa pelaajaa; boss- ja drone-tilanteissa erikoisasetuksia (exportit + kenttäkohtainen wiring level_3:ssa).

---

## 5. Viholliset, bossit ja hazardit

| Tyyppi | Scene | Skripti |
|--------|-------|---------|
| EnemyLevel1 (susi) | `EnemyLevel1.tscn` | `scenes/enemies/EnemyLevel1.cs` |
| EnemyLevel2 (lisko) | `enemy_level_2.tscn` | `scenes/enemies/EnemyLevel2.cs` |
| EnemyLevel3 (käärme) | `Enemy_Level3.tscn` | `scenes/enemies/EnemyLevel3.cs` |
| BossLevel1 | `BossLevel1.tscn` | `scripts/enemies/BossLevel1.cs` — ryhmä **`level1_boss`** |
| BossLevel2 | `boss_level_2.tscn` | `scripts/enemies/BossLevel2.cs` |
| BossLevel3 | `boss_level_3.tscn` | `scripts/enemies/BossLevel3.cs` |
| Level2CeilingSpider | `level2_ceiling_spider.tscn` | `scripts/hazards/Level2CeilingSpider.cs` |
| RollingRockLevel3 | `RollingRockLevel3.tscn` | `scripts/hazards/RollingRockLevel3.cs` |
| Level3DroneBomb | `Level3DroneBomb.tscn` | `scripts/hazards/Level3DroneBomb.cs` |

**Spawnerit:**

- L1: `scenes/levels/EnemySpawner.cs` (aktivoituu `JoystickLever`-palkista)
- L2: `EnemyLevel2Spawner.cs`, `Level2CeilingSpiderSpawner.cs`
- L3: `EnemyLevel3Spawner.cs`, `Level3RollingRockSpawner.cs`

`EnemyBasic.cs` + `ArenaWaveDirector.cs` ovat repossa mutta **eivät ole kytketty** mihinkään kenttään.

---

## 6. Tasot

| Scene | Juuri | Keskeiset skriptit |
|-------|-------|-------------------|
| `level_1.tscn` | `Level 1` | `Level1ArcadePhysicsSetup`, `Level1BossDirector`, `EnemySpawner`, `JoystickLever`, `Level1ExitHole` → L2, `LevelMusicPlayer` |
| `level_2.tscn` | `level_2` | `EnemyLevel2Spawner`, `Level2CeilingSpiderSpawner`, `Level2SpecialCat`, `Level2CrateKokis`, `Level2BossExit` → L3, `LevelMusicPlayer` |
| `level_3.tscn` | `Level3` | `Level3SpecialDrone`, `Level3GroundAlign`, `Level3RoadCenterDashes`, `Level3DeckParkingMarkings`, spawnerit, `LevelMusicPlayer` |
| `test_level.tscn` | `testnode` | Vain `CameraFollow` + peruslattia |

**Eteneminen:**

- L1 → L2: `Level1ExitHole` (reikä lattiaan bossin jälkeen)
- L2 → L3: `Level2BossExit` (seinäreikä; kissa joystick-kontekstissa)
- L3: **ei vielä exitiä** seuraavaan kenttään

**Apuskriptit (ei kaikissa kentissä käytössä):**

- `Checkpoint.cs` — ei kytketty `.tscn`:iin; pelaaja lukee silti `LastPosition`-staattisen
- `LevelExit.cs` — geneerinen exit; oletus `NextScene` vanhentunut, ei käytössä
- `KillZone.cs` — kuolema-alue (jos kentällä instanssoitu)
- `ForestScatter.cs` — proseduraalinen metsä; **ei instanssoitu** nykyisiin kenttiin (L3 käyttää manuaalisia puita)

---

## 7. UI

| Scene | Skripti | Tehtävä |
|-------|---------|---------|
| `main_menu.tscn` | `MainMenu.cs` | Uusi peli, ohjeet, poistu; gamepad-navigaatio |
| `loading_screen.tscn` | `LoadingScreen.cs` | Threaded load `GameState.PendingLoadScenePath` |
| `hud.tscn` | `HUDController.cs` | HP, elämät, R1-cooldown, boss-HP, drone-pommilataus |
| `game_over.tscn` | `GameOver.cs` | Game over -overlay, uusi peli / päävalikko |

---

## 8. GameState ja tallennus

**`GameState`** (autoload):

- `HasJoystick` — tallennetaan `user://savegame.cfg` (`progress/has_joystick`)
- `BeginSceneLoad(path)` — asettaa ladattavan kentän + näyttää overlayn

**`HealthComponent`:** elämät samaan `savegame.cfg`-tiedostoon.

---

## 9. Input

Lähde totuus: `project.godot` → `[input]`. C# käyttää `Input.IsActionPressed("attack")` jne.

Tärkeimmät actionit: `move_*`, `jump`, `block`, `attack`, `attack_r1`, `grab`, `toggle_weapon`, `sit`, `cam_look_*`.

---

## 10. Erikoisaseet (kenttäkohtaiset)

| Kenttä | Skripti | Toiminta |
|--------|---------|----------|
| Level 1 | `JoystickLever` + drone | Joystick-palkki → `HasJoystick`; istu + drone-konteksti |
| Level 2 | `Level2SpecialCat.cs` | Istu + joystick; R2 vs kattohämähäkki |
| Level 3 | `Level3SpecialDrone.cs` | Istu + joystick; R2 pudottaa `Level3DroneBomb` |

---

## 11. Suorituskyky ja Raspberry Pi 5

Pi 5 (8 GB) on min-spec / peliluolatavoite — oletan heikkoa integroitua GPU:ta ja ei työpöytäluokan CPU:ta.

- Julkaisu: **linux-arm64** -export, testaan buildin oikealla laitteella ennen käyttöönottoa.
- Grafiikka: vältän tuhansia erillisiä draw calleja; suosin vähemmän instansseja, MultiMeshia tai LOD:ia. Pi-buildissä kevennän varjoja ja valaistusta.
- **Ei vielä keskitettyä `PiLow`-presettiä** — säädän Inspector-exporteilla (`ForestScatter.TotalTrees`, arcade-kerrokset, varjot). Uudet raskaat efektit vain optioina tai presetin takana.
- Käyttöönotto Pi:llä: [RASPBERRY_PI5_KOTIKONSOLI.md](RASPBERRY_PI5_KOTIKONSOLI.md).

---

## 12. Riippuvuudet

- **C#:** `GameOver.csproj` → `net8.0`, Godot.NET.Sdk 4.6.1
- **Addons:** `addons/Godot-Mixamo-Animation-Retargeter-main`

---

## 13. Liitteet

- [README.md](README.md) — käynnistys ja kansiorakenne
- [GDD.md](GDD.md) — alkuperäinen suunnitelma
- [LEVEL3_GUIDE.md](LEVEL3_GUIDE.md) — Level 3 -kentän opas
- [RASPBERRY_PI5_KOTIKONSOLI.md](RASPBERRY_PI5_KOTIKONSOLI.md) — Pi-käyttöönotto
- [CHANGELOG.md](CHANGELOG.md) — versiohistoria
