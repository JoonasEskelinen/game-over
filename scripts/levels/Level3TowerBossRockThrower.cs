using Godot;

/// <summary>
/// Level 3: tornin katolla oleva hahmo (stattinen glb) heittää samaa <see cref="RollingRockLevel3"/>-kiveä
/// kuin mäen vieritys, kohti pelaajaa satunnaisella XZ-leviämällä.
/// </summary>
public partial class Level3TowerBossRockThrower : Node3D
{
	[Export] public PackedScene RollingRockScene;

	[ExportGroup("Heitto")]
	[Export] public float HeittoVäliMinSek = 2.0f;
	[Export] public float HeittoVäliMaxSek = 4.2f;

	[Export] public float Horisontaalinopeus = 19f;
	[Export] public float YlösKomponentti = 6.5f;

	[Export] public float TähtäysSatunnaisuusX = 2.8f;
	[Export] public float TähtäysSatunnaisuusZ = 1.6f;

	[ExportGroup("Pyöriminen")]
	[Export] public float KulmanopeusMin = 2f;
	[Export] public float KulmanopeusMax = 7f;

	private Timer _timer;
	private Node3D _player;

	public override void _Ready()
	{
		_timer = new Timer { OneShot = true, Autostart = false };
		_timer.Timeout += OnThrowTimer;
		AddChild(_timer);
		_timer.WaitTime = (float)GD.RandRange(HeittoVäliMinSek, HeittoVäliMaxSek);
		_timer.Start();
	}

	private void OnThrowTimer()
	{
		TryThrowOne();
		_timer.WaitTime = (float)GD.RandRange(HeittoVäliMinSek, HeittoVäliMaxSek);
		_timer.Start();
	}

	private void TryThrowOne()
	{
		if (RollingRockScene == null)
		{
			GD.PushWarning("Level3TowerBossRockThrower: RollingRockScene puuttuu.");
			return;
		}

		var tree = GetTree();
		if (tree == null)
			return;

		_player = tree.GetFirstNodeInGroup("player") as Node3D;
		if (_player == null || !GodotObject.IsInstanceValid(_player) || !_player.IsInsideTree())
			return;

		Node inst = RollingRockScene.Instantiate();
		if (inst is not RollingRockLevel3 rock)
		{
			GD.PushError($"Level3TowerBossRockThrower: juuri ei ole RollingRockLevel3 ({inst?.GetType().Name}).");
			inst?.QueueFree();
			return;
		}

		Node rockParent = FindRockParent();
		if (rockParent == null)
		{
			GD.PushError("Level3TowerBossRockThrower: ei löydy Level3-juurta kiven parentiksi.");
			rock.QueueFree();
			return;
		}

		Vector3 spawn = GlobalPosition;
		Vector3 target = _player.GlobalPosition;
		target.X += (float)GD.RandRange(-TähtäysSatunnaisuusX, TähtäysSatunnaisuusX);
		target.Z += (float)GD.RandRange(-TähtäysSatunnaisuusZ, TähtäysSatunnaisuusZ);

		Vector3 horiz = new Vector3(target.X - spawn.X, 0f, target.Z - spawn.Z);
		if (horiz.LengthSquared() < 0.04f)
			horiz = new Vector3(-1f, 0f, 0f);
		horiz = horiz.Normalized();

		float up = YlösKomponentti * (float)GD.RandRange(0.85, 1.12f);
		float hSpeed = Horisontaalinopeus * (float)GD.RandRange(0.88, 1.08f);
		Vector3 linear = horiz * hSpeed + Vector3.Up * up;

		Vector3 axis = new Vector3(
			(float)GD.RandRange(-1f, 1f),
			(float)GD.RandRange(-1f, 1f),
			(float)GD.RandRange(-1f, 1f));
		if (axis.LengthSquared() < 1e-4f)
			axis = Vector3.Up;
		axis = axis.Normalized();
		float omega = (float)GD.RandRange(KulmanopeusMin, KulmanopeusMax);
		Vector3 angular = axis * omega;

		rockParent.AddChild(rock);
		rock.GlobalPosition = spawn;
		rock.LaunchProjectile(linear, angular);
	}

	private Node FindRockParent()
	{
		for (Node n = GetParent(); n != null; n = n.GetParent())
		{
			if (n.Name == "Level3")
				return n;
		}
		return GetTree()?.CurrentScene;
	}
}
