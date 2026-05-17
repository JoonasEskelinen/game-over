using Godot;

/// <summary>
/// Level 2: näkyy ja liikkuu vain kun pelaaja on erikoisase-tilassa (istuu + joystick-konteksti).
/// Liike = sama vasen tat / WASD kuin <see cref="PlayerController.GetSpecialWeaponStickVector"/>.
/// R2 (<c>attack</c>) — <see cref="HitFbxPath"/> (esim. hit.fbx) kun saatavilla; varalla proseduraalinen kallistus.
/// Osuma hämähäkkiin: jänne eteenpäin + nousu ylös (roikkuvat); katolla myös <see cref="CanCatMeleeHitCeilingSpiderAt"/>.
/// </summary>
public partial class Level2SpecialCat : CharacterBody3D
{
	[Export] public string IdleFbxPath = "res://assets/models/kissa/Idle.fbx";
	[Export] public string WalkFbxPath = "res://assets/models/kissa/Walk.fbx";
	[Export] public string HitFbxPath = "res://assets/models/kissa/hit.fbx";
	[Export] public string MixamoSourceTrackName = "mixamo_com";

	[Export] public float MoveSpeed = 2.4f;
	[Export] public float Gravity = 20f;
	[Export] public float FaceYawOffsetDegrees = 0f;

	[ExportGroup("Hyökkäys (R2 / attack)")]
	/// <summary>Normaali 0–1 hit-klipin pituudesta: milloin osumaikkuna alkaa (kun Hit FBX ladattu).</summary>
	[Export(PropertyHint.Range, "0,1,0.01")] public float HitClipOsumaAlkuNorm = 0.2f;
	/// <summary>Normaali 0–1: milloin osumaikkuna päättyy.</summary>
	[Export(PropertyHint.Range, "0,1,0.01")] public float HitClipOsumaLoppuNorm = 0.42f;
	/// <summary>Hit-FBX:n toistonopeus (<see cref="AnimationPlayer.SpeedScale"/>). &gt;1 = nopeampi isku.</summary>
	[Export(PropertyHint.Range, "0.5,3,0.05")] public float HitClipSpeedScale = 1.55f;
	[Export] public float HyökkäysKestoSekuntia = 0.48f;
	[Export] public float HyökkäysOsumaAlku = 0.12f;
	[Export] public float HyökkäysOsumaLoppu = 0.3f;
	/// <summary>Jänne eteenpäin maailmayksiköissä (tassun iskulinja).</summary>
	[Export] public float HyökkäysReachMetriä = 1.05f;
	/// <summary>Maksimietäisyys 3D-jänteestä (tassun ”paksuus”).</summary>
	[Export] public float HyökkäysPawRadius = 0.52f;
	/// <summary>Etäisyys eteenpäin origosta ennen jännettä (nokka/käpälät).</summary>
	[Export] public float HyökkäysSegStartForwardM = 0.12f;
	/// <summary>Jänteen loppuun lisättävä nousu (m) — katossa roikkuvat kohteet.</summary>
	[Export] public float HyökkäysSegEndLiftY = 1.25f;
	[Export] public float HyökkäysOrigoOffsetY = 0.35f;
	[Export] public float HyökkäysPunchPitchDeg = 34f;
	/// <summary>
	/// Skaalaa koko FBX-juuren (Godot FBX-importissa usein jo metreissä).
	/// Liian pieni arvo näkyy valkoisena pisteenä — säädä vain jos malli on väärässä koossa.
	/// </summary>
	[Export] public float VisualUniformScale = 1f;

	[Export] public Vector3 ActivateSpawnOffset = new(0.85f, 0f, 0f);

	private PlayerController _player;
	private AnimationPlayer _animationPlayer;
	private Node3D _visual;
	private bool _wasSpecialActive;
	private const string AnimIdle = "cat_idle";
	private const string AnimWalk = "cat_walk";
	private const string AnimHit = "cat_hit";

