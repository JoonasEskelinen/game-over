using System;
using Godot;

/// <summary>
/// PlayerController hallinnoi kaikkea pelaajaan liittyvää:
/// liikkuminen, hyökkääminen, puolustus, animaatiot ja kuolema.
/// Tämä skripti on kiinnitetty Player-nodeen (CharacterBody3D).
/// </summary>
public partial class PlayerController : CharacterBody3D
{
	// ─────────────────────────────────────────────
	// EXPORTATUT MUUTTUJAT — näkyvät Godot-editorissa
	// ─────────────────────────────────────────────

	/// <summary>Kävelynopeus miekka+kilpi-tilassa.</summary>
	[Export] public float Kävelynopeus = 3.0f;

	/// <summary>Juoksunopeus normaalitilassa (ilman asetta).</summary>
	[Export] public float Juoksunopeus = 8.0f;

	/// <summary>Hypyn alkuvauhti ylöspäin.</summary>
	[Export] public float HypynAlkuvauhti = 10.0f;

	/// <summary>Painovoiman voimakkuus — isompi arvo = nopeampi putoaminen.</summary>
	[Export] public float Painovoima = 28.0f;

	/// <summary>
	/// Kun true, pelaaja voi liikkua myös Z-akselilla (syvyys).
	/// False = vain sivuttaisliike (2.5D-tyyli).
	/// Tätä voi vaihtaa per-kenttä Inspectorissa.
	/// </summary>
	[Export] public bool SyvyysliikeKäytössä = true;

	/// <summary>Z-akselin liikkeen minimi (kauimmainen piste kamerasta).</summary>
	[Export] public float SyvyysAlaraja = -1.75f;

	/// <summary>Z-akselin liikkeen maksimi (lähimpänä kamera).</summary>
	[Export] public float SyvyysYläraja = 1.75f;

	/// <summary>
	/// Ohjaa miten syvyysliike tulkitaan.
	/// -1 = eteenpäin tatista menee kauemmas, 1 = päinvastoin.
	/// </summary>
	[Export] public float SyvyysSyötteenEtumerkki = -1f;

	/// <summary>
	/// Hahmon kääntymisen pehmeys.
	/// 0 = välitön kääntyminen, suurempi arvo = pehmeämpi.
	/// </summary>
	[Export] public float SuunnanPehmennys { get; set; } = 18f;

	/// <summary>
	/// Viive hypyn painalluksesta ponnistukseen (sekunteina).
	/// Säädä kunnes sopii animaation kanssa yhteen.
	/// </summary>
	[Export] public float HypynLämmittelyviive = 0.35f;

	/// <summary>
	/// Siirtää gameover_character-mallia paikallisesti Y-suunnassa (metriä). Negatiivinen = jalat lähemmäs maata.
	/// Kapseli (CollisionShape3D) ja FBX:n origo eivät usein täsmää; säädä tästä ennen kuin muokkaat sceneä uudestaan.
	/// </summary>
	[Export] public float HahmonPohjanOffsetY = 0f;

	// ─────────────────────────────────────────────
	// ASE-TILA
	// ─────────────────────────────────────────────

	/// <summary>
	/// Asemoodi: kolmio (toggle_weapon) vaihtaa vain Normal ↔ SwordShield.
	/// Istuminen on erillinen tila (<c>sit</c>), myöhemmin sidottavissa joystickiin.
	/// </summary>
	private enum WeaponMode { Normal, SwordShield }
	private WeaponMode _weaponMode = WeaponMode.Normal;
	private bool _isSitting;

	// ─────────────────────────────────────────────
	// PRIVAATIT MUUTTUJAT
	// ─────────────────────────────────────────────

	/// <summary>Viittaus pelaajan MeshInstance3D:hen (ei tällä hetkellä käytössä mutta varalla).</summary>
	private MeshInstance3D _mesh;

	/// <summary>Viittaus hahmon 3D-mallinodeen (gameover_character). Tätä käytetään kääntymiseen.</summary>
	private Node3D _characterModel;

	/// <summary>Viittaus AnimationPlayeriin — löydetään gameover_character-noden alta.</summary>
	private AnimationPlayer _animationPlayer;

	/// <summary>Viittaus HealthComponentiin — hallinnoi HP:ta ja elämiä.</summary>
	private HealthComponent _healthComponent;

	/// <summary>True kun pelaaja tekee lyöntianimaation. Estää muut toiminnot sen aikana.</summary>
	private bool _isAttacking = false;

	/// <summary>True kun L2 on pohjassa ja pelaaja on SwordShield-tilassa.</summary>
	private bool _isBlocking = false;

	/// <summary>True hypyn viiveaikana (ennen ponnistusta).</summary>
	private bool _isWindingUp = false;

	/// <summary>Laskee hypyn viiveen.</summary>
	private float _jumpTimer = 0f;

	/// <summary>Hahmon nykyinen katselusuunta Y-akselilla (radiaaneina). Käytetään pehmeyskääntymiseen.</summary>
	private float _facingYaw;

	/// <summary>Nykyisen hyökkäyksen vahinko. R2 = 1, R1 = 3.</summary>
	private int _attackDamage = 1;

	/// <summary>
	/// Miekkalyönnin clip (mixamo_com_005 / _010), asetetaan iskun alussa.
	/// CurrentAnimation + CurrentAnimationLength voivat blendin ensi frameilla viitata väärään animaatioon → R2-osumaikkuna menee ohi.
	/// </summary>
	private StringName _meleeStrikeClip;

	/// <summary>Fysiikkakello R2-osumaa varten (AnimationPlayer-position voi jäädä jälkeen blendissä).</summary>
	private float _meleeSwingElapsed;

	private float _meleeHitStopTimer;
	private double _meleeHitStopSeekPos;

	private RigidBody3D _grabbedBody;

	/// <summary>R2: liipasin uudelleen "sallittu" kun akseli on päästetty tarpeeksi alas (latch).</summary>
	private bool _lightTriggerArmed = true;

	/// <summary>R2: edellisen fysiikkframen analogi (nopea veto -tunnistus).</summary>
	private float _lightAnalogPreviousFrame;

	/// <summary>R2: estää tuplapainallukset / vapinaa (sekunteja).</summary>
	private float _lightAttackDebounce;

	private float _lightMeleeCooldown;

	private float _heavyAttackCooldown;

	private bool _heavyCooldownBarUnlocked;

	/// <summary>Miekka-node — haetaan Skeleton3D:n alta. Näkyy vain SwordShield-tilassa.</summary>
	private Node3D _sword;

	/// <summary>Kilpi-node — haetaan Skeleton3D:n alta. Näkyy vain SwordShield-tilassa.</summary>
	private Node3D _shield;

	/// <summary>Drone-joystickin visuaali oikeassa kädessä — luodaan koodissa SetupDroneJoystick().</summary>
	private Node3D _droneJoystick;

	/// <summary>True kun drone-moodi on aktiivinen (kolmio + HasJoystick).</summary>
	private bool _isDroneMode = false;

	private AudioStreamPlayer _swordSFX;
	private AudioStreamPlayer _damageSFX;

	/// <summary>
	/// True kun jokin EnemyLevel1 on jo rekisteröinyt osuman tällä lyöntiswingillä (tai R2 yksi kohde).
	/// Estää yhden swingin tappamasta useita vihollisia kerralla, ellei käytetä <see cref="TryClaimEnemyHeavyCleaveHit"/>.
	/// </summary>
	private bool _enemyHitThisSwing = false;

	/// <summary>R1: rekisteröidyt cleave-osumat (EnemyLevel2), enintään 2 / swing.</summary>
	private int _heavyMeleeCleaveHitsThisSwing;

