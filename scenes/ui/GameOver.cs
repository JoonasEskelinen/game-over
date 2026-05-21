using Godot;

public partial class GameOver : Control
{
	private ColorRect _overlay;
	private Label _gameOverLabel;
	private Label _subtitleLabel;
	private Button[] _menuButtons;
	private int _focusIndex;
	private float _menuStickArmDelay;
	private bool _stickNavLatched;
	private bool _buttonsVisible;

	private const float StickNavThreshold = 0.45f;
	private const float StickNeutralRelease = 0.22f;
	private const float MenuStickArmDelaySec = 0.28f;

	public override void _Ready()
	{
		_overlay = GetNode<ColorRect>("Overlay");
		_gameOverLabel = GetNode<Label>("CenterContainer/VBoxContainer/GameOverLabel");
		_subtitleLabel = GetNode<Label>("CenterContainer/VBoxContainer/SubtitleLabel");

		_menuButtons =
		[
			GetNode<Button>("CenterContainer/VBoxContainer/NewGameButton"),
			GetNode<Button>("CenterContainer/VBoxContainer/MainMenuButton"),
		];

		for (int i = 0; i < _menuButtons.Length; i++)
		{
			int idx = i;
			var button = _menuButtons[i];
			button.FocusMode = FocusModeEnum.All;
			button.FocusEntered += () => _focusIndex = idx;
		}

		var newGameButton = _menuButtons[0];
		var mainMenuButton = _menuButtons[1];
		var pathSelf0 = newGameButton.GetPathTo(newGameButton);
		var pathSelf1 = mainMenuButton.GetPathTo(mainMenuButton);
		newGameButton.FocusNeighborTop = pathSelf0;
		newGameButton.FocusNeighborBottom = newGameButton.GetPathTo(mainMenuButton);
		newGameButton.FocusNeighborLeft = pathSelf0;
		newGameButton.FocusNeighborRight = pathSelf0;
		mainMenuButton.FocusNeighborTop = mainMenuButton.GetPathTo(newGameButton);
		mainMenuButton.FocusNeighborBottom = pathSelf1;
		mainMenuButton.FocusNeighborLeft = pathSelf1;
		mainMenuButton.FocusNeighborRight = pathSelf1;

		newGameButton.Pressed += OnNewGamePressed;
		mainMenuButton.Pressed += OnMainMenuPressed;

		GetNode<Control>("CenterContainer/VBoxContainer").FocusMode = FocusModeEnum.None;
		StripFocusFromDecorativeControls();

		_gameOverLabel.Modulate = new Color(1, 0, 0, 0);
		_subtitleLabel.Modulate = new Color(1, 1, 1, 0);
		newGameButton.Modulate = new Color(1, 1, 1, 0);
		mainMenuButton.Modulate = new Color(1, 1, 1, 0);

		_focusIndex = 0;
		_menuStickArmDelay = MenuStickArmDelaySec;
		_stickNavLatched = false;
		_buttonsVisible = false;

		PlayGameOverAnimation();
	}

	public override void _Process(double delta)
	{
		if (!_buttonsVisible)
			return;

		if (_menuStickArmDelay > 0f)
			_menuStickArmDelay = Mathf.Max(0f, _menuStickArmDelay - (float)delta);

		if (!HasJoypadConnected())
			return;

		SyncFocusIndexFromFocusOwner();
		RecoverMenuFocusIfLost();

		if (_menuStickArmDelay <= 0f)
			ProcessMenuStickNavigation();

		if (Input.IsActionJustPressed("jump"))
			TryActivateFocusedButton();
	}

	private async void PlayGameOverAnimation()
	{
		var tween = CreateTween();
		tween.TweenProperty(_overlay, "color:a", 0.75f, 1.0f)
			.SetTrans(Tween.TransitionType.Quad);
		await ToSignal(tween, "finished");

		var tween2 = CreateTween();
		tween2.TweenProperty(_gameOverLabel, "modulate:a", 1.0f, 0.5f);
		tween2.Parallel().TweenProperty(_gameOverLabel, "scale", new Vector2(1f, 1f), 0.5f)
			.SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
		_gameOverLabel.Scale = new Vector2(0.5f, 0.5f);
		await ToSignal(tween2, "finished");

		for (int i = 0; i < 3; i++)
		{
			var blinkOut = CreateTween();
			blinkOut.TweenProperty(_gameOverLabel, "modulate:a", 0f, 0.15f);
			await ToSignal(blinkOut, "finished");

			var blinkIn = CreateTween();
			blinkIn.TweenProperty(_gameOverLabel, "modulate:a", 1f, 0.15f);
			await ToSignal(blinkIn, "finished");
		}

		var tween3 = CreateTween();
		tween3.TweenProperty(_subtitleLabel, "modulate:a", 1.0f, 0.4f);
		await ToSignal(tween3, "finished");

		var tween4 = CreateTween();
		tween4.TweenProperty(_menuButtons[0], "modulate:a", 1.0f, 0.3f);
		tween4.TweenProperty(_menuButtons[1], "modulate:a", 1.0f, 0.3f);
		await ToSignal(tween4, "finished");

		_buttonsVisible = true;
		_menuStickArmDelay = MenuStickArmDelaySec;
		if (HasJoypadConnected())
			Callable.From(GrabInitialMenuFocus).CallDeferred();
	}

