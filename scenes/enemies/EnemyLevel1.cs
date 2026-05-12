using System.Collections.Generic;
using Godot;

public partial class EnemyLevel1 : CharacterBody3D
{
	[Export] public float Speed = 3.35f;
	/// <summary>XZ-etäisyys pelaajaan, jolloin ZombieNeckBite alkaa (pienempi = pitää päästä lähemmäs). Jos jää vain juoksuun, nosta hieman.</summary>
	[Export] public float AttackRange = 1.42f;

	/// <summary>
	/// Lisämetrejä AttackRangeen: purema/animaatio ei katkea jos pelaaja liikkuu hieman juuri rajalla (hysteresis).
	/// </summary>
	[Export] public float AttackStickMargin = 0.42f;

	/// <summary>
	/// Kun purema-animaatio pyörii, laajempi “pysy puremassa”-vyöhyke (m) — estää että pieni hipsutus katkaisee animin.
	/// </summary>
	[Export] public float BiteStickMarginDuringAttack = 1.35f;

	/// <summary>Purematilassa sallittu korkeusero pelaajaan (m) — hieman löysempi kuin <c>1.9f</c> vain sticky-tarkistuksessa.</summary>
	[Export] public float BiteHeightToleranceDuringAttack = 2.35f;

	/// <summary>Puremavaurion XZ-säde = <see cref="AttackRange"/> + tämä (m) — sama kuin “näkyvä purema”, ei vain tiukka AttackRange.</summary>
	[Export] public float BiteDamagePlanarExtra = 0.38f;

	/// <summary>Puremavaurion max korkeusero pelaajaan (m).</summary>
	[Export] public float BiteDamageHeightTolerance = 2.05f;

	/// <summary>Juoksu: säde eteenpäin törmäystarkistukseen (m).</summary>
	[Export] public float MoveObstacleRayLength = 0.65f;

	/// <summary>Juoksu: säteen alku Y juuresta.</summary>
	[Export] public float MoveObstacleRayOriginY = 0.35f;

	/// <summary>Kun näin lähellä pelaajaa, hidastetaan lähestymistä (estää “orbitointia” reunalla).</summary>
	[Export] public float ApproachSlowdownStartDistance = 1.15f;

	/// <summary>Miniminopeuskerroin hidastusvyöhykkeellä (0–1).</summary>
	[Export] public float ApproachSlowdownMinFactor = 0.22f;
	[Export] public int Health = 1;
	[Export] public string AttackAnimPath = "res://assets/models/level1_susi/susiWithoutskin/ZombieNeckBite.fbx";

	/// <summary>Pääosuma-akselin korkeus GlobalPositionista (nelijalkainen: rintakehä).</summary>
	[Export] public float HitCenterYOffset = 0.68f;

	/// <summary>Metriä <see cref="HitCenterYOffset"/> suuntaan (ylös) — osumapisteet rintakehän korkeudella, ei vain juuressa.</summary>
	[Export] public float[] SwordHitProbeOffsetsFromHitCenter = { -0.22f, 0f, 0.28f, 0.52f };

	/// <summary>Lisäviive sekunteina GetMeleeStrikeWindowStart()-ajan päälle (säätö).</summary>
	[Export] public float SwordHitActivationTime = 0f;

	/// <summary>Vain R1: lisäviive osumaikkunan alkuun (esim. 0.06–0.12). R2 käyttää vain SwordHitActivationTime.</summary>
	[Export] public float RaskasIskuAktivoitumisenLisäviive = 0f;

	/// <summary>R1: max etäisyys teräviivaan (m); laajentaa cleave-rekisteröintiä. 0 = pelaajan oletus.</summary>
	[Export] public float RaskasIskuLäheisyysYlikirjoitus = 0.95f;

	/// <summary>R1: montako EnemyLevel1:ää voi osua samaan swingiin.</summary>
	[Export] public int RaskasIskuCleaveKohteet = 2;

	[Export] public float DeathTiltDuration = 0.32f;
	[Export] public float DeathSlideDuration = 0.24f;
	[Export] public float DeathShrinkDuration = 0.52f;

	/// <summary>
	/// Sekuntia purema-animaation alusta ennen ensimmäistä vahinkoa (puree "osuu" eikä heti kun anim käynnistyy).
	/// </summary>
	[Export] public float BiteDamageWindupSeconds = 0.38f;

	/// <summary>
	/// Puremavaurio vain kun hyökkäysanimaatio on edennyt vähintään näin paljon (0–1). Estää vahingon animaation alkuosassa.
	/// </summary>
	[Export] public float BiteDamageMinAttackPhase = 0.36f;

