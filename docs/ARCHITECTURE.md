# Tekninen arkkitehtuuridokumentti — Game Over

**Versio:** elää repossa (päivitä tämä otsikko tai git-tag merkittävissä välietapeissa)  
**Pelimoottori:** Godot 4.x, **C# / .NET**  
**Renderöinti:** Forward Plus (`project.godot`)  
**Fysiikka 3D:** Jolt Physics

Tämä dokumentti kuvaa **nykytilan**. Kun skenejä tai vastuita siirretään, päivitä vastaavat kohdat.

---

## 1. Sovelluksen käynnistys

- **`run/main_scene`:** `scenes/ui/main_menu.tscn`
- **Autoloadit:** ei määritelty `project.godot`-issa tällä hetkellä; pelitila ja äänet hoidetaan pääosin scene-puun nodeilla ja skripteillä.

---

## 2. Hakemistot ja roolit

| Polku | Rooli |
|-------|--------|
| `scripts/player/` | `PlayerController`, `HealthComponent`, tangenttikorjaukset |
| `scripts/enemies/` | `BossLevel1`, `EnemyBasic`, playtest-kamera |
| `scripts/levels/` | Boss-director, arcade-fysiikka, scatter, checkpoint, exit, kill zone, aalto-director |
| `scripts/` (juuri) | `CameraFollow`, `HUDController` |
| `scripts/util/` | Yleiset apuskriptit (esim. mesh) |
| `scenes/characters/` | Pelaajan `CharacterBody3D` + instanssi `gameover_character` |
| `scenes/enemies/` | Vihollisten scenet; osa logiikasta `EnemyLevel1.cs` scenen vieressä |
| `scenes/levels/` | Kentät, `EnemySpawner.cs`, testi- ja tuotantotason scenet |
| `scenes/ui/` | Valikot, HUD, game over |
| `assets/models/` | Mixamo/FBX, boss, susi, arcade-paketti, luonto |
| `assets/audio/` | Musiikki ja SFX |

---

## 3. Pelaaja (node-rakenne, käsite)

`scenes/characters/player.tscn`:

- **`CharacterBody3D`** (`PlayerController.cs`) — ryhmä **`player`** (haetaan koodissa `GetTree().GetFirstNodeInGroup("player")`).
- **`HealthComponent`** — elinvoima / vahinko.
- **`gameover_character`** (PackedScene) — varsinainen mesh + animaatiot (Mixamo).
- Törmäys: `CapsuleShape3D`; debug-mesh voi olla piilotettu.

Keskeiset vastuut `PlayerController.cs`: liike (myös syvyysakseli kun käytössä), hyppy, miekka (R2/R1), kilpi (L2), tarttuminen `grabbable`-ryhmään, asemodet, animaatioiden ohjaus.

---

## 4. Kamera

- **`CameraFollow.cs`** — seuraa pelaajaa; tukee mm. boss-tilanteisiin liittyviä blendejä / erikoistiloja (tarkista skriptin exportit ja kenttäkohtainen wiring).

---

## 5. Viholliset ja boss

- **`EnemyLevel1.cs`** / `EnemyLevel1.tscn` — tason vihollinen (esim. susi), purema, torjunta kilvellä (`PlayerController.IsBlockingEffectiveAgainst`).
- **`BossLevel1.cs`** — level 1 -boss: tanssi-/syöksyfaset, kontaktivahinko, miekan osumat, tanssivalo (SpotLight3D), musiikki; ryhmä **`level1_boss`**.
- **`EnemySpawner.cs`** — spawnauslogiikka (ryhmä **`enemy`** spawneille).

---

## 6. Tasot ja ohjaus

Esimerkkejä (nimet voivat laajentua):

| Scene / skripti | Tehtävä |
|-----------------|--------|
| `level_1.tscn` | Pääkenttä / arena-tyyppinen kooste (pelaaja, propsit, boss-setup) |
| `Level1BossDirector.cs` | Bossin ja kameran / draaman synkronointi |
| `Level1ArcadePhysicsSetup.cs` | Arcade-objektien fysiikka; `grabbable`-ryhmä |
| `ForestScatter.cs` | Luonnon propit (instanssit / suorituskyky — Pi-tavoite) |
| `Checkpoint.cs`, `LevelExit.cs`, `KillZone.cs` | Eteminen / kuolema |
| `ArenaWaveDirector.cs` | Aaltopohjainen logiikka (jos käytössä kentällä) |

Tarkka node-puu on kussakin `.tscn`-tiedostossa — älä kopioi vanhoja 2D-puita tästä dokumentista, vaan editori.

---

## 7. UI

- `main_menu.tscn` + `MainMenu.cs`
- `hud.tscn` + `HUDController.cs`
- `game_over.tscn` + `GameOver.cs`

---

## 8. Input

Kaikki määritellään Godotin **Project → Project Settings → Input Map** -kautta; lähde totuus on `project.godot` `[input]`-osio. C# käyttää `Input.GetAxis` / `Input.IsActionPressed` -tyylisiä kutsuja action-nimillä (`move_left`, `attack`, `block`, …).

---

## 9. Suorituskyky ja Raspberry Pi 5

- Tavoite: pelattavuus **heikolla integroidulla GPU:lla** (Pi 5).
- Käytännössä: vältä turhia draw calleja ja varjoja, LOD / presetit / scatter-luvut, testaa **ARM64**-build oikealla laitteella ennen julkaisua.
- Projektissa voi olla Cursor-sääntö `.cursor/rules/raspberry-pi5-target.mdc` — täydentää tätä dokumenttia.

---

## 10. Riippuvuudet ja työkalut

- **C#:** `GameOver.csproj`, assembly name `GameOver` (`project.godot` → `[dotnet]`).
- **Addons:** esim. `addons/Godot-Mixamo-Animation-Retargeter-main` — retarget / animaatioputki editorissa.

---

## 11. Historia vs. toteutus

Alkuperäinen suunnitelma voi sisältää 2D / HD-2D -elementtejä (GDD:n visio). **Nykyinen toteutus** on pääosin **3D-skenet + C#**. Vanhat kaaviot, jotka viittaavat vain `CharacterBody2D` / SubViewport-pikseliputkeen, eivät kuvaa tätä branchia — päivitä ne tähän dokumenttiin tai merkitse arkistoksi erikseen.

---

## 12. Liitteet

- [README.md](README.md) — käynnistys ja kansiorakenne  
- [GDD.md](GDD.md) — pelisuunnitelma  
- [CHANGELOG.md](CHANGELOG.md) — mitä muuttui versiosta toiseen  