	private float _attackTimer;
	private bool _attackActive;
	private float _attackPitchDeg;
	private bool _hasHitClip;
	private bool _swingUsesHitClip;
	private float _hitClipDuration;
	private float _hitWindowStart;
	private float _hitWindowEnd;
	/// <summary>Yksi kattohämähäkki per R2-hit — varattu kun joku <see cref="Level2CeilingSpider"/> on jo napannut osuman tällä iskulla.</summary>
	private bool _ceilingSpiderHitConsumedThisAttack;

	public override void _Ready()
	{
		FloorSnapLength = 0.18f;
		FloorMaxAngle = Mathf.DegToRad(50f);
		CollisionLayer = 128;
		CollisionMask = 1 | 128;
		AddToGroup("level2_special_cat");

		_player = GetTree().GetFirstNodeInGroup("player") as PlayerController;
		BuildVisualAndAnimations();
		Visible = false;
		if (_visual != null)
			_visual.Visible = false;
	}

	private void BuildVisualAndAnimations()
	{
		var idleScene = GD.Load<PackedScene>(IdleFbxPath);
		if (idleScene == null)
		{
			GD.PrintErr($"Level2SpecialCat: Idle FBX puuttuu tai polku väärä: {IdleFbxPath}");
			return;
		}

		var inst = idleScene.Instantiate<Node>();
		// Skaalaa koko scene-puu, älä vain "ensimmäistä" Node3D:ta (voi olla luu / tyhjä → piste ruudulla).
		if (inst is Node3D rootNd)
		{
			_visual = rootNd;
			AddChild(_visual);
		}
		else
		{
			var holder = new Node3D { Name = "KissaVisualRoot" };
			AddChild(holder);
			holder.AddChild(inst);
			_visual = holder;
		}

		_visual.Scale = Vector3.One * VisualUniformScale;

		_animationPlayer = _visual.FindChild("AnimationPlayer", true, false) as AnimationPlayer;
		if (_animationPlayer == null)
		{
			GD.PrintErr("Level2SpecialCat: AnimationPlayer puuttuu Idle FBX:stä.");
			return;
		}

		RenameLibraryClipFromMixamo(AnimIdle);
		LoadAnimFromExternalFbx(WalkFbxPath, AnimWalk, loop: true);
		LoadAnimFromExternalFbx(HitFbxPath, AnimHit, loop: false);
		_hasHitClip = _animationPlayer.HasAnimation(AnimHit);
		if (!_hasHitClip && !string.IsNullOrEmpty(HitFbxPath))
			GD.PrintErr($"Level2SpecialCat: Hit FBX ei löydy tai animaatio puuttuu: {HitFbxPath}");

		if (_animationPlayer.HasAnimation(AnimIdle))
			_animationPlayer.Play(AnimIdle);
	}

