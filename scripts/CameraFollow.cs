using Godot;

public partial class CameraFollow : Camera3D
{
	[Export] public NodePath PlayerPath;
	private Node3D _player;

	[Export] public float FollowSpeed = 5.0f;
	[Export] public Vector3 Offset = new Vector3(0, 2, 10);

	public override void _Ready()
	{
		_player = GetNode<Node3D>(PlayerPath);
	}

	public override void _PhysicsProcess(double delta)
	{
		if (_player == null) return;

		Vector3 targetPosition = _player.GlobalPosition + Offset;
		GlobalPosition = GlobalPosition.Lerp(targetPosition, FollowSpeed * (float)delta);
	}
}
