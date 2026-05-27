using System;
using Godot;

/// <summary>
/// Level 3 tornibossi: Mixamo-heittäjä tornin huipulla, välillä iso hyppy maahan kohti pelaajaa,
/// kuolemaan <c>Death From Back Headshot</c>. HP + dronen pommit (kerros 14).
/// </summary>
public partial class BossLevel3 : Node3D
{
	public const uint DroneBombPhysicsLayer = 1u << 13;

	private const string BigJumpClipName = "big_jump";
	private const string DeathClipName = "death";

	private enum BossPhase
	{
		Throwing,
		JumpToGround,
		OnGround,
		JumpToTower,
		Dead,
	}

	[ExportCategory("Terveys")]
	/// <summary>HUD-otsikko (BossHealthBar).</summary>
	[Export] public string HudDisplayName = "Final Boss";
	[Export] public int BossTerveysAlussa = 28;
	[Export] public int PommiVahinkoPerOsuma = 7;
	[Export] public string BossVahinkoÄäniPolku = "res://assets/audio/sfx/enemybosshit.mp3";
	[Export] public float PelaajaAktivointiSädeXZ = 46f;

	[ExportCategory("Visuaali")]
	/// <summary>Muokkaa hahmon sijaintia/skalausta tiedostossa <c>boss_level_3_visual.tscn</c>.</summary>
	[Export] public NodePath VisuaaliPolku = "Visual";

	[ExportCategory("Animaatiot (Mixamo FBX)")]
	[Export] public string HeittoAnimaatioPolku = "res://assets/models/Level3_BossEnemy/Throw.fbx";
	[Export] public string IsoHyppyAnimaatioPolku = "res://assets/models/Level3_BossEnemy/Big Jump.fbx";
	[Export] public string KuolemaAnimaatioPolku = "res://assets/models/Level3_BossEnemy/Death From Back Headshot.fbx";
	[Export] public string MixamoAnimaatioLähdeNimi = "mixamo_com";
	/// <summary>Lisä-Y-käännös pelaajaa kohti (asteet). Säädä vain jos mesh katsoo väärään suuntaan — oletus 0 kun suunta on kunnossa editorissa.</summary>
	[Export] public float KatseenKiertoAstetta = 0f;

	[ExportCategory("Kivenheitto")]
	[Export] public PackedScene RollingRockScene;
	/// <summary>Marker3D hahmon käden kohdalla — kivi ilmestyy tähän (muokkaa boss_level_3_visual.tscn).</summary>
	[Export] public NodePath KivenSpawnPolku = "Visual/FacingPivot/RockSpawnPoint";
	[Export] public float HeittoVapautusVaiheMin = 0.40f;
	[Export] public float HeittoVapautusVaiheMax = 0.55f;
	[Export] public float HeittoVäliMinSek = 2.0f;
	[Export] public float HeittoVäliMaxSek = 4.2f;
	[Export] public float Horisontaalinopeus = 19f;
	[Export] public float YlösKomponentti = 6.5f;
	[Export] public float TähtäysSatunnaisuusX = 2.8f;
	[Export] public float TähtäysSatunnaisuusZ = 1.6f;
	[Export] public float KulmanopeusMin = 2f;
	[Export] public float KulmanopeusMax = 7f;

	[ExportCategory("Iso hyppy")]
	[Export] public float HyppyVäliMinSek = 14f;
	[Export] public float HyppyVäliMaxSek = 28f;
	[Export] public NodePath RoundaboutDeckPolku = new("World/RoundaboutDeck");
	[Export] public NodePath YlamaaTiePolku = new("World/UphillRoadRoot/UphillRoad");
	[Export] public float HyppyDeckReunaInset = 2.5f;
	[Export] public float HyppyLaskeutumisSatunnaisuusXZ = 0.85f;
	[Export] public float HyppyLaskeutumisNormaaliBias = 0.08f;
	[Export] public float HyppyKaarenKorkeusMin = 6f;
	/// <summary>Bezier-kontrollipiste: 0 = torni, 1 = laskeutuminen. Alempi = syvempi sukellus kohti kohdetta.</summary>
	[Export] public float HyppyKaarenHorisontaalikerroin = 0.38f;
	[Export] public float MaallaHeittoAikaSek = 3.5f;
	[Export] public float MaallaHeittoVäliMinSek = 1.1f;
	[Export] public float MaallaHeittoVäliMaxSek = 1.8f;
	/// <summary>0 = automaattinen (max HP / 3, kuten Level3-käärme).</summary>
	[Export] public int HyppyIskuVahinko = 0;
	[Export] public float HyppyIskuSadeXZ = 9f;
	[Export] public float HyppyIskuSadeY = 7f;

	private int _hp;
	private bool _dead;
	private BossPhase _phase = BossPhase.Throwing;

	private Area3D _hurt;
	private Node3D _visual;
	private Node3D _facingPivot;
	private Node3D _rockSpawn;
	private AnimationPlayer _animationPlayer;
	private string _activeThrowClip;
	private Node3D _player;
	private Node3D _level3Root;
	private Node3D _roundaboutDeck;
	private Node3D _uphillRoad;

