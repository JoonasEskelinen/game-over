using System;
using Godot;

/// <summary>
/// Spawnaa EnemyLevel3-käärmeitä mäkeä pitkin; enintään 15 kokonaisuudessaan, vuorotellen (oletuksena max 1 elossa).
/// Spawn-piste: pelaajan ”edessä” X-suunnassa, Z satunnaisesti tien leveydelle; korkeus säteellä tien pintaan + jalkojen kohdistus.
/// </summary>
public partial class EnemyLevel3Spawner : Node3D
{
	[Export] public PackedScene EnemyScene;

	[Export] public float FirstSpawnAtPlayerX = -18f;
	[Export] public float SpawnEveryPlayerXMeters = 6.2f;

	/// <summary>Kuinka monta käärmettä spawnataan yhteensä (esim. 15).</summary>
	[Export] public int TotalEnemiesToSpawn = 15;

	/// <summary>1 = vain yksi käärme kerrallaan vuorille.</summary>
	[Export] public int MaxConcurrentEnemies = 1;

	[Export] public float MinPlayerX = -80f;
	[Export] public float MaxPlayerX = 88f;

	[Export] public float SpawnAheadOfPlayer = 14f;
	[Export] public float SpawnAheadVariance = 4f;

	/// <summary>Tien käytävän puoliväli metreinä (Z).</summary>
	[Export] public float SpawnZHalfRange = 2.2f;

	[Export] public float RaycastTopY = 45f;

	/// <summary>Pieni siirto osumapisteestä ulos pinnan normaalin suuntaan (ohut CSG-tie + kallistus).</summary>
	[Export] public float SurfaceBiasAlongNormal = 0.06f;

	/// <summary>Lisä siirto pintaa pitkin normaalin suuntaan (hienosäätö editorissa).</summary>
	[Export] public float SpawnGroundYOffset = 0.08f;

	[Export] public float MinWalkableNormalDotUp = 0.35f;

	private Node3D _player;
	private Node3D _roadRoot;
	private Node3D _uphillRoadDeck;
	private float _nextSpawnThresholdX;
	private int _totalSpawned;

	public override void _Ready()
	{
		_nextSpawnThresholdX = FirstSpawnAtPlayerX;
		_player = GetTree().GetFirstNodeInGroup("player") as Node3D;
		ResolveRoadGeometry();
	}

	private void ResolveRoadGeometry()
	{
		_roadRoot = GetParent()?.GetNodeOrNull<Node3D>("World/UphillRoadRoot");
		_uphillRoadDeck = Level3UphillRoadSurface.FindRoadPlaneNode(_roadRoot);
		if (_uphillRoadDeck == null || !Level3UphillRoadSurface.TryGetCsgBoxSize(_uphillRoadDeck, out _))
			GD.PushWarning("EnemyLevel3Spawner: UphillRoad CSGBox3D puuttuu tai size ei luettavissa — käytetään sädettä.");
	}

	public override void _Process(double delta)
	{
		if (EnemyScene == null) return;
		if (_totalSpawned >= TotalEnemiesToSpawn) return;

		if (_player == null || !GodotObject.IsInstanceValid(_player) || !_player.IsInsideTree())
		{
			_player = GetTree().GetFirstNodeInGroup("player") as Node3D;
			return;
		}

		float px = _player.GlobalPosition.X;
		if (px < MinPlayerX || px > MaxPlayerX)
			return;

		if (CountAliveEnemies() >= MaxConcurrentEnemies)
			return;

		if (px < _nextSpawnThresholdX)
			return;

		SpawnOneEnemy();
		_nextSpawnThresholdX += SpawnEveryPlayerXMeters;
	}

	private void SpawnOneEnemy()
	{
		float px = _player.GlobalPosition.X;
		float spawnX = px + SpawnAheadOfPlayer + (float)GD.RandRange(-SpawnAheadVariance, SpawnAheadVariance);
		float spawnZ = (float)GD.RandRange(-SpawnZHalfRange, SpawnZHalfRange);

		var enemy = EnemyScene.Instantiate<CharacterBody3D>();
		GetParent().AddChild(enemy);
		enemy.GlobalPosition = new Vector3(spawnX, _player.GlobalPosition.Y + 8f, spawnZ);

		Vector3 footTarget = ResolveSpawnFootWorld(spawnX, spawnZ);
		AlignCharacterFeetToWorldPoint(enemy, footTarget);

		_totalSpawned++;
		GD.Print($"EnemyLevel3Spawner: spawn {_totalSpawned}/{TotalEnemiesToSpawn} x≈{spawnX:F0} z≈{spawnZ:F1} y≈{enemy.GlobalPosition.Y:F2}.");
	}