	public override void _PhysicsProcess(double delta)
	{
		float dt = (float)delta;
		if (_player == null || !GodotObject.IsInstanceValid(_player) || !_player.IsInsideTree())
			_player = GetTree().GetFirstNodeInGroup("player") as PlayerController;

		bool special = _player != null && _player.IsSpecialWeaponJoystickContextActive();
		bool show = special && _animationPlayer != null && _visual != null;
		if (_visual != null)
			_visual.Visible = show;
		Visible = show;

		if (!special)
		{
			_wasSpecialActive = false;
			_attackActive = false;
			_attackTimer = 0f;
			_attackPitchDeg = 0f;
			_swingUsesHitClip = false;
			_ceilingSpiderHitConsumedThisAttack = false;
			if (_animationPlayer != null)
				_animationPlayer.SpeedScale = 1f;
			Velocity = Vector3.Zero;
			return;
		}

		if (_animationPlayer == null || _visual == null)
		{
			Velocity = Vector3.Zero;
			return;
		}

		if (!_wasSpecialActive)
		{
			_wasSpecialActive = true;
			_attackActive = false;
			_attackTimer = 0f;
			_attackPitchDeg = 0f;
			_swingUsesHitClip = false;
			_ceilingSpiderHitConsumedThisAttack = false;
			var spawn = _player.GlobalPosition + _player.GlobalBasis * ActivateSpawnOffset;
			GlobalPosition = new Vector3(spawn.X, GlobalPosition.Y, spawn.Z);
			if (_visual != null)
				AlignToFloor();
		}

		TryStartOrTickAttack(dt);

		Vector3 v = Velocity;
		if (!IsOnFloor())
			v.Y -= Gravity * dt;

		// Sama vaaka/syvyys-akselit kuin PlayerController: GetVector(..., forward, back) antaa Y:n
		// päinvastoin kuin GetAxis("move_back", "move_forward") → syvyys väärinpäin ilman tätä.
		float dirX = Input.GetAxis("move_left", "move_right");
		float dirZ = 0f;
		if (_player.SyvyysliikeKäytössä)
			dirZ = _player.SyvyysSyötteenEtumerkki * Input.GetAxis("move_back", "move_forward");

		Vector2 planar = new(dirX, dirZ);
		Vector3 wish = Vector3.Zero;
		if (planar.LengthSquared() > 1e-5f)
		{
			planar = planar.Normalized();
			var cam = GetViewport()?.GetCamera3D();
			if (cam != null && cam.IsInsideTree())
			{
				Vector3 lookFlat = -cam.GlobalBasis.Z;
				lookFlat.Y = 0f;
				if (lookFlat.LengthSquared() < 1e-8f)
					lookFlat = new Vector3(0f, 0f, -1f);
				lookFlat = lookFlat.Normalized();
				Vector3 camRight = lookFlat.Cross(Vector3.Up).Normalized();
				wish = (camRight * planar.X + lookFlat * (-planar.Y)) * MoveSpeed;
			}
			else
				wish = new Vector3(planar.X, 0f, planar.Y) * MoveSpeed;
		}

		if (IsOnFloor())
			wish = wish.Slide(GetFloorNormal());

		v.X = wish.X;
		v.Z = wish.Z;
		Velocity = v;
		MoveAndSlide();

		if (_player.SyvyysliikeKäytössä)
		{
			Vector3 p = GlobalPosition;
			p.Z = Mathf.Clamp(p.Z, _player.SyvyysAlaraja, _player.SyvyysYläraja);
			GlobalPosition = p;
		}

		UpdateFacingAndAnim(wish, dt);
	}

	/// <summary>Hämähäkki: iskuikkuna + tarkka etäisyys jänteradan varrelta.</summary>
	public bool IsCatMeleeHitWindowActive()
		=> _attackActive && _attackTimer >= _hitWindowStart && _attackTimer < _hitWindowEnd;

	/// <summary>
	/// Kutsutaan ennen hämähäkin <c>TakeDamage</c>:ia — palauttaa true vain kerran per R2-hit (osumaikkunan aikana).
	/// </summary>
	public bool TryClaimOneCeilingSpiderHitThisAttack()
	{
		if (!IsCatMeleeHitWindowActive())
			return false;
		if (_ceilingSpiderHitConsumedThisAttack)
			return false;
		_ceilingSpiderHitConsumedThisAttack = true;
		return true;
	}

	/// <summary>
	/// Level 2 kattohämähäkki: <see cref="CanCatMeleeHitPoint"/> ulottuu vain ~1–2 m ylös (tassun jänne),
	/// mutta hämähäkki roikkuu ~3 m korkeudella — sama R2 hit-ikkuna, leveämpi vaaka/korkeus-tarkistus.
	/// </summary>
	public bool CanCatMeleeHitCeilingSpiderAt(Vector3 worldPoint)
	{
		if (!IsCatMeleeHitWindowActive())
			return false;
		Vector3 to = worldPoint - GlobalPosition;
		float horiz = new Vector2(to.X, to.Z).Length();
		if (worldPoint.Y < GlobalPosition.Y + 0.2f)
			return false;
		if (horiz > 2.25f)
			return false;
		if (horiz > 0.12f)
		{
			var toN = new Vector3(to.X, 0f, to.Z).Normalized();
			if (toN.Dot(GetCatFlatForward()) < -0.4f)
				return false;
		}
		return true;
	}

