using System.Collections.Generic;
using Godot;

/// <summary>
/// Tason 1 susivihollinen: jahtaa pelaajaa, siirtyy purema-animaatioon lähietäisyydessä,
/// aiheuttaa puremavaikutukset tietyllä rytmillä ja rekisteröi miekan osumat.
/// </summary>
public partial class EnemyLevel1 : CharacterBody3D
{
	// --- Liike ja juoksu ---

	[ExportGroup("Liike")]
	/// <summary>Juoksunopeus (m/s) jahtaustilassa.</summary>
	[Export] public float Juoksunopeus = 3.35f;

	[ExportGroup("Esteet ja juoksun suunta")]
	/// <summary>Eteenpäin ammutun säteen pituus (m): törmääkö seinään juoksusuunnassa.</summary>
	[Export] public float EsteSäteenPituus = 0.65f;
	/// <summary>Säteen lähtökorkeus juuresta (m), jotta tarkistus osuu vartalon korkeudelle.</summary>
	[Export] public float EsteSäteenAlkuKorkeus = 0.35f;

	[ExportGroup("Lähestyminen")]
	/// <summary>Kun pelaaja on tätä lähempänä (m), nopeutta hidastetaan — vähentää “kiertämistä” reunalla.</summary>
	[Export] public float LähestymisHidastusAlku = 1.15f;
	/// <summary>Miniminopeuskerroin hidastusvyöhykkeellä (0–1).</summary>
	[Export] public float LähestymisHidastusMinKerroin = 0.22f;

	// --- Purema ja puremavyöhyke ---

	[ExportGroup("Purema: etäisyydet")]
	/// <summary>
	/// XZ-etäisyys pelaajaan (m), jolloin siirrytään purema-animaatioon.
	/// Pienempi arvo = pitää päästä lähemmäs. Jos susi jää vain juoksemaan, nosta hieman.
	/// </summary>
	[Export] public float PuremanTilaEtäisyys = 1.42f;

	/// <summary>
	/// Lisämetrejä <see cref="PuremanTilaEtäisyys"/>:ään: purema ei katkea, jos pelaaja liikkuu hieman rajalla (hysteresis).
	/// </summary>
	[Export] public float PuremanHystereesi = 0.42f;

	/// <summary>
	/// Pureman aikana laajempi “pysy kiinni”-vyöhyke (m) — estää animaation katkeamisen pienestä hipsutuksesta.
	/// </summary>
	[Export] public float PuremanLaajennettuHystereesi = 1.35f;

	/// <summary>
	/// Purematilassa sallittu korkeusero pelaajaan (m) — löysempi kuin perus ~1.9 m vain sticky-tarkistuksessa.
	/// </summary>
	[Export] public float PuremanKorkeusToleranssi = 2.35f;

	[ExportGroup("Purema: vahinko")]
	/// <summary>Pureman XZ-säde = <see cref="PuremanTilaEtäisyys"/> + tämä (m) — vastaa näkyvää puremaa, ei pelkkää tiukkaa etäisyyttä.</summary>
	[Export] public float PuremanVahinkoTasoLisä = 0.38f;

	/// <summary>Puremavaurion enimmäiskorkeusero pelaajaan (m).</summary>
	[Export] public float PuremanVahinkoKorkeusToleranssi = 2.05f;

	/// <summary>
	/// Sekunteja purema-animaation alusta ennen ensimmäistä vahinkoa (puree “osuu” vasta tämän jälkeen).
	/// </summary>
	[Export] public float PuremanVahinkoAloitusViive = 0.38f;

	/// <summary>
	/// Vahinko vain kun hyökkäysanimaatio on edennyt vähintään näin paljon (0–1). Estää osuman animaation alkuosassa.
	/// </summary>
	[Export] public float PuremanVahinkoMinAnimVaihe = 0.36f;

