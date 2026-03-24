using System.Collections.Generic;
using Godot;

/// <summary>
/// Täyttää metsän instanssioimalla puu-PackedSceneja satunnaisesti paikalliseen alueeseen.
/// Kevyempi työskentely kuin tuhansien noden raahaaminen; suorituskyky riippuu TotalTrees-arvosta
/// ja FBX-mallien raskaustasosta (MultiMesh on paras tuhansille identtisille meshille).
/// </summary>
public partial class ForestScatter : Node3D
{
	[ExportGroup("Prefabs")]
	[Export] public PackedScene CommonTree1 { get; set; }
	[Export] public PackedScene CommonTree2 { get; set; }
	[Export] public PackedScene CommonTree3 { get; set; }
	[Export] public PackedScene CommonTree4 { get; set; }
	[Export] public PackedScene CommonTree5 { get; set; }

	[ExportGroup("Area (world XZ, Forest on yleensä World-lapsi)")]
	[Export] public int TotalTrees { get; set; } = 350;
	[Export] public Vector2 XMinMax { get; set; } = new(-30f, 220f);
	[Export] public Vector2 ZMinMax { get; set; } = new(-150f, -22f);

	[ExportGroup("Poissulje laatikko (world XZ)")]
	/// <summary>
	/// Jos päällä: puu/proppi ei saa osua tähän suorakulmioon (uudelleenheito).
	/// Käytä esim. apronin laatalle: estä generointi laatan päältä vaikka XMinMax olisi leveä.
	/// </summary>
	[Export] public bool ExcludeRectEnabled { get; set; }
	[Export] public Vector2 ExcludeRectMinXZ { get; set; } = new(145f, -120f);
	[Export] public Vector2 ExcludeRectMaxXZ { get; set; } = new(340f, 100f);

	[ExportGroup("Height")]
	[Export] public float BaseHeight { get; set; } = -2f;
	[Export] public float HeightAlongX { get; set; } = 0.33f;
	[Export] public float HeightJitter { get; set; } = 1.5f;

	[ExportGroup("Tien pinta (level 2 mäki)")]
	/// <summary>
	/// Laskee Y tien kaltevan rampin tasosta (World/Terrain/Ramp_Main) + valinnainen apron.
	/// </summary>
	[Export] public bool UseRampRoadSurfaceY { get; set; }
	[Export] public Vector3 RampOrigin { get; set; } = new(64f, 27f, 0f);
	[Export] public Vector3 RampAxisX { get; set; } = new(0.9213955f, 0.3885147f, 0f);
	[Export] public Vector3 RampAxisY { get; set; } = new(-0.3885147f, 0.9213955f, 0f);
	[Export] public float RoadTopLocalY { get; set; } = 0.82f;
	[Export] public float ApronRoadTopWorldY { get; set; } = 55.8f;
	[Export] public float BlendToApronStartX { get; set; } = 112f;
	[Export] public float BlendToApronEndX { get; set; } = 132f;
	[Export] public bool BlendSmoothstep { get; set; } = true;
	/// <summary>
	/// Rajoittaa rampin matemaattista pituutta (lx). Ilman tätä kaava jatkuu loputtomiin vaikka
	/// fyysinen Ramp_Main-mesh päättyy → puut saavat apronin Y:n liian aikaisin ja näyttävät
	/// leijuvan mäen lopussa. MaxRampAlongLx ≈ törmäyslaatikon puolipituus local X (level 2 ~93–94).
	/// </summary>
	[Export] public bool ClampRampAlongMaxLx { get; set; }
	[Export] public float MaxRampAlongLx { get; set; } = 93.5f;

	[ExportGroup("Variation")]
	[Export] public Vector2 ScaleMinMax { get; set; } = new(0.8f, 1.35f);
	[Export] public long RandomSeed { get; set; } = 424242;

	[ExportGroup("Ground props (kivet, pensaat, …)")]
	[Export] public int TotalGroundProps { get; set; }
	[Export] public PackedScene GroundProp1 { get; set; }
	[Export] public PackedScene GroundProp2 { get; set; }
	[Export] public PackedScene GroundProp3 { get; set; }
	[Export] public PackedScene GroundProp4 { get; set; }
	[Export] public PackedScene GroundProp5 { get; set; }
	/// <summary>Oma Z-väli propseille (esim. tiheämmin tien reunan ja metsän väliin).</summary>
	[Export] public Vector2 PropsZMinMax { get; set; } = new(-22f, -6.5f);
	[Export] public Vector2 PropsScaleMinMax { get; set; } = new(0.45f, 1.15f);
	[Export] public float PropsHeightJitter { get; set; } = 0.35f;

