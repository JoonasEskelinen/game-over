using Godot;

/// <summary>
/// Level 1 -bossi: tanssi (satunnainen kesto) → latautuu reunalle → syöksy pelaajaa kohti → takaisin tanssipisteeseen.
/// FBX-polut säädetään Inspectorissa (Mixamo: dance with skin, fast run without skin).
/// 
/// BOSSIN KÄYTTÄYTYMINEN:
/// 1. Dancing-vaihe: bossi tanssii paikallaan satunnaisen ajan
/// 2. Charging-vaihe: bossi teleporttaa areenan reunalle ja syöksyy pelaajaa kohti
/// 3. Bossi voi ottaa vahinkoa vain tanssiessaan (vahva isku R1 tappaa)
/// </summary>
public partial class BossLevel1 : CharacterBody3D
{
	// ─────────────────────────────────────────────
	// EXPORTATUT MUUTTUJAT — säädettävissä Inspectorissa
	// ─────────────────────────────────────────────

	/// <summary>Dancing.fbx polku — Mixamosta ladattu with skin.</summary>
	[Export] public string DanceFbxPath = "res://assets/models/level1_BossEnemy/Dancing.fbx";

	/// <summary>FastRun.fbx polku — Mixamosta ladattu without skin.</summary>
	[Export] public string FastRunFbxPath = "res://assets/models/level1_BossEnemy/FastRun.fbx";
	
	[Export] public string DeathFbxPath = "res://assets/models/level1_BossEnemy/Death.fbx";

	/// <summary>Mixamon animaation sisäinen nimi FBX:ssä.</summary>
	[Export] public string MixamoAnimSourceName = "mixamo_com";

	/// <summary>Tanssianimaation nimi pelissä.</summary>
	[Export] public string DanceClipName = "dance";

	/// <summary>Juoksuanimaation nimi pelissä.</summary>
	[Export] public string RunClipName = "run";
	
	[Export] public string DeathClipName = "death";

	/// <summary>Maksimi-HP — oletus 30 = 10 osumaa × R1-vahinko (3) tanssi-/otteluvaiheessa.</summary>
	[Export] public int MaxBossHealth = 30;

	private int _bossHealth;

	/// <summary>Tanssin minimikesto sekunteina ennen syöksyä.</summary>
	[Export] public float DanceDurationMin = 10.5f;

	/// <summary>Tanssin maksimikesto sekunteina.</summary>
	[Export] public float DanceDurationMax = 14f;

	/// <summary>Aika tanssin päättymisen ja kameran palautumisen jälkeen ennen juoksua (run).</summary>
	[Export] public float PostDanceDelayBeforeCharge = 3f;

	/// <summary>Syöksyn nopeus.</summary>
	[Export] public float ChargeSpeed = 12f;

	/// <summary>Syöksyn maksimikesto — jos pelaajaa ei tavoiteta, palataan tanssimaan.</summary>
	[Export] public float ChargeMaxSeconds = 3.2f;

	/// <summary>Areenan puolisäde — syöksyn spawnauspisteet ovat tämän etäisyydellä keskipisteestä.</summary>
	[Export] public float ArenaEdgeHalf = 13.5f;

	/// <summary>Kontaktivahingon määrä syöksyn aikana.</summary>
	[Export] public float ChargeContactDamage = 1;

	/// <summary>Aika sekunteina ennen kuin kontaktivahinko voi toistua.</summary>
	[Export] public float ChargeContactCooldown = 2.5f;

	/// <summary>Osumakeskipisteen korkeus Y-akselilla.</summary>
	[Export] public float HitCenterYOffset = 0.9f;

	/// <summary>Korkeudet juuren <see cref="GlobalPosition"/>ista (jalat) — pitkä mesh + skaala.</summary>
	[Export] public float[] SwordHitProbeHeights = { 0.35f, 1.0f, 1.85f, 2.7f, 3.5f, 4.35f };

	/// <summary>Lisäpisteet <c>BossVisual</c>-solmun kohdalta (jos FBX on offsetoituna juureen nähden).</summary>
	[Export] public float[] SwordHitProbeVisualYOffset = { -0.4f, 0.35f, 1.1f, 2.0f, 2.9f };

	/// <summary>Lisää max-etäisyyttä terään verrattuna (korkea hahmo / R1-kapea ikkuna).</summary>
	[Export] public float SwordHitExtraProximityMeters = 0.42f;

	/// <summary>Lisäviive miekan osumaikkunaan (0 = heti kun animaatio alkaa).</summary>
	[Export] public float SwordHitActivationTime = 0f;

	/// <summary>Kuolemisanimaation kesto sekunteina (kutistuminen).</summary>
	[Export] public float DeathShrinkDuration = 1.55f;

	/// <summary>
	/// Visuaalisen meshin skaalauskerroin.
	/// Mixamon FBX on usein ~100x liian pieni — oletus ~1,5× aiemmasta (150).
	/// Aseta 1 jos haluat säilyttää scenen lapsen skaalan sellaisenaan.
	/// </summary>
	[Export] public float BossVisualUniformScale = 150f;

