using Godot;

/// <summary>
/// Level 3: dronen erikoisase — näkyy ja liikkuu kun pelaaja istuu (ympyrä/sit) ja joystick on käytössä.
/// Vasen tat / WASD: vaakalento kameran suhteen; oikea tat ylös/alas (<c>cam_look_up</c>/<c>cam_look_down</c>): korkeus;
/// R2 (<c>attack</c>): pudottaa pommin bossia vastaan.
/// </summary>
public partial class Level3SpecialDrone : CharacterBody3D
{
	/// <summary>Jos asetettu Inspectorissa, käytetään tätä (esim. raahaa drone.glb). Muuten yritetään <see cref="DroneGlbPath"/>.</summary>
	[Export] public PackedScene DronePackedScene;
	[Export] public string DroneGlbPath = "res://assets/models/drone/drone.glb";
	[Export] public PackedScene BombScene;
	[Export] public float MoveSpeed = 9f;
	[Export] public float HoverGroundClearance = 3.2f;
	[Export] public float HoverSmoothing = 6f;
	[Export] public float MaxVerticalSpeed = 22f;
	[Export] public float ActivateHeightAbovePlayer = 4.5f;
	[Export] public Vector3 ActivateSpawnOffset = new(0.6f, 0f, 0f);
	[Export] public float VisualUniformScale = 1f;
	[Export] public float PommiJäähdytysSek = 0.9f;
	[Export] public Vector3 PommiSpawnOffset = new(0f, -0.35f, 0f);
	/// <summary>Kuinka kaukana pelaajasta dronen saa lentää (XZ) — estää pommit bossille kentän alusta.</summary>
	[Export] public float MaxEtäisyysPelaajastaXZ = 24f;
	/// <summary>Vain yksi elossa oleva pommi kerrallaan.</summary>
	[Export] public int MaxAktiivisetPommit = 1;
	[Export] public float PommiLiipaisinLaukaisu = 0.4f;
	[Export] public float PommiLiipaisinPalautus = 0.26f;
	[Export] public float PommiPikaVetoMinimi = 0.38f;
	[Export] public float PommiPikaVetoNousu = 0.12f;
	/// <summary>Oikea tat Y: kuinka nopeasti manuaalinen korkeusoffset kasvaa (m/s suuntaan).</summary>
	[Export] public float KorkeusSäätöNopeus = 7f;
	[Export] public float KorkeusSäätöMinOffset = -6f;
	[Export] public float KorkeusSäätöMaxOffset = 22f;

	private PlayerController _player;
	private Node3D _visual;
	private bool _wasSpecialActive;
	private float _bombCooldown;
	private float _targetHoverY;
	private float _manualHeightOffset;
	private bool _bombAttackArmed = true;
	private float _bombAttackStrengthPrev;

	public override void _Ready()
	{
		MotionMode = MotionModeEnum.Floating;
		UpDirection = Vector3.Up;
		FloorStopOnSlope = false;
		CollisionLayer = 128;
		CollisionMask = 1 | 128;

		_player = GetTree().GetFirstNodeInGroup("player") as PlayerController;
		if (BombScene == null)
			BombScene = GD.Load<PackedScene>("res://scenes/hazards/Level3DroneBomb.tscn");
		BuildVisual();
		Visible = false;
		if (_visual != null)
			_visual.Visible = false;
	}

	private void BuildVisual()
	{
		PackedScene scene = DronePackedScene;
		if (scene == null && !string.IsNullOrEmpty(DroneGlbPath) && ResourceLoader.Exists(DroneGlbPath))
			scene = GD.Load<PackedScene>(DroneGlbPath);

		if (scene != null)
		{
			var inst = scene.Instantiate<Node>();
			if (inst is Node3D nd)
			{
				_visual = nd;
				AddChild(_visual);
			}
			else
			{
				var holder = new Node3D { Name = "DroneVisualRoot" };
				AddChild(holder);
				holder.AddChild(inst);
				_visual = holder;
			}
		}
		else
		{
			GD.PushWarning(
				"Level3SpecialDrone: ei dronen mallia — asenna res://assets/models/drone/drone.glb tai aseta Inspectorissa DronePackedScene (raahaa .glb). Näytetään placeholder.");
			_visual = new Node3D { Name = "DronePlaceholder" };
			var mesh = new MeshInstance3D
			{
				Mesh = new SphereMesh { Radius = 0.22f, Height = 0.44f },
			};
			var mat = new StandardMaterial3D
			{
				ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
				AlbedoColor = new Color(0.2f, 0.85f, 0.35f),
			};
			mesh.SetSurfaceOverrideMaterial(0, mat);
			_visual.AddChild(mesh);
			AddChild(_visual);
		}

		_visual.Scale = Vector3.One * VisualUniformScale;
	}

