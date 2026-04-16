using Godot;

// HUDController hallinnoi pelin käyttöliittymää (HUD = Heads Up Display).
// Tällä hetkellä se päivittää healthbarin kun pelaajan elinvoima muuttuu.
// Myöhemmin tähän lisätään muut UI-elementit kuten tason nimi ja erikoisaseet.
public partial class HUDController : CanvasLayer
{
	private ProgressBar _healthBar;
	private ProgressBar _heavyAttackBar;
	private ProgressBar _bossHealthBar;
	private Label _bossR1Hint;

	private PlayerController _player;

	// _Ready ajetaan kun HUD ladataan sceneen
	public override void _Ready()
	{
		// Haetaan HealthBar-node HUD:in lapsista nimellä
		_healthBar = GetNode<ProgressBar>("HealthBar");
		_heavyAttackBar = GetNodeOrNull<ProgressBar>("HeavyAttackCooldownBar");
		_bossHealthBar = GetNodeOrNull<ProgressBar>("BossHealthBar");
		_bossR1Hint = GetNodeOrNull<Label>("BossR1Hint");
		if (_bossHealthBar != null)
			_bossHealthBar.Visible = false;

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

	public override void _Process(double delta)
	{
		var boss1 = GetTree().GetFirstNodeInGroup("level1_boss") as BossLevel1;
		var boss2 = GetTree().GetFirstNodeInGroup("level2_boss") as BossLevel2;

		if (_bossR1Hint != null)
			_bossR1Hint.Visible = boss1 != null && GodotObject.IsInstanceValid(boss1) && boss1.IsInsideTree() && boss1.IsDanceVulnerable;

		if (_bossHealthBar != null)
		{
			if (boss1 != null && GodotObject.IsInstanceValid(boss1) && boss1.IsInsideTree() && !boss1.IsBossDead)
			{
				_bossHealthBar.Visible = true;
				int maxHp = Mathf.Max(1, boss1.GetBossMaxHealth());
				_bossHealthBar.MaxValue = maxHp;
				_bossHealthBar.Value = Mathf.Clamp(boss1.GetBossCurrentHealth(), 0, maxHp);
			}
			else if (boss2 != null && GodotObject.IsInstanceValid(boss2) && boss2.IsInsideTree() && !boss2.IsBossDead)
			{
				_bossHealthBar.Visible = true;
				int maxHp = Mathf.Max(1, boss2.GetBossMaxHealth());
				_bossHealthBar.MaxValue = maxHp;
				_bossHealthBar.Value = Mathf.Clamp(boss2.GetBossCurrentHealth(), 0, maxHp);
			}
			else
				_bossHealthBar.Visible = false;
		}

		if (_player == null || _heavyAttackBar == null) return;
		if (!_player.ShouldShowHeavyCooldownBar())
		{
			_heavyAttackBar.Visible = false;
			return;
		}

		_heavyAttackBar.Visible = true;
		_heavyAttackBar.Value = _player.GetHeavyAttackCooldownFill01() * 100.0;
	}

	// UpdateHealthBar päivittää healthbarin visuaalisen tilan
	// currentHealth = nykyinen elinvoima, maxHealth = maksimielinvoima
	private void UpdateHealthBar(int currentHealth, int maxHealth)
	{
		// Lasketaan elinvoima prosentteina (0-100) ja asetetaan se healthbarille
		// Esimerkki: currentHealth=2, maxHealth=3 → 2/3 * 100 = 66.6%
		float healthPercent = (float)currentHealth / maxHealth * 100f;
		_healthBar.Value = healthPercent;
	}
}