	[ExportGroup("Animaatio")]
	/// <summary>Polku FBX-tiedostoon, josta purema-animaatio ladataan.</summary>
	[Export] public string PuremaAnimPolku = "res://assets/models/level1_susi/susiWithoutskin/ZombieNeckBite.fbx";

	// --- Miekan osuma ---

	[ExportGroup("Miekan osuma: sijainti")]
	/// <summary>Osumapisteiden korkeus juuresta (m) — nelijalkaiselle tyypillisesti rintakehä.</summary>
	[Export] public float OsumaKeskikorkeus = 0.68f;

	/// <summary>
	/// Metrejä <see cref="OsumaKeskikorkeus"/>-pisteestä ylöspäin: useita koetinkorkeuksia (rintakehän eri kohdat).
	/// </summary>
	[Export] public float[] MiekkaOsumaKorkeusSiirtymät = { -0.22f, 0f, 0.28f, 0.52f };

	[ExportGroup("Miekan osuma: ajoitus")]
	/// <summary>Lisäviive sekunteina osumaikkunan alkuun (<c>GetMeleeStrikeWindowStart()</c> + tämä).</summary>
	[Export] public float MiekkaOsumaViive = 0f;

	/// <summary>
	/// Vain R1: viive osumaikkunan alusta (sek). Jos &gt; 0, korvaa <see cref="MiekkaOsumaViive"/> raskaassa iskussa (ei päällekkäistä viivettä).
	/// </summary>
	[Export] public float RaskasIskuAktivoitumisenLisäviive = 0f;

	/// <summary>R1: enimmäisetäisyys teräviivaan (m); laajentaa cleave-rekisteröintiä. 0 = pelaajan oletus.</summary>
	[Export] public float RaskasIskuLäheisyysYlikirjoitus = 0.95f;

	/// <summary>R1: montako EnemyLevel1:ää voi osua samaan swingiin.</summary>
	[Export] public int RaskasIskuCleaveKohteet = 2;

	// --- Yleinen ja kuolema ---

	[ExportGroup("Tila")]
	[Export] public int Elämäpisteet = 1;

	[ExportGroup("Kuoleman animaatio")]
	[Export] public float KuolemanKallistusKesto = 0.32f;
	[Export] public float KuolemanLiuutusKesto = 0.24f;
	[Export] public float KuolemanKutistumisKesto = 0.52f;

	private Node3D _player;
	private PlayerController _playerController;
	private AnimationPlayer _animationPlayer;
	private CameraFollow _camera;
	private float _biteTimer;
	private float _biteInterval = 2.5f;
	private bool _wasInStickyMelee;
	/// <summary>Tosi, kun ollaan vielä purematilassa vaikka pelaaja olisi hieman <see cref="PuremanTilaEtäisyys"/>-vyöhykkeen ulkopuolella.</summary>
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
		// Level1ArcadePhysicsSetup: arcade-prop-kerros (bitmask 16) + pelaaja + maailma
		CollisionMask |= 16u;

		_player = GetTree().GetFirstNodeInGroup("player") as Node3D;
		_playerController = _player as PlayerController;
		_animationPlayer = FindChild("AnimationPlayer", true, false) as AnimationPlayer;
		_biteSFX = GetNodeOrNull<AudioStreamPlayer>("BiteSFX");

