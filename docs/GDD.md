# 📋 Game Design Document (GDD)

**Projekti:** [Pelin Nimi]  
**Versio:** 0.1 — Draft  
**Päivämäärä:** 2025  
**Tekijä:** [Harjoittelijan nimi]  
**Asiakas:** [Asiakkaan nimi]

---

## 1. Visio

### 1.1 Konsepti

Lyhyt lausuma:
> "Mario-tyylinen seikkailupeli, jossa grafiikka olisi little nightmares / Teenage Mutant Ninja Turtles The Arcade Game tyylinen 2.5D peli, asiakkaan omalla hahmolla."
### 1.2 Genre
- Sivulta kuvattu platformer pixelitaide / 2.5D idea, jossa voi liikkua osittain myös pystysuunnassa.
- Yksittäispelaaja + co-op

### 1.3 Kohderyhmä
- Peliluokan asiakkaat (kaikenikäiset)
- Retropelien nostalgikot
- Steam-pelaajat

### 1.4 Tunnelma & Tyyli
- **Visuaalinen tyyli:** HD-2D / 2.5D — pikselitaide / + dynaaminen valaistus 
- **Tunnelma:** Hauska, seikkailullinen, värikäs
- **Vertailupisteet:** Super Mario Bros., little nightmares, Teenage Mutant Ninja Turtles The Arcade Game

---

## 2. Pelimekanikat

### 2.1 Pelaajahahmo

**Liikkuminen:**
- Kävely / juoksu (R2 pohjassa)
- Hyppy (yksinkertainen + ketjuhyppy mahdollisuus)
- Kyykky (alas + hyppy = pitkä hyppy / lasku alas tasanteelta)

**Ominaisuudet:**
- hahmon perusaseena miekka ja kilpi. hahmolla mahdollisuus saada käteen "joystick", jolla ohjaa erikoisaseita, esim drone. Jokaisen kentän päätösviholliselta sen voitettuaan saa uuden erikoisaseen. Näitä voisi olla tuo edellä mainittu drone, 
- Kuoleminen:

### 2.2 Viholliset

| Vihollinen | Käyttäytyminen | Tuhoaminen | Suojautuminen 
|---|---|---|---|
| Perusvihollinen | Patrolloi tasoa | miekka + erikoisase |
| Lentävä vihollinen (drone) | Lentää pelaajaa kohti | Hyppäämällä + miekalla + erikoisaseella | kilpi
| [Lisää asiakkaan toiveiden mukaan] | | |

### 2.3 Keräilyt & Power-upit

- **timantit** — pisteet
- **Sydämet** — lisää elinvoimaa
- **[Asiakkaan erikoisesine]** — joystick päähahmolle tietystä painikkeesta, jolla ohjaa erikoisasetta. 

### 2.4 Tasomekanikat

- Tarkistuspisteet (checkpoints) — lippu/maalilinja
- Liikkuvat alustat
- Trampoliinit / hyppyalustat
- Vaara-alueet: piikki, laava, syvyys

---

## 3. Maailma & Tasot

### 3.1 Rakenne

```
Maailma 1: [Teema, esim. Metsä]
  └── Taso 1-1: Tutoriaali (opitaan perusmekanikat)
  └── Taso 1-2: Haaste kasvaa
  └── Taso 1-3: Bonus/salainen taso
  └── Taso 1-BOSS: Pomovihollainen

Maailma 2: [Teema, esim. Luola]
  └── ...
```

### 3.2 Visuaaliset teemat

| Maailma | Teema | Väripaletti | Erikoisuus |
|---|---|---|---|
| 1 | Metsä | Vihreä, kulta | Parallax puustot |
| 2 | Luola | Tumma, sininen | Kimaltelevat kristallit |
| 3 | [Asiakkaan toive] | | |

---

## 4. Äänisuunnittelu

### 4.1 Musiikki
- Tyyli: Chiptune + modernit instrumentit (kuten Sea of Stars)
- Jokainen maailma: oma teema
- Boss-taistelu: intensiivisempi versio teemasta

### 4.2 Ääniefektit
- Hyppy, kolikko, vahinko, kuolema
- Ympäristöäänet (linnut, tuuli, vesi)
- PS5 DualSense haptinen palaute: hyppyihin ja vahinkoihin

---

## 5. UI / HUD

### 5.1 Pelin aikana (HUD)
- Pisteet (ylävasen)
- Elinvoima / sydämet (ylävasen)
- Aika (keskellä ylhäällä) — valinnainen
- Kerätyt kolikot (yläoikea)

### 5.2 Valikot
- Päävalikko: Aloita, Jatka, Asetukset, Lopeta
- Asetukset: Äänenvoimakkuus, CRT-shader on/off, ohjainasetus
- Peli ohi / tason läpäisy -näyttö

---

## 6. Tekniset vaatimukset

### 6.1 Kohdealustat

| Alusta | Tavoite | Huomio |
|---|---|---|
| Raspberry Pi 5 (8GB) | 60 FPS | ARM64 export, optimointi tärkeää |
| Windows (Steam) | 60 FPS | Pääasiallinen julkaisualusta |
| Linux (Steam) | 60 FPS | Sama buildi kuin Pi |

### 6.2 Pelimoottori
- **Godot 4.x** (GDScript)
- 2D-renderöinti + CanvasModulate valaistukseen
- Steamworks GDNative -integraatio

### 6.3 Resoluutio & Renderöinti
- Sisäinen resoluutio: 320×180 (retro feel)
- Skaalataan näyttöön integer-skaalauksella (terävä pikselitaide)
- CRT-shader valinnainen päälle/pois

---

## 7. Projektin aikataulu (alustava)

| Vaihe | Sisältö | Kesto |
|---|---|---|
| Pre-production | GDD, prototyyppi, asset-lista | Viikko 1-2 |
| Prototype | Pelaajaliike, yksi testikenttä | Viikko 3-4 |
| Alpha | 1 maailma pelattavissa | Viikko 5-8 |
| Beta | Kaikki sisältö, bugitestaus | Viikko 9-11 |
| Release | Steam + Pi -buildit valmiit | Viikko 12 |

---

## 8. Avoimet kysymykset

- [ ] Asiakkaan hahmon nimi ja tarinan taustat?
- [ ] Montako maailmaa / tasoa toivotaan?
- [ ] Onko boss-vihollisia?
- [ ] Millainen juoni / narratiivi (jos ollenkaan)?
- [ ] Moninpeli nyt tai tulevaisuudessa?
- [ ] Steam-saavutukset?
- [ ] DualSense haptinen palaute prioriteetti?