	// ─────────────────────────────────────────────
	// JULKISET METODIT — vihollinen käyttää näitä
	// ─────────────────────────────────────────────

	/// <summary>Palauttaa true jos pelaaja tekee lyöntiä juuri nyt.</summary>
	public bool IsMeleeAttackActive() => _isAttacking;

	/// <summary>Palauttaa nykyisen lyönnin vahingon (1 = normaali, 3 = vahva).</summary>
	public int GetMeleeAttackDamage() => _attackDamage;

	/// <summary>True jos aktiivinen miekkalyönti on R1 (raskas), ei R2.</summary>
	public bool IsHeavyMeleeAttackActive() => _isAttacking && _meleeStrikeClip == "mixamo_com_010";

	/// <summary>Palauttaa true jos pelaaja on SwordShield-tilassa.</summary>
	public bool IsSwordWeaponMode() => !_isSitting && _weaponMode == WeaponMode.SwordShield;

	/// <summary>Palauttaa true jos pelaaja blokkaa kilvillä juuri nyt.</summary>
	public bool IsBlocking() => _isBlocking;

	/// <summary>
	/// True vain jos kilpi on ylhäällä ja uhka on edessä (kapea kartio). Käytä purema-/iskutarkistuksissa.
	/// </summary>
	public bool IsBlockingEffectiveAgainst(Vector3 threatWorldPosition)
	{
		if (!IsBlocking()) return false;
		if (_characterModel == null || !_characterModel.IsInsideTree() || !IsInsideTree())
			return false;
		return IsShieldBlockFacingArc(GlobalPosition, threatWorldPosition, KilpiTorjuntaPuolikulma);
	}

	/// <summary>
	/// Kilven torjunta XZ-kartiossa: katssuunta Mixamo/hahmomallissa on usein <c>-Basis.Z</c> (ei sama kuin miekan facing-kartio).
	/// </summary>
	private bool IsShieldBlockFacingArc(Vector3 origin, Vector3 worldPoint, float halfAngleDeg)
	{
		var to = worldPoint - origin;
		to.Y = 0f;
		if (to.LengthSquared() < 1e-8f) return true;
		to = to.Normalized();
		var forward = _characterModel.GlobalTransform.Basis.Z;
		forward.Y = 0f;
		if (forward.LengthSquared() < 1e-8f) return true;
		forward = forward.Normalized();
		float cosLimit = Mathf.Cos(Mathf.DegToRad(halfAngleDeg));
		return to.Dot(forward) >= cosLimit;
	}
	
	/// <summary>
	/// Terän suunta paikallisissa koordinaateissa (Mixamo-miekka: usein -Z on terän suunta).
	/// </summary>
	[Export] public Vector3 MiekkaTeräPaikallinenOffset = new Vector3(0f, 0f, -0.62f);

	/// <summary>
	/// Pidentää iskusegmenttiä terän kärjestä eteenpäin (metriä). Säädä kantamaa ilman että muutat offset-vektoria.
	/// </summary>
	[Export] public float MiekkaIskuKantamaLisä = 0.13f;

	/// <summary>R1: lyhyempi “löysä” teränjatke — estää puolen kentän osumat.</summary>
	[Export] public float RaskasIskuKantamaLisä = 0.1f;

	/// <summary>
	/// R2 (mixamo_com_005): kuinka paljon aikaisemmin osumaikkuna avautuu (vähennetään ikkunan alusta sekunteina).
	/// </summary>
	[Export] public float KevytIskuOsumaikkunaAikaistus = 0.05f;

	/// <summary>
	/// Maksimikulma (astetta) hahmon etusuunnasta: osuma rekisteröityy vain tämän kartion sisällä.
	/// </summary>
	[Export] public float MiekkaIskuSuuntaPuolikulma = 72f;

	/// <summary>R2-iskun teräkartion puolikulma (asteita). R1 käyttää <see cref="TeräkaariPuolikulmaRaskas"/>.</summary>
	[Export] public float TeräkaariPuolikulmaKevyt = 56f;

	/// <summary>R1: vain teräkartio — hieman leveämpi kuin ennen (edessä oleva vihollinen rekisteröityy luotettavammin).</summary>
	[Export] public float TeräkaariPuolikulmaRaskas = 42f;

	/// <summary>Max etäisyys osumapisteestä iskulinjaan (metriä), R2.</summary>
	[Export] public float KevytIskuLäheisyysMaksimi = 0.92f;

	/// <summary>Max etäisyys iskulinjaan, R1.</summary>
	[Export] public float RaskasIskuLäheisyysMaksimi = 0.58f;

	/// <summary>Facing-kartion origo: rintakorkeus hahmomallista (ei CharacterBody3D jalkojen juurta).</summary>
	[Export] public float IskunRintaKorkeus = 0.88f;

	/// <summary>Sekunteina: miekan osuttua lyönti pysähtyy tähän animaatioasentoon (hit-stop).</summary>
	[Export] public float IskuJähmetysSekuntia = 0.09f;

	/// <summary>
	/// Kilpi torjuu vain uhat tämän puolikulman sisällä (asteita). Kapeampi kuin miekan kaari.
	/// </summary>
	[Export] public float KilpiTorjuntaPuolikulma = 52f;

	/// <summary>Ryhmä <c>grabbable</c> RigidBody3D — neliö + pitää pohjassa (level_1 air-hockey).</summary>
	[Export] public float TartuntaEtäisyys = 2.5f;

	/// <summary>Työntötilassa hahmon käännös kohti kohdetta: kerroin <see cref="SuunnanPehmennys"/>-arvoon (isompi = napakampi).</summary>
	[Export] public float TartuntaKääntöPehmennysKerroin = 2.2f;

	[Export] public float TartuntaPitoEtäisyys = 1.15f;

	[Export] public float TartuntaVetoVahvistus = 7f;
	
	[Export] public float TartuntaLiikeMaxNopeus = 1.1f;

	/// <summary>R2: latch-polku: analogi ≥ tämä laukaisee (kun aseistettu).</summary>
	[Export] public float HyökkäysLiipaisinLaukaisu = 0.4f;

	/// <summary>R2: latch aseutuu kun analogi &lt; tämä (nostettu: PS5 ei aina laske nollaan).</summary>
	[Export] public float HyökkäysLiipaisinPalautus = 0.26f;

	[Export] public float KevytIskuPainallustenSuodatus = 0.1f;

	/// <summary>R2: minimiaika sekunteina kahden iskun välillä (esim. 1 s).</summary>
	[Export] public float KevytIskuToistojäähdytys = 0.6f;

	/// <summary>R2: pikaveto — analogi ≥ tämä ja nousu ≥ SharpPullDelta (latch ohitetaan).</summary>
	[Export] public float KevytIskuPikaVetoMinimi = 0.38f;

	[Export] public float KevytIskuPikaVetoNousu = 0.12f;

	[Export] public float RaskasHyökkäysJäähdytys = 10f;

	public float GetAttackAnimationTime()
	{
		if (!_isAttacking || _animationPlayer == null) return 0f;
		// Älä rajoita animaation nimellä — ensimmäisellä framella / blendissä nimi voi vaihdella
		// ja osumaikkuna (EnemyLevel1 SwordHitActivationTime) jäisi koskaan täyttymättä.
		float pos = (float)_animationPlayer.CurrentAnimationPosition;
		// R2: blendissä pos voi pysyä nollassa — käytä myös fysiikkakelloa (kasvaa _PhysicsProcessissa).
		if (_meleeStrikeClip == "mixamo_com_005")
			return Mathf.Max(pos, _meleeSwingElapsed);
		return pos;
	}

