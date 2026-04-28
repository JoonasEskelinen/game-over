using System;
using Godot;

/// <summary>
/// level_1: lisää propseille yhden BoxShape3D StaticBodyn (ei trimeshäjä — Pi-ystävällinen).
/// air-hockey2: älä lisää editorissa RigidBody3D / CollisionShape3D -lapsia instanssin alle — fysiikka
/// rakennetaan täällä (AirHockeyRigid + yksi laatikko AABB:sta). Väärä hierarkia = varoitus + päällekkäiset törmäykset.
///
/// GLB-instansseissa on usein oma StaticBody/CollisionShape — jos se jää päälle, pelaaja törmää
/// sekä siihen että synteettiseen laatikkoon (väärät mitat → “näkymätön seinä” vain toisesta suunnasta).
/// </summary>
public partial class Level1ArcadePhysicsSetup : Node3D
{
	[Export] public string AirHockeyNodeName = "air-hockey2";

	[Export] public float AirHockeyMass = 38f;

	/// <summary>Kerros (bitmask), jolle arcade-propput menevät — Level 1 -bossin syöksy (mask 1) ei törmää.</summary>
	[Export] public uint ArcadePropCollisionLayer = 16u;

	/// <summary>
	/// Ilmanvastus / lineaaridamppi air-hockey-pöydälle.
	/// Korkea arvo = pöytä pysähtyy nopeasti kun neliö päästetään irti → raskas tuntuma.
	/// </summary>
	[Export] public float AirHockeyLinearDamp = 7f;

	/// <summary>
	/// Air-hockey AABB-laatikon maksimikoko (m) per aksele — jos GLB:ssä on ylimääräinen mesh, AABB voi paisua.
	/// </summary>
	[Export] public Vector3 AirHockeyCollisionSizeMax = new(3.2f, 0.75f, 1.9f);

