using System;
using Godot;

/// <summary>
/// Level 3: kiviä mäkeä alas Timerilla (ei riipu _Processista). RockScene + UphillRoadRoot pakolliset.
/// </summary>
public partial class Level3RollingRockSpawner : Node3D
{
	[Export] public PackedScene RockScene;

	[Export] public NodePath RoadRootPath;

	[Export] public float MinSpawnIntervalSeconds = 2.2f;
	[Export] public float MaxSpawnIntervalSeconds = 5.5f;

	[Export] public float SpawnUphillMin = 16f;
	[Export] public float SpawnUphillMax = 34f;

	[Export] public float LateralHalfWidth = 2.0f;

	[Export] public float RaycastTopY = 70f;
	[Export] public float SpawnHeightAboveHit = 0.55f;

	[Export] public float MinWalkableNormalDotUp = 0.32f;

	private Node3D _player;
	private Node3D _roadRoot;
	private Timer _timer;

	public override void _Ready()
	{
		_timer = new Timer { OneShot = true, Autostart = false };
		_timer.Timeout += OnSpawnTimer;
		AddChild(_timer);

		Callable.From(DeferredBootstrap).CallDeferred();
	}

	private void DeferredBootstrap()
	{
		var tree = GetTree();
		if (tree == null)
		{
			GD.PushError("Level3RollingRockSpawner: SceneTree puuttuu (DeferredBootstrap).");
			return;
		}

		_player = tree.GetFirstNodeInGroup("player") as Node3D;
		ResolveRoadRoot();

		if (RockScene == null)
		{
			GD.PushError("Level3RollingRockSpawner: RockScene puuttuu (PackedScene).");
			return;
		}

		if (_roadRoot == null)
		{
			GD.PushError("Level3RollingRockSpawner: UphillRoadRoot ei löydy — tarkista scene-puu.");
			return;
		}

		_timer.WaitTime = 1.0f;
		_timer.Start();
	}

	private void OnSpawnTimer()
	{
		if (RockScene == null || _roadRoot == null)
			return;

		var tree = GetTree();
		if (tree == null)
			return;
		_player = tree.GetFirstNodeInGroup("player") as Node3D;
		SpawnOneRock();

		_timer.WaitTime = (float)GD.RandRange(MinSpawnIntervalSeconds, MaxSpawnIntervalSeconds);
		_timer.Start();
	}

	private void ResolveRoadRoot()
	{
		_roadRoot = null;
		if (!IsInsideTree())
			return;

		try
		{
			if (!RoadRootPath.IsEmpty && HasNode(RoadRootPath))
				_roadRoot = GetNodeOrNull<Node3D>(RoadRootPath);
		}
		catch (Exception e)
		{
			GD.PushWarning($"Level3RollingRockSpawner: RoadRootPath virheellinen: {e.Message}");
		}

		if (_roadRoot == null)
			_roadRoot = GetParent()?.GetNodeOrNull<Node3D>("World/UphillRoadRoot");

		SceneTree tree = GetTree();
		if (_roadRoot == null && tree?.Root != null)
			_roadRoot = FindUphillRoadRootRecursive(tree.Root);
	}

	private static Node3D FindUphillRoadRootRecursive(Node node)
	{
		if (node == null)
			return null;
		if (node.Name == "UphillRoadRoot" && node is Node3D d)
			return d;
		foreach (Node child in node.GetChildren())
		{
			Node3D found = FindUphillRoadRootRecursive(child);
			if (found != null)
				return found;
		}
		return null;
	}

