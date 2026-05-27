# Muutosloki

Merkitsevät muutokset tähän tiedostoon.

Formaatti perustuu [Keep a Changelog](https://keepachangelog.com/en/1.0.0/) -standardiin.  
Versiointi noudattaa [Semantic Versioning](https://semver.org/) -periaatetta.

---

## [Unreleased]

### Lisätty
- **Level 1:** arcade-kenttä, susi-viholliset (`EnemyLevel1`), boss (`BossLevel1`), joystick-palkki (`JoystickLever`), drone-tila, arcade-fysiikka (`Level1ArcadePhysicsSetup`)
- **Level 2:** lisko-viholliset, kattohämähäkki, kissa-erikoisase (`Level2SpecialCat`), boss (`BossLevel2`), siirtymä L3:een
- **Level 3:** ylämäkitie + liikenneympyrä, käärme-viholliset, vierivät kivet, drone-pommi-erikoisase, boss tornissa
- **Pelaaja:** miekka R2/R1, kilpi, tarttuminen (`grabbable`), syvyysliike, istuminen
- **UI:** päävalikko, HUD (HP, elämät, R1-cooldown, boss-HP), latausruutu, game over
- **Autoloadit:** `GameState`, `LoadingOverlay`
- **Tallennus:** elämät + joystick-lippu (`user://savegame.cfg`)
- **Dokumentaatio:** ARCHITECTURE, LEVEL3_GUIDE, RASPBERRY_PI5_KOTIKONSOLI

### Muutettu
- R1-cleave: useampi vihollinen per swing (EnemyLevel1/2, max 2)
- Kilven torjunta: kamera- ja hahmosuunta (level_1 susi, bossit)

### Korjattu
- R1-miekan osuma useaan lähietäisyyden susiin (cleave-läheisyys + swing-tilan nollaus)

### Tunnettuja puutteita
- Level 3: ei exitiä seuraavaan kenttään
- `Checkpoint.cs` / `LevelExit.cs` / `ForestScatter.cs` eivät kaikissa kentissä kytketty
- Keskitetty Pi-laatupresetti puuttuu (säätö export-kentillä)

---

## [0.1.0] — 2025-XX-XX

### Lisätty
- Pelaajahahmon perusliike (kävely, hyppy)
- Prototyyppitaso (`test_level.tscn`)
- PS5-ohjaintuki (Input Map)

---

<!-- 
Tulevat muutokset kirjataan tähän formaatissa:

## [X.Y.Z] — YYYY-MM-DD

### Lisätty
- Uudet ominaisuudet

### Muutettu
- Muutokset olemassaoleviin ominaisuuksiin

### Korjattu
- Bugikorjaukset

### Poistettu
- Poistetut ominaisuudet
-->
