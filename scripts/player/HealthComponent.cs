using Godot;

public partial class HealthComponent : Node
{
	[Export] public int MaxHealth = 3;
	[Export] public int MaxLives = 3;

	private int _currentHealth;
	private int _currentLives;

	[Signal] public delegate void HealthChangedEventHandler(int currentHealth, int maxHealth);
	[Signal] public delegate void LivesChangedEventHandler(int currentLives, int maxLives);
	[Signal] public delegate void PlayerDiedEventHandler();
	[Signal] public delegate void GameOverEventHandler();

	public override void _Ready()
	{
		// Ladataan tallenne jos se on olemassa
		_currentLives = LoadLives();
		_currentHealth = MaxHealth;

		EmitSignal(SignalName.HealthChanged, _currentHealth, MaxHealth);
		EmitSignal(SignalName.LivesChanged, _currentLives, MaxLives);
	}

	public void TakeDamage(int amount)
	{
		_currentHealth -= amount;
		_currentHealth = Mathf.Max(_currentHealth, 0);

		EmitSignal(SignalName.HealthChanged, _currentHealth, MaxHealth);
		GD.Print($"Vahinkoa: {amount} — HP: {_currentHealth}/{MaxHealth} — Elämät: {_currentLives}");

		if (_currentHealth <= 0)
			Die();
	}

	public void Heal(int amount)
	{
		_currentHealth = Mathf.Min(_currentHealth + amount, MaxHealth);
		EmitSignal(SignalName.HealthChanged, _currentHealth, MaxHealth);
	}

	public int GetCurrentHealth() => _currentHealth;
	public int GetCurrentLives() => _currentLives;

	private void Die()
	{
		_currentLives--;
		EmitSignal(SignalName.LivesChanged, _currentLives, MaxLives);
		GD.Print($"Pelaaja kuoli! Elämät jäljellä: {_currentLives}");

		if (_currentLives <= 0)
		{
			// Ei elämää jäljellä — Game Over
			// Nollataan elämät tallennuksessa
			SaveLives(MaxLives);
			GD.Print("GAME OVER!");
			EmitSignal(SignalName.GameOver);
		}
		else
		{
			// Elämää jäljellä — respawn
			_currentHealth = MaxHealth;
			EmitSignal(SignalName.HealthChanged, _currentHealth, MaxHealth);
			EmitSignal(SignalName.PlayerDied);
		}
	}

	/// <summary>
	/// Kutsutaan kun kenttä on läpäisty — tallentaa elämät
	/// </summary>
	public void OnLevelCompleted()
	{
		SaveLives(_currentLives);
		GD.Print($"Kenttä läpäisty! Tallennettu elämät: {_currentLives}");
	}

	/// <summary>
	/// Palauttaa elämät täyteen (esim. uuden pelin alkaessa)
	/// </summary>
	public void ResetLives()
	{
		_currentLives = MaxLives;
		_currentHealth = MaxHealth;
		SaveLives(MaxLives);
		EmitSignal(SignalName.HealthChanged, _currentHealth, MaxHealth);
		EmitSignal(SignalName.LivesChanged, _currentLives, MaxLives);
	}

	// --- Tallennus ---

	private const string SavePath = "user://savegame.cfg";
	private const string SaveSection = "player";
	private const string SaveKey = "lives";

	private void SaveLives(int lives)
	{
		var config = new ConfigFile();
		config.SetValue(SaveSection, SaveKey, lives);
		config.Save(SavePath);
		GD.Print($"Tallennettu elämät: {lives}");
	}

	private int LoadLives()
	{
		var config = new ConfigFile();
		if (config.Load(SavePath) == Error.Ok)
		{
			int saved = (int)config.GetValue(SaveSection, SaveKey, MaxLives);
			GD.Print($"Ladattu tallennus — elämät: {saved}");
			return saved;
		}
		// Ei tallennetta — aloitetaan täysillä elämillä
		return MaxLives;
	}
}
