using Godot;

/// <summary>
/// Level 2: PipeB:n päätyseinään (Wall_End) ilmestyy aukko bossin kuoltua.
/// Poistuminen: pelaaja (<see cref="PlayerController"/>) tai erikoiskissa (<see cref="Level2SpecialCat"/>)
/// astuu laukaisualueelle → sama latausruutu kuin level 1 → <see cref="NextScene"/>.
/// </summary>
public partial class Level2BossExit : Node3D
{
	[Export] public string NextScene = "res://scenes/levels/level_3.tscn";

	/// <summary>Suhteessa tähän Node3D:hen — PipeB on sibling, ei lapsi.</summary>
	[Export] public NodePath WallEndPath = new("../PipeB/Wall_End");

	/// <summary>
	/// Reiän puolileveys Z-suunnassa (Wall_End paikallinen). Level2SpecialCat on skaalattu ~3× —
	/// tehokas säde ≈0.66 m → aukon pitää olla selvästi leveämpi kuin pelaajan “aukko”-idea ilman skaalaa.
	/// </summary>
	[Export] public float HoleHalfWidthZ = 0.78f;

	/// <summary>Seinän keskipisteen paikallinen Y (Wall_End); laatikko ±2.</summary>
	[Export] public float HoleMinYLocal = -2f;

	/// <summary>Yläreuna-aukon pitää peittää skaalattu kissa (~1.44 m korkea kapseli).</summary>
	[Export] public float HoleMaxYLocal = 0.12f;

	private const string LoadingScreenPath = "res://scenes/ui/loading_screen.tscn";

	private bool _activated;
	private bool _bossSeenAlive;
	private bool _exiting;

	private StaticBody3D _wallEnd;
	private Area3D _exitArea;

	public override void _Ready()
	{
		if (ResolveWallEnd() == null)
			GD.PushWarning("Level2BossExit: Wall_End ei löydy — tarkista WallEndPath (../PipeB/Wall_End).");
	}

	public override void _Process(double delta)
	{
		if (_activated)
			return;

		// Ryhmä tyhjenee heti Die():ssä — luotettavin tapa on sibling BossLevel2 (tunnistaa IsBossDead).
		var boss = GetParent()?.GetNodeOrNull<BossLevel2>("BossLevel2");
		if (boss == null && GetTree() != null)
			boss = GetTree().GetFirstNodeInGroup("level2_boss") as BossLevel2;

		if (boss != null && GodotObject.IsInstanceValid(boss) && !boss.IsBossDead)
			_bossSeenAlive = true;

		bool bossDefeated = boss != null && GodotObject.IsInstanceValid(boss) && boss.IsBossDead;
		bool bossGone = boss == null || !GodotObject.IsInstanceValid(boss);
		if (_bossSeenAlive && (bossDefeated || bossGone))
			ActivateHole();
	}

	private StaticBody3D ResolveWallEnd()
	{
		if (GodotObject.IsInstanceValid(_wallEnd))
			return _wallEnd;

		var w = GetNodeOrNull<StaticBody3D>(WallEndPath);
		if (w == null && GetParent() != null)
			w = GetParent().GetNodeOrNull<StaticBody3D>("PipeB/Wall_End");

		_wallEnd = w;
		return _wallEnd;
	}

