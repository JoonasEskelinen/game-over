using System;
using Godot;

// ═══════════════════════════════════════════════════════════════════════════════
// BossLevel2 — Level 2:n loppubossi (putken pää, CharacterBody3D)
// ═══════════════════════════════════════════════════════════════════════════════
// Pelisuunnittelu:
//   • Boss ei liiku — haaste on ajallinen: satunnainen vasara voi laueta milloin tahansa.
//   • Pelaaja vahingoittaa bossia vain R1-miekalla (R2 ei vaikuta).
//   • Vasaran sähköalue vähentää HP:ta (HealthComponent), ei R1-latauspalkkia; kilpi ei torju tätä.
//
// Tekninen rakenne:
//   • Animaatiot ladataan FBX-tiedostoista Idle/hammer/death ja kopioidaan yhden
//     AnimationPlayerin kirjastoon. AnimationPlayer on Idle-FBX:n sisällä, jotta
//     luuranko ja jäljet täsmäävät (ei T-pose -ongelmaa).
//   • Scenessä on erillinen vasara.glb ("vasara") — se kiinnitetään BoneAttachment3D:llä
//     oikeaan käteen, jotta malli seuraa kättä animaatiossa (eivät jää maailmaan paikalleen).
//
// Ryhmä: "level2_boss" — HUDController voi näyttää bossin HP-palkin.
// ═══════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Level 2:n staattinen boss: idle-loop, satunnainen hammer-clip, kuolema + kutistuminen.
/// </summary>
public partial class BossLevel2 : CharacterBody3D
{
	// ─── Inspector: perus ─────────────────────────────────────────────────────

	/// <summary>Bossin jäljellä oleva elämäpisteet (vähenee TakeDamage-kutsuilla).</summary>
	[Export] public int BossTerveys = 10;

	/// <summary>Mixamo-idle FBX — lähde clipille "idle" (loop).</summary>
	[Export] public string OdotusAnimaatioPolku   = "res://assets/models/level2_BossEnemy/Idle.fbx";
	/// <summary>Hammer-animaatio — lähde clipille "hammer" (kertatoisto).</summary>
	[Export] public string VasaraAnimaatioPolku = "res://assets/models/level2_BossEnemy/hammer.fbx";
	/// <summary>Kuolema-animaatio — lähde clipille "death".</summary>
	[Export] public string KuolemaAnimaatioPolku  = "res://assets/models/level2_BossEnemy/death.fbx";

	// ─── Inspector: pelaajan miekan osumat bossiin ─────────────────────────────

	/// <summary>
	/// Maailmanpisteiden korkeudet (metriä bossin juuresta ylös) joissa testataan miekan osumaa.
	/// Korkea hahmo → useampi piste välttää "väliin jääviä" osumia.
	/// </summary>
	[Export] public float[] MiekkaIskuKoetuskorkeudet = { 0.4f, 1.0f, 1.6f };

	/// <summary>Lisäviive sekunteina lyönnin osumaikkunan alkuun (PlayerControllerin ikkunaan).</summary>
	[Export] public float   MiekkaIskuAktivoitumisaika = 0f;

	// ─── Inspector: kuolema ─────────────────────────────────────────────────────

	/// <summary>Kuoleman jälkeen tween: skaala nollaan tämän ajan kuluessa (sekunteina).</summary>
	[Export] public float KuolemanKutistumisenKesto = 0.8f;

	// ─── Inspector: vasara (satunnainen ajastin + osumavaihe) ───────────────────

	/// <summary>Uusi satunnainen väli arvotaan hammerin päättymisen jälkeen — sekuntia.</summary>
	[Export] public float VasaraVäliMinSekuntia = 1.8f;
	[Export] public float VasaraVäliMaxSekuntia = 5.5f;

	/// <summary>Maksimietäisyys XZ-tasossa: pelaajan on oltava tämän sisällä, jotta vasara voi osua.</summary>
	[Export] public float VasaraIskuKantamaTasossa = 2.85f;
	/// <summary>Pystysuuntainen toleranssi pelaajan ja bossin välillä (metriä).</summary>
	[Export] public float VasaraIskuMaksimiKorkeus = 1.95f;