	/// <summary>Laske koko hahmoa näin paljon spawnin jälkeen (m) — kun FBX-jalat eivät osu juureen.</summary>
	[Export] public float AdditionalStandLowerY = 0f;

	/// <summary>Uudelleenlattiasnap _Ready:ssä (sulkee oman colliderin pois säteestä).</summary>
	[Export] public bool SnapFeetToFloorOnReady = true;

	[Export] public float FeetSnapRayStartUpM = 14f;
	[Export] public float FeetSnapRayLengthDownM = 24f;
	[Export] public float FeetSnapOffsetAboveFloor = 0.06f;

	/// <summary>Level 1 -bossin taustamusiikki (loop) koko eliniän — tiedosto: musiclevel1.mp3.</summary>
	[Export] public string BossMusicPath = "res://assets/audio/music/musiclevel1.mp3";

	[Export] public float BossMusicVolumeDb = -4f;

	/// <summary>Lisäspotti tanssivaiheessa (Pi: varjo pois).</summary>
	[Export] public bool BossDanceHighlightEnabled = true;

	[Export] public float BossDanceLightEnergy = 4.2f;

	/// <summary>Spotin kantama (Godot SpotRange).</summary>
	[Export] public float BossDanceLightRange = 14f;

	[Export] public Color BossDanceLightColor = new(1f, 0.94f, 0.86f, 1f);

	/// <summary>Valokiilan puolikulma (astetta, Godot SpotAngle).</summary>
	[Export] public float BossDanceSpotAngleDeg = 52f;

	/// <summary>Valon sijainti: korkeus bossin jaloista (maailmay).</summary>
	[Export] public float BossDanceSpotHeightM = 4.2f;

	/// <summary>Kuinka paljon valo siirtyy kameraa kohti XZ-tasossa (lavaste).</summary>
	[Export] public float BossDanceSpotTowardCameraM = 1.1f;

	/// <summary>LookAt-kohdan korkeus bossin jaloista (valo osoittaa tähän pisteeseen).</summary>
	[Export] public float BossDanceSpotAimYOffsetM = 1.35f;

	// ─────────────────────────────────────────────
	// TEKSTUURIPOLUT — säädettävissä Inspectorissa
	// ─────────────────────────────────────────────

	/// <summary>Albedo (väri) tekstuuri.</summary>
	[Export] public string TextureBasePath     = "res://assets/models/level1_BossEnemy/anthropomorphichedgehog3dmodel_basecolor.JPEG";

	/// <summary>Normal map tekstuuri (pinnan yksityiskohdat).</summary>
	[Export] public string TextureNormalPath   = "res://assets/models/level1_BossEnemy/anthropomorphichedgehog3dmodel_normal.JPEG";

	/// <summary>Roughness tekstuuri (pinnan karheus).</summary>
	[Export] public string TextureRoughnessPath = "res://assets/models/level1_BossEnemy/anthropomorphichedgehog3dmodel_roughness.JPEG";

	/// <summary>Metallic tekstuuri (metallinen pinta).</summary>
	[Export] public string TextureMetallicPath  = "res://assets/models/level1_BossEnemy/anthropomorphichedgehog3dmodel_metallic.JPEG";

	// ─────────────────────────────────────────────
	// JULKISET OMINAISUUDET
	// ─────────────────────────────────────────────

	/// <summary>True kun bossi tanssii — vain tällöin vahva isku (R1) voi osua (ei post-tanssi-viivettä).</summary>
	public bool IsDanceVulnerable => !_isDead && _phase == BossPhase.Dancing && !_waitingAfterDance;

	/// <summary>True vain tanssilaskurin aikana — lähipiirikamera (ei post-tanssi-viivettä).</summary>
	public bool IsBossCloseupDanceCameraActive => !_isDead && _phase == BossPhase.Dancing && !_waitingAfterDance && _danceTimeLeft > 0f;

	public bool IsBossDead => _isDead;

	public int GetBossCurrentHealth() => _bossHealth;

	public int GetBossMaxHealth() => MaxBossHealth;

	// ─────────────────────────────────────────────
	// PRIVAATIT MUUTTUJAT
	// ─────────────────────────────────────────────

	/// <summary>Bossin käyttäytymistila.</summary>
	private enum BossPhase { Dancing, Charging }

	private BossPhase _phase = BossPhase.Dancing;

	/// <summary>Jäljellä oleva tanssinäytösaika.</summary>
	private float _danceTimeLeft;

	/// <summary>Tanssiaikataulun jälkeen: viive ennen <see cref="BeginCharge"/>.</summary>
	private float _postDanceWaitLeft;

	private bool _waitingAfterDance;

	/// <summary>Jäljellä oleva syöksyaika.</summary>
	private float _chargeTimeLeft;

	/// <summary>Kontaktivahingon ajastin — estää liian tiheän vahingon.</summary>
	private float _chargeContactCd;

	private Node3D _player;
	private PlayerController _playerController;
	private AnimationPlayer _animationPlayer;

