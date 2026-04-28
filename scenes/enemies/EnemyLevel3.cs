using System.Collections.Generic;
using Godot;

/// <summary>
/// Level 3 käärme: iskuetäisyydellä pysähtyy, toistaa purema-/iskuanimaation ja vähentää pelaajan HP:tä (energiaa) animaation vaiheeseen synkassa.
/// Ilman AnimationPlayeria käyttää vanhaa kosketusvahinkoa cooldownilla.
/// </summary>
public partial class EnemyLevel3 : CharacterBody3D
{
	[Export] public float Speed = 3.8f;
	[Export] public int Health = 1;

	/// <summary>XZ-etäisyys jolloin käärme pysähtyy isku-/imemistilaan (animaatio + vahinko).</summary>
	[Export] public float AttackRange = 0.96f;

	/// <summary>Pystytoleranssi iskutilaan (m).</summary>
	[Export] public float StrikeMaxHeightDelta = 1.9f;

	/// <summary>Planetaarinen etäisyys jolla katsotaan pelaajan ja käärmeen koskevan (fallback ilman animaatiota).</summary>
	[Export] public float ContactReachMeters = 0.84f;

	/// <summary>Pystysuoran toleranssi (m), jotta mäessä osuus rekisteröityy (fallback).</summary>
	[Export] public float ContactMaxHeightDelta = 1.5f;

	[Export] public float ContactDamageCooldownSeconds = 0.55f;

	/// <summary>Sekuntia iskun alusta ennen ensimmäistä HP-vähennystä (kun animaatio käytössä).</summary>
	[Export] public float DrainDamageWindupSeconds = 0.36f;

	/// <summary>HP-vähennys vain kun iskuanimaatio on edennyt vähintään näin paljon (0–1).</summary>
	[Export] public float DrainDamageMinAttackPhase = 0.38f;

	/// <summary>Animaation nimi AnimationPlayerissa (tyhjä = ensimmäinen clip listasta).</summary>
	[Export] public string StrikeAnimationName = "";

	/// <summary>Liikkumisanimaatio jahtaamisessa; tyhjä = jokin muu kuin iskuanimaatio tai ei vaihtoa.</summary>
	[Export] public string MoveAnimationName = "";

	[Export] public float FaceYawOffsetDegrees = 180f;

	[Export] public float[] SwordHitProbeHeights = { 0.1f, 0.25f, 0.45f, 0.62f, 0.82f };

	/// <summary>Kevyen iskun max etäisyys miekkaan (m). Matalalle käärmeelle helpompi osuma.</summary>
	[Export] public float LightMeleeProximityOverride = 1.12f;

	[Export] public float SwordHitActivationTime = 0.06f;
	[Export] public float RaskasIskuAktivoitumisenLisäviive = 0.4f;
	[Export] public float HitCenterYOffset = 0.45f;
	[Export] public float RaskasIskuLisäKoetuskorkeus = 0.85f;
	[Export] public float RaskasIskuLäheisyysYlikirjoitus = 0.85f;
	[Export] public int RaskasIskuCleaveKohteet = 2;

	[Export] public float DeathTiltDuration = 0.28f;
	[Export] public float DeathSlideDuration = 0.2f;
	[Export] public float DeathShrinkDuration = 0.45f;

	private Node3D _player;
	private PlayerController _playerController;
	private AnimationPlayer _animationPlayer;

	private string _strikeClip = "";
	private string _moveClip = "";
	private float _drainTimer;
	private float _drainInterval = 1.85f;
	private bool _wasInStrikeRange;
	private bool _useStrikeSequence;

	private float _contactCooldown;
	private bool _isDead;
	private bool _hasBeenHitThisSwing;

	private const float Gravity = 20f;

	public override void _Ready()
	{
		FloorSnapLength = 0.22f;
		FloorMaxAngle = Mathf.DegToRad(52f);

		_player = GetTree().GetFirstNodeInGroup("player") as Node3D;
		_playerController = _player as PlayerController;

		_contactCooldown = 0f;

		_animationPlayer = FindChild("AnimationPlayer", true, false) as AnimationPlayer;
		ResolveAnimationClips();

		AddToGroup("enemy_level3");
		FacePlayer();
	}

