using Godot;

/// <summary>
/// Avaa lattiareiän bossin kuoltua ja ohjaa pelaajan seuraavaan kenttään.
/// Liitetty Level 1 -juurinoodin lapseksi level_1.tscn:ssä.
///
/// Toimintalogiikka:
///  1. _Process pollaa bossia ryhmästä "level1_boss".
///  2. Kun IsBossDead = true, ActivateHole() käynnistyy.
///  3. Lattian yhtenäinen BoxShape poistetaan käytöstä.
///  4. Tilalle luodaan 4 StaticBody3D-palaa reiän ympärille.
///  5. Pelaaja voi kävellä reikään → putoaa → Area3D laukaisee latausruudun (spinner + palkki) ja kentän latauksen.
/// </summary>
public partial class Level1ExitHole : Node3D
{
	/// <summary>Seuraava kenttä — aseta Inspectorista.</summary>
	[Export] public string NextScene = "res://scenes/levels/level_2.tscn";

	/// <summary>
	/// Reiän keskipiste <b>ExitHole-noden paikallisissa</b> koordinaateissa (XZ, Y usein 0).
	/// ExitHole sijoitetaan editorissa reikään — oletus (0,0,0) = noden origo = oikea paikka.
	/// </summary>
	[Export] public Vector3 HoleCenter = Vector3.Zero;

	/// <summary>Reiän puolileveys X-suunnassa (metreissä).</summary>
	[Export] public float HoleHalfX = 1.1f;

	/// <summary>Reiän puolisyvyys Z-suunnassa (metreissä).</summary>
	[Export] public float HoleHalfZ = 0.65f;

	// Lattian mitat level_1:ssä (30×30 taso, puolet = 15)
	private const float FloorHalf = 15f;
	private const float FloorThick = 0.1f;

	private const string LoadingScreenPath = "res://scenes/ui/loading_screen.tscn";

	private bool _activated;
	private bool _bossSeenAlive;
	private bool _exiting;

	private MeshInstance3D _holeMesh;
	private Area3D _exitArea;

	public override void _Ready()
	{
		SetupVisualAndTrigger();
	}

	public override void _Process(double delta)
	{
		if (_activated) return;

		var boss = GetTree().GetFirstNodeInGroup("level1_boss") as BossLevel1;
		if (boss != null && GodotObject.IsInstanceValid(boss))
			_bossSeenAlive = true;

		// Aktivoidaan heti kun bossi on kuollut (IsBossDead = true tai poistunut puusta)
		if (_bossSeenAlive && (boss == null || !GodotObject.IsInstanceValid(boss) || boss.IsBossDead))
			ActivateHole();
	}

	// ─────────────────────────────────────────────
	// SETUP
	// ─────────────────────────────────────────────

	private Vector3 GetHoleCenterWorld()
		=> ToGlobal(HoleCenter);

	private void SetupVisualAndTrigger()
	{
		Vector3 holeLocal = HoleCenter;

		// Pimeä "reikä"-mesh lattiatasolla (piilotettu, kunnes aktivoituu)
		_holeMesh = new MeshInstance3D
		{
			Name      = "HoleMesh",
			Visible   = false,
			Position  = holeLocal + Vector3.Up * 0.02f,
			// QuadMesh on oletuksena XY-tasossa — käännetään XZ-tasoon (näkyy ylhäältä)
			Rotation  = new Vector3(-Mathf.Pi / 2f, 0f, 0f),
		};
		_holeMesh.Mesh = new QuadMesh
		{
			Size = new Vector2(HoleHalfX * 2f, HoleHalfZ * 2f),
		};
		_holeMesh.MaterialOverride = new StandardMaterial3D
		{
			AlbedoColor = Colors.Black,
			ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
			CullMode    = BaseMaterial3D.CullModeEnum.Disabled,
		};
		AddChild(_holeMesh);

		// Area3D reiän alla — tunnistaa pelaajan putoamisen
		_exitArea = new Area3D
		{
			Name        = "ExitArea",
			Monitoring  = false,
			Monitorable = false,
			Position    = holeLocal + Vector3.Down * 2f,
			// Kaikki fysiikkakerrokset (varmistaa BodyEntered vaikka kerroksia muutetaan)
			CollisionMask = uint.MaxValue,
		};
		_exitArea.BodyEntered += OnBodyEntered;
		AddChild(_exitArea);

		var col = new CollisionShape3D
		{
			Shape = new BoxShape3D
			{
				Size = new Vector3(HoleHalfX * 2f, 3f, HoleHalfZ * 2f),
			},
		};
		_exitArea.AddChild(col);
	}

