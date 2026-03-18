using Godot;

public partial class PlayerController : CharacterBody3D
{
	[Export] public float Speed = 5.0f;
	[Export] public float JumpVelocity = 8.0f;
	[Export] public float Gravity = 20.0f;

	public override void _PhysicsProcess(double delta)
	{
		Vector3 velocity = Velocity;

		// Painovoima
		if (!IsOnFloor())
			velocity.Y -= Gravity * (float)delta;

		// Hyppy
		if (Input.IsActionJustPressed("ui_accept") && IsOnFloor())
			velocity.Y = JumpVelocity;

		// Vaaka-liike (vain X-akseli — sivuscrolling)
		float direction = Input.GetAxis("ui_left", "ui_right");
		velocity.X = direction * Speed;

		Velocity = velocity;
		MoveAndSlide();
	}
}