	public bool CanCatMeleeHitPoint(Vector3 worldPoint)
	{
		if (_visual == null) return false;
		var forward = GetCatFlatForward();
		Vector3 origin = GlobalPosition + Vector3.Up * HyökkäysOrigoOffsetY + forward * HyökkäysSegStartForwardM;
		Vector3 segEnd = origin + forward * HyökkäysReachMetriä + Vector3.Up * HyökkäysSegEndLiftY;
		float d = DistancePointToSegment3D(worldPoint, origin, segEnd);
		if (d > HyökkäysPawRadius)
			return false;

		Vector3 ab = segEnd - origin;
		float ab2 = ab.LengthSquared();
		if (ab2 < 1e-8f)
			return GlobalPosition.DistanceTo(worldPoint) <= HyökkäysPawRadius;
		float t = (worldPoint - origin).Dot(ab) / ab2;
		return t >= 0.02f && t <= 0.98f;
	}

	private Vector3 GetCatFlatForward()
	{
		var f = -GlobalBasis.Z;
		if (_visual != null && _visual.IsInsideTree())
			f = -_visual.GlobalTransform.Basis.Z;
		f.Y = 0f;
		return f.LengthSquared() > 1e-6f ? f.Normalized() : new Vector3(0f, 0f, -1f);
	}

	private void TryStartOrTickAttack(float dt)
	{
		if (Input.IsActionJustPressed("attack") && !_attackActive)
		{
			_attackActive = true;
			_attackTimer = 0f;
			_attackPitchDeg = 0f;
			_ceilingSpiderHitConsumedThisAttack = false;
			ConfigureAttackTimingAndPlayHitClip();
		}

		if (!_attackActive)
		{
			_attackPitchDeg = Mathf.MoveToward(_attackPitchDeg, 0f, 420f * dt);
			return;
		}

		if (_swingUsesHitClip && _animationPlayer != null)
		{
			_attackTimer = (float)_animationPlayer.CurrentAnimationPosition;
			bool pastEnd = _attackTimer >= _hitClipDuration - 0.002f;
			if (pastEnd || !_animationPlayer.IsPlaying())
				EndAttack();
			return;
		}

		float atkSpd = Mathf.Max(0.25f, HitClipSpeedScale);
		_attackTimer += dt * atkSpd;
		// Varalla: ei hit-klippiä — proseduraalinen kallistus
		float durTotal = _hitClipDuration;
		if (_attackTimer < _hitWindowStart)
		{
			float u = _hitWindowStart <= 1e-5f ? 1f : _attackTimer / _hitWindowStart;
			_attackPitchDeg = HyökkäysPunchPitchDeg * (1f - Mathf.Cos(u * Mathf.Pi * 0.5f));
		}
		else if (_attackTimer < _hitWindowEnd)
			_attackPitchDeg = HyökkäysPunchPitchDeg * 0.88f;
		else if (_attackTimer < durTotal)
		{
			float recoverDur = durTotal - _hitWindowEnd;
			float v = recoverDur <= 1e-5f ? 1f : (_attackTimer - _hitWindowEnd) / recoverDur;
			_attackPitchDeg = HyökkäysPunchPitchDeg * 0.88f * (1f - SmoothStep(0f, 1f, v));
		}
		else
			EndAttack();
	}