	/// <summary>Bossin "kotipaikka" — tanssipisteeseen palataan syöksyn jälkeen.</summary>
	private Vector3 _standWorldPos;

	/// <summary>True jos Configure() on kutsuttu ennen _Ready():tä.</summary>
	private bool _configuredFromDirector;

	/// <summary>Estää moninkertaisen osuman samasta miekanlyönnistä.</summary>
	private bool _hasBeenHitThisSwing;

	private bool _isDead;

	/// <summary>Lattian Y-korkeus — pidetään bossi lattiatasolla.</summary>
	private float _floorY;

	private uint _savedCollisionMask = 3;

	private AudioStreamPlayer _bossMusic;

	private SpotLight3D _danceSpotLight;

	// ─────────────────────────────────────────────
	// ALUSTUS
	// ─────────────────────────────────────────────

	/// <summary>
	/// Kutsutaan ennen AddChild kun bossi luodaan koodissa.
	/// Asettaa alkusijainnin ilman Godot-varoituksia.
	/// </summary>
	public void Configure(Vector3 standWorldPosition)
	{
		_configuredFromDirector = true;
		_standWorldPos = standWorldPosition;
		_floorY = standWorldPosition.Y;
		_waitingAfterDance = false;
		_postDanceWaitLeft = 0f;
	}

	public override void _Ready()
	{
		_bossHealth = MaxBossHealth;

		// Törmäyskerrokset: Layer 2 = vihollinen, Mask 3 = pelaaja + maailma
		CollisionLayer = 2;
		CollisionMask = 3;
		_savedCollisionMask = CollisionMask;
		AddToGroup("level1_boss");

		SetupBossMusic();

		// Asetetaan sijainti joko Configure():sta tai editorin arvosta
		if (_configuredFromDirector)
		{
			GlobalPosition = _standWorldPos;
			_floorY = _standWorldPos.Y;
		}
		else
		{
			_standWorldPos = GlobalPosition;
			_floorY = GlobalPosition.Y;
		}

		// Haetaan pelaaja ryhmän kautta
		_player = GetTree().GetFirstNodeInGroup("player") as Node3D;
		_playerController = _player as PlayerController;

		// Ladataan visuaali ja tanssianimaatio
		if (!TryMountDanceVisualAndPlayer())
		{
			GD.PrintErr("BossLevel1: Dance FBX puuttuu tai virheellinen — polku: " + DanceFbxPath);
			return;
		}

		// Ladataan juoksuanimaatio sekä kuolema animaatio without skin -tiedostosta
		LoadAnim(FastRunFbxPath, MixamoAnimSourceName, RunClipName, loop: true);
		
		LoadAnim(DeathFbxPath, MixamoAnimSourceName, DeathClipName, loop: false);

		// Aloitetaan tanssi
		_animationPlayer.Play(DanceClipName);
		_danceTimeLeft = (float)GD.RandRange(DanceDurationMin, DanceDurationMax);
		_phase = BossPhase.Dancing;

		// Linkitetään tekstuurit meshiin
		ApplyBossTextures();

		ApplyStandVerticalAdjustments();

		SetupDanceHighlightLight();
	}

	public override void _ExitTree()
	{
		StopBossMusic();
		base._ExitTree();
	}

	private void SetupBossMusic()
	{
		if (string.IsNullOrWhiteSpace(BossMusicPath))
			return;

		_bossMusic = new AudioStreamPlayer { Name = "BossMusicPlayer" };
		AddChild(_bossMusic);

		var stream = GD.Load<AudioStream>(BossMusicPath);
		if (stream == null)
		{
			GD.PrintErr($"BossLevel1: musiikkia ei löydy — laita tiedosto: {BossMusicPath}");
			_bossMusic.QueueFree();
			_bossMusic = null;
			return;
		}

		switch (stream)
		{
			case AudioStreamMP3 mp3:
				mp3.Loop = true;
				break;
			case AudioStreamOggVorbis ogg:
				ogg.Loop = true;
				break;
		}

		_bossMusic.Stream = stream;
		_bossMusic.VolumeDb = BossMusicVolumeDb;
		_bossMusic.Play();
	}

	private void StopBossMusic()
	{
		if (_bossMusic == null)
			return;
		if (_bossMusic.Playing)
			_bossMusic.Stop();
		_bossMusic.Stream = null;
		_bossMusic.QueueFree();
		_bossMusic = null;
	}

	private void SetupDanceHighlightLight()
	{
		if (!BossDanceHighlightEnabled)
			return;

		_danceSpotLight = new SpotLight3D { Name = "BossDanceSpotlight" };
		_danceSpotLight.LightColor = BossDanceLightColor;
		_danceSpotLight.LightEnergy = BossDanceLightEnergy;
		_danceSpotLight.SpotRange = BossDanceLightRange;
		_danceSpotLight.SpotAngle = BossDanceSpotAngleDeg;
		_danceSpotLight.ShadowEnabled = false;
		_danceSpotLight.Visible = false;
		AddChild(_danceSpotLight);
	}

