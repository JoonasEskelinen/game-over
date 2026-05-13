using System.Collections.Generic;
using Godot;

public partial class EnemyLevel2 : CharacterBody3D
{
	[Export] public float Speed = 3.5f;
	[Export] public float AttackRange = 1.2f;

	/// <summary>
	/// Jahtaessa kun pelaaja on miekka+kilpi-tilassa: vähintään <c>pelaajan Kävelynopeus + tämä</c> (m/s).
	/// Estää tilanteen jossa lisko ei koskaan pääse iskuetäisyydelle jos pelaaja kävelee koko ajan.
	/// </summary>
	[Export] public float ChaseMinimumLeadOverPlayerSwordWalk = 1.15f;

	/// <summary>R1 (3 vahinkoa) tappaa yhdellä iskulla; R2 tarvitsee kaksi osumaa (1+1).</summary>
	[Export] public int Health = 2;

	[Export] public string RunAnimPath   = "res://assets/models/level2_lisko/Running.fbx";
	[Export] public string PunchAnimPath = "res://assets/models/level2_lisko/Punching.fbx";
	[Export] public string DeathAnimPath = "res://assets/models/level2_lisko/Death.fbx";

	/// <summary>Mixamo/Godot -Z etusuunta ei aina täsmää — säädä (tyypillisesti 0 tai 180) jos hahmo juoksee väärinpäin.</summary>
	[Export] public float FaceYawOffsetDegrees = 180f;

	/// <summary>Ihmishahmon osumapisteet — hartia, vatsa, jalat.</summary>
	[Export] public float[] SwordHitProbeHeights = { 0.3f, 0.9f, 1.4f };
	[Export] public float   SwordHitActivationTime = 0.06f;

	/// <summary>
	/// Lisäviive sekunteina vain R1-iskulle (myöhentää osumaa animaatiossa). Lisätään
	/// SwordHitActivationTime:n jälkeen. Esim. 0.06–0.12. R2 käyttää vain SwordHitActivationTime.
	/// </summary>
	[Export] public float RaskasIskuAktivoitumisenLisäviive = 0.4f;

	[Export] public float   HitCenterYOffset = 0.9f;

	/// <summary>R1: ylimääräinen koetuskorkeus (m) teräviivan osumaa varten; 0 = ei käytössä.</summary>
	[Export] public float RaskasIskuLisäKoetuskorkeus = 1.12f;

	/// <summary>R1: max etäisyys teräviivaan (m); laajentaa cleavea. 0 = pelaajan oletus.</summary>
	[Export] public float RaskasIskuLäheisyysYlikirjoitus = 0.72f;

	/// <summary>R1: montako EnemyLevel2:ta voi osua samaan swingiin (cleave).</summary>
	[Export] public int RaskasIskuCleaveKohteet = 2;

	[Export] public float PunchDamageWindupSeconds  = 0.35f;
	[Export] public float PunchDamageMinAttackPhase = 0.38f;

	/// <summary>
	/// Metriä lisättynä <see cref="AttackRange"/>:iin kun ollaan jo lähitaistelussa — estää iskun katkeamisen
	/// pienestä liikkeestä ja auttaa useaa vihollista pysymään iskutilassa (ei “juokse/run”-vaihtoa reunalla).
	/// </summary>
	[Export] public float MeleeExitSlack = 0.55f;

	[Export] public float DeathTiltDuration  = 0.32f;
	[Export] public float DeathSlideDuration = 0.24f;
	[Export] public float DeathShrinkDuration = 0.52f;

	private Node3D          _player;
	private PlayerController _playerController;
	private AnimationPlayer  _animationPlayer;
	private CameraFollow     _camera;

	private float _punchTimer;
	private float _punchInterval = 2.0f;
	private bool  _wasInMeleeRange;
	private bool  _isDead = false;
	private bool  _hasBeenHitThisSwing = false;

	private const float Gravity = 20f;

	public override void _Ready()
	{
		FloorSnapLength = 0.18f;
		FloorMaxAngle   = Mathf.DegToRad(50f);

		_player           = GetTree().GetFirstNodeInGroup("player") as Node3D;
		_playerController = _player as PlayerController;
		_animationPlayer  = FindChild("AnimationPlayer", true, false) as AnimationPlayer;

		if (_animationPlayer != null)
		{
			LoadAnim(RunAnimPath,   "mixamo_com", "run",   loop: true);
			LoadAnim(PunchAnimPath, "mixamo_com", "punch", loop: true);
			LoadAnim(DeathAnimPath, "mixamo_com", "death", loop: false);

			_animationPlayer.Play("run");

			var lib = _animationPlayer.GetAnimationLibrary("");
			if (lib != null && lib.HasAnimation("punch"))
			{
				float len = lib.GetAnimation("punch").Length;
				if (len > 0.05f) _punchInterval = len;
			}
		}

		AddToGroup("enemy_level2");

		// Vihollinen aina pelaajaa kohti — käännetään heti oikeaan suuntaan
		FacePlayer();
	}

