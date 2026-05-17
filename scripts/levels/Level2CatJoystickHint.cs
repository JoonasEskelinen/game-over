using Godot;

/// <summary>
/// Level 2 alku: pulssiva HUD-vinkki — istuminen (ohjaimen ympyrä / näppäin I) avaa joystickin ja kissan.
/// Piiloutuu automaattisesti kun erikoisase aktivoituu, pelaaja etenee tai aikaraja täyttyy.
/// </summary>
public partial class Level2CatJoystickHint : CanvasLayer
{
	[Export] public float HideWhenPlayerXBeyond = -94f;
	/// <summary>0 = ei aikarajaa (vain eteneminen / erikoisase piilottaa).</summary>
	[Export] public float MaxShowSeconds = 48f;
	[Export] public float PulseSpeedRadPerSec = 2.85f;

	private Control _root;
	private PanelContainer _panel;
	private RichTextLabel _hint;
	private float _elapsed;
	private float _textRefresh;
	private bool _dismissed;
	private PlayerController _player;

	public override void _Ready()
	{
		Layer = 18;

		_root = new Control { Name = "HintRoot" };
		_root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		_root.MouseFilter = Control.MouseFilterEnum.Ignore;
		AddChild(_root);

		var margin = new MarginContainer();
		margin.SetAnchorsPreset(Control.LayoutPreset.BottomWide);
		margin.OffsetLeft = 24f;
		margin.OffsetRight = -24f;
		margin.OffsetTop = -168f;
		margin.OffsetBottom = -20f;
		_root.AddChild(margin);

		_panel = new PanelContainer();
		margin.AddChild(_panel);

		var sb = new StyleBoxFlat
		{
			BgColor = new Color(0.04f, 0.07f, 0.055f, 0.92f),
			BorderColor = new Color(0.22f, 0.72f, 0.38f, 0.55f),
			BorderWidthLeft = 4,
			BorderWidthTop = 1,
			BorderWidthRight = 1,
			BorderWidthBottom = 1,
			CornerRadiusTopLeft = 12,
			CornerRadiusTopRight = 12,
			CornerRadiusBottomRight = 12,
			CornerRadiusBottomLeft = 12,
			ContentMarginLeft = 18,
			ContentMarginTop = 14,
			ContentMarginRight = 18,
			ContentMarginBottom = 14,
			ShadowColor = new Color(0f, 0f, 0f, 0.42f),
			ShadowSize = 8,
			ShadowOffset = new Vector2(0, 4),
		};
		_panel.AddThemeStyleboxOverride("panel", sb);

		_hint = new RichTextLabel
		{
			BbcodeEnabled = true,
			ScrollActive = false,
			FitContent = true,
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			HorizontalAlignment = HorizontalAlignment.Center,
		};
		_hint.AddThemeConstantOverride("line_separation", 4);
		_panel.AddChild(_hint);
		_hint.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;

		UpdateHintText();
		CallDeferred(nameof(DeferredPivot));
	}

	private void DeferredPivot()
	{
		if (_panel == null || !IsInstanceValid(_panel)) return;
		_panel.PivotOffset = _panel.Size * 0.5f;
	}

	private void UpdateHintText()
	{
		if (_hint == null) return;
		bool pad = Input.GetConnectedJoypads().Count > 0;
		_hint.Text = pad ? BuildPadBbcode() : BuildKeyboardBbcode();
	}

	private static string BuildPadBbcode()
	{
		return "[center]" +
			"[font_size=20][color=#7ef0a0][b]Aktivoi joystick[/b][/color][/font_size]\n" +
			"[font_size=15][color=#c8e6d4]ohjaimen ympyrä painikkeesta.[/color][/font_size]\n" +
			"[font_size=13][color=#7fa894]Käytä kissaa apuna hämähäkkejä vastaan.[b]R2[/b][/color][/font_size]" +
			"[/center]";
	}

	private static string BuildKeyboardBbcode()
	{
		return "[center]" +
			"[font_size=20][color=#7ef0a0][b]Aktivoi joystick[/b][/color][/font_size]\n" +
			"[font_size=15][color=#c8e6d4]painamalla [b]I[/b] (istu).[/color][/font_size]\n" +
			"[font_size=13][color=#7fa894]Kissa · [b]WASD[/b] · isku [b]F[/b][/color][/font_size]" +
			"[/center]";
	}

	public override void _Process(double delta)
	{
		if (_dismissed) return;

		float dt = (float)delta;
		_elapsed += dt;
		_textRefresh += dt;
		if (_textRefresh >= 0.6f)
		{
			_textRefresh = 0f;
			UpdateHintText();
		}

		if (_player == null || !IsInstanceValid(_player) || !_player.IsInsideTree())
			_player = GetTree().GetFirstNodeInGroup("player") as PlayerController;

		if (_player != null)
		{
			if (_player.IsSpecialWeaponJoystickContextActive())
			{
				Dismiss();
				return;
			}
			if (_player.GlobalPosition.X > HideWhenPlayerXBeyond)
			{
				Dismiss();
				return;
			}
		}

		if (MaxShowSeconds > 0.01f && _elapsed >= MaxShowSeconds)
			Dismiss();

		float w = 0.5f + 0.5f * Mathf.Sin(_elapsed * PulseSpeedRadPerSec);
		float a = Mathf.Lerp(0.72f, 1f, w);
		if (_panel != null)
			_panel.Modulate = new Color(1f, 1f, 1f, a);

		float s = Mathf.Lerp(0.988f, 1.015f, w);
		if (_panel != null && _panel.PivotOffset.LengthSquared() > 1f)
			_panel.Scale = new Vector2(s, s);
	}

	private void Dismiss()
	{
		if (_dismissed) return;
		_dismissed = true;
		SetProcess(false);
		if (_root == null || !IsInstanceValid(_root))
		{
			QueueFree();
			return;
		}
		var t = CreateTween();
		t.TweenProperty(_root, "modulate:a", 0f, 0.38f).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.In);
		t.TweenCallback(Callable.From(QueueFree));
	}
}
