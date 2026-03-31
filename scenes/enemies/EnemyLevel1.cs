using System.Collections.Generic;
using Godot;

public partial class EnemyLevel1 : CharacterBody3D
{
	[Export] public float Speed = 3.0f;
	/// <summary>XZ-etäisyys pelaajaan, jolloin ZombieNeckBite alkaa (pienempi = pitää päästä lähemmäs). Jos jää vain juoksuun, nosta hieman.</summary>
	[Export] public float AttackRange = 1.12f;
	/// <summary>R2 tekee 1 ja R1 3 vahinkoa → 3×R2 tai 1×R1 kuolettaa oletuksella.</summary>
	[Export] public int Health = 3;
	[Export] public string AttackAnimPath = "res://assets/models/level1_susi/susiWithoutskin/ZombieNeckBite.fbx";

	/// <summary>Miekan iskulinjan sallittu etäisyys vihollisen osumapisteisiin (metriä).</summary>
	[Export] public float SwordHitRange = 2.65f;

	/// <summary>Pääosuma-akselin korkeus GlobalPositionista (nelijalkainen: rintakehä).</summary>
	[Export] public float HitCenterYOffset = 0.68f;

	/// <summary>Lisäotantakorkeudet (metriä) — yksi piste helposti “ohittaa” terän; tyhjä = vain HitCenterYOffset.</summary>
	[Export] public float[] SwordHitProbeHeights = { 0.38f, 0.68f, 0.95f };

	/// <summary>Lisäviive sekunteina GetMeleeStrikeWindowStart()-ajan päälle (säätö).</summary>
	[Export] public float SwordHitActivationTime = 0f;

	[Export] public float DeathTiltDuration = 0.32f;
	[Export] public float DeathSlideDuration = 0.24f;
	[Export] public float DeathShrinkDuration = 0.52f;

	/// <summary>
	/// Sekuntia purema-animaation alusta ennen ensimmäistä vahinkoa (puree "osuu" eikä heti kun anim käynnistyy).
	/// </summary>
	[Export] public float BiteDamageWindupSeconds = 0.58f;

	/// <summary>
	/// Puremavaurio vain kun hyökkäysanimaatio on edennyt vähintään näin paljon (0–1). Estää vahingon animaation alkuosassa.
	/// </summary>
	[Export] public float BiteDamageMinAttackPhase = 0.5f;

	private Node3D _player;
	private PlayerController _playerController;
	private AnimationPlayer _animationPlayer;
	private float _biteTimer;
	private float _biteInterval = 2.5f;
	private bool _wasInMeleeRange;
	private bool _isDead = false;
	private const float Gravity = 20f;
	private bool _hasBeenHitThisSwing = false;
	private AudioStreamPlayer _biteSFX;

	public override void _Ready()
	{
		FloorSnapLength = 0.18f;
		FloorMaxAngle = Mathf.DegToRad(50f);

		_player = GetTree().GetFirstNodeInGroup("player") as Node3D;
		_playerController = _player as PlayerController;
		_animationPlayer = FindChild("AnimationPlayer", true, false) as AnimationPlayer;
		_biteSFX = GetNodeOrNull<AudioStreamPlayer>("BiteSFX");

		if (_animationPlayer != null)
		{
			LoadAnim(AttackAnimPath, "mixamo_com", "attack", loop: true);
			_animationPlayer.Play("mixamo_com");

			var lib = _animationPlayer.GetAnimationLibrary("");
			if (lib != null && lib.HasAnimation("attack"))
			{
				float len = lib.GetAnimation("attack").Length;
				if (len > 0.05f)
					_biteInterval = len;
			}
		}
	}