	/// <summary>
	/// HP-vahinko per osuma. Jos 0, käytetään pelaajan MaxHealth/3 (sama tyyli kuin liskon isku).
	/// </summary>
	[Export] public int VasaraTerveysvahinko = 0;

	/// <summary>
	/// Hammer-clipin normalisoitu aika 0…1 jolloin "isku" rekisteröityy (Mixamo-aikajänne).
	/// Säädä jos osuma tuntuu liian aikaisin/myöhään.
	/// </summary>
	[Export] public float VasaraOsumaVaiheMin = 0.38f;
	[Export] public float VasaraOsumaVaiheMax = 0.55f;

	/// <summary>
	/// Maan iskurenkaan säde (XZ). 0 = sama kuin <see cref="VasaraIskuKantamaTasossa"/>.
	/// </summary>
	[Export] public float VasaraIskuRenkaanSade = 0f;

	/// <summary>Sekunteina: renkaan näkyvä "sähköisku" ja haalistuminen.</summary>
	[Export] public float VasaraIskuVfxKesto = 0.5f;

	/// <summary>Renkaan korkeus juuren Y:stä (metriä) — säädä jos levy kelluu maan yläpuolella.</summary>
	[Export] public float VasaraIskuVfxKorkeus = 0.04f;

	/// <summary>GPUParticles-särky (rengas); laske Pi:llä tarvittaessa 24–32:een.</summary>
	[Export] public int VasaraIskuSahkoPartikkeliMaara = 52;

	/// <summary>Lyhyt valopulssi (0 = pois päältä).</summary>
	[Export] public float VasaraIskuValoEnergia = 3.2f;

	// ─── Inspector: hahmon suunta ───────────────────────────────────────────────

	/// <summary>
	/// Mixamon malli voi olla väärinpäin suhteessa LookAt:iin — käännä 180° tarvittaessa.
	/// </summary>
	[Export] public float KatseenKiertoAstetta = 180f;

	/// <summary>
	/// Pakota luun nimi (esim. "mixamorig:RightHand"). Tyhjä = automaattihaku "RightHand" / Right+Hand.
	/// </summary>
	[Export] public string VasaraKäsiLuunimiYlikirjoitus = "";

	// ─── Tila (ei export) ─────────────────────────────────────────────────────────

	private int _maxBossHealth;

	private Node3D           _player;
	private PlayerController _playerController;
	private AnimationPlayer  _animationPlayer;
	private CameraFollow     _camera;

	/// <summary>True kun HP ≤ 0 ja kuolema käynnistetty.</summary>
	private bool _isDead = false;

	/// <summary>
	/// Estää saman miekkalyönnin useita osumia yhteen swingiin (PlayerController.TryClaimEnemyMeleeHit).
	/// </summary>
	private bool _hasBeenHitThisSwing = false;

	/// <summary>Sekuntia seuraavaan hammer-yritykseen (laskee kun ei hammer-clipiä).</summary>
	private float _hammerTimer;

	/// <summary>True kun hammer-vaiheen HP-isku on jo annettu tälle swingille.</summary>
	private bool  _hammerHitApplied;

	/// <summary>True kun "hammer"-animaatio pyörii (estää ajastimen ja liikkumisen).</summary>
	private bool  _hammerAnimActive;

	/// <summary>Yksi iskurengas / swing — resetoidaan <see cref="TriggerHammer"/>:issa.</summary>
	private bool _hammerShockVfxSpawned;

	private const float Gravity = 20f;

	// ─── Julkinen API (HUD / muut skriptit) ─────────────────────────────────────

	public bool IsBossDead => _isDead;
	public int  GetBossCurrentHealth() => BossTerveys;
	public int  GetBossMaxHealth()     => _maxBossHealth;