	/// <summary>Käytössä EnemyLevel2Spawner (max elossa kerrallaan).</summary>
	public bool IsAliveForSpawner() => !_isDead && IsInsideTree();

	/// <summary>XZ-jahtausnopeus: miekka/kilpi -tilassa vähintään pelaajan kävely + lead, muuten <see cref="Speed"/>.</summary>
	private float GetChasePlanarSpeed()
	{
		if (_playerController == null || !_playerController.IsSwordWeaponMode())
			return Speed;
		float floor = _playerController.Kävelynopeus + ChaseMinimumLeadOverPlayerSwordWalk;
		return Mathf.Max(Speed, floor);
	}

	public override void _PhysicsProcess(double delta)
	{
		if (_isDead || !IsInsideTree()) return;
		if (_player == null || !GodotObject.IsInstanceValid(_player) || !_player.IsInsideTree()) return;

		float dt = (float)delta;
		Vector3 velocity = Velocity;

		if (!IsOnFloor())
			velocity.Y -= Gravity * dt;

		float planarDist = PlanarDistTo(_player.GlobalPosition);
		float heightDiff = Mathf.Abs(_player.GlobalPosition.Y - GlobalPosition.Y);
		bool heightOk = heightDiff <= 1.9f;
		// Hysteresis: sisään AttackRange, ulos vasta kun ylitetään AttackRange + slack (ei katkaise iskua millimetrillä).
		float meleeEnter = AttackRange;
		float meleeStay = AttackRange + Mathf.Max(0f, MeleeExitSlack);
		bool inMeleeRange = heightOk && (_wasInMeleeRange ? planarDist <= meleeStay : planarDist <= meleeEnter);

		if (inMeleeRange && !_wasInMeleeRange)
		{
			float maxWindup = Mathf.Max(0.55f, _punchInterval * 0.92f);
			_punchTimer = Mathf.Clamp(PunchDamageWindupSeconds, 0.2f, maxWindup);
		}
		_wasInMeleeRange = inMeleeRange;

		if (!inMeleeRange)
		{
			// Liikkuu XZ-tasossa pelaajaa kohti (tukee satunnaisia Z-spawn-positioita)
			var toPlayer = _player.GlobalPosition - GlobalPosition;
			toPlayer.Y = 0f;
			if (toPlayer.LengthSquared() > 0.01f)
			{
				var dir = toPlayer.Normalized();
				float chase = GetChasePlanarSpeed();
				velocity.X = dir.X * chase;
				velocity.Z = dir.Z * chase;
			}
			FacePlayer();

			if (_animationPlayer?.CurrentAnimation != "run")
				_animationPlayer?.Play("run");
		}
		else
		{
			velocity.X = 0f;
			velocity.Z = 0f;
			FacePlayer();

			if (_animationPlayer?.CurrentAnimation != "punch")
				_animationPlayer?.Play("punch");

			_punchTimer -= dt;
			if (_punchTimer <= 0f && CanPunchByAnimPhase())
			{
				TryApplyPunchDamage();
				_punchTimer = _punchInterval;
			}
		}

		// Miekan osuman tarkistus — R1: cleave (useampi vihollinen / swing), hieman laajempi läheisyys
		if (_playerController != null && _playerController.IsMeleeAttackActive())
		{
			float animTime = _playerController.GetAttackAnimationTime();
			float hitFrom  = _playerController.GetMeleeStrikeWindowStart() + SwordHitActivationTime;
			if (_playerController.IsHeavyMeleeAttackActive() && RaskasIskuAktivoitumisenLisäviive > 0f)
				hitFrom += RaskasIskuAktivoitumisenLisäviive;

			if (animTime >= hitFrom && !_hasBeenHitThisSwing)
			{
				bool heavy = _playerController.IsHeavyMeleeAttackActive();
				var heights = SwordHitProbeHeights ?? new[] { HitCenterYOffset };
				int extra = heavy && RaskasIskuLisäKoetuskorkeus > 0.01f ? 1 : 0;
				float proxOverride = heavy && RaskasIskuLäheisyysYlikirjoitus > 0f
					? RaskasIskuLäheisyysYlikirjoitus
					: -1f;

				for (int pi = 0; pi < heights.Length + extra; pi++)
				{
					float h = pi < heights.Length ? heights[pi] : RaskasIskuLisäKoetuskorkeus;
					Vector3 p = GlobalPosition + Vector3.Up * h;
					bool can = proxOverride >= 0f
						? _playerController.CanApplyMeleeHitAtWorldPoint(p, proxOverride)
						: _playerController.CanApplyMeleeHitAtWorldPoint(p);
					if (!can)
						continue;

					bool claimed = heavy
						? _playerController.TryClaimEnemyHeavyCleaveHit(Mathf.Max(1, RaskasIskuCleaveKohteet))
						: _playerController.TryClaimEnemyMeleeHit();
					if (!claimed)
						break;

					TakeDamage(_playerController.GetMeleeAttackDamage());
					_playerController.NotifyMeleeHitLanded();
					_hasBeenHitThisSwing = true;
					break;
				}
			}
		}
		else
		{
			_hasBeenHitThisSwing = false;
			_playerController?.ClearEnemyHitThisSwing();
		}

		Velocity = velocity;
		MoveAndSlide();
	}