	public bool IsAliveForSpawner() => !_isDead && IsInsideTree();

	public override void _PhysicsProcess(double delta)
	{
		if (_isDead || !IsInsideTree()) return;
		if (_player == null || !GodotObject.IsInstanceValid(_player) || !_player.IsInsideTree()) return;

		float dt = (float)delta;
		_contactCooldown = Mathf.Max(0f, _contactCooldown - dt);
		Vector3 velocity = Velocity;

		if (!IsOnFloor())
			velocity.Y -= Gravity * dt;

		float planarToPlayer = PlanarDistTo(_player.GlobalPosition);
		float heightDiff = Mathf.Abs(_player.GlobalPosition.Y - GlobalPosition.Y);
		bool inStrikeRange = planarToPlayer <= AttackRange && heightDiff <= StrikeMaxHeightDelta;

		if (inStrikeRange && !_wasInStrikeRange)
		{
			float maxWind = Mathf.Max(0.55f, _drainInterval * 0.92f);
			float wind = Mathf.Clamp(DrainDamageWindupSeconds, 0.18f, maxWind);
			_drainTimer = wind;
		}

		_wasInStrikeRange = inStrikeRange;

		if (_useStrikeSequence)
		{
			if (!inStrikeRange)
			{
				var toPlayer = _player.GlobalPosition - GlobalPosition;
				toPlayer.Y = 0f;
				if (toPlayer.LengthSquared() > 0.01f)
				{
					var dir = toPlayer.Normalized();
					velocity.X = dir.X * Speed;
					velocity.Z = dir.Z * Speed;
				}

				string chaseClip = !string.IsNullOrEmpty(_moveClip) ? _moveClip : _strikeClip;
				if (_animationPlayer != null && !string.IsNullOrEmpty(chaseClip)
				    && _animationPlayer.CurrentAnimation != chaseClip)
					_animationPlayer.Play(chaseClip);
			}
			else
			{
				velocity.X = 0f;
				velocity.Z = 0f;

				if (_animationPlayer != null && !string.IsNullOrEmpty(_strikeClip)
				    && _animationPlayer.CurrentAnimation != _strikeClip)
					_animationPlayer.Play(_strikeClip);

				_drainTimer -= dt;
				if (_drainTimer <= 0f && CanApplyDrainDamageByAnimPhase())
				{
					TryApplyDrainDamage();
					_drainTimer = _drainInterval;
				}
			}

			FacePlayer();
		}
		else
		{
			var toPlayer = _player.GlobalPosition - GlobalPosition;
			toPlayer.Y = 0f;
			if (toPlayer.LengthSquared() > 0.01f)
			{
				var dir = toPlayer.Normalized();
				velocity.X = dir.X * Speed;
				velocity.Z = dir.Z * Speed;
			}
			FacePlayer();
		}

		if (_playerController != null && _playerController.IsMeleeAttackActive())
		{
			float animTime = _playerController.GetAttackAnimationTime();
			float hitFrom = _playerController.GetMeleeStrikeWindowStart() + SwordHitActivationTime;
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
					float proxForHit = heavy
						? proxOverride
						: (LightMeleeProximityOverride > 0f ? LightMeleeProximityOverride : -1f);
					bool can = proxForHit >= 0f
						? _playerController.CanApplyMeleeHitAtWorldPoint(p, proxForHit)
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

		if (!_useStrikeSequence)
			TryApplyContactDamage();
	}

	private void ResolveAnimationClips()
	{
		_useStrikeSequence = false;
		_strikeClip = "";
		_moveClip = "";

		if (_animationPlayer == null)
			return;

		var list = _animationPlayer.GetAnimationList();
		if (list == null || list.Length == 0)
			return;

		if (!string.IsNullOrEmpty(StrikeAnimationName) && _animationPlayer.HasAnimation(StrikeAnimationName))
			_strikeClip = StrikeAnimationName;
		else
			_strikeClip = list[0];

		if (!string.IsNullOrEmpty(MoveAnimationName) && _animationPlayer.HasAnimation(MoveAnimationName))
			_moveClip = MoveAnimationName;
		else
		{
			foreach (var n in list)
			{
				string s = n.ToString();
				if (s != _strikeClip)
				{
					_moveClip = s;
					break;
				}
			}
		}

		if (string.IsNullOrEmpty(_strikeClip))
			return;

		_useStrikeSequence = true;

		if (_animationPlayer.HasAnimation(_strikeClip))
		{
			var anim = _animationPlayer.GetAnimation(_strikeClip);
			if (anim != null)
			{
				anim.LoopMode = Animation.LoopModeEnum.Linear;
				if (anim.Length > 0.05f)
					_drainInterval = anim.Length;
			}
		}
	}

	private bool CanApplyDrainDamageByAnimPhase()
	{
		if (_animationPlayer == null || DrainDamageMinAttackPhase <= 0.01f)
			return true;
		if (_animationPlayer.CurrentAnimation != _strikeClip)
			return false;
		double len = _animationPlayer.CurrentAnimationLength;
		if (len <= 0.02)
			return true;
		float phase = (float)(_animationPlayer.CurrentAnimationPosition / len);
		return phase >= DrainDamageMinAttackPhase;
	}

	private void TryApplyDrainDamage()
	{
		if (_player == null || !_player.IsInsideTree()) return;
		if (_playerController != null && _playerController.IsBlockingEffectiveAgainst(GlobalPosition))
			return;

		var health = _player.GetNodeOrNull<HealthComponent>("HealthComponent");
		if (health == null) return;

		int dmg = Mathf.Max(1, health.MaxHealth / 3);
		health.TakeDamage(dmg);
		_playerController?.NotifyEnemyEnergyDrainHit();
		PlaySnakeEnergyDrainVisual();
		GetOrFindCamera()?.ShakeImpulse(0.11f, 0.14f);
	}

	private bool IsTouchingPlayer()
	{
		for (int i = 0; i < GetSlideCollisionCount(); i++)
		{
			var obj = GetSlideCollision(i).GetCollider();
			if (obj != null && obj == _player)
				return true;
		}

		float d = PlanarDistTo(_player.GlobalPosition);
		float dy = Mathf.Abs(_player.GlobalPosition.Y - GlobalPosition.Y);
		return d <= ContactReachMeters && dy <= ContactMaxHeightDelta;
	}

	private void TryApplyContactDamage()
	{
		if (_contactCooldown > 0f) return;
		if (!IsTouchingPlayer()) return;

		if (_player == null || !_player.IsInsideTree()) return;
		if (_playerController != null && _playerController.IsBlockingEffectiveAgainst(GlobalPosition))
			return;

		var health = _player.GetNodeOrNull<HealthComponent>("HealthComponent");
		if (health == null) return;

		int dmg = Mathf.Max(1, health.MaxHealth / 3);
		health.TakeDamage(dmg);
		_playerController?.NotifyEnemyEnergyDrainHit();
		PlaySnakeEnergyDrainVisual();
		GetOrFindCamera()?.ShakeImpulse(0.11f, 0.14f);
		_contactCooldown = ContactDamageCooldownSeconds;
	}

	private void FacePlayer()
	{
		if (_player == null || !_player.IsInsideTree() || !IsInsideTree()) return;
		var to = _player.GlobalPosition - GlobalPosition;
		to.Y = 0f;
		if (to.LengthSquared() < 1e-6f) return;
		LookAt(GlobalPosition + to.Normalized() * 3f, Vector3.Up);
		RotateY(Mathf.DegToRad(FaceYawOffsetDegrees));
	}

	private CameraFollow GetOrFindCamera()
	{
		return GetViewport()?.GetCamera3D() as CameraFollow;
	}

	/// <summary>Lyhyt väri- ja mittavaikutelma kun käärme “imee” energiaa (ei ääntä).</summary>
	private void PlaySnakeEnergyDrainVisual()
	{
		var geos = new List<GeometryInstance3D>();
		Node3D visualRoot = GetNodeOrNull<Node3D>("Visual") ?? (Node3D)this;
		CollectGeometryInstances(visualRoot, geos);
		if (geos.Count > 0)
		{
			var mat = new StandardMaterial3D
			{
				ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
				AlbedoColor = new Color(0.72f, 0.45f, 1f),
				EmissionEnabled = true,
				Emission = new Color(0.55f, 0.15f, 0.95f),
				EmissionEnergyMultiplier = 2.1f,
			};
			foreach (var g in geos)
				if (GodotObject.IsInstanceValid(g))
					g.MaterialOverride = mat;

			var flashTween = CreateTween();
			flashTween.TweenInterval(0.06f);
			flashTween.TweenCallback(Callable.From(() =>
			{
				foreach (var g in geos)
					if (GodotObject.IsInstanceValid(g))
						g.MaterialOverride = null;
			}));
		}

		Node3D visual = GetNodeOrNull<Node3D>("Visual");
		if (visual == null || !GodotObject.IsInstanceValid(visual))
			return;
		Vector3 s0 = visual.Scale;
		var pulse = CreateTween();
		pulse.TweenProperty(visual, "scale", s0 * 1.1f, 0.05f)
			.SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
		pulse.TweenProperty(visual, "scale", s0, 0.1f)
			.SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.In);
	}