	/// <summary>
	/// Sekunteina animaation alusta: milloin lyönti katsotaan "osumavaiheessa" (lyhyt R2-animaatio vs pitkä R1).
	/// </summary>
	public float GetMeleeStrikeWindowStart()
	{
		if (!_isAttacking || _animationPlayer == null) return 0.35f;

		float len;
		StringName clip = _meleeStrikeClip;
		if (clip != default && _animationPlayer.GetAnimationLibrary("") is { } lib && lib.HasAnimation(clip))
			len = (float)lib.GetAnimation(clip).Length;
		else
		{
			len = (float)_animationPlayer.CurrentAnimationLength;
			clip = _animationPlayer.CurrentAnimation;
		}

		if (len <= 0.02f) return 0.04f;
		if (clip == "mixamo_com_005")
		{
			float start = Mathf.Clamp(len * 0.048f, 0.01f, 0.14f);
			return Mathf.Max(0f, start - KevytIskuOsumaikkunaAikaistus);
		}
		if (clip == "mixamo_com_010")
			return Mathf.Clamp(len * 0.2f, 0.06f, 0.4f);
		return Mathf.Clamp(len * 0.16f, 0.04f, 0.3f);
	}

	/// <summary>
	/// Miekan iskulinja (kahva → terän kärki) osumatarkistusta varten.
	/// </summary>
	public void GetMeleeHitSegment(out Vector3 segmentStart, out Vector3 segmentEnd)
	{
		// GlobalTransform / GlobalPosition ilman scene-puuta → Godot varoitus NativeCalls + identiteetti-transformi
		if (!IsInsideTree())
		{
			segmentStart = segmentEnd = new Vector3(0f, -1e6f, 0f);
			return;
		}

		if (IsSwordWeaponMode() && _sword != null && GodotObject.IsInstanceValid(_sword) && _sword.IsInsideTree())
		{
			float reachExtra = _attackDamage >= 3 ? RaskasIskuKantamaLisä : MiekkaIskuKantamaLisä;
			segmentStart = _sword.GlobalPosition;
			Vector3 tipWorld = _sword.GlobalTransform.Basis * MiekkaTeräPaikallinenOffset;
			float tipLen = tipWorld.Length();
			if (tipLen > 1e-5f && reachExtra > 0f)
				segmentEnd = segmentStart + tipWorld + (tipWorld / tipLen) * reachExtra;
			else
				segmentEnd = segmentStart + tipWorld;
			return;
		}

		Vector3 fallback = GlobalPosition + Vector3.Up * 0.95f;
		segmentStart = fallback;
		segmentEnd = fallback;
	}

	/// <summary>
	/// Etäisyys pisteen ja iskulinjan välillä (3D).
	/// </summary>
	public float GetMeleeHitDistanceToPoint(Vector3 worldPoint)
	{
		GetMeleeHitSegment(out Vector3 a, out Vector3 b);
		return DistancePointToSegment3D(worldPoint, a, b);
	}

	/// <summary>
	/// Onko piste pelaajan etupuolella miekkaosumaa varten (XZ-taso).
	/// Etu = <c>Basis.Z</c> (sama suunta kuin liikkeen suunta <c>LookingAt(-dir)</c> + mesh-kompensointi).
	/// </summary>
	public bool IsPointInMeleeHitFacingArc(Vector3 worldPoint)
	{
		if (_characterModel == null || !_characterModel.IsInsideTree() || !IsInsideTree())
			return true;
		return IsWithinFacingArcFromOrigin(MeleeArcOriginWorld(), worldPoint, MiekkaIskuSuuntaPuolikulma);
	}

	/// <summary>Onko piste terän iskulinjan suuntaisessa kartiossa (XZ), kahvasta mitattuna.</summary>
	public bool IsPointInMeleeHitBladeArc(Vector3 worldPoint)
	{
		return IsPointInMeleeHitBladeArcWithHalfAngle(worldPoint, GetCurrentBladeArcHalfAngleDeg());
	}

	private float GetCurrentBladeArcHalfAngleDeg()
		=> _attackDamage >= 3 ? TeräkaariPuolikulmaRaskas : TeräkaariPuolikulmaKevyt;

	private bool IsPointInMeleeHitBladeArcWithHalfAngle(Vector3 worldPoint, float halfAngleDeg)
	{
		GetMeleeHitSegment(out Vector3 a, out Vector3 b);
		Vector3 seg = b - a;
		seg.Y = 0f;
		Vector3 toP = worldPoint - a;
		toP.Y = 0f;
		float segL2 = seg.LengthSquared();
		float toL2 = toP.LengthSquared();
		if (segL2 < 1e-8f || toL2 < 1e-8f)
			return true;
		seg /= Mathf.Sqrt(segL2);
		toP /= Mathf.Sqrt(toL2);
		float cosLimit = Mathf.Cos(Mathf.DegToRad(halfAngleDeg));
		return toP.Dot(seg) >= cosLimit;
	}

	/// <summary>
	/// Yksi osumatesti: etäisyys iskulinjaan + R1 vain teräkartio, R2 terä tai facing (rintaorigolla).
	/// </summary>
	/// <param name="proximityMaxOverride">Jos ≥ 0, käytetään tätä max-etäisyytenä metrienä (esim. korkea bossi).</param>
	public bool CanApplyMeleeHitAtWorldPoint(Vector3 worldPoint, float proximityMaxOverride = -1f)
	{
		if (!IsInsideTree() || !IsMeleeAttackActive() || !IsSwordWeaponMode())
			return false;
		float maxDist = proximityMaxOverride >= 0f ? proximityMaxOverride : GetMeleeHitProximityMax();
		if (GetMeleeHitDistanceToPoint(worldPoint) > maxDist)
			return false;
		bool blade = IsPointInMeleeHitBladeArc(worldPoint);
		bool facing = IsPointInMeleeHitFacingArc(worldPoint);
		if (_attackDamage >= 3)
			return blade;
		return blade || facing;
	}

	public float GetMeleeHitProximityMax()
		=> _attackDamage >= 3 ? RaskasIskuLäheisyysMaksimi : KevytIskuLäheisyysMaksimi;

	private Vector3 MeleeArcOriginWorld()
	{
		if (_characterModel != null && _characterModel.IsInsideTree())
			return _characterModel.GlobalPosition + Vector3.Up * IskunRintaKorkeus;
		return GlobalPosition + Vector3.Up * 0.9f;
	}

	/// <summary>
	/// Kutsutaan EnemyLevel1:stä: yrittää varata miekan swingin tälle osumalle.
	/// Palauttaa true vain kerran per swing — estää yhden swingin tappamasta monta vihollista.
	/// Swingien välissä lippu nollataan automaattisesti <see cref="ClearEnemyHitThisSwing"/>.
	/// </summary>
	public bool TryClaimEnemyMeleeHit()
	{
		if (_enemyHitThisSwing) return false;
		_enemyHitThisSwing = true;
		return true;
	}

	/// <summary>
	/// R1: useampi osuma per swing (EnemyLevel2). Enintään <paramref name="maxHits"/> vihollista voi varata saman swingin aikana.
	/// </summary>
	public bool TryClaimEnemyHeavyCleaveHit(int maxHits = 2)
	{
		if (!IsHeavyMeleeAttackActive() || maxHits < 1) return false;
		if (_heavyMeleeCleaveHitsThisSwing >= maxHits) return false;
		_heavyMeleeCleaveHitsThisSwing++;
		return true;
	}

	/// <summary>Kutsutaan kun hyökkäys on ohi — nollaa swingivaraus.</summary>
	public void ClearEnemyHitThisSwing()
	{
		_enemyHitThisSwing = false;
		_heavyMeleeCleaveHitsThisSwing = 0;
	}