	public override void _PhysicsProcess(double delta)
	{
		float dt = (float)delta;
		if (_bombCooldown > 0f)
			_bombCooldown = Mathf.Max(0f, _bombCooldown - dt);

		if (_player == null || !GodotObject.IsInstanceValid(_player) || !_player.IsInsideTree())
			_player = GetTree().GetFirstNodeInGroup("player") as PlayerController;

		bool special = _player != null && _player.IsSpecialWeaponJoystickContextActive();
		bool show = special && _visual != null;
		if (_visual != null)
			_visual.Visible = show;
		Visible = show;

		if (!special)
		{
			_wasSpecialActive = false;
			Velocity = Vector3.Zero;
			_manualHeightOffset = 0f;
			_bombAttackArmed = true;
			_bombAttackStrengthPrev = 0f;
			return;
		}

		if (_visual == null)
		{
			Velocity = Vector3.Zero;
			return;
		}

		if (!_wasSpecialActive)
		{
			_wasSpecialActive = true;
			_manualHeightOffset = 0f;
			var spawn = _player.GlobalPosition + _player.GlobalBasis * ActivateSpawnOffset;
			float startY = spawn.Y + ActivateHeightAbovePlayer;
			GlobalPosition = new Vector3(spawn.X, startY, spawn.Z);
			_targetHoverY = startY;
			UpdateHoverTargetFromGround();
		}

		// Oikea tat Y — sama InputMap kuin kameran orbit (level 3:ssa kamera ei lue tätä side-scroll -tilassa).
		// Miinus: tat ylös = dronen nosto, alas = lasku (orbit-akseli oli päinvastoin).
		float lookY = -Input.GetAxis("cam_look_up", "cam_look_down");
		_manualHeightOffset += lookY * KorkeusSäätöNopeus * dt;
		_manualHeightOffset = Mathf.Clamp(_manualHeightOffset, KorkeusSäätöMinOffset, KorkeusSäätöMaxOffset);

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

		UpdateHoverTargetFromGround();
		float yVel = (GlobalPosition.Y - _targetHoverY) * (-HoverSmoothing);
		yVel = Mathf.Clamp(yVel, -MaxVerticalSpeed, MaxVerticalSpeed);
		Velocity = new Vector3(wish.X, yVel, wish.Z);
		MoveAndSlide();
		ClampHorizontalDistanceFromPlayer();

		if (_player.SyvyysliikeKäytössä)
		{
			Vector3 p = GlobalPosition;
			p.Z = Mathf.Clamp(p.Z, _player.SyvyysAlaraja, _player.SyvyysYläraja);
			GlobalPosition = p;
		}

		TryDropBomb();
		UpdateFacing(wish, dt);
	}

	private void UpdateHoverTargetFromGround()
	{
		var space = GetWorld3D()?.DirectSpaceState;
		if (space == null)
			return;

		var exclude = new Godot.Collections.Array<Rid>();
		exclude.Add(GetRid());
		if (_player != null && GodotObject.IsInstanceValid(_player))
			exclude.Add(_player.GetRid());

		bool TryRay(Vector3 from, Vector3 to, out float groundY)
		{
			groundY = 0f;
			var q = PhysicsRayQueryParameters3D.Create(from, to);
			q.CollisionMask = CollisionMask;
			q.Exclude = exclude;
			q.CollideWithAreas = false;
			var hit = space.IntersectRay(q);
			if (hit.Count > 0 && hit["position"].VariantType == Variant.Type.Vector3)
			{
				groundY = hit["position"].AsVector3().Y;
				return true;
			}
			return false;
		}

		var from = GlobalPosition + Vector3.Up * 0.5f;
		var to = GlobalPosition + Vector3.Down * 120f;
		if (TryRay(from, to, out float gy))
		{
			float want = gy + HoverGroundClearance;
			if (want > GlobalPosition.Y + 0.35f)
			{
				var fromHigh = GlobalPosition + Vector3.Up * 12f;
				if (TryRay(fromHigh, GlobalPosition + Vector3.Down * 140f, out gy))
					want = gy + HoverGroundClearance;
			}
			_targetHoverY = want + _manualHeightOffset;
			return;
		}

		if (TryRay(GlobalPosition + Vector3.Up * 25f, GlobalPosition + Vector3.Down * 180f, out gy))
		{
			_targetHoverY = gy + HoverGroundClearance + _manualHeightOffset;
			return;
		}

		if (_player != null && GodotObject.IsInstanceValid(_player))
			_targetHoverY = _player.GlobalPosition.Y + ActivateHeightAbovePlayer + _manualHeightOffset;
	}

