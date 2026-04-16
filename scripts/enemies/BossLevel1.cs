using Godot;

/// <summary>
/// Level 1 -bossi: tanssi (satunnainen kesto) → latautuu reunalle → syöksy pelaajaa kohti → takaisin tanssipisteeseen.
/// FBX-polut säädetään Inspectorissa (Mixamo: dance with skin, fast run without skin).
/// 
/// BOSSIN KÄYTTÄYTYMINEN:
/// 1. Dancing-vaihe: bossi tanssii paikallaan satunnaisen ajan
/// 2. Charging-vaihe: bossi teleporttaa areenan reunalle ja syöksyy pelaajaa kohti
/// 3. Bossi voi ottaa vahinkoa vain tanssiessaan (vahva isku R1 tappaa)
/// 4. osuma.fbx R1-osumalla; syöksyssä Mma Kick kun etäisyys pelaajaan ≤ MmaPotkuLaukaisuEtäisyys → tanssiin takaisin
/// </summary>
public partial class BossLevel1 : CharacterBody3D
{
	// ─────────────────────────────────────────────
	// EXPORTATUT MUUTTUJAT — säädettävissä Inspectorissa
	// ─────────────────────────────────────────────

	/// <summary>Dancing.fbx polku — Mixamosta ladattu with skin.</summary>
	[Export] public string TanssiFbxPolku = "res://assets/models/level1_BossEnemy/Dancing.fbx";

	/// <summary>FastRun.fbx polku — Mixamosta ladattu without skin.</summary>
	[Export] public string NopeaJuoksuFbxPolku = "res://assets/models/level1_BossEnemy/FastRun.fbx";
	
	[Export] public string KuolemaFbxPolku = "res://assets/models/level1_BossEnemy/Death.fbx";

	/// <summary>Osuma-animaatio (with skin) — toistuu R1-osumalla.</summary>
	[Export] public string OsumaReaktioFbxPolku = "res://assets/models/level1_BossEnemy/osuma.fbx";

	/// <summary>MMA-potku (without skin) — syöksyn päätteeksi kun bossi on pelaajan luona.</summary>
	[Export] public string MmaPotkuFbxPolku = "res://assets/models/level1_BossEnemy/Mma Kick.fbx";

	/// <summary>Mixamon animaation sisäinen nimi FBX:ssä.</summary>
	[Export] public string MixamoAnimaatioLähdeNimi = "mixamo_com";

	/// <summary>Tanssianimaation nimi pelissä.</summary>
	[Export] public string TanssiKlipinNimi = "dance";

	/// <summary>Juoksuanimaation nimi pelissä.</summary>
	[Export] public string JuoksuKlipinNimi = "run";
	
	[Export] public string KuolemaKlipinNimi = "death";

	[Export] public string OsumaKlipinNimi = "hit_react";

	[Export] public string MmaPotkuKlipinNimi = "mma_kick";

	/// <summary>Vaakasuora etäisyys (m): MMA-potku käynnistyy kun bossi on tätä lähempänä pelaajaa.</summary>
	[Export] public float MmaPotkuLaukaisuEtäisyys = 2.0f;

	/// <summary>Maksimi-HP — oletus 30 = 10 osumaa × R1-vahinko (3) tanssi-/otteluvaiheessa.</summary>
	[Export] public int BossMaksimiTerveys = 30;

	private int _bossHealth;

	/// <summary>Tanssin minimikesto sekunteina ennen syöksyä.</summary>
	[Export] public float TanssiKestoMinSekuntia = 8.5f;

	/// <summary>Tanssin maksimikesto sekunteina.</summary>
	[Export] public float TanssiKestoMaxSekuntia = 10.5f;

	/// <summary>Aika tanssin päättymisen ja kameran palautumisen jälkeen ennen juoksua (run).</summary>
	[Export] public float ViiveTanssinJälkeenSekuntia = 1.0f;

	/// <summary>Syöksyn nopeus.</summary>
	[Export] public float SyöksyNopeus = 12f;

	/// <summary>Syöksyn maksimikesto — jos pelaajaa ei tavoiteta, palataan tanssimaan.</summary>
	[Export] public float SyöksyMaksimiKestoSekuntia = 3.2f;

	/// <summary>Areenan puolisäde — syöksyn spawnauspisteet ovat tämän etäisyydellä keskipisteestä.</summary>
	[Export] public float AreenanReunanPuolikas = 13.5f;

	/// <summary>Kontaktivahingon määrä syöksyn aikana.</summary>
	[Export] public float SyöksyTörmäysVahinko = 1;