	public override void _PhysicsProcess(double delta)
	{
		if (_isDead || !IsInsideTree()) return;
		if (_player == null || !GodotObject.IsInstanceValid(_player) || !_player.IsInsideTree()) return;

		float dt = (float)delta;
		Vector3 velocity = Velocity;

		if (!IsOnFloor())
			velocity.Y -= Gravity * dt;

		float planarDist = PlanarDistanceTo(_player.GlobalPosition);
		float heightDiff = Mathf.Abs(_player.GlobalPosition.Y - GlobalPosition.Y);
		bool inMeleeRange = planarDist <= AttackRange && heightDiff <= 1.6f;

		if (inMeleeRange && !_wasInMeleeRange)
		{
			float maxWindup = Mathf.Max(0.55f, _biteInterval * 0.92f);
			float windup = Mathf.Clamp(BiteDamageWindupSeconds, 0.2f, maxWindup);
			_biteTimer = windup;
		}

		_wasInMeleeRange = inMeleeRange;

		if (!inMeleeRange)
		{
			Vector3 direction = (_player.GlobalPosition - GlobalPosition).Normalized();
			direction.Y = 0f;
			velocity.X = direction.X * Speed;
			velocity.Z = direction.Z * Speed;
			TurnTowardsPlayer();

			if (_animationPlayer != null && _animationPlayer.CurrentAnimation != "mixamo_com")
				_animationPlayer.Play("mixamo_com");
		}
		else
		{
			velocity.X = 0f;
			velocity.Z = 0f;
			TurnTowardsPlayer();

			if (_animationPlayer != null && _animationPlayer.CurrentAnimation != "attack")
			{
				_animationPlayer.Play("attack");
				_biteSFX?.Play();
			}

			_biteTimer -= dt;
			if (_biteTimer <= 0f && CanApplyBiteDamageByAnimPhase())
			{
				TryApplyBiteDamage();
				_biteTimer = _biteInterval;
			}
		}

		// Miekan osuman tarkistus (R2 = lyhyt animaatio → ikkuna suhteessa pituuteen; R2-liipasin = reunatunnistus PlayerControllerissa)
		if (_playerController != null && _playerController.IsMeleeAttackActive())
		{
			float animTime = _playerController.GetAttackAnimationTime();
			float hitFrom = _playerController.GetMeleeStrikeWindowStart() + SwordHitActivationTime;
			if (animTime >= hitFrom && !_hasBeenHitThisSwing)
			{
				Vector3 bodyBase = GlobalPosition;
				Vector3 arcRef = bodyBase + Vector3.Up * HitCenterYOffset;
				bool inHitCone = _playerController.IsPointInMeleeHitFacingArc(arcRef)
					|| _playerController.IsPointInMeleeHitBladeArc(arcRef);
				if (inHitCone)
				{
					var probes = SwordHitProbeHeights;
					if (probes == null || probes.Length == 0)
						probes = new[] { HitCenterYOffset };

					for (int i = 0; i < probes.Length; i++)
					{
						Vector3 p = bodyBase + Vector3.Up * probes[i];
						if (_playerController.GetMeleeHitDistanceToPoint(p) < SwordHitRange)
						{
							TakeDamage(_playerController.GetMeleeAttackDamage());
							_playerController.NotifyMeleeHitLanded();
							_hasBeenHitThisSwing = true;
							break;
						}
					}
				}
			}
		}
		else
		{
			// Lyonti loppui - nollataan
			_hasBeenHitThisSwing = false;
		}

		Velocity = velocity;
		MoveAndSlide();
	}

	private bool CanApplyBiteDamageByAnimPhase()
	{
		if (_animationPlayer == null || BiteDamageMinAttackPhase <= 0.01f)
			return true;
		if (_animationPlayer.CurrentAnimation != "attack")
			return false;
		double len = _animationPlayer.CurrentAnimationLength;
		if (len <= 0.02)
			return true;
		float phase = (float)(_animationPlayer.CurrentAnimationPosition / len);
		return phase >= BiteDamageMinAttackPhase;
	}

	private void TurnTowardsPlayer()
	{
		if (_player == null || !_player.IsInsideTree() || !IsInsideTree()) return;
		var lookTarget = _player.GlobalPosition with { Y = GlobalPosition.Y };
		if (GlobalPosition.DistanceTo(lookTarget) > 0.01f)
		{
			LookAt(lookTarget, Vector3.Up);
			RotateY(Mathf.Pi);
		}
	}

