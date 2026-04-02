# GameOver — Peliasetusten säätöopas

Tämä dokumentti kattaa tärkeimmät Inspector-säädöt `PlayerController.cs`, `BossLevel1.cs` ja `EnemyLevel1.cs` skripteissä. Kaikki arvot muutetaan **Godot Inspectorissa** — koodia ei tarvitse muokata.

---

## 1. Kamera — CameraFollow.cs (Camera3D-node, level_1.tscn)

### Normaali seuranta
| Export | Vaikutus |
|--------|----------|
| `Follow Speed` | Kuinka nopeasti kamera seuraa pelaajaa — pienempi = pehmeämpi |
| `Pivot Height` | Miltä korkeudelta pelaajaa seurataan |
| `Look Sensitivity` | Ohjaimen oikean sauvan herkkyys |
| `Min/Max Pitch Deg` | Kameran pystykulman rajat |

### Bossin tanssikamera
| Export | Vaikutus |
|--------|----------|
| `Boss Dance Cam Distance` | Kameran etäisyys bossista tanssin aikana — **isompi = pelaaja näkyy paremmin** |
| `Boss Dance Cam Front Height` | Kameran korkeus tanssin aikana — isompi = ylhäältä päin |
| `Boss Dance Look At Y Offset` | Mihin kohtaan bossia kamera tähtää (0=jalat, 1=vyötärö) |
| `Boss Dance Cam Blend In Seconds` | Kuinka nopeasti siirrytään bossin kameraan |
| `Boss Dance Cam Blend Out Seconds` | Kuinka nopeasti palataan normaalikameraan |
| `Boss Dance Cam Follow Speed` | Seurannan pehmeys tanssin aikana |

---

## 2. Pelaajan miekka — PlayerController.cs (Player-node)

### Kantama ja osumaetäisyys
| Export | Vaikutus |
|--------|----------|
| `Light Melee Proximity Max` | R2-iskun max-osumaetäisyys (metriä) |
| `Heavy Melee Proximity Max` | R1-iskun max-osumaetäisyys — kapea, tarkka isku |
| `Sword Hit Tip Local Offset` | Miekan kärjen paikallinen offset — vaikuttaa iskulinjan pituuteen |
| `Sword Hit Reach Extra Meters` | Lisäkantama miekan kärjestä eteenpäin (R2) |
| `Heavy Sword Hit Reach Extra Meters` | Lisäkantama R1-iskulle |

### Osumaikkuna ja ajoitus
| Export | Vaikutus |
|--------|----------|
| `Light Melee Strike Window Advance Seconds` | Kuinka paljon osumaikkuna avautuu ennen animaation puoliväliä (R2) |
| `Sword Hit Activation Time` | Lisäviive osuman rekisteröintiin |
| `Melee Hit Stop Seconds` | Freeze-kesto kun miekka osuu |

### Osumakartio (mihin suuntaan voi osua)
| Export | Vaikutus |
|--------|----------|
| `Sword Hit Facing Half Angle Deg` | Kuinka leveä kartio edestä — isompi = osuu sivummaltakin |
| `Melee Blade Arc Half Angle Deg` | Teräkartion leveys R2:lle |
| `Heavy Melee Blade Arc Half Angle Deg` | Teräkartion leveys R1:lle — oletuksena kapeampi |

### Kilpi
| Export | Vaikutus |
|--------|----------|
| `Shield Block Threat Half Angle Deg` | Kilven suojakulma — isompi = suojaa sivuiltakin |

### Hyökkäysten cooldown
| Export | Vaikutus |
|--------|----------|
| `Light Melee Repeat Cooldown Seconds` | Minimi aika R2-iskujen välillä |
| `Heavy Attack Cooldown Seconds` | R1-iskun latausaika |
| `Light Attack Debounce Seconds` | Estää R2-tuplapainallukset |

---

## 3. Boss — BossLevel1.cs (BossLevel1-node)

### HP ja vahinko
| Export | Vaikutus |
|--------|----------|
| `Max Boss Health` | Bossin maksimi-HP (oletus 30 = 10 × R1-osumaa) |
| `Charge Contact Damage` | Syöksyn kontaktivahinko pelaajalle |
| `Charge Contact Cooldown` | Aika kahden kontaktivahingon välillä |

