using Godot;

/// <summary>
/// JoystickLever on kenttään sijoitettu vipu joka toimii kahdessa vaiheessa:
///
/// VAIHE 1 — Vipu (ennen bossia):
///   Pelaaja astuu alueelle ja painaa neliötä (grab).
///   Joystick kallistuu eteenpäin, EnemySpawner aktivoituu.
///
/// VAIHE 2 — Palkinto (bossin kuoltua):
///   Skripti pollaa bossia automaattisesti — NotifyBossDefeated() kutsutaan itsestään.
///   Joystick nousee, hohtaa syanilla ja kelluu ylös-alas.
///   Pelaaja painaa neliötä → saa DroneWeapon-avaimen (GameState.HasJoystick = true).
/// </summary>
public partial class JoystickLever : Area3D
{
	// ─────────────────────────────────────────────
	// EXPORTATUT MUUTTUJAT
	// ─────────────────────────────────────────────

	[Export] public NodePath EnemySpawnerPath;
	[Export] public NodePath StickPivotPath;
	[Export] public NodePath StickMeshPath;

	[Export] public float ActivateTiltDeg  = 35f;
	[Export] public float TiltDuration     = 0.4f;

	[Export] public Color  RewardGlowColor = new(0.2f, 0.8f, 1f, 1f);

	/// <summary>Kellumisamplitudi palkintovaiheessa (metriä).</summary>
	[Export] public float FloatAmplitude = 0.18f;

	/// <summary>Kellumisen nopeus (sykliä sekunnissa).</summary>
	[Export] public float FloatFrequency = 1.1f;

	// ─────────────────────────────────────────────
	// PRIVAATIT MUUTTUJAT
	// ─────────────────────────────────────────────

	private bool _playerInside    = false;
	private bool _leverActivated  = false;
	private bool _bossDefeated    = false;
	private bool _bossWasAlive    = false;  // bossi oli jossain vaiheessa paikalla
	private bool _pickedUp        = false;

	private float _floatTimer = 0f;
	private float _floatBaseY = 0f;        // joystickin lähtökorkeus

	private EnemySpawner    _spawner;
	private Node3D          _stickPivot;
	private MeshInstance3D  _stickMesh;
	private PlayerController _playerController;

	// ─────────────────────────────────────────────
	// ALUSTUS
	// ─────────────────────────────────────────────

	public override void _Ready()
	{
		_spawner    = GetNodeOrNull<EnemySpawner>(EnemySpawnerPath);
		_stickPivot = GetNodeOrNull<Node3D>(StickPivotPath);
		_stickMesh  = GetNodeOrNull<MeshInstance3D>(StickMeshPath);

		if (_spawner    == null) GD.PrintErr("JoystickLever: EnemySpawnerPath puuttuu!");
		if (_stickPivot == null) GD.PrintErr("JoystickLever: StickPivotPath puuttuu!");

		BodyEntered += OnBodyEntered;
		BodyExited  += OnBodyExited;
	}

	// ─────────────────────────────────────────────
	// PÄÄSILMUKKA
	// ─────────────────────────────────────────────