	public override void _Ready()
	{
		// CharacterBody3D: pysy lattialla porteissa / reunalla
		FloorSnapLength = 0.18f;
		FloorMaxAngle   = Mathf.DegToRad(50f);

		_maxBossHealth    = Mathf.Max(1, BossTerveys);
		_player           = GetTree().GetFirstNodeInGroup("player") as Node3D;
		_playerController = _player as PlayerController;
		// Tärkeää: AnimationPlayer Idle-FBX:n sisällä (ei juuren tyhjää), jotta jäljet osuvat luurankoon
		_animationPlayer  = FindChild("AnimationPlayer", true, false) as AnimationPlayer;

		if (_animationPlayer != null)
		{
			// mixamo_com = tyypillinen Mixamo-clipin nimi Godot-importissa
			LoadAnim(OdotusAnimaatioPolku,   "mixamo_com", "idle",   loop: true);
			LoadAnim(KuolemaAnimaatioPolku,  "mixamo_com", "death",  loop: false);
			LoadAnim(VasaraAnimaatioPolku, "mixamo_com", "hammer", loop: false);
			_animationPlayer.AnimationFinished += OnAnimationFinished;
			// Yksi frame myöhemmin: kirjasto valmis, luuranko instassoitu
			Callable.From(DeferredBossVisualSetup).CallDeferred();
		}

		_hammerTimer = NextHammerInterval();
		AddToGroup("level2_boss");
	}

	/// <summary>
	/// Idle käyntiin ja vasara käteen — erillisessä kutsussa, jotta skeleton on valmis.
	/// </summary>
	private void DeferredBossVisualSetup()
	{
		PlayIdleAnim();
		AttachHammerToHandBone();
	}

	/// <summary>
	/// Kiinnittää scenen juuren lapsen <c>vasara</c> (GLB) Skeleton3D:n oikeaan käteen.
	/// Ilman tätä vasara olisi staattinen maailmassa kun käsi animoituu erikseen.
	/// </summary>
	private void AttachHammerToHandBone()
	{
		var hammer = GetNodeOrNull<Node3D>("vasara");
		var skel   = FindChild("Skeleton3D", true, false) as Skeleton3D;
		if (hammer == null || skel == null)
		{
			GD.PrintErr("BossLevel2: vasara- tai Skeleton3D-node puuttuu.");
			return;
		}

		string bone = VasaraKäsiLuunimiYlikirjoitus;
		if (string.IsNullOrEmpty(bone))
			bone = FindRightHandBoneName(skel);
		if (string.IsNullOrEmpty(bone))
		{
			GD.PrintErr("BossLevel2: oikeaa kättä vastaavaa luuta ei löytynyt — tarkista FBX-luut.");
			return;
		}

		var attach = new BoneAttachment3D();
		attach.Name = "HammerHandAttach";
		attach.BoneName = bone;
		skel.AddChild(attach);
		// keep_global_transform: säilytä visuaalinen asento hetkellisesti (voidaan säätää Inspectorissa)
		hammer.Reparent(attach, true);
	}

	/// <summary>Etsii luunimen jossa on "RightHand", tai "Right"+"Hand".</summary>
	private static string FindRightHandBoneName(Skeleton3D sk)
	{
		int n = sk.GetBoneCount();
		for (int i = 0; i < n; i++)
		{
			string nm = sk.GetBoneName(i);
			if (nm.ToString().IndexOf("RightHand", StringComparison.OrdinalIgnoreCase) >= 0)
				return nm;
		}
		for (int i = 0; i < n; i++)
		{
			string nm = sk.GetBoneName(i);
			if (nm.ToString().IndexOf("Right", StringComparison.OrdinalIgnoreCase) >= 0
				&& nm.ToString().IndexOf("Hand", StringComparison.OrdinalIgnoreCase) >= 0)
				return nm;
		}
		return "";
	}

	public override void _ExitTree()
	{
		if (_animationPlayer != null && GodotObject.IsInstanceValid(_animationPlayer))
			_animationPlayer.AnimationFinished -= OnAnimationFinished;
		base._ExitTree();
	}

	// ═══════════════════════════════════════════════════════════════════════════
	// Fysiikka (_PhysicsProcess)
	// ═══════════════════════════════════════════════════════════════════════════
	// Boss ei aseta vaakalentoa — vain painovoima. Kasvot pelaajaan, satunnainen hammer,
	// vasaran HP-tarkistus animaation vaiheessa, ja sama miekan osumalogiikka kuin muilla vihollisilla.
	// ═══════════════════════════════════════════════════════════════════════════

