using Godot;

public partial class EnemySpawner : Node3D
{
	[Export] public PackedScene EnemyScene;
	[Export] public float MinSpeed = 1.5f;
	[Export] public float MaxSpeed = 3.5f;
	/// <summary>Kaikki normiviholliset (ryhmä enemy) ennen bossia.</summary>
	[Export] public int TotalNormalEnemiesToSpawn = 20;

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

	private int _enemiesInWave = 0;
	private int _spawned = 0;
	private float _timer = 0f;
	private float _spawnInterval = 2.2f;
	private bool _waveInProgress = false;

	public override void _Ready()
	{
		StartLevelSpawns();
	}

	/// <summary>Kaikki säännellyt viholliset spawattu ja ryhmässä enemy ei ole ketään (boss ei ole tässä ryhmässä).</summary>
	public bool IsNormalEncounterComplete()
	{
		if (!_waveInProgress)
			return false;
		int alive = GetTree().GetNodesInGroup("enemy").Count;
		return _spawned >= _enemiesInWave && alive == 0;
	}

	public override void _Process(double delta)
	{
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

	private void StartLevelSpawns()
	{
		_spawned = 0;
		_waveInProgress = true;
		_enemiesInWave = Mathf.Max(1, TotalNormalEnemiesToSpawn);
		_spawnInterval = Mathf.Max(1.2f, 5.0f - _enemiesInWave * 0.12f);
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
