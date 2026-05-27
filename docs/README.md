# Game Over — dokumentaatio

Tämä kuvaa **nykyistä toteutusta** repossa. Alkuperäinen pelisuunnitelma ja visio on [GDD.md](GDD.md) — sitä en päivitä automaattisesti koodin mukana.

---

## Yleiskatsaus

- **Pelimoottori:** Godot **4.6**, **C# / .NET 8** (`GameOver.csproj`, Godot.NET.Sdk 4.6.1).
- **Renderöinti:** Forward Plus (PC-kehitys); Pi-julkaisussa suositus Compatibility-renderöijää (ks. [RASPBERRY_PI5_KOTIKONSOLI.md](RASPBERRY_PI5_KOTIKONSOLI.md)).
- **Fysiikka:** Jolt Physics.
- **Genre / toteutus:** 2.5D-tyylinen **3D**-action (sivulta kuvattu liike, `CharacterBody3D`, kamera seuraa pelaajaa).
- **Päähahmo:** Game Over — Mixamo-animaatiot (FBX), miekka + kilpi, hyppy, tarttuminen, kenttäkohtaiset erikoisaseet.
- **Kohdealusta:** PC-kehitys; **Raspberry Pi 5 (8 GB)** min-spec / peliluolatavoite (ks. [RASPBERRY_PI5_KOTIKONSOLI.md](RASPBERRY_PI5_KOTIKONSOLI.md)).

---

## Vaatimukset ja käynnistys

1. [Godot 4.6](https://godotengine.org/download/) **.NET**-build (C#-tuki).
2. [.NET SDK 8](https://dotnet.microsoft.com/download).
3. Kloonaa repo ja avaa **`project.godot`** Godotissa → **Run** (F5).

**C#-käännös (CI tai terminaali):**

```bash
dotnet build GameOver.csproj
```

---

## Pelikulku (nykyinen)

```
main_menu → loading_screen → level_1 → level_2 → level_3
                                    ↘ game_over (elämät loppu)
```

| Kenttä | Teema (lyhyesti) | Boss / palkinto |
|--------|------------------|-----------------|
| **Level 1** | Arcade / metsä | BossLevel1 → joystick (`GameState.HasJoystick`) |
| **Level 2** | Luola / putket | BossLevel2 → kissa-erikoisase (joystick-konteksti) |
| **Level 3** | Ylämäkitie + liikenneympyrä | BossLevel3 tornissa |

Lataus: `GameState.BeginSceneLoad()` + autoload `LoadingOverlay` + `loading_screen.tscn`.

---

## Ohjaimet (lyhyt)

Projektin **Input Map** määrittelee PS5 DualSense -akselit ja näppäimet. Kehityksessä (debug-build) PC-näppäimet rekisteröidään `GameState.RegisterDevelopmentPcCombatKeys()` -kautta.

| Toiminto | Ohjain | Debug-näppäin (vain debug-build) |
|----------|--------|----------------------------------|
| Liike XZ | Vasen tatti | WASD |
| Hyppy | ✕ (Cross) | — |
| Kilpi | L2 (`block`) | P |
| Miekka R2 / R1 | R2 (`attack`) / R1 (`attack_r1`) | O / — |
| Tarttuminen | ☐ (Square) / E (`grab`) | `[` |
| Aseistila / drone (L1) | △ (`toggle_weapon`) | U |
| Istu (erikoisase) | ○ (`sit`) | I |
| Kamera | Oikea tatti (`cam_look_*`) | — |

**Huom:** Level 1:ssä, kun `GameState.HasJoystick == true`, kolmio aktivoi **drone-tilan** istumalla; muilla kentillä kolmio vaihtaa miekka+kilpi / normaali -tilaa.

Tarkat bindaukset: `project.godot` → `[input]`.

---

## Hakemistorakenne (korkea taso)

```
/
├── assets/
│   ├── audio/           # musiikki, SFX
│   ├── models/        # FBX/GLB: pelaaja, susi, lisko, käärme, bossit, arcade, luonto…
│   ├── materials/     # materiaalit
│   ├── shaders/       # esim. CRT
│   └── textures/
├── scenes/
│   ├── characters/    # player.tscn, gameover_character.tscn
│   ├── enemies/       # EnemyLevel1–3, BossLevel1–3 (+ playtest)
│   ├── hazards/       # level2 kattohämähäkki, level3 kivi, drone-pommi
│   ├── levels/        # level_1, level_2, level_3, test_level
│   └── ui/            # main_menu, hud, game_over, loading_screen
├── scripts/           # C#: pelaaja, viholliset, kamera, tasologiikka, HUD, GameState
├── docs/                # dokumentaatio (tämä kansio)
├── addons/              # Mixamo-retarget -työkalu
└── project.godot
```

Yksityiskohtainen tekninen jako: [ARCHITECTURE.md](ARCHITECTURE.md).

---

## Muu dokumentaatio

| Tiedosto | Sisältö |
|----------|---------|
| [GDD.md](GDD.md) | Alkuperäinen pelisuunnitelma (visio, ei seuraa toteutusta 1:1) |
| [ARCHITECTURE.md](ARCHITECTURE.md) | Tekninen rakenne, skriptit, tasot, autoloadit |
| [LEVEL3_GUIDE.md](LEVEL3_GUIDE.md) | Level 3 -kentän rakenne ja jatkokehitys |
| [RASPBERRY_PI5_KOTIKONSOLI.md](RASPBERRY_PI5_KOTIKONSOLI.md) | Pi 5 -käyttöönotto, export, kiosk |
| [CHANGELOG.md](CHANGELOG.md) | Julkaisumuistiinpanot |

---

## Lisensointi

Lisenssi: [LICENSE](LICENSE) (proprietary — Pelihuone GameOver).
