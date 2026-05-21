using Godot;

/// <summary>
/// Autoload — latausspinner pysyy scene-vaihtojen yli ja pyörii _Processissa koko latauksen ajan.
/// </summary>
public partial class LoadingOverlay : CanvasLayer
{
	public static LoadingOverlay Instance { get; private set; }

	private Control _root;
	private Control _spinnerPivot;
	private TextureRect _spinner;
	private ProgressBar _progressBar;
	private Label _statusLabel;
	private int _hideFramesRemaining;

	[Export] public float SpinSpeedRadians { get; set; } = 2.8f;

	public override void _Ready()
	{
		Instance = this;
		Layer = 128;
		ProcessMode = ProcessModeEnum.Always;

		BuildUi();
		HideOverlay();
	}

	private void BuildUi()
	{
		_root = new Control { Name = "Root" };
		_root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		_root.MouseFilter = Control.MouseFilterEnum.Stop;
		AddChild(_root);

		var bg = new ColorRect { Color = new Color(0.04f, 0.05f, 0.08f, 0.96f) };
		bg.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		_root.AddChild(bg);

		var center = new CenterContainer();
		center.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		_root.AddChild(center);

		var column = new VBoxContainer
		{
			CustomMinimumSize = new Vector2(320, 0),
			Alignment = BoxContainer.AlignmentMode.Center,
		};
		column.AddThemeConstantOverride("separation", 20);
		center.AddChild(column);

		_statusLabel = new Label
		{
			Text = "Ladataan...",
			HorizontalAlignment = HorizontalAlignment.Center,
		};
		_statusLabel.AddThemeColorOverride("font_color", new Color(0.92f, 0.93f, 0.96f));
		_statusLabel.AddThemeFontSizeOverride("font_size", 22);
		column.AddChild(_statusLabel);

		var spinnerSlot = new Control
		{
			CustomMinimumSize = new Vector2(120, 120),
			ClipContents = true,
			SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter,
		};
		column.AddChild(spinnerSlot);

		// Kiinteä 120×120-paikka; pyöriminen vain pivot-nodeen (ei layout-kaarta).
		_spinnerPivot = new Control
		{
			Position = Vector2.Zero,
			Size = new Vector2(120, 120),
			PivotOffset = new Vector2(60, 60),
		};
		spinnerSlot.AddChild(_spinnerPivot);

		_spinner = new TextureRect
		{
			Position = Vector2.Zero,
			Size = new Vector2(120, 120),
			Texture = GD.Load<Texture2D>("res://assets/textures/loading.png"),
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
		};
		_spinnerPivot.AddChild(_spinner);

		_progressBar = new ProgressBar
		{
			CustomMinimumSize = new Vector2(280, 14),
			MaxValue = 100,
			ShowPercentage = false,
		};
		column.AddChild(_progressBar);
	}

	public override void _Process(double delta)
	{
		if (!_root.Visible)
			return;

		_spinnerPivot.Rotation += (float)(delta * SpinSpeedRadians);

		if (_hideFramesRemaining <= 0)
			return;

		_hideFramesRemaining--;
		if (_hideFramesRemaining == 0)
			HideOverlay();
	}

	public void ShowOverlay()
	{
		_hideFramesRemaining = 0;
		_statusLabel.Text = "Ladataan...";
		_progressBar.Value = 0;
		_root.Visible = true;
	}

	public void SetProgress(float percent)
	{
		_progressBar.Value = Mathf.Clamp(percent, 0, 100);
	}

	public void SetStatus(string text)
	{
		_statusLabel.Text = text;
	}

	public void HideOverlay()
	{
		_hideFramesRemaining = 0;
		_root.Visible = false;
		_spinnerPivot.Rotation = 0f;
		_progressBar.Value = 0;
	}

	/// <summary>Piilota overlay vasta kun uusi kenttä on saanut pyörittää spinneriä muutaman framen ajan.</summary>
	public void ScheduleHideAfterFrames(int frameCount)
	{
		_hideFramesRemaining = Mathf.Max(1, frameCount);
	}
}