	private Timer _throwTimer;
	private float _jumpTimer;
	private float _groundThrowLeft;

	private Vector3 _towerWorldPos;
	private Vector3 _landingWorldPos;
	private float _visualFeetLocalY;
	private Tween _moveTween;

	public bool IsBossDead => _dead;

	public int GetBossMaxHealth() => Mathf.Max(1, BossTerveysAlussa);
	public int GetBossCurrentHealth() => Mathf.Clamp(_hp, 0, BossTerveysAlussa);

	public override void _Ready()
	{
		AddToGroup("level3_boss");
		_hp = BossTerveysAlussa;
		_hurt = GetNodeOrNull<Area3D>("HurtArea");
		_visual = HasExportNodePath(VisuaaliPolku)
			? GetNodeOrNull<Node3D>(VisuaaliPolku)
			: null;
		_visual ??= GetNodeOrNull<Node3D>("Visual");
		if (_hurt != null)
			_hurt.BodyEntered += OnHurtBodyEntered;

		_player = GetTree()?.GetFirstNodeInGroup("player") as Node3D;
		ResolveLevel3Nodes();
		_towerWorldPos = GlobalPosition;

		_throwTimer = new Timer { OneShot = true, Autostart = false };
		_throwTimer.Timeout += OnThrowTimer;
		AddChild(_throwTimer);

		Callable.From(DeferredBossSetup).CallDeferred();
		Callable.From(DeferredMeshTangentFixOnTowerRoot).CallDeferred();
	}

	public override void _ExitTree()
	{
		if (_animationPlayer != null && GodotObject.IsInstanceValid(_animationPlayer))
			_animationPlayer.AnimationFinished -= OnAnimationFinished;
		base._ExitTree();
	}

	public override void _Process(double delta)
	{
		if (_dead || _phase == BossPhase.Dead)
			return;

		if (_player == null || !GodotObject.IsInstanceValid(_player) || !_player.IsInsideTree())
			_player = GetTree()?.GetFirstNodeInGroup("player") as Node3D;

		if (_phase == BossPhase.Throwing && IsDroneFightActiveForPlayer())
		{
			_jumpTimer -= (float)delta;
			if (_jumpTimer <= 0f)
				StartBigJumpToGround();
		}

		if (_phase is BossPhase.Throwing or BossPhase.OnGround)
			FacePlayer();

		if (_phase == BossPhase.OnGround)
		{
			_groundThrowLeft -= (float)delta;
			if (_groundThrowLeft <= 0f)
				StartJumpBackToTower();
		}
	}

	private void DeferredBossSetup()
	{
		if (_visual == null)
		{
			GD.PushError("BossLevel3: Visual-node puuttuu — avaa scenes/enemies/boss_level_3_visual.tscn ja varmista Character-instanssi.");
			return;
		}

		_visual.Visible = true;
		_facingPivot = _visual.GetNodeOrNull<Node3D>("FacingPivot") ?? _visual;
		_rockSpawn = ResolveRockSpawnPoint();
		if (_rockSpawn == null)
			GD.PushWarning("BossLevel3: RockSpawnPoint puuttuu — lisää Marker3D polkuun Visual/FacingPivot/RockSpawnPoint.");

		_animationPlayer = _visual.FindChild("AnimationPlayer", true, false) as AnimationPlayer;
		if (_animationPlayer == null)
		{
			GD.PushError("BossLevel3: AnimationPlayer puuttuu Throw.fbx:n sisältä.");
			return;
		}

		_activeThrowClip = ResolveNativeThrowClipName();
		if (string.IsNullOrEmpty(_activeThrowClip))
		{
			GD.PushError("BossLevel3: Throw.fbx:n animaatiota ei löydy AnimationPlayeristä.");
			return;
		}

		LoadAnim(IsoHyppyAnimaatioPolku, MixamoAnimaatioLähdeNimi, BigJumpClipName, loop: false);
		LoadAnim(KuolemaAnimaatioPolku, MixamoAnimaatioLähdeNimi, DeathClipName, loop: false);
		AlignImportedClipsToThrowRig();
		RemapImportedClipBoneIndicesFromSourceFbx(IsoHyppyAnimaatioPolku, BigJumpClipName);
		RemapImportedClipBoneIndicesFromSourceFbx(KuolemaAnimaatioPolku, DeathClipName);

		CacheVisualFeetLocalY();
		_animationPlayer.AnimationFinished += OnAnimationFinished;
		PlayThrowLoop();
		ScheduleNextThrow();
		_jumpTimer = NextJumpInterval();
	}

	private void DeferredMeshTangentFixOnTowerRoot()
	{
		var towerRoot = GetParent()?.GetParent();
		if (towerRoot == null)
			return;

		// Vain torni-GLB:n staattiset meshit — ei skinattua boss-hahmoa (rikkoisi näkyvyyden ajossa).
		MeshTangentFix.ApplyToSubtree(towerRoot, static n =>
			n.Name == "TowerBossRockThrower" || n is BossLevel3);
	}

	private void OnHurtBodyEntered(Node3D body)
	{
		if (body is Level3DroneBomb bomb)
			TryApplyDroneBombHit(bomb);
	}

