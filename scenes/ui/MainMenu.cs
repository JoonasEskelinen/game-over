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
		GD.Print("MainMenu: Uusi peli — latausruutu → level_1.tscn");
		GameState.Instance.HasJoystick = false;
		GameState.Instance.PendingLoadScenePath = "res://scenes/levels/level_1.tscn";
		var err = GetTree().ChangeSceneToFile("res://scenes/ui/loading_screen.tscn");
		if (err != Error.Ok)
			GD.PrintErr($"MainMenu: loading_screen epäonnistui: {err}");
	}

	private void OnLoadGamePressed()
	{
		// TODO: tallenna myös viimeisin kenttä jotta voidaan ladata oikea kenttä
		GD.Print("MainMenu: Lataa peli — latausruutu → level_1.tscn");
		GameState.Instance.PendingLoadScenePath = "res://scenes/levels/level_1.tscn";
		var err = GetTree().ChangeSceneToFile("res://scenes/ui/loading_screen.tscn");
		if (err != Error.Ok)
			GD.PrintErr($"MainMenu: loading_screen epäonnistui: {err}");
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
