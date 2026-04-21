using System;
using Godot;

/// <summary>
/// JoystickLever on kenttään sijoitettu vipu joka toimii kahdessa vaiheessa:
///
/// VAIHE 1 — Vipu (ennen bossia):
///   Pelaaja astuu Area3D:lle ja painaa neliötä (grab).
///   <see cref="StickPivot"/> kallistuu (GLB-mallin tulee olla sen lapsena), EnemySpawner aktivoituu.
///   Lyhyt hidastus + kamerazoom + tärinä; "Push Start" -teksti vilkkuu.
///
/// VAIHE 2 — Palkinto (bossin kuoltua):
///   Skripti pollaa bossia — <see cref="NotifyBossDefeated"/>.
///   Kellunta jatkuu; lyhyt nousu / skaala-animaatio.
///   Pelaaja painaa neliötä → GameState.HasJoystick = true (drone-moodi myöhemmin).
/// </summary>
public partial class JoystickLever : Area3D
{
	// ─────────────────────────────────────────────
	// EXPORTATUT MUUTTUJAT
	// ─────────────────────────────────────────────

	[Export] public NodePath EnemySpawnerPath;
	[Export] public NodePath StickPivotPath;

	/// <summary>
	/// Positiivinen astetta: kallistus eteenpäin (negatiivinen X-rotaatio; aiemmin oli päinvastoin).
	/// </summary>
	[Export] public float ActivateTiltDeg  = 35f;
	[Export] public float TiltDuration     = 0.4f;

	/// <summary>Hidastus kun vipu aktivoidaan (Engine.TimeScale). Palautetaan ajastimella (reaaliaika).</summary>
	[Export] public float CinematicTimeScale   = 0.4f;
	[Export] public float CinematicSlowSeconds = 0.36f;

	/// <summary>Negatiivinen = kamera lähemmäs (metriä). Kutsuu CameraFollow.ApplyMomentaryDistanceOffset.</summary>
	[Export] public float CinematicZoomDeltaMeters = -2.3f;
	[Export] public float CinematicZoomDurationSec  = 0.55f;
	[Export] public float CinematicShakeAmplitude  = 0.14f;

	/// <summary>Kellumisamplitudi ennen poimintaa (metriä).</summary>
	[Export] public float FloatAmplitude = 0.18f;

	/// <summary>Kellumisen nopeus (sykliä sekunnissa).</summary>
	[Export] public float FloatFrequency = 1.1f;

	[Export] public string PushStartText = "Push Start";
	[Export] public Vector3 PushStartPositionOffset = new(0f, 0.75f, 0f);
	[Export] public int PushStartFontSize = 42;
	[Export] public float PushStartPixelSize = 0.008f;
	[Export] public int PushStartOutlineSize = 10;

	// ─────────────────────────────────────────────
	// PRIVAATIT MUUTTUJAT
	// ─────────────────────────────────────────────

	private bool _playerInside    = false;
	private bool _leverActivated  = false;
	private bool _bossDefeated    = false;
	private bool _bossWasAlive    = false;
	private bool _pickedUp        = false;

	private float _floatTimer = 0f;
	private float _floatBaseY = 0f;

	private EnemySpawner    _spawner;
	private Node3D          _stickPivot;
	private Label3D         _pushStartLabel;
	private PlayerController _playerController;

	// ─────────────────────────────────────────────
	// ALUSTUS
	// ─────────────────────────────────────────────

	public override void _Ready()
	{
		// Area3D: varmista että pelaaja (CharacterBody3D) rekisteröityy — editorissa voi irrota.
		Monitoring  = true;
		Monitorable = true;
		CollisionMask = 0xFFFF_FFFF; // kaikki fyysiset layerit (vältä mask=0 / väärä layer)

		_spawner    = GetNodeOrNull<EnemySpawner>(EnemySpawnerPath);
		_stickPivot = GetNodeOrNull<Node3D>(StickPivotPath);

		if (_spawner    == null) GD.PrintErr("JoystickLever: EnemySpawnerPath puuttuu!");
		if (_stickPivot == null) GD.PrintErr("JoystickLever: StickPivotPath puuttuu!");

		TryReparentStrayJoystickFromLevelRoot();

		// joystick.glb + normal map: varmista tangentit (vältä rendering-varoitus / mustat meshet).
		try
		{
			MeshTangentFix.ApplyToSubtree(this);
		}
		catch (Exception ex)
		{
			GD.PrintErr("JoystickLever MeshTangentFix: " + ex.Message);
		}

		if (_stickPivot != null)
			_floatBaseY = _stickPivot.Position.Y;

		SetupPushStartLabel();

		BodyEntered += OnBodyEntered;
		BodyExited  += OnBodyExited;
	}

