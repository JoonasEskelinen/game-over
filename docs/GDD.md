# 📋 Game Design Document (GDD)

**Projekti:** Game Over  
**Versio:** 0.2 — Päivitetty asiakaspalaverin jälkeen  / 0.3 päivitetty uusien ideoiden myötä
**Päivämäärä:** 2026  
**Tekijä:** Joonas Eskelinen  
**Asiakas:** Pelihuone GameOver  
**GitHub:** https://github.com/JoonasEskelinen/game-over

---

## 1. Visio

### 1.1 Konsepti

> "2.5D action-platformer, jossa pelataan Game Over -hahmolla. Tyyli yhdistää Little Nightmares -tunnelman ja Teenage Mutant Ninja Turtles The Arcade Gamen toiminnan — sivulta kuvattu, värikäs ja hauska, mutta visuaalisesti rikas."

### 1.2 Genre
- 2.5D sivulta kuvattu action-platformer
- Liike myös osittain pystysuunnassa (ei pelkästään vaaka)

### 1.3 Kohderyhmä
- Peliluolan asiakkaat (kaikenikäiset)
- Retropelien nostalgikot
- Steam-pelaajat

### 1.4 Tunnelma & Tyyli
- **Visuaalinen tyyli:** 2.5D — 3D-maailma + pikselitaide-inspiroitu estetiikka, dynaaminen valaistus
- **Tunnelma:** Hauska, seikkailullinen, värikäs — Little Nightmares visuaalisuus + TMNT toiminta
- **Vertailupisteet:** Little Nightmares, Teenage Mutant Ninja Turtles The Arcade Game, Super Mario Bros.

---

## 2. Pelimekanikat

### 2.1 Pelaajahahmo — Game Over

**Perusliikkuminen (PS5 DualSense):**

| Toiminto | Painike |
|---|---|
| Liiku | Vasen tatti |
| Hyppy | ✕ (Cross) — yksinkertainen + ketjuhyppy |
| Miekka (perusase) | R2 |
| Kilpi (puolustus) | L2 |
| aktivoi eri tilat (normaali/miekka+kilpi/joystick erikoisaseille) | kolmio |
| Erikoisaseen ohjaus | Joystick aktivoituna |

**Taistelusysteemi:**
- **Perusase:** Miekka + kilpi — lähitaistelu ja puolustus
- **Erikoisasejärjestelmä:** Painamalla ○ hahmo ottaa käteen "joystickin", jolla ohjataan erikoisaseita (esim. drone). Erikoisaseen painikkeet aktivoituvat joystickin kanssa.

**Co-op erikoisase (ehkä myöhemmin):**
- Toinen pelaaja puolustaa kilpivihollisista erikoisaseen aktivoinnin aikana
- Yksinpelissä: kilpi nousee automaattisesti tietyksi ajaksi erikoisaseen käytön aikana (tarkennetaan)

**Elinvoima:**
- Healthia kuluu osumista — ei kuolema yhdestä osumasta
- Tarkentuu: sydämet / healthbar (sovitaan myöhemmin)

### 2.2 Erikoisasejärjestelmä

Jokaisen maailman loppuvihollisen (boss) voittamisen jälkeen pelaaja saa uuden erikoisaseen.

| Erikoisase | Saadaan | Käyttö |
|---|---|---|
| Drone | Boss 1 | Lentää ja hyökkää etäältä joystickin ohjaamana |
| [Selventyy] | Boss 2 | [Täytetään myöhemmin] |
| [Selventyy] | Boss 3 | [Täytetään myöhemmin] |

### 2.3 Viholliset

| Vihollinen | Käyttäytyminen | Tuhoaminen | Suojautuminen |
|---|---|---|---|
| Perusvihollinen | Patrolloi tasoa | Miekka / erikoisase | Kilpi |
| Lentävä vihollinen (drone) | Lentää pelaajaa kohti | Miekka hypyllä / erikoisase | Kilpi |
| Boss (per maailma) | Ainutlaatuinen pattern | Miekka + erikoisaseet | Vaihtelee |
| [Lisää myöhemmin] | | | |

### 2.4 Keräilyt & Power-upit

- [Täytetään myöhemmin asiakkaan kanssa]

### 2.5 Tasomekanikat

- Tarkistuspisteet (checkpoints) — lippu/maalilinja
- Vaara-alueet: viholliset
- Loppuvastus joka kentässä
- [Lisää selventyy kehityksen myötä]

---

## 3. Maailma & Tasot

### 3.1 Rakenne

```
Maailma 1: Metsä
  └── Taso 1-1: Tutoriaali (opitaan perusmekanikat: liike, hyppy, miekka, kilpi)
  └── Taso 1-2: Haaste kasvaa (uusia vihollisia)
  └── Taso 1-3: Bonus / salainen taso
  └── Taso 1-BOSS: Pomovihollinen → palkinto: 1. erikoisase (Drone)

Maailma 4: Metsä
  └── [Rakenne selventyy]

Maailma 3: Futuristinen pelimaailma
  └── [Rakenne selventyy]

Maailma 4: [Myöhemmin]
  └── [Rakenne selventyy]
```

