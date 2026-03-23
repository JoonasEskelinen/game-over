using Godot;

// KillZone — Area3D joka tappaa pelaajan kun hän putoaa kentän ulkopuolelle.
// Lisää tämä skripti Area3D-nodeen tason pohjalle.
public partial class KillZone : Area3D
{
	public override void _Ready()
	{
		BodyEntered += OnBodyEntered;
	}

	private void OnBodyEntered(Node3D body)
	{
		if (body is PlayerController player)
			player.Respawn(Checkpoint.LastPosition);
	}
}
