using Godot;

public partial class PlayerController : CharacterBody3D
{
	[Export] public float Speed = 5.0f;
	[Export] public float JumpVelocity = 8.0f;
	[Export] public float Gravity = 20.0f;
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
	private float _lastDirection = 1.0f;
	private bool _isWindingUp = false;
	private float _jumpTimer = 0f;

	public override void _Ready()
	{
		_mesh = GetNode<MeshInstance3D>("MeshInstance3D");
		_characterModel = GetNode<Node3D>("gameover_character");
		_healthComponent = GetNode<HealthComponent>("HealthComponent");
		_healthComponent.HealthChanged += OnHealthChanged;
		_healthComponent.PlayerDied += OnPlayerDied;

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

		// Liike — istumistilassa hahmo ei liiku (hallitsee dronea myöhemmin)
		float direction = _weaponState == WeaponState.Sitting ? 0f : Input.GetAxis("move_left", "move_right");
		velocity.X = direction * Speed;

		// Kääntyminen
		if (direction > 0) _lastDirection = 1.0f;
		else if (direction < 0) _lastDirection = -1.0f;

		float yaw = _lastDirection > 0 ? Mathf.DegToRad(90) : Mathf.DegToRad(-90);
		_characterModel.Rotation = new Vector3(0, yaw, 0);

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
					target = Mathf.Abs(direction) > 0.1f ? "mixamo_com_009" : "mixamo_com_007";
					break;
				case WeaponState.Sitting:
					// Istuminen: aina istumisanimaatio
					target = "mixamo_com_004";
					break;
				default:
					// Normaali: juoksu tai idle
					target = Mathf.Abs(direction) > 0.1f ? "mixamo_com_003" : "mixamo_com";
					break;
			}
			PlayAnim(target);
		}

		if (Input.IsActionJustPressed("test_damage"))
			_healthComponent.TakeDamage(1);

		Velocity = velocity;
		MoveAndSlide();
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

	private void OnHealthChanged(int currentHealth, int maxHealth)
	{
		GD.Print($"UI päivitys: {currentHealth}/{maxHealth}");
	}

	private void OnPlayerDied()
	{
		GD.Print("Game Over!");
	}
}