	/// <summary>
	/// L2-blokkaus keskeyttää lyöntianimaation (<c>PlayAnim(006)</c>) ilman että <c>OnAnimationFinished</c> laukeaa —
	/// muuten <c>_isAttacking</c> jäisi päälle eikä seuraava isku rekisteröityisi vihollisille.
	/// </summary>
	private void EndMeleeAttackIfInterruptedByBlock()
	{
		if (!_isAttacking) return;
		_isAttacking = false;
		_meleeStrikeClip = default;
		_meleeHitStopTimer = 0f;
		ClearEnemyHitThisSwing();
		if (_animationPlayer != null)
			_animationPlayer.SpeedScale = 1f;
	}

	/// <summary>Kutsutaan kun miekka osuu viholliseen — lyhyt freeze osumakuvaan.</summary>
	public void NotifyMeleeHitLanded()
	{
		if (!_isAttacking || _animationPlayer == null)
			return;
		if (_meleeHitStopTimer <= 0f)
			_meleeHitStopSeekPos = _animationPlayer.CurrentAnimationPosition;
		_meleeHitStopTimer = IskuJähmetysSekuntia;
	}

	/// <summary>XZ-kartio kilven torjuntaan: origo pelaajan juuresta.</summary>
	private bool IsWithinFacingArcXZ(Vector3 worldPoint, float halfAngleDeg)
		=> IsWithinFacingArcFromOrigin(GlobalPosition, worldPoint, halfAngleDeg);

	private bool IsWithinFacingArcFromOrigin(Vector3 origin, Vector3 worldPoint, float halfAngleDeg)
	{
		if (_characterModel == null || !_characterModel.IsInsideTree())
			return true;
		var to = worldPoint - origin;
		to.Y = 0f;
		if (to.LengthSquared() < 1e-8f) return true;
		to = to.Normalized();
		var forward = _characterModel.GlobalTransform.Basis.Z;
		forward.Y = 0f;
		if (forward.LengthSquared() < 1e-8f) return true;
		forward = forward.Normalized();
		float cosLimit = Mathf.Cos(Mathf.DegToRad(halfAngleDeg));
		return to.Dot(forward) >= cosLimit;
	}

	/// <summary>
	/// Palauttaa iskulinjan keskipisteen (yhteensopivuus vanhan probe-pisteen kanssa).
	/// </summary>
	public Vector3 GetMeleeHitProbeGlobalPosition()
	{
		GetMeleeHitSegment(out Vector3 a, out Vector3 b);
		return (a + b) * 0.5f;
	}

	/// <summary>1 = isku valmis, 0 = juuri käytetty. HUD täyttää palkin tällä.</summary>
	public float GetHeavyAttackCooldownFill01()
	{
		if (_heavyAttackCooldown <= 0f || RaskasHyökkäysJäähdytys <= 0.01f) return 1f;
		return Mathf.Clamp(1f - _heavyAttackCooldown / RaskasHyökkäysJäähdytys, 0f, 1f);
	}

	public bool ShouldShowHeavyCooldownBar() => _heavyCooldownBarUnlocked;

	/// <summary>
	/// Lisää R1-jäähdytyspalkin odotusaikaa (esim. tulevia haasteita varten). BossLevel2 käyttää HP-vahinkoa.
	/// </summary>
	public void ApplyHeavyAttackCooldownPenalty(float seconds)
	{
		if (seconds <= 0f || RaskasHyökkäysJäähdytys <= 0.01f) return;
		_heavyCooldownBarUnlocked = true;
		_heavyAttackCooldown = Mathf.Clamp(_heavyAttackCooldown + seconds, 0f, RaskasHyökkäysJäähdytys);
	}

	private static float DistancePointToSegment3D(Vector3 p, Vector3 a, Vector3 b)
	{
		Vector3 ab = b - a;
		float abLenSq = ab.LengthSquared();
		if (abLenSq < 1e-10f)
			return p.DistanceTo(a);
		float t = Mathf.Clamp((p - a).Dot(ab) / abLenSq, 0f, 1f);
		return p.DistanceTo(a + ab * t);
	}

	/// <summary>
	/// Yhdistää InputMap raw-strengthin ja kaikkien ohjaimien oikean liipasimen akselin (PS5 / Xbox).
	/// </summary>
	private float ReadAggregatedLightAttackAnalog()
	{
		float v = Input.GetActionRawStrength("attack");
		try
		{
			var pads = Input.GetConnectedJoypads();
			int n = pads.Count;
			for (int i = 0; i < n; i++)
			{
				int deviceId = pads[i];
				float ax = NormalizeTrigger01(Input.GetJoyAxis(deviceId, JoyAxis.TriggerRight));
				if (ax > v) v = ax;
			}
		}
		catch (Exception ex)
		{
			GD.PrintErr($"ReadAggregatedLightAttackAnalog: {ex.Message}");
		}
		return Mathf.Clamp(v, 0f, 1f);
	}

	/// <summary>0 = vapaa, 1 = täysi. Älä mapaa pientä negatiivista driftiä → ~0.5 (rikkoi PS5-lukeman).</summary>
	private static float NormalizeTrigger01(float axisValue)
	{
		if (axisValue >= 0f)
			return Mathf.Clamp(axisValue, 0f, 1f);
		if (axisValue <= -0.2f)
			return Mathf.Clamp((axisValue + 1f) * 0.5f, 0f, 1f);
		return 0f;
	}

	// ─────────────────────────────────────────────
	// _READY — suoritetaan kun scene ladataan
	// ─────────────────────────────────────────────