	// --------------- Apumetodit ---------------

	private void FacePlayer()
	{
		if (_player == null || !_player.IsInsideTree() || !IsInsideTree()) return;
		var to = _player.GlobalPosition - GlobalPosition;
		to.Y = 0f;
		if (to.LengthSquared() < 1e-6f) return;
		LookAt(GlobalPosition + to.Normalized() * 3f, Vector3.Up);
		RotateY(Mathf.DegToRad(FaceYawOffsetDegrees));
	}

	private bool CanPunchByAnimPhase()
	{
		if (_animationPlayer == null || PunchDamageMinAttackPhase <= 0.01f) return true;
		if (_animationPlayer.CurrentAnimation != "punch") return false;
		double len = _animationPlayer.CurrentAnimationLength;
		if (len <= 0.02) return true;
		float phase = (float)(_animationPlayer.CurrentAnimationPosition / len);
		return phase >= PunchDamageMinAttackPhase;
	}

	private void TryApplyPunchDamage()
	{
		if (_playerController == null || _player == null || !_player.IsInsideTree()) return;

		// Torjuntatarkistus: sama logiikka kuin EnemyLevel1 susella (hahmon -Basis.Z = suunta pelaaja liikkuessa).
		// Uhkapiste liskon ja pelaajan välissä — epäonnistuu vain jos pelaaja kääntyy poispäin.
		Vector3 threat = GlobalPosition.Lerp(_player.GlobalPosition, 0.35f);
		// Kilven kartio: hahmon +Z vastaa “eteen” liskon + sivukameran tilanteessa (-Z antoi torjunnan vain selin).
		if (_playerController.IsBlockingEffectiveAgainst(threat, -1f, flipShieldFacing180: true))
		{
			GD.Print("EnemyLevel2: isku torjuttu kilpellä!");
			_playerController.NotifyBossLevel1StrikeBlocked();
			return;
		}

		var health = _player.GetNodeOrNull<HealthComponent>("HealthComponent");
		if (health == null) { GD.PrintErr("HealthComponent puuttuu!"); return; }

		int dmg = Mathf.Max(1, health.MaxHealth / 3);
		health.TakeDamage(dmg);
	}

	private float PlanarDistTo(Vector3 target)
	{
		float dx = target.X - GlobalPosition.X;
		float dz = target.Z - GlobalPosition.Z;
		return Mathf.Sqrt(dx * dx + dz * dz);
	}

	private CameraFollow GetOrFindCamera()
	{
		if (_camera != null && GodotObject.IsInstanceValid(_camera)) return _camera;
		_camera = GetViewport()?.GetCamera3D() as CameraFollow;
		return _camera;
	}

	private void OnSwordHitFeedback()
	{
		GetOrFindCamera()?.ShakeImpulse(0.20f, 0.24f);

		var geos = new List<GeometryInstance3D>();
		CollectGeometryInstances(this, geos);
		if (geos.Count > 0)
		{
			var flashMat = new StandardMaterial3D
			{
				ShadingMode              = BaseMaterial3D.ShadingModeEnum.Unshaded,
				AlbedoColor              = new Color(1f, 0.82f, 0.82f),
				EmissionEnabled          = true,
				Emission                 = new Color(1f, 0.35f, 0.35f),
				EmissionEnergyMultiplier = 2.2f,
			};
			foreach (var g in geos)
				if (GodotObject.IsInstanceValid(g)) g.MaterialOverride = flashMat;

			var t = CreateTween();
			t.TweenInterval(0.05f);
			t.TweenCallback(Callable.From(() =>
			{
				foreach (var g in geos)
					if (GodotObject.IsInstanceValid(g)) g.MaterialOverride = null;
			}));
		}

		if (_animationPlayer != null && GodotObject.IsInstanceValid(_animationPlayer))
		{
			_animationPlayer.SpeedScale = 0f;
			var t = CreateTween();
			t.TweenInterval(0.06f);
			t.TweenCallback(Callable.From(() =>
			{
				if (GodotObject.IsInstanceValid(_animationPlayer))
					_animationPlayer.SpeedScale = 1f;
			}));
		}
	}