	private void ApplyStandVerticalAdjustments()
	{
		if (SnapFeetToFloorOnReady)
			TrySnapFeetToWorldFloor();
		if (!Mathf.IsZeroApprox(AdditionalStandLowerY))
			ShiftStandAndPosition(-AdditionalStandLowerY);
			GD.Print($"[BossSnap] GlobalPosition.Y after snap = {GlobalPosition.Y:F3}");
	}

	private void ShiftStandAndPosition(float deltaY)
	{
		var d = Vector3.Up * deltaY;
		GlobalPosition += d;
		_standWorldPos += d;
		_floorY = GlobalPosition.Y;
	}

	/// <summary>Lattia-säde juuren XZ:stä; oma RID pois jotta isokapseli ei osu itseensä.</summary>
	private void TrySnapFeetToWorldFloor()
	{
		if (!IsInsideTree())
			return;
		var space = GetWorld3D()?.DirectSpaceState;
		if (space == null)
			return;

		Vector3 p = GlobalPosition;
		var from = p + Vector3.Up * FeetSnapRayStartUpM;
		var to = p + Vector3.Down * FeetSnapRayLengthDownM;
		var q = PhysicsRayQueryParameters3D.Create(from, to);
		q.CollideWithAreas = false;
		q.Exclude = new Godot.Collections.Array<Rid> { GetRid() };

		var hit = space.IntersectRay(q);
		if (hit.Count == 0 || !hit.ContainsKey("position"))
			return;

		float floorY = ((Vector3)hit["position"]).Y;
		float targetRootY = floorY + FeetSnapOffsetAboveFloor;
		ShiftStandAndPosition(targetRootY - GlobalPosition.Y);
	}

	// ─────────────────────────────────────────────
	// TEKSTUURIEN LINKITYS
	// ─────────────────────────────────────────────

	/// <summary>
	/// Luo StandardMaterial3D ja linkittää tekstuurit kaikkiin MeshInstance3D-nodeeihin.
	/// Kutsutaan _Ready():ssä visuaalin mounttaamisen jälkeen.
	/// </summary>
	private void ApplyBossTextures()
	{
		// Ladataan tekstuurit tiedostoista
		var albedo    = GD.Load<Texture2D>(TextureBasePath);
		var normal    = GD.Load<Texture2D>(TextureNormalPath);
		var roughness = GD.Load<Texture2D>(TextureRoughnessPath);
		var metallic  = GD.Load<Texture2D>(TextureMetallicPath);

		if (albedo == null)
		{
			GD.PrintErr("BossLevel1: Albedo-tekstuuri ei löydy: " + TextureBasePath);
			return;
		}

		// Luodaan materiaali
		var mat = new StandardMaterial3D();
		mat.AlbedoTexture    = albedo;
		mat.NormalEnabled    = normal != null;
		mat.NormalTexture    = normal;
		mat.RoughnessTexture = roughness;
		mat.MetallicTexture  = metallic;

		// Käydään läpi kaikki MeshInstance3D:t hahmon hierarkiassa
		// FindChildren etsii rekursiivisesti kaikki lapset
		foreach (var node in FindChildren("*", "MeshInstance3D", true, false))
		{
			if (node is MeshInstance3D mi)
			{
				// Asetetaan materiaali jokaiselle pinnalle (surface)
				int surfaceCount = mi.GetSurfaceOverrideMaterialCount();
				if (surfaceCount == 0)
				{
					// Jos pintoja ei ole vielä laskettu, kokeile suoraan
					mi.SetSurfaceOverrideMaterial(0, mat);
				}
				else
				{
					for (int i = 0; i < surfaceCount; i++)
						mi.SetSurfaceOverrideMaterial(i, mat);
				}
				GD.Print($"BossLevel1: Tekstuuri asetettu meshille: {mi.Name}");
			}
		}
	}

	// ─────────────────────────────────────────────
	// VISUAALIN LATAUS
	// ─────────────────────────────────────────────

	/// <summary>
	/// Yrittää löytää tai ladata bossin visuaalin ja AnimationPlayerin.
	/// Ensin etsitään editorissa lisätty mesh, sitten ladataan FBX koodissa.
	/// </summary>
	private bool TryMountDanceVisualAndPlayer()
	{
		// 1) Etsitään editorissa lisätty BossVisual-node
		Node3D visualRoot = GetNodeOrNull<Node3D>("BossVisual");

		// Jos ei löydy nimellä, etsitään lapsi jolla on AnimationPlayer
		if (visualRoot == null)
		{
			foreach (Node child in GetChildren())
			{
				if (child is CollisionShape3D) continue;
				if (child is not Node3D n3) continue;
				if (n3.FindChild("AnimationPlayer", true, false) is AnimationPlayer)
				{
					visualRoot = n3;
					visualRoot.Name = "BossVisual"; // Nimetään uudelleen löytämistä varten
					break;
				}
			}
		}

		// Editorissa lisätty mesh löytyi
		if (visualRoot != null)
		{
			_animationPlayer = visualRoot.FindChild("AnimationPlayer", true, false) as AnimationPlayer;
			if (_animationPlayer == null) return false;
			EnsureAnimationLibrary();
			if (!CloneMixamoClipFromSamePlayer(DanceFbxPath, DanceClipName)) return false;
			ApplyBossVisualScale(visualRoot);
			_animationPlayer.Play(DanceClipName);
			return true;
		}

		// 2) Ei editorissa lisättyä meshiä — ladataan FBX koodissa
		var scene = GD.Load<PackedScene>(DanceFbxPath);
		if (scene == null) return false;

		var root = scene.Instantiate() as Node3D;
		if (root == null) return false;

		root.Name = "BossVisual";
		AddChild(root);

		_animationPlayer = root.FindChild("AnimationPlayer", true, false) as AnimationPlayer;
		if (_animationPlayer == null) { root.QueueFree(); return false; }

		EnsureAnimationLibrary();
		if (!CloneMixamoClipFromSamePlayer(DanceFbxPath, DanceClipName)) return false;
		ApplyBossVisualScale(root);
		_animationPlayer.Play(DanceClipName);
		return true;
	}