	private void SetupPushStartLabel()
	{
		_pushStartLabel = new Label3D
		{
			Name = "PushStartLabel",
			Text = PushStartText,
			Visible = false,
			FontSize = PushStartFontSize,
			PixelSize = PushStartPixelSize,
			OutlineSize = PushStartOutlineSize,
			OutlineModulate = new Color(0f, 0f, 0f, 0.85f),
			Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
			Modulate = new Color(1f, 1f, 1f, 0f),
			Position = PushStartPositionOffset,
		};
		AddChild(_pushStartLabel);
	}

	/// <summary>
	/// Editorissa joystick voi jäädä kentän juureen (parent = ".") — silloin kallistus ei toimi.
	/// Siirretään automaattisesti StickPivotin lapseksi säilyttäen maailma-asento.
	/// </summary>
	private void TryReparentStrayJoystickFromLevelRoot()
	{
		if (_stickPivot == null) return;

		// Level 1: joystick.glb voi olla StickPivotin sisaruksena — kallistus ei vaikuta malliin.
		foreach (Node c in GetChildren())
		{
			if (c == _stickPivot) continue;
			var path = c.SceneFilePath;
			if (string.IsNullOrEmpty(path) || path.IndexOf("joystick.glb", StringComparison.OrdinalIgnoreCase) < 0)
				continue;
			if (c.GetParent() == _stickPivot) return;
			GD.Print("JoystickLever: joystick.glb oli StickPivotin ulkopuolella — siirretään lapseksi.");
			c.Reparent(_stickPivot, true);
			return;
		}

		var levelRoot = GetParent();
		if (levelRoot == null) return;

		foreach (Node c in levelRoot.GetChildren())
		{
			if (c == this) continue;
			var path = c.SceneFilePath;
			if (string.IsNullOrEmpty(path) || path.IndexOf("joystick.glb", StringComparison.OrdinalIgnoreCase) < 0)
				continue;
			if (c.GetParent() == _stickPivot) return;
			GD.Print("JoystickLever: joystick.glb oli väärässä paikassa — siirretään StickPivotin lapseksi.");
			c.Reparent(_stickPivot, true);
			return;
		}
	}

	// ─────────────────────────────────────────────
	// PÄÄSILMUKKA
	// ─────────────────────────────────────────────

	public override void _Process(double delta)
	{
		if (_leverActivated && !_bossDefeated && !_pickedUp)
		{
			var boss = GetTree().GetFirstNodeInGroup("level1_boss") as BossLevel1;
			if (boss != null && GodotObject.IsInstanceValid(boss))
				_bossWasAlive = true;
			if (_bossWasAlive && (boss == null || !GodotObject.IsInstanceValid(boss) || boss.IsBossDead))
				NotifyBossDefeated();
		}

		if (!_pickedUp && _stickPivot != null)
		{
			_floatTimer += (float)delta * FloatFrequency * Mathf.Tau;
			float offset = Mathf.Sin(_floatTimer) * FloatAmplitude;
			var pos = _stickPivot.Position;
			_stickPivot.Position = new Vector3(pos.X, _floatBaseY + offset, pos.Z);
		}

		if (!_playerInside || _pickedUp)
			return;

		if (Input.IsActionJustPressed("grab"))
		{
			if (!_leverActivated)
				ActivateLever();
			else if (_bossDefeated)
				PickUpReward();
		}
	}

	// ─────────────────────────────────────────────
	// JULKISET METODIT
	// ─────────────────────────────────────────────

	public void NotifyBossDefeated()
	{
		if (_bossDefeated || _pickedUp) return;
		_bossDefeated = true;
		GD.Print("JoystickLever: bossi kuollut — joystick poimittavissa!");
		AnimateReward();
	}

	// ─────────────────────────────────────────────
	// VAIHE 1: aktivointi
	// ─────────────────────────────────────────────

	private void ActivateLever()
	{
		_leverActivated = true;
		GD.Print("JoystickLever: aktivoitu — viholliset spawnaavat!");

		if (_stickPivot != null)
		{
			var tween = CreateTween();
			tween.SetIgnoreTimeScale(true);
			// Negatiivinen X: kallistus eteenpäin (päinvastoin kuin aiempi +X → "taakse").
			tween.TweenProperty(_stickPivot, "rotation_degrees",
				new Vector3(-ActivateTiltDeg, 0f, 0f), TiltDuration)
				.SetTrans(Tween.TransitionType.Back)
				.SetEase(Tween.EaseType.Out);
		}

		PlayPushStartAnimation();

		RunActivateCinematic();

		_spawner?.Activate();
	}

