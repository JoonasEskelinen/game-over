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
	/// True kun pelaaja on poiminut joystickin level_1:ssä.
	/// Level 2:sta eteenpäin aktivoi drone-moodin kolmio-painikkeella.
	/// </summary>
	public bool HasJoystick { get; set; } = false;

	// ─────────────────────────────────────────────

	public override void _Ready()
	{
		Instance = this;
		GD.Print("GameState: alustettu.");
	}
}
