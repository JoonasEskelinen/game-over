# Tekninen Arkkitehtuuridokumentti

**Projekti:** [Pelin Nimi]  
**Versio:** 0.1  
**Pelimoottori:** Godot 4.x

---

## 1. Projektihakemistorakenne

```
project.godot
├── assets/
│   ├── sprites/
│   │   ├── player/          # Pelaajahahmon spritesheet + animaatiot
│   │   ├── enemies/         # Vihollisten spritesheet
│   │   ├── tiles/           # Tilesetit kenttiin
│   │   └── ui/              # HUD-elementit, ikonit
│   ├── audio/
│   │   ├── music/           # Taustamusiikki (.ogg)
│   │   └── sfx/             # Ääniefektit (.wav)
│   └── shaders/
│       ├── crt.gdshader      # CRT-efekti
│       └── pixellight.gdshader
│
├── scenes/
│   ├── autoload/
│   │   ├── GameManager.tscn  # Autoload: pelin tila, pisteet
│   │   └── AudioManager.tscn # Autoload: äänentoisto
│   ├── characters/
│   │   ├── Player.tscn       # Pelaajahahmo
│   │   └── enemies/
│   ├── levels/
│   │   ├── World1/
│   │   └── World2/
│   ├── ui/
│   │   ├── HUD.tscn
│   │   ├── MainMenu.tscn
│   │   └── PauseMenu.tscn
│   └── objects/
│       ├── Coin.tscn
│       └── Checkpoint.tscn
│
├── scripts/
│   ├── player/
│   │   ├── PlayerController.gd
│   │   └── PlayerAnimator.gd
│   ├── enemies/
│   │   └── BaseEnemy.gd
│   ├── managers/
│   │   ├── GameManager.gd
│   │   └── AudioManager.gd
│   └── ui/
│       └── HUD.gd
│
└── docs/                     # Tämä kansio
```

---

## 2. Autoload (Singleton) -rakenne

```
GameManager      — Pelin tila, pisteet, elinvoima, scene-vaihdot
AudioManager     — Musiikin ja SFX:n hallinta
InputManager     — Ohjainmäppäykset (PS5 + näppäimistö)
SaveManager      — Tallennukset (Godot ResourceSaver)
```

---

## 3. Pelaajan Node-rakenne

```
Player (CharacterBody2D)
├── CollisionShape2D        — Törmäysalue
├── Sprite2D / AnimatedSprite2D
├── Camera2D                — Seuraa pelaajaa, smooth + limits
├── CoyoteTimer (Timer)     — Coyote time -toteutus
├── JumpBufferTimer (Timer) — Jump buffer
└── Hurtbox (Area2D)        — Vahinkoalue
    └── CollisionShape2D
```

---

## 4. Renderöintiputki

```
SubViewport (320×180)
  └── Pelin sisältö (tilemap, hahmot, valaistus)
      ↓ (integer scale)
TextureRect (koko näyttö)
  └── crt.gdshader (valinnainen, asetuksista)
```

**Pikseliresoluutio:** 320×180  
**Skaalaus:** Integer scaling (1x, 2x, 3x... ruudun koon mukaan)  
**Tavoite FPS:** 60 (Raspberry Pi 5 + PC)

---

## 5. Tallennusjärjestelmä

- Godot `FileAccess` + JSON tai `ResourceSaver`
- Tallennetaan: kenttäedistyminen, pisteet, asetukset
- Tiedostopolku: `user://save_data.json`
- Steam Cloud Save -tuki lisätään Steamworks-integraation yhteydessä

---

## 6. Ohjainintegraatio

### PS5 DualSense
- Godot 4 tunnistaa DualSense:n automaattisesti SDL2:n kautta
- Haptinen palaute: `Input.start_joy_vibration(device, weak, strong, duration)`
- Adaptive triggers: vaatii lisäplugineja (tutkitaan myöhemmin)

### Input Map (Godot Project Settings)
```
ui_left / ui_right / ui_up / ui_down  — Liike
jump                                   — Hyppy (✕ + välilyönti)
run                                    — Juoksu (R2 + Shift)
interact                               — Interaktio (▲ + E)
pause                                  — Tauko (OPTIONS + Esc)
```

---

## 7. Steam-integraatio

1. Lataa [GodotSteam](https://godotsteam.com/) — suositeltava Godot 4 Steamworks-plugin
2. Aseta Steam AppID: `steam_appid.txt` projektin juureen
3. Toteuta: saavutukset, pisteet, cloud save
4. Testaa SteamDeckilla ja Linuxilla ennen julkaisua

---

## 8. Raspberry Pi 5 -optimointi

- Käytä **Compatibility** render mode (ei Forward+)
- Pidä draw call -määrä pienenä (sprite atlakset)
- Vältä raskaita shadereitä — CRT vain jos FPS riittää
- Testaa aina fyysisellä laitteella, ei vain emulaattorilla
- Kohdenna ARM64 Linux export template

---

## 9. Suorituskykybudetti (Raspberry Pi 5)

| Resurssi | Tavoite |
|---|---|
| FPS | 60 vakaasti |
| Muisti (RAM) | < 512 MB |
| GPU draw calls / frame | < 200 |
| Äänikanavat yhtaikaa | max 16 |