	public bool TryApplyDroneBombHit(Level3DroneBomb bomb)
	{
		if (_dead || bomb == null || !IsDroneFightActiveForPlayer())
			return false;
		if (!bomb.TryConsumeBossHit())
			return false;
		ApplyDamage(Mathf.Max(1, bomb.BossDamage > 0 ? bomb.BossDamage : PommiVahinkoPerOsuma));
		return true;
	}

	private bool IsDroneFightActiveForPlayer()
	{
		if (_player == null || !GodotObject.IsInstanceValid(_player))
			return false;
		var a = new Vector2(GlobalPosition.X, GlobalPosition.Z);
		var b = new Vector2(_player.GlobalPosition.X, _player.GlobalPosition.Z);
		return a.DistanceTo(b) <= PelaajaAktivointiSädeXZ;
	}

	private void ApplyDamage(int amount)
	{
		if (_dead)
			return;
		_hp -= amount;
		PlayHitSound();
		GD.Print($"BossLevel3 HP: {_hp} / {BossTerveysAlussa}");
		if (_hp <= 0)
			Die();
	}

	private void PlayHitSound()
	{
		if (string.IsNullOrEmpty(BossVahinkoÄäniPolku))
			return;
		var p = new AudioStreamPlayer3D
		{
			Stream = GD.Load<AudioStream>(BossVahinkoÄäniPolku),
			MaxDistance = 80f,
			UnitSize = 4f,
		};
		AddChild(p);
		p.Finished += () => p.QueueFree();
		p.Play();
	}

	private void Die()
	{
		if (_dead)
			return;
		_dead = true;
		_phase = BossPhase.Dead;
		StopThrowing();
		KillMoveTween();

		if (_hurt != null)
		{
			_hurt.BodyEntered -= OnHurtBodyEntered;
			_hurt.SetDeferred(Area3D.PropertyName.Monitoring, false);
		}

		if (_animationPlayer != null && GodotObject.IsInstanceValid(_animationPlayer) && _animationPlayer.HasAnimation(DeathClipName))
			_animationPlayer.Play(DeathClipName);

		GD.Print("BossLevel3: kuollut — heitot pysähtyvät, kuolema-animaatio.");
	}

	private void StopThrowing()
	{
		_throwTimer?.Stop();
	}

	private void OnThrowTimer()
	{
		if (_dead || _phase is BossPhase.JumpToGround or BossPhase.JumpToTower or BossPhase.Dead)
			return;

		TryThrowOneRock();
		ScheduleNextThrow();
	}

	private Node3D ResolveRockSpawnPoint()
	{
		if (HasExportNodePath(KivenSpawnPolku))
		{
			var explicitNode = GetNodeOrNull<Node3D>(KivenSpawnPolku);
			if (explicitNode != null)
				return explicitNode;
		}

		return _visual?.GetNodeOrNull<Node3D>("FacingPivot/RockSpawnPoint")
			?? _visual?.GetNodeOrNull<Node3D>("Character/RockSpawnPoint");
	}

	private static bool HasExportNodePath(NodePath path) =>
		path != null && !path.IsEmpty;

	private void ScheduleNextThrow()
	{
		if (_dead || _throwTimer == null)
			return;

		float min = _phase == BossPhase.OnGround ? MaallaHeittoVäliMinSek : HeittoVäliMinSek;
		float max = _phase == BossPhase.OnGround ? MaallaHeittoVäliMaxSek : HeittoVäliMaxSek;
		_throwTimer.WaitTime = (float)GD.RandRange(min, max);
		_throwTimer.Start();
	}

	private bool TryThrowOneRock()
	{
		if (_dead || RollingRockScene == null)
			return false;

		if (_player == null || !GodotObject.IsInstanceValid(_player) || !_player.IsInsideTree())
			return false;

		Node inst = RollingRockScene.Instantiate();
		if (inst is not RollingRockLevel3 rock)
		{
			GD.PushError($"BossLevel3: juuri ei ole RollingRockLevel3 ({inst?.GetType().Name}).");
			inst?.QueueFree();
			return false;
		}

		Node rockParent = FindRockParent();
		if (rockParent == null)
		{
			GD.PushError("BossLevel3: ei löydy Level3-juurta kiven parentiksi.");
			rock.QueueFree();
			return false;
		}

		var spawnNode = _rockSpawn != null && GodotObject.IsInstanceValid(_rockSpawn) ? _rockSpawn : this;
		Vector3 spawn = spawnNode.GlobalPosition;
		Vector3 target = _player.GlobalPosition;
		target.X += (float)GD.RandRange(-TähtäysSatunnaisuusX, TähtäysSatunnaisuusX);
		target.Z += (float)GD.RandRange(-TähtäysSatunnaisuusZ, TähtäysSatunnaisuusZ);

		Vector3 horiz = new Vector3(target.X - spawn.X, 0f, target.Z - spawn.Z);
		if (horiz.LengthSquared() < 0.04f)
			horiz = new Vector3(-1f, 0f, 0f);
		horiz = horiz.Normalized();

		float up = YlösKomponentti * (float)GD.RandRange(0.85, 1.12f);
		float hSpeed = Horisontaalinopeus * (float)GD.RandRange(0.88, 1.08f);
		Vector3 linear = horiz * hSpeed + Vector3.Up * up;

		Vector3 axis = new Vector3(
			(float)GD.RandRange(-1f, 1f),
			(float)GD.RandRange(-1f, 1f),
			(float)GD.RandRange(-1f, 1f));
		if (axis.LengthSquared() < 1e-4f)
			axis = Vector3.Up;
		axis = axis.Normalized();
		float omega = (float)GD.RandRange(KulmanopeusMin, KulmanopeusMax);
		Vector3 angular = axis * omega;

		rockParent.AddChild(rock);
		rock.GlobalPosition = spawn;
		rock.LaunchProjectile(linear, angular);
		return true;
	}

