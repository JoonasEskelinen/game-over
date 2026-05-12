using Godot;

public partial class MainMenu : Control
{
	private Button[] _mainMenuButtons;
	private int _focusIndex;
	private float _menuStickArmDelay;
	private bool _stickNavLatched;

	private const float StickNavThreshold = 0.45f;
	private const float StickNeutralRelease = 0.22f;
	private const float MenuStickArmDelaySec = 0.28f;

	public override void _Ready()
	{
		_mainMenuButtons =
		[
			GetNode<Button>("VBoxContainer/NewGameButton"),
			GetNode<Button>("VBoxContainer/InstructionsButton"),
			GetNode<Button>("VBoxContainer/QuitButton"),
		];

		for (int i = 0; i < _mainMenuButtons.Length; i++)
		{
			int idx = i;
			var b = _mainMenuButtons[i];
			b.FocusMode = FocusModeEnum.All;
			b.FocusEntered += () => _focusIndex = idx;
		}

		// Eksplisiittinen ketju: vältä automaattisten naapurien + usean joypadin outoja yhdistelmiä (esim. PS5 + virtuaalilaite).
		var btn0 = _mainMenuButtons[0];
		var btn1 = _mainMenuButtons[1];
		var btn2 = _mainMenuButtons[2];
		var pathSelf0 = btn0.GetPathTo(btn0);
		var pathSelf1 = btn1.GetPathTo(btn1);
		var pathSelf2 = btn2.GetPathTo(btn2);
		btn0.FocusNeighborTop = pathSelf0;
		btn0.FocusNeighborBottom = btn0.GetPathTo(btn1);
		btn0.FocusNeighborLeft = pathSelf0;
		btn0.FocusNeighborRight = pathSelf0;
		btn1.FocusNeighborTop = btn1.GetPathTo(btn0);
		btn1.FocusNeighborBottom = btn1.GetPathTo(btn2);
		btn1.FocusNeighborLeft = pathSelf1;
		btn1.FocusNeighborRight = pathSelf1;
		btn2.FocusNeighborTop = btn2.GetPathTo(btn1);
		btn2.FocusNeighborBottom = pathSelf2;
		btn2.FocusNeighborLeft = pathSelf2;
		btn2.FocusNeighborRight = pathSelf2;

		GetNode<Button>("VBoxContainer/NewGameButton").Pressed += OnNewGamePressed;
		GetNode<Button>("VBoxContainer/InstructionsButton").Pressed += OnInstructionsPressed;
		GetNode<Button>("VBoxContainer/QuitButton").Pressed += OnQuitPressed;

		GetNode<Button>("InstructionsPanel/CenterContainer/Panel/VBox/CloseInstructionsButton").Pressed +=
			OnCloseInstructionsPressed;

		if (GetNodeOrNull<Control>("VBoxContainer/Spacer") is Control spacer)
			spacer.FocusMode = FocusModeEnum.None;
		GetNode<Control>("VBoxContainer").FocusMode = FocusModeEnum.None;

		StripFocusFromNonButtons(GetNode("InstructionsPanel"));
		var closeBtn = GetNode<Button>("InstructionsPanel/CenterContainer/Panel/VBox/CloseInstructionsButton");
		closeBtn.FocusMode = FocusModeEnum.All;

		ApplyInstructionsText();

		StripFocusFromDecorativeMainMenuControls();

		_focusIndex = 0;
		_menuStickArmDelay = MenuStickArmDelaySec;
		_stickNavLatched = false;
		if (HasJoypadConnected())
			Callable.From(GrabInitialMainMenuFocus).CallDeferred();
	}

	private void GrabInitialMainMenuFocus()
	{
		if (!IsInsideTree() || _mainMenuButtons == null || _mainMenuButtons.Length == 0)
			return;
		_mainMenuButtons[0].GrabFocus();
		SyncFocusIndexFromFocusOwner();
		_stickNavLatched = Mathf.Abs(ReadMenuVerticalAxis()) > StickNavThreshold;
	}

	private void StripFocusFromDecorativeMainMenuControls()
	{
		foreach (var path in new[] { "Backround", "DarkOverlay" })
		{
			if (GetNodeOrNull<Control>(path) is not Control c)
				continue;
			c.FocusMode = FocusModeEnum.None;
			c.MouseFilter = Control.MouseFilterEnum.Ignore;
		}
	}

	public override void _Process(double delta)
	{
		if (_menuStickArmDelay > 0f)
			_menuStickArmDelay = Mathf.Max(0f, _menuStickArmDelay - (float)delta);

		if (!HasJoypadConnected())
			return;

		if (GetNode<Control>("InstructionsPanel").Visible)
		{
			if (Input.IsActionJustPressed("jump"))
			{
				CloseInstructions();
				GetViewport().SetInputAsHandled();
			}
			return;
		}

		SyncFocusIndexFromFocusOwner();
		RecoverMainMenuFocusIfLost();

		if (_menuStickArmDelay <= 0f)
			ProcessMainMenuStickNavigation();

		if (Input.IsActionJustPressed("jump"))
			TryActivateFocusedMainMenuButton();
	}

	private void SyncFocusIndexFromFocusOwner()
	{
		if (_mainMenuButtons == null)
			return;
		if (GetViewport().GuiGetFocusOwner() is not Button b)
			return;
		for (int i = 0; i < _mainMenuButtons.Length; i++)
		{
			if (_mainMenuButtons[i] == b)
			{
				_focusIndex = i;
				return;
			}
		}
	}