	/// <summary>Skaalaa visuaalinode BossVisualUniformScale-kertoimen mukaan.</summary>
	private void ApplyBossVisualScale(Node3D visualRoot)
	{
		if (visualRoot == null || BossVisualUniformScale <= 0f) return;
		if (Mathf.IsEqualApprox(BossVisualUniformScale, 1f)) return;
		visualRoot.Scale = Vector3.One * BossVisualUniformScale;
	}

	/// <summary>Varmistaa että AnimationPlayerilla on tyhjä animaatiokirjasto.</summary>
	private void EnsureAnimationLibrary()
	{
		if (_animationPlayer == null) return;
		try { if (_animationPlayer.GetAnimationLibrary("") != null) return; }
		catch { }
		var lib = new AnimationLibrary();
		_animationPlayer.AddAnimationLibrary("", lib);
	}

	// ─────────────────────────────────────────────
	// FYSIIKKA — suoritetaan joka framella
	// ─────────────────────────────────────────────

	public override void _PhysicsProcess(double delta)
	{
		if (_isDead || !IsInsideTree() || _animationPlayer == null) return;

		float dt = (float)delta;

		// Lasketaan kontaktivahingon ajastin
		if (_chargeContactCd > 0f) _chargeContactCd -= dt;

		// Haetaan pelaaja uudelleen jos kadonnut
		if (_player == null || !GodotObject.IsInstanceValid(_player))
		{
			_player = GetTree().GetFirstNodeInGroup("player") as Node3D;
			_playerController = _player as PlayerController;
		}

		// Käyttäytyminen vaiheen mukaan
		switch (_phase)
		{
			case BossPhase.Dancing:  ProcessDancing(dt);  break;
			case BossPhase.Charging:
				UpdateDanceHighlightLight(false);
				ProcessCharging(dt);
				break;
		}

		// Tarkistetaan osuuko pelaajan miekka
		TrySwordHits();
		MoveAndSlide();

		// Jolt-fysiikka voi depenetroida bossit irti pelaajasta MoveAndSlide():ssa
		// vaikka Velocity=Zero — lukitaan positio uudestaan tanssivaiheen jälkeen.
		if (_phase == BossPhase.Dancing)
			GlobalPosition = _standWorldPos;
	}

	// ─────────────────────────────────────────────
	// TANSSIVAIHEEN LOGIIKKA
	// ─────────────────────────────────────────────

	/// <summary>
	/// Tanssivaiheessa bossi pysyy paikallaan ja laskee ajastinta.
	/// Kun aika loppuu, siirrytään syöksyvaiheeseen.
	/// </summary>
	private void ProcessDancing(float dt)
	{
		GlobalPosition = _standWorldPos; // Pidetään bossi paikallaan
		Velocity = Vector3.Zero;

		FaceTowardActiveCamera();
		UpdateDanceHighlightLight(true);

		if (_waitingAfterDance)
		{
			_postDanceWaitLeft -= dt;
			if (_postDanceWaitLeft <= 0f)
			{
				_waitingAfterDance = false;
				BeginCharge();
			}
			return;
		}

		_danceTimeLeft -= dt;
		if (_danceTimeLeft > 0f) return;

		_waitingAfterDance = true;
		_postDanceWaitLeft = Mathf.Max(0f, PostDanceDelayBeforeCharge);
		if (_postDanceWaitLeft <= 0f)
		{
			_waitingAfterDance = false;
			BeginCharge();
		}
	}

	/// <summary>Tanssissa katsotaan aktiivista kameraa (XZ) + 180° kuten Mixamo-juoksu.</summary>
	private void FaceTowardActiveCamera()
	{
		if (!IsInsideTree())
			return;

		Camera3D cam = GetViewport()?.GetCamera3D();
		Vector3 target = cam != null && cam.IsInsideTree()
			? cam.GlobalPosition
			: (_player != null && GodotObject.IsInstanceValid(_player) ? _player.GlobalPosition : GlobalPosition + Vector3.Forward);

		var look = target with { Y = GlobalPosition.Y };
		if (GlobalPosition.DistanceTo(look) < 0.06f)
			return;

		LookAt(look, Vector3.Up);
		RotateY(Mathf.Pi);
	}