	private void StartBigJumpToGround()
	{
		if (_dead || _phase != BossPhase.Throwing || _animationPlayer == null)
			return;
		if (!_animationPlayer.HasAnimation(BigJumpClipName))
			return;
		if (!TryComputeJumpLandingWorld(out _landingWorldPos))
		{
			_jumpTimer = NextJumpInterval();
			return;
		}

		Vector3 jumpStart = GlobalPosition;
		_phase = BossPhase.JumpToGround;
		StopThrowing();
		_jumpTimer = NextJumpInterval();
		PlayBigJumpOnce();
		float arc = ComputeJumpArcHeight(jumpStart, _landingWorldPos);
		TweenAlongArc(jumpStart, _landingWorldPos, arc, OnJumpToGroundFinished);
	}

	private void OnJumpToGroundFinished()
	{
		if (_dead || _phase != BossPhase.JumpToGround)
			return;

		GlobalPosition = _landingWorldPos;
		Vector3 pos = GlobalPosition;
		RefineLandingRootOnRoad(pos.X, pos.Z, ref pos);
		GlobalPosition = pos;
		TryApplyJumpLandingImpact();
		_phase = BossPhase.OnGround;
		_groundThrowLeft = MaallaHeittoAikaSek;
		PlayThrowLoop();
		ScheduleNextThrow();
	}

	private void StartJumpBackToTower()
	{
		if (_dead || _phase != BossPhase.OnGround || _animationPlayer == null)
			return;
		if (!_animationPlayer.HasAnimation(BigJumpClipName))
		{
			GlobalPosition = _towerWorldPos;
			ResumeTowerThrowing();
			return;
		}

		_phase = BossPhase.JumpToTower;
		StopThrowing();
		PlayBigJumpOnce();
		float arc = ComputeJumpArcHeight(GlobalPosition, _towerWorldPos);
		TweenAlongArc(GlobalPosition, _towerWorldPos, arc * 0.85f, OnJumpToTowerFinished);
	}

	private void OnJumpToTowerFinished()
	{
		if (_dead)
			return;
		GlobalPosition = _towerWorldPos;
		ResumeTowerThrowing();
	}

	private void ResumeTowerThrowing()
	{
		_phase = BossPhase.Throwing;
		PlayThrowLoop();
		ScheduleNextThrow();
	}

	private void PlayThrowLoop()
	{
		if (_animationPlayer == null || string.IsNullOrEmpty(_activeThrowClip))
			return;
		if (!_animationPlayer.HasAnimation(_activeThrowClip))
			return;

		_animationPlayer.GetAnimation(_activeThrowClip).LoopMode = Animation.LoopModeEnum.Linear;
		_animationPlayer.Play(_activeThrowClip);
	}

	private void PlayBigJumpOnce()
	{
		if (_animationPlayer == null || !_animationPlayer.HasAnimation(BigJumpClipName))
			return;
		_animationPlayer.Play(BigJumpClipName);
	}

	private void OnAnimationFinished(StringName animName)
	{
		if (_dead)
			return;

		string name = animName.ToString();
		if (name == BigJumpClipName && _phase is BossPhase.JumpToGround or BossPhase.JumpToTower)
			return;

		if (_phase is BossPhase.Throwing or BossPhase.OnGround
			&& name != _activeThrowClip
			&& !string.IsNullOrEmpty(_activeThrowClip)
			&& _animationPlayer.HasAnimation(_activeThrowClip))
			PlayThrowLoop();
	}

	private void TweenAlongArc(Vector3 start, Vector3 end, float arcHeight, Action onFinished)
	{
		KillMoveTween();

		float duration = 1.2f;
		if (_animationPlayer != null && _animationPlayer.HasAnimation(BigJumpClipName))
		{
			double len = _animationPlayer.GetAnimation(BigJumpClipName).Length;
			if (len > 0.05)
				duration = (float)len;
		}

		Vector3 horizDelta = new(end.X - start.X, 0f, end.Z - start.Z);
		float tControl = Mathf.Clamp(HyppyKaarenHorisontaalikerroin, 0.2f, 0.75f);
		Vector3 control = start + horizDelta * tControl + Vector3.Up * arcHeight;

		_moveTween = CreateTween();
		_moveTween.TweenMethod(Callable.From<float>(t =>
		{
			// Vaaka suoraan kohti kohdetta (pelaaja); Y erillinen kaari — ei sivusuuntaista harhaa mäkeä pitkin.
			float y = SampleQuadraticBezier(start, control, end, t).Y;
			float nx = Mathf.Lerp(start.X, end.X, t);
			float nz = Mathf.Lerp(start.Z, end.Z, t);
			GlobalPosition = new Vector3(nx, y, nz);
			FaceHorizontalToward(
				Mathf.Lerp(start.X, end.X, Mathf.Min(1f, t + 0.06f)),
				Mathf.Lerp(start.Z, end.Z, Mathf.Min(1f, t + 0.06f)));
		}), 0f, 1f, duration)
			.SetTrans(Tween.TransitionType.Sine)
			.SetEase(Tween.EaseType.In);
		_moveTween.Finished += () => onFinished?.Invoke();
	}