	private void ConfigureAttackTimingAndPlayHitClip()
	{
		if (_hasHitClip && _animationPlayer != null && _animationPlayer.HasAnimation(AnimHit))
		{
			_swingUsesHitClip = true;
			_hitClipDuration = Mathf.Max(0.05f, GetClipLengthSeconds(AnimHit));
			float a = Mathf.Clamp(Mathf.Min(HitClipOsumaAlkuNorm, HitClipOsumaLoppuNorm), 0f, 1f);
			float b = Mathf.Clamp(Mathf.Max(HitClipOsumaAlkuNorm, HitClipOsumaLoppuNorm), 0f, 1f);
			_hitWindowStart = _hitClipDuration * a;
			_hitWindowEnd = _hitClipDuration * Mathf.Max(a + 0.04f, b);
			_animationPlayer.Play(AnimHit);
			_animationPlayer.Seek(0d, true);
			_animationPlayer.SpeedScale = Mathf.Max(0.25f, HitClipSpeedScale);
			_attackTimer = 0f;
			return;
		}

		_swingUsesHitClip = false;
		_hitClipDuration = HyökkäysKestoSekuntia;
		_hitWindowStart = HyökkäysOsumaAlku;
		_hitWindowEnd = HyökkäysOsumaLoppu;
	}

	private void EndAttack()
	{
		if (_animationPlayer != null)
			_animationPlayer.SpeedScale = 1f;

		if (_swingUsesHitClip && _animationPlayer != null && _animationPlayer.CurrentAnimation == AnimHit)
		{
			if (_animationPlayer.HasAnimation(AnimIdle))
				_animationPlayer.Play(AnimIdle);
		}
		_swingUsesHitClip = false;
		_attackActive = false;
		_attackTimer = 0f;
		_attackPitchDeg = 0f;
		_ceilingSpiderHitConsumedThisAttack = false;
	}

	private float GetClipLengthSeconds(StringName clipName)
	{
		if (_animationPlayer?.GetAnimationLibrary("") is not AnimationLibrary lib)
			return 0f;
		if (!lib.HasAnimation(clipName))
			return 0f;
		return (float)lib.GetAnimation(clipName).Length;
	}

	private static float DistancePointToSegment3D(Vector3 p, Vector3 a, Vector3 b)
	{
		Vector3 ab = b - a;
		float abLenSq = ab.LengthSquared();
		if (abLenSq < 1e-10f)
			return p.DistanceTo(a);
		float t = Mathf.Clamp((p - a).Dot(ab) / abLenSq, 0f, 1f);
		return p.DistanceTo(a + ab * t);
	}

	private static float SmoothStep(float edge0, float edge1, float x)
	{
		float t = Mathf.Clamp((x - edge0) / Mathf.Max(1e-5f, edge1 - edge0), 0f, 1f);
		return t * t * (3f - 2f * t);
	}

	private void AlignToFloor()
	{
		var space = GetWorld3D().DirectSpaceState;
		var from = GlobalPosition + Vector3.Up * 2f;
		var to = GlobalPosition + Vector3.Down * 8f;
		var query = PhysicsRayQueryParameters3D.Create(from, to);
		query.CollisionMask = CollisionMask;
		var hit = space.IntersectRay(query);
		if (hit.Count > 0 && hit["position"].VariantType == Variant.Type.Vector3)
		{
			var pos = hit["position"].AsVector3();
			GlobalPosition = new Vector3(GlobalPosition.X, pos.Y, GlobalPosition.Z);
		}
	}