	/// <summary>Aika sekunteina ennen kuin kontaktivahinko voi toistua.</summary>
	[Export] public float SyöksyTörmäysJäähdytys = 2.5f;

	/// <summary>Osumakeskipisteen korkeus Y-akselilla.</summary>
	[Export] public float OsumaKeskusOffsetY = 0.9f;

	/// <summary>Korkeudet juuren <see cref="GlobalPosition"/>ista (jalat) — pitkä mesh + skaala.</summary>
	[Export] public float[] MiekkaIskuKoetuskorkeudet = { 0.35f, 1.0f, 1.85f, 2.7f, 3.5f, 4.35f };

	/// <summary>Lisäpisteet <c>BossVisual</c>-solmun kohdalta (jos FBX on offsetoituna juureen nähden).</summary>
	[Export] public float[] MiekkaIskuKoetusNäkyväOffsetY = { -0.4f, 0.35f, 1.1f, 2.0f, 2.9f };

	/// <summary>Lisää max-etäisyyttä terään verrattuna (korkea hahmo / R1-kapea ikkuna).</summary>
	[Export] public float MiekkaIskuLisäLäheisyys = 0.42f;

	/// <summary>Lisäviive miekan osumaikkunaan (0 = heti kun animaatio alkaa).</summary>
	[Export] public float MiekkaIskuAktivoitumisaika = 0f;

	/// <summary>Kuolemisanimaation kesto sekunteina (kutistuminen).</summary>
	[Export] public float KuolemanKutistumisenKesto = 1.55f;

	/// <summary>
	/// Visuaalisen meshin skaalauskerroin.
	/// Mixamon FBX on usein ~100x liian pieni — oletus ~1,5× aiemmasta (150).
	/// Aseta 1 jos haluat säilyttää scenen lapsen skaalan sellaisenaan.
	/// </summary>
	[Export] public float BossVisuaalinenSkaala = 150f;

	/// <summary>Laske koko hahmoa näin paljon spawnin jälkeen (m) — kun FBX-jalat eivät osu juureen.</summary>
	[Export] public float LisäSeisomaAlennusY = 0f;

	/// <summary>Uudelleenlattiasnap _Ready:ssä (sulkee oman colliderin pois säteestä).</summary>
	[Export] public bool JalatMaahanValmiudessa = true;

	[Export] public float JalatMaahanSädeAlkuYlös = 14f;
	[Export] public float JalatMaahanSädePituusAlas = 24f;
	[Export] public float JalatMaahanOffsetLattiasta = 0.06f;

	/// <summary>Level 1 -bossin taustamusiikki (loop) koko eliniän — tiedosto: musiclevel1.mp3.</summary>
	[Export] public string BossMusiikkiPolku = "res://assets/audio/music/musiclevel1.mp3";

	[Export] public float BossMusiikkiVoimakkuusDb = -4f;

	/// <summary>Kuolema-äänitehoste — sijoita tiedosto tähän polkuun.</summary>
	[Export] public string KuolemaÄäniPolku = "res://assets/audio/sfx/death.mp3";

	[Export] public float KuolemaÄäniVoimakkuusDb = 0f;

	/// <summary>Ääni kun pelaajan miekka osuu bossiin.</summary>
	[Export] public string MiekkaIskuÄäniPolku = "res://assets/audio/sfx/miekka.mp3";

	/// <summary>Bossin vahinkoääni osuman jälkeen.</summary>
	[Export] public string BossVahinkoÄäniPolku = "res://assets/audio/sfx/enemybosshit.mp3";

	[Export] public float OsumaÄäniVoimakkuusDb = 0f;

	/// <summary>Viive sekunteina miekka-äänen ja vahinko-äänen välillä.</summary>
	[Export] public float BossVahinkoÄäniViive = 0.1f;

	/// <summary>Lisäspotti tanssivaiheessa (Pi: varjo pois).</summary>
	[Export] public bool BossTanssiKorostusKäytössä = true;

	[Export] public float BossTanssiValonEnergia = 4.2f;

	/// <summary>Spotin kantama (Godot SpotRange).</summary>
	[Export] public float BossTanssiValonKantama = 14f;

	[Export] public Color BossTanssiValonVäri = new(1f, 0.94f, 0.86f, 1f);

	/// <summary>Valokiilan puolikulma (astetta, Godot SpotAngle).</summary>
	[Export] public float BossTanssiKohdevalonKulmaAstetta = 52f;

	/// <summary>Valon sijainti: korkeus bossin jaloista (maailmay).</summary>
	[Export] public float BossTanssiKohdevalonKorkeus = 4.2f;