	private void KillMoveTween()
	{
		if (_moveTween != null && GodotObject.IsInstanceValid(_moveTween))
		{
			_moveTween.Kill();
			_moveTween = null;
		}
	}

	private static Vector3 SampleQuadraticBezier(Vector3 a, Vector3 control, Vector3 b, float t)
	{
		float u = 1f - t;
		return u * u * a + 2f * u * t * control + t * t * b;
	}

	private bool TryComputeJumpLandingWorld(out Vector3 landing)
	{
		landing = _towerWorldPos;
		if (_player == null || !GodotObject.IsInstanceValid(_player))
			return false;

		float spread = Mathf.Max(0f, HyppyLaskeutumisSatunnaisuusXZ);
		float targetX = _player.GlobalPosition.X + (float)GD.RandRange(-spread, spread);
		float targetZ = _player.GlobalPosition.Z + (float)GD.RandRange(-spread, spread);

		if (TryRaycastRoadLanding(targetX, targetZ, out landing))
			return true;

		if (TrySampleRoundaboutNearTarget(targetX, targetZ, out landing))
			return true;

		if (TrySampleUphillRoadNearTarget(targetX, targetZ, out landing))
			return true;

		return TryFindLandingNearPlayer(targetX, targetZ, out landing);
	}

	private bool TrySampleUphillRoadNearTarget(float worldX, float worldZ, out Vector3 landing)
	{
		landing = _towerWorldPos;
		if (_uphillRoad == null || !IsWorldXzInsideUphillRoad(worldX, worldZ))
			return false;
		if (!Level3UphillRoadSurface.TrySampleTopFaceWorldAtXZ(_uphillRoad, worldX, worldZ, out landing, out Vector3 roadN))
			return false;

		FinalizeJumpLandingRoot(ref landing, roadN);
		return true;
	}

	private bool IsWorldXzInsideUphillRoad(float worldX, float worldZ)
	{
		if (_uphillRoad == null || !GodotObject.IsInstanceValid(_uphillRoad))
			return false;
		if (!Level3UphillRoadSurface.TryGetCsgBoxSize(_uphillRoad, out Vector3 size))
			return false;

		Vector3 local = _uphillRoad.GlobalTransform.AffineInverse() * new Vector3(worldX, _uphillRoad.GlobalPosition.Y, worldZ);
		return Mathf.Abs(local.X) <= size.X * 0.5f && Mathf.Abs(local.Z) <= size.Z * 0.5f;
	}

	private bool TryFindLandingNearPlayer(float targetX, float targetZ, out Vector3 landing)
	{
		landing = _towerWorldPos;
		float[] radii = { 0f, 1.5f, 3f, 5f, 8f };
		foreach (float radius in radii)
		{
			if (radius <= 1e-4f)
			{
				if (TryAnyLandingAt(targetX, targetZ, out landing))
					return true;
				continue;
			}

			for (int i = 0; i < 8; i++)
			{
				float angle = i * Mathf.Tau / 8f;
				float x = targetX + Mathf.Cos(angle) * radius;
				float z = targetZ + Mathf.Sin(angle) * radius;
				if (TryAnyLandingAt(x, z, out landing))
					return true;
			}
		}

		return false;
	}

	private bool TryAnyLandingAt(float worldX, float worldZ, out Vector3 landing)
	{
		if (TryRaycastRoadLanding(worldX, worldZ, out landing))
			return true;
		if (TrySampleRoundaboutNearTarget(worldX, worldZ, out landing))
			return true;
		if (TrySampleUphillRoadNearTarget(worldX, worldZ, out landing))
			return true;
		return false;
	}

	private bool TrySampleRoundaboutNearTarget(float worldX, float worldZ, out Vector3 landing)
	{
		landing = _towerWorldPos;
		if (_roundaboutDeck == null || !GodotObject.IsInstanceValid(_roundaboutDeck))
			return false;
		if (!Level3UphillRoadSurface.TryGetCsgCylinderParams(_roundaboutDeck, out float radius, out _))
			return false;

		Transform3D gt = _roundaboutDeck.GlobalTransform;
		Vector3 local = gt.AffineInverse() * new Vector3(worldX, gt.Origin.Y, worldZ);
		float maxR = Mathf.Max(0.35f, radius - Mathf.Max(0f, HyppyDeckReunaInset));
		if (new Vector2(local.X, local.Z).Length() > maxR)
			return false;

		if (!Level3UphillRoadSurface.TrySampleCsgCylinderTopAtXZ(
			_roundaboutDeck, worldX, worldZ, HyppyDeckReunaInset, out landing, out Vector3 deckN))
			return false;

		FinalizeJumpLandingRoot(ref landing, deckN);
		return true;
	}

