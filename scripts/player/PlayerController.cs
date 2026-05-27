using System;
using System.Collections.Generic;
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
	/// Siirtää gameover_character-mallia paikallisesti Y-suunnassa (metriä). Negatiivinen = jalat lähemmäs maata.
	/// Kapseli (CollisionShape3D) ja FBX:n origo eivät usein täsmää; säädä tästä ennen kuin muokkaat sceneä uudestaan.
	/// </summary>
	[Export] public float HahmonPohjanOffsetY = 0f;

	/// <summary>
	/// Kun true, joystick-malli näkyy istuessa ilman <see cref="GameState.HasJoystick"/> (esim. level_3 playtest).
	/// Kampanjassa false — keräyksen jälkeen HasJoystick riittää.
	/// </summary>
	[Export] public bool JoystickInHandAlwaysWhenSitting = false;

	/// <summary>Kolmio: sekunteja ennen kuin miekka/kilpi näkyy kädessä (syötettä ei lukita).</summary>
	[Export] public float MiekkaKilpiNäkyviinViive = 0.38f;

	[ExportGroup("Joystick glb (oikea käsi)")]
	/// <summary>Paikallinen siirtymä <c>SwordAttachment</c> / mixamorig_RightHand -akselissa (metriä).</summary>
	[Export] public Vector3 JoystickHandLocalPosition = new Vector3(0.02f, -0.05f, 0.04f);
	/// <summary>Euler-kulmat asteina (Godot YXZ).</summary>
	[Export] public Vector3 JoystickHandLocalEulerDeg = new Vector3(-88f, 8f, -6f);
	[Export] public Vector3 JoystickHandLocalScale = new Vector3(0.42f, 0.42f, 0.42f);

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

	/// <summary>Miekkasoundi kerran per swing — vasta kun osumaikkuna alkaa, ei R2-painalluksessa.</summary>
	private bool _meleeStrikeSfxPlayedThisSwing;

	private float _meleeHitStopTimer;
	private double _meleeHitStopSeekPos;

	private RigidBody3D _grabbedBody;

	/// <summary>Kapselin säde (CollisionShape3D / CapsuleShape3D) — välys lasketaan törmäyspinnalle, ei vain juureen.</summary>
	private float _playerCapsuleRadius = 0.5f;

	/// <summary>R2: liipasin uudelleen "sallittu" kun akseli on päästetty tarpeeksi alas (latch).</summary>
	private bool _lightTriggerArmed = true;

	/// <summary>R2: edellisen fysiikkframen analogi (nopea veto -tunnistus).</summary>
	private float _lightAnalogPreviousFrame;

	/// <summary>R2: estää tuplapainallukset / vapinaa (sekunteja).</summary>
	private float _lightAttackDebounce;

	private float _lightMeleeCooldown;

	private float _heavyAttackCooldown;

	private bool _heavyCooldownBarUnlocked;

	private float _weaponDrawTimer;

	/// <summary>Oikean käden BoneAttachment (miekka + mahdollinen joystick-lapsi).</summary>
	private Node3D _sword;

	/// <summary>Miekan mesh (<c>miekka</c>) — näkyvyys erikseen, jotta joystick voi olla SwordAttachmentin lapsena.</summary>
	private Node3D _swordMeshVisual;

	/// <summary>Kilpi-node — haetaan Skeleton3D:n alta. Näkyy vain SwordShield-tilassa.</summary>
	private Node3D _shield;

	/// <summary>Duplikaatti <c>miekka</c> selkäluun BoneAttachmentissa — näkyy vain normaalitilassa (ei istu/drone).</summary>
	private Node3D _swordBackVisual;

	/// <summary>Duplikaatti <c>kilpi</c> selässä — sama logiikka kuin <see cref="_swordBackVisual"/>.</summary>
	private Node3D _shieldBackVisual;

	/// <summary>Drone-joystickin visuaali oikeassa kädessä — luodaan koodissa SetupDroneJoystick() jos scene:ssä ei ole joystick.glb.</summary>
	private Node3D _droneJoystick;

	/// <summary>Scene-puun joystick.glb -instanssi — siirretään SwordAttachmentiin _Readyssä.</summary>
	private Node _handJoystickSceneRoot;

	/// <summary>True kun drone-moodi on aktiivinen (kolmio + HasJoystick).</summary>
	private bool _isDroneMode = false;

	private AudioStreamPlayer _swordSFX;

	/// <summary>gameover_character juuren skaala _Readyssä — palautetaan vahinkoreaktion tweenissä.</summary>
	private Vector3 _characterModelBaseScale = Vector3.One;

	private Tween _damageHitBodyTween;

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
	public bool IsSwordWeaponMode() => !_isSitting && _weaponMode == WeaponMode.SwordShield && _weaponDrawTimer <= 0f;

	/// <summary>Palauttaa true jos pelaaja blokkaa kilvillä juuri nyt.</summary>
	public bool IsBlocking() => _isBlocking;

	/// <summary>HUD / erikoisase: istutaan ja joystick on käytössä (sama ehto kuin käsimalli).</summary>
	public bool IsSpecialWeaponJoystickContextActive()
		=> _isSitting && (JoystickInHandAlwaysWhenSitting || GameState.Instance?.HasJoystick == true);

	/// <summary>
	/// Liikkeen syöte (<c>move_*</c> = vasen tat + WASD). HUD-ikonin kallistus; myöhemmin erikoisaseen ohjaus.
	/// </summary>
	public Vector2 GetSpecialWeaponStickVector()
	{
		Vector2 v = Input.GetVector("move_left", "move_right", "move_forward", "move_back");
		return new Vector2(Mathf.Clamp(v.X, -1f, 1f), Mathf.Clamp(v.Y, -1f, 1f));
	}

	/// <summary>
	/// R2 / kevyt isku: yhdistetty analogi 0–1 (näppäin + kaikkien padien oikea liipasin). Level3-drone pommi käyttää.
	/// </summary>
	public float GetLightAttackTriggerAnalog() => ReadAggregatedLightAttackAnalog();

	/// <summary>
	/// True vain jos kilpi on ylhäällä ja uhka on edessä (kapea kartio). Käytä purema-/iskutarkistuksissa.
	/// Lukee syötteen suoraan — ei riipu <see cref="_isBlocking"/>-välimuistista (vihollisen fysiikka voi ajaa ennen pelaajaa).
	/// </summary>
	/// <param name="shieldHalfAngleDegreesOverride">
	/// Jos &gt; 0, käytetään tätä puolikulmaa (asteina) kartiolle; muuten <see cref="KilpiTorjuntaPuolikulma"/>.
	/// Boss L1 spin käyttää leveämpää kartiota. (Arvo 0 = sama kuin oletus — ei “suljettu kartio”.)
	/// </param>
	/// <param name="flipShieldFacing180">
	/// Kun true, kilven “edessä”-akseli käyttää <c>+Basis.Z</c> eikä <c>-Basis.Z</c> (EnemyLevel2 lisko + sivukamera).
	/// Älä käytä level_1 -poluilla — susi/boss pysyvät oletuksella.
	/// </param>
	public bool IsBlockingEffectiveAgainst(Vector3 threatWorldPosition, float shieldHalfAngleDegreesOverride = -1f, bool flipShieldFacing180 = false)
	{
		bool blockHeld = Input.IsActionPressed("block") || Input.GetActionStrength("block") > 0.42f;
		if (!blockHeld || !IsSwordWeaponMode()) return false;
		if (_characterModel == null || !_characterModel.IsInsideTree() || !IsInsideTree())
			return false;
		float half = shieldHalfAngleDegreesOverride > 0f ? shieldHalfAngleDegreesOverride : KilpiTorjuntaPuolikulma;

		// Kamera kohti uhkaa (blokatessa ei voi kääntyä liikkeellä — raaka -Z jätti level_1-suden läpi).
		Vector3 camFwd = GetCameraFlatShieldForwardTowardThreat(threatWorldPosition);
		if (IsShieldBlockFacingArcWithForward(GlobalPosition, threatWorldPosition, half, camFwd))
			return true;

		// Hahmo: Mixamo-kilpi -Z tai +Z — ensin vihollisen preferoima, sitten toinen akseli.
		if (IsShieldBlockFacingArc(GlobalPosition, threatWorldPosition, half, flipShieldFacing180))
			return true;
		if (IsShieldBlockFacingArc(GlobalPosition, threatWorldPosition, half, !flipShieldFacing180))
			return true;

		return false;
	}

	/// <summary>Live-kilpitarkistus (ei riipu _isBlocking-välimuistista — vihollinen voi ajaa ennen pelaajaa).</summary>
	public bool IsShieldBlockHeldLive()
		=> (Input.IsActionPressed("block") || Input.GetActionStrength("block") > 0.42f) && IsSwordWeaponMode();

	/// <summary>Torjuttu isku (Boss L1 spin / isku) — ei HP-tappiota, kevyt palaute.</summary>
	public void NotifyBossLevel1StrikeBlocked()
	{
		if (GetViewport()?.GetCamera3D() is CameraFollow cf)
			cf.ShakeImpulse(0.1f, 0.14f);

		Vibrate(0.35f, 0.55f, 0.1f);
		PlayShieldBlockFlashVisual();
	}

	/// <summary>
	/// Palaute kun vihollinen (esim. käärme) vie HP:ta — värinä + lyhyt visuaalinen “energia”-välähdys; ei keskeytä miekkalyöntiä.
	/// </summary>
	public void NotifyEnemyEnergyDrainHit()
	{
		Vibrate(0.52f, 0.52f, 0.2f);
		PlayEnergyDrainFlashVisual();
	}

	/// <summary>
	/// Level 1 susipurema: ruututärinä + lyhyt punainen välähdys + kevyt hahmon iskureaktio jos HP ei ole nollassa (<see cref="OnHealthChanged"/> hoitaa elämän menetyksen).
	/// </summary>
	public void NotifyLevel1BiteHit()
	{
		if (GetViewport()?.GetCamera3D() is CameraFollow cf)
			cf.ShakeImpulse(0.19f, 0.24f);

		if (_healthComponent != null && _healthComponent.GetCurrentHealth() > 0)
			PlayDamageHitBodyTween(strong: false);

		PlayBiteDamageFlashVisual();
	}

	/// <summary>Boss Level 1 MMA-potku osuu: voimakkaampi palaute kuin purema (HP väheni juuri).</summary>
	public void NotifyBossLevel1MmaKickHit()
	{
		if (GetViewport()?.GetCamera3D() is CameraFollow cf)
		{
			cf.ApplyMomentaryDistanceOffset(0.2f, 0.14f);
			cf.ShakeImpulse(0.28f, 0.32f);
		}

		Vibrate(0.62f, 1f, 0.16f);

		if (_healthComponent != null && _healthComponent.GetCurrentHealth() > 0)
			PlayDamageHitBodyTween(strong: true);

		PlayBossMmaKickFlashVisual();
	}

	/// <summary>Kutsutaan kun joystick kerätään level_1:ssä — päivittää käsimallin jos pelaaja jo istuu.</summary>
	public void SyncHandJoystickAfterPickup() => RefreshHandJoystickVisibility();

	/// <summary>Miekka esiin / piiloon: lyhyt zoom-pulse + kevyt ruututärinä + ohjainvärinä (Pi-ystävällinen, ei erillisiä meshejä).</summary>
	private void PlayWeaponToggleFeedback(bool toSwordShield)
	{
		if (GetViewport()?.GetCamera3D() is CameraFollow cf)
		{
			cf.ApplyMomentaryDistanceOffset(toSwordShield ? -0.2f : 0.12f, 0.13f);
			cf.ShakeImpulse(toSwordShield ? 0.065f : 0.045f, 0.14f);
		}

		Vibrate(toSwordShield ? 0.2f : 0.1f, toSwordShield ? 0.32f : 0.16f, 0.085f);
	}

	/// <summary>Käynnistää piirtymisviiveen: meshet piilossa kunnes <see cref="FinishWeaponEquipVisual"/>.</summary>
	private void BeginWeaponEquipVisualDelay()
	{
		float d = MiekkaKilpiNäkyviinViive;
		_weaponDrawTimer = d <= 0.001f ? 0f : d;
		RefreshWeaponCarryVisuals();
		if (_weaponDrawTimer <= 0f)
			FinishWeaponEquipVisual();
	}

	private void FinishWeaponEquipVisual()
	{
		if (_weaponMode != WeaponMode.SwordShield || _isSitting)
		{
			RefreshWeaponCarryVisuals();
			return;
		}

		RefreshWeaponCarryVisuals();
		PlayWeaponToggleFeedback(true);
	}

	/// <summary>Käsi vs selkä: kädessä vain miekka+kilpi -tilassa (ei piirtymisviiveen aikana); selässä normaalitilassa kun ei istu eikä drone.</summary>
	private void RefreshWeaponCarryVisuals()
	{
		bool sittingOrDrone = _isSitting || _isDroneMode;
		bool equipping = _weaponDrawTimer > 0f;
		bool handVisible = !sittingOrDrone && _weaponMode == WeaponMode.SwordShield && !equipping;
		bool backVisible = !sittingOrDrone && _weaponMode == WeaponMode.Normal && !equipping;

		SetSwordMeshVisible(handVisible);
		if (_shield != null)
			_shield.Visible = handVisible;

		if (_swordBackVisual != null)
			_swordBackVisual.Visible = backVisible;
		if (_shieldBackVisual != null)
			_shieldBackVisual.Visible = backVisible;
	}

	/// <summary>
	/// Selkäaseet: <c>gameover_character.tscn</c> → <c>Skeleton3D/BackSwordAttachment/miekka_selkä</c> ja <c>.../kilpi_selkä</c>.
	/// Muokkaa asentoa 3D-näkymässä avaamalla tuo scene (ei player.tscn).
	/// </summary>
	private void SetupBackWeaponCarry()
	{
		_swordBackVisual = _characterModel?.GetNodeOrNull<Node3D>("Skeleton3D/BackSwordAttachment/miekka_selkä");
		_shieldBackVisual = _characterModel?.GetNodeOrNull<Node3D>("Skeleton3D/BackShieldAttachment/kilpi_selkä");

		if (_swordBackVisual != null)
		{
			try
			{
				MeshTangentFix.ApplyToSubtree(_swordBackVisual);
			}
			catch (Exception ex)
			{
				GD.PrintErr("MeshTangentFix (selkämiekka): " + ex.Message);
			}
		}

		if (_shieldBackVisual != null)
		{
			try
			{
				MeshTangentFix.ApplyToSubtree(_shieldBackVisual);
			}
			catch (Exception ex)
			{
				GD.PrintErr("MeshTangentFix (selkäkilpi): " + ex.Message);
			}
		}

		if (_swordBackVisual == null && _shieldBackVisual == null)
			GD.PrintErr("Player: selkäaseet puuttuvat — odotettiin gameover_character/Skeleton3D/Back*Attachment/miekka_selkä ja kilpi_selkä.");
	}

	/// <summary>Lyhyt skaala-“isku” vahinkopalautteessa (ei ääntä, kevyt GPU-kuorma).</summary>
	private void PlayDamageHitBodyTween(bool strong)
	{
		if (_characterModel == null || !GodotObject.IsInstanceValid(_characterModel) || !IsInsideTree())
			return;

		_damageHitBodyTween?.Kill();
		_characterModel.Scale = _characterModelBaseScale;

		float peak = strong ? 1.042f : 1.028f;
		var t = CreateTween();
		_damageHitBodyTween = t;
		Vector3 b = _characterModelBaseScale;
		t.TweenProperty(_characterModel, "scale", b * peak, 0.038f)
			.SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
		t.TweenProperty(_characterModel, "scale", b * 0.988f, 0.055f)
			.SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.In);
		t.TweenProperty(_characterModel, "scale", b, 0.11f)
			.SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
	}

	private void PlayBiteDamageFlashVisual()
	{
		if (_characterModel == null || !GodotObject.IsInstanceValid(_characterModel) || !IsInsideTree())
			return;

		var geos = new List<GeometryInstance3D>();
		CollectGeometryInstancesForDrainFx(_characterModel, geos);
		if (geos.Count == 0)
			return;

		var flashMat = new StandardMaterial3D
		{
			ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
			AlbedoColor = new Color(1f, 0.35f, 0.32f),
			EmissionEnabled = true,
			Emission = new Color(1f, 0.15f, 0.12f),
			EmissionEnergyMultiplier = 2.2f,
		};
		foreach (var g in geos)
			if (GodotObject.IsInstanceValid(g))
				g.MaterialOverride = flashMat;

		var t = CreateTween();
		t.TweenInterval(0.055f);
		t.TweenCallback(Callable.From(() =>
		{
			foreach (var g in geos)
				if (GodotObject.IsInstanceValid(g))
					g.MaterialOverride = null;
		}));
	}

	private void PlayBossMmaKickFlashVisual()
	{
		if (_characterModel == null || !GodotObject.IsInstanceValid(_characterModel) || !IsInsideTree())
			return;

		var geos = new List<GeometryInstance3D>();
		CollectGeometryInstancesForDrainFx(_characterModel, geos);
		if (geos.Count == 0)
			return;

		var flashMat = new StandardMaterial3D
		{
			ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
			AlbedoColor = new Color(1f, 0.72f, 0.28f),
			EmissionEnabled = true,
			Emission = new Color(1f, 0.35f, 0.08f),
			EmissionEnergyMultiplier = 2.85f,
		};
		foreach (var g in geos)
			if (GodotObject.IsInstanceValid(g))
				g.MaterialOverride = flashMat;

		var t = CreateTween();
		t.TweenInterval(0.075f);
		t.TweenCallback(Callable.From(() =>
		{
			foreach (var g in geos)
				if (GodotObject.IsInstanceValid(g))
					g.MaterialOverride = null;
		}));
	}

	/// <summary>Kilven torjunta (boss-spinn yms.) — lyhyt hopeansininen välähdys.</summary>
	private void PlayShieldBlockFlashVisual()
	{
		if (_characterModel == null || !GodotObject.IsInstanceValid(_characterModel) || !IsInsideTree())
			return;

		var geos = new List<GeometryInstance3D>();
		CollectGeometryInstancesForDrainFx(_characterModel, geos);
		if (geos.Count == 0)
			return;

		var flashMat = new StandardMaterial3D
		{
			ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
			AlbedoColor = new Color(0.72f, 0.88f, 1f),
			EmissionEnabled = true,
			Emission = new Color(0.35f, 0.72f, 1f),
			EmissionEnergyMultiplier = 2.1f,
		};
		foreach (var g in geos)
			if (GodotObject.IsInstanceValid(g))
				g.MaterialOverride = flashMat;

		var t = CreateTween();
		t.TweenInterval(0.055f);
		t.TweenCallback(Callable.From(() =>
		{
			foreach (var g in geos)
				if (GodotObject.IsInstanceValid(g))
					g.MaterialOverride = null;
		}));
	}

	private void PlayEnergyDrainFlashVisual()
	{
		if (_characterModel == null || !GodotObject.IsInstanceValid(_characterModel) || !IsInsideTree())
			return;

		var geos = new List<GeometryInstance3D>();
		CollectGeometryInstancesForDrainFx(_characterModel, geos);
		if (geos.Count == 0)
			return;

		var flashMat = new StandardMaterial3D
		{
			ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
			AlbedoColor = new Color(0.55f, 0.92f, 1f),
			EmissionEnabled = true,
			Emission = new Color(0.2f, 0.75f, 0.95f),
			EmissionEnergyMultiplier = 2.4f,
		};
		foreach (var g in geos)
			if (GodotObject.IsInstanceValid(g))
				g.MaterialOverride = flashMat;

		var t = CreateTween();
		t.TweenInterval(0.07f);
		t.TweenCallback(Callable.From(() =>
		{
			foreach (var g in geos)
				if (GodotObject.IsInstanceValid(g))
					g.MaterialOverride = null;
		}));
	}

	/// <summary>
	/// Kun HP putoaa nollaan ja elämä kuluu — ei ääntä; lyhyt hahmon välähdys + kameran isku (HUD pulssaa elämät erikseen).
	/// </summary>
	private void PlayLifeLostImpactVisual()
	{
		if (GetViewport()?.GetCamera3D() is CameraFollow cf)
		{
			cf.ApplyMomentaryDistanceOffset(0.22f, 0.16f);
			cf.ShakeImpulse(0.34f, 0.36f);
		}

		if (_characterModel == null || !GodotObject.IsInstanceValid(_characterModel) || !IsInsideTree())
			return;

		var geos = new List<GeometryInstance3D>();
		CollectGeometryInstancesForDrainFx(_characterModel, geos);
		if (geos.Count == 0)
			return;

		var flashMat = new StandardMaterial3D
		{
			ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
			AlbedoColor = new Color(0.92f, 0.55f, 1f),
			EmissionEnabled = true,
			Emission = new Color(0.55f, 0.22f, 0.95f),
			EmissionEnergyMultiplier = 2.55f,
		};
		foreach (var g in geos)
			if (GodotObject.IsInstanceValid(g))
				g.MaterialOverride = flashMat;

		var t = CreateTween();
		t.TweenInterval(0.13f);
		t.TweenCallback(Callable.From(() =>
		{
			foreach (var g in geos)
				if (GodotObject.IsInstanceValid(g))
					g.MaterialOverride = null;
		}));
	}

	private static void CollectGeometryInstancesForDrainFx(Node node, List<GeometryInstance3D> list)
	{
		if (node is GeometryInstance3D gi)
			list.Add(gi);
		foreach (Node child in node.GetChildren())
			CollectGeometryInstancesForDrainFx(child, list);
	}

	/// <summary>
	/// Kilven torjunta XZ-kartiossa: katssuunta Mixamo/hahmomallissa on usein <c>-Basis.Z</c> (ei sama kuin miekan facing-kartio).
	/// </summary>
	private bool IsShieldBlockFacingArc(Vector3 origin, Vector3 worldPoint, float halfAngleDeg, bool flipShieldFacing180 = false)
	{
		// Mixamo / CharacterBody: tyypillisesti katsomissuunta = maailman -Basis.Z; flipShieldFacing180 = +Z (EnemyLevel2).
		var forward = flipShieldFacing180
			? _characterModel.GlobalTransform.Basis.Z
			: -_characterModel.GlobalTransform.Basis.Z;
		forward.Y = 0f;
		if (forward.LengthSquared() < 1e-8f) return true;
		forward = forward.Normalized();
		return IsShieldBlockFacingArcWithForward(origin, worldPoint, halfAngleDeg, forward);
	}

	/// <summary>XZ-kartio mielivaltaisella etuakselilla (hahmo tai kamera).</summary>
	private bool IsShieldBlockFacingArcWithForward(Vector3 origin, Vector3 worldPoint, float halfAngleDeg, Vector3 forwardWorldXZ)
	{
		var to = worldPoint - origin;
		to.Y = 0f;
		if (to.LengthSquared() < 1e-8f) return true;
		to = to.Normalized();
		forwardWorldXZ.Y = 0f;
		if (forwardWorldXZ.LengthSquared() < 1e-8f) return true;
		forwardWorldXZ = forwardWorldXZ.Normalized();
		float cosLimit = Mathf.Cos(Mathf.DegToRad(halfAngleDeg));
		return to.Dot(forwardWorldXZ) >= cosLimit;
	}

	/// <summary>Sama “taso eteen” kuin liikkeessä: kameran -Z XZ:ssä, fallback hahmon suuntaan.</summary>
	private Vector3 GetCameraFlatForwardForShield()
	{
		var cam = GetViewport()?.GetCamera3D();
		if (cam != null && cam.IsInsideTree())
		{
			Vector3 f = -cam.GlobalTransform.Basis.Z;
			f.Y = 0f;
			if (f.LengthSquared() >= 1e-8f)
				return f.Normalized();
		}

		Vector3 fb = -_characterModel.GlobalTransform.Basis.Z;
		fb.Y = 0f;
		return fb.LengthSquared() >= 1e-8f ? fb.Normalized() : new Vector3(0f, 0f, -1f);
	}

	/// <summary>
	/// Kameran “katso”-akseli XZ:ssä, käänetty niin että osoittaa uhkaa kohti (ei pois), kun kamera on putken sivulla.
	/// </summary>
	private Vector3 GetCameraFlatShieldForwardTowardThreat(Vector3 threatWorldPosition)
	{
		Vector3 f = GetCameraFlatForwardForShield();
		var toThreat = threatWorldPosition - GlobalPosition;
		toThreat.Y = 0f;
		if (toThreat.LengthSquared() < 1e-10f)
			return f;
		toThreat = toThreat.Normalized();
		if (f.Dot(toThreat) < 0f)
			f = -f;
		return f;
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
	[Export] public float TeräkaariPuolikulmaKevyt = 50f;

	/// <summary>R1: teräkartio (jos cleave pois päältä). Cleave käyttää <see cref="R1CleavePuolikulma"/>.</summary>
	[Export] public float TeräkaariPuolikulmaRaskas = 54f;

	/// <summary>
	/// R1 cleave: puolikulma (°) pelaajan rintapisteestä XZ-tasossa. 180° = edessä, takana ja sivut lähellä.
	/// </summary>
	[Export] public float R1CleavePuolikulma = 180f;

	/// <summary>Max etäisyys osumapisteestä iskulinjaan (metriä), R2.</summary>
	[Export] public float KevytIskuLäheisyysMaksimi = 0.92f;

	/// <summary>Max etäisyys pelaajan rintatasoon XZ:ssä, R1 cleave (vihollisen proxOverride voi laajentaa).</summary>
	[Export] public float RaskasIskuLäheisyysMaksimi = 1.35f;

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

	/// <summary>
	/// Fallback jos grabbablella ei ole BoxShapeä: minimi XZ-etäisyys pelurista <see cref="GetGrabbableReferenceWorld"/>-pisteeseen (ei laatikon pintaan).
	/// </summary>
	[Export] public float TartuntaMinKeskietäisyysXZ = 0.54f;

	/// <summary>
	/// Ilmakiekko / laatikko: minimivälys (m) kapselin ulkokehän ja BoxShape-hillin välillä XZ-suunnassa (katso Level1ArcadePhysicsSetup — RB-origin ei ole laatikon keskipiste).
	/// </summary>
	[Export] public float TartuntaVaakaVälysPöytään = 0.12f;

	/// <summary>Mitä kauemmas tästä (metriä) minimistä eteenpäin nopeus pysyy täydenä (pehmeä jarru).</summary>
	[Export] public float TartuntaLähestymisPehmennysLeveys = 0.38f;

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
		// ja osumaikkuna (EnemyLevel1 MiekkaOsumaViive) jäisi koskaan täyttymättä.
		float pos = (float)_animationPlayer.CurrentAnimationPosition;
		// R2: blendissä pos voi pysyä nollassa — käytä myös fysiikkakelloa (kasvaa _PhysicsProcessissa).
		if (_meleeStrikeClip == "mixamo_com_005" || _meleeStrikeClip == "mixamo_com_010")
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

	/// <summary>R1: etäisyys osumapisteestä pelaajan rintatasoon XZ:ssä (ei vain terän suuntaan).</summary>
	private float GetR1CleavePlanarDistance(Vector3 worldPoint)
	{
		var o = MeleeArcOriginWorld();
		float dx = worldPoint.X - o.X;
		float dz = worldPoint.Z - o.Z;
		return Mathf.Sqrt(dx * dx + dz * dz);
	}

	private bool IsPointInR1CleaveArc(Vector3 worldPoint)
	{
		float half = Mathf.Clamp(R1CleavePuolikulma, 1f, 180f);
		return IsWithinFacingArcFromOrigin(MeleeArcOriginWorld(), worldPoint, half);
	}

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
	/// Yksi osumatesti: etäisyys iskulinjaan + R1 vain teräkartio, R2 teräkartio ja facing (rintaorigolla).
	/// </summary>
	/// <param name="proximityMaxOverride">Jos ≥ 0, käytetään tätä max-etäisyytenä metrienä (esim. korkea bossi).</param>
	public bool CanApplyMeleeHitAtWorldPoint(Vector3 worldPoint, float proximityMaxOverride = -1f)
	{
		if (!IsInsideTree() || !IsMeleeAttackActive() || !IsSwordWeaponMode())
			return false;
		float maxDist = proximityMaxOverride >= 0f ? proximityMaxOverride : GetMeleeHitProximityMax();

		if (_attackDamage >= 3)
		{
			// R1 cleave: lähellä pelaajaa kaikkiin suuntiin (edessä + takana), ei vain terän etukaarella.
			if (GetR1CleavePlanarDistance(worldPoint) > maxDist)
				return false;
			return IsPointInR1CleaveArc(worldPoint);
		}

		if (GetMeleeHitDistanceToPoint(worldPoint) > maxDist)
			return false;
		return IsPointInMeleeHitBladeArc(worldPoint) && IsPointInMeleeHitFacingArc(worldPoint);
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
		if (maxHits < 1) return false;
		// R1: _meleeStrikeClip voi olla yhden framen väärä blendin aikana — riittää vahva isku + aktiivinen swing.
		bool heavySwing = IsHeavyMeleeAttackActive()
			|| (_isAttacking && _attackDamage >= 3);
		if (!heavySwing) return false;
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
		_meleeStrikeSfxPlayedThisSwing = false;
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
		_characterModelBaseScale = _characterModel.Scale;
		if (!Mathf.IsZeroApprox(HahmonPohjanOffsetY))
			_characterModel.Position += new Vector3(0f, HahmonPohjanOffsetY, 0f);
		_healthComponent = GetNode<HealthComponent>("HealthComponent");
		_swordSFX = GetNodeOrNull<AudioStreamPlayer>("SwordSFX");

		// Kytketään HealthComponentin signaalit tähän skriptiin
		_healthComponent.HealthChanged += OnHealthChanged;
		_healthComponent.PlayerDied += OnPlayerDied;
		_healthComponent.GameOver += OnGameOver; // ← Game Over -signaali

		if (GetNodeOrNull("CollisionShape3D") is CollisionShape3D capShape && capShape.Shape is CapsuleShape3D cap)
			_playerCapsuleRadius = cap.Radius;

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
		_swordMeshVisual = _sword?.GetNodeOrNull<Node3D>("miekka");

		_handJoystickSceneRoot = GetNodeOrNull("joystick");
		// joystick.glb on scene-juurella vain siksi että se voidaan Reparentata käteen — älä näytä pelaajan spawnapisteessä.
		if (_handJoystickSceneRoot != null)
			SetJoystickAttachTreeVisible(_handJoystickSceneRoot, false);

		if (_handJoystickSceneRoot != null && _sword != null)
		{
			_handJoystickSceneRoot.Reparent(_sword, keepGlobalTransform: false);
			ApplyJoystickHandLocalPose();
			SetJoystickAttachTreeVisible(_handJoystickSceneRoot, false);
			try
			{
				MeshTangentFix.ApplyToSubtree(_handJoystickSceneRoot);
			}
			catch (Exception ex)
			{
				GD.PrintErr("MeshTangentFix (joystick): " + ex.Message);
			}
		}
		else if (_handJoystickSceneRoot != null && _sword == null)
		{
			GD.PrintErr("Player: SwordAttachment puuttuu — joystickia ei kiinnitetty luurankoon (pysyy piilossa juuressa).");
			SetJoystickAttachTreeVisible(_handJoystickSceneRoot, false);
		}

		// Luodaan yksinkertainen joystick vain jos scene:ssä ei ole joystick.glb
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

		SetupBackWeaponCarry();
		RefreshWeaponCarryVisuals();

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
		// Level2BossExit: reiän näkymätön täyte — vain pelaaja (ei Level2SpecialCat kerros 8=128).
		SetCollisionMaskValue(9, true);

		RefreshHandJoystickVisibility();

		Callable.From(DeferredReloadJoystickProgressFromSave).CallDeferred();
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

		if (_weaponDrawTimer > 0f)
		{
			_weaponDrawTimer -= dt;
			if (_weaponDrawTimer <= 0f)
				FinishWeaponEquipVisual();
		}

		float lightAnalog = ReadAggregatedLightAttackAnalog();
		if (lightAnalog < HyökkäysLiipaisinPalautus)
			_lightTriggerArmed = true;

		// ── Painovoima ──
		// Lisätään painovoimaa kun pelaaja on ilmassa
		if (!IsOnFloor())
			velocity.Y -= Painovoima * (float)delta;

		// ── Istuminen (sit) — erillinen syöte; joystick voidaan sitoa tähän myöhemmin
		if (Input.IsActionJustPressed("sit"))
		{
			_isSitting = !_isSitting;
			_isAttacking = false;
			ClearEnemyHitThisSwing();
			_meleeStrikeClip = default;
			_grabbedBody = null;
			_isBlocking  = false;

			if (_isSitting)
			{
				_weaponDrawTimer = 0f;
				RefreshWeaponCarryVisuals();
				PlayAnim("mixamo_com_004");
			}
			else
			{
				if (_weaponMode == WeaponMode.SwordShield)
					BeginWeaponEquipVisualDelay();
				else
					RefreshWeaponCarryVisuals();
				PlayAnim(IsSwordWeaponMode() ? "mixamo_com_007" : "mixamo_com");
			}

			RefreshHandJoystickVisibility();
		}

		// ── Asemoodi (kolmio): drone vain level_1:ssä (arcade-joystick). Muilla kentillä kolmio = miekka/kilpi kuten ilman joystickia.
		if (Input.IsActionJustPressed("toggle_weapon"))
		{
			if (GameState.Instance?.HasJoystick == true && CurrentSceneFilePathLooksLikeLevel1())
			{
				if (_isDroneMode) ExitDroneMode();
				else              EnterDroneMode();
				return;
			}

			_weaponMode = _weaponMode == WeaponMode.Normal ? WeaponMode.SwordShield : WeaponMode.Normal;
			_isAttacking = false;
			ClearEnemyHitThisSwing();
			_meleeStrikeClip = default;
			_grabbedBody = null;
			_isBlocking  = false;

			if (!_isSitting)
			{
				if (_weaponMode == WeaponMode.Normal)
				{
					_weaponDrawTimer = 0f;
					RefreshWeaponCarryVisuals();
					PlayAnim("mixamo_com");
					PlayWeaponToggleFeedback(false);
				}
				else
				{
					BeginWeaponEquipVisualDelay();
					PlayAnim("mixamo_com");
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
			_meleeStrikeSfxPlayedThisSwing = false;
			PlayAnim("mixamo_com_005");
			Vibrate(0.3f, 0.5f, 0.15f);
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
			_meleeStrikeSfxPlayedThisSwing = false;
			PlayAnim("mixamo_com_010");
			Vibrate(0.6f, 1.0f, 0.25f);
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

		// Juostaan ilman (tai ennen kuin miekka on piirtynyt); täysi miekka+kilpi = kävely
		float moveSpeed = Kävelynopeus;
		if (canMove && planarInput.LengthSquared() > 1e-6f && !IsSwordWeaponMode())
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
		if (IsOnFloor())
			wish = wish.Slide(GetFloorNormal());

		// Tarttuessa (neliö pohjassa) liike rajoitetaan vain pöytää kohti.
		// Sivuttaisliike ja taaksepäin kävely estetään kokonaan.
		if (_grabbedBody != null && Input.IsActionPressed("grab")
			&& GodotObject.IsInstanceValid(_grabbedBody) && _grabbedBody.IsInsideTree())
		{
			var refPt = GetGrabbableReferenceWorld(_grabbedBody);
			var toObj = refPt - GlobalPosition;
			toObj.Y = 0f;
			if (toObj.LengthSquared() > 1e-5f)
			{
				var pushDir = toObj.Normalized();
				float fwd = new Vector3(wish.X, 0f, wish.Z).Dot(pushDir);
				fwd = Mathf.Max(0f, fwd);

				// BoxShape (ilmakiekko): älä jarruta eteenpäin-toive clearance-luvulla — 3D-pintamatka + säde + margin
				// nollasi käytännössä aina fwd:n (pöytä ei liikkunut). Vaakasuuntainen välys EnforceGrabMinimumStandoffXZ:ssä.
				if (!TryGetPrimaryBoxCollision(_grabbedBody, out _, out _))
				{
					float dist = toObj.Length();
					float hardMin = Mathf.Max(0.08f, TartuntaMinKeskietäisyysXZ);
					float softEnd = hardMin + Mathf.Max(0.05f, TartuntaLähestymisPehmennysLeveys);
					if (dist < softEnd)
					{
						float u = Mathf.Clamp((dist - hardMin) / Mathf.Max(0.02f, softEnd - hardMin), 0f, 1f);
						fwd *= u * u;
					}
				}

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
		if (IsOnFloor())
			velocity.Y = wish.Y;

		// ── Hahmon kääntyminen ──
		// Työntäessä katsotaan kohdetta (push-suunta), muuten liikkeen suuntaan.
		Vector3 wishHorizontal = new(wish.X, 0f, wish.Z);
		bool grabbing = _grabbedBody != null && Input.IsActionPressed("grab")
			&& GodotObject.IsInstanceValid(_grabbedBody) && _grabbedBody.IsInsideTree();
		if (grabbing)
		{
			var refPt = GetGrabbableReferenceWorld(_grabbedBody);
			var toObj = refPt - GlobalPosition;
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
		// Ei päällekirjoiteta hyökkäysanimaatioita
		bool grabPlaying = _grabbedBody != null && Input.IsActionPressed("grab");
		if (IsOnFloor() && !_isAttacking && !_isBlocking && !grabPlaying)
		{
			string target;
			if (_isSitting)
				target = "mixamo_com_004";
			else if (IsSwordWeaponMode())
				target = planarInput.LengthSquared() > 0.01f ? "mixamo_com_009" : "mixamo_com_007";
			else
				target = planarInput.LengthSquared() > 0.01f ? "mixamo_com_003" : "mixamo_com";
			PlayAnim(target);
		}

		if (_isSitting)
			RefreshHandJoystickVisibility();

		// Testitoiminto — ottaa vahinkoa (poista julkaisusta)
		if (Input.IsActionJustPressed("test_damage"))
			_healthComponent.TakeDamage(1);

		Velocity = velocity;
		MoveAndSlide();

		if (grabbing)
			EnforceGrabMinimumStandoffXZ();

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

		if (_isAttacking && IsSwordWeaponMode() && _swordSFX != null && !_meleeStrikeSfxPlayedThisSwing
			&& GetAttackAnimationTime() >= GetMeleeStrikeWindowStart())
		{
			_meleeStrikeSfxPlayedThisSwing = true;
			_swordSFX.Play();
		}
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
	/// Nollaa hyökkäystilan oikeaan aikaan.
	/// </summary>
	private void OnAnimationFinished(StringName animName)
	{
		// Normaali lyönti loppui
		if (animName == "mixamo_com_005")
		{
			_isAttacking = false;
			ClearEnemyHitThisSwing();
			_meleeStrikeClip = default;
			_meleeHitStopTimer = 0f;
			_meleeStrikeSfxPlayedThisSwing = false;
			if (_animationPlayer != null)
				_animationPlayer.SpeedScale = 1f;
		}

		// Vahva lyönti loppui
		if (animName == "mixamo_com_010")
		{
			_isAttacking = false;
			ClearEnemyHitThisSwing();
			_meleeStrikeClip = default;
			_meleeHitStopTimer = 0f;
			_meleeStrikeSfxPlayedThisSwing = false;
			if (_animationPlayer != null)
				_animationPlayer.SpeedScale = 1f;
		}

		// Tartunta (loop pois päältä / vapautus keskellä)
		if (animName == "mixamo_com_011" && (_grabbedBody == null || !GodotObject.IsInstanceValid(_grabbedBody)))
			PlayAnim(IsSwordWeaponMode() ? "mixamo_com_007" : "mixamo_com");
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
			PlayLifeLostImpactVisual();
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
		ClearEnemyHitThisSwing();
		_meleeStrikeClip = default;
		_meleeSwingElapsed = 0f;
		_meleeStrikeSfxPlayedThisSwing = false;
		_grabbedBody   = null;
		_isBlocking    = false;
		_weaponMode = WeaponMode.Normal;
		_isSitting  = false;
		_heavyAttackCooldown = 0f;
		_lightAttackDebounce = 0f;
		_lightMeleeCooldown = 0f;
		_lightTriggerArmed = true;
		_lightAnalogPreviousFrame = 0f;
		_weaponDrawTimer = 0f;

		RefreshWeaponCarryVisuals();

		_isDroneMode = false;
		RefreshHandJoystickVisibility();

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

	private void SetSwordMeshVisible(bool visible)
	{
		if (_swordMeshVisual != null)
			_swordMeshVisual.Visible = visible;
		else if (_sword != null)
			_sword.Visible = visible;
	}

	/// <summary>
	/// Luo yksinkertaisen joystick-visuaalin oikeaan käteen (SwordAttachment-boneen).
	/// Käytetään CylinderMesh + SphereMesh -yhdistelmää. Korvaa myöhemmin oikealla mallilla.
	/// </summary>
	/// <summary>Drone (kolmio + joystick) vain level_1-scenessä — level_2+ kolmio vaihtaa asetta.</summary>
	private bool CurrentSceneFilePathLooksLikeLevel1()
	{
		var cur = GetTree()?.CurrentScene as Node;
		if (cur == null) return false;
		string p = cur.SceneFilePath?.Replace("\\", "/") ?? "";
		if (p.Length == 0) return false;
		return p.Contains("level_1", StringComparison.OrdinalIgnoreCase);
	}

	private void SetupDroneJoystick()
	{
		var swordAttach = GetNodeOrNull<Node3D>("gameover_character/Skeleton3D/SwordAttachment");
		if (swordAttach == null) return;
		if (_handJoystickSceneRoot != null)
			return;

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

	/// <summary>Joystick kädessä istuessa kun kerätty tai <see cref="JoystickInHandAlwaysWhenSitting"/> (level_3).</summary>
	private void RefreshHandJoystickVisibility()
	{
		bool hasJoy = JoystickInHandAlwaysWhenSitting || GameState.Instance?.HasJoystick == true;
		bool show = hasJoy && _isSitting;
		if (_handJoystickSceneRoot != null)
			SetJoystickAttachTreeVisible(_handJoystickSceneRoot, show);
		if (_droneJoystick != null)
			_droneJoystick.Visible = show && _handJoystickSceneRoot == null;
	}

	private static void SetJoystickAttachTreeVisible(Node root, bool visible)
	{
		if (root == null) return;
		if (root is Node3D n3)
			n3.Visible = visible;
		foreach (Node ch in root.GetChildren())
			SetJoystickAttachTreeVisible(ch, visible);
	}

	/// <summary>Asettaa joystick.glb:n paikallisen asennon oikean käden BoneAttachmentissa (ei Player-juuren offsettia).</summary>
	private void ApplyJoystickHandLocalPose()
	{
		if (_handJoystickSceneRoot == null)
			return;

		Node3D target = _handJoystickSceneRoot as Node3D;
		if (target == null)
		{
			foreach (Node c in _handJoystickSceneRoot.GetChildren())
			{
				if (c is Node3D d)
				{
					target = d;
					break;
				}
			}
		}

		if (target == null)
			return;

		var eulerRad = new Vector3(
			Mathf.DegToRad(JoystickHandLocalEulerDeg.X),
			Mathf.DegToRad(JoystickHandLocalEulerDeg.Y),
			Mathf.DegToRad(JoystickHandLocalEulerDeg.Z));
		var basis = Basis.FromEuler(eulerRad, EulerOrder.Yxz).Scaled(JoystickHandLocalScale);
		target.Transform = new Transform3D(basis, JoystickHandLocalPosition);
	}

	private void DeferredReloadJoystickProgressFromSave()
	{
		GameState.Instance?.LoadJoystickFromSave();
		RefreshHandJoystickVisibility();
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
		_weaponDrawTimer = 0f;

		RefreshWeaponCarryVisuals();
		RefreshHandJoystickVisibility();

		PlayAnim("mixamo_com_004"); // istumisanimaatio
		GD.Print("DroneMode: aktivoitu — joystick kädessä.");
	}

	/// <summary>Poistaa drone-moodin: pelaaja nousee ylös, joystick häviää kädestä.</summary>
	private void ExitDroneMode()
	{
		_isDroneMode = false;
		_isSitting   = false;

		RefreshHandJoystickVisibility();

		if (_weaponMode == WeaponMode.SwordShield)
		{
			BeginWeaponEquipVisualDelay();
			PlayAnim("mixamo_com");
		}
		else
		{
			RefreshWeaponCarryVisuals();
			PlayAnim("mixamo_com");
		}

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

	/// <summary>
	/// CharacterBody vs RigidBody — työntö voi hilata pelaajan törmäyskuoren osittain pöydän sisään; korjataan XZ pois.
	/// </summary>
	private void EnforceGrabMinimumStandoffXZ()
	{
		if (_grabbedBody == null || !GodotObject.IsInstanceValid(_grabbedBody) || !_grabbedBody.IsInsideTree())
			return;
		if (!Input.IsActionPressed("grab"))
			return;

		if (TryGetPrimaryBoxCollision(_grabbedBody, out CollisionShape3D col, out BoxShape3D box))
		{
			float margin = Mathf.Max(0.01f, TartuntaVaakaVälysPöytään);
			float need = _playerCapsuleRadius + margin;
			if (TryGetPushOutFromObbXZ(col.GlobalTransform, box.Size, GlobalPosition, need, out Vector3 deltaXZ))
				GlobalPosition += deltaXZ;
			return;
		}

		Vector3 refPt = GetGrabbableReferenceWorld(_grabbedBody);
		Vector3 toRb = refPt - GlobalPosition;
		toRb.Y = 0f;
		float d = toRb.Length();
		float minD = Mathf.Max(0.08f, TartuntaMinKeskietäisyysXZ);
		if (d >= minD || d < 1e-6f)
			return;

		Vector3 away = (-toRb) / d;
		float fix = minD - d;
		GlobalPosition += new Vector3(away.X * fix, 0f, away.Z * fix);
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

		Vector3 refPt = GetGrabbableReferenceWorld(_grabbedBody);
		var toRb = refPt - GlobalPosition;
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

	private Vector3 GetGrabbableReferenceWorld(RigidBody3D rb)
	{
		if (TryGetPrimaryBoxCollision(rb, out CollisionShape3D col, out _))
			return col.GlobalTransform.Origin;
		return rb.GlobalPosition;
	}

	private static bool TryGetPrimaryBoxCollision(RigidBody3D rb, out CollisionShape3D col, out BoxShape3D box)
	{
		col = null;
		box = null;
		if (rb == null)
			return false;
		foreach (Node child in rb.GetChildren())
		{
			if (child is CollisionShape3D c && c.Shape is BoxShape3D b)
			{
				col = c;
				box = b;
				return true;
			}
		}
		return false;
	}

	private static void ClosestPointOnObb(Transform3D xf, Vector3 size, Vector3 worldP, out Vector3 closestWorld)
	{
		Vector3 half = size * 0.5f;
		Transform3D inv = xf.AffineInverse();
		Vector3 localP = inv * worldP;

		Vector3 inner = new(
			Mathf.Clamp(localP.X, -half.X, half.X),
			Mathf.Clamp(localP.Y, -half.Y, half.Y),
			Mathf.Clamp(localP.Z, -half.Z, half.Z));

		bool inside = Mathf.Abs(localP.X) <= half.X
			&& Mathf.Abs(localP.Y) <= half.Y
			&& Mathf.Abs(localP.Z) <= half.Z;

		Vector3 closestL;
		if (!inside)
			closestL = inner;
		else
		{
			float ex = half.X - Mathf.Abs(localP.X);
			float ey = half.Y - Mathf.Abs(localP.Y);
			float ez = half.Z - Mathf.Abs(localP.Z);
			if (ex <= ey && ex <= ez)
			{
				float sx = Mathf.Sign(localP.X);
				closestL = new Vector3(
					sx * half.X,
					Mathf.Clamp(localP.Y, -half.Y, half.Y),
					Mathf.Clamp(localP.Z, -half.Z, half.Z));
			}
			else if (ey <= ez)
			{
				float sy = Mathf.Sign(localP.Y);
				closestL = new Vector3(
					Mathf.Clamp(localP.X, -half.X, half.X),
					sy * half.Y,
					Mathf.Clamp(localP.Z, -half.Z, half.Z));
			}
			else
			{
				float sz = Mathf.Sign(localP.Z);
				closestL = new Vector3(
					Mathf.Clamp(localP.X, -half.X, half.X),
					Mathf.Clamp(localP.Y, -half.Y, half.Y),
					sz * half.Z);
			}
		}

		closestWorld = xf * closestL;
	}

	/// <summary>XZ-korjaus kun kapseli on liian lähellä laatikon pintaa (projisoidaan ulos-suunta vaakatasoon).</summary>
	private static bool TryGetPushOutFromObbXZ(Transform3D xf, Vector3 size, Vector3 worldP, float needSeparation, out Vector3 deltaXZ)
	{
		deltaXZ = Vector3.Zero;
		ClosestPointOnObb(xf, size, worldP, out Vector3 closestW);
		// Vaakasuora etäisyys (XZ): juuren Y vs laatikon pinnan Y vääristi 3D-etäisyyttä → ei työnnä ulos / pöytä jäi jumiin.
		Vector3 sepH = new(worldP.X - closestW.X, 0f, worldP.Z - closestW.Z);
		float horizDist = sepH.Length();
		if (horizDist < 1e-7f)
		{
			Vector3 p = worldP;
			p.Y = 0f;
			Vector3 c = xf.Origin;
			c.Y = 0f;
			sepH = p - c;
			horizDist = sepH.Length();
		}
		if (horizDist >= needSeparation - 1e-5f || horizDist < 1e-7f)
			return false;

		Vector3 horiz = sepH / horizDist;
		float fix = needSeparation - horizDist;
		deltaXZ = new Vector3(horiz.X * fix, 0f, horiz.Z * fix);
		return fix > 1e-5f;
	}
}
