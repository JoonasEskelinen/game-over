using Godot;

/// <summary>
/// Käynnistää level 1 -bossin kun EnemySpawner on spawmannut kaikki normiviholliset ja ne on tuhottu.
/// Spawn-kohta: käytä <see cref="BossStandOffsetGlobal"/> (maailma-avaruus) — paikallinen offset tanssikoneen rotaation kanssa helposti vie hahmon seinän taakse.
/// </summary>
public partial class Level1BossDirector : Node
{
	[Export] public NodePath EnemySpawnerPath = new("../EnemySpawner");
	[Export] public NodePath DanceMachinePath = new("../dance-machine2");
	[Export] public NodePath CameraPath = new("../Camera3D");
	[Export] public PackedScene BossScene;

	/// <summary>
	/// Kun true: <see cref="BossStandOffsetLocal"/> koneen avaruudessa (varo rotaatiota/skaalaa).
	/// Kun false (suositus): <see cref="BossStandOffsetGlobal"/> lisätään koneen maailmasijaintiin.
	/// </summary>
	[Export] public bool UseDanceMachineLocalOffset = false;

	[Export] public Vector3 BossStandOffsetLocal = new(0f, 0.08f, -2.1f);

	/// <summary>Lisätään dance-machine2 GlobalPositioniin (tyypillisesti negatiivinen Z vie kentän keskemmäs).</summary>
	[Export] public Vector3 BossStandOffsetGlobal = new(0f, 0.2f, -5.5f);

	[Export] public bool ClampStandInsideArena = true;
	[Export] public float ArenaStandClampHalf = 12f;

	[Export] public bool SnapStandYToFloorRaycast = true;

	/// <summary>Laskee bossin hieman alemmas snapatun lattiatason suhteen (älä käytä >~0.5 ilman clampia).</summary>
	[Export] public float BossStandExtraLowerY = 0.28f;

	/// <summary>Lisätään säteen osuman Y:hin (pieni positiivinen = jalkojen juuri lattian päällä).</summary>
	[Export] public float BossFloorRayHitYOffset = 0.08f;

	/// <summary>Et saa upottaa snapattua seisontaa enempää kuin tämä metreinä (estää putoamisen kentän alle).</summary>
	[Export] public float BossStandMaxSinkBelowRayM = 0.55f;

	[Export] public float CinematicBlendIn = 1.05f;
	[Export] public float CinematicHold = 0.45f;
	[Export] public float CinematicBlendOut = 1.35f;
	[Export] public Vector3 CinematicCameraOffsetFromBoss = new(5f, 3.4f, 7.5f);

	private EnemySpawner _spawner;
	private Node3D _danceMachine;
	private CameraFollow _camera;
	private bool _bossStarted;

	public override void _Ready()
	{
		_spawner = GetNodeOrNull<EnemySpawner>(EnemySpawnerPath);
		_danceMachine = GetNodeOrNull<Node3D>(DanceMachinePath);
		_camera = GetNodeOrNull<CameraFollow>(CameraPath);
		if (BossScene == null)
			GD.PrintErr("Level1BossDirector: BossScene puuttuu (BossLevel1.tscn).");
	}

	public override void _Process(double delta)
	{
		if (_bossStarted || BossScene == null || _spawner == null || _danceMachine == null)
			return;
		if (!_spawner.IsNormalEncounterComplete())
			return;

		_bossStarted = true;
		SpawnBossAndPlayIntro();
	}

	private Vector3 ComputeBossStandWorld()
	{
		if (!_danceMachine.IsInsideTree())
			return BossStandOffsetGlobal;

		Vector3 stand = UseDanceMachineLocalOffset
			? _danceMachine.ToGlobal(BossStandOffsetLocal)
			: _danceMachine.GlobalPosition + BossStandOffsetGlobal;

		if (ClampStandInsideArena)
		{
			float h = ArenaStandClampHalf;
			stand.X = Mathf.Clamp(stand.X, -h, h);
			stand.Z = Mathf.Clamp(stand.Z, -h, h);
		}

		bool floorSnapped = false;
		if (SnapStandYToFloorRaycast && IsInsideTree())
		{
			stand = SnapStandYToFloor(stand, out floorSnapped);
		}

		stand.Y -= Mathf.Max(0f, BossStandExtraLowerY);
		if (floorSnapped && BossStandMaxSinkBelowRayM > 0f)
		{
			float floorRef = stand.Y + BossStandExtraLowerY;
			float minAllowedY = floorRef - BossStandMaxSinkBelowRayM;
			if (stand.Y < minAllowedY)
				stand.Y = minAllowedY;
		}

		return stand;
	}

	private Vector3 SnapStandYToFloor(Vector3 stand, out bool hitFloor)
	{
		hitFloor = false;
		var w3d = (GetParent() as Node3D)?.GetWorld3D() ?? GetViewport()?.GetWorld3D();
		var space = w3d?.DirectSpaceState;
		if (space == null)
			return stand;

		var from = stand + Vector3.Up * 6f;
		var to = stand + Vector3.Down * 12f;
		var q = PhysicsRayQueryParameters3D.Create(from, to);
		q.CollideWithAreas = false;
		var hit = space.IntersectRay(q);
		if (hit.Count > 0 && hit.ContainsKey("position"))
		{
			float y = ((Vector3)hit["position"]).Y;
			stand.Y = y + BossFloorRayHitYOffset;
			hitFloor = true;
		}

		return stand;
	}

	private void SpawnBossAndPlayIntro()
	{
		Vector3 stand = ComputeBossStandWorld();
		GD.Print($"[BossDirector] stand Y={stand.Y:F3}, danceMachine Y={_danceMachine.GlobalPosition.Y:F3}");
		var boss = BossScene.Instantiate() as BossLevel1;
		if (boss == null)
		{
			GD.PrintErr("Level1BossDirector: BossScene ei ole BossLevel1.");
			return;
		}

		boss.Configure(stand);

		Node parent = GetParent();
		if (parent == null)
		{
			GD.PrintErr("Level1BossDirector: ei parent-nodea — bossia ei lisätty.");
			boss.QueueFree();
			return;
		}

		parent.AddChild(boss);

		if (_camera != null)
		{
			// Älä lue boss.GlobalPosition heti AddChild:n jälkeen (C# / puun synkronointi → !is_inside_tree).
			Vector3 lookAt = stand + Vector3.Up * 1.35f;
			Vector3 camEnd = lookAt + CinematicCameraOffsetFromBoss;
			_camera.PlayBossIntroShot(lookAt, camEnd, CinematicBlendIn, CinematicHold, CinematicBlendOut);
		}
	}
}