	private bool TryRaycastRoadSurface(float worldX, float worldZ, out Vector3 surfacePoint, out Vector3 normal)
	{
		surfacePoint = _towerWorldPos;
		normal = Vector3.Up;
		var world = GetWorld3D();
		if (world == null)
			return false;

		float probeY = 36f;
		if (_player != null && GodotObject.IsInstanceValid(_player))
			probeY = Mathf.Max(probeY, _player.GlobalPosition.Y + 10f);
		if (_roundaboutDeck != null && GodotObject.IsInstanceValid(_roundaboutDeck))
			probeY = Mathf.Max(probeY, _roundaboutDeck.GlobalPosition.Y + 8f);

		var from = new Vector3(worldX, probeY, worldZ);
		var to = from + Vector3.Down * 56f;

		var query = PhysicsRayQueryParameters3D.Create(from, to);
		query.CollideWithAreas = false;
		query.CollideWithBodies = true;
		query.CollisionMask = 0xFFFF_FFFFu;

		var hit = world.DirectSpaceState.IntersectRay(query);
		if (hit.Count == 0 || !hit.TryGetValue("position", out var posObj))
			return false;
		if (!Level3UphillRoadSurface.ColliderIsLevel3RoadDeck(hit))
			return false;

		normal = hit.TryGetValue("normal", out var nrmObj)
			? ((Vector3)nrmObj).Normalized()
			: Vector3.Up;
		if (normal.Dot(Vector3.Up) < 0.5f)
			normal = Vector3.Up;

		surfacePoint = (Vector3)posObj;
		return true;
	}

	private bool TryRaycastRoadLanding(float worldX, float worldZ, out Vector3 landing)
	{
		landing = _towerWorldPos;
		if (!TryRaycastRoadSurface(worldX, worldZ, out Vector3 surface, out Vector3 normal))
			return false;

		landing = surface;
		FinalizeJumpLandingRoot(ref landing, normal);
		return true;
	}

	private void RefineLandingRootOnRoad(float worldX, float worldZ, ref Vector3 rootPos)
	{
		if (!TryRaycastRoadSurface(worldX, worldZ, out Vector3 surface, out Vector3 normal))
			return;

		rootPos.X = worldX;
		rootPos.Z = worldZ;
		rootPos.Y = surface.Y;
		FinalizeJumpLandingRoot(ref rootPos, normal);
	}

	private void TryApplyJumpLandingImpact()
	{
		if (_player == null || !GodotObject.IsInstanceValid(_player) || !_player.IsInsideTree())
			return;

		var delta = _player.GlobalPosition - GlobalPosition;
		if (new Vector2(delta.X, delta.Z).Length() > HyppyIskuSadeXZ)
			return;
		if (Mathf.Abs(delta.Y) > HyppyIskuSadeY)
			return;

		var pc = _player as PlayerController;
		if (pc != null && pc.IsBlockingEffectiveAgainst(GlobalPosition))
			return;

		var health = _player.GetNodeOrNull<HealthComponent>("HealthComponent");
		if (health == null)
			return;

		int dmg = HyppyIskuVahinko > 0
			? HyppyIskuVahinko
			: Mathf.Max(1, health.MaxHealth / 3);
		health.TakeDamage(dmg);
		pc?.NotifyEnemyEnergyDrainHit();

		if (GetViewport()?.GetCamera3D() is CameraFollow cam)
			cam.ShakeImpulse(0.24f, 0.3f);
	}

	private void FinalizeJumpLandingRoot(ref Vector3 landing, Vector3 surfaceNormal)
	{
		Vector3 n = surfaceNormal.LengthSquared() > 1e-8f ? surfaceNormal.Normalized() : Vector3.Up;
		landing += n * HyppyLaskeutumisNormaaliBias;
		landing.Y -= GetFeetWorldYOffsetFromRoot();
	}

	private float GetFeetWorldYOffsetFromRoot()
	{
		if (_visual == null)
			return 0f;
		// Paikallinen jalka-offset pitää skaalata juuren Basisilla (torni × boss × visual).
		Vector3 worldDelta = GlobalTransform.Basis * new Vector3(0f, _visualFeetLocalY, 0f);
		return worldDelta.Y;
	}

	private void CacheVisualFeetLocalY()
	{
		_visualFeetLocalY = 0f;
		if (_visual != null)
			_visualFeetLocalY = MeasureLowestMeshLocalY(_visual);

		// Skinned mesh AABB voi olla tyhjä — varalla HurtArea-kapselin pohja.
		if (Mathf.Abs(_visualFeetLocalY) < 0.02f && _hurt != null)
		{
			var colNode = _hurt.GetNodeOrNull<CollisionShape3D>("CollisionShape3D");
			if (colNode?.Shape is CapsuleShape3D cap)
				_visualFeetLocalY = colNode.Position.Y - cap.Height * 0.5f;
		}
	}

