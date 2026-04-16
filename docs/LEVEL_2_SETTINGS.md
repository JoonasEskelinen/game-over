# Level 2 — kentän säätöjen ja riippuvuuksien kuvaus

Tämä dokumentti kokoaa yhteen **level_2** -scenen keskeiset asetukset: pelaaja, kamera, vihollisten spawner, bossi ja ympäristö. Tarkemmat logiikkakommentit ovat skripteissä `BossLevel2.cs` ja `EnemyLevel2Spawner.cs`.

**Pääscene:** `scenes/levels/level_2.tscn`  
**Kentän juuri:** `level_2` (Node3D)

---

## 1. Pelaaja (`Player`)

- **Instanssi:** `scenes/characters/player.tscn`
- **Sijainti levelissä (tyypillinen):** skaala 1.1, positio noin **X = -105**, **Y ≈ -0.074**, **Z = 0** (tarkista `.tscn`)

### Kenttäkohtaiset exportit (`level_2.tscn`)

| Asetus | Arvo (viite) | Selitys |
|--------|----------------|--------|
| `RunSpeed` | `6.0` | Juoksunopeus (normaalitilassa; miekka-tilassa kävely käyttää `Speed`-oletusta) |
| `DepthClampMin` | `-14.0` | Syvyysliikkeen alaraja (Z) |
| `DepthClampMax` | `14.0` | Syvyysliikkeen yläraja (Z) — luola-osio leveämpi kuin putki |
| `SwordHitReachExtraMeters` | `0.05` | R2-iskun terän “jatke” metreinä (kapeampi kuin oletus) |
| `HeavySwordHitReachExtraMeters` | `0.1` | R1-iskun terän jatke |
| `HeavyMeleeBladeArcHalfAngleDeg` | `42` | R1 teräkartion puolikulma (asteina) — vaikuttaa osumien rekisteröintiin |
| `LightMeleeProximityMax` | `0.85` | R2: max etäisyys iskulinjaan |
| `HeavyMeleeProximityMax` | `0.9` | R1: max etäisyys iskulinjaan |

**Huom:** Globaalit miekan oletukset määritellään `PlayerController.cs`:ssä; yllä olevat **ylikirjoittavat** vain tämän kentän instanssin.

---

## 2. Kamera (`Camera3D` + `CameraFollow.cs`)

- **Script:** `scripts/CameraFollow.cs`
- **PlayerPath:** `../Player`

### Kenttäkohtaiset exportit (`level_2.tscn`)

| Asetus | Arvo (viite) | Selitys |
|--------|----------------|--------|
| `FollowSpeed` | `12` | Seurannan nopeus (kun ei käytetä snap-tilaa) |
| `Offset` | `(0, 3.2, 24)` | Määrittää kameran etäisyyden juuren pituutena (`SideScrollerLock`:ssa käytetään pituutta) |
| `PivotHeight` | `1.2` | Katselupisteen korotus pelaajan juuresta |
| `MinPitchDeg` / `MaxPitchDeg` | `-55` / `55` | Pitch-rajat |
| `ClampCameraAboveGround` | `false` | Ei maahan clampattua pitchiä tässä kentässä |
| `MouseLookEnabled` | `false` | Ei hiiren orbit |
| `SideScrollerLock` | `true` | Kiinteä sivunäkymä (yaw/pitch asetuksista) |
| `SideScrollerPitchDeg` | `11` | Katselukulma (astetta) |
| `SideScrollerSkipWallRayAndSnap` | `true` | **Ei** seinä-ray’n lyhennystä (sama zoom) + täysi seuranta yhdellä blendillä (vakaa sivukulma) |
| `EnableBossDanceCamera` | `false` | Level 1 -bossin tanssikamera pois päältä |

**Huom:** `SideScrollerYawDeg` ei ole erikseen listattu level_2:ssa → käytetään skriptin oletusta (`0`), ellei sceneä muuteta.

---

## 3. EnemyLevel2Spawner (node: `EnemyLevel2Spawner`)

- **Script:** `scripts/enemies/EnemyLevel2Spawner.cs`
- **EnemyScene:** `enemy_level_2.tscn` (PackedScene)

### Oletus-exportit (skriptissä; voi ylikirjoittaa Inspectorissa)