	public override void _Ready()
	{
		// Lattian kiinnitysasetukset — estää hahmon "pomppaavan" portailta
		FloorSnapLength = 0.18f;
		FloorMaxAngle = Mathf.DegToRad(50f);

		// Areena: jos scene asettaa vain leveän SyvyysAlarajan, älä jätä SyvyysYlärajaa oletukseen 1.75
		// (muuten Z-clamp lukitsee koko +Z-puolen ~1.75 m asti.)
		if (SyvyysliikeKäytössä && SyvyysAlaraja < -3f && SyvyysYläraja < 3f)
			SyvyysYläraja = Mathf.Abs(SyvyysAlaraja);

		// Haetaan tarvittavat nodet scene-puusta
		_mesh = GetNode<MeshInstance3D>("MeshInstance3D");
		_characterModel = GetNode<Node3D>("gameover_character");
		if (!Mathf.IsZeroApprox(HahmonPohjanOffsetY))
			_characterModel.Position += new Vector3(0f, HahmonPohjanOffsetY, 0f);
		_healthComponent = GetNode<HealthComponent>("HealthComponent");
		_swordSFX = GetNodeOrNull<AudioStreamPlayer>("SwordSFX");
		_damageSFX = GetNodeOrNull<AudioStreamPlayer>("DamageSFX");

		// Kytketään HealthComponentin signaalit tähän skriptiin
		_healthComponent.HealthChanged += OnHealthChanged;
		_healthComponent.PlayerDied += OnPlayerDied;
		_healthComponent.GameOver += OnGameOver; // ← Game Over -signaali

		// Tallennetaan hahmon alkuperäinen katselusuunta
		_facingYaw = _characterModel.Rotation.Y;

		// Haetaan AnimationPlayer hahmon sisältä
		_animationPlayer = _characterModel.FindChild("AnimationPlayer", true, false) as AnimationPlayer;
		if (_animationPlayer != null)
		{
			// Ladataan kaikki animaatiot FBX-tiedostoista
			// Ensimmäinen parametri = tiedostopolku
			// Toinen = animaation nimi FBX:ssä (Mixamo käyttää "mixamo_com")
			// Kolmas = nimi jonka alla animaatio tallennetaan pelissä
			LoadAnim("res://assets/models/animations/Jumping.fbx",                            "mixamo_com", "mixamo_com_001");
			LoadAnim("res://assets/models/animations/Push Start.fbx",                         "mixamo_com", "mixamo_com_011", loop: true);
			LoadAnim("res://assets/models/animations/Orc Walk.fbx",                           "mixamo_com", "mixamo_com_002");
			LoadAnim("res://assets/models/animations/Running.fbx",                            "mixamo_com", "mixamo_com_003");
			LoadAnim("res://assets/models/animations/sitting.fbx",                            "mixamo_com", "mixamo_com_004");
			LoadAnim("res://assets/models/animations/Sword And Shield Attack.fbx",            "mixamo_com", "mixamo_com_005");
			LoadAnim("res://assets/models/animations/Sword And Shield Crouch Block Idle.fbx", "mixamo_com", "mixamo_com_006");
			LoadAnim("res://assets/models/animations/Sword And Shield Idle.fbx",              "mixamo_com", "mixamo_com_007");
			LoadAnim("res://assets/models/animations/Sword And Shield Run.fbx",               "mixamo_com", "mixamo_com_008");
			LoadAnim("res://assets/models/animations/Sword And Shield SlashLyonti2.fbx",      "mixamo_com", "mixamo_com_010");
			LoadAnim("res://assets/models/animations/Sword And Shield Walk.fbx",              "mixamo_com", "mixamo_com_009");

			_animationPlayer.AnimationFinished += OnAnimationFinished;
			PlayAnim("mixamo_com"); // Oletusanimaatio: idle
		}

		// Haetaan miekka ja kilpi Skeleton3D:n BoneAttachment-nodeista
		// Jos polku ei täsmää, tarkista scene-puusta että nimet ovat samat
		_sword  = GetNodeOrNull<Node3D>("gameover_character/Skeleton3D/SwordAttachment");
		_shield = GetNodeOrNull<Node3D>("gameover_character/Skeleton3D/ShieldAttachment");

		// Piilotetaan aseet oletuksena — tulevat näkyviin SwordShield-tilassa
		if (_sword  != null) _sword.Visible  = false;
		if (_shield != null) _shield.Visible = false;

		// Luodaan drone-joystickin visuaali oikeaan käteen (piilotettu kunnes drone-moodi aktivoituu)
		SetupDroneJoystick();

		// Korjataan tangentit miekalle ja kilpelle (estää shader-varoitukset)
		try
		{
			MeshTangentFix.ApplyToSubtree(_sword);
			MeshTangentFix.ApplyToSubtree(_shield);
		}
		catch (Exception ex)
		{
			GD.PrintErr("MeshTangentFix: " + ex.Message);
		}

		// Piilotetaan ylimääräiset Tripo3D-meshet (jätetään vain ensimmäinen näkyviin)
		// Tripo3D generoi joskus useita mesh-nodeja — tämä piilottaa ylimääräiset
		bool firstMeshFound = false;
		foreach (Node armature in _characterModel.GetChildren())
		{
			if (armature is Node3D)
			{
				foreach (Node child in armature.GetChildren())
				{
					foreach (Node meshNode in child.GetChildren())
					{
						if (meshNode.Name.ToString().StartsWith("tripo_node"))
						{
							if (!firstMeshFound)
							{
								firstMeshFound = true;
							}
							else if (meshNode is Node3D meshNode3D)
							{
								meshNode3D.Visible = false;
							}
						}
					}
				}
			}
		}

		// Arcade-prop kerros 5 (Level1ArcadePhysicsSetup: bitmask 16) — pelaaja törmää, bossin syöksy-maski 1 ei.
		SetCollisionMaskValue(5, true);
	}

	// ─────────────────────────────────────────────
	// _PHYSICSPROCESS — suoritetaan joka fysiikkaframella
	// ─────────────────────────────────────────────