	public void TakeDamage(int amount)
	{
		if (_isDead) return;
		Health -= amount;
		OnSwordHitFeedback();
		if (Health <= 0) Die();
	}

	private void Die()
	{
		_isDead  = true;
		Velocity = Vector3.Zero;

		var col = GetNodeOrNull<CollisionShape3D>("CollisionShape3D");
		if (col != null) col.Disabled = true;

		if (_animationPlayer != null && GodotObject.IsInstanceValid(_animationPlayer))
			_animationPlayer.Play("death");

		Node3D visual = GetNodeOrNull<Node3D>("Run") ?? (Node3D)this;

		Vector3 away = Vector3.Right;
		if (_player != null && GodotObject.IsInstanceValid(_player) && _player.IsInsideTree())
		{
			away = GlobalPosition - _player.GlobalPosition;
			away.Y = 0f;
			if (away.LengthSquared() > 1e-5f) away = away.Normalized();
		}

		Engine.TimeScale = 0.15f;
		if (IsInsideTree())
		{
			var slowTimer = GetTree().CreateTimer(0.07f, processInPhysics: false, ignoreTimeScale: true);
			slowTimer.Timeout += () => { if (Engine.TimeScale < 1f) Engine.TimeScale = 1f; };
		}

		float rollY = (float)GD.RandRange(-38.0, 38.0);
		float rollZ = (float)GD.RandRange(-28.0, 28.0);
		Vector3 tilt    = visual.RotationDegrees + new Vector3(86f, rollY, rollZ);
		Vector3 basePos = visual.IsInsideTree() ? visual.GlobalPosition : visual.Position;
		Vector3 kickPos = basePos  + new Vector3(away.X * 0.45f,  0.10f, away.Z * 0.45f);
		Vector3 slidePos = kickPos + new Vector3(away.X * 0.10f, -0.38f, away.Z * 0.10f);

		var tween = CreateTween();
		tween.SetParallel(true);
		tween.TweenProperty(visual, "scale",            new Vector3(1.13f, 1.13f, 1.13f), 0.05f)
			.SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
		tween.TweenProperty(visual, "rotation_degrees", tilt,    DeathTiltDuration)
			.SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
		tween.TweenProperty(visual, "global_position",  kickPos, 0.07f)
			.SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
		tween.SetParallel(false);

		tween.TweenProperty(visual, "global_position", slidePos, DeathSlideDuration)
			.SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.In);

		var geos = new List<GeometryInstance3D>();
		CollectGeometryInstances(visual, geos);
		if (geos.Count > 0)
		{
			tween.SetParallel(true);
			tween.TweenProperty(visual, "scale", Vector3.Zero, DeathShrinkDuration)
				.SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.In);
			tween.TweenMethod(
				Callable.From<float>(a =>
				{
					foreach (var g in geos)
						if (GodotObject.IsInstanceValid(g)) g.Transparency = a;
				}),
				0f, 1f, DeathShrinkDuration);
			tween.SetParallel(false);
		}
		else
		{
			tween.TweenProperty(visual, "scale", Vector3.Zero, DeathShrinkDuration)
				.SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.In);
		}

		tween.TweenCallback(Callable.From(() => QueueFree()));
	}

	private static void CollectGeometryInstances(Node node, List<GeometryInstance3D> list)
	{
		if (node is GeometryInstance3D gi) list.Add(gi);
		foreach (Node child in node.GetChildren())
			CollectGeometryInstances(child, list);
	}

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

		var animList = ap.GetAnimationList();
		if (anim == null && animList.Length > 0)
		{
			var first = animList[0];
			anim = ap.GetAnimation(first);
			GD.Print($"EnemyLevel2: käytetään ensimmäistä animaatiota '{first}' tiedostossa {path} (ei löytynyt '{sourceName}')");
		}

		if (anim == null)
		{
			GD.PrintErr($"Animaatiota '{sourceName}' ei löydy tiedostosta: {path}");
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
