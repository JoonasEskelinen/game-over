using Godot;

public partial class PlayerController : CharacterBody3D
{
	/// <summary>Kävelynopeus (miekka+kilpi kävely). Juoksu normaalitilassa käyttää RunSpeed.</summary>
	[Export] public float Speed = 4.0f;
	[Export] public float RunSpeed = 8.5f;
	[Export] public float JumpVelocity = 10.0f;
	[Export] public float Gravity = 20.0f;
	/// <summary>Little Nightmares -tyylinen syvyys (Z): vasen tatti Y / W-S. Rajat suhteessa tien leveyteen.</summary>
	[Export] public bool DepthMovementEnabled = true;
	[Export] public float DepthClampMin = -1.75f;
	[Export] public float DepthClampMax = 1.75f;
	/// <summary>Ohjain: GetAxis(move_back, move_forward). +1 → negatiivinen Z (kauemmas kamerasta).</summary>
	[Export] public float DepthInputSign = -1f;
	/// <summary>0 = välitön kääntyminen. Isompi arvo = pehmeämpi pyörähdys (XZ-liikesuunta).</summary>
	[Export] public float FacingSmoothing { get; set; } = 16f;
	// Säädä tätä arvoa kunnes ponnistus täsmää animaatioon (sekunteina)
	[Export] public float JumpWindupTime = 0.35f;

	// Ase-tila: Normal = ei asetta, SwordShield = miekka+kilpi, Sitting = istuminen/drone
	private enum WeaponState { Normal, SwordShield, Sitting }
	private WeaponState _weaponState = WeaponState.Normal;

	private MeshInstance3D _mesh;
	private Node3D _characterModel;
	private AnimationPlayer _animationPlayer;
	private HealthComponent _healthComponent;
	private bool _isAttacking = false;
	private bool _isBlocking = false;
	private bool _isWindingUp = false;
	private float _jumpTimer = 0f;
	private float _facingYaw;

	public override void _Ready()
	{
		FloorSnapLength = 0.18f;
		FloorMaxAngle = Mathf.DegToRad(50f);

		_mesh = GetNode<MeshInstance3D>("MeshInstance3D");
		_characterModel = GetNode<Node3D>("gameover_character");
		_healthComponent = GetNode<HealthComponent>("HealthComponent");
		_healthComponent.HealthChanged += OnHealthChanged;
		_healthComponent.PlayerDied += OnPlayerDied;

		_facingYaw = _characterModel.Rotation.Y;

		_animationPlayer = _characterModel.FindChild("AnimationPlayer", true, false) as AnimationPlayer;
		if (_animationPlayer != null)
		{
			LoadAnim("res://assets/models/animations/Jumping.fbx",                            "mixamo_com", "mixamo_com_001");
			LoadAnim("res://assets/models/animations/Orc Walk.fbx",                           "mixamo_com", "mixamo_com_002");
			LoadAnim("res://assets/models/animations/Running.fbx",                            "mixamo_com", "mixamo_com_003");
			LoadAnim("res://assets/models/animations/sitting.fbx",                            "mixamo_com", "mixamo_com_004");
			LoadAnim("res://assets/models/animations/Sword And Shield Attack.fbx",            "mixamo_com", "mixamo_com_005");
			LoadAnim("res://assets/models/animations/Sword And Shield Crouch Block Idle.fbx", "mixamo_com", "mixamo_com_006");
			LoadAnim("res://assets/models/animations/Sword And Shield Idle.fbx",              "mixamo_com", "mixamo_com_007");
			LoadAnim("res://assets/models/animations/Sword And Shield Run.fbx",               "mixamo_com", "mixamo_com_008");
			LoadAnim("res://assets/models/animations/Sword And Shield Walk.fbx",              "mixamo_com", "mixamo_com_009");

			_animationPlayer.AnimationFinished += OnAnimationFinished;
			PlayAnim("mixamo_com");
		}

		bool firstMeshFound = false;
		foreach (Node armature in _characterModel.GetChildren())
		{
			if (armature is Node3D)
			{
				foreach (Node child in armature.GetChildren())
				{
					foreach (Node meshNode in child.GetChildren())
					{
						if (meshNode.Name.ToString().StartsWith("tripo_node"))
						{
							if (!firstMeshFound)
							{
								firstMeshFound = true;
								GD.Print("Mesh näkyvissä: " + meshNode.Name);
							}
							else if (meshNode is Node3D meshNode3D)
							{
								meshNode3D.Visible = false;
							}
						}
					}
				}
			}
		}
	}