	/// <summary>Kuinka paljon valo siirtyy kameraa kohti XZ-tasossa (lavaste).</summary>
	[Export] public float BossTanssiKohdevaloKohtiKameraa = 1.1f;

	/// <summary>LookAt-kohdan korkeus bossin jaloista (valo osoittaa tähän pisteeseen).</summary>
	[Export] public float BossTanssiKohdevaloTähtäysOffsetY = 1.35f;

	// ─────────────────────────────────────────────
	// TEKSTUURIPOLUT — säädettävissä Inspectorissa
	// ─────────────────────────────────────────────

	/// <summary>Albedo (väri) tekstuuri.</summary>
	[Export] public string TekstuuriPääPolku     = "res://assets/models/level1_BossEnemy/anthropomorphichedgehog3dmodel_basecolor.JPEG";

	/// <summary>Normal map tekstuuri (pinnan yksityiskohdat).</summary>
	[Export] public string TekstuuriNormaaliPolku   = "res://assets/models/level1_BossEnemy/anthropomorphichedgehog3dmodel_normal.JPEG";

	/// <summary>Roughness tekstuuri (pinnan karheus).</summary>
	[Export] public string TekstuuriKarheusPolku = "res://assets/models/level1_BossEnemy/anthropomorphichedgehog3dmodel_roughness.JPEG";

	/// <summary>Metallic tekstuuri (metallinen pinta).</summary>
	[Export] public string TekstuuriMetalliPolku  = "res://assets/models/level1_BossEnemy/anthropomorphichedgehog3dmodel_metallic.JPEG";

	// ─────────────────────────────────────────────
	// JULKISET OMINAISUUDET
	// ─────────────────────────────────────────────

	/// <summary>True kun bossi tanssii — vain tällöin vahva isku (R1) voi osua (ei post-tanssi-viivettä).</summary>
	public bool IsDanceVulnerable => !_isDead && _phase == BossPhase.Dancing && !_waitingAfterDance;

	/// <summary>True vain tanssilaskurin aikana — lähipiirikamera (ei post-tanssi-viivettä).</summary>
	public bool IsBossCloseupDanceCameraActive => !_isDead && _phase == BossPhase.Dancing && !_waitingAfterDance && _danceTimeLeft > 0f;

	public bool IsBossDead => _isDead;

	public int GetBossCurrentHealth() => _bossHealth;

	public int GetBossMaxHealth() => BossMaksimiTerveys;

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

	// Hit-feedback
	private CameraFollow _camera;
	private Vector3 _bossVisualBaseScale;
	private Tween _recoilTween;
	private Tween _squashTween;
	private Tween _hitFlashTween;

	private bool _hitReactPlaying;

	/// <summary>Animaatio johon palataan osuman jälkeen (tanssi tai juoksu).</summary>
	private string _resumeClipAfterHit = "";

	private bool _chargingKickActive;