	// ─────────────────────────────────────────────
	// AKTIVOINTI BOSSIN KUOLTUA
	// ─────────────────────────────────────────────

	private void ActivateHole()
	{
		if (_activated) return;
		_activated = true;

		RebuildFloorWithHole();

		_holeMesh.Visible        = true;
		_exitArea.Monitoring     = true;
		_exitArea.Monitorable    = true;
	}

	/// <summary>
	/// Poistaa lattian yhtenäisen BoxShape-törmäyksen ja korvaa sen
	/// neljällä palalla jotka kiertävät reiän.
	/// </summary>
	private void RebuildFloorWithHole()
	{
		var floor = GetParent()?.GetNodeOrNull<StaticBody3D>("Floor");
		if (floor == null) return;

		// Poistetaan vanha lattiatörmäys
		var oldCol = floor.GetNodeOrNull<CollisionShape3D>("CollisionShape3D");
		if (oldCol != null) oldCol.Disabled = true;

		Vector3 w = GetHoleCenterWorld();
		float hx1 = w.X - HoleHalfX;
		float hx2 = w.X + HoleHalfX;
		float hz1 = w.Z - HoleHalfZ;
		float hz2 = w.Z + HoleHalfZ;
		float f   = FloorHalf;

		// Neljä lattiapalaa reiän ympärille (xMin, zMin, leveys, syvyys)
		BuildSlab(-f,  -f,       f * 2f,   hz1 + f);     // eteläinen kaista
		BuildSlab(-f,   hz2,     f * 2f,   f - hz2);     // pohjoinen kaista
		BuildSlab(-f,   hz1,     hx1 + f,  HoleHalfZ * 2f);  // läntinen pala
		BuildSlab( hx2, hz1,     f - hx2,  HoleHalfZ * 2f);  // itäinen pala
	}

	private void BuildSlab(float xMin, float zMin, float width, float depth)
	{
		if (width <= 0f || depth <= 0f) return;

		var sb = new StaticBody3D
		{
			Name     = "FloorSlab",
			Position = new Vector3(xMin + width * 0.5f, -FloorThick * 0.5f, zMin + depth * 0.5f),
		};
		sb.AddChild(new CollisionShape3D
		{
			Shape = new BoxShape3D { Size = new Vector3(width, FloorThick, depth) },
		});
		GetParent().AddChild(sb);
	}

	// ─────────────────────────────────────────────
	// SCENE-VAIHTO
	// ─────────────────────────────────────────────

	private void OnBodyEntered(Node3D body)
	{
		if (_exiting || body is not PlayerController) return;
		if (!ResourceLoader.Exists(NextScene))
		{
			GD.PrintErr($"Level1ExitHole: seuraavaa kenttää ei löydy: {NextScene}");
			return;
		}
		_exiting = true;
		GD.Print($"Level1ExitHole: pelaaja putosi reikään → latausruutu → {NextScene}");
		GameState.Instance.BeginSceneLoad(NextScene);
		Callable.From(DeferredChangeToNextLevel).CallDeferred();
	}

	private void DeferredChangeToNextLevel()
	{
		if (!IsInsideTree() || GetTree() == null)
		{
			_exiting = false;
			GameState.Instance.PendingLoadScenePath = "";
			LoadingOverlay.Instance?.HideOverlay();
			return;
		}

		Error err = GetTree().ChangeSceneToFile(LoadingScreenPath);
		if (err != Error.Ok)
		{
			GD.PrintErr($"Level1ExitHole: loading_screen epäonnistui ({err}) — synkroninen fallback.");
			GameState.Instance.PendingLoadScenePath = "";
			LoadingOverlay.Instance?.ScheduleHideAfterFrames(4);
			Error err2 = GetTree().ChangeSceneToFile(NextScene);
			if (err2 != Error.Ok)
			{
				GD.PrintErr($"Level1ExitHole: ChangeSceneToFile epäonnistui ({err2}): {NextScene}");
				LoadingOverlay.Instance?.HideOverlay();
				_exiting = false;
			}
		}
	}
}
