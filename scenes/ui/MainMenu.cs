using Godot;

public partial class MainMenu : Control
{
	public override void _Ready()
	{
		GetNode<Button>("VBoxContainer/NewGameButton").Pressed       += OnNewGamePressed;
		GetNode<Button>("VBoxContainer/LoadGameButton").Pressed      += OnLoadGamePressed;
		GetNode<Button>("VBoxContainer/InstructionsButton").Pressed  += OnInstructionsPressed;
		GetNode<Button>("VBoxContainer/QuitButton").Pressed          += OnQuitPressed;

		// Tarkista onko tallenne olemassa — jos ei, harmauta Load-nappi
		var config = new ConfigFile();
		if (config.Load("user://savegame.cfg") != Error.Ok)
			GetNode<Button>("VBoxContainer/LoadGameButton").Disabled = true;
	}

	private void OnNewGamePressed()
	{
		// Nollataan elämät ja aloitetaan alusta
		GetTree().ChangeSceneToFile("res://scenes/levels/World1/level_1.tscn");
	}

	private void OnLoadGamePressed()
	{
		// Ladataan tallennus — jatkaa siitä mihin jäi
		// TODO: tallenna myös viimeisin kenttä jotta voidaan ladata oikea kenttä
		GetTree().ChangeSceneToFile("res://scenes/levels/World1/level_1.tscn");
	}

	private void OnInstructionsPressed()
	{
		// TODO: tee ohjeet-scene
		GD.Print("Ohjeet — tulossa!");
	}

	private void OnQuitPressed()
	{
		GetTree().Quit();
	}
}