	/// <summary>
	/// Floor-lapsia joiden kohdalla ei lisätä automaattista laatikko-fysiikkaa (koristeet, kapeat käytävät).
	/// wall2/wall3: nyt normaali AABB-<c>_Phys</c> (layer 1) — läpikävely estetään; bossi väistää kerroksella 16.
	/// </summary>
	[Export] public string[] FloorSkipAutoPhysicsNames = { "wall-window2" };

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
			if (ShouldSkipFloorAutoPhysics(nd.Name))
			{
				DisableAllCollision(nd);
				continue;
			}
			if (nd.HasMeta("arcade_phys_done"))
				continue;
			AddStaticBoxForVisual(nd, floor, false);
		}
	}

	private bool ShouldSkipFloorAutoPhysics(StringName nodeName)
	{
		if (FloorSkipAutoPhysicsNames == null || FloorSkipAutoPhysicsNames.Length == 0)
			return false;
		string n = nodeName.ToString();
		foreach (string skip in FloorSkipAutoPhysicsNames)
		{
			if (string.IsNullOrEmpty(skip))
				continue;
			if (n == skip)
				return true;
		}
		return false;
	}

	private static void DisableAllCollision(Node node)
	{
		if (node is CollisionObject3D co)
		{
			co.CollisionLayer = 0;
			co.CollisionMask = 0;
		}
		foreach (Node c in node.GetChildren())
			DisableAllCollision(c);
	}

	private void SetupRootProps()
	{
		foreach (var child in GetChildren())
		{
			if (child is MeshInstance3D or CollisionShape3D)
				continue;
			// Area3D (esim. JoystickLever): autop-fysiikka kutsuu StripBuiltInCollisionUnder → törmäys nollaan → BodyEntered ei koskaan laukea.
			if (child is Area3D)
				continue;
			var s = child.Name.ToString();
			if (s is "Player" or "Camera3D" or "WorldEnvironment" or "Sun" or "EnemySpawner" or "HUD" or "Floor")
				continue;
			if (s.StartsWith("Wall_") || s.StartsWith("Neon_"))
				continue;
			// Ikkunaseinät: autop-laatikko voi olla ylimitallinen; poistetaan myös GLB:n oma törmäys.
			if (s.StartsWith("wall-window", StringComparison.OrdinalIgnoreCase))
			{
				if (child is Node3D wnd)
					DisableAllCollision(wnd);
				continue;
			}

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
			LinearDamp = AirHockeyLinearDamp,
			AngularDamp = 8f
		};
		rb.AddToGroup("grabbable");
		parent.AddChild(rb);
		parent.MoveChild(rb, idx);
		ApplyRigidBodyWithoutInheritedScale(rb, gt);
		rb.CollisionLayer = ArcadePropCollisionLayer;
		// Mask = 0: pöytä ei reagoi fysiikalla kenenkään törmäykseen (ei pelaajan eikä muidenkaan).
		// AxisLockLinearY pitää pöydän korkeudella ilman lattiakollisiota.
		// Pelaajan oma maski (kerros 5) silti estää pelaajaa kävelemästä pöydän läpi.
		rb.CollisionMask = 0;
		rb.AddChild(airRoot);
		airRoot.Transform = Transform3D.Identity;

		Vector3 inheritedScale = gt.Basis.Scale;
		if (inheritedScale.LengthSquared() > 1e-8f)
			airRoot.Scale = inheritedScale;

		if (!TryUnionVisualAabb(airRoot, out Aabb worldAabb))
		{
			var fsz = new Vector3(2.2f, 0.45f, 1.25f);
			var fctr = rb.GlobalPosition + Vector3.Up * 0.35f;
			worldAabb = new Aabb(fctr - fsz * 0.5f, fsz);
		}

		Vector3 unionFull = worldAabb.Size * 1.02f;

		Vector3 sz = unionFull;
		sz.X = Mathf.Min(sz.X, Mathf.Max(0.05f, AirHockeyCollisionSizeMax.X));
		sz.Y = Mathf.Min(sz.Y, Mathf.Max(0.05f, AirHockeyCollisionSizeMax.Y));
		sz.Z = Mathf.Min(sz.Z, Mathf.Max(0.05f, AirHockeyCollisionSizeMax.Z));

		Vector3 centerW = worldAabb.GetCenter();
		bool shrankXZ = sz.X + 1e-4f < unionFull.X || sz.Z + 1e-4f < unionFull.Z;
		if (shrankXZ)
		{
			centerW.X = rb.GlobalPosition.X;
			centerW.Z = rb.GlobalPosition.Z;
		}

		var col = new CollisionShape3D
		{
			Position = rb.ToLocal(centerW),
			Shape = new BoxShape3D { Size = sz }
		};
		rb.AddChild(col);
		StripBuiltInCollisionUnder(airRoot);
	}

	/// <summary>
	/// Godot skaalaa törmäysmuotoja RigidBodyn transformilla — iso editor-skaala (esim. 2×) tuplaa BoxShapen maailmakoon.
	/// Puretaan rotaatio + positio RB:lle ja siirretään mittakaava vain visuaalin juureen.
	/// </summary>
	private static void ApplyRigidBodyWithoutInheritedScale(RigidBody3D rb, Transform3D globalWithScale)
	{
		Vector3 origin = globalWithScale.Origin;
		Basis b = globalWithScale.Basis;
		Vector3 sc = b.Scale;

		Basis rotationOnly = Mathf.Max(Mathf.Max(Mathf.Abs(sc.X), Mathf.Abs(sc.Y)), Mathf.Abs(sc.Z)) > 1e-8f
			? b.Scaled(new Vector3(
				1f / Mathf.Max(Mathf.Abs(sc.X), 1e-8f),
				1f / Mathf.Max(Mathf.Abs(sc.Y), 1e-8f),
				1f / Mathf.Max(Mathf.Abs(sc.Z), 1e-8f)))
			: Basis.Identity;

		rb.GlobalTransform = new Transform3D(rotationOnly, origin);
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
		StripBuiltInCollisionUnder(visualRoot);
	}

	/// <summary>
	/// Poistaa vain tämän visuaalipuun alta löytyvät törmäykset (ei koske juuri luotua *_Phys / RigidBodyä).
	/// </summary>
	private static void StripBuiltInCollisionUnder(Node node)
	{
		if (node is CollisionObject3D co)
		{
			co.CollisionLayer = 0;
			co.CollisionMask = 0;
		}
		foreach (Node c in node.GetChildren())
			StripBuiltInCollisionUnder(c);
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
