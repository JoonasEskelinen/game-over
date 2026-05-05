using Godot;

/// <summary>
/// AutoLoad-singleton — säilyttää pelin tilan kenttien välillä muistissa.
/// Rekisteröity project.godot:n [autoload]-osiossa nimellä "GameState".
///
/// Käyttö: GameState.Instance.HasJoystick = true;
/// Joystick-lippu tallennetaan samaan <c>user://savegame.cfg</c> -tiedostoon kuin elämät (<see cref="HealthComponent"/>).
/// </summary>
public partial class GameState : Node
{
	public static GameState Instance { get; private set; }

	private const string SavePath = "user://savegame.cfg";
	private const string JoystickSection = "progress";
	private const string JoystickKey = "has_joystick";

	// ─────────────────────────────────────────────
	// PELITILA
	// ─────────────────────────────────────────────

	/// <summary>
	/// True kun pelaaja on poiminut joystickin level_1:ssä (tarina / drone level_1:ssä).
	/// Kolmio → drone vain level_1-scenessä; muilla kentillä kolmio = miekka/kilpi (<see cref="PlayerController"/>).
	/// </summary>
	public bool HasJoystick { get; set; } = false;

	/// <summary>
	/// Latausruudulle: kentän res://-polku, joka ladataan taustalla ennen scene-vaihtoa.
	/// Tyhjennetään kun lataus on käynnistetty.
	/// </summary>
	public string PendingLoadScenePath { get; set; } = "";

	// ─────────────────────────────────────────────

	public override void _Ready()
	{
		Instance = this;
		LoadJoystickFromSave();
		GD.Print("GameState: alustettu.");
	}

	/// <summary>Lataa joystick-keräyksen tallenteesta (sama tiedosto kuin elämät).</summary>
	public void LoadJoystickFromSave()
	{
		var config = new ConfigFile();
		if (config.Load(SavePath) != Error.Ok)
			return;
		if (!config.HasSectionKey(JoystickSection, JoystickKey))
			return;
		HasJoystick = config.GetValue(JoystickSection, JoystickKey, false).AsBool();
	}

	/// <summary>Tallenna <see cref="HasJoystick"/> levylle (yhdistää olemassa olevan savegame.cfg:n).</summary>
	public void PersistHasJoystickToSave()
	{
		var config = new ConfigFile();
		config.Load(SavePath);
		config.SetValue(JoystickSection, JoystickKey, HasJoystick);
		var err = config.Save(SavePath);
		if (err != Error.Ok)
			GD.PrintErr("GameState: PersistHasJoystickToSave epäonnistui: " + err);
	}
}