	public override void _PhysicsProcess(double delta)
	{
		if (_isDead || !IsInsideTree()) return;

		float dt = (float)delta;
		Vector3 velocity = Velocity;

		if (!IsOnFloor())
			velocity.Y -= Gravity * dt;

		// Pelaaja voi kuolla / respata — päivitä viittaus tarvittaessa
		if (_player == null || !GodotObject.IsInstanceValid(_player) || !_player.IsInsideTree())
		{
			_player = GetTree().GetFirstNodeInGroup("player") as Node3D;
			_playerController = _player as PlayerController;
		}

		if (_player != null)
			FacePlayer();

		// Vasara vain kun ei jo hammer-clipiä (yksi kerrallaan)
		if (!_hammerAnimActive)
		{
			_hammerTimer -= dt;
			if (_hammerTimer <= 0f)
				TriggerHammer();
		}

		// Osumahetki: iskurengas (VFX) + HP kerran per swing, kun clipin vaihe osuu iskuikkunaan
		if (_hammerAnimActive && _animationPlayer?.CurrentAnimation == "hammer")
			ProcessHammerImpactWindow();

		// Pelaaja lyö bossia: vain R1 (raskas); miekan ikkuna + probe-pisteet + yksi osuma per swing (TryClaim)
		if (_playerController != null && _playerController.IsMeleeAttackActive()
			&& _playerController.IsHeavyMeleeAttackActive())
		{
			float animTime = _playerController.GetAttackAnimationTime();
			float hitFrom  = _playerController.GetMeleeStrikeWindowStart() + MiekkaIskuAktivoitumisaika;

			if (animTime >= hitFrom && !_hasBeenHitThisSwing)
			{
				var probes = MiekkaIskuKoetuskorkeudet ?? new float[] { 1.0f };
				foreach (float h in probes)
				{
					Vector3 p = GlobalPosition + Vector3.Up * h;
					if (_playerController.CanApplyMeleeHitAtWorldPoint(p)
						&& _playerController.TryClaimEnemyMeleeHit())
					{
						TakeDamage(_playerController.GetMeleeAttackDamage());
						_playerController.NotifyMeleeHitLanded();
						_hasBeenHitThisSwing = true;
						break;
					}
				}
			}
		}
		else
		{
			// Lyönti ei käynnissä → sallitaan uusi swing seuraavalle iskulle
			_hasBeenHitThisSwing = false;
			_playerController?.ClearEnemyHitThisSwing();
		}

		Velocity = velocity;
		MoveAndSlide();
	}

	// ═══════════════════════════════════════════════════════════════════════════
	// Vasara: käynnistys ja HP-vahinko
	// ═══════════════════════════════════════════════════════════════════════════

	private void TriggerHammer()
	{
		if (_isDead || _animationPlayer == null || !GodotObject.IsInstanceValid(_animationPlayer)) return;
		if (!_animationPlayer.HasAnimation("hammer")) return;

		_hammerAnimActive = true;
		_hammerHitApplied = false;
		_hammerShockVfxSpawned = false;
		_animationPlayer.Play("hammer");
	}

	/// <summary>
	/// Iskuikkuna: kerran renkaan VFX (sähkö / isku maahan), sitten yksi HP-tarkistus jos ei vielä osunut.
	/// </summary>
	private void ProcessHammerImpactWindow()
	{
		if (_animationPlayer == null || _animationPlayer.CurrentAnimation != "hammer") return;

		double len = _animationPlayer.CurrentAnimationLength;
		if (len <= 0.02) return;
		float phase = (float)(_animationPlayer.CurrentAnimationPosition / len);
		if (phase < VasaraOsumaVaiheMin || phase > VasaraOsumaVaiheMax) return;

		if (!_hammerShockVfxSpawned)
		{
			SpawnHammerShockGroundVfx();
			_hammerShockVfxSpawned = true;
		}

		TryApplyHammerHPDamage();
	}

