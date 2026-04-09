using Godot;
 
/// <summary>
/// JoystickLever on kenttään sijoitettu vipu joka toimii kahdessa vaiheessa:
///
/// VAIHE 1 — Vipu (ennen bossia):
///   Pelaaja astuu alueelle ja painaa neliötä (grab).
///   Joystick kallistuu eteenpäin, EnemySpawner aktivoituu.
///
/// VAIHE 2 — Palkinto (bossin kuoltua):
///   Joystick nousee takaisin pystyyn ja alkaa hohtaa.
///   Pelaaja voi poimia sen neliöllä — saa DroneWeapon-aseen käyttöön.
///
/// Kutsu NotifyBossDefeated() ulkoa (esim. BossLevel1.cs Die()-metodista).
/// </summary>
public partial class JoystickLever : Area3D
{
	// ─────────────────────────────────────────────
	// EXPORTATUT MUUTTUJAT
	// ─────────────────────────────────────────────
 
	/// <summary>Polku EnemySpawner-nodeen.</summary>
	[Export] public NodePath EnemySpawnerPath;
 
	/// <summary>Polku StickPivot-nodeen (kallistuu aktivoituessa).</summary>
	[Export] public NodePath StickPivotPath;
 
	/// <summary>Polku varren MeshInstance3D:hen (väri vaihtuu palkintovaiheessa).</summary>
	[Export] public NodePath StickMeshPath;
 
	/// <summary>Kallistuskulma asteina kun vipu aktivoidaan (eteenpäin).</summary>
	[Export] public float ActivateTiltDeg = 35f;
 
	/// <summary>Kallistusanimaation kesto sekunteina.</summary>
	[Export] public float TiltDuration = 0.4f;
 
	/// <summary>Palkinnon hohtamisen väri.</summary>
	[Export] public Color RewardGlowColor = new(0.2f, 0.8f, 1f, 1f);
 
	// ─────────────────────────────────────────────
	// PRIVAATIT MUUTTUJAT
	// ─────────────────────────────────────────────
 
	private bool _playerInside = false;
	private bool _leverActivated = false;   // vipu painettu
	private bool _bossDefeated = false;     // bossi kuollut
	private bool _pickedUp = false;         // pelaaja poiminut
 
	private EnemySpawner _spawner;
	private Node3D _stickPivot;
	private MeshInstance3D _stickMesh;
	private PlayerController _playerController;
 
	// ─────────────────────────────────────────────
	// ALUSTUS
	// ─────────────────────────────────────────────
 
	public override void _Ready()
	{
		_spawner     = GetNodeOrNull<EnemySpawner>(EnemySpawnerPath);
		_stickPivot  = GetNodeOrNull<Node3D>(StickPivotPath);
		_stickMesh   = GetNodeOrNull<MeshInstance3D>(StickMeshPath);
 
		if (_spawner == null)
			GD.PrintErr("JoystickLever: EnemySpawnerPath puuttuu!");
		if (_stickPivot == null)
			GD.PrintErr("JoystickLever: StickPivotPath puuttuu!");
 
		BodyEntered += OnBodyEntered;
		BodyExited  += OnBodyExited;
	}
 
	// ─────────────────────────────────────────────
	// PÄÄSILMUKKA
	// ─────────────────────────────────────────────
 
	public override void _Process(double delta)
	{
		if (!_playerInside || _pickedUp)
			return;
 
		if (Input.IsActionJustPressed("grab"))
		{
			if (!_leverActivated)
			{
				// VAIHE 1: Aktivoi vipu
				ActivateLever();
			}
			else if (_bossDefeated)
			{
				// VAIHE 2: Poimi palkinto
				PickUpReward();
			}
		}
	}
 
	// ─────────────────────────────────────────────
	// JULKISET METODIT
	// ─────────────────────────────────────────────
 
	/// <summary>
	/// Kutsutaan BossLevel1.cs:n Die()-metodista kun bossi kuolee.
	/// Muuttaa joystickin poimittavaksi palkinnoksi.
	/// </summary>
	public void NotifyBossDefeated()
	{
		if (_pickedUp) return;
		_bossDefeated = true;
		GD.Print("JoystickLever: bossi kuollut — joystick poimittavissa!");
		AnimateReward();
	}
 
	// ─────────────────────────────────────────────
	// PRIVAATIT METODIT
	// ─────────────────────────────────────────────
 
	/// <summary>Kallistaa joystickin eteenpäin ja aktivoi spawnerin.</summary>
	private void ActivateLever()
	{
		_leverActivated = true;
		GD.Print("JoystickLever: aktivoitu — viholliset spawnaavat!");
 
		// Kallistetaan StickPivot eteenpäin Tweenillä
		if (_stickPivot != null)
		{
			var tween = CreateTween();
			tween.TweenProperty(
				_stickPivot,
				"rotation_degrees",
				new Vector3(ActivateTiltDeg, 0f, 0f),
				TiltDuration)
				.SetTrans(Tween.TransitionType.Back)
				.SetEase(Tween.EaseType.Out);
		}
 
		// Käynnistetään viholliset
		_spawner?.Activate();
	}
 
	/// <summary>
	/// Nostaa joystickin takaisin pystyyn ja vaihtaa värin
	/// merkiksi että sen voi poimia.
	/// </summary>
	private void AnimateReward()
	{
		if (_stickPivot == null) return;
 
		// Nostetaan takaisin pystyyn
		var tween = CreateTween();
		tween.TweenProperty(
			_stickPivot,
			"rotation_degrees",
			Vector3.Zero,
			TiltDuration * 1.5f)
			.SetTrans(Tween.TransitionType.Elastic)
			.SetEase(Tween.EaseType.Out);
 
		// Vaihdetaan varren väri hohtavaksi
		if (_stickMesh != null)
		{
			var mat = new StandardMaterial3D();
			mat.AlbedoColor = RewardGlowColor;
			mat.EmissionEnabled = true;
			mat.Emission = RewardGlowColor;
			mat.EmissionEnergyMultiplier = 2.0f;
			_stickMesh.MaterialOverride = mat;
		}
	}
 
	/// <summary>
	/// Pelaaja poimii joystickin — antaa DroneWeapon-aseen
	/// ja poistaa joystickin kentästä.
	/// </summary>
private void PickUpReward()
{
	if (_playerController == null) return;
	_pickedUp = true;
	GD.Print("JoystickLever: pelaaja poimi joystickin!");

	// TODO: DroneWeapon lisätään myöhemmin
	// var drone = _playerController.GetNodeOrNull<DroneWeapon>("DroneWeapon");
	// if (drone != null) drone.Unlock();

	QueueFree();
}
 
	/// <summary>Pelaaja astuu alueelle.</summary>
	private void OnBodyEntered(Node3D body)
	{
		if (body is PlayerController pc)
		{
			_playerInside = true;
			_playerController = pc;
 
			if (!_leverActivated)
				GD.Print("Paina [Neliö] aktivoidaksesi joystick");
			else if (_bossDefeated)
				GD.Print("Paina [Neliö] poimiaksesi joystick");
		}
	}
 
	/// <summary>Pelaaja poistuu alueelta.</summary>
	private void OnBodyExited(Node3D body)
	{
		if (body is PlayerController)
		{
			_playerInside = false;
			_playerController = null;
		}
	}
}
