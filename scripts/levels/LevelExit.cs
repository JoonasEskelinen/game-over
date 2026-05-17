using Godot;

// LevelExit — Area3D joka vaihtaa seuraavaan kenttään kun pelaaja astuu siihen.
// Aseta NextScene Inspectorissa.
public partial class LevelExit : Area3D
{
	[Export] public string NextScene = "res://scenes/levels/World1/level_1_2.tscn";

	private bool _triggered = false;

	public override void _Ready()
	{
		BodyEntered += OnBodyEntered;
	}

	private void OnBodyEntered(Node3D body)
	{
		if (_triggered) return;
		if (body is not PlayerController) return;

		_triggered = true;
		GD.Print($"Kenttä läpäisty! Siirrytään: {NextScene}");

		Callable.From(DeferredChangeScene).CallDeferred();
	}

	private void DeferredChangeScene()
	{
		if (!IsInsideTree() || GetTree() == null)
		{
			_triggered = false;
			return;
		}
		GetTree().ChangeSceneToFile(NextScene);
	}
}
