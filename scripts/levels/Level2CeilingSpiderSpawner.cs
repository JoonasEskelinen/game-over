using Godot;

/// <summary>
/// Synnyttää putoavia hämähäkkejä level_2 putkeen satunnaisella Z:llä ja pelaajan X:n lähellä.
/// </summary>
public partial class Level2CeilingSpiderSpawner : Node3D
{
	[ExportGroup("Viittaukset")]
	[Export] public PackedScene SpiderScene;
	[Export] public NodePath PlayerPath = new("../Player");

	[ExportGroup("Synnytys")]
	[Export] public float SynnytysVäliMinSekuntia = 6f;
	[Export] public float SynnytysVäliMaxSekuntia = 14f;
	[Export] public int MaxHämähäkkejäKerrallaan = 3;
	[Export] public float SynnytysXSatunnainenMetri = 26f;

	[ExportGroup("Putki (maailmakoordinaatit)")]
	[Export] public float PutkiZPuolileveys = 3.35f;
	[Export] public float PutkiMinX = -119f;
	[Export] public float PutkiMaxX = -0.5f;
	[Export] public float KattoKorkeusY = 3.48f;
	[Export] public float UlkonaPutkessaYritäUudelleenSekuntia = 2.5f;

	private float _nextSpawnIn;
	private PlayerController _player;

	public override void _Ready()
	{
		if (SpiderScene == null)
			SpiderScene = GD.Load<PackedScene>("res://scenes/hazards/level2_ceiling_spider.tscn");
		_nextSpawnIn = (float)GD.RandRange(SynnytysVäliMinSekuntia, SynnytysVäliMaxSekuntia);
		TryCachePlayer();
	}

	public override void _Process(double delta)
	{
		if (SpiderScene == null)
			return;

		TryCachePlayer();
		if (_player == null || !GodotObject.IsInstanceValid(_player) || !_player.IsInsideTree())
			return;

		float px = _player.GlobalPosition.X;
		if (px < PutkiMinX || px > PutkiMaxX)
		{
			_nextSpawnIn = UlkonaPutkessaYritäUudelleenSekuntia;
			return;
		}

		_nextSpawnIn -= (float)delta;
		if (_nextSpawnIn > 0f)
			return;

		if (CountLiveSpiders() >= MaxHämähäkkejäKerrallaan)
		{
			_nextSpawnIn = 1.2f;
			return;
		}

		float spawnX = px + (float)GD.RandRange(-SynnytysXSatunnainenMetri, SynnytysXSatunnainenMetri);
		spawnX = Mathf.Clamp(spawnX, PutkiMinX, PutkiMaxX);
		float spawnZ = (float)GD.RandRange(-PutkiZPuolileveys, PutkiZPuolileveys);

		var spider = SpiderScene.Instantiate<Node3D>();
		GetParent().AddChild(spider);
		spider.GlobalPosition = new Vector3(spawnX, KattoKorkeusY, spawnZ);

		_nextSpawnIn = (float)GD.RandRange(SynnytysVäliMinSekuntia, SynnytysVäliMaxSekuntia);
	}

	private void TryCachePlayer()
	{
		if (_player != null && GodotObject.IsInstanceValid(_player) && _player.IsInsideTree())
			return;
		if (PlayerPath != default && GetNodeOrNull(PlayerPath) is PlayerController pc)
			_player = pc;
		else
			_player = GetTree().GetFirstNodeInGroup("player") as PlayerController;
	}

	private int CountLiveSpiders()
	{
		var tree = GetTree();
		if (tree == null)
			return 0;
		int c = 0;
		foreach (var n in tree.GetNodesInGroup("level2_ceiling_spider"))
		{
			if (!GodotObject.IsInstanceValid(n) || n.IsQueuedForDeletion())
				continue;
			c++;
		}
		return c;
	}
}