	private void OnSwordHitFeedback()
	{
		GetOrFindCamera()?.ShakeImpulse(0.26f, 0.32f);

		var geos = new List<GeometryInstance3D>();
		Node3D visualRoot = GetNodeOrNull<Node3D>("Visual") ?? (Node3D)this;
		CollectGeometryInstances(visualRoot, geos);
		if (geos.Count > 0)
		{
			var flashMat = new StandardMaterial3D
			{
				ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
				AlbedoColor = new Color(1f, 0.82f, 0.82f),
				EmissionEnabled = true,
				Emission = new Color(1f, 0.35f, 0.35f),
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
		else if (visualRoot != this)
		{
			Vector3 s0 = visualRoot.Scale;
			var t = CreateTween();
			t.TweenProperty(visualRoot, "scale", s0 * 1.14f, 0.07f)
				.SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
			t.TweenProperty(visualRoot, "scale", s0, 0.12f)
				.SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.In);
		}

		if (_animationPlayer != null && GodotObject.IsInstanceValid(_animationPlayer))
		{
			_animationPlayer.SpeedScale = 0f;
			var animFreeze = CreateTween();
			animFreeze.TweenInterval(0.06f);
			animFreeze.TweenCallback(Callable.From(() =>
			{
				if (GodotObject.IsInstanceValid(_animationPlayer))
					_animationPlayer.SpeedScale = 1f;
			}));
		}
	}

	private float PlanarDistTo(Vector3 target)
	{
		float dx = target.X - GlobalPosition.X;
		float dz = target.Z - GlobalPosition.Z;
		return Mathf.Sqrt(dx * dx + dz * dz);
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
		_isDead = true;
		Velocity = Vector3.Zero;

		var col = GetNodeOrNull<CollisionShape3D>("CollisionShape3D");
		if (col != null) col.Disabled = true;

		Node3D visual = GetNodeOrNull<Node3D>("Visual") ?? (Node3D)this;

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
		Vector3 tilt = visual.RotationDegrees + new Vector3(82f, rollY, 0f);
		Vector3 kickPos = visual.GlobalPosition + new Vector3(away.X * 0.4f, 0.08f, away.Z * 0.4f);
		Vector3 slidePos = kickPos + new Vector3(away.X * 0.08f, -0.25f, away.Z * 0.08f);

		var tween = CreateTween();
		tween.SetParallel(true);
		tween.TweenProperty(visual, "scale", new Vector3(1.08f, 1.08f, 1.08f), 0.05f)
			.SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
		tween.TweenProperty(visual, "rotation_degrees", tilt, DeathTiltDuration)
			.SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
		tween.TweenProperty(visual, "global_position", kickPos, 0.06f)
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
}
