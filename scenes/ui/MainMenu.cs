using Godot;

public partial class MainMenu : Control
{
	private Button[] _mainMenuButtons;
	private Button _closeInstructionsButton;
	private ScrollContainer _instructionsScroll;
	private int _focusIndex;
	private float _menuStickArmDelay;
	private bool _stickNavLatched;
	private bool _instructionsScrollLatched;

	private const float StickNavThreshold = 0.45f;
	private const float StickNeutralRelease = 0.15f;
	private const float MenuStickArmDelaySec = 0.28f;
	private const float InstructionsScrollSpeed = 480f;
	private const float InstructionsScrollStep = 80f;

	public override void _Ready()
	{
		_mainMenuButtons =
		[
			GetNode<Button>("CenterContainer/VBoxContainer/NewGameButton"),
			GetNode<Button>("CenterContainer/VBoxContainer/InstructionsButton"),
			GetNode<Button>("CenterContainer/VBoxContainer/QuitButton"),
		];

		_closeInstructionsButton =
			GetNode<Button>("InstructionsPanel/CenterContainer/Panel/VBox/CloseInstructionsButton");
		_instructionsScroll = GetNode<ScrollContainer>("InstructionsPanel/CenterContainer/Panel/VBox/Scroll");

		for (int i = 0; i < _mainMenuButtons.Length; i++)
		{
			var button = _mainMenuButtons[i];
			button.FocusMode = FocusModeEnum.All;
		}

		var newGameButton = _mainMenuButtons[0];
		var instructionsButton = _mainMenuButtons[1];
		var quitButton = _mainMenuButtons[2];
		var pathSelf0 = newGameButton.GetPathTo(newGameButton);
		var pathSelf1 = instructionsButton.GetPathTo(instructionsButton);
		var pathSelf2 = quitButton.GetPathTo(quitButton);
		newGameButton.FocusNeighborTop = pathSelf0;
		newGameButton.FocusNeighborBottom = newGameButton.GetPathTo(instructionsButton);
		newGameButton.FocusNeighborLeft = pathSelf0;
		newGameButton.FocusNeighborRight = pathSelf0;
		instructionsButton.FocusNeighborTop = instructionsButton.GetPathTo(newGameButton);
		instructionsButton.FocusNeighborBottom = instructionsButton.GetPathTo(quitButton);
		instructionsButton.FocusNeighborLeft = pathSelf1;
		instructionsButton.FocusNeighborRight = pathSelf1;
		quitButton.FocusNeighborTop = quitButton.GetPathTo(instructionsButton);
		quitButton.FocusNeighborBottom = pathSelf2;
		quitButton.FocusNeighborLeft = pathSelf2;
		quitButton.FocusNeighborRight = pathSelf2;

		newGameButton.Pressed += OnNewGamePressed;
		instructionsButton.Pressed += OnInstructionsPressed;
		quitButton.Pressed += OnQuitPressed;
		_closeInstructionsButton.Pressed += OnCloseInstructionsPressed;

		GetNode<Control>("CenterContainer/VBoxContainer").FocusMode = FocusModeEnum.None;
		StripFocusFromNonButtons(GetNode("InstructionsPanel"));
		_closeInstructionsButton.FocusMode = FocusModeEnum.None;

		ApplyInstructionsText();
		StripFocusFromDecorativeMainMenuControls();

		_focusIndex = 0;
		_menuStickArmDelay = MenuStickArmDelaySec;
		_stickNavLatched = false;
		if (HasJoypadConnected())
			Callable.From(GrabInitialMainMenuFocus).CallDeferred();
	}

	public override void _Process(double delta)
	{
		if (_menuStickArmDelay > 0f)
			_menuStickArmDelay = Mathf.Max(0f, _menuStickArmDelay - (float)delta);

		if (GetNode<Control>("InstructionsPanel").Visible)
		{
			ProcessInstructionsScroll(delta);

			if (HasJoypadConnected() && Input.IsActionJustPressed("jump"))
			{
				CloseInstructions();
				GetViewport().SetInputAsHandled();
			}

			return;
		}

		if (!HasJoypadConnected())
			return;

		if (_menuStickArmDelay <= 0f)
			ProcessMainMenuStickNavigation();

		EnforceMainMenuFocusForJoypad();

		if (Input.IsActionJustPressed("jump"))
			TryActivateFocusedMainMenuButton();
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (GetNode<Control>("InstructionsPanel").Visible)
		{
			if (@event is InputEventKey keyEvent && keyEvent.Pressed && keyEvent.Keycode == Key.Escape)
			{
				CloseInstructions();
				GetViewport().SetInputAsHandled();
			}
			else if (@event.IsAction("ui_up") || @event.IsAction("ui_down")
				|| @event.IsAction("ui_left") || @event.IsAction("ui_right"))
			{
				GetViewport().SetInputAsHandled();
			}

			return;
		}

		if (!HasJoypadConnected())
			return;

		if (@event.IsAction("ui_up") || @event.IsAction("ui_down")
			|| @event.IsAction("ui_left") || @event.IsAction("ui_right"))
		{
			GetViewport().SetInputAsHandled();
		}
	}