	public override void _PhysicsProcess(double delta)
	{
		if (!IsInsideTree())
			return;

		float dt = (float)delta;
		Vector3 velocity = Velocity;

		if (_heavyAttackCooldown > 0f)
			_heavyAttackCooldown = Mathf.Max(0f, _heavyAttackCooldown - dt);
		if (_lightAttackDebounce > 0f)
			_lightAttackDebounce = Mathf.Max(0f, _lightAttackDebounce - dt);
		if (_lightMeleeCooldown > 0f)
			_lightMeleeCooldown = Mathf.Max(0f, _lightMeleeCooldown - dt);

		float lightAnalog = ReadAggregatedLightAttackAnalog();
		if (lightAnalog < HyökkäysLiipaisinPalautus)
			_lightTriggerArmed = true;

		// ── Painovoima ──
		// Lisätään painovoimaa kun pelaaja on ilmassa
		if (!IsOnFloor())
			velocity.Y -= Painovoima * (float)delta;

		// ── Hyppy ──
		// Hyppy on sallittu vain Normal- ja SwordShield-tiloissa (ei Sitting)
		bool canJump = !_isSitting;

		// Hypyn viiveajastin — odottaa animaation ponnistushetkeä
		if (_isWindingUp)
		{
			_jumpTimer -= (float)delta;
			if (_jumpTimer <= 0f)
			{
				_isWindingUp = false;
				velocity.Y = HypynAlkuvauhti; // Ponnistus!
			}
		}

		// Hyppy käynnistyy kun painetaan "jump" ja ollaan lattialla
		if (Input.IsActionJustPressed("jump") && IsOnFloor() && !_isAttacking && !_isBlocking && canJump && !_isWindingUp)
		{
			PlayAnim("mixamo_com_001");
			_isWindingUp = true;
			_jumpTimer = HypynLämmittelyviive;
		}

		// ── Istuminen (sit) — erillinen syöte; joystick voidaan sitoa tähän myöhemmin
		if (Input.IsActionJustPressed("sit"))
		{
			_isSitting = !_isSitting;
			_isAttacking = false;
			_meleeStrikeClip = default;
			_grabbedBody = null;
			_isBlocking  = false;

			if (_isSitting)
			{
				if (_sword  != null) _sword.Visible  = false;
				if (_shield != null) _shield.Visible = false;
				PlayAnim("mixamo_com_004");
			}
			else
			{
				bool showWeapons = _weaponMode == WeaponMode.SwordShield;
				if (_sword  != null) _sword.Visible  = showWeapons;
				if (_shield != null) _shield.Visible = showWeapons;
				PlayAnim(showWeapons ? "mixamo_com_007" : "mixamo_com");
			}
		}

		// ── Asemoodi (kolmio): drone-moodi jos joystick on poimittu, muuten asevaihto
		if (Input.IsActionJustPressed("toggle_weapon"))
		{
			// Joystick on pelaajan hallussa → kolmio = drone-moodi
			if (GameState.Instance?.HasJoystick == true)
			{
				if (_isDroneMode) ExitDroneMode();
				else              EnterDroneMode();
				return; // ei jatketa normaalia asevaihtoon
			}

			_weaponMode = _weaponMode == WeaponMode.Normal ? WeaponMode.SwordShield : WeaponMode.Normal;
			_isAttacking = false;
			_meleeStrikeClip = default;
			_grabbedBody = null;
			_isBlocking  = false;

			if (!_isSitting)
			{
				if (_weaponMode == WeaponMode.Normal)
				{
					if (_sword  != null) _sword.Visible  = false;
					if (_shield != null) _shield.Visible = false;
					PlayAnim("mixamo_com");
				}
				else
				{
					if (_sword  != null) _sword.Visible  = true;
					if (_shield != null) _shield.Visible = true;
					PlayAnim("mixamo_com_007");
				}
			}
		}

		// ── Puolustus L2 ──
		// Blokkaus toimii vain SwordShield-tilassa
		_isBlocking = Input.IsActionPressed("block") && IsSwordWeaponMode();
		if (_isBlocking && Input.IsActionJustPressed("block"))
			Vibrate(0.2f, 0.1f, 0.1f);
		if (_isBlocking)
		{
			// Estää jumin: blokki korvaa lyönticlipin → AnimationFinished ei tule → nollataan hyökkäystila.
			EndMeleeAttackIfInterruptedByBlock();
			PlayAnim("mixamo_com_006");
		}

		// ── Hyökkäys R2 (normaali isku, vahinko 1) ──
		// 1) Näppäin JustPressed  2) Latch + kynnys  3) Nopea veto (delta), jos liipasin ei ehdi "aseutua"
		bool r2KeyJust = Input.IsActionJustPressed("attack");
		bool r2LatchFire = _lightTriggerArmed
			&& lightAnalog >= HyökkäysLiipaisinLaukaisu;
		bool r2SharpPull = lightAnalog >= KevytIskuPikaVetoMinimi
			&& (lightAnalog - _lightAnalogPreviousFrame) >= KevytIskuPikaVetoNousu;
		bool r2AnalogFire = r2LatchFire || r2SharpPull;
		bool r2Pressed = (r2KeyJust || r2AnalogFire)
			&& _lightAttackDebounce <= 0f
			&& _lightMeleeCooldown <= 0f;

		if (r2Pressed && IsSwordWeaponMode() && !_isBlocking)
		{
			_isAttacking = true;
			_attackDamage = 1;
			_meleeStrikeClip = "mixamo_com_005";
			PlayAnim("mixamo_com_005");
			Vibrate(0.3f, 0.5f, 0.15f);
			_swordSFX?.Play();
			_lightTriggerArmed = false;
			_lightAttackDebounce = KevytIskuPainallustenSuodatus;
			_lightMeleeCooldown = Mathf.Max(0f, KevytIskuToistojäähdytys);
		}

		// ── Hyökkäys R1 (vahva isku, vahinko 3) + cooldown ──
		if (Input.IsActionJustPressed("attack_r1")
			&& IsSwordWeaponMode()
			&& !_isBlocking
			&& _heavyAttackCooldown <= 0f)
		{
			_isAttacking = true;
			_attackDamage = 3;
			_meleeStrikeClip = "mixamo_com_010";
			PlayAnim("mixamo_com_010");
			Vibrate(0.6f, 1.0f, 0.25f);
			_swordSFX?.Play();
			_heavyCooldownBarUnlocked = true;
			_heavyAttackCooldown = RaskasHyökkäysJäähdytys;
		}

		// ── Tarttuminen (neliö / grab) — RigidBody3D ryhmässä "grabbable" ──
		if (Input.IsActionJustPressed("grab"))
		{
			TryBeginGrab();
			if (!_isSitting && !_isAttacking && !_isBlocking)
			{
				_animationPlayer?.Stop();
				_animationPlayer?.Play("mixamo_com_011");
			}
		}
		if (!Input.IsActionPressed("grab"))
			_grabbedBody = null;

		// ── Liikkuminen ──
		// Istumistilassa, blokatessa tai miekan iskun aikana ei voi liikkua
		bool canMove = !_isSitting && !_isBlocking && !_isAttacking;
		float dirX = canMove ? Input.GetAxis("move_left", "move_right") : 0f;
		float dirZ = 0f;

		// Z-liike (syvyys) — vain jos SyvyysliikeKäytössä on päällä
		if (canMove && SyvyysliikeKäytössä)
			dirZ = SyvyysSyötteenEtumerkki * Input.GetAxis("move_back", "move_forward");

		Vector2 planarInput = new(dirX, dirZ);

		// Normal-tilassa juostaan, SwordShield-tilassa kävellään
		float moveSpeed = Kävelynopeus;
		if (canMove && planarInput.LengthSquared() > 1e-6f && _weaponMode == WeaponMode.Normal)
			moveSpeed = Juoksunopeus;

		Vector3 wish = Vector3.Zero;
		if (planarInput.LengthSquared() > 1e-6f)
		{
			planarInput = planarInput.Normalized();
			var cam = GetViewport()?.GetCamera3D();

			if (cam != null && cam.IsInsideTree())
			{
				// Liike suhteessa kameran katselusuuntaan
				// Näin "eteen" tarkoittaa aina ruudun "eteen" riippumatta kameran kulmasta
				Vector3 lookFlat = -cam.GlobalBasis.Z;
				lookFlat.Y = 0f;
				if (lookFlat.LengthSquared() < 1e-8f)
					lookFlat = new Vector3(0f, 0f, -1f);
				lookFlat = lookFlat.Normalized();

				// Kameran oikea suunta XZ-tasossa
				Vector3 camRight = lookFlat.Cross(Vector3.Up).Normalized();

				// Yhdistetään vaaka- ja syvyysliike kameran suuntaan nähden
				wish = (camRight * planarInput.X + lookFlat * (-planarInput.Y)) * moveSpeed;
			}
			else
			{
				// Varasuunta jos kameraa ei löydy
				wish = new Vector3(planarInput.X, 0f, planarInput.Y) * moveSpeed;
			}
		}

		// Slide lattian pintaa pitkin (estää "kellumisen" rinteillä)
		if (IsOnFloor() && !_isWindingUp)
			wish = wish.Slide(GetFloorNormal());

		// Tarttuessa (neliö pohjassa) liike rajoitetaan vain pöytää kohti.
		// Sivuttaisliike ja taaksepäin kävely estetään kokonaan.
		if (_grabbedBody != null && Input.IsActionPressed("grab")
			&& GodotObject.IsInstanceValid(_grabbedBody) && _grabbedBody.IsInsideTree())
		{
			var toObj = _grabbedBody.GlobalPosition - GlobalPosition;
			toObj.Y = 0f;
			if (toObj.LengthSquared() > 1e-5f)
			{
				var pushDir = toObj.Normalized();
				float fwd = new Vector3(wish.X, 0f, wish.Z).Dot(pushDir);
				fwd = Mathf.Max(0f, fwd);
				wish.X = pushDir.X * fwd;
				wish.Z = pushDir.Z * fwd;
			}
			else
			{
				wish.X = 0f;
				wish.Z = 0f;
			}
		}

		velocity.X = wish.X;
		velocity.Z = wish.Z;
		if (IsOnFloor() && !_isWindingUp && velocity.Y < HypynAlkuvauhti * 0.25f)
			velocity.Y = wish.Y;

		// ── Hahmon kääntyminen ──
		// Työntäessä katsotaan kohdetta (push-suunta), muuten liikkeen suuntaan.
		Vector3 wishHorizontal = new(wish.X, 0f, wish.Z);
		bool grabbing = _grabbedBody != null && Input.IsActionPressed("grab")
			&& GodotObject.IsInstanceValid(_grabbedBody) && _grabbedBody.IsInsideTree();
		if (grabbing)
		{
			var toObj = _grabbedBody.GlobalPosition - GlobalPosition;
			toObj.Y = 0f;
			if (toObj.LengthSquared() > 1e-5f)
			{
				var dirObj = toObj.Normalized();
				var targetYaw = Basis.LookingAt(-dirObj, Vector3.Up).GetEuler(EulerOrder.Yxz).Y;
				float s = SuunnanPehmennys * Mathf.Max(0.01f, TartuntaKääntöPehmennysKerroin);
				if (s <= 0.01f)
					_facingYaw = targetYaw;
				else
					_facingYaw = Mathf.LerpAngle(_facingYaw, targetYaw, 1f - Mathf.Exp(-s * dt));
				_characterModel.Rotation = new Vector3(0f, _facingYaw, 0f);
			}
		}
		else if (wishHorizontal.LengthSquared() > 1e-5f)
		{
			var dir = wishHorizontal.Normalized();
			var targetYaw = Basis.LookingAt(-dir, Vector3.Up).GetEuler(EulerOrder.Yxz).Y;
			if (SuunnanPehmennys <= 0.01f)
				_facingYaw = targetYaw;
			else
				_facingYaw = Mathf.LerpAngle(_facingYaw, targetYaw, 1f - Mathf.Exp(-SuunnanPehmennys * dt));

			_characterModel.Rotation = new Vector3(0f, _facingYaw, 0f);
		}

	if (grabbing)
		ApplyGrabPull(new Vector3(wish.X, 0f, wish.Z));

		// ── Liike-animaatiot ──
		// Vaihdetaan animaatiota tilanteen mukaan
		// Ei päällekirjoiteta hyppy- tai hyökkäysanimaatioita
		bool jumpPlaying = _animationPlayer?.CurrentAnimation == "mixamo_com_001" || _isWindingUp;
		bool grabPlaying = _grabbedBody != null && Input.IsActionPressed("grab");
		if (IsOnFloor() && !_isAttacking && !_isBlocking && !jumpPlaying && !grabPlaying)
		{
			string target;
			if (_isSitting)
				target = "mixamo_com_004";
			else if (_weaponMode == WeaponMode.SwordShield)
				target = planarInput.LengthSquared() > 0.01f ? "mixamo_com_009" : "mixamo_com_007";
			else
				target = planarInput.LengthSquared() > 0.01f ? "mixamo_com_003" : "mixamo_com";
			PlayAnim(target);
		}

		// Testitoiminto — ottaa vahinkoa (poista julkaisusta)
		if (Input.IsActionJustPressed("test_damage"))
			_healthComponent.TakeDamage(1);

		Velocity = velocity;
		MoveAndSlide();

		// ── Z-akselin rajaus ──
		// Rajoittaa pelaajan Z-liikettä kun SyvyysliikeKäytössä on päällä
		// Muuta SyvyysAlaraja / SyvyysYläraja Inspectorissa kentän koon mukaan
		if (SyvyysliikeKäytössä)
		{
			Vector3 p = GlobalPosition;
			p.Z = Mathf.Clamp(p.Z, SyvyysAlaraja, SyvyysYläraja);
			GlobalPosition = p;
		}

		_lightAnalogPreviousFrame = lightAnalog;

		if (_meleeHitStopTimer > 0f)
		{
			_meleeHitStopTimer = Mathf.Max(0f, _meleeHitStopTimer - dt);
			if (_animationPlayer != null && _isAttacking)
			{
				_animationPlayer.SpeedScale = 0f;
				_animationPlayer.Seek(_meleeHitStopSeekPos, true);
			}
			if (_meleeHitStopTimer <= 0f && _animationPlayer != null)
				_animationPlayer.SpeedScale = 1f;
		}
		else if (_animationPlayer != null && _animationPlayer.SpeedScale == 0f && !_isAttacking)
			_animationPlayer.SpeedScale = 1f;

		float swingDt = (_meleeHitStopTimer > 0f) ? 0f : dt;
		if (_isAttacking)
			_meleeSwingElapsed += swingDt;
		else
			_meleeSwingElapsed = 0f;
	}

