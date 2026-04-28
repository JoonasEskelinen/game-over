using System;
using Godot;

/// <summary>
/// Vain level_3: kohdistaa pelaajan tien kaltevan pinnan päälle säteellä.
/// Odotetaan fysiikkakehys (CSG-törmäykset), ohitetaan väärät osumat (seinät, pelaaja), sitten jalkapiste pintaan.
/// </summary>
public partial class Level3GroundAlign : Node
{
	[Export] public float RaycastTopY = 120f;
	[Export] public float RaycastDepth = 250f;
	[Export] public float SurfaceBiasAlongNormal = 0.06f;
	/// <summary>Hyväksy osuma vain jos normaali on suunnilleen ylös (ohittaa pystyseinät).</summary>
	[Export] public float MinWalkableNormalDotUp = 0.28f;
	/// <summary>Jos tarkka tie-osuma epäonnistuu, viimeinen yritys: ensimmäinen käveltävä osuma (lievempi kulma).</summary>
	[Export] public float FallbackMinNormalDotUp = 0.12f;

	private bool _snapped;

	public override void _Ready()
	{
		var tree = GetTree();
		if (tree == null)
			return;
		tree.PhysicsFrame += OnPhysicsFrameSnapOnce;
	}

	private void OnPhysicsFrameSnapOnce()
	{
		var tree = GetTree();
		if (tree == null)
			return;
		tree.PhysicsFrame -= OnPhysicsFrameSnapOnce;
		SnapPlayerToRoadSurface();
	}

	private void SnapPlayerToRoadSurface()
	{
		if (_snapped) return;

		var levelRoot = GetParent();
		var player = levelRoot?.GetNodeOrNull<CharacterBody3D>("Player");
		if (player == null || !player.IsInsideTree())
		{
			GD.PushWarning("Level3GroundAlign: Player-nodia ei löydy Level3-juuren alta (nimi 'Player').");
			return;
		}

		var world = player.GetWorld3D();
		if (world == null)
		{
			GD.PushWarning("Level3GroundAlign: World3D puuttuu.");
			return;
		}

		Vector3 p = player.GlobalPosition;
		var from = new Vector3(p.X, RaycastTopY, p.Z);
		var to = from + Vector3.Down * RaycastDepth;

		var exclude = new Godot.Collections.Array<Rid> { player.GetRid() };

		if (!TryRaycastRoadSurface(world.DirectSpaceState, from, to, exclude, out Vector3 hitPos, out Vector3 hitNormal))
		{
			if (!TryRaycastFirstWalkableHit(world.DirectSpaceState, from, to, exclude, FallbackMinNormalDotUp, out hitPos, out hitNormal))
			{
				if (!TryFallbackSnapFromRoadRoot(levelRoot, player, out hitPos, out hitNormal))
					return;
			}
		}

		hitPos += hitNormal.Normalized() * SurfaceBiasAlongNormal;

		float feetY = GetCapsuleBottomGlobalY(player);
		float targetFeetY = hitPos.Y;
		float dy = targetFeetY - feetY;

		// Aiemmin virheellinen ehto (rajattiin väärin); nyt vain järkevä maksimisiirto.
		if (Mathf.Abs(dy) > 1e-4f && Mathf.Abs(dy) < 12f)
		{
			player.GlobalPosition += new Vector3(0f, dy, 0f);
			_snapped = true;
		}
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

			if (hitNormal.Dot(Vector3.Up) >= MinWalkableNormalDotUp &&
			    ColliderLooksLikeLevel3Road(hit))
				return true;

			if (hit.TryGetValue("collider", out var colObj) &&
			    colObj.Obj is CollisionObject3D co)
				exclude.Add(co.GetRid());
			else
				return false;
		}

		return false;
	}

	private static bool TryRaycastFirstWalkableHit(
		PhysicsDirectSpaceState3D space,
		Vector3 from,
		Vector3 to,
		Godot.Collections.Array<Rid> exclude,
		float minDot,
		out Vector3 hitPos,
		out Vector3 hitNormal)
	{
		hitPos = default;
		hitNormal = Vector3.Up;
		for (int attempt = 0; attempt < 24; attempt++)
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

			if (hitNormal.Dot(Vector3.Up) >= minDot)
				return true;

			if (hit.TryGetValue("collider", out var colVar) && colVar.Obj is CollisionObject3D co)
				exclude.Add(co.GetRid());
			else
				return false;
		}

		return false;
	}

	private static bool TryFallbackSnapFromRoadRoot(Node levelRoot, CharacterBody3D player, out Vector3 hitPos, out Vector3 hitNormal)
	{
		hitPos = default;
		hitNormal = Vector3.Up;
		var road = levelRoot?.GetNodeOrNull<Node3D>("World/UphillRoadRoot");
		if (road == null)
			return false;

		Vector3 local = road.GlobalTransform.Basis.Inverse() * (player.GlobalPosition - road.GlobalPosition);
		local = new Vector3(local.X, 0.42f, Mathf.Clamp(local.Z, -2.2f, 2.2f));
		hitPos = road.ToGlobal(local);
		hitNormal = road.GlobalTransform.Basis.Y.Normalized();
		return true;
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

	private static float GetCapsuleBottomGlobalY(CharacterBody3D body)
	{
		if (body.GetNodeOrNull("CollisionShape3D") is not CollisionShape3D shapeNode)
			return body.GlobalPosition.Y;
		if (shapeNode.Shape is not CapsuleShape3D cap)
			return body.GlobalPosition.Y;

		Transform3D gt = shapeNode.GlobalTransform;
		float half = cap.Height * 0.5f;
		Vector3 yAxis = gt.Basis.Y;
		if (yAxis.LengthSquared() < 1e-8f)
			return body.GlobalPosition.Y;
		yAxis = yAxis.Normalized();
		Vector3 bottom = gt.Origin - yAxis * half;
		return bottom.Y;
	}
}
