using System.Threading.Tasks;
using Godot;

/// <summary>
/// Areenan aaltotehtävä: spawnaa vihollisia merkkipisteistä, odottaa kuolemat, avaa oven lopussa.
/// </summary>
public partial class ArenaWaveDirector : Node
{
	[Export] public PackedScene EnemyScene;
	[Export] public bool AutoStart = true;
	// Polut suhteessa *ArenaWaveDirector*-nodeen (worldin lapsi → sisarukset ../-alkuisina).
	[Export] public NodePath EnemiesRootPath = new("../Enemies");
	[Export] public NodePath DoorExitPath = new("../Triggers/LevelExit");
	[Export] public NodePath SpawnRightPath = new("../Spawns/SpawnRight");
	[Export] public NodePath SpawnLeftPath = new("../Spawns/SpawnLeft");
	[Export] public NodePath SpawnRight2Path = new("../Spawns/SpawnRight2");
	[Export] public NodePath SpawnLeft2Path = new("../Spawns/SpawnLeft2");
	[Export] public NodePath SpawnBossPath = new("../Spawns/SpawnBoss");

	private Node3D _enemiesRoot;
	private LevelExit _door;
	private Node3D _spawnR;
	private Node3D _spawnL;
	private Node3D _spawnR2;
	private Node3D _spawnL2;
	private Node3D _spawnBoss;

	private TaskCompletionSource<bool> _waveClear;
	private int _aliveInWave;

	public override void _Ready()
	{
		Checkpoint.ResetToDefault(new Vector3(0f, 1.2f, 0f));

		if (EnemyScene == null)
			EnemyScene = GD.Load<PackedScene>("res://scenes/enemies/EnemyBasic.tscn");

		_enemiesRoot = GetNodeOrNull<Node3D>(EnemiesRootPath);
		_door = GetNodeOrNull<LevelExit>(DoorExitPath);
		_spawnR = GetNodeOrNull<Node3D>(SpawnRightPath);
		_spawnL = GetNodeOrNull<Node3D>(SpawnLeftPath);
		_spawnR2 = GetNodeOrNull<Node3D>(SpawnRight2Path);
		_spawnL2 = GetNodeOrNull<Node3D>(SpawnLeft2Path);
		_spawnBoss = GetNodeOrNull<Node3D>(SpawnBossPath);

		if (_enemiesRoot == null || _door == null || _spawnR == null || _spawnL == null || _spawnBoss == null)
		{
			GD.PrintErr("ArenaWaveDirector: puuttuvia nodeja (tarkista polut Inspectorissa). " +
				$"Enemies={EnemiesRootPath}, Door={DoorExitPath}, R/L/Boss spawns.");
			return;
		}

		foreach (var child in _enemiesRoot.GetChildren())
			child.QueueFree();

		_door.Monitoring = false;

		if (AutoStart)
			_ = RunArenaAsync();
	}

	private async Task RunArenaAsync()
	{
		await Delay(0.9f);

		await RunWave1();
		await Delay(1.4f);
		await RunWave2();
		await Delay(1.4f);
		await RunWave3();
		await Delay(1.4f);
		await RunWave4();
		await Delay(1.6f);
		await RunWaveBoss();

		_door.Monitoring = true;
		GD.Print("Areena läpi — ovi auki.");
	}

	private async Task RunWave1()
	{
		BeginWave();
		SpawnEnemy(_spawnR, 2f, 1, 1f);
		await _waveClear.Task;
	}

	private async Task RunWave2()
	{
		BeginWave();
		SpawnEnemy(_spawnL, 3.6f, 1, 1f);
		await _waveClear.Task;
	}

	private async Task RunWave3()
	{
		BeginWave();
		SpawnEnemy(_spawnR, 2.4f, 1, 1f);
		SpawnEnemy(_spawnL, 4.2f, 1, 1f);
		await _waveClear.Task;
	}

	private async Task RunWave4()
	{
		BeginWave();
		SpawnEnemy(_spawnR, 2.9f, 1, 1f);
		await Delay(0.4f);
		SpawnEnemy(_spawnL, 3.4f, 1, 1f);
		await Delay(0.4f);
		if (_spawnR2 != null)
			SpawnEnemy(_spawnR2, 3.1f, 1, 1f);
		else
			SpawnEnemy(_spawnR, 3.1f, 1, 1f);
		await Delay(0.4f);
		if (_spawnL2 != null)
			SpawnEnemy(_spawnL2, 3.7f, 1, 1f);
		else
			SpawnEnemy(_spawnL, 3.7f, 1, 1f);
		await _waveClear.Task;
	}

	private async Task RunWaveBoss()
	{
		BeginWave();
		SpawnEnemy(_spawnBoss, 1.65f, 36, 1.5f);
		await _waveClear.Task;
	}

	private void BeginWave()
	{
		_waveClear = new TaskCompletionSource<bool>();
		_aliveInWave = 0;
	}

	private void SpawnEnemy(Node3D marker, float speed, int hp, float scaleMul)
	{
		var e = EnemyScene.Instantiate<EnemyBasic>();
		e.MoveSpeed = speed;
		e.MaxHealth = hp;
		e.Scale = Vector3.One * scaleMul;
		e.Died += OnEnemyDied;
		_enemiesRoot.AddChild(e);
		e.GlobalPosition = marker.GlobalPosition;
		_aliveInWave++;
	}

	private void OnEnemyDied()
	{
		_aliveInWave--;
		if (_aliveInWave <= 0)
			_waveClear?.TrySetResult(true);
	}

	private async Task Delay(float seconds)
	{
		await ToSignal(GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);
	}
}