	private void GrabInitialMainMenuFocus()
	{
		if (!IsInsideTree() || _mainMenuButtons == null || _mainMenuButtons.Length == 0)
			return;

		_focusIndex = 0;
		_mainMenuButtons[0].GrabFocus();
		_stickNavLatched = Mathf.Abs(ReadMenuVerticalAxis()) > StickNavThreshold;
	}

	private void StripFocusFromDecorativeMainMenuControls()
	{
		foreach (var path in new[] { "Background", "DarkOverlay" })
		{
			if (GetNodeOrNull<Control>(path) is not Control control)
				continue;

			control.FocusMode = FocusModeEnum.None;
			control.MouseFilter = Control.MouseFilterEnum.Ignore;
		}
	}

	private void ProcessInstructionsScroll(double delta)
	{
		if (_instructionsScroll == null)
			return;

		var scrollBar = _instructionsScroll.GetVScrollBar();
		if (scrollBar.MaxValue <= scrollBar.MinValue + 0.5f)
			return;

		float stickY = ReadMenuVerticalAxis();
		bool stickNeutral = Mathf.Abs(stickY) < StickNeutralRelease;
		bool dpadNeutral = !Input.IsActionPressed("ui_down") && !Input.IsActionPressed("ui_up");

		if (!stickNeutral)
		{
			scrollBar.Value = Mathf.Clamp(
				scrollBar.Value + stickY * InstructionsScrollSpeed * (float)delta,
				scrollBar.MinValue,
				scrollBar.MaxValue);
			_instructionsScrollLatched = true;
			return;
		}

		if (stickNeutral && dpadNeutral)
			_instructionsScrollLatched = false;

		if (_instructionsScrollLatched)
			return;

		if (Input.IsActionJustPressed("ui_down"))
		{
			scrollBar.Value = Mathf.Min(scrollBar.MaxValue, scrollBar.Value + InstructionsScrollStep);
			_instructionsScrollLatched = true;
		}
		else if (Input.IsActionJustPressed("ui_up"))
		{
			scrollBar.Value = Mathf.Max(scrollBar.MinValue, scrollBar.Value - InstructionsScrollStep);
			_instructionsScrollLatched = true;
		}
	}

	private void ResetInstructionsScroll()
	{
		if (_instructionsScroll == null)
			return;

		_instructionsScroll.ScrollVertical = 0;
	}

	private void EnforceMainMenuFocusForJoypad()
	{
		if (_mainMenuButtons == null || _mainMenuButtons.Length == 0)
			return;

		var expected = _mainMenuButtons[_focusIndex];
		if (GetViewport().GuiGetFocusOwner() != expected)
			expected.GrabFocus();
	}

	private void ProcessMainMenuStickNavigation()
	{
		float stickY = ReadMenuVerticalAxis();
		bool stickNeutral = Mathf.Abs(stickY) < StickNeutralRelease;
		bool dpadNeutral = !Input.IsActionPressed("ui_down") && !Input.IsActionPressed("ui_up");

		if (stickNeutral && dpadNeutral)
		{
			_stickNavLatched = false;
			return;
		}

		if (_stickNavLatched)
			return;

		if (Input.IsActionJustPressed("ui_down") || stickY > StickNavThreshold)
		{
			MoveMainMenuFocus(1);
			_stickNavLatched = true;
		}
		else if (Input.IsActionJustPressed("ui_up") || stickY < -StickNavThreshold)
		{
			MoveMainMenuFocus(-1);
			_stickNavLatched = true;
		}
	}

	private static bool HasJoypadConnected() => Input.GetConnectedJoypads().Count > 0;

	private static float ReadMenuVerticalAxis()
		=> Input.GetAxis("move_forward", "move_back");

	private void MoveMainMenuFocus(int delta)
	{
		if (_mainMenuButtons == null || _mainMenuButtons.Length == 0)
			return;

		_focusIndex = Mathf.Clamp(_focusIndex + delta, 0, _mainMenuButtons.Length - 1);
		_mainMenuButtons[_focusIndex].GrabFocus();
	}

	private void TryActivateFocusedMainMenuButton()
	{
		EnforceMainMenuFocusForJoypad();

		switch (_focusIndex)
		{
			case 0:
				OnNewGamePressed();
				break;
			case 1:
				OnInstructionsPressed();
				break;
			case 2:
				OnQuitPressed();
				break;
		}
	}

	private static void StripFocusFromNonButtons(Node node)
	{
		foreach (Node child in node.GetChildren())
			StripFocusFromNonButtons(child);

		if (node is BaseButton)
			return;

		if (node is Control control)
			control.FocusMode = FocusModeEnum.None;
	}

	private void ApplyInstructionsText()
	{
		var rt = GetNode<RichTextLabel>("InstructionsPanel/CenterContainer/Panel/VBox/Scroll/RichTextLabel");
		rt.Text = InstructionsBbcode;
	}

