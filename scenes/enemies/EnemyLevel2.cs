using System.Collections.Generic;
using Godot;

public partial class EnemyLevel2 : CharacterBody3D
{
	[Export] public float Speed = 3.5f;
	[Export] public float AttackRange = 1.2f;
	[Export] public int Health = 2;

	[Export] public string RunAnimPath    = "res://assets/models/animations/Running.fbx";
	[Export] public string PunchAnimPath  = "res://assets/models/animations/Punching.fbx";
	[Export] public string DeathAnimPath  = "res://assets/models/animations/Death.fbx";

	/// <summary>Ihmishahmon osumapisteet — hartia, vatsa, jalat.</summary>
	[Export] public float[] SwordHitProbeHeights = { 0.3f, 0.9f, 1.4f };
	[Export] public float   SwordHitActivationTime = 0f;
	[Export] public float   HitCenterYOffset = 0.9f;

	[Export] public float PunchDamageWindupSeconds  = 0.35f;
	[Export] public float PunchDamageMinAttackPhase = 0.38f;

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

		// Vihollinen aina pelaajaa kohti — käännetään heti oikeaan suuntaan
		FacePlayer();
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
		bool inMeleeRange = planarDist <= AttackRange && heightDiff <= 1.9f;

		if (inMeleeRange && !_wasInMeleeRange)
		{
			float maxWindup = Mathf.Max(0.55f, _punchInterval * 0.92f);
			_punchTimer = Mathf.Clamp(PunchDamageWindupSeconds, 0.2f, maxWindup);
		}
		_wasInMeleeRange = inMeleeRange;

		if (!inMeleeRange)
		{
			// Liikkuu VAIN X-akselilla — tulee suoraan edestäpäin putkessa
			float dirX = Mathf.Sign(_player.GlobalPosition.X - GlobalPosition.X);
			velocity.X = dirX * Speed;
			velocity.Z = 0f;
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

		// Miekan osuman tarkistus
		if (_playerController != null && _playerController.IsMeleeAttackActive())
		{
			float animTime = _playerController.GetAttackAnimationTime();
			float hitFrom  = _playerController.GetMeleeStrikeWindowStart() + SwordHitActivationTime;

			if (animTime >= hitFrom && !_hasBeenHitThisSwing)
			{
				var probes = SwordHitProbeHeights ?? new[] { HitCenterYOffset };
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
		// Putkessa riittää X-suunnan kääntö — ei tarvita LookAt-temppuja
		float dirX = _player.GlobalPosition.X - GlobalPosition.X;
		RotationDegrees = new Vector3(0f, dirX >= 0f ? 270f : 90f, 0f);
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

		if (_playerController.IsBlockingEffectiveAgainst(GlobalPosition))
		{
			GD.Print("Isku torjuttu kilpellä!");
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
				ShadingMode             = BaseMaterial3D.ShadingModeEnum.Unshaded,
				AlbedoColor             = new Color(1f, 0.82f, 0.82f),
				EmissionEnabled         = true,
				Emission                = new Color(1f, 0.35f, 0.35f),
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

		// Soita death-animaatio jos löytyy
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
