# Game Over — projektin esittely (docs)

**Huom:** Tämä kuvaus päivitetään sitä mukaa kun peli ja rakenne elävät. Tarkin pelisuunnitelma: [GDD.md](GDD.md).

---

## Yleiskatsaus

- **Pelimoottori:** Godot 4.x (projektissa määritelty esim. 4.6), **C# / .NET** (`config/features` sisältää C#).
- **Genre / toteutus:** 2.5D-tyylinen **3D**-action (sivulta kuvattu liike, `CharacterBody3D`, kamera seuraa pelaajaa).
- **Päähahmo:** Game Over — Mixamo-animaatiot (FBX), miekka, kilpi, hyppy, tarttuminen (mm. arcade-kentän fysiikkapropit).
- **Kohdealusta:** PC-kehitys; **Raspberry Pi 5 (8 GB)** min-spec / peliluolatavoite (ks. projektin `.cursor/rules` ja alla).

---

## Vaatimukset ja käynnistys

1. [Godot 4.x](https://godotengine.org/download/) **.NET**-build (C#-tuki).
2. [.NET SDK](https://dotnet.microsoft.com/download), joka vastaa Godotin odotuksia.
3. Kloonaa repo ja avaa **`project.godot`** Godotissa → **Run** (F5).


**C#-käännös (CI tai terminaali):**

```bash
dotnet build GameOver.csproj
```

Polku repojuuresta: `GameOver.csproj`.

---

## Ohjaimet (lyhyt)

Projektin **Input Map** määrittelee mm. PS5 DualSense -akselit ja näppäimet. Tärkeitä faktoja (tarkista `project.godot`):

| Toiminto | Esimerkki (ohjain / näppäin) |
|----------|------------------------------|
| Liike XZ | Vasen tatti |
| Hyppy | Ristinäppäin (Cross) |
| Kilpi | L2 (axis `block`) |
| Hyökkäys / tatti | R2 (`attack`), R1 (`attack_r1`) |
| Tarttuminen | Kolmio / E (`grab`) |
| Aseistila | Kolmio (`toggle_weapon`) |
| Kamera | Oikea tatti (`cam_look_*`) |

Tarkat bindaukset elävät editorissa — älä luota vain tähän taulukkoon jos inputtia muutetaan.

---

## Hakemistorakenne

```
/
├── assets/
│   ├── audio/           # musiikki, SFX (.mp3 / importit)
│   └── models/          # FBX/GLB: pelaaja-animaatiot, susi, boss, arcade, luonto…
├── scenes/
│   ├── characters/      # player.tscn, gameover_character.tscn
│   ├── enemies/       # EnemyLevel1, BossLevel1 (+ playtest)
│   ├── levels/        # level_1, test_level, World1/*, World2/*, spawnerit
│   └── ui/            # main_menu, hud, game_over
├── scripts/             # C#: pelaaja, viholliset, kamera, tasologiikka, HUD
├── docs/                # GDD, arkkitehtuuri, changelog, tämä tiedosto
├── addons/              # esim. Mixamo-retarget -työkalu
└── project.godot
```

Yksityiskohtainen tekninen jako: [ARCHITECTURE.md](ARCHITECTURE.md).

---

## Muu dokumentaatio

| Tiedosto | Sisältö |
|----------|---------|
| [GDD.md](GDD.md) | Pelisuunnitelma, mekaniikat, maailmat |
| [ARCHITECTURE.md](ARCHITECTURE.md) | Tekninen rakenne, skriptit, tasot |
| [CONTRIBUTING.md](CONTRIBUTING.md) | Git, commitit, PR:t (päivitä C#-tyyli tarvittaessa) |
| [CHANGELOG.md](CHANGELOG.md) | Julkaisumuistiinpanot |

---

## Lisensointi

Katso [LICENSE](LICENSE)
