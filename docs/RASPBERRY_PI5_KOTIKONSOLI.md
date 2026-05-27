# Raspberry Pi 5 — käyttöönotto kotikonsolina (Game Over)

## Pelin tekniset speksit

| | |
|---|---|
| **Nimi** | Game Over |
| **Tyyli** | 2.5D-toiminta — 3D-maailma, sivukamera (kiinteä Z) |
| **Pelimoottori** | Godot **4.6** (projektin `config/features`), **C# / .NET 8** (`GameOver.csproj`, Godot.NET.Sdk 4.6.1) |
| **Fysiikka** | Jolt Physics |
| **Autoloadit** | `GameState`, `LoadingOverlay` |
| **Julkaisu / integraatiot** | GodotSteam (Steam Windows/Linux); Pi-kotikonsoli = suora **linux-arm64** -export, ei Steamia |

**Kohdealustat ja tavoite-FPS** (GDD, luku 6.1 — [GDD.md](GDD.md)):

| Alusta | Tavoite FPS | Huomio |
|--------|-------------|--------|
| Raspberry Pi 5 (8 GB) | 60 | **linux-arm64** -export, optimointi tärkeää |
| Windows (Steam) | 60 | Pääasiallinen julkaisualusta |
| Linux (Steam) | 60 | Sama linja kuin Pi-puolen Linux-buildiin |

**Renderöinti ja laatu**

- Kehitys / PC: **Forward+** (projektin oletus `project.godot`).
- **Raspberry Pi:** **Compatibility**-renderöijä suorituskyvyn varmistamiseksi (heikko integroitu GPU).
- Sisäinen peliresoluutio: retro / skaalattu (tarkentuu suunnitelmassa); **CRT-shader** valinnainen asetuksista.

**Ohjaimet**

- **PS5 DualSense** — täysi tuki (kehityksessä).
- **Näppäimistö** — kehitys ja testaus.
- Haptinen palaute: mahdollinen myöhemmin (GDD).

Alkuperäinen suunnitelma: [GDD.md](GDD.md). Tekninen rakenne: [ARCHITECTURE.md](ARCHITECTURE.md). Käynnistys: [README.md](README.md).

---

Käyttöönotto Pi 5:lle kotikonsolina: käyttöjärjestelmästä kiosk-tilaan ja ohjaimeen. Komentolohkot voi kopioida suoraan Pi:lle. Tavoite on **linux-arm64** -export ja kevyt grafiikkaprofiili Pi 5:lle.

---

## 1. Käyttöjärjestelmä ja pohja

### 1.1 Suositus: Raspberry Pi OS (64-bit)