	/// <summary>
	/// Yksi HP-isku per hammer-swingi, kun animaation vaihe on iskuikkunassa ja pelaaja sähköalueella.
	/// Maan sähköisku ei ole kilvellä torjuttavissa (toisin kuin suora vasaraosuma voisi olla muissa peleissä).
	/// </summary>
	private void TryApplyHammerHPDamage()
	{
		if (_hammerHitApplied || _player == null || !GodotObject.IsInstanceValid(_player)) return;
		if (_animationPlayer == null || _animationPlayer.CurrentAnimation != "hammer") return;

		double len = _animationPlayer.CurrentAnimationLength;
		if (len <= 0.02) return;
		float phase = (float)(_animationPlayer.CurrentAnimationPosition / len);
		if (phase < VasaraOsumaVaiheMin || phase > VasaraOsumaVaiheMax) return;

		float shockRadius = VasaraIskuRenkaanSade > 0.01f ? VasaraIskuRenkaanSade : VasaraIskuKantamaTasossa;
		float planarDist = PlanarDistTo(_player.GlobalPosition);
		float heightDiff = Mathf.Abs(_player.GlobalPosition.Y - GlobalPosition.Y);
		if (planarDist > shockRadius || heightDiff > VasaraIskuMaksimiKorkeus) return;

		var health = _player.GetNodeOrNull<HealthComponent>("HealthComponent");
		if (health == null) return;

		int dmg = VasaraTerveysvahinko > 0 ? VasaraTerveysvahinko : Mathf.Max(1, health.MaxHealth / 3);
		health.TakeDamage(dmg);
		_hammerHitApplied = true;

		GD.Print($"BossLevel2 vasara osui! -{dmg} HP");
		if (_camera == null) _camera = GetViewport()?.GetCamera3D() as CameraFollow;
		_camera?.ShakeImpulse(0.2f, 0.22f);
	}

	/// <summary>
	/// Maan sähköisku: shader-pohjainen rengas + kevyt GPUParticles-renkaan särky + valopulssi (Pi-säädöt exporteissa).
	/// </summary>
	private void SpawnHammerShockGroundVfx()
	{
		if (!IsInsideTree()) return;

		float r = VasaraIskuRenkaanSade > 0.01f ? VasaraIskuRenkaanSade : VasaraIskuKantamaTasossa;
		var root = new Node3D { Name = "HammerShockElectricVfx" };

		Shader electricShader = GD.Load<Shader>("res://assets/shaders/hammer_shock_electric.gdshader");
		if (electricShader == null)
		{
			GD.PrintErr("BossLevel2: hammer_shock_electric.gdshader puuttuu — ohitetaan VFX.");
			return;
		}

		var shockMat = new ShaderMaterial { Shader = electricShader };
		shockMat.SetShaderParameter("u_fade", 1f);

		var plane = new MeshInstance3D();
		plane.Mesh = new PlaneMesh
		{
			Size = new Vector2(r * 2f, r * 2f),
			SubdivideWidth = 1,
			SubdivideDepth = 1,
		};
		plane.MaterialOverride = shockMat;
		plane.RotationDegrees = new Vector3(-90f, 0f, 0f);
		plane.Position = Vector3.Up * VasaraIskuVfxKorkeus;
		root.AddChild(plane);

		// Rengaspartikkelit: sähkökipinät ylöspäin renkaan varrelta
		var sparks = new GpuParticles3D { Name = "ShockSparks" };
		int amt = Mathf.Clamp(VasaraIskuSahkoPartikkeliMaara, 16, 96);
		sparks.Amount = amt;
		sparks.Lifetime = 0.52f;
		sparks.Explosiveness = 0.98f;
		sparks.OneShot = true;
		sparks.LocalCoords = true;
		sparks.Position = new Vector3(0f, 0.06f, 0f);

		var pm = new ParticleProcessMaterial
		{
			EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Ring,
			EmissionRingRadius = r,
			EmissionRingInnerRadius = Mathf.Max(0.1f, r * 0.58f),
			EmissionRingHeight = 0.14f,
			EmissionRingAxis = Vector3.Up,
			Direction = Vector3.Up,
			Spread = 32f,
			InitialVelocityMin = 2f,
			InitialVelocityMax = 6.2f,
			Gravity = new Vector3(0f, -11f, 0f),
			ScaleMin = 0.05f,
			ScaleMax = 0.16f,
		};
		pm.Color = new Color(0.5f, 0.95f, 1f, 1f);
		sparks.ProcessMaterial = pm;
		root.AddChild(sparks);

		OmniLight3D pulse = null;
		if (VasaraIskuValoEnergia > 0.05f)
		{
			pulse = new OmniLight3D
			{
				LightColor = new Color(0.45f, 0.92f, 1f),
				LightEnergy = VasaraIskuValoEnergia,
				OmniRange = r * 1.2f,
				ShadowEnabled = false,
				Position = new Vector3(0f, 0.35f, 0f),
			};
			root.AddChild(pulse);
		}

		AddChild(root);
		root.GlobalPosition = new Vector3(GlobalPosition.X, GlobalPosition.Y, GlobalPosition.Z);
		root.Scale = new Vector3(0.08f, 1f, 0.08f);

		float dur = Mathf.Max(0.15f, VasaraIskuVfxKesto);
		var tw = CreateTween();
		tw.SetParallel(true);
		tw.TweenProperty(root, "scale", Vector3.One, Mathf.Min(0.14f, dur * 0.4f))
			.SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
		tw.TweenMethod(Callable.From<float>(fade =>
		{
			if (GodotObject.IsInstanceValid(shockMat))
				shockMat.SetShaderParameter("u_fade", fade);
			if (pulse != null && GodotObject.IsInstanceValid(pulse))
				pulse.LightEnergy = VasaraIskuValoEnergia * fade;
		}), 1f, 0f, dur).SetDelay(0.04f).SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.In);

