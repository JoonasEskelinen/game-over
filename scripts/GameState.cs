using Godot;

/// <summary>
/// AutoLoad-singleton — säilyttää pelin tilan kenttien välillä muistissa.
/// Rekisteröity project.godot:n [autoload]-osiossa nimellä "GameState".
///
/// Käyttö: GameState.Instance.HasJoystick = true;
/// </summary>
public partial class GameState : Node
{
	public static GameState Instance { get; private set; }

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
		GD.Print("GameState: alustettu.");
	}
}
