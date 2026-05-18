using System;
using Godot;

/// <summary>
/// Level 3 mäkitie: näytteenotto suoraan <c>UphillRoad</c> CSGBox3D-geometriasta (render + editor-koordinaatit).
/// Fysiikan CSG-törmäys voi poiketa hieman — tämä poistaa "puoliksi tien sisässä" -spawnit kun käytetään pintaa.
/// </summary>
public static class Level3UphillRoadSurface
{
	public static Node3D FindRoadPlaneNode(Node3D uphillRoadRoot) =>
		uphillRoadRoot?.GetNodeOrNull<Node3D>("UphillRoad");

	public static bool TryGetCsgBoxSize(Node3D uphillRoad, out Vector3 size)
	{
		size = default;
		if (uphillRoad == null || !GodotObject.IsInstanceValid(uphillRoad))
			return false;
		if (!uphillRoad.IsClass("CSGBox3D"))
			return false;
		Variant v = uphillRoad.Get("size");
		if (v.VariantType != Variant.Type.Vector3)
			return false;
		size = v.AsVector3();
		return size.X > 1e-4f && size.Y > 1e-4f && size.Z > 1e-4f;
	}

	/// <summary>
	/// Palauttaa tien yläpinnan pisteen annetulla maailman XZ:llä ja ulososoittavan yksikkönormaalin.
	/// </summary>
	public static bool TrySampleTopFaceWorldAtXZ(Node3D boxNode, float worldX, float worldZ, out Vector3 surfaceWorld, out Vector3 outwardNormal)
	{
		surfaceWorld = default;
		outwardNormal = default;
		if (boxNode == null || !GodotObject.IsInstanceValid(boxNode))
			return false;
		if (!TryGetCsgBoxSize(boxNode, out Vector3 size))
			return false;

		Transform3D gt = boxNode.GlobalTransform;

		Vector3 basisY = gt.Basis.Y;
		float yLen = basisY.Length();
		if (yLen < 1e-5f)
			return false;

		Vector3 yDir = basisY / yLen;
		Vector3 nNeg = -yDir;
		outwardNormal = yDir.Dot(Vector3.Up) >= nNeg.Dot(Vector3.Up) ? yDir : nNeg;

		float halfH = size.Y * 0.5f;
		float topLocalY = outwardNormal.Dot(basisY) >= 0f ? halfH : -halfH;

		float ny = outwardNormal.Y;
		if (Mathf.Abs(ny) < 1e-4f)
			return false;

		Vector3 topFaceCenter = gt.Origin + basisY * topLocalY;
		float d = outwardNormal.Dot(topFaceCenter);

		float y = (d - outwardNormal.X * worldX - outwardNormal.Z * worldZ) / ny;
		Vector3 candidate = new Vector3(worldX, y, worldZ);

		Transform3D inv = gt.AffineInverse();
		Vector3 local = inv * candidate;

		float halfX = size.X * 0.5f;
		float halfZ = size.Z * 0.5f;
		local.X = Mathf.Clamp(local.X, -halfX, halfX);
		local.Z = Mathf.Clamp(local.Z, -halfZ, halfZ);
		local.Y = topLocalY;

		surfaceWorld = gt * local;
		return true;
	}

	/// <summary>
	/// Tunnistaa Level 3 ajoradan CSG-tien (ei esim. RampSide*, jotka ovat UphillRoadRootin alla).
	/// </summary>
	public static bool ColliderIsLevel3RoadDeck(Godot.Collections.Dictionary hit)
	{
		if (!hit.TryGetValue("collider", out var colVar) || colVar.Obj is not Node node)
			return false;
		if (node.Name == "UphillRoad" || node.Name == "RoundaboutDeck")
			return true;
		string p = node.GetPath().ToString();
		if (p.Contains("RoundaboutDeck", StringComparison.Ordinal))
			return true;
		return p.Contains("/UphillRoad/", StringComparison.Ordinal)
		       || p.EndsWith("/UphillRoad", StringComparison.Ordinal);
	}
}
