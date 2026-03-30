using Godot;

public partial class GameOver : Control
{
	private ColorRect _overlay;
	private Label _gameOverLabel;
	private Label _subtitleLabel;
	private Control _buttons;
	private AnimationPlayer _animationPlayer;

	public override void _Ready()
	{
		_overlay       = GetNode<ColorRect>("Overlay");
		_gameOverLabel = GetNode<Label>("CenterContainer/VBoxContainer/GameOverLabel");
		_subtitleLabel = GetNode<Label>("CenterContainer/VBoxContainer/SubtitleLabel");
		_buttons       = GetNode<Control>("CenterContainer/VBoxContainer/RetryButton");

		// Piilotetaan aluksi
		_gameOverLabel.Modulate = new Color(1, 0, 0, 0);
		_subtitleLabel.Modulate = new Color(1, 1, 1, 0);
		GetNode<Button>("CenterContainer/VBoxContainer/RetryButton").Modulate     = new Color(1, 1, 1, 0);
		GetNode<Button>("CenterContainer/VBoxContainer/MainMenuButton").Modulate  = new Color(1, 1, 1, 0);

		// Kytketään napit
		GetNode<Button>("CenterContainer/VBoxContainer/RetryButton").Pressed     += OnRetryPressed;
		GetNode<Button>("CenterContainer/VBoxContainer/MainMenuButton").Pressed  += OnMainMenuPressed;

		// Käynnistetään animaatio
		PlayGameOverAnimation();
	}

	private async void PlayGameOverAnimation()
	{
		// 1. Tummennetaan tausta 1 sekunnissa
		var tween = CreateTween();
		tween.TweenProperty(_overlay, "color:a", 0.75f, 1.0f)
			 .SetTrans(Tween.TransitionType.Quad);
		await ToSignal(tween, "finished");

		// 2. GAME OVER teksti ilmestyy + kasvaa
		var tween2 = CreateTween();
		tween2.TweenProperty(_gameOverLabel, "modulate:a", 1.0f, 0.5f);
		tween2.Parallel().TweenProperty(_gameOverLabel, "scale", new Vector2(1f, 1f), 0.5f)
			  .SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
		_gameOverLabel.Scale = new Vector2(0.5f, 0.5f); // Alkukoko
		await ToSignal(tween2, "finished");

		// 3. Vilkkuminen 3 kertaa (arcade-tyyli!)
		for (int i = 0; i < 3; i++)
		{
			var blinkOut = CreateTween();
			blinkOut.TweenProperty(_gameOverLabel, "modulate:a", 0f, 0.15f);
			await ToSignal(blinkOut, "finished");

			var blinkIn = CreateTween();
			blinkIn.TweenProperty(_gameOverLabel, "modulate:a", 1f, 0.15f);
			await ToSignal(blinkIn, "finished");
		}

		// 4. Subtitle ja napit ilmestyvät
		var tween3 = CreateTween();
		tween3.TweenProperty(_subtitleLabel, "modulate:a", 1.0f, 0.4f);
		await ToSignal(tween3, "finished");

		var tween4 = CreateTween();
		tween4.TweenProperty(
			GetNode<Button>("CenterContainer/VBoxContainer/RetryButton"), "modulate:a", 1.0f, 0.3f);
		tween4.TweenProperty(
			GetNode<Button>("CenterContainer/VBoxContainer/MainMenuButton"), "modulate:a", 1.0f, 0.3f);
	}

	private void OnRetryPressed()
	{
		GetTree().ChangeSceneToFile("res://scenes/levels/level_1.tscn");
	}

	private void OnMainMenuPressed()
	{
		// TODO: vaihda päävalikon polkuun kun se on valmis
		GetTree().ChangeSceneToFile("res://scenes/ui/main_menu.tscn");
	}
}