	private void TryApplyBiteDamage()
	{
		if (_playerController == null || _player == null || !_player.IsInsideTree()) return;

		if (_playerController.IsBlockingEffectiveAgainst(GlobalPosition))
		{
			GD.Print("Isku torjuttu kilpella!");
			return;
		}

		var health = _player.GetNodeOrNull<HealthComponent>("HealthComponent");
		if (health == null) { GD.PrintErr("HealthComponent puuttuu!"); return; }

		// Yksi pureman loopin vahinko ≈ kolmannes max-HP:stä (3 HP → 1 per animaatio).
		int biteDamage = Mathf.Max(1, health.MaxHealth / 3);
		health.TakeDamage(biteDamage);
	}

	private static float PlanarDistanceTo(Vector3 from, Vector3 to)
	{
		float dx = to.X - from.X;
		float dz = to.Z - from.Z;
		return Mathf.Sqrt(dx * dx + dz * dz);
	}

	private float PlanarDistanceTo(Vector3 targetGlobal)
		=> PlanarDistanceTo(GlobalPosition, targetGlobal);

	public void TakeDamage(int amount)
	{
		if (_isDead) return;
		Health -= amount;
		if (Health <= 0) Die();
	}

	private void Die()
	{
		_isDead = true;
		Velocity = Vector3.Zero;

		var col = GetNodeOrNull<CollisionShape3D>("CollisionShape3D");
		if (col != null) col.Disabled = true;

		Node3D visual = GetNodeOrNull<Node3D>("Run") ?? (Node3D)this;

		Vector3 away = new Vector3(1f, 0f, 0f);
		if (_player != null && GodotObject.IsInstanceValid(_player) && _player.IsInsideTree())
		{
			away = GlobalPosition - _player.GlobalPosition;
			away.Y = 0f;
			if (away.LengthSquared() > 1e-5f)
				away = away.Normalized();
		}

		float rollY = (float)GD.RandRange(-38.0, 38.0);
		float rollZ = (float)GD.RandRange(-28.0, 28.0);
		Vector3 tilt = visual.RotationDegrees + new Vector3(86f, rollY, rollZ);
		Vector3 slide = visual.IsInsideTree()
			? visual.GlobalPosition + new Vector3(away.X * 0.28f, -0.22f, away.Z * 0.28f)
			: visual.Position + new Vector3(away.X * 0.28f, -0.22f, away.Z * 0.28f);

		var tween = CreateTween();
		tween.TweenProperty(visual, "rotation_degrees", tilt, DeathTiltDuration)
			.SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
		tween.TweenProperty(visual, "global_position", slide, DeathSlideDuration)
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
					{
						if (GodotObject.IsInstanceValid(g))
							g.Transparency = a;
					}
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
		if (node is GeometryInstance3D gi)
			list.Add(gi);
		foreach (Node child in node.GetChildren())
			CollectGeometryInstances(child, list);
	}

	private void LoadAnim(string path, string sourceName, string targetName, bool loop = false)
	{
		var scene = GD.Load<PackedScene>(path);
		if (scene == null) { GD.PrintErr($"Animaatiota ei loydy: {path}"); return; }
		var inst = scene.Instantiate();
		var ap = inst.FindChild("AnimationPlayer", true, false) as AnimationPlayer;
		if (ap == null) { inst.QueueFree(); return; }

		Animation anim = null;
		foreach (var name in new[] { sourceName, sourceName.Replace("_", ".") })
			if (ap.HasAnimation(name)) { anim = ap.GetAnimation(name); break; }

		if (anim == null)
		{
			GD.PrintErr($"Animaatiota '{sourceName}' ei loydy: {path}");
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

		if (loop)
			anim.LoopMode = Animation.LoopModeEnum.Linear;

		if (lib.HasAnimation(targetName)) lib.RemoveAnimation(targetName);
		lib.AddAnimation(targetName, anim);
		inst.QueueFree();
	}
}