	private Vector3 ResolveSpawnFootWorld(float spawnX, float spawnZ)
	{
		if (_uphillRoadDeck != null && GodotObject.IsInstanceValid(_uphillRoadDeck)
		    && Level3UphillRoadSurface.TrySampleTopFaceWorldAtXZ(_uphillRoadDeck, spawnX, spawnZ, out Vector3 deck, out Vector3 deckN))
			return deck + deckN * SurfaceBiasAlongNormal + deckN * SpawnGroundYOffset;

		var world = GetWorld3D();
		if (world == null)
			return new Vector3(spawnX, _player.GlobalPosition.Y, spawnZ);

		var from = new Vector3(spawnX, RaycastTopY, spawnZ);
		var to = from + Vector3.Down * 120f;

		var exclude = new Godot.Collections.Array<Rid>();
		if (_player is CollisionObject3D pco)
			exclude.Add(pco.GetRid());

		if (!TryRaycastRoadSurface(world.DirectSpaceState, from, to, exclude, out Vector3 hitPos, out Vector3 hitNormal))
			return new Vector3(spawnX, _player.GlobalPosition.Y, spawnZ);

		Vector3 n = hitNormal.Normalized();
		if (n.Dot(Vector3.Up) < 0f)
			n = -n;
		return hitPos + n * SurfaceBiasAlongNormal + n * SpawnGroundYOffset;
	}

	private bool TryRaycastRoadSurface(
		PhysicsDirectSpaceState3D space,
		Vector3 from,
		Vector3 to,
		Godot.Collections.Array<Rid> exclude,
		out Vector3 hitPos,
		out Vector3 hitNormal)
	{
		hitPos = default;
		hitNormal = Vector3.Up;

		for (int attempt = 0; attempt < 16; attempt++)
		{
			var query = PhysicsRayQueryParameters3D.Create(from, to);
			query.CollideWithAreas = false;
			query.CollideWithBodies = true;
			query.CollisionMask = 0xFFFF_FFFFu;
			query.Exclude = exclude;

			var hit = space.IntersectRay(query);
			if (hit.Count == 0 || !hit.TryGetValue("position", out var posObj))
				return false;

			hitPos = (Vector3)posObj;
			hitNormal = hit.TryGetValue("normal", out var nrmObj)
				? ((Vector3)nrmObj).Normalized()
				: Vector3.Up;

			if (hitNormal.Dot(Vector3.Up) >= MinWalkableNormalDotUp && Level3UphillRoadSurface.ColliderIsLevel3RoadDeck(hit))
				return true;

			if (hit.TryGetValue("collider", out var colVar) && colVar.Obj is CollisionObject3D co)
				exclude.Add(co.GetRid());
			else
				return false;
		}

		return false;
	}


	/// <summary>
	/// Siirtää hahmoa niin että kapselin alin piste (maailmakoordinaatit) osuu <paramref name="targetFootWorld"/>-kohtaan.
	/// Kaltevalla tiellä pelkkä Y-korjaus vääristää sijainnin.
	/// </summary>
	private static void AlignCharacterFeetToWorldPoint(CharacterBody3D body, Vector3 targetFootWorld)
	{
		Vector3 foot = GetCapsuleBottomWorld(body);
		body.GlobalPosition += targetFootWorld - foot;
	}

	private static Vector3 GetCapsuleBottomWorld(CharacterBody3D body)
	{
		if (body.GetNodeOrNull("CollisionShape3D") is not CollisionShape3D shapeNode)
			return body.GlobalPosition;
		if (shapeNode.Shape is not CapsuleShape3D cap)
			return body.GlobalPosition;

		Transform3D gt = shapeNode.GlobalTransform;
		float half = cap.Height * 0.5f;
		Vector3 yAxis = gt.Basis.Y;
		if (yAxis.LengthSquared() < 1e-8f)
			return body.GlobalPosition;
		yAxis = yAxis.Normalized();
		return gt.Origin - yAxis * half;
	}

	private int CountAliveEnemies()
	{
		var tree = GetTree();
		if (tree == null) return 0;

		int c = 0;
		foreach (var n in tree.GetNodesInGroup("enemy_level3"))
		{
			if (!GodotObject.IsInstanceValid(n) || n.IsQueuedForDeletion()) continue;
			if (n is EnemyLevel3 e && e.IsAliveForSpawner())
				c++;
		}
		return c;
	}
}