- **Raspberry Pi OS (Bookworm), 64-bit** — virallinen tuki, hyvät ajurit HDMI:lle, Bluetoothille ja näppäimistölle/ohjaimelle.
- Asenna **Raspberry Pi Imager** -työkalulla ([raspberrypi.com/software](https://www.raspberrypi.com/software/)): valitse oikea levy, OS ja **Customize** ennen kirjoitusta:
  - käyttäjänimi / salasana,
  - Wi‑Fi (jos käytössä),
  - **SSH** päälle (kehität headless-tilassa tai haluat etähallinnan).

**Desktop vs Lite**

| Vaihtoehto | Milloin |
|------------|---------|
| **Desktop** | Helppo alku: graafinen työpöytä, testaat pelin ikkunan ja kiosk-autostartin nopeasti. |
| **Lite** (ei työpöytää) | Kevyempi, mutta joudut itse asentamaan näyttöpalvelimen ja ikkunointi-pinon, jos et käynnistä peliä suoraan KMS/DRM:stä. Godot-linux-export on käytännössä usein helpompi **Desktopilla** tai kevyellä ikkunamanagerilla. |

**Käytännön linja kotikonsoliin:** aloita **Desktop 64-bit** — saat näytön, äänen ja ohjaimen kuntoon ensin; kevennä (Lite + minimaalinen UI) vasta kun pelin käynnistys on vakaa.

### 1.2 Päivitykset ja peruskalusto

Kirjaudu sisään ja päivitä:

```bash
sudo apt update && sudo apt full-upgrade -y
sudo reboot
```

Työkaluja vianetsintään (valinnaisia mutta hyödyllisiä):

```bash
sudo apt install -y git curl htop vim
```

---

## 2. Näyttö ja grafiikka

### 2.1 Resoluutio ja virkistystaajuus

- Käytä näytön **nativiivia resoluutiota**; vältä skaalaamista, jos mahdollista (vähemmän latenssia ja GPU-kuormaa).
- Raspberry Pi OS: **Preferences → Screen Configuration** (tai `arandr` jos asennettuna) — aseta oikea resoluutio ja orientaatio.

### 2.2 `/boot/firmware/config.txt` (edistynyt)

Muokkaukset tehdään yleensä tiedostoon `/boot/firmware/config.txt` (Bookworm). Esimerkkejä tarpeen mukaan — **testaa yksi muutos kerrallaan**:

- **HDMI-ongelmat:** joskus `hdmi_force_hotplug=1` auttaa tietyissä näytöissä.
- **Mustat reunat (overscan):** vanhoissa TV:issä; Pi OS:n näyttöasetuksissa on usein helpompi säätää kuin käsin.

Varmuuskopioi ennen muokkausta:

```bash
sudo cp /boot/firmware/config.txt /boot/firmware/config.txt.bak
```

**Pi‑vaikutus peliin:** tavoitteena on heikko integroitu GPU — pidän sisäisen renderöintiresoluution ja varjot maltillisina. Repossa **ei vielä keskitettyä `PiLow`-presettiä**; säädän kenttäkohtaisesti (esim. `ForestScatter.TotalTrees`, varjot pois, Compatibility-export).

### 2.3 Vähemmän “työpöytäkuormaa” kiosk-tilassa

- Poista turhat **autostart**-ohjelmat.
- Kytke **näytön säästö** ja **näytön sammutus** pois pelitilassa (tai käytä alla olevaa `xset`-komentoa sessiossa).

---

## 3. Ääni

- **HDMI-ääni:** usein oletuksena; testaa äänenvoimakkuus paneelista.
- **USB-äänikortti / DAC:** valitse oletuslaite `alsamixer` / järjestelmän ääniasetuksista.

Jos ääni “katoaa” käynnistyksissä, kirjaa ylös käytössä oleva desktop (PulseAudio / PipeWire) ja tarkista oletuslaite.

---

## 4. Ohjain (Bluetooth / USB)

### 4.1 DualSense (PS5) ja yleinen peliohjain

Linux-ydin sisältää usein **hid-playstation** -tuen. Käytännössä:

1. **USB:** liitä ohjain — usein toimii heti; tarkista laite:

   ```bash
   ls /dev/input/js*
   ```

2. **Bluetooth:** Pi OS Desktop: Bluetooth-paneelista laiteparitus. Jos paritus epäonnistuu:

   ```bash
   bluetoothctl
   # power on
   # scan on
   # pair <MAC>
   # trust <MAC>
   # connect <MAC>
   ```

3. Testaa syötteitä:

   ```bash
   sudo apt install -y joystick
   jstest /dev/input/js0
   ```

Godot käyttää SDL2-pohjaista joypad-tunnistusta — kun laite näkyy järjestelmälle oikein, Godot näkee sen yleensä samana ohjaimena kuin Windowsillakin (nappien järjestys voi vaihdella; projektin Input Map on `project.godot`-tiedostossa).

### 4.2 Oikeudet (jos laite on `input`-ryhmässä)

Lisää käyttäjä ryhmään (jakeluista riippuen):

```bash
sudo usermod -aG input $USER
# kirjaudu ulos ja takaisin
```

---

## 5. Godot-pelin build ja sijoitus Pi:lle

### 5.1 Mitä tarvitset kehityskoneella

- Godot **.NET** -build (sama suurpiirteinen versio kuin projektissa, esim. 4.6.x).
- Export-preset kohteelle **Linux / arm64** (tai “Linux ARM64”), **Release**, ja projektin mukaan **Compatibility**-renderöijä Pi-suorituskyvyn vuoksi (ks. [GDD.md](GDD.md)).
- .NET **linux-arm64** -julkaisu: varmista, että exportattu paketti sisältää tarvittavat natiivikirjastot ja että ajat testin oikealla Pi 5:llä tai vastaavalla ARM64-ympäristöllä.

Tarkat export-asetukset elävät projektissa — kirjataan tähän kun ensimmäinen toimiva **arm64**-paketti on varmistettu.

### 5.2 Minne asennat pelin Pi:llä

Tyypillinen paikka:

```text
/opt/gameover/
```

tai käyttäjän hakemistoon:

```text
/home/pi/Games/gameover/
```

Anna suoritusoikeudet pääbinaarille (nimi riippuu exportista):

```bash
chmod +x GameOver   # esimerkki; Godot exportin pääbinaarin nimi voi vaihdella
```

---

## 6. Kiosk-tila: peli käynnistyksiin koko näytölle

Tavoite: **käynnistyksen jälkeen suoraan peli**, ilman kirjautumista työpöydälle säätämään.

### 6.1 Automaattinen kirjautuminen (GUI)

- **Raspberry Pi OS Desktop:** *Raspberry Pi Configuration* → **Auto Login** → valitse käyttäjä ja “Desktop” tai “Console” tarpeen mukaan.

Näin sessio käynnistyy ilman salasanaa — sopii suljettuun peliluolaan; **älä käytä julkisessa verkossa** ilman lisäsuojausta.

### 6.2 Käynnistä peli kirjautumisen jälkeen

**LXDE/LXSession-autostart (Desktop):**

Luo tiedosto (polku voi vaihdella hieman OS-versiosta riippuen):

```text
~/.config/lxsession/LXDE-pi/autostart
```

tai käytä **Menu → Preferences → Default applications for LXSession** / “Autostart” -käyttöliittymää.

Lisää rivejä esimerkiksi:

```text
@xset s off
@xset -dpms
@xset s noblank
@/opt/gameover/GameOver.sh
```

`GameOver.sh` voi olla:

```bash
#!/bin/bash
cd /opt/gameover
./GameOver --fullscreen
```

Anna suoritusoikeus:

```bash
chmod +x /opt/gameover/GameOver.sh
```

**Huom:** Godotin komentoriviparametrit — tarkista Godot 4 -dokumentaatio exportillesi (`--fullscreen` tms.).

### 6.3 Vaihtoehto: systemd user service (hallittu käynnistys)

Kun haluat uudelleenkäynnistyksen kestävän käynnistyksen ja lokeja:

```ini
# ~/.config/systemd/user/gameover.service
[Unit]
Description=Game Over
After=graphical-session.target

[Service]
Type=simple
WorkingDirectory=/opt/gameover
ExecStart=/opt/gameover/GameOver --fullscreen
Restart=on-failure

[Install]
WantedBy=default.target
```

```bash
systemctl --user enable gameover.service
systemctl --user start gameover.service
```

Tämä vaatii, että user-palvelut käynnistyvät graafisen istunnon mukana (Desktop-asennuksessa yleensä ok).

### 6.4 “Kiosk” ilman työpöytää

Jos haluat vain pelin ja mahdollisimman vähän taustaa, vaihtoehdot ovat jakelu- ja ikkunointi-pinosta riippuen:

- minimaalinen ikkunamanageri + peli fullscreen;
- tai Wayland/X -sessio, jossa ainoa asiakasohjelma on peli.

Tämä on jo pidemmälle viety; Desktop + autostart on usein nopein polku **ensimmäiseen toimivaan** laitteistoon.

---

## 7. Suorituskyky ja vakaus (lyhyt checklist)

- [ ] Export: **arm64**, **Release**, **Compatibility** (Forward Plus vain PC-kehityksessä).
- [ ] Testaa kolme kampanjakenttää (`level_1`–`level_3`) — instanssimäärät ja varjot eri kentillä.
- [ ] Näyttö: natiiviresoluutio; ei tarpeettomia skaalauksia.
- [ ] Taustasovellukset ja selaimet kiinni pelitilassa.
- [ ] Lämpö: varmista ilmanvaihto/kotelo — throttling laskee FPS:ää.
- [ ] Virtalähde: riittävä USB-C PD -laturi (Pi 5).

---

## 8. Etähallinta ja ylläpito

- **SSH** päälle vain luotetussa verkossa; käytä avainautentikointia.
- Päivitykset: `apt update && apt full-upgrade` hallituin väliajoin.
- Pelin päivitys: korvaa `/opt/gameover/` uudella buildilla (pidä varmuuskopio).

---

## 9. Vianetsintä (pikavilkaisu)

| Oire | Suunta |
|------|--------|
| Musta näyttö bootin jälkeen | Viimeisin toimiva `config.txt`-muutos takaisin; testaa toinen HDMI-portti/kaapeli. |
| Ei ääntä | Oletusäänilaitteen valinta; HDMI vs analog/USB. |
| Ohjain ei reagoi | `jstest`, Bluetooth-paritus uudelleen, USB-portti, käyttäjän `input`-ryhmä. |
| Peli käynnistyy ikkunassa | `--fullscreen` tai Godot Project Settings -ikkuna fullscreen/alustus. |
| Matala FPS | Compatibility, render scale, varjot pois / laatupresetti, resoluutio alas. |

---

## 10. Seuraavat täsmennykset (täytä kun build on lukittu)

Kun ensimmäinen **linux-arm64** -export on valmis, kannattaa kirjata tähän:

- tarkka **Godot-versionumero** ja **.NET**-runtime-vaatimus Pi:llä;
- exportatun **pääbinaarin nimi** ja tarvittavat **seuraavat tiedostot** (pck, so-libit);
- mahdollinen **wrapper-skripti** ympäristömuuttujilla (`MESA_GL_VERSION_OVERRIDE` yms. vain jos tarpeen — testaa ensin ilman).

---

*Muistilista Pi 5 -kotikonsolin käyttöönottoon. Tarvittaessa laajennan erillisillä liitteillä (esim. Steam Link, verkkopelit).*
