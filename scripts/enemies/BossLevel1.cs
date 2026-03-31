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

	/// <summary>Mixamon animaation sisäinen nimi FBX:ssä.</summary>
	[Export] public string MixamoAnimSourceName = "mixamo_com";

	/// <summary>Tanssianimaation nimi pelissä.</summary>
	[Export] public string DanceClipName = "dance";

	/// <summary>Juoksuanimaation nimi pelissä.</summary>
	[Export] public string RunClipName = "run";

	/// <summary>Bossin elinvoima — vahva isku (R1, dmg=3) tappaa kerralla jos HP <= 3.</summary>
	[Export] public int Health = 30;

	/// <summary>Tanssin minimikesto sekunteina ennen syöksyä.</summary>
	[Export] public float DanceDurationMin = 3.5f;

	/// <summary>Tanssin maksimikesto sekunteina.</summary>
	[Export] public float DanceDurationMax = 7f;

	/// <summary>Syöksyn nopeus.</summary>
	[Export] public float ChargeSpeed = 14f;

	/// <summary>Syöksyn maksimikesto — jos pelaajaa ei tavoiteta, palataan tanssimaan.</summary>
	[Export] public float ChargeMaxSeconds = 3.2f;

	/// <summary>Areenan puolisäde — syöksyn spawnauspisteet ovat tämän etäisyydellä keskipisteestä.</summary>
	[Export] public float ArenaEdgeHalf = 13.5f;

	/// <summary>Kontaktivahingon määrä syöksyn aikana.</summary>
	[Export] public float ChargeContactDamage = 1;

	/// <summary>Aika sekunteina ennen kuin kontaktivahinko voi toistua.</summary>
	[Export] public float ChargeContactCooldown = 0.85f;

	/// <summary>Miekan kantama — kuinka kaukaa osuma rekisteröidään.</summary>
	[Export] public float SwordHitRange = 2.85f;

	/// <summary>Osumakeskipisteen korkeus Y-akselilla.</summary>
	[Export] public float HitCenterYOffset = 0.9f;

	/// <summary>Useita korkeuksia osumatarkistusta varten (jalat, keskivartalo, pää).</summary>
	[Export] public float[] SwordHitProbeHeights = { 0.45f, 0.9f, 1.25f };

	/// <summary>Lisäviive miekan osumaikkunaan (0 = heti kun animaatio alkaa).</summary>
	[Export] public float SwordHitActivationTime = 0f;

	/// <summary>Kuolemisanimaation kesto sekunteina (kutistuminen).</summary>
	[Export] public float DeathShrinkDuration = 0.55f;

	/// <summary>
	/// Visuaalisen meshin skaalauskerroin.
	/// Mixamon FBX on usein ~100x liian pieni — aseta 100 tai säädä editorissa.
	/// </summary>
	[Export] public float BossVisualUniformScale = 100f;

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

	/// <summary>True kun bossi tanssii — vain tällöin vahva isku (R1) voi osua.</summary>
	public bool IsDanceVulnerable => !_isDead && _phase == BossPhase.Dancing;

	// ─────────────────────────────────────────────
	// PRIVAATIT MUUTTUJAT
	// ─────────────────────────────────────────────

	/// <summary>Bossin käyttäytymistila.</summary>
	private enum BossPhase { Dancing, Charging }

	private BossPhase _phase = BossPhase.Dancing;

	/// <summary>Jäljellä oleva tanssinäytösaika.</summary>
	private float _danceTimeLeft;

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
	}

	public override void _Ready()
	{
		// Törmäyskerrokset: Layer 2 = vihollinen, Mask 3 = pelaaja + maailma
		CollisionLayer = 2;
		CollisionMask = 3;
		AddToGroup("level1_boss");

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

		// Ladataan juoksuanimaatio without skin -tiedostosta
		LoadAnim(FastRunFbxPath, MixamoAnimSourceName, RunClipName, loop: true);

		// Aloitetaan tanssi
		_animationPlayer.Play(DanceClipName);
		_danceTimeLeft = (float)GD.RandRange(DanceDurationMin, DanceDurationMax);
		_phase = BossPhase.Dancing;

		// Linkitetään tekstuurit meshiin
		ApplyBossTextures();
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
			case BossPhase.Charging: ProcessCharging(dt); break;
		}

		// Tarkistetaan osuuko pelaajan miekka
		TrySwordHits();
		MoveAndSlide();
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
		_danceTimeLeft -= dt;
		if (_danceTimeLeft > 0f) return;
		BeginCharge();
	}

	/// <summary>
	/// Aloittaa syöksyn: teleporttaa areenan reunalle ja suuntaa pelaajaan.
	/// </summary>
	private void BeginCharge()
	{
		_phase = BossPhase.Charging;

		// Teleportataan satunnaiselle reunalle
		Vector3 edge = PickRandomArenaEdge();
		edge.Y = _floorY;
		GlobalPosition = edge;

		// Käännetään pelaajaa kohti
		if (_player != null && GodotObject.IsInstanceValid(_player) && _player.IsInsideTree())
		{
			var look = _player.GlobalPosition with { Y = GlobalPosition.Y };
			if (GlobalPosition.DistanceTo(look) > 0.05f)
				LookAt(look, Vector3.Up);
				RotateY(Mathf.Pi); // ← käännetään 180°
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
		if (_chargeContactCd > 0f || _playerController == null) return;

		for (int i = 0; i < GetSlideCollisionCount(); i++)
		{
			if (GetSlideCollision(i).GetCollider() is PlayerController)
			{
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
		_phase = BossPhase.Dancing;
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

		// Tarkistetaan onko bossi oikeassa kulmassa miekkaan nähden
		Vector3 arcRef = GlobalPosition + Vector3.Up * HitCenterYOffset;
		bool inCone = _playerController.IsPointInMeleeHitFacingArc(arcRef)
			|| _playerController.IsPointInMeleeHitBladeArc(arcRef);
		if (!inCone) return;

		// Tarkistetaan etäisyys useissa eri korkeuksissa
		var probes = SwordHitProbeHeights ?? new[] { HitCenterYOffset };
		for (int i = 0; i < probes.Length; i++)
		{
			Vector3 p = GlobalPosition + Vector3.Up * probes[i];
			if (_playerController.GetMeleeHitDistanceToPoint(p) < SwordHitRange)
			{
				TakeDamage(dmg);
				_playerController.NotifyMeleeHitLanded();
				_hasBeenHitThisSwing = true;
				return;
			}
		}
	}

	// ─────────────────────────────────────────────
	// VAHINKO JA KUOLEMA
	// ─────────────────────────────────────────────

	/// <summary>Vähennetään bossin HP:ta. Kuolee kun HP menee nollaan.</summary>
	public void TakeDamage(int amount)
	{
		if (_isDead) return;
		Health -= amount;
		GD.Print($"Bossi sai {amount} vahinkoa! HP jäljellä: {Health}");
		if (Health <= 0) Die();
	}

	/// <summary>Bossin kuolema — poistetaan törmäys ja kutistetaan mesh nollaan.</summary>
	private void Die()
	{
		_isDead = true;
		Velocity = Vector3.Zero;

		// Poistetaan törmäys heti ettei fysiikkavirheitä tule
		var col = GetNodeOrNull<CollisionShape3D>("CollisionShape3D");
		if (col != null) col.Disabled = true;

		// Kutistetaan visuaali nollaan — haetaan BossVisual tai käytetään juurta
		Node3D visual = GetNodeOrNull<Node3D>("BossVisual") ?? (Node3D)this;
		var tween = CreateTween();
		tween.TweenProperty(visual, "scale", Vector3.Zero, DeathShrinkDuration)
			.SetTrans(Tween.TransitionType.Cubic)
			.SetEase(Tween.EaseType.In);
		tween.TweenCallback(Callable.From(() => QueueFree()));
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
