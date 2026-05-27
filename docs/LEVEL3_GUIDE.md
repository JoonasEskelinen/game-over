# Level 3 — Rakennusopas ja nykytila

Konsepti: ylämäki **autotie** pääreittinä, **metsä** tien sivuilla, tien päässä **iso liikenneympyrän tasanne** (litteä pelattava alue).

Tekninen tausta: [ARCHITECTURE.md](ARCHITECTURE.md).

---

## Nykytila repossa (2026-05)

Kenttä on **pelattavassa vaiheessa** — ei enää stub. Juuri-node: **`Level3`** (`scenes/levels/level_3.tscn`).

| Kohde | Tila |
|-------|------|
| `level_3.tscn` | Rakennettu: tie, ympyrä, pysäköintialue, metsä (manuaaliset puut), valaistus |
| `Level3UphillRoadSurface.cs` | Tiepinnan apu (jos käytössä rampissa) |
| `Level3GroundAlign.cs` | Pelaajan / maan kohdistus (`Level3MaaKohdistus`) |
| `Level3RoadCenterDashes.cs` | Keskiviivan katkoviivat |
| `Level3DeckParkingMarkings.cs` | Pysäköintimerkinnät ympyrällä |
| `Level3SpecialDrone.cs` | Erikoisase: istu + joystick → drone-pommit |
| `EnemyLevel3Spawner.cs` | Käärmeviholliset |
| `Level3RollingRockSpawner.cs` | Vierivät kivet |
| `boss_level_3.tscn` | Boss tornissa |
| `LevelMusicPlayer` | `level3_bgm.ogg` |
| **Level exit** | **Puuttuu** — ei siirtymää seuraavaan kenttään |
| `ForestScatter.cs` | **Ei kytketty** L3:een; puut on asetettu käsin `World`-alle |

**Scene-kulku:** `level_2` → `level_3` (`Level2BossExit`). L3 on tällä hetkellä kampanjan viimeinen pelattava kenttä.

---

## Visuaalinen brief (mittasuhteet)

1. **Tie:** pääakseli kohti ympyrää kaltevuudella (`World/UphillRoadRoot/UphillRoad` — CSG-rampi).
2. **Metsä:** puut tien ulko- ja taustapuolella (`ForestEmbankment`, `CameraEmbankment`, manuaaliset `CommonTree_*` -instanssit).
3. **Liikenneympyrä:** `RoundaboutDeck` (CSG-sylinteri) + rengasmurto (`PlateauRingWalls`) + pysäköintipaikat (`ParkingLot`).

---

## Node-rakenne (yhteenveto)

```
Level3 (Node3D)
├── Player                    ← scenes/characters/player.tscn
├── Camera3D                  ← CameraFollow.cs (drone-follow -polku L3:ssa)
├── HUD                       ← scenes/ui/hud.tscn
├── LevelMusic                ← LevelMusicPlayer.cs
├── Level3SpecialDrone        ← erikoisase-node
├── Level3MaaKohdistus        ← Level3GroundAlign.cs
├── EnemyLevel3Spawner
├── Level3RollingRockSpawner
├── DirectionalLight3D
├── WorldEnvironment
└── World
    ├── UphillRoadRoot        ← tie, reunamuurit, katkoviiva, laatikko-props
    ├── RoundaboutDeck        ← ympyrän kansi
    ├── PlateauRingWalls      ← reunavallit
    ├── ParkingLot            ← pysäköintipaikat
    └── [puut, boss-torni, hazardit…]
```

---

## Pelilogiikka

| Järjestelmä | Kuvaus |
|-------------|--------|
| **Spawn** | Pelaaja alussa tien juuressa (asetettu scenessä) |
| **Viholliset** | `EnemyLevel3Spawner` — käärmeet |
| **Hazardit** | Vierivät kivet, drone-pommit (erikoisase) |
| **Boss** | `boss_level_3.tscn` tornissa |
| **Erikoisase** | `Level3SpecialDrone` — vaatii istumisen + joystick-kontekstin (sama malli kuin L2-kissa) |
| **Checkpoint** | Ei vielä kytketty; respawn käyttää oletusta |
| **Exit** | **Tekemättä** — lisää `LevelExit` tai oma skripti kun seuraava kenttä on olemassa |

---

## Jatkokehitys (checklist)

### Pakollinen ennen kampanjan jatkoa

- [ ] **Level exit** ympyrältä / bossin jälkeen → seuraava scene (kun se on olemassa)
- [ ] **Checkpoint** (valinnainen) — kopioin mallin L1/L2:sta jos haluan respawn-pisteen

### Visuaalinen / sisältö

- [ ] Viimeistelen boss-taistelun ja kamerakulmat
- [ ] Teen ympyrän keskiosan / kaistat visuaalisesti selkeämmiksi
- [ ] Lisään ääniefektit ja ympäristöäänet (tuuli, liikenne)

### Suorituskyky (Raspberry Pi 5)

- [ ] Testaan **linux-arm64** + **Compatibility**-renderöijän
- [ ] Puumäärä: nykyiset manuaaliset instanssit vs. `ForestScatter` preset (`TotalTrees` alhaisena)
- [ ] Varjot: DirectionalLight3D varjot pois/päälle Pi-testin mukaan
- [ ] Vältän raskaita FBX-propseja ilman instanssointia

---

## Viitteet

| Tiedosto | Miksi |
|----------|--------|
| [ARCHITECTURE.md](ARCHITECTURE.md) | Pelaaja, kamera, tasojen roolit |
| [level_3.tscn](../scenes/levels/level_3.tscn) | Kentän scene |
| [Level3SpecialDrone.cs](../scripts/levels/Level3SpecialDrone.cs) | Drone-erikoisase |
| [ForestScatter.cs](../scripts/levels/ForestScatter.cs) | Vaihtoehtoinen metsän generointi (ei käytössä L3:ssa) |
| [LevelExit.cs](../scripts/levels/LevelExit.cs) | Geneerinen exit-komponentti |
| [RASPBERRY_PI5_KOTIKONSOLI.md](RASPBERRY_PI5_KOTIKONSOLI.md) | Pi-export ja kiosk |

---

## Changelog

- **2026-05-27:** Päivitetty nykytilaan — kenttä rakennettu, exit puuttuu, ForestScatter ei käytössä.
- **2026-04-21:** Ensimmäinen versio Level 3 -oppaasta (tie + metsä + liikenneympyrä).