	public override void _Process(double delta)
	{
		// ── Boss-pollaus: havaitaan automaattisesti kun bossi kuolee ──
		if (_leverActivated && !_bossDefeated && !_pickedUp)
		{
			var boss = GetTree().GetFirstNodeInGroup("level1_boss") as BossLevel1;
			if (boss != null && GodotObject.IsInstanceValid(boss))
				_bossWasAlive = true;
			if (_bossWasAlive && (boss == null || !GodotObject.IsInstanceValid(boss) || boss.IsBossDead))
				NotifyBossDefeated();
		}

		// ── Kellumisanimaatio palkintovaiheessa ──
		if (_bossDefeated && !_pickedUp && _stickPivot != null)
		{
			_floatTimer += (float)delta * FloatFrequency * Mathf.Tau;
			float offset = Mathf.Sin(_floatTimer) * FloatAmplitude;
			var pos = _stickPivot.Position;
			_stickPivot.Position = new Vector3(pos.X, _floatBaseY + offset, pos.Z);
		}

		// ── Pelaajan syöte ──
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

	/// <summary>
	/// Muuttaa joystickin poimittavaksi palkinnoksi.
	/// Kutsutaan automaattisesti boss-pollauksesta; voidaan kutsua myös ulkoa.
	/// </summary>
	public void NotifyBossDefeated()
	{
		if (_bossDefeated || _pickedUp) return;
		_bossDefeated = true;
		GD.Print("JoystickLever: bossi kuollut — joystick poimittavissa!");
		AnimateReward();
	}

	// ─────────────────────────────────────────────
	// PRIVAATIT METODIT
	// ─────────────────────────────────────────────

	private void ActivateLever()
	{
		_leverActivated = true;
		GD.Print("JoystickLever: aktivoitu — viholliset spawnaavat!");

		if (_stickPivot != null)
		{
			var tween = CreateTween();
			tween.TweenProperty(_stickPivot, "rotation_degrees",
				new Vector3(ActivateTiltDeg, 0f, 0f), TiltDuration)
				.SetTrans(Tween.TransitionType.Back)
				.SetEase(Tween.EaseType.Out);
		}

		_spawner?.Activate();
	}

	/// <summary>
	/// Nostaa joystickin pystyyn, vaihtaa sen syaanin hohtavaksi ja alkaa
	/// kelluttamaan sitä ylös-alas. Tämä on level_1:n "tajuamisanimaatio" —
	/// pelaaja näkee joystickin kohoavan ja hohtavan kun bossi kaatuu.
	/// </summary>
	private void AnimateReward()
	{
		if (_stickPivot == null) return;

		_floatBaseY = _stickPivot.Position.Y;

		var tween = CreateTween();
		tween.SetParallel(true);

		// 1. Nosta joystick takaisin pystyyn
		tween.TweenProperty(_stickPivot, "rotation_degrees",
			Vector3.Zero, TiltDuration * 1.5f)
			.SetTrans(Tween.TransitionType.Elastic)
			.SetEase(Tween.EaseType.Out);

		// 2. Nosta ylöspäin (alkuasennosta FloatAmplitude eteenpäin)
		tween.TweenProperty(_stickPivot, "position:y",
			_floatBaseY + FloatAmplitude * 1.5f, TiltDuration * 2f)
			.SetTrans(Tween.TransitionType.Cubic)
			.SetEase(Tween.EaseType.Out);

		// 3. Skaalaa hetken isommaksi "herätys"-efektinä, sitten takaisin
		tween.TweenProperty(_stickPivot, "scale",
			Vector3.One * 1.35f, TiltDuration)
			.SetTrans(Tween.TransitionType.Cubic)
			.SetEase(Tween.EaseType.Out);
		tween.Chain().TweenProperty(_stickPivot, "scale",
			Vector3.One, TiltDuration * 0.8f)
			.SetTrans(Tween.TransitionType.Cubic)
			.SetEase(Tween.EaseType.In);

		// Vaihda mesh-materiaali hohtavaksi
		if (_stickMesh != null)
		{
			var mat = new StandardMaterial3D
			{
				AlbedoColor              = RewardGlowColor,
				EmissionEnabled          = true,
				Emission                 = RewardGlowColor,
				EmissionEnergyMultiplier = 2.5f,
			};
			_stickMesh.MaterialOverride = mat;
		}
	}

	/// <summary>
	/// Pelaaja poimii joystickin — tallentaa tilan GameStateen
	/// ja poistaa joystickin kentästä.
	/// </summary>
	private void PickUpReward()
	{
		if (_playerController == null) return;
		_pickedUp = true;

		// Tallennetaan pysyvä tila — level 2:sta eteenpäin kolmio aktivoi drone-moodin
		if (GameState.Instance != null)
			GameState.Instance.HasJoystick = true;

		GD.Print("JoystickLever: pelaaja poimi joystickin! (GameState.HasJoystick = true)");

		// Pieni "pickup"-animaatio ennen poistamista
		var tween = CreateTween();
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
