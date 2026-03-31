using Godot;

/// <summary>
/// Käytössä vain BossLevel1_Playtest.tscn — kääntää kameran kohti origoa (boss).
/// </summary>
public partial class BossLevel1PlaytestCamera : Camera3D
{
	[Export] public Vector3 LookAtTarget = new(0f, 1.35f, 0f);

	public override void _Ready()
	{
		LookAt(LookAtTarget, Vector3.Up);
	}
}