	/// <summary>
	/// Yksi askel per tatin ulos–sisään -sykli; akseli InputMapista (move_forward / move_back), ei yksittäistä joypad-ID:tä.
	/// </summary>
	private void ProcessMainMenuStickNavigation()
	{
		float stickY = ReadMenuVerticalAxis();
		bool beyond = Mathf.Abs(stickY) > StickNavThreshold;
		if (beyond && !_stickNavLatched)
		{
			if (stickY < 0f)
				MoveMainMenuFocus(-1);
			else
				MoveMainMenuFocus(1);
			_stickNavLatched = true;
		}
		else if (!beyond && Mathf.Abs(stickY) < StickNeutralRelease)
			_stickNavLatched = false;
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (!GetNode<Control>("InstructionsPanel").Visible)
			return;
		if (@event is InputEventKey k && k.Pressed && k.Keycode == Key.Escape)
		{
			CloseInstructions();
			GetViewport().SetInputAsHandled();
		}
	}

	private static bool HasJoypadConnected() => Input.GetConnectedJoypads().Count > 0;

	private void RecoverMainMenuFocusIfLost()
	{
		if (_mainMenuButtons == null || _mainMenuButtons.Length == 0)
			return;
		if (GetViewport().GuiGetFocusOwner() is Button b)
		{
			foreach (var mb in _mainMenuButtons)
			{
				if (mb == b)
					return;
			}
		}

		_mainMenuButtons[Mathf.Clamp(_focusIndex, 0, _mainMenuButtons.Length - 1)].GrabFocus();
		SyncFocusIndexFromFocusOwner();
	}

	/// <summary>
	/// Käytä InputMapin move_forward / move_back (device -1, deadzone) — ei vain "ensimmäistä" joypad-ID:tä,
	/// joka Windowsilla voi olla virtuaaliohjain ja vääristää päävalikon tattia.
	/// </summary>
	private static float ReadMenuVerticalAxis()
		=> Input.GetAxis("move_forward", "move_back");

	private void MoveMainMenuFocus(int delta)
	{
		if (_mainMenuButtons == null || _mainMenuButtons.Length == 0)
			return;
		_focusIndex = Mathf.Clamp(_focusIndex + delta, 0, _mainMenuButtons.Length - 1);
		_mainMenuButtons[_focusIndex].GrabFocus();
		SyncFocusIndexFromFocusOwner();
	}

	private void TryActivateFocusedMainMenuButton()
	{
		RecoverMainMenuFocusIfLost();
		SyncFocusIndexFromFocusOwner();
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
		if (node is Control c)
			c.FocusMode = FocusModeEnum.None;
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
		GameState.Instance.PendingLoadScenePath = "res://scenes/levels/level_1.tscn";
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
		_stickNavLatched = true;
		GetNode<Button>("InstructionsPanel/CenterContainer/Panel/VBox/CloseInstructionsButton").GrabFocus();
	}

	private void CloseInstructions()
	{
		GetNode<Control>("InstructionsPanel").Visible = false;
		_menuStickArmDelay = MenuStickArmDelaySec;
		_stickNavLatched = true;
		if (HasJoypadConnected() && _mainMenuButtons != null && _mainMenuButtons.Length > 0)
		{
			_mainMenuButtons[Mathf.Clamp(_focusIndex, 0, _mainMenuButtons.Length - 1)].GrabFocus();
			SyncFocusIndexFromFocusOwner();
		}
	}

	private void OnQuitPressed()
	{
		GetTree().Quit();
	}

	private const string InstructionsBbcode =
		"[b]Pelin idea[/b]\n" +
		"2.5D-tasohyppely ja lähitaistelu: ohjaa Game Over -hahmoa, etene kentässä, torju ja hyökkää, voita bossit.\n\n" +
		"[b]Tallennus[/b]\n" +
		"Projektissa ei ole pelitilan tallennusta — ei tallennetta kentästä tai edistymisestä. [b]Uusi peli[/b] aloittaa kertomuksen alusta.\n\n" +

		"[b]Ohjain (DualSense / vastaava)[/b]\n" +
		"• Päävalikko: [b]vasen tatti[/b] valitsee rivin, [b]Cross (X)[/b] vahvistaa ([i]jump[/i]).\n" +
		"• Liike: [b]vasen tatti[/b]\n" +
		"• Kamera: [b]oikea tatti[/b]\n" +
		"• Hyppy: [b]Cross (Ristinäppäin)[/b]\n" +
		"• Kyykky: toiminto [i]crouch[/i] (ks. Input Map)\n" +
		"• Istu: toiminto [i]sit[/i]\n" +
		"• Kilpi: [b]L2[/b] ([i]block[/i])\n" +
		"• Miekan isku: [b]R2[/b] ([i]attack[/i])\n" +
		"• Toinen lyönti: [b]R1[/b] ([i]attack_r1[/i])\n" +
		"• Tarttuminen: näppäin [b]E[/b] / ohjain ([i]grab[/i])\n" +
		"• Ase-/tilanvaihto: ohjain ([i]toggle_weapon[/i])\n\n" +

		"[b]Näppäimistö[/b]\n" +
		"• Liike: [b]W A S D[/b]\n" +
		"• Hyökkäys: [b]F[/b]\n" +
		"• Kyykky: [b]C[/b]\n" +
		"• Istu: [b]I[/b]\n" +
		"• Tarttuminen: [b]E[/b]\n" +
		"• Huom: [i]jump[/i] on määritelty ohjaimelle — lisää näppäin Project Settings → Input Map -kohtaan [i]jump[/i], jos haluat hypätä näppäimistöllä.\n\n" +

		"[b]Taistelu[/b]\n" +
		"Torju kilvellä, hyökkää miekkalla, käytä tarttumista ja kentän objekteja tilanteen mukaan.\n\n" +

		"[b]Vinkki[/b]\n" +
		"Tarkat näppäimet löytyvät Godotin [i]Project → Project Settings → Input Map[/i]. [b]Esc[/b] sulkee tämän ohjeikkunan.";

}
