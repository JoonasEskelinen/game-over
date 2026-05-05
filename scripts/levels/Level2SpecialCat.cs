using Godot;

/// <summary>
/// Level 2: näkyy ja liikkuu vain kun pelaaja on erikoisase-tilassa (istuu + joystick-konteksti).
/// Liike = sama vasen tat / WASD kuin <see cref="PlayerController.GetSpecialWeaponStickVector"/>.
/// Idle / Walk FBX:t kuten EnemyLevel2 — Walk (ilman skiniä) tuodaan anim-kirjastoon rigille.
/// </summary>
public partial class Level2SpecialCat : CharacterBody3D
{
	[Export] public string IdleFbxPath = "res://assets/models/kissa/Idle.fbx";
	[Export] public string WalkFbxPath = "res://assets/models/kissa/Walk.fbx";
	[Export] public string MixamoSourceTrackName = "mixamo_com";

	[Export] public float MoveSpeed = 2.4f;
	[Export] public float Gravity = 20f;
	[Export] public float FaceYawOffsetDegrees = 0f;
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

	public override void _Ready()
	{
		FloorSnapLength = 0.18f;
		FloorMaxAngle = Mathf.DegToRad(50f);
		CollisionLayer = 128;
		CollisionMask = 1 | 128;

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
			var spawn = _player.GlobalPosition + _player.GlobalBasis * ActivateSpawnOffset;
			GlobalPosition = new Vector3(spawn.X, GlobalPosition.Y, spawn.Z);
			if (_visual != null)
				AlignToFloor();
		}

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
		string want = moving && _animationPlayer.HasAnimation(AnimWalk) ? AnimWalk : AnimIdle;
		if (_animationPlayer.CurrentAnimation != want && _animationPlayer.HasAnimation(want))
			_animationPlayer.Play(want);

		if (_visual == null || h.LengthSquared() < 1e-6f)
			return;

		var dir = h.Normalized();
		float targetYaw = Basis.LookingAt(-dir, Vector3.Up).GetEuler(EulerOrder.Yxz).Y
			+ Mathf.DegToRad(FaceYawOffsetDegrees);
		float cur = _visual.Rotation.Y;
		float smooth = 1f - Mathf.Exp(-12f * dt);
		_visual.Rotation = new Vector3(0f, Mathf.LerpAngle(cur, targetYaw, smooth), 0f);
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
		if (loop) animCopy.LoopMode = Animation.LoopModeEnum.Linear;
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