	// ─────────────────────────────────────────────
	// ANIMAATIOIDEN LATAUS
	// ─────────────────────────────────────────────

	/// <summary>
	/// Lataa animaation FBX-tiedostosta ja lisää sen AnimationPlayeriin.
	/// path       = tiedostopolku (res://...)
	/// sourceName = animaation nimi FBX:ssä
	/// targetName = nimi jonka alla tallennetaan pelissä
	/// </summary>
	private void LoadAnim(string path, string sourceName, string targetName, bool loop = false)
	{
		var scene = GD.Load<PackedScene>(path);
		if (scene == null)
		{
			GD.PrintErr($"Animaatiotiedostoa ei löydy: {path}");
			return;
		}

		var inst = scene.Instantiate();
		var ap = inst.FindChild("AnimationPlayer", true, false) as AnimationPlayer;
		if (ap == null)
		{
			GD.PrintErr($"AnimationPlayer puuttuu: {path}");
			inst.QueueFree();
			return;
		}

		// Kokeillaan molempia nimiä (Mixamo käyttää joskus pisteitä alaviivan sijaan)
		Animation anim = null;
		foreach (var name in new[] { sourceName, sourceName.Replace("_", ".") })
		{
			if (ap.HasAnimation(name))
			{
				anim = ap.GetAnimation(name);
				break;
			}
		}

		if (anim == null)
		{
			GD.PrintErr($"Animaatiota '{sourceName}' ei löydy: {path}");
			GD.Print($"  Saatavilla: {string.Join(", ", ap.GetAnimationList())}");
			inst.QueueFree();
			return;
		}

		// Lisätään animaatio AnimationPlayeriin
		AnimationLibrary lib;
		if (_animationPlayer.HasAnimationLibrary(""))
			lib = _animationPlayer.GetAnimationLibrary("");
		else
		{
			lib = new AnimationLibrary();
			_animationPlayer.AddAnimationLibrary("", lib);
		}

		if (lib.HasAnimation(targetName))
			lib.RemoveAnimation(targetName);
		if (loop)
			anim.LoopMode = Animation.LoopModeEnum.Linear;
		lib.AddAnimation(targetName, anim);
		GD.Print($"Ladattu animaatio: {targetName}");
		inst.QueueFree();
	}

	/// <summary>
	/// Toistaa animaation nimellä.
	/// Ei tee mitään jos sama animaatio on jo käynnissä.
	/// </summary>
	private void PlayAnim(string name)
	{
		if (_animationPlayer == null) return;
		if (_animationPlayer.CurrentAnimation == name) return;
		_animationPlayer.Play(name);
	}

	// ─────────────────────────────────────────────
	// SIGNAALIKÄSITTELIJÄT
	// ─────────────────────────────────────────────

	/// <summary>
	/// Kutsutaan kun animaatio loppuu.
	/// Nollaa hyökkäys- ja hyppytilan oikeaan aikaan.
	/// </summary>
	private void OnAnimationFinished(StringName animName)
	{
		// Normaali lyönti loppui
		if (animName == "mixamo_com_005")
		{
			_isAttacking = false;
			_meleeStrikeClip = default;
			_meleeHitStopTimer = 0f;
			if (_animationPlayer != null)
				_animationPlayer.SpeedScale = 1f;
		}

		// Vahva lyönti loppui
		if (animName == "mixamo_com_010")
		{
			_isAttacking = false;
			_meleeStrikeClip = default;
			_meleeHitStopTimer = 0f;
			if (_animationPlayer != null)
				_animationPlayer.SpeedScale = 1f;
		}

		// Hyppyanimaatio loppui — palataan idle:en
		if (animName == "mixamo_com_001")
			PlayAnim("mixamo_com");

		// Tartunta (loop pois päältä / vapautus keskellä)
		if (animName == "mixamo_com_011" && (_grabbedBody == null || !GodotObject.IsInstanceValid(_grabbedBody)))
			PlayAnim(_weaponMode == WeaponMode.SwordShield && !_isSitting ? "mixamo_com_007" : "mixamo_com");
	}

	/// <summary>
	/// Kutsutaan HealthComponentilta kun HP muuttuu.
	/// Tähän voi myöhemmin lisätä HUD-päivityksen.
	/// </summary>
	private void OnHealthChanged(int currentHealth, int maxHealth)
	{
		GD.Print($"HP: {currentHealth}/{maxHealth}");
		Vibrate(0.8f, 0.8f, 0.3f);
		if (currentHealth <= 0)
			_damageSFX?.Play();
		// TODO: päivitä HUD tässä
	}

