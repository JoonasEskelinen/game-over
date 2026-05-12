using Godot;

/// <summary>
/// Dronen pudottama pommi (RigidBody). Osuu <see cref="BossLevel3"/>-alueeseen fysiikkakerroksen 14 kautta.
/// Visuaali: <see cref="BombPackedScene"/> tai <see cref="BombGlbPath"/> (oletus bomb.glb); varalla yksinkertainen pallo.
/// </summary>
public partial class Level3DroneBomb : RigidBody3D
{
	[Export] public PackedScene BombPackedScene;
	[Export] public string BombGlbPath = "res://assets/models/drone/bomb.glb";
	[Export] public float VisualUniformScale = 1f;
	[Export] public int BossDamage = 7;
	[Export] public float ElinaikaSek = 18f;
	[Export] public float PainovoimaSkaala = 1.35f;
	[Export] public float FallbackSphereRadius = 0.28f;

	private bool _hitConsumed;
	private double _alive;

	public override void _Ready()
	{
		AddToGroup("level3_drone_bomb");
		GravityScale = PainovoimaSkaala;
		ContactMonitor = true;
		MaxContactsReported = 4;
		CollisionLayer = BossLevel3.DroneBombPhysicsLayer;
		CollisionMask = 1;
		BuildVisual();
	}

	private void BuildVisual()
	{
		PackedScene scene = BombPackedScene;
		if (scene == null && !string.IsNullOrEmpty(BombGlbPath) && ResourceLoader.Exists(BombGlbPath))
			scene = GD.Load<PackedScene>(BombGlbPath);

		if (scene != null)
		{
			var inst = scene.Instantiate<Node>();
			Node3D rootNd;
			if (inst is Node3D nd)
			{
				rootNd = nd;
				AddChild(rootNd);
			}
			else
			{
				var holder = new Node3D { Name = "BombVisualRoot" };
				holder.AddChild(inst);
				AddChild(holder);
				rootNd = holder;
			}

			rootNd.Scale = Vector3.One * VisualUniformScale;
			AngularVelocity = new Vector3(2.2f, 3.6f, 1.1f);
			return;
		}

		GD.PushWarning($"Level3DroneBomb: mallia ei löydy ({BombGlbPath}) — käytetään pallo-placeholderia.");
		AddFallbackSphere();
	}

	private void AddFallbackSphere()
	{
		var mesh = new MeshInstance3D { Name = "BombVisual" };
		float r = FallbackSphereRadius;
		mesh.Mesh = new SphereMesh { Radius = r, Height = r * 2f };
		var mat = new StandardMaterial3D
		{
			ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
			AlbedoColor = new Color(1f, 0.42f, 0.06f),
			EmissionEnabled = true,
			Emission = new Color(1f, 0.35f, 0.02f),
			EmissionEnergyMultiplier = 1.35f,
		};
		mesh.SetSurfaceOverrideMaterial(0, mat);
		AddChild(mesh);
		AngularVelocity = new Vector3(2.2f, 3.6f, 1.1f);
	}

	public override void _PhysicsProcess(double delta)
	{
		_alive += delta;
		if (_alive >= ElinaikaSek)
			QueueFree();
	}

	/// <summary>Yksi pommi voi vahingoittaa bossia vain kerran.</summary>
	public bool TryConsumeBossHit()
	{
		if (_hitConsumed)
			return false;
		_hitConsumed = true;
		CallDeferred(nameof(DeferredFree));
		return true;
	}

	private void DeferredFree() => QueueFree();
}
