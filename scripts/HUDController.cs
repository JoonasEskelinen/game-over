using Godot;

// HUDController hallinnoi pelin käyttöliittymää (HUD = Heads Up Display).
// Tällä hetkellä se päivittää healthbarin kun pelaajan elinvoima muuttuu.
// Myöhemmin tähän lisätään muut UI-elementit kuten tason nimi ja erikoisaseet.
public partial class HUDController : CanvasLayer
{
	// Viittaus HealthBar-nodeen — päivitetään kun pelaaja ottaa vahinkoa
	private ProgressBar _healthBar;

	// Viittaus pelaajaan — tarvitaan HealthComponentin signaalien kuunteluun
	private PlayerController _player;

	// _Ready ajetaan kun HUD ladataan sceneen
	public override void _Ready()
	{
		// Haetaan HealthBar-node HUD:in lapsista nimellä
		_healthBar = GetNode<ProgressBar>("HealthBar");

		// Haetaan pelaaja scenetreestä — polku muuttuu myöhemmin jos rakenne muuttuu
		// GetTree().Root hakee scenen juuresta, sitten etsitään Player-node
		_player = GetTree().Root.FindChild("Player", true, false) as PlayerController;

		// Tarkistetaan että pelaaja löytyi
		if (_player == null)
		{
			GD.PrintErr("HUDController: Pelaajaa ei löydy scenestä!");
			return;
		}

		// Haetaan HealthComponent pelaajalta
		HealthComponent healthComponent = _player.GetNode<HealthComponent>("HealthComponent");

		// Kytketään HealthComponentin signaali HUD:iin
		// Kun elinvoima muuttuu, kutsutaan UpdateHealthBar-funktiota
		healthComponent.HealthChanged += UpdateHealthBar;

		// Alustetaan healthbar oikeaan arvoon heti pelin alussa
		UpdateHealthBar(healthComponent.GetCurrentHealth(), healthComponent.MaxHealth);
	}

	// UpdateHealthBar päivittää healthbarin visuaalisen tilan
	// currentHealth = nykyinen elinvoima, maxHealth = maksimielinvoima
	private void UpdateHealthBar(int currentHealth, int maxHealth)
	{
		// Lasketaan elinvoima prosentteina (0-100) ja asetetaan se healthbarille
		// Esimerkki: currentHealth=2, maxHealth=3 → 2/3 * 100 = 66.6%
		float healthPercent = (float)currentHealth / maxHealth * 100f;
		_healthBar.Value = healthPercent;

		GD.Print($"HealthBar päivitetty: {healthPercent}%");
	}
}