	public override void _PhysicsProcess(double delta)
	{
		Vector3 velocity = Velocity;

		if (!IsOnFloor())
			velocity.Y -= Gravity * (float)delta;

		// Hyppy — vain normaalitilassa tai miekka+kilpi-tilassa
		bool canJump = _weaponState != WeaponState.Sitting;

		// Laske viiveajastin — laukaisee hypyn ponnistuksen jälkeen
		if (_isWindingUp)
		{
			_jumpTimer -= (float)delta;
			if (_jumpTimer <= 0f)
			{
				_isWindingUp = false;
				velocity.Y = JumpVelocity;
			}
		}

		if (Input.IsActionJustPressed("jump") && IsOnFloor() && !_isAttacking && !_isBlocking && canJump && !_isWindingUp)
		{
			PlayAnim("mixamo_com_001");
			_isWindingUp = true;
			_jumpTimer = JumpWindupTime;
		}

		// Kolmio: kierrätä ase-tilaa Normal → SwordShield → Sitting → Normal
		if (Input.IsActionJustPressed("toggle_weapon"))
		{
			_weaponState = (WeaponState)(((int)_weaponState + 1) % 3);
			_isAttacking = false;
			_isBlocking = false;
			switch (_weaponState)
			{
				case WeaponState.Normal:
					PlayAnim("mixamo_com");
					break;
				case WeaponState.SwordShield:
					PlayAnim("mixamo_com_007");
					break;
				case WeaponState.Sitting:
					PlayAnim("mixamo_com_004");
					break;
			}
		}

		// Suojaus L2 — vain miekka+kilpi-tilassa
		_isBlocking = Input.IsActionPressed("block") && _weaponState == WeaponState.SwordShield;
		if (_isBlocking)
			PlayAnim("mixamo_com_006");

		// Lyönti R2 — vain miekka+kilpi-tilassa
		if (Input.IsActionJustPressed("attack") && _weaponState == WeaponState.SwordShield && !_isBlocking)
		{
			_isAttacking = true;
			PlayAnim("mixamo_com_005");
		}

		// Liike — istumistilassa tai kilpi pohjassa ei liikuta
		bool canMove = _weaponState != WeaponState.Sitting && !_isBlocking;
		float dirX = canMove ? Input.GetAxis("move_left", "move_right") : 0f;
		float dirZ = 0f;
		if (canMove && DepthMovementEnabled)
			dirZ = DepthInputSign * Input.GetAxis("move_back", "move_forward");

		Vector2 planarInput = new(dirX, dirZ);
		float moveSpeed = Speed;
		if (canMove && planarInput.LengthSquared() > 1e-6f && _weaponState == WeaponState.Normal)
			moveSpeed = RunSpeed;

		Vector3 wish = Vector3.Zero;
		if (planarInput.LengthSquared() > 1e-6f)
		{
			planarInput = planarInput.Normalized();
			wish = new Vector3(planarInput.X, 0f, planarInput.Y) * moveSpeed;
		}

		if (IsOnFloor() && !_isWindingUp)
			wish = wish.Slide(GetFloorNormal());

		velocity.X = wish.X;
		velocity.Z = wish.Z;
		if (IsOnFloor() && !_isWindingUp && velocity.Y < JumpVelocity * 0.25f)
			velocity.Y = wish.Y;

		// Kääntyminen: täysi 360° liikkeen suuntaan XZ-tasossa (esim. kameraan päin = +Z / -Z riippuen inputista).
		Vector3 wishHorizontal = new(wish.X, 0f, wish.Z);
		if (wishHorizontal.LengthSquared() > 1e-5f)
		{
			var dir = wishHorizontal.Normalized();
			// Mixamo / lapsi-malli: etenemissuunta vastakkaisena kuin Godot LookingAt(-Z).
			var targetYaw = Basis.LookingAt(-dir, Vector3.Up).GetEuler(EulerOrder.Yxz).Y;
			float dt = (float)delta;
			if (FacingSmoothing <= 0.01f)
				_facingYaw = targetYaw;
			else
				_facingYaw = Mathf.LerpAngle(_facingYaw, targetYaw, 1f - Mathf.Exp(-FacingSmoothing * dt));
			_characterModel.Rotation = new Vector3(0f, _facingYaw, 0f);
		}

		// Liike-animaatiot
		// Ei päällekirjoiteta hyppy- tai hyökkäysanimaatioita
		bool jumpPlaying = _animationPlayer?.CurrentAnimation == "mixamo_com_001" || _isWindingUp;
		if (IsOnFloor() && !_isAttacking && !_isBlocking && !jumpPlaying)
		{
			string target;
			switch (_weaponState)
			{
				case WeaponState.SwordShield:
					// Miekka+kilpi: kävely tai idle (miekka+kilpi kävelyllä on oma animaatio)
					target = planarInput.LengthSquared() > 0.01f ? "mixamo_com_009" : "mixamo_com_007";
					break;
				case WeaponState.Sitting:
					// Istuminen: aina istumisanimaatio
					target = "mixamo_com_004";
					break;
				default:
					// Normaali: juoksu tai idle (myös syvyys Z)
					target = planarInput.LengthSquared() > 0.01f ? "mixamo_com_003" : "mixamo_com";
					break;
			}
			PlayAnim(target);
		}

		if (Input.IsActionJustPressed("test_damage"))
			_healthComponent.TakeDamage(1);

		Velocity = velocity;
		MoveAndSlide();

		if (DepthMovementEnabled)
		{
			Vector3 p = GlobalPosition;
			p.Z = Mathf.Clamp(p.Z, DepthClampMin, DepthClampMax);
			GlobalPosition = p;
		}
	}