	private void OnNewGamePressed()
	{
		GD.Print("MainMenu: Uusi peli — latausruutu → level_1.tscn");
		GameState.Instance.HasJoystick = false;
		GameState.Instance.PersistHasJoystickToSave();
		GameState.Instance.BeginSceneLoad("res://scenes/levels/level_1.tscn");
		var err = GetTree().ChangeSceneToFile("res://scenes/ui/loading_screen.tscn");
		if (err != Error.Ok)
			GD.PrintErr($"MainMenu: loading_screen epäonnistui: {err}");
	}

	private void OnInstructionsPressed()
	{
		OpenInstructions();
	}

	private void OnCloseInstructionsPressed()
	{
		CloseInstructions();
	}

	private void OpenInstructions()
	{
		GetNode<Control>("InstructionsPanel").Visible = true;
		_closeInstructionsButton.FocusMode = FocusModeEnum.All;
		_stickNavLatched = true;
		_instructionsScrollLatched = true;
		ResetInstructionsScroll();
		Callable.From(ResetInstructionsScroll).CallDeferred();
		_closeInstructionsButton.GrabFocus();
	}

	private void CloseInstructions()
	{
		GetNode<Control>("InstructionsPanel").Visible = false;
		_closeInstructionsButton.FocusMode = FocusModeEnum.None;
		_menuStickArmDelay = MenuStickArmDelaySec;
		_stickNavLatched = true;

		if (HasJoypadConnected() && _mainMenuButtons != null && _mainMenuButtons.Length > 0)
		{
			_mainMenuButtons[Mathf.Clamp(_focusIndex, 0, _mainMenuButtons.Length - 1)].GrabFocus();
		}
	}

	private void OnQuitPressed()
	{
		GetTree().Quit();
	}

	private const string InstructionsBbcode =
		"[center][font_size=24][b]GAME OVER[/b][/font_size][/center]\n" +
		"[center]Toiminta- ja seikkailupeli · Pelihuone GameOver[/center]\n\n" +

		"[b]Tavoite[/b]\n" +
		"Selviydy kolmen maailman läpi, voita päävastustajat ja löydä tie eteenpäin. Tutki kenttiä — ratkaisut ja erikoisaseet avautuvat pelatessa. Elämät ja edistyminen tallentuvat automaattisesti.\n\n" +

		"[b]Ohjain — perustoiminnot[/b]\n" +
		"[table=2]\n" +
		"[cell][b]Vasen tatti[/b][/cell][cell]Liiku[/cell]\n" +
		"[cell][b]Kolmio △[/b][/cell][cell]Miekka ja kilpi päälle / pois[/cell]\n" +
		"[cell][b]L2[/b][/cell][cell]Kilpi (pidä pohjassa)[/cell]\n" +
		"[cell][b]R2[/b][/cell][cell]Kevyt miekan isku[/cell]\n" +
		"[cell][b]R1[/b][/cell][cell]Voimakas miekan isku[/cell]\n" +
		"[cell][b]Neliö □[/b][/cell][cell]Tartu ja työnnä esineitä[/cell]\n" +
		"[cell][b]Ympyrä ○[/b][/cell][cell]Istu / erikoisase-tila[/cell]\n" +
		"[/table]\n\n" +

		"[b]Taistelu[/b]\n" +
		"• [b]Miekka ja kilpi[/b] — aktivoi kolmiolla △. R2 on nopea isku, R1 voimakas (lyhyempi lataus).\n" +
		"• [b]Kilpi[/b] — pidä L2 pohjassa ja suuntaudu uhkauksen puoleen. Torjuttu isku ei vie terveyttä.\n" +
		"• [b]Terveys[/b] — osumat kuluttavat energiapalkkia. Energia loppuu → menetät yhden elämän. Kaikki elämät loppuvat → peli päättyy.\n" +
		"• [b]Ympäristö[/b] — tartu ja työnnä esineitä (□). Jotkin kohteet voivat avata uusia mahdollisuuksia.\n\n" +

		"[b]Vinkkejä[/b]\n" +
		"• R1 on voimakkaampi kuin R2, mutta sitä ei voi käyttää yhtä usein peräkkäin.\n" +
		"• Istu- ja erikoisasetilat (○) muuttavat, mitä painikkeet tekevät — kokeile turvallisissa paikoissa.\n" +
		"• HUD näyttää terveyden, elämät ja raskaan iskun latauksen.\n" +
		"• Päävastustajilla on omat rytminsä — tarkkaile ennen kuin hyökkäät.\n\n" +

		"[b]Tallennus ja uusi peli[/b]\n" +
		"Edistyminen ja elämät tallentuvat automaattisesti. [b]Uusi peli[/b] aloittaa kampanjan alusta.\n\n" +

		"[font_size=12][i]Tämä ikkuna: vasen tatti ylös/alas scrollaa tekstiä. Esc sulkee.[/i][/font_size]";

}