	private void ActivateHole()
	{
		if (_activated)
			return;

		var wall = ResolveWallEnd();
		if (wall == null || !GodotObject.IsInstanceValid(wall))
		{
			GD.PushError("Level2BossExit: Wall_End puuttuu — ei voitu avata reikää.");
			return;
		}

		_activated = true;
		_wallEnd = wall;

		float hz = Mathf.Clamp(HoleHalfWidthZ, 0.08f, 3.8f);
		float yLo = Mathf.Clamp(HoleMinYLocal, -2f, 2f);
		float yHi = Mathf.Clamp(HoleMaxYLocal, -2f, 2f);
		if (yHi <= yLo + 0.05f)
			yHi = Mathf.Min(yLo + 0.55f, 2f);

		var oldCol = _wallEnd.GetNodeOrNull<CollisionShape3D>("CollisionShape3D");
		if (oldCol != null)
			oldCol.Disabled = true;

		var oldMesh = _wallEnd.GetNodeOrNull<MeshInstance3D>("MeshInstance3D");
		if (oldMesh != null)
			oldMesh.Visible = false;

		const float hzWall = 4f;

		var frameRoot = new Node3D { Name = "HoleCollisionFrame" };
		_wallEnd.AddChild(frameRoot);

		var mat = CreatePipeLikeMaterial();

		if (yLo > -2f + 1e-3f)
			AddSlab(frameRoot, mat, new Vector3(0f, (yLo + -2f) * 0.5f, 0f), new Vector3(0.5f, yLo + 2f, hzWall * 2f));

		if (yHi < 2f - 1e-3f)
			AddSlab(frameRoot, mat, new Vector3(0f, (2f + yHi) * 0.5f, 0f), new Vector3(0.5f, 2f - yHi, hzWall * 2f));

		if (hz < hzWall - 1e-3f)
		{
			float zNegHalf = (-hzWall + -hz) * 0.5f;
			float zNegDepth = hzWall - hz;
			AddSlab(frameRoot, mat, new Vector3(0f, (yLo + yHi) * 0.5f, zNegHalf), new Vector3(0.5f, yHi - yLo, zNegDepth));

			float zPosHalf = (hz + hzWall) * 0.5f;
			AddSlab(frameRoot, mat, new Vector3(0f, (yLo + yHi) * 0.5f, zPosHalf), new Vector3(0.5f, yHi - yLo, zNegDepth));
		}

		AddHoleQuad(frameRoot, hz, yLo, yHi);

		// Iso laukaisin PipeB-tilassa seinän ulkopuolella (Jolt + Area -maskit vaihtelevat).
		var pipeB = GetParent()?.GetNodeOrNull<Node3D>("PipeB");
		if (pipeB != null)
		{
			_exitArea = new Area3D
			{
				Name = "CatExitToLevel3Area",
				Monitoring = true,
				Monitorable = false,
				CollisionMask = uint.MaxValue,
			};
			_exitArea.BodyEntered += OnExitBodyEntered;
			pipeB.AddChild(_exitArea);
			// Wall_End ≈ x=180; ohut seinä — törmäysaukko jälkeen kissan kuuluu kävellä tähän (maailma ≈ PipeB).
			_exitArea.Position = new Vector3(181.6f, 0.55f, 0f);
			var exitShape = new CollisionShape3D
			{
				Shape = new BoxShape3D { Size = new Vector3(5f, 2.8f, 4.5f) },
			};
			_exitArea.AddChild(exitShape);
		}
		else
		{
			GD.PushWarning("Level2BossExit: PipeB puuttuu — käytä seinän paikallista exit-aluetta.");
			float cy = (yLo + yHi) * 0.5f;
			_exitArea = new Area3D
			{
				Name = "CatExitToLevel3Area",
				Position = new Vector3(0.55f, cy, 0f),
				Monitoring = true,
				Monitorable = false,
				CollisionMask = uint.MaxValue,
			};
			_exitArea.BodyEntered += OnExitBodyEntered;
			_wallEnd.AddChild(_exitArea);
			_exitArea.AddChild(new CollisionShape3D
			{
				Shape = new BoxShape3D
				{
					Size = new Vector3(2.4f, Mathf.Max(yHi - yLo, 0.4f) + 0.6f, hz * 2f + 0.5f),
				},
			});
		}
	}

	private static void AddSlab(Node3D parent, StandardMaterial3D mat, Vector3 centerLocal, Vector3 size)
	{
		var sb = new StaticBody3D();
		var mi = new MeshInstance3D
		{
			Mesh = new BoxMesh { Size = size },
			MaterialOverride = mat,
		};
		sb.Position = centerLocal;
		sb.AddChild(mi);
		sb.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = size } });
		parent.AddChild(sb);
	}

	private static void AddHoleQuad(Node3D parent, float hz, float yLo, float yHi)
	{
		float h = yHi - yLo;
		if (h <= 0f || hz <= 0f)
			return;

		var quad = new MeshInstance3D
		{
			Name = "HoleDarkQuad",
			Position = new Vector3(0.02f, (yLo + yHi) * 0.5f, 0f),
			Rotation = new Vector3(0f, Mathf.Pi / 2f, 0f),
			Mesh = new QuadMesh { Size = new Vector2(h, hz * 2f) },
			MaterialOverride = new StandardMaterial3D
			{
				AlbedoColor = Colors.Black,
				ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
				CullMode = BaseMaterial3D.CullModeEnum.Disabled,
			},
		};
		parent.AddChild(quad);
	}

	private static StandardMaterial3D CreatePipeLikeMaterial()
	{
		return new StandardMaterial3D
		{
			AlbedoColor = new Color(0.13f, 0.16f, 0.13f),
			Metallic = 0.7f,
			Roughness = 0.35f,
		};
	}

	private void OnExitBodyEntered(Node3D body)
	{
		if (_exiting)
			return;
		if (body is not PlayerController && body is not Level2SpecialCat)
			return;
		_exiting = true;

		if (!ResourceLoader.Exists(NextScene))
		{
			GD.PrintErr($"Level2BossExit: seuraavaa kenttää ei löydy: {NextScene}");
			_exiting = false;
			return;
		}

		GameState.Instance.PendingLoadScenePath = NextScene;
		Error err = GetTree().ChangeSceneToFile(LoadingScreenPath);
		if (err != Error.Ok)
		{
			GD.PrintErr($"Level2BossExit: loading_screen epäonnistui ({err}).");
			GameState.Instance.PendingLoadScenePath = "";
			Error err2 = GetTree().ChangeSceneToFile(NextScene);
			if (err2 != Error.Ok)
			{
				GD.PrintErr($"Level2BossExit: ChangeSceneToFile ({err2}): {NextScene}");
				_exiting = false;
			}
		}
	}
}
