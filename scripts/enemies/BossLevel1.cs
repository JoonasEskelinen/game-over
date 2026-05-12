using System;
using Godot;

/// <summary>
/// Level 1 -bossi: tanssi → syöksy (kierto kentällä → linjaus + spin-hyökkäys) → takaisin tanssiin.
/// FBX-polut säädetään Inspectorissa (Mixamo: dance with skin, muut ilman skiniä).
///
/// Tärkeää: ilman skiniä tuodut FBX:t käyttävät usein eri NodePath-etuliitettä raidoissa kuin Dancing.fbx.
/// Kaikki klipit kohdistetaan tanssianimaation skeletin polkuihin (<see cref="AlignImportedClipsToDanceRig"/>),
/// muuten juoksu voi näyttää tyhjältä (mesh jää väärään asentoon / root motion vie visuaalin kauas).
///
/// BOSSIN KÄYTTÄYTYMINEN:
/// 1. Dancing-vaihe: bossi tanssii paikallaan satunnaisen ajan
/// 2. Charging-vaihe: reunalle → kierto kentällä → suora lähestyminen pelaajaan → spin
/// 3. Bossi voi ottaa vahinkoa vain tanssiessaan (vahva isku R1 tappaa)
/// 4. Spin osuu pelaajaan lähietäisyydellä — 1 HP ellei torjuta
/// 5. Jos pelaaja torjuu kilvellä, bossi horjahtaa taaksepäin.
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

	/// <summary>Spin.fbx polku — pyörimishyökkäys pelaajaa kohti (In Place -animaatio).</summary>
	[Export] public string SpinFbxPolku = "res://assets/models/level1_BossEnemy/Spin.fbx";

	/// <summary>Mixamon animaation sisäinen nimi FBX:ssä.</summary>
	[Export] public string MixamoAnimaatioLähdeNimi = "mixamo_com";

	/// <summary>Tanssianimaation nimi pelissä.</summary>
	[Export] public string TanssiKlipinNimi = "dance";

	/// <summary>Juoksuanimaation nimi pelissä.</summary>
	[Export] public string JuoksuKlipinNimi = "run";
	
	[Export] public string KuolemaKlipinNimi = "death";

	[Export] public string OsumaKlipinNimi = "hit_react";

	/// <summary>Spin-animaation nimi pelissä.</summary>
	[Export] public string SpinKlipinNimi = "spin";

	/// <summary>Vaakasuora etäisyys (m): spin-hyökkäys laukeaa kun bossi saavuttaa tämän etäisyyden pelaajaan.
	/// Suurempi kuin MMA kick — bossi alkaa pyöriä kauempaa ja liikkuu animaation aikana kohti pelaajaa.</summary>
	[Export] public float SpinLaukaisuEtäisyys = 5.0f;

	/// <summary>Nopeus (m/s) jolla bossi liikkuu pelaajaa kohti spin-animaation aikana.</summary>
	[Export] public float SpinHyökkäysNopeus = 6.0f;

	/// <summary>Vahinko rekisteröityy vain jos pelaaja on tämän sisällä spin-kontaktihetkellä.
	/// Suurempi kuin trigger-etäisyys koska bossi liikkuu animaation aikana.</summary>
	[Export] public float SpinOsumaEtäisyys = 4.5f;

	/// <summary>Spin-animaation kohta (0–1) jolloin osuma / torjunta tarkistetaan.</summary>
	[Export] public float SpinOsumaAnimFraaktio = 0.5f;

	/// <summary>Spin: kilven torjunnan kartion puolikulma (°). Kapeampi kuin aiempi leveä tila — torjunta vaatii tarkemman suunnan.</summary>
	[Export] public float SpinTorjuntaKilpiPuolikulma = 46f;

	/// <summary>Spin: uhkan XZ-piste lerppaa kohti pelaajaa (0–1). Pieni arvo = uhka pysyy bossin suunnassa (tiukempi kilpi).</summary>
	[Export] public float SpinTorjuntaUhkaLerpPelaajaan = 0.18f;

	/// <summary>Torjuntahorjahduksen karkea siirtymä (m) — ei teleporttia; pieni arvo välttää &quot;kimpoamisen&quot;.</summary>
	[Export] public float TorjuntaHorjahdusVoima = 1.35f;

	/// <summary>Torjuntahorjahduksen kesto sekunteina.</summary>
	[Export] public float TorjuntaHorjahdusKesto = 0.65f;

	/// <summary>Palautumisviive horjahduksen jälkeen ennen tanssiin paluuta (s).</summary>
	[Export] public float TorjuntaPalautumisViive = 0.35f;

	/// <summary>Spin FBX:n pituus fallback (s) jos klipin pituus on 0.</summary>
	[Export] public float SpinFallbackPituusSekuntia = 1.5f;

	/// <summary>Spin Play()-blend sekunteina juoksusta.</summary>
	[Export] public float SpinBlendSekunteina = 0.15f;

	/// <summary>Kierto: kuinka monta sekuntia bossi kiertää ennen kuin se suuntaa suoraan pelaajaan.</summary>
	[Export] public float SyöksyKiertoKestoSekuntia = 1.2f;

	/// <summary>Kierto: tangentti vs pelaajaan (0–1). Korkea = enemmän ympyrää kentällä.</summary>
	[Export] public float SyöksyKiertoOrbitPaino = 0.65f;

	/// <summary>Jos bossi on jumiutunut liikkumatta näin monta sekuntia, palataan tanssiin ja yritetään uudelleen.</summary>
	[Export] public float SyöksyJumiTeleporttiSekuntia = 1.5f;

	/// <summary>Kiertokeskus XZ-maailmassa (yleensä 0,0 — areenan keskipiste).</summary>
	[Export] public Vector2 SyöksyKiertoKeskipisteXZ = Vector2.Zero;

	/// <summary>Maksimi-HP.</summary>
	[Export] public int BossMaksimiTerveys = 30;

	private int _bossHealth;

	/// <summary>Tanssin minimikesto sekunteina ennen syöksyä.</summary>
	[Export] public float TanssiKestoMinSekuntia = 8.5f;

	/// <summary>Tanssin maksimikesto sekunteina.</summary>
	[Export] public float TanssiKestoMaxSekuntia = 10.5f;

	/// <summary>Aika tanssin päättymisen ja kameran palautumisen jälkeen ennen juoksua (run).</summary>
	[Export] public float ViiveTanssinJälkeenSekuntia = 1.0f;

	/// <summary>Syöksyn nopeus.</summary>
	[Export] public float SyöksyNopeus = 10.5f;

	/// <summary>Syöksyn maksimikesto.</summary>
	[Export] public float SyöksyMaksimiKestoSekuntia = 4.6f;

	/// <summary>Juoksuklipin SpeedScale syöksyvaiheessa.</summary>
	[Export] public float JuoksuClipSpeedScaleLatauksessa = 0.68f;

	/// <summary>Areenan puolisäde.</summary>
	[Export] public float AreenanReunanPuolikas = 13.5f;

	/// <summary>Syöksyssä: törmäysmaski.</summary>
	[Export] public uint SyöksyTörmäysMaski = 19u;

	[Export] public float SyöksyEsteSäde = 2.35f;

	[Export] public float SyöksyEsteSäteenAlkuY = 0.65f;

	/// <summary>Osumakeskipisteen korkeus Y-akselilla.</summary>
	[Export] public float OsumaKeskusOffsetY = 0.9f;

	/// <summary>Korkeudet juuren GlobalPositionista.</summary>
	[Export] public float[] MiekkaIskuKoetuskorkeudet = { 0.35f, 1.0f, 1.85f, 2.7f, 3.5f, 4.35f };

	/// <summary>Lisäpisteet BossVisual-solmun kohdalta.</summary>
	[Export] public float[] MiekkaIskuKoetusNäkyväOffsetY = { -0.4f, 0.35f, 1.1f, 2.0f, 2.9f };

	/// <summary>Lisää max-etäisyyttä terään.</summary>
	[Export] public float MiekkaIskuLisäLäheisyys = 0.42f;

	/// <summary>Lisäviive miekan osumaikkunaan.</summary>
	[Export] public float MiekkaIskuAktivoitumisaika = 0f;

	/// <summary>Kuolemisanimaation kesto sekunteina.</summary>
	[Export] public float KuolemanKutistumisenKesto = 1.55f;

	/// <summary>Visuaalisen meshin skaalauskerroin.</summary>
	[Export] public float BossVisuaalinenSkaala = 158f;

	/// <summary>Laske koko hahmoa näin paljon spawnin jälkeen (m).</summary>
	[Export] public float LisäSeisomaAlennusY = 0.06f;

	/// <summary>Uudelleenlattiasnap _Ready:ssä.</summary>
	[Export] public bool JalatMaahanValmiudessa = true;

	[Export] public float JalatMaahanSädeAlkuYlös = 14f;
	[Export] public float JalatMaahanSädePituusAlas = 24f;
	[Export] public float JalatMaahanOffsetLattiasta = 0.11f;

	/// <summary>True: lattia koskettaa visuaalin matalinta pistettä.</summary>
	[Export] public bool JalatMaahanKäytäVisuaalinAlareunaa = true;

	/// <summary>Pieni ilma visuaalin alapinnan ja lattian väliin (metriä).</summary>
	[Export] public float JalatMaahanVisuaaliLattiaVälys = 0.02f;

	/// <summary>Level 1 -bossin taustamusiikki.</summary>
	[Export] public string BossMusiikkiPolku = "res://assets/audio/music/musiclevel1.mp3";

	[Export] public float BossMusiikkiVoimakkuusDb = -4f;

	/// <summary>Kuolema-äänitehoste.</summary>
	[Export] public string KuolemaÄäniPolku = "res://assets/audio/sfx/death.mp3";

	[Export] public float KuolemaÄäniVoimakkuusDb = 0f;

	/// <summary>Ääni kun pelaajan miekka osuu bossiin.</summary>
	[Export] public string MiekkaIskuÄäniPolku = "res://assets/audio/sfx/miekka.mp3";

	/// <summary>Bossin vahinkoääni osuman jälkeen.</summary>
	[Export] public string BossVahinkoÄäniPolku = "res://assets/audio/sfx/enemybosshit.mp3";

	[Export] public float OsumaÄäniVoimakkuusDb = 0f;

	/// <summary>Viive sekunteina miekka-äänen ja vahinko-äänen välillä.</summary>
	[Export] public float BossVahinkoÄäniViive = 0.1f;

	/// <summary>Lisäspotti tanssivaiheessa.</summary>
	[Export] public bool BossTanssiKorostusKäytössä = true;

	[Export] public float BossTanssiValonEnergia = 4.2f;

	[Export] public float BossTanssiValonKantama = 14f;

	[Export] public Color BossTanssiValonVäri = new(1f, 0.94f, 0.86f, 1f);

	[Export] public float BossTanssiKohdevalonKulmaAstetta = 52f;

	[Export] public float BossTanssiKohdevalonKorkeus = 4.2f;

	[Export] public float BossTanssiKohdevaloKohtiKameraa = 1.1f;

	[Export] public float BossTanssiKohdevaloTähtäysOffsetY = 1.35f;

	// ─────────────────────────────────────────────
	// TEKSTUURIPOLUT
	// ─────────────────────────────────────────────

	[Export] public string TekstuuriPääPolku     = "res://assets/models/level1_BossEnemy/anthropomorphichedgehog3dmodel_basecolor.JPEG";
	[Export] public string TekstuuriNormaaliPolku   = "res://assets/models/level1_BossEnemy/anthropomorphichedgehog3dmodel_normal.JPEG";
	[Export] public string TekstuuriKarheusPolku = "res://assets/models/level1_BossEnemy/anthropomorphichedgehog3dmodel_roughness.JPEG";
	[Export] public string TekstuuriMetalliPolku  = "res://assets/models/level1_BossEnemy/anthropomorphichedgehog3dmodel_metallic.JPEG";

	// ─────────────────────────────────────────────
	// JULKISET OMINAISUUDET
	// ─────────────────────────────────────────────

	[Signal]
	public delegate void BossHealthChangedEventHandler(int currentHealth, int maxHealth);

	public bool IsDanceVulnerable => !_isDead && _phase == BossPhase.Dancing && !_waitingAfterDance;

	public bool IsBossCloseupDanceCameraActive =>
		!_isDead && _phase == BossPhase.Dancing && !_waitingAfterDance && _danceTimeLeft > 0f && !_hitReactPlaying;

	public bool IsBossDead => _isDead;

	public int GetBossCurrentHealth() => _bossHealth;

	public int GetBossMaxHealth() => BossMaksimiTerveys;

	// ─────────────────────────────────────────────
	// PRIVAATIT MUUTTUJAT
	// ─────────────────────────────────────────────

	private enum BossPhase { Dancing, Charging }

	private BossPhase _phase = BossPhase.Dancing;

	private enum ChargeRoutePhase { Circuit, Closing }

	private ChargeRoutePhase _chargeRoutePhase = ChargeRoutePhase.Circuit;

	public bool IsChargingPhase => !_isDead && _phase == BossPhase.Charging;

	private float _danceTimeLeft;
	private float _postDanceWaitLeft;
	private bool _waitingAfterDance;
	private float _chargeTimeLeft;
	private float _chargeStuckTimer;
	private Node3D _player;
	private PlayerController _playerController;
	private AnimationPlayer _animationPlayer;
	private Vector3 _standWorldPos;
	private bool _configuredFromDirector;
	private bool _hasBeenHitThisSwing;
	private bool _isDead;
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
	private string _resumeClipAfterHit = "";
	private bool _chargingKickActive;
	private bool _kickTriggeredThisCharge;
	private bool _kickDamageAppliedThisKick;
	private float _chargeOrbitSign = 1f;
	private float _circuitElapsed;
	private float _closingElapsed;
	private bool _mmaKickFallbackPending;

	// Horjahdus (block stagger)
	private bool _staggerActive;
	private float _staggerTimeLeft;
	private Vector3 _staggerDirection;
	private Tween _staggerTween;

	// Todellinen paikka edellisellä framella — jumiutuminen mitataan positiodeltasta
	private Vector3 _prevPosition;
	private float _realStuckTimer;

	// #region agent log
	private static readonly string _agentLogPath = "debug-1be988.log";
	private static void AgentDebugNdjson(string hypothesisId, string message, string dataJson)
	{
		try
		{
			long ts = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
			System.IO.File.AppendAllText(_agentLogPath,
				$"{{\"sessionId\":\"1be988\",\"hypothesisId\":\"{hypothesisId}\",\"timestamp\":{ts},\"message\":\"{message}\",\"data\":{dataJson}}}\n");
		}
		catch { /* debug */ }
	}
	// #endregion

	// ─────────────────────────────────────────────
	// ALUSTUS
	// ─────────────────────────────────────────────

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

		CollisionLayer = 2;
		CollisionMask = 3;
		_savedCollisionMask = CollisionMask;
		AddToGroup("level1_boss");

		SetupBossMusic();

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

		_player = GetTree().GetFirstNodeInGroup("player") as Node3D;
		_playerController = _player as PlayerController;

		if (!TryMountDanceVisualAndPlayer())
		{
			GD.PrintErr("BossLevel1: Dance FBX puuttuu tai virheellinen — polku: " + TanssiFbxPolku);
			return;
		}

		LoadAnim(NopeaJuoksuFbxPolku, MixamoAnimaatioLähdeNimi, JuoksuKlipinNimi, loop: true);
		LoadAnim(KuolemaFbxPolku, MixamoAnimaatioLähdeNimi, KuolemaKlipinNimi, loop: false);
		LoadAnim(OsumaReaktioFbxPolku, MixamoAnimaatioLähdeNimi, OsumaKlipinNimi, loop: false);
		LoadAnim(SpinFbxPolku, MixamoAnimaatioLähdeNimi, SpinKlipinNimi, loop: false);
		AlignImportedClipsToDanceRig();
		// Spin voi olla eri Mixamo-latauksesta: bones/N -indeksit eivät välttämättä vastaa Dancing-skeletonia vaikka polut kohdistuvat.
		RemapImportedClipBoneIndicesFromSourceFbx(SpinFbxPolku, SpinKlipinNimi);

		if (!_animationPlayer.HasAnimation(SpinKlipinNimi))
			GD.PrintErr($"BossLevel1: Spin-klippi '{SpinKlipinNimi}' puuttuu — tarkista FBX ({SpinFbxPolku}).");
		else
		{
			float kl = (float)_animationPlayer.GetAnimation(SpinKlipinNimi).Length;
			if (kl <= 0.02f)
				GD.PrintErr($"BossLevel1: Spin-klipin pituus ~0 — käytetään fallback {SpinFallbackPituusSekuntia:F2}s.");
		}

		_animationPlayer.AnimationFinished += OnBossAnimationFinished;

		_animationPlayer.Play(TanssiKlipinNimi);
		_danceTimeLeft = (float)GD.RandRange(TanssiKestoMinSekuntia, TanssiKestoMaxSekuntia);
		_phase = BossPhase.Dancing;

		ApplyBossTextures();
		ApplyStandVerticalAdjustments();
		SetupDanceHighlightLight();

		EmitSignal(SignalName.BossHealthChanged, _bossHealth, BossMaksimiTerveys);
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
	}

	private void ShiftStandAndPosition(float deltaY)
	{
		var d = Vector3.Up * deltaY;
		GlobalPosition += d;
		_standWorldPos += d;
		_floorY = GlobalPosition.Y;
	}

	private void SnapChargeBodyToFloor()
	{
		if (!_chargingKickActive && Velocity.Y > 0.4f)
			return;
		float dy = GlobalPosition.Y - _floorY;
		if (dy <= 0.09f)
			return;
		var p = GlobalPosition;
		p.Y = _floorY;
		GlobalPosition = p;
		Velocity = new Vector3(Velocity.X, Mathf.Min(0f, Velocity.Y), Velocity.Z);
	}

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
		float pad = Mathf.Max(0f, JalatMaahanVisuaaliLattiaVälys);

		if (JalatMaahanKäytäVisuaalinAlareunaa && TryGetBossVisualLowestWorldY(out float lowestWorldY))
		{
			float delta = (floorY + pad) - lowestWorldY;
			if (Mathf.Abs(delta) > 1e-4f)
				ShiftStandAndPosition(delta);
			return;
		}

		float targetRootY = floorY + JalatMaahanOffsetLattiasta;
		ShiftStandAndPosition(targetRootY - GlobalPosition.Y);
	}

	private bool TryGetBossVisualLowestWorldY(out float lowestY)
	{
		lowestY = float.MaxValue;
		var visual = GetNodeOrNull<Node3D>("BossVisual");
		if (visual == null || !visual.IsInsideTree())
			return false;

		foreach (Node n in visual.FindChildren("*", "MeshInstance3D", true, false))
		{
			if (n is not MeshInstance3D mi || !mi.Visible || mi.Mesh == null)
				continue;
			Aabb local = mi.GetAabb();
			Transform3D xf = mi.GlobalTransform;
			for (int i = 0; i < 8; i++)
			{
				Vector3 c = local.Position + new Vector3(
					(i & 1) != 0 ? local.Size.X : 0f,
					(i & 2) != 0 ? local.Size.Y : 0f,
					(i & 4) != 0 ? local.Size.Z : 0f);
				Vector3 w = xf * c;
				if (w.Y < lowestY)
					lowestY = w.Y;
			}
		}

		return lowestY < float.MaxValue - 1f;
	}

	// ─────────────────────────────────────────────
	// TEKSTUURIEN LINKITYS
	// ─────────────────────────────────────────────

	private void ApplyBossTextures()
	{
		var albedo    = GD.Load<Texture2D>(TekstuuriPääPolku);
		var normal    = GD.Load<Texture2D>(TekstuuriNormaaliPolku);
		var roughness = GD.Load<Texture2D>(TekstuuriKarheusPolku);
		var metallic  = GD.Load<Texture2D>(TekstuuriMetalliPolku);

		if (albedo == null)
		{
			GD.PrintErr("BossLevel1: Albedo-tekstuuri ei löydy: " + TekstuuriPääPolku);
			return;
		}

		var mat = new StandardMaterial3D();
		mat.AlbedoTexture    = albedo;
		mat.NormalEnabled    = normal != null;
		mat.NormalTexture    = normal;
		mat.RoughnessTexture = roughness;
		mat.MetallicTexture  = metallic;

		foreach (var node in FindChildren("*", "MeshInstance3D", true, false))
		{
			if (node is MeshInstance3D mi)
			{
				int surfaceCount = mi.GetSurfaceOverrideMaterialCount();
				if (surfaceCount == 0)
				{
					mi.SetSurfaceOverrideMaterial(0, mat);
				}
				else
				{
					for (int i = 0; i < surfaceCount; i++)
						mi.SetSurfaceOverrideMaterial(i, mat);
				}
			}
		}
	}

	// ─────────────────────────────────────────────
	// VISUAALIN LATAUS
	// ─────────────────────────────────────────────

	private bool TryMountDanceVisualAndPlayer()
	{
		Node3D visualRoot = GetNodeOrNull<Node3D>("BossVisual");

		if (visualRoot == null)
		{
			foreach (Node child in GetChildren())
			{
				if (child is CollisionShape3D) continue;
				if (child is not Node3D n3) continue;
				if (n3.FindChild("AnimationPlayer", true, false) is AnimationPlayer)
				{
					visualRoot = n3;
					visualRoot.Name = "BossVisual";
					break;
				}
			}
		}

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

	private void ApplyBossVisualScale(Node3D visualRoot)
	{
		if (visualRoot == null || BossVisuaalinenSkaala <= 0f) return;
		if (!Mathf.IsEqualApprox(BossVisuaalinenSkaala, 1f))
			visualRoot.Scale = Vector3.One * BossVisuaalinenSkaala;
		_bossVisualBaseScale = visualRoot.Scale;
	}

	private CameraFollow GetOrFindCamera()
	{
		if (_camera != null && GodotObject.IsInstanceValid(_camera)) return _camera;
		_camera = GetViewport()?.GetCamera3D() as CameraFollow;
		return _camera;
	}

	private void EnsureAnimationLibrary()
	{
		if (_animationPlayer == null) return;
		try { if (_animationPlayer.GetAnimationLibrary("") != null) return; }
		catch { }
		var lib = new AnimationLibrary();
		_animationPlayer.AddAnimationLibrary("", lib);
	}

	// ─────────────────────────────────────────────
	// FYSIIKKA
	// ─────────────────────────────────────────────

	public override void _PhysicsProcess(double delta)
	{
		if (_isDead || !IsInsideTree() || _animationPlayer == null) return;

		float dt = (float)delta;

		if (_player == null || !GodotObject.IsInstanceValid(_player))
		{
			_player = GetTree().GetFirstNodeInGroup("player") as Node3D;
			_playerController = _player as PlayerController;
		}

		switch (_phase)
		{
			case BossPhase.Dancing:  ProcessDancing(dt);  break;
			case BossPhase.Charging:
				UpdateDanceHighlightLight(false);
				ProcessCharging(dt);
				break;
		}

		TrySwordHits();
		MoveAndSlide();

		if (_phase == BossPhase.Charging && !_isDead)
		{
			SnapChargeBodyToFloor();
			if (!_chargingKickActive && !_hitReactPlaying && !_staggerActive)
				TryChargeSeparationUnstick(dt);
		}

		if (_phase == BossPhase.Dancing)
			GlobalPosition = _standWorldPos;

		// prevPosition päivitetään MoveAndSlide()-kutsun jälkeen (sulkemisvaiheen jumitunnistus)
		if (_phase == BossPhase.Charging)
			_prevPosition = GlobalPosition;
	}

	// ─────────────────────────────────────────────
	// TANSSIVAIHEEN LOGIIKKA
	// ─────────────────────────────────────────────

	private void ProcessDancing(float dt)
	{
		GlobalPosition = _standWorldPos;
		Velocity = Vector3.Zero;

		FaceTowardActiveCamera();
		UpdateDanceHighlightLight(true);

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

	// ─────────────────────────────────────────────
	// SYÖKSYN ALOITUS
	// ─────────────────────────────────────────────

	private void BeginCharge()
	{
		UpdateDanceHighlightLight(false);
		_phase = BossPhase.Charging;
		_chargingKickActive = false;
		_kickTriggeredThisCharge = false;
		_staggerActive = false;
		_chargeRoutePhase = ChargeRoutePhase.Circuit;
		_circuitElapsed = 0f;
		_closingElapsed = 0f;
		_mmaKickFallbackPending = false;
		_chargeOrbitSign = GD.Randf() < 0.5f ? 1f : -1f;
		CollisionMask = SyöksyTörmäysMaski;

		Vector3 edge = PickArenaEdgeTowardPlayerOrRandom();
		edge.Y = _floorY;
		GlobalPosition = edge;

		FaceTowardPlayerFlat();

		if (_animationPlayer.HasAnimation(JuoksuKlipinNimi))
		{
			_animationPlayer.Play(JuoksuKlipinNimi);
			_animationPlayer.SpeedScale = Mathf.Clamp(JuoksuClipSpeedScaleLatauksessa, 0.15f, 1.5f);
		}

		// Alkunopeus: aivan reunalta, suoraan pelaajaan (kiertovaihe jatkaa ohjauksen)
		Vector3 initDir = (_player != null && GodotObject.IsInstanceValid(_player) && _player.IsInsideTree())
			? (_player.GlobalPosition - GlobalPosition with { Y = _player.GlobalPosition.Y }).Normalized()
			: -GlobalTransform.Basis.Z;
		initDir.Y = 0f;
		if (initDir.LengthSquared() < 0.01f) initDir = Vector3.Forward;
		else initDir = initDir.Normalized();
		float vy = IsOnFloor() ? 0f : Velocity.Y;
		Velocity = new Vector3(initDir.X * SyöksyNopeus, vy, initDir.Z * SyöksyNopeus);
		_chargeTimeLeft = SyöksyMaksimiKestoSekuntia;
		_chargeStuckTimer = 0f;
		_realStuckTimer = 0f;
		_prevPosition = GlobalPosition;
	}

	/// <summary>Kiertovaihe: tangentti areenan ympärillä, sekoitettuna vähän pelaajaan.</summary>
	private Vector3 ComputeCircuitSteerPlanar(Vector3 toPlayerNorm)
	{
		Vector3 posXz = new(GlobalPosition.X, 0f, GlobalPosition.Z);
		Vector3 centerXz = new(SyöksyKiertoKeskipisteXZ.X, 0f, SyöksyKiertoKeskipisteXZ.Y);
		Vector3 radial = posXz - centerXz;
		radial.Y = 0f;

		Vector3 tangent = radial.LengthSquared() > 0.06f
			? radial.Cross(Vector3.Up).Normalized() * _chargeOrbitSign
			: (toPlayerNorm.LengthSquared() > 0.01f
				? toPlayerNorm.Cross(Vector3.Up).Normalized() * _chargeOrbitSign
				: Vector3.Forward);

		// Pieni wobble-efekti tekee liikkeestä orgaanisemman
		float wobble = Mathf.Sin(_circuitElapsed * 2.5f) * 0.25f;
		tangent = tangent.Rotated(Vector3.Up, wobble).Normalized();

		// Sekoita tangentista kohti pelaajaa (orbit-paino säädettävissä)
		float orb = Mathf.Clamp(SyöksyKiertoOrbitPaino, 0f, 1f);
		Vector3 toP = toPlayerNorm.LengthSquared() > 0.01f ? toPlayerNorm : tangent;
		Vector3 mix = tangent * orb + toP * (1f - orb);
		return mix.LengthSquared() < 1e-6f ? tangent : mix.Normalized();
	}

	private Vector3 AdjustChargeDirForObstacles(Vector3 wish)
	{
		wish.Y = 0f;
		if (wish.LengthSquared() < 1e-6f)
			return wish;
		wish = wish.Normalized();

		var space = GetWorld3D()?.DirectSpaceState;
		if (space == null)
			return wish;

		Vector3 from = GlobalPosition + Vector3.Up * SyöksyEsteSäteenAlkuY;
		float len = Mathf.Max(0.45f, SyöksyEsteSäde);
		if (!RayChargeBlocked(space, from, wish, len))
			return wish;

		float[] probeAngles = { -38f, 38f, -72f, 72f, -20f, 20f, -105f, 105f };
		foreach (float deg in probeAngles)
		{
			Vector3 dir = wish.Rotated(Vector3.Up, Mathf.DegToRad(deg));
			dir.Y = 0f;
			if (dir.LengthSquared() < 1e-6f)
				continue;
			dir = dir.Normalized();
			if (!RayChargeBlocked(space, from, dir, len))
				return dir;
		}

		Vector3 perp = Vector3.Up.Cross(wish).Normalized();
		if (!RayChargeBlocked(space, from, perp, len * 0.55f))
			return perp;
		perp = -perp;
		if (!RayChargeBlocked(space, from, perp, len * 0.55f))
			return perp;
		return wish;
	}

	private bool RayChargeBlocked(PhysicsDirectSpaceState3D space, Vector3 from, Vector3 dirXZ, float len)
	{
		dirXZ.Y = 0f;
		if (dirXZ.LengthSquared() < 1e-6f)
			return true;
		dirXZ = dirXZ.Normalized();
		var q = PhysicsRayQueryParameters3D.Create(from, from + dirXZ * len);
		q.CollisionMask = SyöksyTörmäysMaski;
		q.CollideWithAreas = false;
		var ex = new Godot.Collections.Array<Rid> { GetRid() };
		if (_player is CollisionObject3D pc)
			ex.Add(pc.GetRid());
		q.Exclude = ex;
		return space.IntersectRay(q).Count > 0;
	}

	// ─────────────────────────────────────────────
	// SYÖKSYVAIHEEN LOGIIKKA (UUDELLEENKIRJOITETTU)
	// ─────────────────────────────────────────────

	private void ProcessCharging(float dt)
	{
		// Torjuntahorjahdus
		if (_staggerActive)
		{
			ProcessStagger(dt);
			return;
		}

		// Spin-hyökkäys käynnissä — bossi liikkuu kohti pelaajaa koko animaation ajan (In Place -animaatio, liike koodissa)
		if (_chargingKickActive)
		{
			ApplyChargeVerticalPhysics(dt);
			if (!_kickDamageAppliedThisKick && _player != null && GodotObject.IsInstanceValid(_player))
			{
				Vector3 toP = _player.GlobalPosition - GlobalPosition;
				toP.Y = 0f;
				float dist = toP.Length();
				if (dist > 0.5f)
				{
					Vector3 slideDir = toP.Normalized();
					Velocity = new Vector3(slideDir.X * SpinHyökkäysNopeus, Velocity.Y, slideDir.Z * SpinHyökkäysNopeus);
					FaceTowardPlayerFlat();
				}
				else
				{
					Velocity = new Vector3(0f, Velocity.Y, 0f);
				}
			}
			else
			{
				Velocity = new Vector3(0f, Velocity.Y, 0f);
			}
			return;
		}

		// Osuma-animaatio käynnissä — seistään paikallaan
		if (_hitReactPlaying)
		{
			ApplyChargeVerticalPhysics(dt);
			Velocity = new Vector3(0f, Velocity.Y, 0f);
			return;
		}

		_chargeTimeLeft -= dt;

		// ─── Laske etäisyys pelaajaan ───
		float planarDistPlayer = float.MaxValue;
		Vector3 toPlayerDir = Vector3.Zero;
		if (_player != null && GodotObject.IsInstanceValid(_player) && _player.IsInsideTree())
		{
			var w = _player.GlobalPosition - GlobalPosition;
			w.Y = 0f;
			planarDistPlayer = w.Length();
			if (planarDistPlayer > 0.01f)
				toPlayerDir = w / planarDistPlayer;
		}

		// ─── Kiertovaihe → sulkemisvaihe ───
		if (_chargeRoutePhase == ChargeRoutePhase.Circuit)
		{
			_circuitElapsed += dt;
			if (_circuitElapsed >= SyöksyKiertoKestoSekuntia)
			{
				_chargeRoutePhase = ChargeRoutePhase.Closing;
				_closingElapsed = 0f;
				_realStuckTimer = 0f;
				_prevPosition = GlobalPosition;
			}
		}
		else
		{
			_closingElapsed += dt;
		}

		// ─── Liikeohjaus ───
		Vector3 steer;
		if (_chargeRoutePhase == ChargeRoutePhase.Circuit)
		{
			// Kiertovaihe: tangentti + pieni vetovoima pelaajaan
			Vector3 circuitDir = ComputeCircuitSteerPlanar(toPlayerDir);
			steer = AdjustChargeDirForObstacles(circuitDir);
		}
		else
		{
			// Sulkemisvaihe: suoraan pelaajaan — EI esteiden kiertoa, MoveAndSlide hoitaa liukumisen
			steer = toPlayerDir.LengthSquared() > 0.01f ? toPlayerDir : -GlobalTransform.Basis.Z with { Y = 0f };
			if (steer.LengthSquared() > 0.01f) steer = steer.Normalized();
		}

		Velocity = new Vector3(steer.X * SyöksyNopeus, Velocity.Y, steer.Z * SyöksyNopeus);
		FacePlanarDirection(steer);
		ApplyChargeVerticalPhysics(dt);

		if (_animationPlayer != null && _animationPlayer.CurrentAnimation == JuoksuKlipinNimi)
			_animationPlayer.SpeedScale = Mathf.Clamp(JuoksuClipSpeedScaleLatauksessa, 0.15f, 1.5f);

		float effSpinRange = Mathf.Max(0.55f, SpinLaukaisuEtäisyys);

		// ─── Spin-hyökkäys: laukeaa kun bossi on riittävän lähellä pelaajaa; animaation aikana bossi jatkaa liikettä kohti pelaajaa ───
		if (!_kickTriggeredThisCharge
			&& _chargeRoutePhase == ChargeRoutePhase.Closing
			&& planarDistPlayer <= effSpinRange
			&& _player != null && GodotObject.IsInstanceValid(_player)
			&& _animationPlayer != null)
		{
			FaceTowardPlayerFlat();
			AgentDebugNdjson("H1", "spin_trigger",
				$"{{\"planarDist\":{planarDistPlayer:F2},\"effSpinRange\":{effSpinRange:F2}}}");
			StartSpinAttack(dt);
			return;
		}

		// ─── Jumi: jos bossi jumittuu pitkäksi aikaa, palataan tanssiin (ei teleporttia) ───
		if (_chargeRoutePhase == ChargeRoutePhase.Closing)
		{
			float realMovedXZ = (new Vector2(GlobalPosition.X, GlobalPosition.Z) - new Vector2(_prevPosition.X, _prevPosition.Z)).Length();
			if (realMovedXZ < 0.055f)
				_realStuckTimer += dt;
			else
				_realStuckTimer = 0f;

			if (_realStuckTimer >= SyöksyJumiTeleporttiSekuntia && !_kickTriggeredThisCharge)
			{
				AgentDebugNdjson("H5", "stuck_return_dance",
					$"{{\"stuckTime\":{_realStuckTimer:F2},\"dist\":{planarDistPlayer:F2}}}");
				ReturnToDance();
				return;
			}
		}

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

	// ─────────────────────────────────────────────
	// MMA-POTKU JA TORJUNTAHORJAHDUS
	// ─────────────────────────────────────────────

	private void StartSpinAttack(float dt)
	{
		_kickTriggeredThisCharge = true;
		_kickDamageAppliedThisKick = false;

		if (_animationPlayer == null || !_animationPlayer.HasAnimation(SpinKlipinNimi))
		{
			GD.PrintErr($"BossLevel1: Spin-klippi '{SpinKlipinNimi}' puuttuu — palataan tanssiin.");
			ReturnToDance();
			return;
		}

		_chargingKickActive = true;
		FaceTowardPlayerFlat();

		float blend = Mathf.Clamp(SpinBlendSekunteina, 0f, 0.55f);
		_animationPlayer.Play(SpinKlipinNimi, blend);
		_animationPlayer.SpeedScale = 1f;

		_mmaKickFallbackPending = true;
		float rawLen = (float)_animationPlayer.GetAnimation(SpinKlipinNimi).Length;
		float spinLen = rawLen > 0.02f ? rawLen : SpinFallbackPituusSekuntia;
		float wait = spinLen / Mathf.Max(0.12f, _animationPlayer.SpeedScale) + 0.15f;
		GetTree().CreateTimer(wait).Timeout += OnSpinAnimFallbackTimeout;

		float contactDelay = spinLen * Mathf.Clamp(SpinOsumaAnimFraaktio, 0.1f, 0.9f);
		GetTree().CreateTimer(contactDelay).Timeout += OnSpinContactMoment;

		// Ei pysäytetä vauhtia — ProcessCharging hoitaa liikkeen SpinHyökkäysNopeus-nopeudella
		ApplyChargeVerticalPhysics(dt);
	}

	/// <summary>
	/// Ajastin kutsuu tätä spin-animaation osumahetkellä (SpinOsumaAnimFraaktio).
	/// Vahinko rekisteröidään vain jos pelaaja on lähellä — koska bossi on liikkunut animaation aikana,
	/// onnistumistodennäköisyys on huomattavasti parempi kuin MMA kickissä.
	/// </summary>
	private void OnSpinContactMoment()
	{
		if (_isDead || !_chargingKickActive || _kickDamageAppliedThisKick) return;
		if (_player == null || !GodotObject.IsInstanceValid(_player)) return;

		Vector3 diff = _player.GlobalPosition - GlobalPosition;
		diff.Y = 0f;
		float dist = diff.Length();

		if (dist > SpinOsumaEtäisyys)
		{
			AgentDebugNdjson("H4", "spin_miss_too_far",
				$"{{\"dist\":{dist:F2},\"maxHitRange\":{SpinOsumaEtäisyys:F2}}}");
			return;
		}

		_kickDamageAppliedThisKick = true;
		TryKickContactDamage(spinAttack: true);
	}

	private void OnSpinAnimFallbackTimeout()
	{
		if (!_mmaKickFallbackPending || !_chargingKickActive || _isDead)
			return;
		_mmaKickFallbackPending = false;
		_chargingKickActive = false;
		if (_phase == BossPhase.Charging && !_staggerActive)
			ReturnToDance();
	}

	/// <summary>
	/// Spin-/isku-vahinko tai torjuntahorjahdus.
	/// Kilvellä ja oikeaan suuntaan: <see cref="BeginBlockStagger"/> + pelaajan kevyt palaute.
	/// </summary>
	private void TryKickContactDamage(bool spinAttack = false)
	{
		if (_playerController == null || _isDead) return;

		Vector3 threatFromBoss = GlobalPosition + Vector3.Up * OsumaKeskusOffsetY;
		if (spinAttack && _player != null && GodotObject.IsInstanceValid(_player))
		{
			float t = Mathf.Clamp(SpinTorjuntaUhkaLerpPelaajaan, 0f, 1f);
			Vector3 xz = GlobalPosition.Lerp(_player.GlobalPosition, t);
			threatFromBoss = xz + Vector3.Up * OsumaKeskusOffsetY;
		}

		float blockHalfAngle = spinAttack
			? SpinTorjuntaKilpiPuolikulma
			: _playerController.KilpiTorjuntaPuolikulma;

		if (_playerController.IsBlockingEffectiveAgainst(threatFromBoss, blockHalfAngle))
		{
			// #region agent log
			float _dLog = 0f;
			if (_player != null && GodotObject.IsInstanceValid(_player))
			{
				var _wLog = _player.GlobalPosition - GlobalPosition;
				_wLog.Y = 0f;
				_dLog = _wLog.Length();
			}
			AgentDebugNdjson("H3", "block_stagger",
				$"{{\"distToPlayer\":{_dLog:F2},\"horjahdusM\":{TorjuntaHorjahdusVoima:F2},\"spin\":{spinAttack.ToString().ToLowerInvariant()}}}");
			// #endregion
			_playerController.NotifyBossLevel1StrikeBlocked();
			BeginBlockStagger();
			return;
		}

		var hc = _playerController.GetNodeOrNull<HealthComponent>("HealthComponent");
		if (hc != null)
		{
			hc.TakeDamage(1);
			_playerController.NotifyBossLevel1MmaKickHit();
		}
	}

	/// <summary>Bossi horjahtaa taaksepäin kun pelaaja torjuu kilvellä.</summary>
	private void BeginBlockStagger()
	{
		_chargingKickActive = false;
		_mmaKickFallbackPending = false;
		_staggerActive = true;
		_staggerTimeLeft = TorjuntaHorjahdusKesto + TorjuntaPalautumisViive;

		// Suunta: pelaajasta poispäin
		if (_player != null && GodotObject.IsInstanceValid(_player))
		{
			Vector3 away = GlobalPosition - _player.GlobalPosition;
			away.Y = 0f;
			_staggerDirection = away.LengthSquared() > 0.01f ? away.Normalized() : -GlobalTransform.Basis.Z;
		}
		else
		{
			_staggerDirection = -GlobalTransform.Basis.Z;
		}

		// Visuaalinen horjahdus: BossVisual heilahtaa taaksepäin
		var visual = GetNodeOrNull<Node3D>("BossVisual");
		if (visual != null && GodotObject.IsInstanceValid(visual))
		{
			Vector3 pushLocal = GlobalTransform.Basis.Inverse() * (_staggerDirection * 0.6f);
			_staggerTween?.Kill();
			_staggerTween = CreateTween();
			_staggerTween.TweenProperty(visual, "position", pushLocal, TorjuntaHorjahdusKesto * 0.3f)
				.SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
			_staggerTween.TweenProperty(visual, "position", Vector3.Zero, TorjuntaHorjahdusKesto * 0.7f)
				.SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
		}

		// Kamera-shake pelaajalle (onnistunut torjunta tuntuu hyvältä)
		GetOrFindCamera()?.ShakeImpulse(0.12f, 0.18f);

		// Pysäytä juoksuanimaatio ja toista hit_react jos löytyy
		if (_animationPlayer != null && _animationPlayer.HasAnimation(OsumaKlipinNimi))
		{
			_animationPlayer.SpeedScale = 1f;
			_animationPlayer.Play(OsumaKlipinNimi);
		}
	}

	/// <summary>Horjahduksen fysiikka: liu'u taaksepäin hidastuva.</summary>
	private void ProcessStagger(float dt)
	{
		_staggerTimeLeft -= dt;

		// Horjahdusliike (ensimmäinen osa ajasta)
		float staggerMoveDuration = TorjuntaHorjahdusKesto;
		float elapsed = (TorjuntaHorjahdusKesto + TorjuntaPalautumisViive) - _staggerTimeLeft;

		if (elapsed < staggerMoveDuration)
		{
			float progress = elapsed / staggerMoveDuration;
			// Hidastava liike: nopeampi alussa, pysähtyy lopussa
			float speedFactor = Mathf.Max(0f, 1f - progress * progress);
			float staggerSpeed = (TorjuntaHorjahdusVoima / staggerMoveDuration) * speedFactor;
			Velocity = new Vector3(
				_staggerDirection.X * staggerSpeed,
				IsOnFloor() ? 0f : Velocity.Y - 35f * dt,
				_staggerDirection.Z * staggerSpeed);
		}
		else
		{
			Velocity = new Vector3(0f, IsOnFloor() ? 0f : Velocity.Y - 35f * dt, 0f);
		}

		if (_staggerTimeLeft <= 0f)
		{
			_staggerActive = false;
			// Älä teleporttaa alkuperäiseen tanssipisteeseen torjunnan jälkeen (näytti bugimaiselta hypyltä).
			ReturnToDance(anchorDanceAtCurrentPosition: true);
		}
	}

	/// <summary>Palaa tanssivaiheeseen.</summary>
	/// <param name="anchorDanceAtCurrentPosition">True torjunnan jälkeen: tanssi jatkuu siitä missä bossi on (ei snap-teleporttia).</param>
	private void ReturnToDance(bool anchorDanceAtCurrentPosition = false)
	{
		_chargingKickActive = false;
		_staggerActive = false;
		if (anchorDanceAtCurrentPosition)
			_standWorldPos = GlobalPosition;
		else
			GlobalPosition = _standWorldPos;
		// #region agent log
		{
			float d = 0f;
			if (_player != null && GodotObject.IsInstanceValid(_player))
			{
				var w = _player.GlobalPosition - GlobalPosition;
				w.Y = 0f;
				d = w.Length();
			}
			AgentDebugNdjson("H2", "ReturnToDance",
				$"{{\"anchorAtCurrent\":{anchorDanceAtCurrentPosition.ToString().ToLowerInvariant()},\"distToPlayer\":{d:F2}}}");
		}
		// #endregion
		Velocity = Vector3.Zero;
		CollisionMask = _savedCollisionMask;
		_phase = BossPhase.Dancing;
		_waitingAfterDance = false;
		_postDanceWaitLeft = 0f;
		_danceTimeLeft = (float)GD.RandRange(TanssiKestoMinSekuntia, TanssiKestoMaxSekuntia);
		if (_animationPlayer.HasAnimation(TanssiKlipinNimi))
			_animationPlayer.Play(TanssiKlipinNimi);
		if (_animationPlayer != null)
			_animationPlayer.SpeedScale = 1f;
	}

	private void OnBossAnimationFinished(StringName animName)
	{
		if (AnimationNameMatches(animName, OsumaKlipinNimi))
		{
			_hitReactPlaying = false;
			if (_isDead) return;
			if (!string.IsNullOrEmpty(_resumeClipAfterHit) && _animationPlayer != null
				&& _animationPlayer.HasAnimation(_resumeClipAfterHit))
			{
				_animationPlayer.Play(_resumeClipAfterHit);
				_animationPlayer.SpeedScale = _resumeClipAfterHit == JuoksuKlipinNimi
					? Mathf.Clamp(JuoksuClipSpeedScaleLatauksessa, 0.15f, 1.5f)
					: 1f;
			}
			_resumeClipAfterHit = "";
			return;
		}

		if (AnimationNameMatches(animName, SpinKlipinNimi) && _chargingKickActive)
		{
			_mmaKickFallbackPending = false;
			_chargingKickActive = false;
			if (!_isDead && !_staggerActive)
				ReturnToDance();
		}
	}

	private static bool AnimationNameMatches(StringName played, string expected)
	{
		if (string.IsNullOrEmpty(expected))
			return false;
		string p = played.ToString();
		return p == expected || p.EndsWith("/" + expected, StringComparison.Ordinal);
	}

	private void FaceTowardPlayerFlat()
	{
		if (_player == null || !GodotObject.IsInstanceValid(_player) || !_player.IsInsideTree())
			return;
		var look = _player.GlobalPosition with { Y = GlobalPosition.Y };
		if (GlobalPosition.DistanceTo(look) < 0.05f)
			return;
		LookAt(look, Vector3.Up);
		RotateY(Mathf.Pi);
	}

	private void FacePlanarDirection(Vector3 dirWorldFlat)
	{
		dirWorldFlat.Y = 0f;
		if (dirWorldFlat.LengthSquared() < 1e-6f)
			return;
		dirWorldFlat = dirWorldFlat.Normalized();
		var look = GlobalPosition + dirWorldFlat with { Y = 0f };
		look.Y = GlobalPosition.Y;
		if (GlobalPosition.DistanceSquaredTo(look) < 1e-8f)
			return;
		LookAt(look, Vector3.Up);
		RotateY(Mathf.Pi);
	}

	private Vector3 PickArenaEdgeTowardPlayerOrRandom()
	{
		if (_player == null || !GodotObject.IsInstanceValid(_player) || !_player.IsInsideTree())
			return PickRandomArenaEdge();

		float h = Mathf.Max(0.5f, AreenanReunanPuolikas);
		Vector3 p = _player.GlobalPosition;
		Vector3 radial = new(p.X, 0f, p.Z);
		if (radial.LengthSquared() < 0.12f)
			return PickRandomArenaEdge();

		radial = radial.Normalized();
		float tx = Mathf.Abs(radial.X) > 1e-5f ? h / Mathf.Abs(radial.X) : float.MaxValue;
		float tz = Mathf.Abs(radial.Z) > 1e-5f ? h / Mathf.Abs(radial.Z) : float.MaxValue;
		float tEdge = Mathf.Min(tx, tz);
		float inset = Mathf.Clamp(h * 0.12f, 0.35f, 1.15f);
		Vector3 edge = radial * Mathf.Max(0.28f, tEdge - inset);
		edge.Y = _floorY;
		return edge;
	}

	/// <summary>
	/// Muut FBX:t (juoksu ilman skiniä jne.) tuovat usein eri etuliitteen kuin Dancing.fbx — raiteet eivät osu skeletoniin.
	/// </summary>
	private void AlignImportedClipsToDanceRig()
	{
		if (_animationPlayer == null || !_animationPlayer.HasAnimation(TanssiKlipinNimi))
			return;

		var dance = _animationPlayer.GetAnimation(TanssiKlipinNimi);
		void AlignIfHas(string clipName)
		{
			if (_animationPlayer.HasAnimation(clipName))
				AlignAnimationPathsFromTemplate(dance, _animationPlayer.GetAnimation(clipName));
		}

		AlignIfHas(JuoksuKlipinNimi);
		AlignIfHas(KuolemaKlipinNimi);
		AlignIfHas(OsumaKlipinNimi);
		AlignIfHas(SpinKlipinNimi);
	}

	/// <summary>
	/// Kohdistaa <paramref name="clipName"/>-klipin luuindeksit bossin näkyvään skeletoniin käyttäen
	/// <paramref name="fbxPackedScenePath"/>-tiedoston luumäärittelyä (luunimet Mixamossa yleensä samat, indeksit eivät).
	/// </summary>
	private void RemapImportedClipBoneIndicesFromSourceFbx(string fbxPackedScenePath, string clipName)
	{
		if (_animationPlayer == null || string.IsNullOrWhiteSpace(fbxPackedScenePath) || string.IsNullOrWhiteSpace(clipName))
			return;
		if (!_animationPlayer.HasAnimation(clipName))
			return;

		var packed = GD.Load<PackedScene>(fbxPackedScenePath);
		if (packed == null)
		{
			GD.PrintErr($"BossLevel1: RemapBoneIndices — FBX ei lataudu: {fbxPackedScenePath}");
			return;
		}

		var inst = packed.Instantiate();
		var srcSkel = FindFirstSkeleton3DDeep(inst);
		var dstVisual = GetNodeOrNull<Node3D>("BossVisual");
		var dstSkel = dstVisual != null ? dstVisual.FindChild("Skeleton3D", true, false) as Skeleton3D : null;

		if (srcSkel == null || dstSkel == null)
		{
			GD.PrintErr("BossLevel1: RemapBoneIndices — Skeleton3D puuttuu (lähde-FBX tai BossVisual).");
			inst.QueueFree();
			return;
		}

		var anim = _animationPlayer.GetAnimation(clipName);
		int ok = 0, skipped = 0, missingName = 0;

		for (int i = 0; i < anim.GetTrackCount(); i++)
		{
			if (anim.TrackGetType(i) != Animation.TrackType.Value)
				continue;

			if (!TrySplitAnimationTrackPath(anim.TrackGetPath(i), out string nodePathPart, out string prop))
			{
				skipped++;
				continue;
			}

			if (!TryParseBonesValueProperty(prop, out int srcBoneIdx, out string propTail))
			{
				skipped++;
				continue;
			}

			if (srcBoneIdx < 0 || srcBoneIdx >= srcSkel.GetBoneCount())
			{
				missingName++;
				continue;
			}

			StringName boneName = srcSkel.GetBoneName(srcBoneIdx);
			int dstIdx = dstSkel.FindBone(boneName);
			if (dstIdx < 0)
			{
				missingName++;
				continue;
			}

			string newProp = $"bones/{dstIdx}{propTail}";
			anim.TrackSetPath(i, new NodePath($"{nodePathPart}:{newProp}"));
			ok++;
		}

		inst.QueueFree();

		if (ok == 0 && anim.GetTrackCount() > 0)
			GD.PrintErr(
				$"BossLevel1: '{clipName}' — ei yhtään luuraidan remappia (väärä Mixamo-hahmo?). Lataa animaatio samasta hahmosta kuin Dancing.fbx.");
		else if (missingName > 0)
			GD.Print(
				$"BossLevel1: '{clipName}' bone-remap: ok={ok}, ei vastaavaa luita={missingName}, muut raiteet={skipped} (src bones={srcSkel.GetBoneCount()}, dst={dstSkel.GetBoneCount()})");
	}

	private static Skeleton3D FindFirstSkeleton3DDeep(Node node)
	{
		if (node is Skeleton3D sk)
			return sk;
		foreach (Node c in node.GetChildren())
		{
			var d = FindFirstSkeleton3DDeep(c);
			if (d != null)
				return d;
		}

		return null;
	}

	/// <summary>Godot 4: value-raidan polku on muotoa <c>NodePath:property</c>, esim. <c>Armature/Skeleton3D:bones/3/rotation</c>.</summary>
	private static bool TrySplitAnimationTrackPath(NodePath trackPath, out string nodePathPart, out string propertyPart)
	{
		nodePathPart = null;
		propertyPart = null;
		string full = trackPath.ToString();
		int colon = full.IndexOf(':');
		if (colon <= 0)
			return false;
		nodePathPart = full.Substring(0, colon);
		propertyPart = full.Substring(colon + 1);
		return !string.IsNullOrEmpty(propertyPart);
	}

	/// <summary>Godotin skeletin value-raide: bones/&lt;idx&gt;/position|rotation|scale</summary>
	private static bool TryParseBonesValueProperty(string prop, out int boneIndex, out string tailAfterIndex)
	{
		boneIndex = -1;
		tailAfterIndex = null;
		const string prefix = "bones/";
		if (string.IsNullOrEmpty(prop) || !prop.StartsWith(prefix, StringComparison.Ordinal))
			return false;

		int start = prefix.Length;
		int slash = prop.IndexOf('/', start);
		if (slash <= start)
			return false;
		if (!int.TryParse(prop.AsSpan(start, slash - start), out boneIndex))
			return false;
		tailAfterIndex = prop.Substring(slash);
		return true;
	}

	private static void AlignAnimationPathsFromTemplate(Animation template, Animation imported)
	{
		if (template == null || imported == null) return;
		if (template.GetTrackCount() == 0 || imported.GetTrackCount() == 0) return;

		if (!TryGetRigPathPrefix(template, out string pathPrefix))
			return;

		int n = imported.GetTrackCount();
		for (int i = 0; i < n; i++)
		{
			string p = imported.TrackGetPath(i).ToString();
			int arm = IndexOfRigRoot(p);
			if (arm < 0)
				continue;
			string suffix = p.Substring(arm);
			imported.TrackSetPath(i, new NodePath(pathPrefix + suffix));
		}
	}

	/// <summary>Ensimmäinen raita jonka polussa on Armature/Skeleton3D — ei oleta että raita 0 on luu.</summary>
	private static bool TryGetRigPathPrefix(Animation template, out string pathPrefix)
	{
		pathPrefix = null;
		for (int i = 0; i < template.GetTrackCount(); i++)
		{
			string refPath = template.TrackGetPath(i).ToString();
			int armRef = IndexOfRigRoot(refPath);
			if (armRef >= 0)
			{
				pathPrefix = refPath.Substring(0, armRef);
				return true;
			}
		}

		return false;
	}

	private static int IndexOfRigRoot(string nodePath)
	{
		int a = nodePath.IndexOf("Armature", StringComparison.Ordinal);
		if (a >= 0) return a;
		return nodePath.IndexOf("Skeleton3D", StringComparison.Ordinal);
	}

	private void TryChargeSeparationUnstick(float dt)
	{
		// Kerää törmäysnormaalit seinistä/esteistä (ei lattiasta, ei pelaajasta)
		Vector3 accum = Vector3.Zero;
		for (int i = 0; i < GetSlideCollisionCount(); i++)
		{
			var col = GetSlideCollision(i);
			Vector3 n = col.GetNormal();
			if (n.Y > 0.62f) continue; // lattia
			// Varmista ettei törmäys ole pelaajaan
			if (_player is CollisionObject3D pc && col.GetCollider() == pc) continue;
			Vector3 flat = new(n.X, 0f, n.Z);
			if (flat.LengthSquared() > 1e-5f)
				accum += flat.Normalized();
		}

		if (accum.LengthSquared() < 1e-8f)
			return;

		GlobalPosition += accum.Normalized() * Mathf.Clamp(dt * 6f, 0f, 0.22f);
	}

	private Vector3 PickRandomArenaEdge()
	{
		int edge = GD.RandRange(0, 3);
		double t = GD.RandRange(-AreenanReunanPuolikas + 0.5, AreenanReunanPuolikas - 0.5);
		return edge switch
		{
			0 => new Vector3(-AreenanReunanPuolikas, _floorY, (float)t),
			1 => new Vector3(AreenanReunanPuolikas,  _floorY, (float)t),
			2 => new Vector3((float)t, _floorY, -AreenanReunanPuolikas),
			_ => new Vector3((float)t, _floorY,  AreenanReunanPuolikas),
		};
	}

	// ─────────────────────────────────────────────
	// MIEKAN OSUMAT
	// ─────────────────────────────────────────────

	private void TrySwordHits()
	{
		if (_playerController == null || !_playerController.IsMeleeAttackActive())
		{
			_hasBeenHitThisSwing = false;
			return;
		}

		float animTime = _playerController.GetAttackAnimationTime();
		float hitFrom  = _playerController.GetMeleeStrikeWindowStart() + MiekkaIskuAktivoitumisaika;
		if (animTime < hitFrom || _hasBeenHitThisSwing) return;

		int dmg = _playerController.GetMeleeAttackDamage();

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
		OnSwordHitFeedback(_isDead, dmg);
	}

	private void OnSwordHitFeedback(bool isKillingBlow, int damageDealt)
	{
		float hpFrac = BossMaksimiTerveys > 0 ? (float)_bossHealth / BossMaksimiTerveys : 0f;
		float shakeAmp = isKillingBlow ? 0.32f : Mathf.Lerp(0.22f, 0.10f, hpFrac);
		GetOrFindCamera()?.ShakeImpulse(shakeAmp, 0.30f);

		if (isKillingBlow && IsInsideTree())
		{
			Engine.TimeScale = 0.15f;
			var slowTimer = GetTree().CreateTimer(0.07f, processInPhysics: false, ignoreTimeScale: true);
			slowTimer.Timeout += () => { if (Engine.TimeScale < 1f) Engine.TimeScale = 1f; };
		}

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

		if (isKillingBlow) return;

		bool playHitAnim = damageDealt >= 3
			&& _animationPlayer != null && GodotObject.IsInstanceValid(_animationPlayer)
			&& _animationPlayer.HasAnimation(OsumaKlipinNimi);

		if (playHitAnim)
		{
			_resumeClipAfterHit = _phase == BossPhase.Dancing ? TanssiKlipinNimi : JuoksuKlipinNimi;
			_hitReactPlaying = true;
			if (_chargingKickActive)
			{
				_chargingKickActive = false;
				_mmaKickFallbackPending = false;
			}
			_animationPlayer.SpeedScale = 1f;
			_animationPlayer.Play(OsumaKlipinNimi);
		}
		else if (_animationPlayer != null && GodotObject.IsInstanceValid(_animationPlayer))
		{
			_animationPlayer.SpeedScale = 0f;
			var animFreeze = CreateTween();
			animFreeze.TweenInterval(0.08f);
			animFreeze.TweenCallback(Callable.From(() =>
			{
				if (!GodotObject.IsInstanceValid(_animationPlayer) || _isDead)
					return;
				bool runCharge = _phase == BossPhase.Charging
					&& _animationPlayer.CurrentAnimation == JuoksuKlipinNimi;
				_animationPlayer.SpeedScale = runCharge
					? Mathf.Clamp(JuoksuClipSpeedScaleLatauksessa, 0.15f, 1.5f)
					: 1f;
			}));
		}

		var visualFeedback = GetNodeOrNull<Node3D>("BossVisual");
		if (visualFeedback != null && GodotObject.IsInstanceValid(visualFeedback) && !playHitAnim)
		{
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
			_recoilTween.TweenProperty(visualFeedback, "position", pushLocal, 0.04f)
				.SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
			_recoilTween.TweenProperty(visualFeedback, "position", Vector3.Zero, 0.22f)
				.SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);

			if (_bossVisualBaseScale.LengthSquared() > 0.001f)
			{
				Vector3 squashTarget = _bossVisualBaseScale * new Vector3(1.08f, 0.90f, 1.08f);
				_squashTween?.Kill();
				_squashTween = CreateTween();
				_squashTween.TweenProperty(visualFeedback, "scale", squashTarget, 0.04f)
					.SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
				_squashTween.TweenProperty(visualFeedback, "scale", _bossVisualBaseScale, 0.24f)
					.SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
			}
		}

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
		PlaySfxOnRoot(MiekkaIskuÄäniPolku, OsumaÄäniVoimakkuusDb);

		if (!string.IsNullOrWhiteSpace(BossVahinkoÄäniPolku))
		{
			GetTree().CreateTimer(BossVahinkoÄäniViive).Timeout +=
				() => PlaySfxOnRoot(BossVahinkoÄäniPolku, OsumaÄäniVoimakkuusDb);
		}
	}

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

	public void TakeDamage(int amount)
	{
		if (_isDead) return;
		_bossHealth -= amount;
		if (_bossHealth <= 0)
		{
			_bossHealth = 0;
			Die();
		}

		EmitSignal(SignalName.BossHealthChanged, _bossHealth, BossMaksimiTerveys);
	}

	private void Die()
	{
		_isDead = true;
		_hitReactPlaying = false;
		_chargingKickActive = false;
		_staggerActive = false;
		Velocity = Vector3.Zero;
		StopBossMusic();
		UpdateDanceHighlightLight(false);

		var col = GetNodeOrNull<CollisionShape3D>("CollisionShape3D");
		if (col != null) col.Disabled = true;

		if (_animationPlayer != null && _animationPlayer.HasAnimation(KuolemaKlipinNimi))
		{
			_animationPlayer.SpeedScale = 0.18f;
			_animationPlayer.Play(KuolemaKlipinNimi);
			float deathLen = (float)_animationPlayer.CurrentAnimationLength;
			GetTree().CreateTimer(deathLen / _animationPlayer.SpeedScale).Timeout += () => QueueFree();
		}
		else
		{
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

	private bool CloneMixamoClipFromSamePlayer(string path, string targetName)
	{
		var scene = GD.Load<PackedScene>(path);
		if (scene == null) return false;

		var inst = scene.Instantiate();
		var ap   = inst.FindChild("AnimationPlayer", true, false) as AnimationPlayer;
		if (ap == null) { inst.QueueFree(); return false; }

		Animation sourceAnim = null;
		foreach (var n in new[] { MixamoAnimaatioLähdeNimi, MixamoAnimaatioLähdeNimi.Replace("_", ".") })
		{
			if (ap.HasAnimation(n)) { sourceAnim = ap.GetAnimation(n); break; }
		}

		if (sourceAnim == null)
		{
			sourceAnim = ResolveAnimationFromPlayer(ap, MixamoAnimaatioLähdeNimi, path);
		}

		if (sourceAnim == null)
		{
			inst.QueueFree();
			GD.PrintErr($"BossLevel1: animaatiota '{MixamoAnimaatioLähdeNimi}' ei löydy: {path}");
			return false;
		}

		var anim = (Animation)sourceAnim.Duplicate();
		inst.QueueFree();

		anim.LoopMode = Animation.LoopModeEnum.Linear;
		var lib = _animationPlayer.GetAnimationLibrary("");
		if (lib.HasAnimation(targetName)) lib.RemoveAnimation(targetName);
		lib.AddAnimation(targetName, anim);
		return true;
	}

	private void LoadAnim(string path, string sourceName, string targetName, bool loop = false)
	{
		var scene = GD.Load<PackedScene>(path);
		if (scene == null) { GD.PrintErr($"BossLevel1 LoadAnim: {path}"); return; }

		var inst = scene.Instantiate();
		var ap   = inst.FindChild("AnimationPlayer", true, false) as AnimationPlayer;
		if (ap == null) { inst.QueueFree(); return; }

		Animation sourceAnim = ResolveAnimationFromPlayer(ap, sourceName, path);
		if (sourceAnim == null)
		{
			inst.QueueFree();
			GD.PrintErr($"BossLevel1: clip '{sourceName}' puuttuu: {path}");
			return;
		}

		var anim = (Animation)sourceAnim.Duplicate();
		inst.QueueFree();

		if (loop) anim.LoopMode = Animation.LoopModeEnum.Linear;
		else anim.LoopMode = Animation.LoopModeEnum.None;
		var lib = _animationPlayer.GetAnimationLibrary("");
		if (lib.HasAnimation(targetName)) lib.RemoveAnimation(targetName);
		lib.AddAnimation(targetName, anim);
	}

	private static Animation ResolveAnimationFromPlayer(AnimationPlayer ap, string sourceName, string pathForLog)
	{
		if (ap == null) return null;

		foreach (var name in new[] { sourceName, sourceName.Replace("_", ".") })
		{
			if (ap.HasAnimation(name))
				return ap.GetAnimation(name);
		}

		foreach (StringName nm in ap.GetAnimationList())
		{
			string sn = nm.ToString();
			if (sn.IndexOf("RESET", StringComparison.OrdinalIgnoreCase) >= 0)
				continue;
			if (sn.IndexOf("mixamo", StringComparison.OrdinalIgnoreCase) >= 0)
				return ap.GetAnimation(nm);
		}

		foreach (StringName nm in ap.GetAnimationList())
		{
			string sn = nm.ToString();
			if (sn.IndexOf("RESET", StringComparison.OrdinalIgnoreCase) >= 0)
				continue;
			return ap.GetAnimation(nm);
		}

		return null;
	}
}