	private void UpdateDanceHighlightLight(bool dancePhase)
	{
		if (_danceSpotLight == null)
			return;
		_danceSpotLight.Visible = dancePhase && BossDanceHighlightEnabled;
		if (_danceSpotLight.Visible)
		{
			_danceSpotLight.LightEnergy = BossDanceLightEnergy;
			_danceSpotLight.SpotRange = BossDanceLightRange;
			_danceSpotLight.SpotAngle = BossDanceSpotAngleDeg;
			_danceSpotLight.LightColor = BossDanceLightColor;
			UpdateBossDanceSpotTransform();
		}
	}

	/// <summary>Päivittää spotin maailmasijainnin ja suunnan (kamera kohti, kohde bossin yläpuolella).</summary>
	private void UpdateBossDanceSpotTransform()
	{
		if (_danceSpotLight == null || !_danceSpotLight.Visible || !_danceSpotLight.IsInsideTree() || !IsInsideTree())
			return;

		Vector3 anchor = GlobalPosition;
		Camera3D cam = GetViewport()?.GetCamera3D();
		Vector3 camPos = cam != null && cam.IsInsideTree()
			? cam.GlobalPosition
			: anchor + Vector3.Forward * 8f;

		Vector3 flat = camPos with { Y = anchor.Y };
		flat -= anchor with { Y = anchor.Y };
		if (flat.LengthSquared() < 1e-6f)
			flat = Vector3.Forward;
		else
			flat = flat.Normalized();

		Vector3 eyeWorld = anchor + Vector3.Up * BossDanceSpotHeightM + flat * BossDanceSpotTowardCameraM;
		Vector3 aimWorld = anchor + Vector3.Up * BossDanceSpotAimYOffsetM;
		_danceSpotLight.GlobalPosition = eyeWorld;
		_danceSpotLight.LookAt(aimWorld, Vector3.Up);
	}

	/// <summary>
	/// Aloittaa syöksyn: teleporttaa areenan reunalle ja suuntaa pelaajaan.
	/// </summary>
	private void BeginCharge()
	{
		UpdateDanceHighlightLight(false);
		_phase = BossPhase.Charging;
		// Syöksy: vain kerros 1 (lattia + pelaaja) — ei arcade-proppeja (kerros 5).
		CollisionMask = 1;

		// Teleportataan satunnaiselle reunalle
		Vector3 edge = PickRandomArenaEdge();
		edge.Y = _floorY;
		GlobalPosition = edge;

		// Käännetään pelaajaa kohti
		if (_player != null && GodotObject.IsInstanceValid(_player) && _player.IsInsideTree())
		{
			var look = _player.GlobalPosition with { Y = GlobalPosition.Y };
			if (GlobalPosition.DistanceTo(look) > 0.05f)
			{
				LookAt(look, Vector3.Up);
				RotateY(Mathf.Pi);
			}
		}

		// Vaihdetaan juoksuanimaatioon
		if (_animationPlayer.HasAnimation(RunClipName))
			_animationPlayer.Play(RunClipName);

		// Lasketaan syöksyn suunta pelaajaa kohti
		Vector3 dir = Vector3.Zero;
		if (_player != null && GodotObject.IsInstanceValid(_player))
		{
			dir = _player.GlobalPosition - GlobalPosition;
			dir.Y = 0f;
			if (dir.LengthSquared() > 1e-5f) dir = dir.Normalized();
		}
		if (dir.LengthSquared() < 1e-5f)
			dir = -GlobalTransform.Basis.Z;

		float vy = IsOnFloor() ? 0f : Velocity.Y;
		Velocity = new Vector3(dir.X * ChargeSpeed, vy, dir.Z * ChargeSpeed);
		_chargeTimeLeft = ChargeMaxSeconds;
	}

	// ─────────────────────────────────────────────
	// SYÖKSYVAIHEEN LOGIIKKA
	// ─────────────────────────────────────────────

	/// <summary>
	/// Syöksyvaiheessa bossi seuraa pelaajaa ja vahingoittaa kontaktista.
	/// Palataan tanssimaan kun aika loppuu tai bossi menee ulos areenasta.
	/// </summary>
	private void ProcessCharging(float dt)
	{
		_chargeTimeLeft -= dt;

		// Seurataan pelaajaa reaaliajassa
		if (_player != null && GodotObject.IsInstanceValid(_player))
		{
			var flat = _player.GlobalPosition - GlobalPosition;
			flat.Y = 0f;
			if (flat.LengthSquared() > 1e-5f)
			{
				flat = flat.Normalized();
				Velocity = new Vector3(flat.X * ChargeSpeed, Velocity.Y, flat.Z * ChargeSpeed);
			}
		}

		// Painovoima
		if (!IsOnFloor())
			Velocity = new Vector3(Velocity.X, Velocity.Y - 35f * dt, Velocity.Z);
		else
			Velocity = new Vector3(Velocity.X, 0f, Velocity.Z);

		// Kontaktivahinko
		TryChargeContactDamage();

		// Palataan tanssimaan jos aika loppuu tai mennään ulos areenasta
		if (_chargeTimeLeft <= 0f
			|| Mathf.Abs(GlobalPosition.X) > ArenaEdgeHalf + 1.5f
			|| Mathf.Abs(GlobalPosition.Z) > ArenaEdgeHalf + 1.5f)
			ReturnToDance();
	}

