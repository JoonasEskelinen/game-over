using System;
using Godot;

/// <summary>
/// Level 3: kiviä mäkeä alas Timerilla (ei riipu _Processista). KivienSkena + UphillRoadRoot pakolliset.
/// </summary>
/// <remarks>
/// Tämän noden <b>Transform (Position / Rotation)</b> ei vaikuta kiven ilmestymispaikkaan:
/// spawn on <see cref="GlobalPosition"/>-koordinaateissa ja lasketaan pelaajan + UphillRoadRoot -geometriasta.
/// Testausta varten käytä <see cref="SpawnWorldOffset"/> Inspectorissa, älä oleta että noden siirto siirtää kiviä.
/// </remarks>
public partial class Level3RollingRockSpawner : Node3D
{
	[ExportCategory("Kierivät kivet")]
	[ExportGroup("Skena ja polku")]
	[Export] public PackedScene KivienSkena;

	[ExportGroup("Testi (maailmakoordinaatit)")]
	/// <summary>Lisätään jokaisen spawnin lopulliseen sijaintiin. Noden oma Position ei vaikuta.</summary>
	[Export] public Vector3 SpawnWorldOffset;

	[Export] public NodePath TienJuurenPolku;

	[ExportGroup("Ilmestymisvälit")]
	[Export] public float IlmestymisväliMinSek = 2.2f;
	[Export] public float IlmestymisväliMaxSek = 5.5f;

	[ExportGroup("Sijainti pelaajaan nähden")]
	[Export] public float EtäisyysYlämäkeenMin = 16f;
	[Export] public float EtäisyysYlämäkeenMax = 34f;

	[Export] public float SivuttaisenPuolikasLeveys = 2.0f;

	[ExportGroup("Tien pinta (säde)")]
	[Export] public float SäteenLähtöY = 70f;
	/// <summary>
	/// Ylimääräinen etäisyys tien <b>normaalin</b> suuntaan säteen (RollingRockLevel3.RollRadius) päälle.
	/// Vanha arvo world-Up -offsetina työnsi kaltevalla tiellä kiven osittain tien sisään.
	/// </summary>
	[Export] public float NormaalisuuntaanYlimäärä = 0.06f;

	/// <summary>
	/// Lyhyt säde tien <b>fysiikan</b> pintaan (CSG-törmäys voi olla hieman eri kuin render-mesh).
	/// </summary>
	[Export] public bool KäytäFysiikanPintaaSpawnissa = true;

	[Export] public float MinKuljettavaNormaaliY = 0.32f;

	private Node3D _player;
	private Node3D _roadRoot;
	private Node3D _uphillRoadDeck;
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

		if (KivienSkena == null)
		{
			GD.PushError("Level3RollingRockSpawner: KivienSkena puuttuu (PackedScene).");
			return;
		}

		if (_roadRoot == null)
		{
			GD.PushError("Level3RollingRockSpawner: UphillRoadRoot ei löydy — tarkista scene-puu.");
			return;
		}

		_uphillRoadDeck = Level3UphillRoadSurface.FindRoadPlaneNode(_roadRoot);
		if (_uphillRoadDeck == null || !Level3UphillRoadSurface.TryGetCsgBoxSize(_uphillRoadDeck, out _))
			GD.PushWarning("Level3RollingRockSpawner: UphillRoad CSGBox3D puuttuu tai size ei luettavissa — käytetään vain sädettä.");