		sparks.Emitting = true;

		tw.Chain().TweenCallback(Callable.From(() =>
		{
			if (GodotObject.IsInstanceValid(root)) root.QueueFree();
		}));
	}

	private float NextHammerInterval() =>
		(float)GD.RandRange(VasaraVäliMinSekuntia, VasaraVäliMaxSekuntia);

	// ═══════════════════════════════════════════════════════════════════════════
	// Animaatiot: idle, hammer loppui
	// ═══════════════════════════════════════════════════════════════════════════

	private void PlayIdleAnim()
	{
		if (_animationPlayer != null && GodotObject.IsInstanceValid(_animationPlayer)
			&& _animationPlayer.HasAnimation("idle"))
			_animationPlayer.Play("idle");
	}

	/// <summary>Hammer päättyi → takaisin idleen ja uusi satunnainen väli ennen seuraavaa iskua.</summary>
	private void OnAnimationFinished(StringName animName)
	{
		if (_isDead || _animationPlayer == null || !GodotObject.IsInstanceValid(_animationPlayer)) return;
		if (animName == "hammer")
		{
			_hammerAnimActive = false;
			_hammerHitApplied = false;
			_hammerTimer = NextHammerInterval();
			if (_animationPlayer.HasAnimation("idle"))
				_animationPlayer.Play("idle");
		}
	}

	// ═══════════════════════════════════════════════════════════════════════════
	// Vahinko, osumaräjähdys, kuolema
	// ═══════════════════════════════════════════════════════════════════════════

	public void TakeDamage(int amount)
	{
		if (_isDead) return;
		BossTerveys -= amount;
		GD.Print($"BossLevel2 HP: {BossTerveys}");
		OnHitFeedback();
		if (BossTerveys <= 0) Die();
	}

	/// <summary>Punaflash meshiin + lyhyt hit-stop animaatiossa (SpeedScale 0).</summary>
	private void OnHitFeedback()
	{
		if (_camera == null) _camera = GetViewport()?.GetCamera3D() as CameraFollow;
		_camera?.ShakeImpulse(0.25f, 0.28f);

		var geos = new System.Collections.Generic.List<GeometryInstance3D>();
		CollectGeometryInstances(this, geos);
		if (geos.Count == 0) return;

		var flashMat = new StandardMaterial3D
		{
			ShadingMode              = BaseMaterial3D.ShadingModeEnum.Unshaded,
			AlbedoColor              = new Color(1f, 0.82f, 0.82f),
			EmissionEnabled          = true,
			Emission                 = new Color(1f, 0.35f, 0.35f),
			EmissionEnergyMultiplier = 2.5f,
		};
		foreach (var g in geos)
			if (GodotObject.IsInstanceValid(g)) g.MaterialOverride = flashMat;

		var t = CreateTween();
		t.TweenInterval(0.06f);
		t.TweenCallback(Callable.From(() =>
		{
			foreach (var g in geos)
				if (GodotObject.IsInstanceValid(g)) g.MaterialOverride = null;
		}));

		if (_animationPlayer != null && GodotObject.IsInstanceValid(_animationPlayer))
		{
			_animationPlayer.SpeedScale = 0f;
			var freeze = CreateTween();
			freeze.TweenInterval(0.08f);
			freeze.TweenCallback(Callable.From(() =>
			{
				if (GodotObject.IsInstanceValid(_animationPlayer))
					_animationPlayer.SpeedScale = 1f;
			}));
		}
	}

	/// <summary>Kollisio pois, death-clip, hidastus, kutistuminen ja QueueFree.</summary>
	private void Die()
	{
		_isDead = true;
		Velocity = Vector3.Zero;
		_hammerAnimActive = false;

		var col = GetNodeOrNull<CollisionShape3D>("CollisionShape3D");
		if (col != null) col.Disabled = true;

		if (_animationPlayer != null && GodotObject.IsInstanceValid(_animationPlayer))
			_animationPlayer.Play("death");

		RemoveFromGroup("level2_boss");
		GD.Print("BossLevel2 kuoli — aktivoi exit täällä!");

		Engine.TimeScale = 0.15f;
		if (IsInsideTree())
		{
			var slow = GetTree().CreateTimer(0.1f, processInPhysics: false, ignoreTimeScale: true);
			slow.Timeout += () => { if (Engine.TimeScale < 1f) Engine.TimeScale = 1f; };
		}

		var tween = CreateTween();
		tween.TweenInterval(1.2f);
		tween.TweenProperty(this, "scale", Vector3.Zero, KuolemanKutistumisenKesto)
			.SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.In);
		tween.TweenCallback(Callable.From(() => QueueFree()));
	}

	// ═══════════════════════════════════════════════════════════════════════════
	// Apu: suunta, etäisyys, geometria, FBX → AnimationLibrary
	// ═══════════════════════════════════════════════════════════════════════════

	private void FacePlayer()
	{
		if (_player == null || !_player.IsInsideTree() || !IsInsideTree()) return;
		var to = _player.GlobalPosition - GlobalPosition;
		to.Y = 0f;
		if (to.LengthSquared() < 1e-6f) return;
		LookAt(GlobalPosition + to.Normalized() * 3f, Vector3.Up);
		RotateY(Mathf.DegToRad(KatseenKiertoAstetta));
	}

	private float PlanarDistTo(Vector3 target)
	{
		float dx = target.X - GlobalPosition.X;
		float dz = target.Z - GlobalPosition.Z;
		return Mathf.Sqrt(dx * dx + dz * dz);
	}

	private static void CollectGeometryInstances(Node node, System.Collections.Generic.List<GeometryInstance3D> list)
	{
		if (node is GeometryInstance3D gi) list.Add(gi);
		foreach (Node child in node.GetChildren())
			CollectGeometryInstances(child, list);
	}

	/// <summary>
	/// Lataa FBX-scenen, poimii yhden Mixamo-clipin ja lisää sen tämän bossin AnimationLibraryyn.
	/// Alkuperäinen instanssi tuhotaan — data jää vain kirjastoon.
	/// </summary>
	private void LoadAnim(string path, string sourceName, string targetName, bool loop = false)
	{
		var scene = GD.Load<PackedScene>(path);
		if (scene == null) { GD.PrintErr($"Animaatiota ei löydy: {path}"); return; }
		var inst = scene.Instantiate();
		var ap   = inst.FindChild("AnimationPlayer", true, false) as AnimationPlayer;
		if (ap == null) { inst.QueueFree(); return; }

		Animation anim = null;
		foreach (var n in new[] { sourceName, sourceName.Replace("_", ".") })
			if (ap.HasAnimation(n)) { anim = ap.GetAnimation(n); break; }

		if (anim == null)
		{
			GD.PrintErr($"Animaatiota '{sourceName}' ei löydy: {path}");
			GD.Print($"Saatavilla: {string.Join(", ", ap.GetAnimationList())}");
			inst.QueueFree();
			return;
		}

		AnimationLibrary lib;
		if (_animationPlayer.HasAnimationLibrary(""))
			lib = _animationPlayer.GetAnimationLibrary("");
		else
		{
			lib = new AnimationLibrary();
			_animationPlayer.AddAnimationLibrary("", lib);
		}

		if (loop) anim.LoopMode = Animation.LoopModeEnum.Linear;
		if (lib.HasAnimation(targetName)) lib.RemoveAnimation(targetName);
		lib.AddAnimation(targetName, anim);
		inst.QueueFree();
	}
}