	public override void _Ready()
	{
		var variants = new List<PackedScene>();
		if (CommonTree1 != null) variants.Add(CommonTree1);
		if (CommonTree2 != null) variants.Add(CommonTree2);
		if (CommonTree3 != null) variants.Add(CommonTree3);
		if (CommonTree4 != null) variants.Add(CommonTree4);
		if (CommonTree5 != null) variants.Add(CommonTree5);
		if (variants.Count == 0)
			GD.PushWarning($"{Name}: ForestScatter — ei puuprefabeja, puut ohitetaan.");

		var propVariants = new List<PackedScene>();
		if (GroundProp1 != null) propVariants.Add(GroundProp1);
		if (GroundProp2 != null) propVariants.Add(GroundProp2);
		if (GroundProp3 != null) propVariants.Add(GroundProp3);
		if (GroundProp4 != null) propVariants.Add(GroundProp4);
		if (GroundProp5 != null) propVariants.Add(GroundProp5);

		var rng = new RandomNumberGenerator();
		rng.Seed = unchecked((ulong)RandomSeed);

		if (variants.Count > 0)
		{
			var placed = 0;
			var tries = 0;
			var maxTries = Mathf.Max(TotalTrees * 64, 64);
			while (placed < TotalTrees && tries < maxTries)
			{
				tries++;
				if (!TrySampleXZ(rng, out var x, out var z))
					continue;

				var prefab = variants[rng.RandiRange(0, variants.Count - 1)];
				var tree = prefab.Instantiate<Node3D>();
				AddChild(tree);

				var y = SampleGroundY(x) + BaseHeight + rng.RandfRange(-HeightJitter, HeightJitter);
				tree.Position = new Vector3(x, y, z);

				tree.Rotation = new Vector3(0f, rng.RandfRange(0f, Mathf.Tau), 0f);
				var s = rng.RandfRange(ScaleMinMax.X, ScaleMinMax.Y);
				tree.Scale = new Vector3(s, s, s);
				placed++;
			}

			if (placed < TotalTrees)
				GD.PushWarning($"{Name}: ForestScatter — sijoitettiin vain {placed}/{TotalTrees} puuta (ExcludeRect tai alue liian tiukka).");
		}

		if (propVariants.Count == 0 || TotalGroundProps <= 0)
			return;

		var propsPlaced = 0;
		var propTries = 0;
		var maxPropTries = Mathf.Max(TotalGroundProps * 64, 64);
		while (propsPlaced < TotalGroundProps && propTries < maxPropTries)
		{
			propTries++;
			if (!TrySamplePropsXZ(rng, out var x, out var z))
				continue;

			var prefab = propVariants[rng.RandiRange(0, propVariants.Count - 1)];
			var prop = prefab.Instantiate<Node3D>();
			AddChild(prop);

			var y = SampleGroundY(x) + BaseHeight + rng.RandfRange(-PropsHeightJitter, PropsHeightJitter);
			prop.Position = new Vector3(x, y, z);

			prop.Rotation = new Vector3(0f, rng.RandfRange(0f, Mathf.Tau), 0f);
			var s = rng.RandfRange(PropsScaleMinMax.X, PropsScaleMinMax.Y);
			prop.Scale = new Vector3(s, s, s);
			propsPlaced++;
		}

		if (propsPlaced < TotalGroundProps)
			GD.PushWarning($"{Name}: ForestScatter — sijoitettiin vain {propsPlaced}/{TotalGroundProps} proppia (ExcludeRect tai alue liian tiukka).");
	}

	/// <summary>
	/// Tien yläpinta world-X:ää pitkin: rampin taso (local y = RoadTopLocalY) → sekoitus → apron-taso.
	/// </summary>
	private float SampleGroundY(float worldX)
	{
		if (!UseRampRoadSurfaceY)
			return worldX * HeightAlongX;

		var ax = RampAxisX;
		var ay = RampAxisY;
		var o = RampOrigin;
		// W = O + lx*Ax + RoadTopLocalY*Ay (+ lz*Az); Az=(0,0,1) → lx ratkaistaan W.x:stä
		var lx = (worldX - o.X - RoadTopLocalY * ay.X) / ax.X;
		if (ClampRampAlongMaxLx)
			lx = Mathf.Min(lx, MaxRampAlongLx);
		var rampY = o.Y + lx * ax.Y + RoadTopLocalY * ay.Y;

		if (worldX <= BlendToApronStartX)
			return rampY;
		if (worldX >= BlendToApronEndX)
			return ApronRoadTopWorldY;
		var span = BlendToApronEndX - BlendToApronStartX;
		if (span <= 0.0001f)
			return ApronRoadTopWorldY;
		var t = (worldX - BlendToApronStartX) / span;
		t = Mathf.Clamp(t, 0f, 1f);
		if (BlendSmoothstep)
			t = t * t * (3f - 2f * t);
		return Mathf.Lerp(rampY, ApronRoadTopWorldY, t);
	}

	private bool IsInsideExcludeRect(float x, float z)
	{
		if (!ExcludeRectEnabled)
			return false;
		return x >= ExcludeRectMinXZ.X && x <= ExcludeRectMaxXZ.X
			&& z >= ExcludeRectMinXZ.Y && z <= ExcludeRectMaxXZ.Y;
	}

	private bool TrySampleXZ(RandomNumberGenerator rng, out float x, out float z)
	{
		const int maxAttempts = 48;
		for (var a = 0; a < maxAttempts; a++)
		{
			x = rng.RandfRange(XMinMax.X, XMinMax.Y);
			z = rng.RandfRange(ZMinMax.X, ZMinMax.Y);
			if (!IsInsideExcludeRect(x, z))
				return true;
		}

		x = 0f;
		z = 0f;
		return false;
	}

	private bool TrySamplePropsXZ(RandomNumberGenerator rng, out float x, out float z)
	{
		const int maxAttempts = 48;
		for (var a = 0; a < maxAttempts; a++)
		{
			x = rng.RandfRange(XMinMax.X, XMinMax.Y);
			z = rng.RandfRange(PropsZMinMax.X, PropsZMinMax.Y);
			if (!IsInsideExcludeRect(x, z))
				return true;
		}

		x = 0f;
		z = 0f;
		return false;
	}
}