		if (_animationPlayer != null)
		{
			LoadAnim(PuremaAnimPolku, "mixamo_com", "attack", loop: true);
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
			? Mathf.Max(PuremanHystereesi, PuremanLaajennettuHystereesi)
			: PuremanHystereesi;
		float stickDist = PuremanTilaEtäisyys + Mathf.Max(0f, stickMargin);
		float heightTol = (attackAnim || _inExtendedMelee)
			? Mathf.Max(1.9f, PuremanKorkeusToleranssi)
			: 1.9f;
		bool stickyMelee = planarDist <= stickDist && heightDiff <= heightTol;

		// Sticky-vyöhyke = purematila (ei vaadi tiukkaa etäisyyttä ensin — estää juoksuloopin reunalla).
		if (stickyMelee)
			_inExtendedMelee = true;
		else if (!stickyMelee)
			_inExtendedMelee = false;

		bool inBiteMode = stickyMelee;

		if (inBiteMode && !_wasInStickyMelee)
		{
			float maxWindup = Mathf.Max(0.55f, _biteInterval * 0.92f);
			float windup = Mathf.Clamp(PuremanVahinkoAloitusViive, 0f, maxWindup);
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
			bool heavy = _playerController.IsHeavyMeleeAttackActive()
				|| (_playerController.GetMeleeAttackDamage() >= 3 && _playerController.IsMeleeAttackActive());
			float osumaViive = heavy && RaskasIskuAktivoitumisenLisäviive > 0f
				? RaskasIskuAktivoitumisenLisäviive
				: MiekkaOsumaViive;
			float hitFrom = _playerController.GetMeleeStrikeWindowStart() + Mathf.Max(0f, osumaViive);

			if (animTime >= hitFrom && !_hasBeenHitThisSwing)
			{
				Vector3 bodyBase = GlobalPosition;
				var offsets = MiekkaOsumaKorkeusSiirtymät;
				if (offsets == null || offsets.Length == 0)
					offsets = new[] { 0f };

				float proxOverride = heavy && RaskasIskuLäheisyysYlikirjoitus > 0f
					? RaskasIskuLäheisyysYlikirjoitus
					: -1f;

				for (int i = 0; i < offsets.Length; i++)
				{
					Vector3 p = bodyBase + Vector3.Up * (OsumaKeskikorkeus + offsets[i]);
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
			// Lyönti loppui — nollataan omat ja pelaajan swingivaraus
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
		if (_animationPlayer == null || PuremanVahinkoMinAnimVaihe <= 0.01f)
			return true;
		if (_animationPlayer.CurrentAnimation != "attack")
			return false;
		double len = _animationPlayer.CurrentAnimationLength;
		if (len <= 0.02)
			return true;
		float phase = (float)(_animationPlayer.CurrentAnimationPosition / len);
		return phase >= PuremanVahinkoMinAnimVaihe;
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
		float maxPlanar = PuremanTilaEtäisyys + Mathf.Max(0f, PuremanVahinkoTasoLisä);
		if (planarDist > maxPlanar || heightDiff > PuremanVahinkoKorkeusToleranssi)
			return;

		Vector3 threat = GlobalPosition.Lerp(_player.GlobalPosition, 0.35f);
		if (_playerController.IsBlockingEffectiveAgainst(threat))
		{
			GD.Print("Isku torjuttu kilpella!");
			return;
		}

		var health = _player.GetNodeOrNull<HealthComponent>("HealthComponent");
		if (health == null) { GD.PrintErr("HealthComponent puuttuu!"); return; }

		// Noin 1/3 max-HP per purema → kolme osumaa vie yhden elämän, kun MaxHealth = 3.
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
		float start = Mathf.Max(PuremanTilaEtäisyys * 0.85f, LähestymisHidastusAlku);
		if (planarDistToPlayer >= start)
			return Juoksunopeus;
		float t = Mathf.Clamp(planarDistToPlayer / Mathf.Max(0.05f, start), 0f, 1f);
		float factor = Mathf.Lerp(LähestymisHidastusMinKerroin, 1f, t);
		return Juoksunopeus * factor;
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

		float rayLen = Mathf.Max(0.25f, EsteSäteenPituus);
		Vector3 from = GlobalPosition + Vector3.Up * EsteSäteenAlkuKorkeus;
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

	/// <summary>
	/// Jos susi ei liiku juoksun aikana tarpeeksi pitkään, annetaan pieni sivuttaisnudge (jumituksen purku).
	/// </summary>
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

	/// <summary>Kutsutaan miekan osuman yhteydessä: ruututärinä, välähdys ja lyhyt animaatiosulku.</summary>
	private void OnSwordHitFeedback(int damage)
	{
		bool heavy = damage >= 3;
		// 1. Ruututärinä (R1 vahvempi kuin R2)
		GetOrFindCamera()?.ShakeImpulse(heavy ? 0.28f : 0.20f, heavy ? 0.32f : 0.24f);

		// 2. Hit flash: hetkellinen vaalea/punainen siluetti (~50 ms peliaikaa)
		var geos = new List<GeometryInstance3D>();
		CollectGeometryInstances(this, geos);
		if (geos.Count > 0)
		{
			var flashMat = new StandardMaterial3D
			{
				ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
				AlbedoColor = heavy ? new Color(1f, 0.72f, 0.72f) : new Color(1f, 0.82f, 0.82f),
				EmissionEnabled = true,
				Emission = heavy ? new Color(1f, 0.22f, 0.22f) : new Color(1f, 0.35f, 0.35f),
				EmissionEnergyMultiplier = heavy ? 2.85f : 2.2f,
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

		// 3. Lyhyt hit-stop: animaation nopeus nollaan (R1 hieman pidempi)
		float hitStop = heavy ? 0.085f : 0.06f;
		if (_animationPlayer != null && GodotObject.IsInstanceValid(_animationPlayer))
		{
			_animationPlayer.SpeedScale = 0f;
			var animFreeze = CreateTween();
			animFreeze.TweenInterval(hitStop);
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
		Elämäpisteet -= amount;
		OnSwordHitFeedback(amount);
		if (Elämäpisteet <= 0) Die();
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

		// Slow motion: ~70 ms reaaliaikaa, ei riipu TimeScalesta
		Engine.TimeScale = 0.15f;
		if (IsInsideTree())
		{
			var slowTimer = GetTree().CreateTimer(0.07f, processInPhysics: false, ignoreTimeScale: true);
			slowTimer.Timeout += () => { if (Engine.TimeScale < 1f) Engine.TimeScale = 1f; };
		}

		float rollY = (float)GD.RandRange(-38.0, 38.0);
		float rollZ = (float)GD.RandRange(-28.0, 28.0);
		Vector3 tilt = visual.RotationDegrees + new Vector3(86f, rollY, rollZ);

		// Potkaisu poispäin, sitten liuku alas
		Vector3 basePos = visual.IsInsideTree() ? visual.GlobalPosition : visual.Position;
		Vector3 kickPos  = basePos  + new Vector3(away.X * 0.45f,  0.10f, away.Z * 0.45f);
		Vector3 slidePos = kickPos  + new Vector3(away.X * 0.10f, -0.38f, away.Z * 0.10f);

		var tween = CreateTween();

		tween.SetParallel(true);
		tween.TweenProperty(visual, "scale", new Vector3(1.13f, 1.13f, 1.13f), 0.05f)
			.SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
		tween.TweenProperty(visual, "rotation_degrees", tilt, KuolemanKallistusKesto)
			.SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
		tween.TweenProperty(visual, "global_position", kickPos, 0.07f)
			.SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
		tween.SetParallel(false);

		tween.TweenProperty(visual, "global_position", slidePos, KuolemanLiuutusKesto)
			.SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.In);

		var geos = new List<GeometryInstance3D>();
		CollectGeometryInstances(visual, geos);
		if (geos.Count > 0)
		{
			tween.SetParallel(true);
			tween.TweenProperty(visual, "scale", Vector3.Zero, KuolemanKutistumisKesto)
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
				0f, 1f, KuolemanKutistumisKesto);
			tween.SetParallel(false);
		}
		else
		{
			tween.TweenProperty(visual, "scale", Vector3.Zero, KuolemanKutistumisKesto)
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

	/// <summary>Lataa animaation ulkoisesta FBX:stä nykyisen AnimationPlayerin kirjastoon.</summary>
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