	private void UpdateFacingAndAnim(Vector3 wishXZ, float dt)
	{
		if (_animationPlayer == null)
			return;

		Vector3 h = new(wishXZ.X, 0f, wishXZ.Z);
		bool moving = h.LengthSquared() > 0.04f;
		bool hitClipPlaying = _attackActive && _swingUsesHitClip;
		string want = hitClipPlaying ? AnimHit
			: _attackActive ? AnimIdle
			: moving && _animationPlayer.HasAnimation(AnimWalk) ? AnimWalk : AnimIdle;
		if (!hitClipPlaying && _animationPlayer.CurrentAnimation != want && _animationPlayer.HasAnimation(want))
			_animationPlayer.Play(want);

		if (_visual == null)
			return;

		float targetYaw = _visual.Rotation.Y;
		if (h.LengthSquared() > 1e-6f)
		{
			var dir = h.Normalized();
			targetYaw = Basis.LookingAt(-dir, Vector3.Up).GetEuler(EulerOrder.Yxz).Y
				+ Mathf.DegToRad(FaceYawOffsetDegrees);
		}

		float curY = _visual.Rotation.Y;
		float smooth = 1f - Mathf.Exp(-12f * dt);
		bool useProcPitch = _attackActive && !hitClipPlaying;
		float pitchRad = Mathf.DegToRad(useProcPitch ? _attackPitchDeg : 0f);
		_visual.Scale = Vector3.One * VisualUniformScale;
		_visual.Rotation = new Vector3(pitchRad, Mathf.LerpAngle(curY, targetYaw, smooth), 0f);
	}

	private void RenameLibraryClipFromMixamo(string targetName)
	{
		if (_animationPlayer == null) return;
		AnimationLibrary lib = _animationPlayer.GetAnimationLibrary("");
		if (lib == null)
		{
			lib = new AnimationLibrary();
			_animationPlayer.AddAnimationLibrary("", lib);
		}

		foreach (var n in new[] { MixamoSourceTrackName, MixamoSourceTrackName.Replace("_", ".") })
		{
			if (!_animationPlayer.HasAnimation(n)) continue;
			var anim = (Animation)_animationPlayer.GetAnimation(n).Duplicate();
			anim.LoopMode = Animation.LoopModeEnum.Linear;
			if (lib.HasAnimation(targetName)) lib.RemoveAnimation(targetName);
			lib.AddAnimation(targetName, anim);
			lib.RemoveAnimation(n);
			return;
		}

		var list = _animationPlayer.GetAnimationList();
		if (list.Length > 0)
		{
			var first = list[0];
			var anim = (Animation)_animationPlayer.GetAnimation(first).Duplicate();
			anim.LoopMode = Animation.LoopModeEnum.Linear;
			if (lib.HasAnimation(targetName)) lib.RemoveAnimation(targetName);
			lib.AddAnimation(targetName, anim);
			if (first != targetName) lib.RemoveAnimation(first);
		}
	}

	private void LoadAnimFromExternalFbx(string path, string targetName, bool loop)
	{
		if (_animationPlayer == null || string.IsNullOrEmpty(path)) return;
		var scene = GD.Load<PackedScene>(path);
		if (scene == null)
		{
			GD.PrintErr($"Level2SpecialCat: Walk / lisä-FBX puuttuu: {path}");
			return;
		}

		var inst = scene.Instantiate();
		var ap = inst.FindChild("AnimationPlayer", true, false) as AnimationPlayer;
		if (ap == null)
		{
			inst.QueueFree();
			return;
		}

		Animation anim = null;
		foreach (var n in new[] { MixamoSourceTrackName, MixamoSourceTrackName.Replace("_", ".") })
			if (ap.HasAnimation(n)) { anim = ap.GetAnimation(n); break; }

		if (anim == null && ap.GetAnimationList().Length > 0)
		{
			var first = ap.GetAnimationList()[0];
			anim = ap.GetAnimation(first);
		}

		if (anim == null)
		{
			inst.QueueFree();
			GD.PrintErr($"Level2SpecialCat: ei animaatiota FBX:ssä {path}");
			return;
		}

		var animCopy = (Animation)anim.Duplicate();
		animCopy.LoopMode = loop ? Animation.LoopModeEnum.Linear : Animation.LoopModeEnum.None;
		var lib2 = _animationPlayer.GetAnimationLibrary("");
		if (lib2 == null)
		{
			lib2 = new AnimationLibrary();
			_animationPlayer.AddAnimationLibrary("", lib2);
		}

		if (lib2.HasAnimation(targetName)) lib2.RemoveAnimation(targetName);
		lib2.AddAnimation(targetName, animCopy);
		inst.QueueFree();
	}
}