		_timer.WaitTime = 1.0f;
		_timer.Start();
	}

	private void OnSpawnTimer()
	{
		if (KivienSkena == null || _roadRoot == null)
			return;

		var tree = GetTree();
		if (tree == null)
			return;
		_player = tree.GetFirstNodeInGroup("player") as Node3D;
		SpawnOneRock();

		_timer.WaitTime = (float)GD.RandRange(IlmestymisväliMinSek, IlmestymisväliMaxSek);
		_timer.Start();
	}

	private void ResolveRoadRoot()
	{
		_roadRoot = null;
		if (!IsInsideTree())
			return;

		// level_3.tscn: juuri → World/UphillRoadRoot (vältetään rikkinäinen / null TienJuurenPolku-export).
		var sceneRoot = GetParent();
		if (sceneRoot != null)
			_roadRoot = sceneRoot.GetNodeOrNull<Node3D>("World/UphillRoadRoot");

		if (_roadRoot == null)
		{
			try
			{
				var path = TienJuurenPolku;
				if (!path.IsEmpty && HasNode(path))
					_roadRoot = GetNodeOrNull<Node3D>(path);
			}
			catch (Exception e)
			{
				GD.PushWarning($"Level3RollingRockSpawner: TienJuurenPolku virheellinen: {e.Message}");
			}
		}

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

		float up = (float)GD.RandRange(EtäisyysYlämäkeenMin, EtäisyysYlämäkeenMax);
		float side = (float)GD.RandRange(-SivuttaisenPuolikasLeveys, SivuttaisenPuolikasLeveys);

		Vector3 basePos = (_player != null && GodotObject.IsInstanceValid(_player) && _player.IsInsideTree())
			? _player.GlobalPosition
			: _roadRoot.GlobalPosition;

		Vector3 anchorBeforeClamp = basePos + uphill * up + lateral * side;
		Vector3 anchor = anchorBeforeClamp;
		if (_uphillRoadDeck != null && GodotObject.IsInstanceValid(_uphillRoadDeck)
			&& Level3UphillRoadSurface.TrySampleTopFaceWorldAtXZ(_uphillRoadDeck, anchor.X, anchor.Z, out Vector3 deckAnchorSnap, out _))
			anchor = new Vector3(deckAnchorSnap.X, anchor.Y, deckAnchorSnap.Z);

		var roadHit = TryRaycastRoadTop(anchor, _player);

		Node inst = KivienSkena.Instantiate();
		if (inst is not RollingRockLevel3 rock)
		{
			GD.PushError($"Level3RollingRockSpawner: juuri ei ole RollingRockLevel3 (oli {inst?.GetType().Name}).");
			inst?.QueueFree();
			return;
		}

		float r = ReadBodySphereRadius(rock);
		float margin = Mathf.Max(0f, NormaalisuuntaanYlimäärä);
		float alongN = r + margin;
		Vector3 roadNormal = _roadRoot.GlobalTransform.Basis.Y.Normalized();

		Vector3 surfaceHint;
		Vector3 outwardN;
		Vector3 spawn;
		if (_uphillRoadDeck != null && GodotObject.IsInstanceValid(_uphillRoadDeck)
			&& Level3UphillRoadSurface.TrySampleTopFaceWorldAtXZ(_uphillRoadDeck, anchor.X, anchor.Z, out Vector3 deckPt, out Vector3 deckN))
		{
			surfaceHint = deckPt;
			outwardN = deckN;
			spawn = deckPt + deckN * alongN;
		}
		else if (roadHit.HasValue)
		{
			var hit = roadHit.Value;
			Vector3 n = hit.Normal;
			if (n.Dot(Vector3.Up) < 0f)
				n = -n;
			surfaceHint = hit.Position;
			outwardN = n;
			spawn = hit.Position + n * alongN;
		}
		else
		{
			Vector3 localOnRamp = _roadRoot.GlobalTransform.Basis.Inverse() * (anchor - _roadRoot.GlobalPosition);
			localOnRamp = new Vector3(localOnRamp.X, 0.42f, Mathf.Clamp(localOnRamp.Z, -SivuttaisenPuolikasLeveys, SivuttaisenPuolikasLeveys));
			Vector3 deckGuess = _roadRoot.ToGlobal(localOnRamp);
			surfaceHint = deckGuess;
			outwardN = roadNormal;
			spawn = deckGuess + roadNormal * alongN;
		}

		bool snapOk = false;
		Vector3 physicsCenter = default;
		if (KäytäFysiikanPintaaSpawnissa)
			snapOk = TrySnapRockCenterToPhysicsRoad(surfaceHint, outwardN, r, margin, _player, out physicsCenter, out _);
		if (snapOk)
			spawn = physicsCenter;

		spawn += SpawnWorldOffset;

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

	private static float ReadBodySphereRadius(RollingRockLevel3 rock)
	{
		if (rock.GetNodeOrNull<CollisionShape3D>("CollisionShape3D")?.Shape is SphereShape3D sp)
			return Mathf.Max(sp.Radius, 0.01f);
		return Mathf.Max(rock.RollRadius, 0.05f);
	}

	/// <summary>
	/// Lyhyt säde tien fysiikkapintaan (CSG vs render). Palauttaa RigidBody-keskipisteen maailmassa.
	/// </summary>
	private bool TrySnapRockCenterToPhysicsRoad(
		Vector3 surfaceHint,
		Vector3 outwardHint,
		float sphereRadius,
		float marginAlongNormal,
		Node3D player,
		out Vector3 bodyCenterWorld,
		out string failReason)
	{
		bodyCenterWorld = default;
		failReason = "";
		var world = GetWorld3D();
		if (world == null)
		{
			failReason = "no_world";
			return false;
		}

		Vector3 n = outwardHint.Normalized();
		if (n.LengthSquared() < 1e-8f)
		{
			failReason = "zero_normal";
			return false;
		}

		float probe = Mathf.Max(0.08f, sphereRadius * 0.2f);
		Vector3 from = surfaceHint + n * probe;
		Vector3 to = surfaceHint - n * Mathf.Max(0.55f, sphereRadius * 1.4f);

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
			{
				failReason = "ray_empty";
				return false;
			}

			var hitPos = (Vector3)posObj;
			Vector3 nh = hit.TryGetValue("normal", out var nrmObj)
				? ((Vector3)nrmObj).Normalized()
				: n;

			if (nh.Dot(Vector3.Up) < MinKuljettavaNormaaliY * 0.75f || !Level3UphillRoadSurface.ColliderIsLevel3RoadDeck(hit))
			{
				string cname = "";
				if (hit.TryGetValue("collider", out var colVar) && colVar.Obj is Node rej)
					cname = rej.Name;
				bool deck = Level3UphillRoadSurface.ColliderIsLevel3RoadDeck(hit);
				failReason = $"reject:{cname}:deck={deck}:dotUp={nh.Dot(Vector3.Up):F3}";
				if (hit.TryGetValue("collider", out var colVar2) && colVar2.Obj is CollisionObject3D co)
					exclude.Add(co.GetRid());
				else
					return false;
				continue;
			}

			if (nh.Dot(n) < 0f)
				nh = -nh;

			float skin = Mathf.Max(0.10f, Mathf.Max(marginAlongNormal, sphereRadius * 0.08f));
			bodyCenterWorld = hitPos + nh * (sphereRadius + skin);
			failReason = "ok";
			return true;
		}

		failReason = "exhausted_attempts";
		return false;
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

	private (Vector3 Position, Vector3 Normal)? TryRaycastRoadTop(Vector3 worldAnchor, Node3D player)
	{
		var world = GetWorld3D();
		if (world == null)
			return null;

		var from = new Vector3(worldAnchor.X, SäteenLähtöY, worldAnchor.Z);
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

			if (hitNormal.Dot(Vector3.Up) >= MinKuljettavaNormaaliY && Level3UphillRoadSurface.ColliderIsLevel3RoadDeck(hit))
				return (hitPos, hitNormal);

			if (hit.TryGetValue("collider", out var colVar) && colVar.Obj is CollisionObject3D co)
				exclude.Add(co.GetRid());
			else
				return null;
		}

		return null;
	}
}