| Asetus | Oletus | Selitys |
|--------|--------|---------|
| `FirstSpawnAtPlayerX` | `-90` | Ensimmäisen spawnin X-kynnys |
| `SpawnEveryPlayerXMeters` | `15` | Kuinka monta metriä X-etäisyyttä seuraavaan kynnykseen onnistuneen spawnin jälkeen |
| `MaxConcurrentEnemies` | `2` | Enintään näin monta elossa kerralla |
| `MinPlayerX` | `-108` | Spawner aktiivinen alkaen |
| `MaxPlayerX` | `168` | Spawner pysähtyy (ei spawneja bossin lähelle) |
| `SpawnAheadOfPlayer` | `10` | Vihollinen syntyy tämän verran pelaajan X:n edelle (+) |
| `SpawnAheadVariance` | `3.5` | Satunnainen ± metri `SpawnAheadOfPlayer`-arvoon |
| `SpawnZHalfRange` | `2.8` | Satunnainen Z ∈ [-2.8, 2.8] |
| `SpawnY` | `-0.07` | Spawn-korkeus |

**Riippuvuus:** `EnemyLevel2` lisää ryhmän `enemy_level2` ja toteuttaa `IsAliveForSpawner()`.

---

## 4. BossLevel2 (node: `BossLevel2`)

- **Scene:** `scenes/enemies/boss_level_2.tscn`
- **Script:** `scripts/enemies/BossLevel2.cs`

### Sijainti kentässä (`level_2.tscn`)

| Kenttä | Arvo (viite) |
|--------|----------------|
| Position | noin **X = 175**, **Y ≈ -0.074**, **Z = 0** (kentän loppu, putken pääty) |

### Tärkeimmät skriptin exportit (Inspector / oletukset)

- **HP:** `Health` (oletus 10)
- **FBX-polut:** `IdleAnimPath`, `HammerAnimPath`, `DeathAnimPath`
- **Miekan osumat bossiin:** `SwordHitProbeHeights`, `SwordHitActivationTime`
- **Vasara:** `HammerIntervalMin/Max`, `HammerHitRangePlanar`, `HammerHitHeightMax`, `HammerHPDamage`, `HammerHitPhaseMin/Max`
- **Kuolema:** `DeathShrinkDuration`
- **Suunta:** `FaceYawOffsetDegrees`
- **Vasaran luu:** `HammerHandBoneNameOverride` (tyhjä = automaattihaku)

**Ryhmä:** `level2_boss` — HUD voi näyttää boss-HP:n (`HUDController.cs`).

---

## 5. HUD

- **Instanssi:** `scenes/ui/hud.tscn`
- Bossin HP-palkki reagoi ryhmään `level2_boss` (sama logiikka kuin level 1 -bossille omalla ryhmällään).

---

## 6. Vihollinen EnemyLevel2 (ei suoraan level-scenessä)

- **PackedScene:** `scenes/enemies/enemy_level_2.tscn`
- **Script:** `scenes/enemies/EnemyLevel2.cs` (tai projektin polun mukaan)

Tyypilliset säätöideat: `Health` (2 HP → R1 yksi osuma, R2 kaksi), `Speed`, `AttackRange`, animaatiopolut, `FaceYawOffsetDegrees`.

---

## 7. Ympäristö ja valaistus

- **WorldEnvironment:** `Environment`-resurssi (tausta, ambient)
- **Sun:** DirectionalLight3D (suunta ja varjot kentän mukaan)

Geometria (putket, luola, sillat) on määritelty `level_2.tscn`:ssä suurina staattisina kappaleina — muutokset suoraan scene-editorissa tai `.tscn`-tekstinä.

---

## 8. Nopea tiedostohakemisto

| Tiedosto | Sisältö |
|----------|---------|
| `scenes/levels/level_2.tscn` | Kenttä, pelaaja, kamera, spawner, boss, HUD |
| `scripts/enemies/BossLevel2.cs` | Bossin logiikka, animaatiot, vasara, osumat |
| `scripts/enemies/EnemyLevel2Spawner.cs` | Dynaaminen spawn X-kynnyksillä |
| `scripts/CameraFollow.cs` | Seuranta, side-scroller, seinä-ray (pois level 2:ssa kun Snap käytössä) |
| `scripts/player/PlayerController.cs` | Pelaajan miekan osumat, blokki, vahingot |
| `scripts/HUDController.cs` | Boss-HP, heavy-bar, pelaajan HP |

---

