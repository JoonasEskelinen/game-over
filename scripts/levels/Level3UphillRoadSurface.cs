using System;
using Godot;

/// <summary>
/// Level 3 mÃ¤kitie: nÃ¤ytteenotto suoraan <c>UphillRoad</c> CSGBox3D-geometriasta (render + editor-koordinaatit).
/// Fysiikan CSG-tÃ¶rmÃ¤ys voi poiketa hieman â€” tÃ¤mÃ¤ poistaa "puoliksi tien sisÃ¤ssÃ¤" -spawnit kun kÃ¤ytetÃ¤Ã¤n pintaa.
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
	/// Palauttaa tien ylÃ¤pinnan pisteen annetulla maailman XZ:llÃ¤ ja ulososoittavan yksikkÃ¶normaalin.
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

	public static bool TryGetCsgCylinderParams(Node3D cylinderNode, out float radius, out float height)
	{
		radius = 0f;
		height = 0f;
		if (cylinderNode == null || !GodotObject.IsInstanceValid(cylinderNode))
			return false;
		if (!cylinderNode.IsClass("CSGCylinder3D"))
			return false;

		Variant r = cylinderNode.Get("radius");
		Variant h = cylinderNode.Get("height");
		if (r.VariantType != Variant.Type.Float || h.VariantType != Variant.Type.Float)
			return false;

		radius = r.AsSingle();
		height = h.AsSingle();
		return radius > 1e-4f && height > 1e-4f;
	}

	/// <summary>
	/// CSGCylinder3D:n yläpinta (esim. RoundaboutDeck). XZ rajataan ympyrän sisään <paramref name="edgeInset"/>-marginaalilla.
	/// </summary>
	public static bool TrySampleCsgCylinderTopAtXZ(
		Node3D cylinderNode,
		float worldX,
		float worldZ,
		float edgeInset,
		out Vector3 surfaceWorld,
		out Vector3 outwardNormal)
	{
		surfaceWorld = default;
		outwardNormal = Vector3.Up;
		if (!TryGetCsgCylinderParams(cylinderNode, out float radius, out float height))
			return false;

		Transform3D gt = cylinderNode.GlobalTransform;
		Vector3 axisY = gt.Basis.Y;
		float axisLen = axisY.Length();
		if (axisLen < 1e-5f)
			return false;
		axisY /= axisLen;

		outwardNormal = axisY.Dot(Vector3.Up) >= 0f ? axisY : -axisY;
		float halfH = height * 0.5f;
		float topLocalY = outwardNormal.Dot(axisY) >= 0f ? halfH : -halfH;

		Transform3D inv = gt.AffineInverse();
		Vector3 localProbe = inv * new Vector3(worldX, gt.Origin.Y, worldZ);
		Vector2 localXz = new Vector2(localProbe.X, localProbe.Z);

		float maxR = Mathf.Max(0.35f, radius - Mathf.Max(0f, edgeInset));
		if (localXz.Length() > maxR)
			localXz = localXz.Normalized() * maxR;

		surfaceWorld = gt * new Vector3(localXz.X, topLocalY, localXz.Y);
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
