# 🎮 [Pelin Nimi] — Modern Retro Platformer

> Mario-tyylinen retropeli modernilla HD-2D -visuaalisella tyylillä. Kehitetty työharjoitteluprojektina.

![Godot](https://img.shields.io/badge/Godot-4.x-478CBF?logo=godot-engine)
![Platform](https://img.shields.io/badge/Platform-Linux%20%7C%20Windows%20%7C%20Steam-blue)
![License](https://img.shields.io/badge/License-Proprietary-red)

---

## 📖 Sisällysluettelo

- [Yleiskatsaus](#yleiskatsaus)
- [Ominaisuudet](#ominaisuudet)
- [Asennus ja käynnistys](#asennus-ja-käynnistys)
- [Ohjaimet](#ohjaimet)
- [Rakenne](#rakenne)
- [Kehitysympäristö](#kehitysympäristö)
- [Lisensointi](#lisensointi)

---

## Yleiskatsaus

Tämä projekti on asiakkaan tilaama peliluola- ja Steam-julkaisu. Peli yhdistää klassisen Mario-tyylisen platformer-pelimekaniikan moderniin HD-2D -visuaaliseen tyyliin — pikselitaide kohtaa dynaamisen valaistuksen ja partikkeliefektit.

**Kohdelaitteet:**
- Raspberry Pi 5 (8GB) — peliluokkaympäristö
- PS5 DualSense -ohjain
- Steam (Windows/Linux)

---

## Ominaisuudet

- ✅ Klassiset platformer-mekanikat (hyppy, juoksu, vihollispolku)
- ✅ HD-2D -visuaalisuus: pikselitaide + dynaaminen 2D-valaistus
- ✅ Parallax-taustat syvyysvaikutelmalla
- ✅ PS5 DualSense -tuki (haptinen palaute suunnitteilla)
- ✅ Raspberry Pi 5 -optimoitu Linux-buildi
- ✅ Steam-yhteensopiva (Steamworks SDK)
- ✅ CRT-shader -vaihtoehto asetuksissa
- ✅ Asiakkaan omat hahmot ja maailma

---

## Asennus ja käynnistys

### Vaatimukset

- [Godot 4.x](https://godotengine.org/download/) (stable)
- Git
- PS5-ohjain (USB tai Bluetooth)

### Kloonausprojekti

```bash
git clone https://github.com/[käyttäjä]/[repo-nimi].git
cd [repo-nimi]
```

### Avaa Godotissa

1. Avaa Godot Engine
2. Valitse **Import** → etsi `project.godot`
3. Paina **Run** (F5)

### Raspberry Pi 5 -buildi

```bash
# Exportoi Godot Editorista:
# Project → Export → Linux (ARM64)
# Kopioi .pck ja binary Raspberry Pi:lle
```

---

## Ohjaimet

| Toiminto | PS5 Ohjain | Näppäimistö |
|---|---|---|
| Liiku | Vasen tatti / D-pad | WASD / nuolinäppäimet |
| Hyppy | ✕ (Cross) | Välilyönti |
| Juoksu | R2 | Shift |
| Tauko | OPTIONS | Esc |
| Interaktio | ▲ (Triangle) | E |

---

## Rakenne

```
/
├── assets/
│   ├── sprites/          # Hahmot ja tilet
│   ├── audio/            # Musiikki ja äänet
│   └── shaders/          # CRT-shader, valo-efektit
├── scenes/
│   ├── characters/       # Pelaaja, viholliset
│   ├── levels/           # Kentät
│   └── ui/               # HUD, valikot
├── scripts/              # GDScript-tiedostot
├── docs/                 # Projektidokumentaatio
└── project.godot
```

---

## Kehitysympäristö

- **Pelimoottori:** Godot 4.x
- **Kieli:** GDScript
- **Versionhallinta:** Git + GitHub
- **Taiteentuotanto:** Aseprite (pikselitaide)
- **Äänet:** [DAW / äänikirjasto]

---

## Lisensointi

Peli ja sen sisältö ovat asiakkaan omaisuutta. Katso [LICENSE](LICENSE) lisätietoja varten.
