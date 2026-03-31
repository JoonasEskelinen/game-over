using System;
using Godot;

/// <summary>
/// level_1: lisää propseille yhden BoxShape3D StaticBodyn (ei trimeshäjä — Pi-ystävällinen).
/// air-hockey2 → RigidBody3D + ryhmä <c>grabbable</c> (PlayerController tarttuu).
/// </summary>
public partial class Level1ArcadePhysicsSetup : Node3D
{
	[Export] public string AirHockeyNodeName = "air-hockey2";

	[Export] public float AirHockeyMass = 38f;

	/// <summary>Kerros (bitmask), jolle arcade-propput menevät — Level 1 -bossin syöksy (mask 1) ei törmää.</summary>
	[Export] public uint ArcadePropCollisionLayer = 16u;

	public override void _Ready()
	{
		SetupRootProps();
		SetupFloorProps();
	}

	private void SetupFloorProps()
	{
		var floor = GetNodeOrNull<Node>("Floor");
		if (floor == null) return;
		foreach (var child in floor.GetChildren())
		{
			if (child is MeshInstance3D or CollisionShape3D)
				continue;
			if (child is not Node3D nd)
				continue;
			if (nd.HasMeta("arcade_phys_done"))
				continue;
			AddStaticBoxForVisual(nd, floor, false);
		}
	}

	private void SetupRootProps()
	{
		foreach (var child in GetChildren())
		{
			if (child is MeshInstance3D or CollisionShape3D)
				continue;
			var s = child.Name.ToString();
			if (s is "Player" or "Camera3D" or "WorldEnvironment" or "Sun" or "EnemySpawner" or "HUD" or "Floor")
				continue;
			if (s.StartsWith("Wall_") || s.StartsWith("Neon_"))
				continue;

			if (child is not Node3D nd)
				continue;
			if (nd.HasMeta("arcade_phys_done"))
				continue;

			if (child.Name == AirHockeyNodeName)
				SetupAirHockey(nd);
			else
			{
				bool floorPiece = s.StartsWith("floor", StringComparison.OrdinalIgnoreCase);
				AddStaticBoxForVisual(nd, this, !floorPiece);
			}
		}
	}

	private void SetupAirHockey(Node3D airRoot)
	{
		if (airRoot.HasMeta("arcade_phys_done"))
			return;
		airRoot.SetMeta("arcade_phys_done", true);

		var parent = airRoot.GetParent();
		if (parent == null) return;

		int idx = airRoot.GetIndex();
		var gt = airRoot.GlobalTransform;

		parent.RemoveChild(airRoot);
		var rb = new RigidBody3D
		{
			Name = "AirHockeyRigid",
			Mass = AirHockeyMass,
			GravityScale = 1f,
			CanSleep = true,
			Sleeping = false,
			ContinuousCd = false,
			AxisLockLinearY = true,
			AxisLockAngularX = true,
			AxisLockAngularY = true,
			AxisLockAngularZ = true,
			LinearDamp = 2.2f,
			AngularDamp = 8f
		};
		rb.AddToGroup("grabbable");
		parent.AddChild(rb);
		parent.MoveChild(rb, idx);
		rb.GlobalTransform = gt;
		rb.CollisionLayer = ArcadePropCollisionLayer;
		rb.AddChild(airRoot);
		airRoot.Transform = Transform3D.Identity;

		if (!TryUnionVisualAabb(airRoot, out Aabb worldAabb))
		{
			var fsz = new Vector3(2.2f, 0.45f, 1.25f);
			var fctr = rb.GlobalPosition + Vector3.Up * 0.35f;
			worldAabb = new Aabb(fctr - fsz * 0.5f, fsz);
		}

		var centerW = worldAabb.GetCenter();
		var col = new CollisionShape3D
		{
			Position = rb.ToLocal(centerW),
			Shape = new BoxShape3D { Size = worldAabb.Size * 1.02f }
		};
		rb.AddChild(col);
	}

	private void AddStaticBoxForVisual(Node3D visualRoot, Node parent, bool useArcadePropLayer)
	{
		if (visualRoot.HasMeta("arcade_phys_done"))
			return;
		visualRoot.SetMeta("arcade_phys_done", true);

		if (!TryUnionVisualAabb(visualRoot, out Aabb worldAabb))
			return;

		var sb = new StaticBody3D { Name = visualRoot.Name + "_Phys" };
		sb.CollisionLayer = useArcadePropLayer ? ArcadePropCollisionLayer : 1u;
		parent.AddChild(sb);
		sb.GlobalPosition = worldAabb.GetCenter();

		var col = new CollisionShape3D
		{
			Shape = new BoxShape3D { Size = worldAabb.Size * 1.02f }
		};
		sb.AddChild(col);
	}

	private static bool TryUnionVisualAabb(Node root, out Aabb worldAabb)
	{
		worldAabb = default;
		bool has = false;
		Accumulate(root, ref worldAabb, ref has);
		return has;
	}

	private static void Accumulate(Node node, ref Aabb acc, ref bool hasAny)
	{
		if (node is MeshInstance3D mi && mi.Visible && mi.Mesh != null)
		{
			var local = mi.GetAabb();
			var xf = mi.GlobalTransform;
			for (int i = 0; i < 8; i++)
			{
				var c = local.Position + new Vector3(
					(i & 1) != 0 ? local.Size.X : 0f,
					(i & 2) != 0 ? local.Size.Y : 0f,
					(i & 4) != 0 ? local.Size.Z : 0f);
				var w = xf.Basis * c + xf.Origin;
				if (!hasAny)
				{
					acc = new Aabb(w, Vector3.Zero);
					hasAny = true;
				}
				else
					acc = acc.Expand(w);
			}
		}

		foreach (var c in node.GetChildren())
			Accumulate(c, ref acc, ref hasAny);
	}
}