	private void SpawnOneRock()
	{
		Vector3 downhill = ComputeDownhillOnRoad(_roadRoot);
		Vector3 uphill = -downhill;
		Vector3 lateral = _roadRoot.GlobalTransform.Basis.Z.Normalized();

		float up = (float)GD.RandRange(SpawnUphillMin, SpawnUphillMax);
		float side = (float)GD.RandRange(-LateralHalfWidth, LateralHalfWidth);

		Vector3 basePos = (_player != null && GodotObject.IsInstanceValid(_player) && _player.IsInsideTree())
			? _player.GlobalPosition
			: _roadRoot.GlobalPosition;

		Vector3 anchor = basePos + uphill * up + lateral * side;
		Vector3? hitPos = TryRaycastRoadTop(anchor, _player);

		Vector3 spawn;
		if (hitPos.HasValue)
			spawn = hitPos.Value + Vector3.Up * SpawnHeightAboveHit;
		else
		{
			Vector3 localOnRamp = _roadRoot.GlobalTransform.Basis.Inverse() * (anchor - _roadRoot.GlobalPosition);
			localOnRamp = new Vector3(localOnRamp.X, 0.42f, Mathf.Clamp(localOnRamp.Z, -LateralHalfWidth, LateralHalfWidth));
			spawn = _roadRoot.ToGlobal(localOnRamp);
		}

		Node inst = RockScene.Instantiate();
		if (inst is not RollingRockLevel3 rock)
		{
			GD.PushError($"Level3RollingRockSpawner: juuri ei ole RollingRockLevel3 (oli {inst?.GetType().Name}).");
			inst?.QueueFree();
			return;
		}

		float speedScale = (float)GD.RandRange(0.85, 1.15);
		rock.PendingRoadRoot = _roadRoot;
		rock.PendingSpeedScale = speedScale;

		Node parent = GetParent();
		if (parent == null)
		{
			rock.QueueFree();
			return;
		}

		parent.AddChild(rock);
		rock.GlobalPosition = spawn;
	}

	private static Vector3 ComputeDownhillOnRoad(Node3D roadRoot)
	{
		Basis b = roadRoot.GlobalTransform.Basis;
		Vector3 n = b.Y.Normalized();
		Vector3 downhill = Vector3.Down - n * Vector3.Down.Dot(n);
		if (downhill.LengthSquared() < 1e-6f)
			downhill = -b.X;
		return downhill.Normalized();
	}

	private Vector3? TryRaycastRoadTop(Vector3 worldAnchor, Node3D player)
	{
		var world = GetWorld3D();
		if (world == null)
			return null;

		var from = new Vector3(worldAnchor.X, RaycastTopY, worldAnchor.Z);
		var to = from + Vector3.Down * 220f;

		var exclude = new Godot.Collections.Array<Rid>();
		if (player is CollisionObject3D pco)
			exclude.Add(pco.GetRid());

		for (int attempt = 0; attempt < 14; attempt++)
		{
			var query = PhysicsRayQueryParameters3D.Create(from, to);
			query.CollideWithAreas = false;
			query.CollideWithBodies = true;
			query.CollisionMask = 0xFFFF_FFFFu;
			query.Exclude = exclude;

			var hit = world.DirectSpaceState.IntersectRay(query);
			if (hit.Count == 0 || !hit.TryGetValue("position", out var posObj))
				return null;

			var hitPos = (Vector3)posObj;
			Vector3 hitNormal = hit.TryGetValue("normal", out var nrmObj)
				? ((Vector3)nrmObj).Normalized()
				: Vector3.Up;

			if (hitNormal.Dot(Vector3.Up) >= MinWalkableNormalDotUp && ColliderLooksLikeLevel3Road(hit))
				return hitPos;

			if (hit.TryGetValue("collider", out var colVar) && colVar.Obj is CollisionObject3D co)
				exclude.Add(co.GetRid());
			else
				return null;
		}

		return null;
	}

	private static bool ColliderLooksLikeLevel3Road(Godot.Collections.Dictionary hit)
	{
		if (IsUnderUphillRoadRoot(hit))
			return true;
		if (!hit.TryGetValue("collider", out var colVar) || colVar.Obj is not Node node)
			return false;
		string path = node.GetPath().ToString();
		return path.Contains("UphillRoad", StringComparison.Ordinal)
		       || path.Contains("RoundaboutDeck", StringComparison.Ordinal);
	}

	private static bool IsUnderUphillRoadRoot(Godot.Collections.Dictionary hit)
	{
		if (!hit.TryGetValue("collider", out var colVar) || colVar.Obj is not Node node)
			return false;
		for (Node n = node; n != null; n = n.GetParent())
		{
			if (n.Name == "UphillRoadRoot")
				return true;
		}
		return false;
	}
}
