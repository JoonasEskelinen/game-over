# Osallistumisohje (Contributing Guide)

Kiitos kiinnostuksestasi projektia kohtaan! Tämä dokumentti kertoo, miten projektiin voi osallistua.

---

## 🌿 Git-työnkulku (Branching Strategy)

Käytämme **GitHub Flow** -mallia:

```
main          ← vakaa, julkaisuvalmis koodi
develop       ← aktiivinen kehitys
feature/xxx   ← uudet ominaisuudet
bugfix/xxx    ← bugikorjaukset
hotfix/xxx    ← kriittiset korjaukset mainiin
```

### Uuden ominaisuuden kehittäminen

```bash
git checkout develop
git pull origin develop
git checkout -b feature/hyppymekaniikka
# ... tee muutokset ...
git commit -m "feat: lisää kaksoishyppy pelaajalle"
git push origin feature/hyppymekaniikka
# Avaa Pull Request → develop
```

---

## ✅ Commit-viestiformaatti

Käytämme [Conventional Commits](https://www.conventionalcommits.org/) -standardia:

```
<tyyppi>: <lyhyt kuvaus>

[valinnainen leipäteksti]

[valinnainen footer]
```

### Tyypit

| Tyyppi | Käyttötarkoitus |
|---|---|
| `feat` | Uusi ominaisuus |
| `fix` | Bugikorjaus |
| `docs` | Dokumentaatio |
| `style` | Koodin muotoilu (ei toiminnallista muutosta) |
| `refactor` | Refaktorointi |
| `test` | Testit |
| `chore` | Rakenne, buildi, riippuvuudet |
| `assets` | Peliresurssit (spritet, äänet, kentät) |

### Esimerkkejä

```
feat: lisää CRT-shader asetusvalikkoon
fix: korjaa pelaajan törmäys kapeiden tasanteiden kanssa
assets: päivitä päähenkilön walk-animaatio 8-framea
docs: päivitä README Raspberry Pi -asennusohje
```

---

## 🔍 Pull Request -käytäntö

1. PR avataan **develop**-haaraan (ei suoraan mainiin)
2. Kuvaa muutokset selkeästi PR:n kuvauksessa
3. Linkitä liittyvä Issue jos sellainen on
4. Varmista, että peli käynnistyy ja muutokset toimivat
5. Odota review ennen mergeä

### PR-kuvauspohja

```markdown
## Muutokset
- Lyhyt kuvaus muutoksista

## Testaustapa
- Miten testasin nämä muutokset

## Screenshots / Video (jos visuaalinen muutos)
[liitä kuva tai gif]

## Liittyvät Issuet
Closes #[issue-numero]
```

---

## 🐛 Bugi-ilmoitukset

Käytä GitHub Issues -toimintoa. Liitä mukaan:

- Kuvaus bugista
- Askeleet toistamiseen
- Odotettu vs. havaittu toiminta
- Laite ja käyttöjärjestelmä (erityisesti Raspberry Pi vs. PC)
- Screenshotteja tai videota jos mahdollista

---

## 📂 Koodityyli

- Muuttujanimet: `snake_case` (GDScript-standardi)
- Luokkanimet: `PascalCase`
- Konstantit: `SCREAMING_SNAKE_CASE`
- Kommentoi monimutkaiset loogiset osuudet suomeksi tai englanniksi
- Pidä funktiot lyhyinä ja yhden vastuun periaatteella