	private void GrabInitialMenuFocus()
	{
		if (!IsInsideTree() || _menuButtons == null || _menuButtons.Length == 0)
			return;

		_menuButtons[0].GrabFocus();
		SyncFocusIndexFromFocusOwner();
		_stickNavLatched = Mathf.Abs(ReadMenuVerticalAxis()) > StickNavThreshold;
	}

	private void StripFocusFromDecorativeControls()
	{
		foreach (var path in new[] { "Background", "Overlay" })
		{
			if (GetNodeOrNull<Control>(path) is not Control control)
				continue;

			control.FocusMode = FocusModeEnum.None;
			control.MouseFilter = Control.MouseFilterEnum.Ignore;
		}
	}

	private void SyncFocusIndexFromFocusOwner()
	{
		if (_menuButtons == null)
			return;

		if (GetViewport().GuiGetFocusOwner() is not Button focusedButton)
			return;

		for (int i = 0; i < _menuButtons.Length; i++)
		{
			if (_menuButtons[i] == focusedButton)
			{
				_focusIndex = i;
				return;
			}
		}
	}

	private void ProcessMenuStickNavigation()
	{
		float stickY = ReadMenuVerticalAxis();
		bool beyond = Mathf.Abs(stickY) > StickNavThreshold;
		if (beyond && !_stickNavLatched)
		{
			if (stickY < 0f)
				MoveMenuFocus(-1);
			else
				MoveMenuFocus(1);

			_stickNavLatched = true;
		}
		else if (!beyond && Mathf.Abs(stickY) < StickNeutralRelease)
		{
			_stickNavLatched = false;
		}
	}

	private void RecoverMenuFocusIfLost()
	{
		if (_menuButtons == null || _menuButtons.Length == 0)
			return;

		if (GetViewport().GuiGetFocusOwner() is Button focusedButton)
		{
			foreach (var button in _menuButtons)
			{
				if (button == focusedButton)
					return;
			}
		}

		_menuButtons[Mathf.Clamp(_focusIndex, 0, _menuButtons.Length - 1)].GrabFocus();
		SyncFocusIndexFromFocusOwner();
	}

	private static bool HasJoypadConnected() => Input.GetConnectedJoypads().Count > 0;

	private static float ReadMenuVerticalAxis()
		=> Input.GetAxis("move_forward", "move_back");

	private void MoveMenuFocus(int delta)
	{
		if (_menuButtons == null || _menuButtons.Length == 0)
			return;

		_focusIndex = Mathf.Clamp(_focusIndex + delta, 0, _menuButtons.Length - 1);
		_menuButtons[_focusIndex].GrabFocus();
		SyncFocusIndexFromFocusOwner();
	}

	private void TryActivateFocusedButton()
	{
		RecoverMenuFocusIfLost();
		SyncFocusIndexFromFocusOwner();

		switch (_focusIndex)
		{
			case 0:
				OnNewGamePressed();
				break;
			case 1:
				OnMainMenuPressed();
				break;
		}
	}

	private void OnNewGamePressed()
	{
		GameState.Instance.HasJoystick = false;
		GameState.Instance.PersistHasJoystickToSave();
		GameState.Instance.BeginSceneLoad("res://scenes/levels/level_1.tscn");
		var err = GetTree().ChangeSceneToFile("res://scenes/ui/loading_screen.tscn");
		if (err != Error.Ok)
			GD.PrintErr($"GameOver: loading_screen epäonnistui: {err}");
	}

	private void OnMainMenuPressed()
	{
		GetTree().ChangeSceneToFile("res://scenes/ui/main_menu.tscn");
	}
}