	private bool _kickTriggeredThisCharge;

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
		_bossHealth = BossMaksimiTerveys;

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
			GD.PrintErr("BossLevel1: Dance FBX puuttuu tai virheellinen — polku: " + TanssiFbxPolku);
			return;
		}

		// Ladataan juoksuanimaatio sekä kuolema animaatio without skin -tiedostosta
		LoadAnim(NopeaJuoksuFbxPolku, MixamoAnimaatioLähdeNimi, JuoksuKlipinNimi, loop: true);
		
		LoadAnim(KuolemaFbxPolku, MixamoAnimaatioLähdeNimi, KuolemaKlipinNimi, loop: false);

		LoadAnim(OsumaReaktioFbxPolku, MixamoAnimaatioLähdeNimi, OsumaKlipinNimi, loop: false);
		LoadAnim(MmaPotkuFbxPolku, MixamoAnimaatioLähdeNimi, MmaPotkuKlipinNimi, loop: false);

		_animationPlayer.AnimationFinished += OnBossAnimationFinished;

		// Aloitetaan tanssi
		_animationPlayer.Play(TanssiKlipinNimi);
		_danceTimeLeft = (float)GD.RandRange(TanssiKestoMinSekuntia, TanssiKestoMaxSekuntia);
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
		if (string.IsNullOrWhiteSpace(BossMusiikkiPolku))
			return;

		_bossMusic = new AudioStreamPlayer { Name = "BossMusicPlayer" };
		AddChild(_bossMusic);

		var stream = GD.Load<AudioStream>(BossMusiikkiPolku);
		if (stream == null)
		{
			GD.PrintErr($"BossLevel1: musiikkia ei löydy — laita tiedosto: {BossMusiikkiPolku}");
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
		_bossMusic.VolumeDb = BossMusiikkiVoimakkuusDb;
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

	private void PlayDeathSfx()
	{
		PlaySfxOnRoot(KuolemaÄäniPolku, KuolemaÄäniVoimakkuusDb);
	}

	private void SetupDanceHighlightLight()
	{
		if (!BossTanssiKorostusKäytössä)
			return;

		_danceSpotLight = new SpotLight3D { Name = "BossDanceSpotlight" };
		_danceSpotLight.LightColor = BossTanssiValonVäri;
		_danceSpotLight.LightEnergy = BossTanssiValonEnergia;
		_danceSpotLight.SpotRange = BossTanssiValonKantama;
		_danceSpotLight.SpotAngle = BossTanssiKohdevalonKulmaAstetta;
		_danceSpotLight.ShadowEnabled = false;
		_danceSpotLight.Visible = false;
		AddChild(_danceSpotLight);
	}

	private void ApplyStandVerticalAdjustments()
	{
		if (JalatMaahanValmiudessa)
			TrySnapFeetToWorldFloor();
		if (!Mathf.IsZeroApprox(LisäSeisomaAlennusY))
			ShiftStandAndPosition(-LisäSeisomaAlennusY);
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
		var from = p + Vector3.Up * JalatMaahanSädeAlkuYlös;
		var to = p + Vector3.Down * JalatMaahanSädePituusAlas;
		var q = PhysicsRayQueryParameters3D.Create(from, to);
		q.CollideWithAreas = false;
		q.Exclude = new Godot.Collections.Array<Rid> { GetRid() };

		var hit = space.IntersectRay(q);
		if (hit.Count == 0 || !hit.ContainsKey("position"))
			return;

		float floorY = ((Vector3)hit["position"]).Y;
		float targetRootY = floorY + JalatMaahanOffsetLattiasta;
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
		var albedo    = GD.Load<Texture2D>(TekstuuriPääPolku);
		var normal    = GD.Load<Texture2D>(TekstuuriNormaaliPolku);
		var roughness = GD.Load<Texture2D>(TekstuuriKarheusPolku);
		var metallic  = GD.Load<Texture2D>(TekstuuriMetalliPolku);

		if (albedo == null)
		{
			GD.PrintErr("BossLevel1: Albedo-tekstuuri ei löydy: " + TekstuuriPääPolku);
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
			if (!CloneMixamoClipFromSamePlayer(TanssiFbxPolku, TanssiKlipinNimi)) return false;
			ApplyBossVisualScale(visualRoot);
			_animationPlayer.Play(TanssiKlipinNimi);
			return true;
		}

		// 2) Ei editorissa lisättyä meshiä — ladataan FBX koodissa
		var scene = GD.Load<PackedScene>(TanssiFbxPolku);
		if (scene == null) return false;

		var root = scene.Instantiate() as Node3D;
		if (root == null) return false;

		root.Name = "BossVisual";
		AddChild(root);

		_animationPlayer = root.FindChild("AnimationPlayer", true, false) as AnimationPlayer;
		if (_animationPlayer == null) { root.QueueFree(); return false; }

		EnsureAnimationLibrary();
		if (!CloneMixamoClipFromSamePlayer(TanssiFbxPolku, TanssiKlipinNimi)) return false;
		ApplyBossVisualScale(root);
		_animationPlayer.Play(TanssiKlipinNimi);
		return true;
	}

	/// <summary>Skaalaa visuaalinode BossVisuaalinenSkaala-kertoimen mukaan.</summary>
	private void ApplyBossVisualScale(Node3D visualRoot)
	{
		if (visualRoot == null || BossVisuaalinenSkaala <= 0f) return;
		if (!Mathf.IsEqualApprox(BossVisuaalinenSkaala, 1f))
			visualRoot.Scale = Vector3.One * BossVisuaalinenSkaala;
		_bossVisualBaseScale = visualRoot.Scale; // tallennetaan squash-laskentaa varten
	}

	private CameraFollow GetOrFindCamera()
	{
		if (_camera != null && GodotObject.IsInstanceValid(_camera)) return _camera;
		_camera = GetViewport()?.GetCamera3D() as CameraFollow;
		return _camera;
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

		// Osuma-animaatio: ei kuluteta tanssiaikaa
		if (_hitReactPlaying)
			return;

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
		_postDanceWaitLeft = Mathf.Max(0f, ViiveTanssinJälkeenSekuntia);
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
		_danceSpotLight.Visible = dancePhase && BossTanssiKorostusKäytössä;
		if (_danceSpotLight.Visible)
		{
			_danceSpotLight.LightEnergy = BossTanssiValonEnergia;
			_danceSpotLight.SpotRange = BossTanssiValonKantama;
			_danceSpotLight.SpotAngle = BossTanssiKohdevalonKulmaAstetta;
			_danceSpotLight.LightColor = BossTanssiValonVäri;
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

		Vector3 eyeWorld = anchor + Vector3.Up * BossTanssiKohdevalonKorkeus + flat * BossTanssiKohdevaloKohtiKameraa;
		Vector3 aimWorld = anchor + Vector3.Up * BossTanssiKohdevaloTähtäysOffsetY;
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
		_chargingKickActive = false;
		_kickTriggeredThisCharge = false;
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
		if (_animationPlayer.HasAnimation(JuoksuKlipinNimi))
			_animationPlayer.Play(JuoksuKlipinNimi);

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
		Velocity = new Vector3(dir.X * SyöksyNopeus, vy, dir.Z * SyöksyNopeus);
		_chargeTimeLeft = SyöksyMaksimiKestoSekuntia;
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
		// MMA-potku: pysähdys, ei kuluteta syöksyaikaa
		if (_chargingKickActive)
		{
			ApplyChargeVerticalPhysics(dt);
			Velocity = new Vector3(0f, Velocity.Y, 0f);
			return;
		}

		// Miekan osuma-animaatio syöksyn aikana: pysäytä vaakasuunta
		if (_hitReactPlaying)
		{
			ApplyChargeVerticalPhysics(dt);
			Velocity = new Vector3(0f, Velocity.Y, 0f);
			return;
		}

		_chargeTimeLeft -= dt;

		// MMA-potku kun ollaan tarpeeksi lähellä (kerran per syöksy)
		if (!_kickTriggeredThisCharge
			&& _player != null && GodotObject.IsInstanceValid(_player)
			&& _animationPlayer != null
			&& _animationPlayer.HasAnimation(MmaPotkuKlipinNimi))
		{
			var flat = _player.GlobalPosition - GlobalPosition;
			flat.Y = 0f;
			if (flat.Length() <= MmaPotkuLaukaisuEtäisyys)
			{
				_kickTriggeredThisCharge = true;
				_chargingKickActive = true;
				_animationPlayer.Play(MmaPotkuKlipinNimi);
				TryKickContactDamage();
				ApplyChargeVerticalPhysics(dt);
				Velocity = new Vector3(0f, Velocity.Y, 0f);
				return;
			}
		}

		// Seurataan pelaajaa reaaliajassa
		if (_player != null && GodotObject.IsInstanceValid(_player))
		{
			var flat = _player.GlobalPosition - GlobalPosition;
			flat.Y = 0f;
			if (flat.LengthSquared() > 1e-5f)
			{
				flat = flat.Normalized();
				Velocity = new Vector3(flat.X * SyöksyNopeus, Velocity.Y, flat.Z * SyöksyNopeus);
			}
		}

		ApplyChargeVerticalPhysics(dt);

		// Kontaktivahinko
		TryChargeContactDamage();

		// Palataan tanssimaan jos aika loppuu tai mennään ulos areenasta
		if (_chargeTimeLeft <= 0f
			|| Mathf.Abs(GlobalPosition.X) > AreenanReunanPuolikas + 1.5f
			|| Mathf.Abs(GlobalPosition.Z) > AreenanReunanPuolikas + 1.5f)
			ReturnToDance();
	}

	private void ApplyChargeVerticalPhysics(float dt)
	{
		if (!IsOnFloor())
			Velocity = new Vector3(Velocity.X, Velocity.Y - 35f * dt, Velocity.Z);
		else
			Velocity = new Vector3(Velocity.X, 0f, Velocity.Z);
	}

	/// <summary>Vahinko MMA-potkun käynnistyksessä (ei jatkuvaa törmäystä).</summary>
	private void TryKickContactDamage()
	{
		if (_playerController == null || _isDead) return;
		Vector3 threatFromBoss = GlobalPosition + Vector3.Up * OsumaKeskusOffsetY;
		if (_playerController.IsBlockingEffectiveAgainst(threatFromBoss))
			return;
		var hc = _playerController.GetNodeOrNull<HealthComponent>("HealthComponent");
		hc?.TakeDamage(Mathf.RoundToInt(SyöksyTörmäysVahinko));
		_chargeContactCd = SyöksyTörmäysJäähdytys;
	}

	/// <summary>Tarkistaa osuuko bossi pelaajaan ja vahingoittaa tarvittaessa.</summary>
	private void TryChargeContactDamage()
	{
		// Vain syöksyvaiheessa — tanssi ei saa vahingoittaa (turva myös jos tila epäsynkassa).
		if (_phase != BossPhase.Charging || _isDead)
			return;
		if (_chargingKickActive || _hitReactPlaying)
			return;
		if (_chargeContactCd > 0f || _playerController == null) return;

		Vector3 threatFromBoss = GlobalPosition + Vector3.Up * OsumaKeskusOffsetY;

		for (int i = 0; i < GetSlideCollisionCount(); i++)
		{
			if (GetSlideCollision(i).GetCollider() is PlayerController)
			{
				if (_playerController.IsBlockingEffectiveAgainst(threatFromBoss))
					return;

				var hc = _playerController.GetNodeOrNull<HealthComponent>("HealthComponent");
				hc?.TakeDamage(Mathf.RoundToInt(SyöksyTörmäysVahinko));
				_chargeContactCd = SyöksyTörmäysJäähdytys;
				return;
			}
		}
	}

	/// <summary>Palaa tanssivaiheeseen: siirtyy kotipaikalle ja aloittaa tanssin.</summary>
	private void ReturnToDance()
	{
		_chargingKickActive = false;
		GlobalPosition = _standWorldPos;
		Velocity = Vector3.Zero;
		CollisionMask = _savedCollisionMask;
		_phase = BossPhase.Dancing;
		_waitingAfterDance = false;
		_postDanceWaitLeft = 0f;
		_danceTimeLeft = (float)GD.RandRange(TanssiKestoMinSekuntia, TanssiKestoMaxSekuntia);
		if (_animationPlayer.HasAnimation(TanssiKlipinNimi))
			_animationPlayer.Play(TanssiKlipinNimi);
	}

	private void OnBossAnimationFinished(StringName animName)
	{
		if (animName == OsumaKlipinNimi)
		{
			_hitReactPlaying = false;
			if (_isDead) return;
			if (!string.IsNullOrEmpty(_resumeClipAfterHit) && _animationPlayer != null
				&& _animationPlayer.HasAnimation(_resumeClipAfterHit))
				_animationPlayer.Play(_resumeClipAfterHit);
			_resumeClipAfterHit = "";
			return;
		}

		if (animName == MmaPotkuKlipinNimi && _chargingKickActive)
		{
			_chargingKickActive = false;
			if (!_isDead)
				ReturnToDance();
		}
	}

	/// <summary>Valitsee satunnaisen spawnauspiste areenan reunalta.</summary>
	private Vector3 PickRandomArenaEdge()
	{
		int edge = GD.RandRange(0, 3);
		double t = GD.RandRange(-AreenanReunanPuolikas + 0.5, AreenanReunanPuolikas - 0.5);
		return edge switch
		{
			0 => new Vector3(-AreenanReunanPuolikas, _floorY, (float)t), // Länsi
			1 => new Vector3(AreenanReunanPuolikas,  _floorY, (float)t), // Itä
			2 => new Vector3((float)t, _floorY, -AreenanReunanPuolikas), // Pohjoinen
			_ => new Vector3((float)t, _floorY,  AreenanReunanPuolikas), // Etelä
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
		float hitFrom  = _playerController.GetMeleeStrikeWindowStart() + MiekkaIskuAktivoitumisaika;
		if (animTime < hitFrom || _hasBeenHitThisSwing) return;

		int dmg = _playerController.GetMeleeAttackDamage();

		// Tanssivaiheessa vain vahva isku (R1) osuu
		if (_phase == BossPhase.Dancing && dmg < 3) return;

		float proxMax = _playerController.GetMeleeHitProximityMax() + MiekkaIskuLisäLäheisyys;

		var rootHeights = MiekkaIskuKoetuskorkeudet;
		if (rootHeights == null || rootHeights.Length == 0)
			rootHeights = new[] { OsumaKeskusOffsetY };

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
			var visOff = MiekkaIskuKoetusNäkyväOffsetY;
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
		PlaySwordHitSfx();
		OnSwordHitFeedback(_isDead, dmg); // tappoisku / R1-vahinko (≥3) osuma-animaatiota varten
	}

	/// <summary>
	/// Kaikki visuaalinen palaute miekkaosumahetkellä — shake, flash, hitstop, recoil, squash, spotlight.
	/// </summary>
	private void OnSwordHitFeedback(bool isKillingBlow, int damageDealt)
	{
		// 1. Ruututärinä — voimistuu mitä vähemmän HP on jäljellä
		float hpFrac = BossMaksimiTerveys > 0 ? (float)_bossHealth / BossMaksimiTerveys : 0f;
		float shakeAmp = isKillingBlow ? 0.32f : Mathf.Lerp(0.22f, 0.10f, hpFrac);
		GetOrFindCamera()?.ShakeImpulse(shakeAmp, 0.30f);

		// 2. Slow motion vain tappoiskussa (70 ms reaaliaikaa)
		if (isKillingBlow && IsInsideTree())
		{
			Engine.TimeScale = 0.15f;
			var slowTimer = GetTree().CreateTimer(0.07f, processInPhysics: false, ignoreTimeScale: true);
			slowTimer.Timeout += () => { if (Engine.TimeScale < 1f) Engine.TimeScale = 1f; };
		}

		// 3. Hit flash: hetkellinen valkoinen/punainen siluetti kaikille mesheille
		var meshList = new System.Collections.Generic.List<MeshInstance3D>();
		foreach (Node n in FindChildren("*", "MeshInstance3D", true, false))
			if (n is MeshInstance3D mi) meshList.Add(mi);

		if (meshList.Count > 0)
		{
			var flashMat = new StandardMaterial3D
			{
				ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
				AlbedoColor = new Color(1f, 0.78f, 0.78f),
				EmissionEnabled = true,
				Emission = new Color(1f, 0.20f, 0.20f),
				EmissionEnergyMultiplier = 2.8f,
			};
			foreach (var m in meshList)
				if (GodotObject.IsInstanceValid(m))
					m.MaterialOverride = flashMat;

			float flashDuration = isKillingBlow ? 0.09f : 0.06f;
			_hitFlashTween?.Kill();
			_hitFlashTween = CreateTween();
			_hitFlashTween.TweenInterval(flashDuration);
			_hitFlashTween.TweenCallback(Callable.From(() =>
			{
				foreach (var m in meshList)
					if (GodotObject.IsInstanceValid(m))
						m.MaterialOverride = null;
			}));
		}

		// Tappoiskussa jätetään recoil/squash/hitstop pois — kuolema-animaatio hoitaa draaman
		if (isKillingBlow) return;

		// Vain R1-vahva isku (≥3) — sama kynnys kuin tanssivaiheessa
		bool playHitAnim = damageDealt >= 3
			&& _animationPlayer != null && GodotObject.IsInstanceValid(_animationPlayer)
			&& _animationPlayer.HasAnimation(OsumaKlipinNimi);

		if (playHitAnim)
		{
			_resumeClipAfterHit = _phase == BossPhase.Dancing ? TanssiKlipinNimi : JuoksuKlipinNimi;
			_hitReactPlaying = true;
			if (_chargingKickActive)
				_chargingKickActive = false;
			_animationPlayer.SpeedScale = 1f;
			_animationPlayer.Play(OsumaKlipinNimi);
		}
		else if (_animationPlayer != null && GodotObject.IsInstanceValid(_animationPlayer))
		{
			// 4. Boss AnimationPlayer hit-stop (80 ms) — fallback jos osuma.fbx puuttuu
			_animationPlayer.SpeedScale = 0f;
			var animFreeze = CreateTween();
			animFreeze.TweenInterval(0.08f);
			animFreeze.TweenCallback(Callable.From(() =>
			{
				if (GodotObject.IsInstanceValid(_animationPlayer) && !_isDead)
					_animationPlayer.SpeedScale = 1f;
			}));
		}

		var visual = GetNodeOrNull<Node3D>("BossVisual");
		if (visual != null && GodotObject.IsInstanceValid(visual) && !playHitAnim)
		{
			// 5. Body recoil: BossVisual loikkaa poispäin pelaajasta, sitten palaa jousimaisesti
			Vector3 awayDir = -GlobalTransform.Basis.Z;
			if (_player != null && GodotObject.IsInstanceValid(_player))
			{
				var d = GlobalPosition - _player.GlobalPosition;
				d.Y = 0f;
				if (d.LengthSquared() > 1e-5f) awayDir = d.Normalized();
			}
			Vector3 pushLocal = GlobalTransform.Basis.Inverse() * (awayDir * 0.32f);

			_recoilTween?.Kill();
			_recoilTween = CreateTween();
			_recoilTween.TweenProperty(visual, "position", pushLocal, 0.04f)
				.SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
			_recoilTween.TweenProperty(visual, "position", Vector3.Zero, 0.22f)
				.SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);

			// 6. Squash-stretch: litistää pystysuunnassa, laajenee XZ:ssä (kuin iskun paino)
			if (_bossVisualBaseScale.LengthSquared() > 0.001f)
			{
				Vector3 squashTarget = _bossVisualBaseScale * new Vector3(1.08f, 0.90f, 1.08f);
				_squashTween?.Kill();
				_squashTween = CreateTween();
				_squashTween.TweenProperty(visual, "scale", squashTarget, 0.04f)
					.SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
				_squashTween.TweenProperty(visual, "scale", _bossVisualBaseScale, 0.24f)
					.SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
			}
		}

		// 7. Spotlight flash: tanssivalo välkähtää punaisena
		if (_danceSpotLight != null && GodotObject.IsInstanceValid(_danceSpotLight) && _danceSpotLight.Visible)
		{
			_danceSpotLight.LightEnergy = BossTanssiValonEnergia * 3.0f;
			_danceSpotLight.LightColor = new Color(1f, 0.22f, 0.18f);
			var lightTween = CreateTween();
			lightTween.TweenInterval(0.10f);
			lightTween.TweenCallback(Callable.From(() =>
			{
				if (GodotObject.IsInstanceValid(_danceSpotLight))
				{
					_danceSpotLight.LightEnergy = BossTanssiValonEnergia;
					_danceSpotLight.LightColor = BossTanssiValonVäri;
				}
			}));
		}
	}

	private void PlaySwordHitSfx()
	{
		// Miekan iskuääni heti
		PlaySfxOnRoot(MiekkaIskuÄäniPolku, OsumaÄäniVoimakkuusDb);

		// Bossin vahinkoääni pienen viiveen jälkeen
		if (!string.IsNullOrWhiteSpace(BossVahinkoÄäniPolku))
		{
			GetTree().CreateTimer(BossVahinkoÄäniViive).Timeout +=
				() => PlaySfxOnRoot(BossVahinkoÄäniPolku, OsumaÄäniVoimakkuusDb);
		}
	}

	/// <summary>
	/// Toistaa äänen scene-juuressa, jotta se ei katoa bossin QueueFreen mukana.
	/// </summary>
	private void PlaySfxOnRoot(string path, float volumeDb)
	{
		if (string.IsNullOrWhiteSpace(path) || !IsInsideTree()) return;
		var stream = GD.Load<AudioStream>(path);
		if (stream == null) { GD.PrintErr($"BossLevel1: SFX ei löydy: {path}"); return; }
		var sfx = new AudioStreamPlayer { VolumeDb = volumeDb };
		GetTree().Root.AddChild(sfx);
		sfx.Stream = stream;
		sfx.Play();
		sfx.Finished += () => sfx.QueueFree();
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
		_hitReactPlaying = false;
		_chargingKickActive = false;
		Velocity = Vector3.Zero;
		StopBossMusic();
		UpdateDanceHighlightLight(false);

		var col = GetNodeOrNull<CollisionShape3D>("CollisionShape3D");
		if (col != null) col.Disabled = true;

		// Toista kuolema-animaatio jos löytyy
		if (_animationPlayer != null && _animationPlayer.HasAnimation(KuolemaKlipinNimi))
		{
			_animationPlayer.SpeedScale = 0.18f; // ~1/5.5 nopeudesta — pitkä, dramaattinen kuolema
			_animationPlayer.Play(KuolemaKlipinNimi);
			float deathLen = (float)_animationPlayer.CurrentAnimationLength;
			// Huom: jaetaan SpeedScalella jotta ajastin vastaa todellista toistoaikaa
			GetTree().CreateTimer(deathLen / _animationPlayer.SpeedScale).Timeout += () => QueueFree();
		}
		else
		{
			// Fallback: vanha kutistuminen jos animaatiota ei löydy
			Node3D visual = GetNodeOrNull<Node3D>("BossVisual") ?? (Node3D)this;
			var tween = CreateTween();
			tween.TweenProperty(visual, "scale", Vector3.Zero, KuolemanKutistumisenKesto)
				.SetTrans(Tween.TransitionType.Cubic)
				.SetEase(Tween.EaseType.In);
			tween.TweenCallback(Callable.From(() => QueueFree()));
		}
		PlayDeathSfx();
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
		foreach (var n in new[] { MixamoAnimaatioLähdeNimi, MixamoAnimaatioLähdeNimi.Replace("_", ".") })
		{
			if (ap.HasAnimation(n)) { anim = ap.GetAnimation(n); break; }
		}

		inst.QueueFree();
		if (anim == null)
		{
			GD.PrintErr($"BossLevel1: animaatiota '{MixamoAnimaatioLähdeNimi}' ei löydy: {path}");
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
		else anim.LoopMode = Animation.LoopModeEnum.None;
		var lib = _animationPlayer.GetAnimationLibrary("");
		if (lib.HasAnimation(targetName)) lib.RemoveAnimation(targetName);
		lib.AddAnimation(targetName, anim);
	}
}