	private Node3D _player;
	private PlayerController _playerController;
	private AnimationPlayer _animationPlayer;
	private CameraFollow _camera;
	private float _biteTimer;
	private float _biteInterval = 2.5f;
	private bool _wasInStickyMelee;
	/// <summary>True kun ollaan vielä "purentatilassa" vaikka pelaaja olisi hipsuttanut hieman AttackRange ulkopuolelle.</summary>
	private bool _inExtendedMelee;
	private Vector3 _prevChasePos;
	private float _chaseStuckTimer;
	private bool _isDead = false;
	private const float Gravity = 20f;
	private bool _hasBeenHitThisSwing = false;
	private AudioStreamPlayer _biteSFX;

	public override void _Ready()
	{
		FloorSnapLength = 0.22f;
		FloorMaxAngle = Mathf.DegToRad(50f);
		SafeMargin = 0.11f;
		// Level1ArcadePhysicsSetup: arcade-prop kerros (bitmask 16) + pelaaja + maailma
		CollisionMask |= 16u;

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

		_prevChasePos = GlobalPosition;
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
		bool attackAnim = _animationPlayer != null && _animationPlayer.CurrentAnimation == "attack";
		float stickMargin = (attackAnim || _inExtendedMelee)
			? Mathf.Max(AttackStickMargin, BiteStickMarginDuringAttack)
			: AttackStickMargin;
		float stickDist = AttackRange + Mathf.Max(0f, stickMargin);
		float heightTol = (attackAnim || _inExtendedMelee)
			? Mathf.Max(1.9f, BiteHeightToleranceDuringAttack)
			: 1.9f;
		bool stickyMelee = planarDist <= stickDist && heightDiff <= heightTol;

		// Sticky-vyöhyke = purematila (ei vaadi että strictMelee olisi käynyt ensin — estää juoksuloopin reunalla).
		if (stickyMelee)
			_inExtendedMelee = true;
		else if (!stickyMelee)
			_inExtendedMelee = false;

		bool inBiteMode = stickyMelee;

		if (inBiteMode && !_wasInStickyMelee)
		{
			float maxWindup = Mathf.Max(0.55f, _biteInterval * 0.92f);
			float windup = Mathf.Clamp(BiteDamageWindupSeconds, 0.2f, maxWindup);
			_biteTimer = windup;
		}

		_wasInStickyMelee = inBiteMode;

		bool chasing = !inBiteMode;

		if (chasing)
		{
			Vector3 direction = _player.GlobalPosition - GlobalPosition;
			direction.Y = 0f;
			if (direction.LengthSquared() > 1e-6f)
				direction = direction.Normalized();
			else
				direction = Vector3.Forward;

			float speed = ComputeChaseSpeed(planarDist);
			Vector3 steer = AdjustChaseDirectionForObstacles(direction);
			velocity.X = steer.X * speed;
			velocity.Z = steer.Z * speed;
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
				_animationPlayer.Play("attack");

			_biteTimer -= dt;
			if (_biteTimer <= 0f && CanApplyBiteDamageByAnimPhase())
			{
				TryApplyBiteDamage();
				_biteTimer = _biteInterval;
			}
		}

		// Miekan osuma: R2 yksi kohde/swing; R1 cleave — PlayerController.TryClaimEnemyHeavyCleaveHit (max 2)
		if (_playerController != null && _playerController.IsMeleeAttackActive())
		{
			float animTime = _playerController.GetAttackAnimationTime();
			float hitFrom = _playerController.GetMeleeStrikeWindowStart() + SwordHitActivationTime;
			bool heavy = _playerController.IsHeavyMeleeAttackActive();
			if (heavy && RaskasIskuAktivoitumisenLisäviive > 0f)
				hitFrom += RaskasIskuAktivoitumisenLisäviive;

			if (animTime >= hitFrom && !_hasBeenHitThisSwing)
			{
				Vector3 bodyBase = GlobalPosition;
				var offsets = SwordHitProbeOffsetsFromHitCenter;
				if (offsets == null || offsets.Length == 0)
					offsets = new[] { 0f };

				float proxOverride = heavy && RaskasIskuLäheisyysYlikirjoitus > 0f
					? RaskasIskuLäheisyysYlikirjoitus
					: -1f;

				for (int i = 0; i < offsets.Length; i++)
				{
					Vector3 p = bodyBase + Vector3.Up * (HitCenterYOffset + offsets[i]);
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
			// Lyonti loppui — nollataan omat ja pelaajan swingivaraus
			_hasBeenHitThisSwing = false;
			_playerController?.ClearEnemyHitThisSwing();
		}

		Velocity = velocity;
		MoveAndSlide();

		if (chasing)
		{
			TryUnstickFromWalls(dt);
			TrackChaseStuckAndNudge(dt);
		}
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

		float planarDist = PlanarDistanceTo(_player.GlobalPosition);
		float heightDiff = Mathf.Abs(_player.GlobalPosition.Y - GlobalPosition.Y);
		float maxPlanar = AttackRange + Mathf.Max(0f, BiteDamagePlanarExtra);
		if (planarDist > maxPlanar || heightDiff > BiteDamageHeightTolerance)
			return;

		Vector3 threat = GlobalPosition.Lerp(_player.GlobalPosition, 0.35f);
		if (_playerController.IsBlockingEffectiveAgainst(threat))
		{
			GD.Print("Isku torjuttu kilpella!");
			return;
		}

		var health = _player.GetNodeOrNull<HealthComponent>("HealthComponent");
		if (health == null) { GD.PrintErr("HealthComponent puuttuu!"); return; }

		// ~1/3 max-HP per purema → kolme osumaa vie yhden elämän kun MaxHealth = 3 (aiemmin ~39% = 2 puremaa).
		int biteDamage = Mathf.Max(1, Mathf.CeilToInt(health.MaxHealth / 3f));
		health.TakeDamage(biteDamage);
		_playerController?.NotifyLevel1BiteHit();
		_biteSFX?.Play();
	}

	private static float PlanarDistanceTo(Vector3 from, Vector3 to)
	{
		float dx = to.X - from.X;
		float dz = to.Z - from.Z;
		return Mathf.Sqrt(dx * dx + dz * dz);
	}

	private float PlanarDistanceTo(Vector3 targetGlobal)
		=> PlanarDistanceTo(GlobalPosition, targetGlobal);

	private float ComputeChaseSpeed(float planarDistToPlayer)
	{
		float start = Mathf.Max(AttackRange * 0.85f, ApproachSlowdownStartDistance);
		if (planarDistToPlayer >= start)
			return Speed;
		float t = Mathf.Clamp(planarDistToPlayer / Mathf.Max(0.05f, start), 0f, 1f);
		float factor = Mathf.Lerp(ApproachSlowdownMinFactor, 1f, t);
		return Speed * factor;
	}

	private Vector3 AdjustChaseDirectionForObstacles(Vector3 dirNorm)
	{
		dirNorm.Y = 0f;
		if (dirNorm.LengthSquared() < 1e-6f)
			return Vector3.Forward;
		dirNorm = dirNorm.Normalized();

		var space = GetWorld3D()?.DirectSpaceState;
		if (space == null)
			return dirNorm;

		float rayLen = Mathf.Max(0.25f, MoveObstacleRayLength);
		Vector3 from = GlobalPosition + Vector3.Up * MoveObstacleRayOriginY;
		if (!RayChaseBlocked(space, from, dirNorm, rayLen))
			return dirNorm;

		float[] degOffsets = { -35f, 35f, -70f, 70f, -110f, 110f, 160f };
		foreach (float deg in degOffsets)
		{
			Vector3 alt = dirNorm.Rotated(Vector3.Up, Mathf.DegToRad(deg));
			alt.Y = 0f;
			if (alt.LengthSquared() < 1e-6f)
				continue;
			alt = alt.Normalized();
			if (!RayChaseBlocked(space, from, alt, rayLen * 0.85f))
				return alt;
		}

		Vector3 perp = new Vector3(-dirNorm.Z, 0f, dirNorm.X);
		if (!RayChaseBlocked(space, from, perp, rayLen * 0.5f))
			return perp;
		perp = -perp;
		if (!RayChaseBlocked(space, from, perp, rayLen * 0.5f))
			return perp;

		return dirNorm;
	}

	private bool RayChaseBlocked(PhysicsDirectSpaceState3D space, Vector3 from, Vector3 dirXZ, float len)
	{
		dirXZ.Y = 0f;
		if (dirXZ.LengthSquared() < 1e-6f)
			return true;
		dirXZ = dirXZ.Normalized();
		var q = PhysicsRayQueryParameters3D.Create(from, from + dirXZ * len);
		q.CollisionMask = CollisionMask;
		q.CollideWithAreas = false;
		var ex = new Godot.Collections.Array<Rid> { GetRid() };
		if (_player is CollisionObject3D pc)
			ex.Add(pc.GetRid());
		q.Exclude = ex;
		return space.IntersectRay(q).Count > 0;
	}

	private void TryUnstickFromWalls(float dt)
	{
		Vector3 accum = Vector3.Zero;
		for (int i = 0; i < GetSlideCollisionCount(); i++)
		{
			var col = GetSlideCollision(i);
			Vector3 n = col.GetNormal();
			if (n.Y > 0.55f)
				continue;
			if (_player is CollisionObject3D pc && col.GetCollider() == pc)
				continue;
			Vector3 flat = new Vector3(n.X, 0f, n.Z);
			if (flat.LengthSquared() > 1e-5f)
				accum += flat.Normalized();
		}

		if (accum.LengthSquared() < 1e-8f)
			return;

		GlobalPosition += accum.Normalized() * Mathf.Clamp(dt * 5f, 0f, 0.18f);
	}

	private void TrackChaseStuckAndNudge(float dt)
	{
		Vector2 cur = new(GlobalPosition.X, GlobalPosition.Z);
		Vector2 prev = new(_prevChasePos.X, _prevChasePos.Z);
		if ((cur - prev).Length() < 0.04f)
			_chaseStuckTimer += dt;
		else
			_chaseStuckTimer = 0f;
		_prevChasePos = GlobalPosition;

		if (_chaseStuckTimer < 0.45f || _player == null || !GodotObject.IsInstanceValid(_player))
			return;

		_chaseStuckTimer = 0f;
		Vector3 side = (_player.GlobalPosition - GlobalPosition).Cross(Vector3.Up);
		side.Y = 0f;
		if (side.LengthSquared() < 1e-5f)
			side = Vector3.Right;
		else
			side = side.Normalized();
		if (GD.Randf() < 0.5f)
			side = -side;
		GlobalPosition += side * 0.35f;
	}

	private CameraFollow GetOrFindCamera()
	{
		if (_camera != null && GodotObject.IsInstanceValid(_camera)) return _camera;
		_camera = GetViewport()?.GetCamera3D() as CameraFollow;
		return _camera;
	}

	/// <summary>Välitön visuaalinen palaute miekkaosumahetkellä — ennen kuolemaa.</summary>
	private void OnSwordHitFeedback()
	{
		// 1. Ruututärinä
		GetOrFindCamera()?.ShakeImpulse(0.20f, 0.24f);

		// 2. Hit flash: hetkellinen valkoinen/punainen siluetti (50 ms peliaika)
		var geos = new List<GeometryInstance3D>();
		CollectGeometryInstances(this, geos);
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
				if (GodotObject.IsInstanceValid(g))
					g.MaterialOverride = flashMat;

			var flashTween = CreateTween();
			flashTween.TweenInterval(0.05f);
			flashTween.TweenCallback(Callable.From(() =>
			{
				foreach (var g in geos)
					if (GodotObject.IsInstanceValid(g))
						g.MaterialOverride = null;
			}));
		}

		// 3. Vihollisen hit-stop: jäädyttää animaatio 60 ms
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

		Node3D visual = GetNodeOrNull<Node3D>("Run") ?? (Node3D)this;

		Vector3 away = new Vector3(1f, 0f, 0f);
		if (_player != null && GodotObject.IsInstanceValid(_player) && _player.IsInsideTree())
		{
			away = GlobalPosition - _player.GlobalPosition;
			away.Y = 0f;
			if (away.LengthSquared() > 1e-5f)
				away = away.Normalized();
		}

		// Slow motion: 70 ms reaaliaikaa, riippumaton TimeScalesta
		Engine.TimeScale = 0.15f;
		if (IsInsideTree())
		{
			var slowTimer = GetTree().CreateTimer(0.07f, processInPhysics: false, ignoreTimeScale: true);
			slowTimer.Timeout += () => { if (Engine.TimeScale < 1f) Engine.TimeScale = 1f; };
		}

		float rollY = (float)GD.RandRange(-38.0, 38.0);
		float rollZ = (float)GD.RandRange(-28.0, 28.0);
		Vector3 tilt = visual.RotationDegrees + new Vector3(86f, rollY, rollZ);

		// Kickback: nopea loikka poispäin → sitten kaatuminen alas
		Vector3 basePos = visual.IsInsideTree() ? visual.GlobalPosition : visual.Position;
		Vector3 kickPos  = basePos  + new Vector3(away.X * 0.45f,  0.10f, away.Z * 0.45f);
		Vector3 slidePos = kickPos  + new Vector3(away.X * 0.10f, -0.38f, away.Z * 0.10f);

		var tween = CreateTween();

		// Rinnakkainen alkuisku: scale-punch + tilt + kickback alkaa yhtä aikaa
		tween.SetParallel(true);
		tween.TweenProperty(visual, "scale", new Vector3(1.13f, 1.13f, 1.13f), 0.05f)
			.SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
		tween.TweenProperty(visual, "rotation_degrees", tilt, DeathTiltDuration)
			.SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
		tween.TweenProperty(visual, "global_position", kickPos, 0.07f)
			.SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
		tween.SetParallel(false);

		// Kaatumisliuku
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
