using Godot;

// Checkpoint — Area3D joka tallentaa viimeisimmän respawn-pisteen.
// Lisää Area3D-nodeen kentällä. Aseta SpawnOffset niin että pelaaja
// spawnautuu alustan päälle, ei sisälle.
public partial class Checkpoint : Area3D
{
	// Staattinen — kaikki checkpointit jakavat saman viimeisimmän sijainnin
	public static Vector3 LastPosition { get; private set; } = new Vector3(0, 2, 0);

	// Offsetti alustan pinnasta — pelaaja spawnataan tämän verran ylöspäin
	[Export] public Vector3 SpawnOffset = new Vector3(0, 1.5f, 0);

	private bool _activated = false;
	private MeshInstance3D _indicator;

	public override void _Ready()
	{
		BodyEntered += OnBodyEntered;

		// Visuaalinen indikaattori (vihreä pallo jos löytyy lapsinodesta)
		_indicator = GetNodeOrNull<MeshInstance3D>("Indicator");
	}

	private void OnBodyEntered(Node3D body)
	{
		if (_activated) return;
		if (body is not PlayerController) return;

		_activated = true;
		LastPosition = GlobalPosition + SpawnOffset;
		GD.Print($"Checkpoint aktivoitu: {LastPosition}");

		// Vaihda indikaattorin väriä aktivoinnin merkiksi
		if (_indicator?.GetSurfaceOverrideMaterial(0) is StandardMaterial3D mat)
			mat.AlbedoColor = new Color(0.2f, 1f, 0.3f);
	}

	// Nollaa checkpoint uuden pelin alussa (kutsutaan Level_1_1:stä)
	public static void ResetToDefault(Vector3 spawnPosition)
	{
		LastPosition = spawnPosition;
	}
}