	/// <summary>Tarkistaa osuuko bossi pelaajaan ja vahingoittaa tarvittaessa.</summary>
	private void TryChargeContactDamage()
	{
		// Vain syöksyvaiheessa — tanssi ei saa vahingoittaa (turva myös jos tila epäsynkassa).
		if (_phase != BossPhase.Charging || _isDead)
			return;
		if (_chargeContactCd > 0f || _playerController == null) return;

		Vector3 threatFromBoss = GlobalPosition + Vector3.Up * HitCenterYOffset;

		for (int i = 0; i < GetSlideCollisionCount(); i++)
		{
			if (GetSlideCollision(i).GetCollider() is PlayerController)
			{
				if (_playerController.IsBlockingEffectiveAgainst(threatFromBoss))
					return;

				var hc = _playerController.GetNodeOrNull<HealthComponent>("HealthComponent");
				hc?.TakeDamage(Mathf.RoundToInt(ChargeContactDamage));
				_chargeContactCd = ChargeContactCooldown;
				return;
			}
		}
	}

	/// <summary>Palaa tanssivaiheeseen: siirtyy kotipaikalle ja aloittaa tanssin.</summary>
	private void ReturnToDance()
	{
		GlobalPosition = _standWorldPos;
		Velocity = Vector3.Zero;
		CollisionMask = _savedCollisionMask;
		_phase = BossPhase.Dancing;
		_waitingAfterDance = false;
		_postDanceWaitLeft = 0f;
		_danceTimeLeft = (float)GD.RandRange(DanceDurationMin, DanceDurationMax);
		if (_animationPlayer.HasAnimation(DanceClipName))
			_animationPlayer.Play(DanceClipName);
	}

	/// <summary>Valitsee satunnaisen spawnauspiste areenan reunalta.</summary>
	private Vector3 PickRandomArenaEdge()
	{
		int edge = GD.RandRange(0, 3);
		double t = GD.RandRange(-ArenaEdgeHalf + 0.5, ArenaEdgeHalf - 0.5);
		return edge switch
		{
			0 => new Vector3(-ArenaEdgeHalf, _floorY, (float)t), // Länsi
			1 => new Vector3(ArenaEdgeHalf,  _floorY, (float)t), // Itä
			2 => new Vector3((float)t, _floorY, -ArenaEdgeHalf), // Pohjoinen
			_ => new Vector3((float)t, _floorY,  ArenaEdgeHalf), // Etelä
		};
	}

	// ─────────────────────────────────────────────
	// MIEKAN OSUMAT
	// ─────────────────────────────────────────────

	/// <summary>
	/// Tarkistaa osuuko pelaajan miekka bossiin.
	/// Tanssivaiheessa vain vahva isku (R1, dmg >= 3) osuu.
	/// Syöksyvaiheessa molemmat iskut osuvat.
	/// </summary>
	private void TrySwordHits()
	{
		if (_playerController == null || !_playerController.IsMeleeAttackActive())
		{
			_hasBeenHitThisSwing = false;
			return;
		}

		// Tarkistetaan animaation ajoitus
		float animTime = _playerController.GetAttackAnimationTime();
		float hitFrom  = _playerController.GetMeleeStrikeWindowStart() + SwordHitActivationTime;
		if (animTime < hitFrom || _hasBeenHitThisSwing) return;

		int dmg = _playerController.GetMeleeAttackDamage();

		// Tanssivaiheessa vain vahva isku (R1) osuu
		if (_phase == BossPhase.Dancing && dmg < 3) return;

		float proxMax = _playerController.GetMeleeHitProximityMax() + SwordHitExtraProximityMeters;

		var rootHeights = SwordHitProbeHeights;
		if (rootHeights == null || rootHeights.Length == 0)
			rootHeights = new[] { HitCenterYOffset };

		for (int i = 0; i < rootHeights.Length; i++)
		{
			Vector3 p = GlobalPosition + Vector3.Up * rootHeights[i];
			if (_playerController.CanApplyMeleeHitAtWorldPoint(p, proxMax))
			{
				ApplySwordHitFromPlayer(dmg);
				return;
			}
		}

		var visual = GetNodeOrNull<Node3D>("BossVisual");
		if (visual != null && GodotObject.IsInstanceValid(visual) && visual.IsInsideTree())
		{
			var visOff = SwordHitProbeVisualYOffset;
			if (visOff != null && visOff.Length > 0)
			{
				for (int i = 0; i < visOff.Length; i++)
				{
					Vector3 p = visual.GlobalPosition + Vector3.Up * visOff[i];
					if (_playerController.CanApplyMeleeHitAtWorldPoint(p, proxMax))
					{
						ApplySwordHitFromPlayer(dmg);
						return;
					}
				}
			}
		}
	}

