using Godot;

// HealthComponent on erillinen komponentti joka hallinnoi pelaajan elinvoimaa.
// Pitämällä health-logiikka erillään PlayerControllerista koodi pysyy siistinä
// ja samaa komponenttia voi myöhemmin käyttää myös vihollisilla.
public partial class HealthComponent : Node
{
	// MaxHealth määrittää pelaajan maksimielinvoiman.
	// [Export] tarkoittaa että arvo näkyy ja on muutettavissa Godot-editorissa
	// ilman että tarvitsee koskea koodiin.
	[Export] public int MaxHealth = 3;

	// CurrentHealth on pelaajan tämänhetkinen elinvoima.
	// Se alustetaan MaxHealthin arvoon pelin alussa.
	private int _currentHealth;

	// Signal on Godotin viestijärjestelmä — kun pelaaja ottaa vahinkoa tai kuolee,
	// tämä komponentti lähettää signaalin jota muut nodet (esim. UI) voivat kuunnella.
	// Näin HealthComponent ei tarvitse tietää mitään UI:sta — ne ovat erillisiä.
	[Signal] public delegate void HealthChangedEventHandler(int currentHealth, int maxHealth);
	[Signal] public delegate void PlayerDiedEventHandler();

	// _Ready ajetaan kun node ladataan sceneen.
	// Tässä alustetaan elinvoima maksimiin pelin alussa.
	public override void _Ready()
	{
		_currentHealth = MaxHealth;
	}

	// TakeDamage vähennetään pelaajan elinvoimaa.
	// amount = kuinka paljon vahinkoa otetaan
	public void TakeDamage(int amount)
	{
		// Vähennetään elinvoimaa
		_currentHealth -= amount;

		// Varmistetaan että elinvoima ei mene negatiiviseksi
		_currentHealth = Mathf.Max(_currentHealth, 0);

		// Lähetetään signaali UI:lle että elinvoima muuttui
		EmitSignal(SignalName.HealthChanged, _currentHealth, MaxHealth);

		GD.Print($"Pelaaja otti {amount} vahinkoa! Elinvoima: {_currentHealth}/{MaxHealth}");

		// Jos elinvoima on 0, pelaaja kuolee
		if (_currentHealth <= 0)
		{
			Die();
		}
	}

	// Heal palauttaa pelaajan elinvoimaa.
	// amount = kuinka paljon elinvoimaa palautetaan
	public void Heal(int amount)
	{
		// Lisätään elinvoimaa mutta ei ylitetä maksimia
		_currentHealth = Mathf.Min(_currentHealth + amount, MaxHealth);

		// Lähetetään signaali UI:lle että elinvoima muuttui
		EmitSignal(SignalName.HealthChanged, _currentHealth, MaxHealth);

		GD.Print($"Pelaaja parani {amount}! Elinvoima: {_currentHealth}/{MaxHealth}");
	}

	// GetCurrentHealth palauttaa nykyisen elinvoiman.
	// Tätä käytetään kun jokin muu node haluaa tietää pelaajan elinvoiman.
	public int GetCurrentHealth()
	{
		return _currentHealth;
	}

	// Die käsittelee pelaajan kuoleman.
	// Tällä hetkellä lähetetään vain signaali — myöhemmin tähän lisätään
	// kuolemisanimaatio, respawn-logiikka ja game over -näyttö.
	private void Die()
	{
		GD.Print("Pelaaja kuoli!");

		// Lähetetään signaali muille nodeille että pelaaja kuoli
		EmitSignal(SignalName.PlayerDied);
	}
}