	private void PlayPushStartAnimation()
	{
		if (_pushStartLabel == null || !GodotObject.IsInstanceValid(_pushStartLabel)) return;

		_pushStartLabel.Text = PushStartText;
		_pushStartLabel.Visible = true;
		_pushStartLabel.Modulate = new Color(1f, 1f, 1f, 0f);

		var tween = CreateTween();
		tween.SetIgnoreTimeScale(true);
		tween.SetParallel(false);
		// Arcade-tyylinen väläyttely
		for (int i = 0; i < 3; i++)
		{
			tween.TweenProperty(_pushStartLabel, "modulate", new Color(1f, 1f, 1f, 1f), 0.09f)
				.SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
			tween.TweenProperty(_pushStartLabel, "modulate", new Color(1f, 1f, 1f, 0.12f), 0.09f)
				.SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.In);
		}
		tween.TweenProperty(_pushStartLabel, "modulate", new Color(1f, 1f, 1f, 1f), 0.12f);
		tween.TweenProperty(_pushStartLabel, "modulate", new Color(1f, 1f, 1f, 0f), 0.45f)
			.SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.In);
		tween.TweenCallback(Callable.From(() =>
		{
			if (GodotObject.IsInstanceValid(_pushStartLabel))
				_pushStartLabel.Visible = false;
		}));
	}

	/// <summary>Hidastus (reaaliaikainen palautus), kamera lähemmäs, kevyt tärinä.</summary>
	private void RunActivateCinematic()
	{
		var cam = GetViewport()?.GetCamera3D() as CameraFollow;
		cam?.ApplyMomentaryDistanceOffset(CinematicZoomDeltaMeters, CinematicZoomDurationSec);
		cam?.ShakeImpulse(CinematicShakeAmplitude, 0.22f);

		double saved = Engine.TimeScale;
		Engine.TimeScale = Mathf.Clamp(CinematicTimeScale, 0.08f, 1f);
		var timer = GetTree().CreateTimer(CinematicSlowSeconds, false, true);
		timer.Timeout += () =>
		{
			Engine.TimeScale = saved;
		};
	}

	// ─────────────────────────────────────────────
	// VAIHE 2: palkinto
	// ─────────────────────────────────────────────

	private void AnimateReward()
	{
		if (_stickPivot == null) return;

		_floatBaseY = _stickPivot.Position.Y;

		var tween = CreateTween();
		tween.SetIgnoreTimeScale(true);
		tween.SetParallel(true);

		tween.TweenProperty(_stickPivot, "rotation_degrees",
			Vector3.Zero, TiltDuration * 1.5f)
			.SetTrans(Tween.TransitionType.Elastic)
			.SetEase(Tween.EaseType.Out);

		tween.TweenProperty(_stickPivot, "position:y",
			_floatBaseY + FloatAmplitude * 1.5f, TiltDuration * 2f)
			.SetTrans(Tween.TransitionType.Cubic)
			.SetEase(Tween.EaseType.Out);

		tween.TweenProperty(_stickPivot, "scale",
			Vector3.One * 1.35f, TiltDuration)
			.SetTrans(Tween.TransitionType.Cubic)
			.SetEase(Tween.EaseType.Out);
		tween.Chain().TweenProperty(_stickPivot, "scale",
			Vector3.One, TiltDuration * 0.8f)
			.SetTrans(Tween.TransitionType.Cubic)
			.SetEase(Tween.EaseType.In);
	}

	private void PickUpReward()
	{
		if (_playerController == null) return;
		_pickedUp = true;

		if (GameState.Instance != null)
			GameState.Instance.HasJoystick = true;

		GD.Print("JoystickLever: pelaaja poimi joystickin! (GameState.HasJoystick = true)");

		var tween = CreateTween();
		tween.SetIgnoreTimeScale(true);
		tween.TweenProperty(this, "scale", Vector3.Zero, 0.25f)
			.SetTrans(Tween.TransitionType.Cubic)
			.SetEase(Tween.EaseType.In);
		tween.TweenCallback(Callable.From(() => QueueFree()));
	}

	private void OnBodyEntered(Node3D body)
	{
		if (body is not PlayerController pc) return;
		_playerInside      = true;
		_playerController  = pc;

		if (!_leverActivated)
			GD.Print("Paina [Neliö] aktivoidaksesi joystick");
		else if (_bossDefeated)
			GD.Print("Paina [Neliö] poimiaksesi joystick — saat drone-ohjauksen!");
	}

	private void OnBodyExited(Node3D body)
	{
		if (body is not PlayerController) return;
		_playerInside     = false;
		_playerController = null;
	}
}