	private float MeasureLowestMeshLocalY(Node root)
	{
		float minY = float.MaxValue;
		bool any = false;
		Transform3D toBoss = GlobalTransform.AffineInverse();
		foreach (Node n in root.FindChildren("*", "MeshInstance3D", true, false))
		{
			if (n is not MeshInstance3D mesh || mesh.Mesh == null)
				continue;

			Aabb box = mesh.GetAabb();
			Transform3D meshToBoss = toBoss * mesh.GlobalTransform;
			AccumulateAabbMinLocalY(box, meshToBoss, ref minY, ref any);
		}

		return any ? minY : 0f;
	}

	private static void AccumulateAabbMinLocalY(Aabb box, Transform3D xform, ref float minY, ref bool any)
	{
		Vector3 min = box.Position;
		Vector3 max = box.Position + box.Size;
		for (int ix = 0; ix < 2; ix++)
		for (int iy = 0; iy < 2; iy++)
		for (int iz = 0; iz < 2; iz++)
		{
			var corner = new Vector3(
				ix == 0 ? min.X : max.X,
				iy == 0 ? min.Y : max.Y,
				iz == 0 ? min.Z : max.Z);
			float y = (xform * corner).Y;
			if (!any || y < minY)
			{
				minY = y;
				any = true;
			}
		}
	}

	private float ComputeJumpArcHeight(Vector3 start, Vector3 end)
	{
		float horiz = new Vector2(end.X - start.X, end.Z - start.Z).Length();
		float drop = Mathf.Max(0f, start.Y - end.Y);
		return Mathf.Max(HyppyKaarenKorkeusMin, horiz * 0.18f + drop * 0.28f);
	}

	private void ResolveLevel3Nodes()
	{
		_level3Root = null;
		_roundaboutDeck = null;
		_uphillRoad = null;

		if (!IsInsideTree())
			return;

		for (Node n = GetParent(); n != null; n = n.GetParent())
		{
			if (n.Name == "Level3" && n is Node3D d)
			{
				_level3Root = d;
				break;
			}
		}

		_level3Root ??= GetTree()?.CurrentScene as Node3D;
		if (_level3Root == null)
			return;

		if (HasExportNodePath(RoundaboutDeckPolku))
			_roundaboutDeck = _level3Root.GetNodeOrNull<Node3D>(RoundaboutDeckPolku);

		_roundaboutDeck ??= _level3Root.GetNodeOrNull<Node3D>("World/RoundaboutDeck");

		if (HasExportNodePath(YlamaaTiePolku))
			_uphillRoad = _level3Root.GetNodeOrNull<Node3D>(YlamaaTiePolku);

		_uphillRoad ??= _level3Root.GetNodeOrNull<Node3D>("World/UphillRoadRoot/UphillRoad");
	}

	private Node FindRockParent()
	{
		for (Node n = GetParent(); n != null; n = n.GetParent())
		{
			if (n.Name == "Level3")
				return n;
		}
		return GetTree()?.CurrentScene;
	}

	private float NextJumpInterval() =>
		(float)GD.RandRange(HyppyVäliMinSek, HyppyVäliMaxSek);

	private void FacePlayer()
	{
		if (_player == null || !_player.IsInsideTree())
			return;
		FaceHorizontalToward(_player.GlobalPosition.X, _player.GlobalPosition.Z);
	}

	private void FaceHorizontalToward(float worldX, float worldZ)
	{
		var pivot = _facingPivot ?? _visual;
		if (pivot == null || !pivot.IsInsideTree())
			return;

		var to = new Vector3(worldX, pivot.GlobalPosition.Y, worldZ) - pivot.GlobalPosition;
		to.Y = 0f;
		if (to.LengthSquared() < 1e-6f)
			return;

		float yaw = Mathf.Atan2(to.X, to.Z) + Mathf.DegToRad(KatseenKiertoAstetta);
		var rot = pivot.Rotation;
		pivot.Rotation = new Vector3(rot.X, yaw, rot.Z);
	}

	private string ResolveNativeThrowClipName()
	{
		if (_animationPlayer == null)
			return null;

		foreach (var name in new[] { MixamoAnimaatioLähdeNimi, MixamoAnimaatioLähdeNimi.Replace("_", ".") })
		{
			if (_animationPlayer.HasAnimation(name))
				return name;
		}

		foreach (StringName nm in _animationPlayer.GetAnimationList())
		{
			string sn = nm.ToString();
			if (sn.IndexOf("RESET", StringComparison.OrdinalIgnoreCase) >= 0)
				continue;
			if (sn.IndexOf("mixamo", StringComparison.OrdinalIgnoreCase) >= 0)
				return sn;
		}

		foreach (StringName nm in _animationPlayer.GetAnimationList())
		{
			string sn = nm.ToString();
			if (sn.IndexOf("RESET", StringComparison.OrdinalIgnoreCase) >= 0)
				continue;
			return sn;
		}

		return null;
	}