	/// <summary>
	/// Kutsutaan HealthComponentilta kun HP loppuu mutta elämiä on jäljellä.
	/// Pelaaja respawnataan viimeiseltä checkpointilta.
	/// </summary>
	private void OnPlayerDied()
	{
		GD.Print("Pelaaja kuoli — respawn!");
		Respawn(Checkpoint.LastPosition);
	}

	/// <summary>
	/// Kutsutaan HealthComponentilta kun kaikki elämät on käytetty.
	/// Tällä hetkellä lataa scenen uudelleen — myöhemmin lisätään Game Over -valikko.
	/// </summary>
	private void OnGameOver()
	{
		GD.Print("GAME OVER!");
		GetTree().ChangeSceneToFile("res://scenes/ui/game_over.tscn");
	}

	// ─────────────────────────────────────────────
	// RESPAWN
	// ─────────────────────────────────────────────

	/// <summary>
	/// Siirtää pelaajan annettuun sijaintiin ja nollaa tilan.
	/// Kutsutaan KillZonesta, checkpointista tai Game Overista.
	/// </summary>
	public void Respawn(Vector3 position)
	{
		GlobalPosition = position;
		Velocity       = Vector3.Zero;
		_isAttacking   = false;
		_meleeStrikeClip = default;
		_meleeSwingElapsed = 0f;
		_grabbedBody   = null;
		_isBlocking    = false;
		_isWindingUp   = false;
		_jumpTimer     = 0f;
		_weaponMode = WeaponMode.Normal;
		_isSitting  = false;
		_heavyAttackCooldown = 0f;
		_lightAttackDebounce = 0f;
		_lightMeleeCooldown = 0f;
		_lightTriggerArmed = true;
		_lightAnalogPreviousFrame = 0f;

		if (_sword  != null) _sword.Visible  = false;
		if (_shield != null) _shield.Visible = false;

		PlayAnim("mixamo_com");
	}

	/// <summary>
	/// Täristää ohjainta.
	/// device    = ohjaimen numero (0 = ensimmäinen ohjain)
	/// weak      = heikko moottori (0.0 - 1.0)
	/// strong    = vahva moottori (0.0 - 1.0)
	/// duration  = kesto sekunteina
	/// </summary>
	private void Vibrate(float weak, float strong, float duration)
	{
		Input.StartJoyVibration(0, weak, strong, duration);
	}

	// ─────────────────────────────────────────────
	// DRONE-MOODI (joystick kädessä)
	// ─────────────────────────────────────────────

	/// <summary>
	/// Luo yksinkertaisen joystick-visuaalin oikeaan käteen (SwordAttachment-boneen).
	/// Käytetään CylinderMesh + SphereMesh -yhdistelmää. Korvaa myöhemmin oikealla mallilla.
	/// </summary>
	private void SetupDroneJoystick()
	{
		var swordAttach = GetNodeOrNull<Node3D>("gameover_character/Skeleton3D/SwordAttachment");
		if (swordAttach == null) return;

		// Varsi (lieriö)
		var handle = new MeshInstance3D { Name = "DroneJoystick", Visible = false };
		handle.Mesh = new CylinderMesh
		{
			Height       = 0.11f,
			TopRadius    = 0.014f,
			BottomRadius = 0.02f,
		};
		// Sijoitus kämmeneen — säädä tarvittaessa Inspectorista tai suoraan tästä
		handle.Position = new Vector3(0f, -0.04f, 0.02f);

		// Nappi/pallo ylhäällä
		var ball = new MeshInstance3D { Name = "DroneJoystickBall" };
		ball.Mesh = new SphereMesh { Radius = 0.026f, Height = 0.052f };
		ball.Position = new Vector3(0f, 0.07f, 0f);
		handle.AddChild(ball);

		var mat = new StandardMaterial3D { AlbedoColor = new Color(0.12f, 0.12f, 0.14f) };
		handle.MaterialOverride = mat;
		ball.MaterialOverride   = mat;

		swordAttach.AddChild(handle);
		_droneJoystick = handle;
	}

	/// <summary>Aktivoi drone-moodin: pelaaja istuu, joystick tulee käteen.</summary>
	private void EnterDroneMode()
	{
		_isDroneMode     = true;
		_isSitting       = true;
		_isAttacking     = false;
		_meleeStrikeClip = default;
		_grabbedBody     = null;
		_isBlocking      = false;

		if (_sword         != null) _sword.Visible         = false;
		if (_shield        != null) _shield.Visible        = false;
		if (_droneJoystick != null) _droneJoystick.Visible = true;

		PlayAnim("mixamo_com_004"); // istumisanimaatio
		GD.Print("DroneMode: aktivoitu — joystick kädessä.");
	}

	/// <summary>Poistaa drone-moodin: pelaaja nousee ylös, joystick häviää kädestä.</summary>
	private void ExitDroneMode()
	{
		_isDroneMode = false;
		_isSitting   = false;

		if (_droneJoystick != null) _droneJoystick.Visible = false;

		bool showWeapons = _weaponMode == WeaponMode.SwordShield;
		if (_sword  != null) _sword.Visible  = showWeapons;
		if (_shield != null) _shield.Visible = showWeapons;

		PlayAnim(showWeapons ? "mixamo_com_007" : "mixamo_com");
		GD.Print("DroneMode: poistettu.");
	}

	// ─────────────────────────────────────────────

	private void TryBeginGrab()
	{
		if (_characterModel == null || !IsInsideTree())
			return;
		if (_isSitting || _isAttacking || _isBlocking)
			return;

		// Lähin grabbable XZ-tasossa — ei vaadi "edestä" -asentoa; työntö kääntää hahmon kohti kohdetta.
		RigidBody3D best = null;
		float bestDistSq = float.MaxValue;
		foreach (var n in GetTree().GetNodesInGroup("grabbable"))
		{
			if (n is not RigidBody3D rb || !GodotObject.IsInstanceValid(rb) || !rb.IsInsideTree())
				continue;
			var to = rb.GlobalPosition - GlobalPosition;
			to.Y = 0f;
			float dSq = to.LengthSquared();
			float d = Mathf.Sqrt(dSq);
			if (d > TartuntaEtäisyys || d < 0.06f)
				continue;
			if (dSq < bestDistSq)
			{
				bestDistSq = dSq;
				best = rb;
			}
		}

		_grabbedBody = best;
	}

	private void ApplyGrabPull(Vector3 playerWishXZ)
	{
		if (_grabbedBody == null || !GodotObject.IsInstanceValid(_grabbedBody) || !_grabbedBody.IsInsideTree())
		{
			_grabbedBody = null;
			return;
		}

		if (_characterModel == null || !_characterModel.IsInsideTree() || !IsInsideTree())
		{
			_grabbedBody = null;
			return;
		}

		var toRb = _grabbedBody.GlobalPosition - GlobalPosition;
		toRb.Y = 0f;
		if (toRb.Length() > TartuntaEtäisyys * 1.35f)
		{
			_grabbedBody = null;
			return;
		}

		var pushDir = toRb.Normalized();

		// Työntövoima = pelaajan liikevektorin projektio kohti pöytää.
		// Paikallaan seisominen ei liikuta pöytää; kävely kohti pöytää siirtää sitä.
		// Negatiivinen projektio (pelaaja kävelee poispäin) nollataan — ei vedetä takaisin.
		float speed = Mathf.Clamp(playerWishXZ.Dot(pushDir), 0f, TartuntaLiikeMaxNopeus);

		var lv = _grabbedBody.LinearVelocity;
		_grabbedBody.LinearVelocity = new Vector3(
			pushDir.X * speed,
			lv.Y,
			pushDir.Z * speed);
	}
}