> **Huom:** Maailmojen määrä ja tasojen lukumäärä tarkentuvat kehityksen myötä.

### 3.2 Visuaaliset teemat

| Maailma | Teema | Väripaletti | Erikoisuus |
|---|---|---|---|
| 2 | Metsä | Vihreä, kulta | Parallax puustot, luonnonvalo |
| 3 | Futuristinen pelimaailma | Neon, tumma | Sci-fi elementit, hologrammit |
| 4 | Luola | Tumma, sininen | Kimaltelevat kristallit, pistemäinen valo |
| 5 | [Myöhemmin] | | |

---

## 4. Äänisuunnittelu

### 4.1 Musiikki
- Tyyli: Chiptune + modernit instrumentit (kuten Sea of Stars)
- Jokainen maailma: oma teema
- Boss-taistelu: intensiivisempi versio maailman teemasta

### 4.2 Ääniefektit
- Hyppy, miekkalyönti, kilven parry, vahinko, kuolema
- Erikoisaseen aktivointi ja drone-äänet
- Ympäristöäänet (linnut, tuuli, vesi, luola-kaiku)
- PS5 DualSense haptinen palaute: hyppyihin, osumiin, erikoisaseen aktivointiin (ehkä)

---

## 5. UI / HUD

### 5.1 Pelin aikana (HUD)
- Elinvoima / healthbar (ylävasen)
- Tason nimi / numero (keskellä ylhäällä)
- Aika (valinnainen, keskellä ylhäällä)
- Aktiivinen erikoisase ja sen status (alaosa)

### 5.2 Valikot
- **Päävalikko:** Aloita, Jatka, Asetukset, Lopeta
- **Kenttävalikko:** Oma minimaailma — ei perinteinen lista vaan visuaalinen kartta
- **Asetukset:** Äänenvoimakkuus, CRT-shader on/off, ohjainasetus
- **Tason läpäisy / Game Over -näyttö**

---

## 6. Tekniset vaatimukset

### 6.1 Kohdealustat

| Alusta | Tavoite FPS | Huomio |
|---|---|---|
| Raspberry Pi 5 (8GB) | 60 FPS | ARM64 Linux export, optimointi tärkeää |
| Windows (Steam) | 60 FPS | Pääasiallinen julkaisualusta |
| Linux (Steam) | 60 FPS | Sama buildi kuin Pi |

### 6.2 Pelimoottori & Teknologia
- **Godot 4.x (.NET / C#)**
- 2.5D: 3D-maailma + sivukamera kiinteällä Z-akselilla
- Dynaaminen valaistus (Forward+ tai Compatibility Pi:llä)
- GodotSteam -plugin Steam-integraatioon

### 6.3 Renderöinti
- Sisäinen resoluutio: [tarkentuu — 320×180 retro tai korkeampi 2.5D:lle]
- CRT-shader valinnainen päälle/pois asetuksista
- Raspberry Pi: Compatibility-renderöijä suorituskyvyn varmistamiseksi

### 6.4 Ohjaimet
- PS5 DualSense — täysi tuki
- Näppäimistö (kehityskäyttö)
- DualSense haptinen palaute: ehkä (tutkitaan)

---

## 7. Projektin aikataulu

| Vaihe | Sisältö | Viikot |
|---|---|---|
| Pre-production | GDD, palaveri, projektirakenne, GitHub | 1–2 |
| Prototype | Pelaajaliike, miekka/kilpi, yksi testikenttä | 3–4 |
| Alpha | Maailma 1 pelattavissa, boss, drone-erikoisase | 5–8 |
| Beta | Kaikki sisältö, co-op, bugitestaus | 9–10 |
| Release | Steam + Pi -buildit, dokumentaatio viimeistely | 10 |

---

## 8. Avoimet kysymykset

- [x] Hahmon nimi → **Game Over**
- [ ] Hahmon tarkempi ulkonäkö ja taustatarina?
- [ ] Juoni / narratiivi — onko tarinaa vai pelkkiä tasoja?
- [x] Boss-vihollisia → **Kyllä, yksi per maailma**
- [x] Co-op → **Ehkä myöhemmin**
- [ ] Maailmojen lopullinen määrä?
- [ ] Keräilyt ja power-upit — mitä haluaa peliin?
- [x] DualSense haptinen palaute → **Ehkä, tutkitaan**
- [ ] Erikoisaseiden tarkemmat ideat (boss 2, boss 3...)?
- [ ] Kenttävalikon minimaailman visuaalinen idea tarkemmin?
