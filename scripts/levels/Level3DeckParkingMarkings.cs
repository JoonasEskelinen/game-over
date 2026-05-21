using System.Collections.Generic;
using Godot;

/// <summary>
/// Level 3 ylätaso (RoundaboutDeck): valkoiset tieviivat + 2 riviä parkkiruutuja keskelle (MultiMesh).
/// Oletetaan vaakataso (Cylinder Y-akselilla); ParkingLot on Worldin lapsi deckin vieressä.
/// </summary>
public partial class Level3DeckParkingMarkings : Node3D
{
	[Export] public NodePath DeckPath = new("../RoundaboutDeck");

	[Export] public int StallCountPerRow = 5;
	[Export] public float StallPitchX = 2.45f;
	[Export] public float StallDepthZ = 4.6f;
	/// <summary> Rivin keskipisteen |Z| (rivit ±RowCenterOffsetZ). </summary>
	[Export] public float RowCenterOffsetZ = 5.35f;

	[Export] public float LinePlanWidth = 0.12f;
	[Export] public float LineThicknessY = 0.035f;
	[Export] public float LiftAboveDeckM = 0.025f;

	[Export] public float AisleDashLengthM = 2.2f;
	[Export] public float AisleDashGapM = 1.8f;

	private static StandardMaterial3D CreateWhiteLineMat() =>
		new()
		{
			AlbedoColor = new Color(0.92f, 0.92f, 0.93f),
			Roughness = 0.75f,
			RenderPriority = 1
		};

	public override void _Ready()
	{
		var parkingLot = GetParent() as Node3D;
		if (parkingLot == null)
		{
			GD.PushWarning("Level3DeckParkingMarkings: parent ei ole Node3D (odotetaan ParkingLot).");
			return;
		}

		var deck = parkingLot.GetNodeOrNull<Node3D>(DeckPath);
		if (deck == null || !TryGetCylinderHeight(deck, out float deckH))
		{
			GD.PushWarning("Level3DeckParkingMarkings: RoundaboutDeck puuttuu tai height ei luettavissa.");
			return;
		}

		float topYWorld = deck.GlobalPosition.Y + deckH * 0.5f;
		float yLocal = topYWorld + LiftAboveDeckM + LineThicknessY * 0.5f - parkingLot.GlobalPosition.Y;

		float xLeft = -(StallCountPerRow * StallPitchX) * 0.5f;
		float totalW = StallCountPerRow * StallPitchX;
		float halfD = StallDepthZ * 0.5f;

		var mat = CreateWhiteLineMat();

		// 1) Ruudukon jakoviivat (Z-suunta), molemmat rivit
		var divCenters = new List<Vector3>();
		for (int k = 0; k <= StallCountPerRow; k++)
		{
			float xk = xLeft + k * StallPitchX;
			divCenters.Add(new Vector3(xk, yLocal, RowCenterOffsetZ));
			divCenters.Add(new Vector3(xk, yLocal, -RowCenterOffsetZ));
		}

		AddChild(BuildMmi(divCenters, new BoxMesh
		{
			Size = new Vector3(LinePlanWidth, LineThicknessY, StallDepthZ)
		}, mat));

		// 2) Rivin päädyt (X-suunta): ulko- ja käytäväreuna per rivi
		var capCenters = new List<Vector3>
		{
			new(0f, yLocal, RowCenterOffsetZ + halfD),
			new(0f, yLocal, RowCenterOffsetZ - halfD),
			new(0f, yLocal, -RowCenterOffsetZ + halfD),
			new(0f, yLocal, -RowCenterOffsetZ - halfD)
		};
		AddChild(BuildMmi(capCenters, new BoxMesh
		{
			Size = new Vector3(totalW, LineThicknessY, LinePlanWidth)
		}, mat));

		// 3) Käytävän keskelle katkoviiva (X), sama tyyli kuin mäkitiellä
		var aisleXs = new List<float>();
		float half = totalW * 0.5f - LinePlanWidth;
		if (half > AisleDashLengthM * 0.5f)
		{
			float period = AisleDashLengthM + AisleDashGapM;
			float x = -half + AisleDashLengthM * 0.5f;
			float endL = half - AisleDashLengthM * 0.5f;
			int guard = 0;
			while (x <= endL + 1e-3f && guard++ < 64)
			{
				aisleXs.Add(x);
				x += period;
			}
		}

		var aisleCenters = new List<Vector3>();
		foreach (float ax in aisleXs)
			aisleCenters.Add(new Vector3(ax, yLocal, 0f));
		if (aisleCenters.Count > 0)
		{
			AddChild(BuildMmi(aisleCenters, new BoxMesh
			{
				Size = new Vector3(AisleDashLengthM, LineThicknessY, LinePlanWidth)
			}, mat));
		}
	}

	private static bool TryGetCylinderHeight(Node3D deck, out float height)
	{
		height = 0f;
		if (!GodotObject.IsInstanceValid(deck) || !deck.IsClass("CSGCylinder3D"))
			return false;
		Variant v = deck.Get("height");
		if (v.VariantType != Variant.Type.Float && v.VariantType != Variant.Type.Int)
			return false;
		height = v.VariantType == Variant.Type.Int ? v.AsInt32() : v.AsSingle();
		return height > 1e-4f;
	}

	private static MultiMeshInstance3D BuildMmi(IReadOnlyList<Vector3> localCenters, BoxMesh mesh, StandardMaterial3D mat)
	{
		var mm = new MultiMesh
		{
			TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
			InstanceCount = localCenters.Count,
			Mesh = mesh
		};
		for (int i = 0; i < localCenters.Count; i++)
			mm.SetInstanceTransform(i, Transform3D.Identity.Translated(localCenters[i]));

		return new MultiMeshInstance3D
		{
			Multimesh = mm,
			MaterialOverride = mat
		};
	}
}