	private void ApplySwordHitFromPlayer(int dmg)
	{
		TakeDamage(dmg);
		_playerController.NotifyMeleeHitLanded();
		_hasBeenHitThisSwing = true;
	}

	// ─────────────────────────────────────────────
	// VAHINKO JA KUOLEMA
	// ─────────────────────────────────────────────

	/// <summary>Vähennetään bossin HP:ta. Kuolee kun HP menee nollaan.</summary>
	public void TakeDamage(int amount)
	{
		if (_isDead) return;
		_bossHealth -= amount;
		if (_bossHealth <= 0)
		{
			_bossHealth = 0;
			Die();
		}
	}

	/// <summary>Bossin kuolema — poistetaan törmäys ja kutistetaan mesh nollaan.</summary>
	private void Die()
	{
		_isDead = true;
		Velocity = Vector3.Zero;
		StopBossMusic();
		UpdateDanceHighlightLight(false);

		var col = GetNodeOrNull<CollisionShape3D>("CollisionShape3D");
		if (col != null) col.Disabled = true;

		// Toista kuolema-animaatio jos löytyy
		if (_animationPlayer != null && _animationPlayer.HasAnimation(DeathClipName))
		{
			_animationPlayer.SpeedScale = 0.25f; // 1.0 = normaali, 0.25 = neljäsosa nopeudesta
			_animationPlayer.Play(DeathClipName);
			float deathLen = (float)_animationPlayer.CurrentAnimationLength;
			// Poistetaan hahmo animaation jälkeen
			GetTree().CreateTimer(deathLen).Timeout += () => QueueFree();
		}
		else
		{
			// Fallback: vanha kutistuminen jos animaatiota ei löydy
			Node3D visual = GetNodeOrNull<Node3D>("BossVisual") ?? (Node3D)this;
			var tween = CreateTween();
			tween.TweenProperty(visual, "scale", Vector3.Zero, DeathShrinkDuration)
				.SetTrans(Tween.TransitionType.Cubic)
				.SetEase(Tween.EaseType.In);
			tween.TweenCallback(Callable.From(() => QueueFree()));
		}
	}

	// ─────────────────────────────────────────────
	// ANIMAATIOIDEN LATAUS
	// ─────────────────────────────────────────────

	/// <summary>
	/// Kloonaa Mixamo-animaation FBX-tiedostosta AnimationPlayeriin.
	/// Käytetään dancing-animaation lataukseen samasta tiedostosta.
	/// </summary>
	private bool CloneMixamoClipFromSamePlayer(string path, string targetName)
	{
		var scene = GD.Load<PackedScene>(path);
		if (scene == null) return false;

		var inst = scene.Instantiate();
		var ap   = inst.FindChild("AnimationPlayer", true, false) as AnimationPlayer;
		if (ap == null) { inst.QueueFree(); return false; }

		Animation anim = null;
		foreach (var n in new[] { MixamoAnimSourceName, MixamoAnimSourceName.Replace("_", ".") })
		{
			if (ap.HasAnimation(n)) { anim = ap.GetAnimation(n); break; }
		}

		inst.QueueFree();
		if (anim == null)
		{
			GD.PrintErr($"BossLevel1: animaatiota '{MixamoAnimSourceName}' ei löydy: {path}");
			return false;
		}

		anim.LoopMode = Animation.LoopModeEnum.Linear;
		var lib = _animationPlayer.GetAnimationLibrary("");
		if (lib.HasAnimation(targetName)) lib.RemoveAnimation(targetName);
		lib.AddAnimation(targetName, anim);
		return true;
	}

	/// <summary>
	/// Lataa animaation without skin -FBX:stä AnimationPlayeriin.
	/// Käytetään run-animaation lataukseen.
	/// </summary>
	private void LoadAnim(string path, string sourceName, string targetName, bool loop = false)
	{
		var scene = GD.Load<PackedScene>(path);
		if (scene == null) { GD.PrintErr($"BossLevel1 LoadAnim: {path}"); return; }

		var inst = scene.Instantiate();
		var ap   = inst.FindChild("AnimationPlayer", true, false) as AnimationPlayer;
		if (ap == null) { inst.QueueFree(); return; }

		Animation anim = null;
		foreach (var name in new[] { sourceName, sourceName.Replace("_", ".") })
		{
			if (ap.HasAnimation(name)) { anim = ap.GetAnimation(name); break; }
		}

		inst.QueueFree();
		if (anim == null) { GD.PrintErr($"BossLevel1: clip '{sourceName}' puuttuu: {path}"); return; }

		if (loop) anim.LoopMode = Animation.LoopModeEnum.Linear;
		var lib = _animationPlayer.GetAnimationLibrary("");
		if (lib.HasAnimation(targetName)) lib.RemoveAnimation(targetName);
		lib.AddAnimation(targetName, anim);
	}
}