	private void ClampHorizontalDistanceFromPlayer()
	{
		if (_player == null || !GodotObject.IsInstanceValid(_player) || MaxEtäisyysPelaajastaXZ <= 0f)
			return;
		var delta = GlobalPosition - _player.GlobalPosition;
		delta.Y = 0f;
		float max = MaxEtäisyysPelaajastaXZ;
		if (delta.LengthSquared() <= max * max)
			return;
		var clamped = _player.GlobalPosition + delta.Normalized() * max;
		GlobalPosition = new Vector3(clamped.X, GlobalPosition.Y, clamped.Z);
	}

	private int CountActiveBombs()
	{
		int n = 0;
		foreach (var node in GetTree().GetNodesInGroup("level3_drone_bomb"))
		{
			if (node is Level3DroneBomb b && GodotObject.IsInstanceValid(b) && b.IsInsideTree())
				n++;
		}
		return n;
	}

	private void TryDropBomb()
	{
		if (_bombCooldown > 0f)
			return;
		if (MaxAktiivisetPommit > 0 && CountActiveBombs() >= MaxAktiivisetPommit)
			return;

		float analog = _player != null && GodotObject.IsInstanceValid(_player)
			? _player.GetLightAttackTriggerAnalog()
			: Input.GetActionRawStrength("attack");
		if (analog < PommiLiipaisinPalautus)
			_bombAttackArmed = true;
		bool r2Key = Input.IsActionJustPressed("attack");
		bool r2Analog = _bombAttackArmed && analog >= PommiLiipaisinLaukaisu;
		bool r2SharpPull = analog >= PommiPikaVetoMinimi
			&& (analog - _bombAttackStrengthPrev) >= PommiPikaVetoNousu;
		_bombAttackStrengthPrev = analog;
		if (!r2Key && !r2Analog && !r2SharpPull)
			return;
		_bombAttackArmed = false;
		if (BombScene == null)
		{
			GD.PushWarning("Level3SpecialDrone: BombScene puuttuu — ei voi pudottaa.");
			return;
		}

		_bombCooldown = PommiJäähdytysSek;
		var parent = FindLevelRoot();
		if (parent == null)
		{
			GD.PushError("Level3SpecialDrone: Level3-juurta ei löydy.");
			return;
		}

		var bomb = BombScene.Instantiate<Level3DroneBomb>();
		parent.AddChild(bomb);
		bomb.GlobalPosition = GlobalPosition + GlobalTransform.Basis * PommiSpawnOffset;
		bomb.LinearVelocity = Velocity + Vector3.Down * 1.2f;
	}

	private Node FindLevelRoot()
	{
		for (Node n = GetParent(); n != null; n = n.GetParent())
		{
			if (n.Name == "Level3")
				return n;
		}
		return GetTree()?.CurrentScene;
	}

	private void UpdateFacing(Vector3 wishXZ, float dt)
	{
		if (_visual == null)
			return;
		Vector3 h = new(wishXZ.X, 0f, wishXZ.Z);
		if (h.LengthSquared() < 1e-4f)
			return;
		var dir = h.Normalized();
		float targetYaw = Basis.LookingAt(-dir, Vector3.Up).GetEuler(EulerOrder.Yxz).Y;
		float cur = _visual.Rotation.Y;
		float smooth = 1f - Mathf.Exp(-10f * dt);
		_visual.Rotation = new Vector3(0f, Mathf.LerpAngle(cur, targetYaw, smooth), 0f);
	}
}
