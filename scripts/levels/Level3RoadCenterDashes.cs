using System.Collections.Generic;
using Godot;

/// <summary>
/// Level 3 mäkitie: keltainen katkoviiva tien keskelle (MultiMesh, yksi piirtokutsu).
/// Sijainti lasketaan UphillRoadin pintaan — kiinteä Y UphillRoadRootissa jäi asfaltin alle.
/// </summary>
public partial class Level3RoadCenterDashes : Node3D
{
	[Export] public float DashLengthM = 4.0f;
	[Export] public float GapLengthM = 3.0f;
	[Export] public float LineWidthZ = 0.22f;
	[Export] public float LineThicknessY = 0.04f;
	/// <summary> Siirto tien paikallista +Y:tä (pinnan normaali) pitkin sentteinä, vähentää z-fightia. </summary>
	[Export] public float LiftAlongRoadNormalM = 0.03f;
	[Export] public float EdgeMarginM = 0.25f;

	public override void _Ready()
	{
		var roadRoot = GetParent() as Node3D;
		var road = roadRoot?.GetNodeOrNull<Node3D>("UphillRoad");
		if (road == null || roadRoot == null || !Level3UphillRoadSurface.TryGetCsgBoxSize(road, out Vector3 roadSize))
		{
			GD.PushWarning("Level3RoadCenterDashes: UphillRoad puuttuu tai size ei luettavissa.");
			return;
		}

		float half = roadSize.X * 0.5f - EdgeMarginM;
		if (half < DashLengthM * 0.5f)
			return;

		float period = DashLengthM + GapLengthM;
		if (period < 0.05f)
			return;

		int maxCount = Mathf.CeilToInt((half * 2f + period) / period) + 2;
		var xs = new List<float>(maxCount);
		float x = -half + DashLengthM * 0.5f;
		float endLimit = half - DashLengthM * 0.5f;
		while (x <= endLimit + 1e-3f && xs.Count < maxCount)
		{
			xs.Add(x);
			x += period;
		}

		if (xs.Count == 0)
			return;

		var mm = new MultiMesh
		{
			TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
			InstanceCount = xs.Count,
			Mesh = new BoxMesh
			{
				Size = new Vector3(DashLengthM, LineThicknessY, LineWidthZ)
			}
		};

		Transform3D invRoot = roadRoot.GlobalTransform.AffineInverse();
		float halfH = roadSize.Y * 0.5f;
		Basis rb = road.GlobalTransform.Basis;
		if (rb.X.LengthSquared() < 1e-8f || rb.Y.LengthSquared() < 1e-8f)
		{
			GD.PushWarning("Level3RoadCenterDashes: UphillRoad-basis puutteellinen.");
			return;
		}

		Vector3 bx = rb.X.Normalized();
		Vector3 by = rb.Y.Normalized();
		if (by.Dot(Vector3.Up) < 0f)
			by = -by;
		Vector3 bz = bx.Cross(by).Normalized();
		bx = by.Cross(bz).Normalized();
		var roadRot = new Basis(bx, by, bz);

		for (int i = 0; i < xs.Count; i++)
		{
			float xl = xs[i];
			Vector3 deckWorld = road.GlobalTransform * new Vector3(xl, halfH, 0f);
			Vector3 boxCenterWorld = deckWorld + by * (LiftAlongRoadNormalM + LineThicknessY * 0.5f);
			var worldXf = new Transform3D(roadRot, boxCenterWorld);
			mm.SetInstanceTransform(i, invRoot * worldXf);
		}

		var mat = new StandardMaterial3D
		{
			AlbedoColor = new Color(0.78f, 0.65f, 0.08f),
			Roughness = 0.7f,
			RenderPriority = 1
		};

		var mmi = new MultiMeshInstance3D
		{
			Multimesh = mm,
			MaterialOverride = mat
		};
		AddChild(mmi);
	}
}
