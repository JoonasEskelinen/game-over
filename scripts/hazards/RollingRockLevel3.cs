using Godot;

/// <summary>
/// Level 3: vierivä kivi (RigidBody3D). Pyöriminen tulee AngularVelocitysta ja liikkeestä — ei animaatiota.
/// Aseta <see cref="PendingRoadRoot"/> + <see cref="PendingSpeedScale"/> ennen AddChild, tai kutsu <see cref="Configure"/> myöhemmin.
/// </summary>
public partial class RollingRockLevel3 : RigidBody3D
{
	[Export] public int DamageToPlayer = 1;
	[Export] public float DespawnAfterSeconds = 28f;
	[Export] public float DespawnBelowY = -120f;

	public Node3D PendingRoadRoot;
	public float PendingSpeedScale = 1f;

	private Area3D _hitArea;
	private bool _damagedPlayer;
	private double _alive;
	private bool _hasPendingConfigure;

	public override void _Ready()
	{
		_hitArea = GetNodeOrNull<Area3D>("HitArea");
		if (_hitArea != null)
			_hitArea.BodyEntered += OnHitBodyEntered;

		if (PendingRoadRoot != null && GodotObject.IsInstanceValid(PendingRoadRoot))
		{
			_hasPendingConfigure = true;
			CallDeferred(nameof(ApplyPendingConfigure));
		}
	}

	private void ApplyPendingConfigure()
	{
		if (!_hasPendingConfigure)
			return;
		_hasPendingConfigure = false;
		Node3D rr = PendingRoadRoot;
		float s = PendingSpeedScale;
		PendingRoadRoot = null;
		if (rr != null && GodotObject.IsInstanceValid(rr))
			Configure(rr, s);
	}

	public void Configure(Node3D roadRoot, float speedScale = 1f)
	{
		if (roadRoot == null || !GodotObject.IsInstanceValid(roadRoot))
			return;

		Basis b = roadRoot.GlobalTransform.Basis;
		Vector3 n = b.Y.Normalized();
		Vector3 downhill = Vector3.Down - n * Vector3.Down.Dot(n);
		if (downhill.LengthSquared() < 1e-6f)
			downhill = -b.X;
		downhill = downhill.Normalized();

		float speed = 11f * speedScale;
		LinearVelocity = downhill * speed;

		Vector3 spinAxis = downhill.Cross(n);
		if (spinAxis.LengthSquared() < 1e-6f)
			spinAxis = b.Z;
		else
			spinAxis = spinAxis.Normalized();
		AngularVelocity = spinAxis * (speed * 0.55f);

		GravityScale = 1.15f;
		ContactMonitor = true;
		MaxContactsReported = 6;
	}

	public override void _PhysicsProcess(double delta)
	{
		_alive += delta;
		if (_alive >= DespawnAfterSeconds || GlobalPosition.Y < DespawnBelowY)
			QueueFree();
	}

	private void OnHitBodyEntered(Node3D body)
	{
		TryDamagePlayer(body);
	}

	private void TryDamagePlayer(Node3D body)
	{
		if (_damagedPlayer || body == null) return;
		if (!body.IsInGroup("player")) return;
		var hc = body.GetNodeOrNull<HealthComponent>("HealthComponent");
		if (hc == null) return;
		hc.TakeDamage(Mathf.Max(1, DamageToPlayer));
		_damagedPlayer = true;
	}
}
