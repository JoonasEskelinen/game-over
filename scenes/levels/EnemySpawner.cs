using Godot;

public partial class EnemySpawner : Node3D
{
	[Export] public PackedScene EnemyScene;
	[Export] public float MinSpeed = 1.5f;
	[Export] public float MaxSpeed = 3.5f;

	private Vector3[] _spawnPoints = new Vector3[]
	{
		new(13, 0, 0),
		new(-13, 0, 0),
		new(0, 0, 13),
		new(0, 0, -13),
		new(10, 0, 10),
		new(-10, 0, 10),
		new(10, 0, -10),
		new(-10, 0, -10),
	};

	private int _wave = 1;
	private int _enemiesInWave = 0;
	private int _spawned = 0;
	private float _timer = 0f;
	private float _spawnInterval = 3.0f;
	private bool _waveInProgress = false;

	public override void _Ready()
	{
		StartWave(_wave);
	}

	public override void _Process(double delta)
	{
		// Tarkista onko aalto ohi
		if (_waveInProgress)
		{
			int aliveEnemies = GetTree().GetNodesInGroup("enemy").Count;
			if (aliveEnemies == 0 && _spawned >= _enemiesInWave)
			{
				_wave++;
				GD.Print("Aalto " + _wave + " alkaa!");
				StartWave(_wave);
			}
		}

		// Spawnaa vihollisia aaltoon
		if (_spawned < _enemiesInWave)
		{
			_timer += (float)delta;
			if (_timer >= _spawnInterval)
			{
				SpawnEnemy();
				_timer = 0f;
			}
		}
	}

	private void StartWave(int wave)
	{
		_spawned = 0;
		_waveInProgress = true;

		// Aalto 1: 1 vihollinen, Aalto 2: 2 vihollista jne.
		// Satunnainen nopeusvihollinen silloin tällöin
		_enemiesInWave = wave;
		_spawnInterval = Mathf.Max(1.5f, 4.0f - wave * 0.2f); // Nopeutuu aalloittain
	}

	private void SpawnEnemy()
	{
		if (EnemyScene == null) return;

		var enemy = EnemyScene.Instantiate() as CharacterBody3D;
		if (enemy == null) return;

		var point = _spawnPoints[GD.RandRange(0, _spawnPoints.Length - 1)];
		enemy.GlobalPosition = point;

		// Satunnainen nopeus — silloin tällöin nopea vihollinen
		var script = enemy as EnemyLevel1;
		if (script != null)
		{
			bool fastEnemy = GD.RandRange(0, 4) == 0; // 20% todennäköisyys
			script.Speed = fastEnemy
				? (float)GD.RandRange(MaxSpeed, MaxSpeed * 1.5f)
				: (float)GD.RandRange(MinSpeed, MaxSpeed);
		}

		enemy.AddToGroup("enemy");
		GetParent().AddChild(enemy);
		_spawned++;
	}
}