	private void LoadAnim(string path, string sourceName, string targetName, bool loop = false)
	{
		var scene = GD.Load<PackedScene>(path);
		if (scene == null)
		{
			GD.PrintErr($"BossLevel3: animaatiota ei löydy: {path}");
			return;
		}

		var inst = scene.Instantiate();
		var ap = inst.FindChild("AnimationPlayer", true, false) as AnimationPlayer;
		if (ap == null)
		{
			inst.QueueFree();
			return;
		}

		Animation sourceAnim = ResolveAnimationFromPlayer(ap, sourceName, path);
		if (sourceAnim == null)
		{
			inst.QueueFree();
			GD.PrintErr($"BossLevel3: clip '{sourceName}' puuttuu: {path}");
			return;
		}

		var anim = (Animation)sourceAnim.Duplicate();
		inst.QueueFree();

		if (loop)
			anim.LoopMode = Animation.LoopModeEnum.Linear;
		else
			anim.LoopMode = Animation.LoopModeEnum.None;

		var lib = GetOrCreateAnimationLibrary();
		if (lib.HasAnimation(targetName))
			lib.RemoveAnimation(targetName);
		lib.AddAnimation(targetName, anim);
	}

	private AnimationLibrary GetOrCreateAnimationLibrary()
	{
		foreach (StringName libName in _animationPlayer.GetAnimationLibraryList())
			return _animationPlayer.GetAnimationLibrary(libName);

		var lib = new AnimationLibrary();
		_animationPlayer.AddAnimationLibrary("", lib);
		return lib;
	}

	private static Animation ResolveAnimationFromPlayer(AnimationPlayer ap, string sourceName, string pathForLog)
	{
		if (ap == null)
			return null;

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

		GD.PrintErr($"BossLevel3: ei animaatioklippiä: {pathForLog} ({string.Join(", ", ap.GetAnimationList())})");
		return null;
	}

	private void AlignImportedClipsToThrowRig()
	{
		if (_animationPlayer == null || string.IsNullOrEmpty(_activeThrowClip))
			return;
		if (!_animationPlayer.HasAnimation(_activeThrowClip))
			return;

		var template = _animationPlayer.GetAnimation(_activeThrowClip);
		void AlignIfHas(string clipName)
		{
			if (_animationPlayer.HasAnimation(clipName))
				AlignAnimationPathsFromTemplate(template, _animationPlayer.GetAnimation(clipName));
		}

		AlignIfHas(BigJumpClipName);
		AlignIfHas(DeathClipName);
	}

	private void RemapImportedClipBoneIndicesFromSourceFbx(string fbxPackedScenePath, string clipName)
	{
		if (_animationPlayer == null || string.IsNullOrWhiteSpace(fbxPackedScenePath) || string.IsNullOrWhiteSpace(clipName))
			return;
		if (!_animationPlayer.HasAnimation(clipName))
			return;

		var packed = GD.Load<PackedScene>(fbxPackedScenePath);
		if (packed == null)
		{
			GD.PrintErr($"BossLevel3: RemapBoneIndices — FBX ei lataudu: {fbxPackedScenePath}");
			return;
		}

		var inst = packed.Instantiate();
		var srcSkel = FindFirstSkeleton3DDeep(inst);
		var dstSkel = _visual?.FindChild("Skeleton3D", true, false) as Skeleton3D;
		if (srcSkel == null || dstSkel == null)
		{
			GD.PrintErr("BossLevel3: RemapBoneIndices — Skeleton3D puuttuu.");
			inst.QueueFree();
			return;
		}

		var anim = _animationPlayer.GetAnimation(clipName);
		for (int i = 0; i < anim.GetTrackCount(); i++)
		{
			if (anim.TrackGetType(i) != Animation.TrackType.Value)
				continue;
			if (!TrySplitAnimationTrackPath(anim.TrackGetPath(i), out string nodePathPart, out string prop))
				continue;
			if (!TryParseBonesValueProperty(prop, out int srcBoneIdx, out string propTail))
				continue;
			if (srcBoneIdx < 0 || srcBoneIdx >= srcSkel.GetBoneCount())
				continue;

			StringName boneName = srcSkel.GetBoneName(srcBoneIdx);
			int dstIdx = dstSkel.FindBone(boneName);
			if (dstIdx < 0)
				continue;

			string newProp = $"bones/{dstIdx}{propTail}";
			anim.TrackSetPath(i, new NodePath($"{nodePathPart}:{newProp}"));
		}

		inst.QueueFree();
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
		if (template == null || imported == null)
			return;
		if (template.GetTrackCount() == 0 || imported.GetTrackCount() == 0)
			return;
		if (!TryGetRigPathPrefix(template, out string pathPrefix))
			return;

		for (int i = 0; i < imported.GetTrackCount(); i++)
		{
			string p = imported.TrackGetPath(i).ToString();
			int arm = IndexOfRigRoot(p);
			if (arm < 0)
				continue;
			string suffix = p.Substring(arm);
			imported.TrackSetPath(i, new NodePath(pathPrefix + suffix));
		}
	}

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
		if (a >= 0)
			return a;
		return nodePath.IndexOf("Skeleton3D", StringComparison.Ordinal);
	}
}