### Bossin osumapisteet (miekan osumatarkistus)
| Export | Vaikutus |
|--------|----------|
| `Sword Hit Probe Heights` | Korkeudet (m) joista osuma tarkistetaan juuren Y:stä ylöspäin — **muuta nämä jos boss on eri kokoinen** |
| `Sword Hit Probe Visual Y Offset` | Lisäpisteet visuaalin sijainnista |
| `Sword Hit Extra Proximity Meters` | Leventää osumaetäisyyttä kaikkiin pisteisiin |
| `Sword Hit Activation Time` | Viive ennen kuin isku rekisteröityy |

**Suositusarvot normaalikokoiselle bossille:**
```
Sword Hit Probe Heights:        0.05, 0.45, 0.90, 1.40, 1.90, 2.40
Sword Hit Probe Visual Y Offset: -0.5, 0.05, 0.55, 1.10, 1.70
Sword Hit Extra Proximity Meters: 0.55
```

### Bossin liike ja käyttäytyminen
| Export | Vaikutus |
|--------|----------|
| `Dance Duration Min/Max` | Tanssin kesto sekunteina ennen syöksyä |
| `Post Dance Delay Before Charge` | Viive tanssin jälkeen ennen syöksyä |
| `Charge Speed` | Syöksyn nopeus |
| `Charge Max Seconds` | Syöksyn maksimikesto |
| `Arena Edge Half` | Areenan puolisäde — syöksyn reunapisteet |

### Bossin sijainti spawnissa
| Export | Vaikutus |
|--------|----------|
| `Boss Stand Offset Global` | Offset dance-machine2:n sijainnista (X, Y, Z) |
| `Boss Stand Extra Lower Y` | Laskee bossia alaspäin raycastin jälkeen |
| `Boss Floor Ray Hit Y Offset` | Lisätään lattian Y:hin — säätää jalkojen korkeutta |
| `Additional Stand Lower Y` | Siirtää koko hahmoa alaspäin spawnin jälkeen |
| `Snap Feet To Floor On Ready` | Automaattinen lattiaan kiinnitys spawnissa |

### Visuaali ja koko
| Export | Vaikutus |
|--------|----------|
| `Boss Visual Uniform Scale` | Visuaalisen meshin skaalaus — **älä käytä juurinoden Scalea** |

### Tanssivalo (SpotLight3D)
| Export | Vaikutus |
|--------|----------|
| `Boss Dance Highlight Enabled` | Päälle/pois |
| `Boss Dance Light Energy` | Kirkkaus |
| `Boss Dance Light Range` | Spotin kantama |
| `Boss Dance Spot Angle Deg` | Valokiilan leveys asteina |
| `Boss Dance Spot Height M` | Valon korkeus bossin jaloista |

---

## 4. Normaaliviholliset — EnemyLevel1.cs

### Liike ja hyökkäys
| Export | Vaikutus |
|--------|----------|
| `Speed` | Vihollisen liikenopeus |
| `Attack Range` | Etäisyys jolla purema alkaa |
| `Health` | Vihollisen HP |
| `Bite Damage Windup Seconds` | Viive ennen ensimmäistä puremavahinkoa |
| `Bite Damage Min Attack Phase` | Animaation edistyminen (0–1) ennen vahinkoa — estää vahingon animaation alussa |

### Osumapisteet (miekan tarkistus)
| Export | Vaikutus |
|--------|----------|
| `Sword Hit Probe Heights` | Korkeudet joista osuma tarkistetaan — säädä hahmon koon mukaan |
| `Hit Center Y Offset` | Pääosumapiste Y-akselilla |
| `Sword Hit Activation Time` | Lisäviive osuman rekisteröintiin |

### Kuolema-animaatio
| Export | Vaikutus |
|--------|----------|
| `Death Tilt Duration` | Kaatumisaika |
| `Death Slide Duration` | Liukumisaika |
| `Death Shrink Duration` | Kutistumisaika |

---

## Muistiinpanoja

- Kaikki muutokset tehdään **Inspectorissa** — tallenna Ctrl+S ja aja F5
- `BossLevel1.cs`: bossi ottaa vahinkoa **vain tanssin aikana** R1-iskusta (dmg ≥ 3)
- Kilpisuojaus toimii **bossin syöksyä vastaan** — ei tanssin aikana
- Bossin syöksyn aikana `CollisionMask = 1` eli se läpäisee arcade-esineet (layer 5)