	private void LoadAnim(string path, string sourceName, string targetName)
	{
		var scene = GD.Load<PackedScene>(path);
		if (scene == null)
		{
			GD.PrintErr($"Animaatiotiedostoa ei löydy: {path}");
			return;
		}
		var inst = scene.Instantiate();
		var ap = inst.FindChild("AnimationPlayer", true, false) as AnimationPlayer;
		if (ap == null)
		{
			GD.PrintErr($"AnimationPlayer puuttuu: {path}");
			inst.QueueFree();
			return;
		}

		Animation anim = null;
		foreach (var name in new[] { sourceName, sourceName.Replace("_", ".") })
		{
			if (ap.HasAnimation(name))
			{
				anim = ap.GetAnimation(name);
				break;
			}
		}

		if (anim == null)
		{
			GD.PrintErr($"Animaatiota '{sourceName}' ei löydy: {path}");
			GD.Print($"  Saatavilla: {string.Join(", ", ap.GetAnimationList())}");
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

		if (lib.HasAnimation(targetName))
			lib.RemoveAnimation(targetName);
		lib.AddAnimation(targetName, anim);
		GD.Print($"Ladattu animaatio: {targetName}");
		inst.QueueFree();
	}

	private void PlayAnim(string name)
	{
		if (_animationPlayer == null) return;
		if (_animationPlayer.CurrentAnimation == name) return;
		_animationPlayer.Play(name);
	}

	private void OnAnimationFinished(StringName animName)
	{
		if (animName == "mixamo_com_005")
			_isAttacking = false;
		if (animName == "mixamo_com_001")
			PlayAnim("mixamo_com");
	}

	// Respawn — kutsutaan KillZone:sta tai Game Over -tilanteesta
	public void Respawn(Vector3 position)
	{
		GlobalPosition = position;
		Velocity = Vector3.Zero;
		_isAttacking = false;
		_isBlocking = false;
		_isWindingUp = false;
		_jumpTimer = 0f;
		_weaponState = WeaponState.Normal;
		PlayAnim("mixamo_com");
	}

	private void OnHealthChanged(int currentHealth, int maxHealth)
	{
		GD.Print($"UI päivitys: {currentHealth}/{maxHealth}");
	}

	private void OnPlayerDied()
	{
		GD.Print("Game Over!");
		Respawn(Checkpoint.LastPosition);
	}
}
