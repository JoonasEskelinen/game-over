using Godot;

/// <summary>
/// Level 3: vierivä kivi (RigidBody3D). Pyöriminen tulee AngularVelocitysta ja liikkeestä — ei animaatiota.
/// Aseta <see cref="PendingRoadRoot"/> + <see cref="PendingSpeedScale"/> ennen AddChild, tai kutsu <see cref="Configure"/> / <see cref="LaunchProjectile"/> myöhemmin.
/// </summary>
public partial class RollingRockLevel3 : RigidBody3D
{
	[Export] public int DamageToPlayer = 1;
	[Export] public float DespawnAfterSeconds = 28f;
	[Export] public float DespawnBelowY = -120f;

	public Node3D PendingRoadRoot;
	public float PendingSpeedScale = 4f;

	/// <summary>Kiven pallovanteen säde (CollisionShape3D:n SphereShape3D) — käytetään vierintänopeuden laskuun.</summary>
	[Export] public float RollRadius = 0.44f;
	/// <summary>Kuinka suuri kulmanopeus on suhteessa ideaalivierintään (v/r). Pienempi = hitaampi, hillitympi pyöriminen.</summary>
	[Export] public float RollSpinScale = 0.14f;
	/// <summary>0 = ei tasoitusta, 1 = kova kiinnitys ideaalivierintään. Pieni arvo tasoittaa "legopalikka"-tummaa.</summary>
	[Export] public float RollAngularAlign = 0.11f;
	[Export] public float MaxRollOmega = 12f;
	[Export] public float MinTiltDegrees = 0f;
	[Export] public float MaxTiltDegrees = 3f;

	private Node3D _roadRoot;
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

	public override void _IntegrateForces(PhysicsDirectBodyState3D state)
	{
		if (_roadRoot == null || !GodotObject.IsInstanceValid(_roadRoot) || RollAngularAlign <= 0f)
			return;

		Basis b = _roadRoot.GlobalTransform.Basis;
		Vector3 n = b.Y.Normalized();
		Vector3 downhill = Vector3.Down - n * Vector3.Down.Dot(n);
		if (downhill.LengthSquared() < 1e-6f)
			downhill = -b.X;
		downhill = downhill.Normalized();

		Vector3 spinAxis = ComputeRollAxis(downhill, n, b);
		float v = state.LinearVelocity.Length();
		float targetOmega = ComputeTargetRollOmega(v);
		Vector3 target = spinAxis * Mathf.Clamp(targetOmega, 0f, MaxRollOmega);
		float t = Mathf.Clamp(RollAngularAlign, 0f, 1f);
		state.AngularVelocity = state.AngularVelocity.Lerp(target, t);
	}

	/// <summary>
	/// Heitetty kivi (torni): ei tien vierintäkiinnitystä, vain painovoima + annettu nopeus.
	/// Kutsu heti <c>AddChild</c>-ketjun ja <c>GlobalPosition</c>-asetuksen jälkeen (sama frame riittää).
	/// </summary>
	public void LaunchProjectile(Vector3 linearVelocity, Vector3 angularVelocity)
	{
		_hasPendingConfigure = false;
		PendingRoadRoot = null;
		_roadRoot = null;
		LinearVelocity = linearVelocity;
		AngularVelocity = angularVelocity;
		GravityScale = 1.15f;
		ContactMonitor = true;
		MaxContactsReported = 6;
		ApplyVisualTilt();
	}

	public void Configure(Node3D roadRoot, float speedScale = 1f)
	{
		if (roadRoot == null || !GodotObject.IsInstanceValid(roadRoot))
			return;

		_roadRoot = roadRoot;

		Basis b = roadRoot.GlobalTransform.Basis;
		Vector3 n = b.Y.Normalized();
		Vector3 downhill = Vector3.Down - n * Vector3.Down.Dot(n);
		if (downhill.LengthSquared() < 1e-6f)
			downhill = -b.X;
		downhill = downhill.Normalized();

		float speed = 11f * speedScale;
		LinearVelocity = downhill * speed;

		Vector3 spinAxis = ComputeRollAxis(downhill, n, b);
		float targetOmega = ComputeTargetRollOmega(speed);
		AngularVelocity = spinAxis * targetOmega;

		ApplyVisualTilt();

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

	private static Vector3 ComputeRollAxis(Vector3 downhill, Vector3 surfaceNormal, Basis roadBasis)
	{
		Vector3 spinAxis = downhill.Cross(surfaceNormal);
		if (spinAxis.LengthSquared() < 1e-6f)
			spinAxis = roadBasis.Z;
		else
			spinAxis = spinAxis.Normalized();
		return spinAxis;
	}

	private float ComputeTargetRollOmega(float linearSpeed)
	{
		float r = Mathf.Max(RollRadius, 0.05f);
		float omega = linearSpeed / r * RollSpinScale;
		return Mathf.Min(omega, MaxRollOmega);
	}

	private void ApplyVisualTilt()
	{
		var visual = GetNodeOrNull<Node3D>("Visual");
		if (visual == null)
			return;
		float tx = (float)GD.RandRange(MinTiltDegrees, MaxTiltDegrees);
		float tz = (float)GD.RandRange(MinTiltDegrees * 0.55f, MaxTiltDegrees * 0.9f);
		float yaw = (float)GD.RandRange(0f, 360f);
		visual.RotationDegrees = new Vector3(tx, yaw, tz);
	}
}
