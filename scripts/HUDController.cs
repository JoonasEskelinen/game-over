using Godot;

public partial class HUDController : CanvasLayer
{
	private ProgressBar _healthBar;
	private ProgressBar _heavyAttackBar;
	private ProgressBar _bossHealthBar;
	private Label _bossR1Hint;
	private Label _r1Label;
	private PlayerController _player;

	// Värit
	private static readonly Color _hpGreen      = new(0.15f, 0.85f, 0.25f, 1f);
	private static readonly Color _hpRed        = new(0.85f, 0.12f, 0.12f, 1f);
	private static readonly Color _hpBg         = new(0.08f, 0.08f, 0.08f, 0.85f);
	private static readonly Color _heavyFill    = new(0.95f, 0.75f, 0.10f, 1f);
	private static readonly Color _heavyBg      = new(0.08f, 0.08f, 0.08f, 0.85f);
	private static readonly Color _bossFill     = new(0.80f, 0.10f, 0.10f, 1f);
	private static readonly Color _bossBg       = new(0.08f, 0.08f, 0.08f, 0.85f);
	private static readonly Color _bossGold     = new(0.95f, 0.70f, 0.05f, 1f);
	private static readonly Color _borderColor  = new(0.25f, 0.25f, 0.25f, 1f);

	public override void _Ready()
	{
		_healthBar      = GetNode<ProgressBar>("HealthBar");
		_heavyAttackBar = GetNodeOrNull<ProgressBar>("HeavyAttackCooldownBar");
		_bossHealthBar  = GetNodeOrNull<ProgressBar>("BossHealthBar");
		_bossR1Hint     = GetNodeOrNull<Label>("BossR1Hint");

		SetupLayout();
		StyleHealthBar();
		StyleHeavyBar();
		StyleBossBar();

		if (_bossHealthBar != null)
			_bossHealthBar.Visible = false;

		_player = GetTree().Root.FindChild("Player", true, false) as PlayerController;
		if (_player == null) { GD.PrintErr("HUDController: Pelaajaa ei löydy!"); return; }

		var hc = _player.GetNode<HealthComponent>("HealthComponent");
		hc.HealthChanged += UpdateHealthBar;
		UpdateHealthBar(hc.GetCurrentHealth(), hc.MaxHealth);
	}

	// ─── Layout: sijainnit ruudulla ───────────────────────────────────────────

	private void SetupLayout()
	{
		// Pelaajan HP — vasen yläkulma
		if (_healthBar != null)
		{
			_healthBar.SetAnchorsPreset(Control.LayoutPreset.TopLeft);
			_healthBar.Position = new Vector2(20, 20);
			_healthBar.Size     = new Vector2(220, 22);
			_healthBar.ShowPercentage = false;
		}

		// R1-latauspalkki — HP:n alle
		if (_heavyAttackBar != null)
		{
			_heavyAttackBar.SetAnchorsPreset(Control.LayoutPreset.TopLeft);
			_heavyAttackBar.Position = new Vector2(20, 52);
			_heavyAttackBar.Size     = new Vector2(150, 14);
			_heavyAttackBar.ShowPercentage = false;
			_heavyAttackBar.Visible  = false;

			// "R1 ⚔" -teksti palkin ylle
			_r1Label = new Label();
			_r1Label.Text = "R1  ⚔";
			_r1Label.Position = new Vector2(20, 36);
			_r1Label.AddThemeColorOverride("font_color", _heavyFill);
			_r1Label.AddThemeFontSizeOverride("font_size", 11);
			AddChild(_r1Label);
			_r1Label.Visible = false;
		}

		// Boss HP — oikea yläkulma
		if (_bossHealthBar != null)
		{
			_bossHealthBar.SetAnchorsPreset(Control.LayoutPreset.TopRight);
			_bossHealthBar.AnchorLeft   = 1f;
			_bossHealthBar.AnchorRight  = 1f;
			_bossHealthBar.Position     = new Vector2(-320, 20);
			_bossHealthBar.Size         = new Vector2(300, 26);
			_bossHealthBar.ShowPercentage = false;
		}
	}

	// ─── Tyylittelyt ─────────────────────────────────────────────────────────

	private void StyleHealthBar()
	{
		if (_healthBar == null) return;

		var fill = MakeStyleBox(_hpGreen, _borderColor, cornerRadius: 4);
		var bg   = MakeStyleBox(_hpBg,    _borderColor, cornerRadius: 4);

		var theme = new Theme();
		theme.SetStylebox("fill",       "ProgressBar", fill);
		theme.SetStylebox("background", "ProgressBar", bg);
		_healthBar.Theme = theme;
	}

	private void StyleHeavyBar()
	{
		if (_heavyAttackBar == null) return;

		var fill = MakeStyleBox(_heavyFill, _borderColor, cornerRadius: 3);
		var bg   = MakeStyleBox(_heavyBg,   _borderColor, cornerRadius: 3);

		var theme = new Theme();
		theme.SetStylebox("fill",       "ProgressBar", fill);
		theme.SetStylebox("background", "ProgressBar", bg);
		_heavyAttackBar.Theme = theme;
	}

	private void StyleBossBar()
	{
		if (_bossHealthBar == null) return;

		var fill = MakeStyleBox(_bossFill, _bossGold, cornerRadius: 5, borderWidth: 2);
		var bg   = MakeStyleBox(_bossBg,   _bossGold, cornerRadius: 5, borderWidth: 2);

		var theme = new Theme();
		theme.SetStylebox("fill",       "ProgressBar", fill);
		theme.SetStylebox("background", "ProgressBar", bg);
		_bossHealthBar.Theme = theme;
	}

	private static StyleBoxFlat MakeStyleBox(Color fill, Color border,
		int cornerRadius = 4, int borderWidth = 1)
	{
		return new StyleBoxFlat
		{
			BgColor             = fill,
			BorderColor         = border,
			BorderWidthTop      = borderWidth,
			BorderWidthBottom   = borderWidth,
			BorderWidthLeft     = borderWidth,
			BorderWidthRight    = borderWidth,
			CornerRadiusTopLeft     = cornerRadius,
			CornerRadiusTopRight    = cornerRadius,
			CornerRadiusBottomLeft  = cornerRadius,
			CornerRadiusBottomRight = cornerRadius,
		};
	}

	// ─── Pääsilmukka ─────────────────────────────────────────────────────────

	public override void _Process(double delta)
	{
		UpdateBossBar();
		UpdateHeavyBar();
	}

	private void UpdateBossBar()
	{
		if (_bossHealthBar == null) return;

		var boss1 = GetTree().GetFirstNodeInGroup("level1_boss") as BossLevel1;
		var boss2 = GetTree().GetFirstNodeInGroup("level2_boss") as BossLevel2;

		if (_bossR1Hint != null)
			_bossR1Hint.Visible = boss1 != null && GodotObject.IsInstanceValid(boss1)
				&& boss1.IsInsideTree() && boss1.IsDanceVulnerable;

		if (boss1 != null && GodotObject.IsInstanceValid(boss1)
			&& boss1.IsInsideTree() && !boss1.IsBossDead)
		{
			_bossHealthBar.Visible = true;
			int maxHp = Mathf.Max(1, boss1.GetBossMaxHealth());
			_bossHealthBar.MaxValue = maxHp;
			_bossHealthBar.Value    = Mathf.Clamp(boss1.GetBossCurrentHealth(), 0, maxHp);
		}
		else if (boss2 != null && GodotObject.IsInstanceValid(boss2)
			&& boss2.IsInsideTree() && !boss2.IsBossDead)
		{
			_bossHealthBar.Visible = true;
			int maxHp = Mathf.Max(1, boss2.GetBossMaxHealth());
			_bossHealthBar.MaxValue = maxHp;
			_bossHealthBar.Value    = Mathf.Clamp(boss2.GetBossCurrentHealth(), 0, maxHp);
		}
		else
		{
			_bossHealthBar.Visible = false;
		}
	}

	private void UpdateHeavyBar()
	{
		if (_player == null || _heavyAttackBar == null) return;

		bool show = _player.ShouldShowHeavyCooldownBar();
		_heavyAttackBar.Visible = show;
		if (_r1Label != null) _r1Label.Visible = show;

		if (!show) return;
		_heavyAttackBar.Value = _player.GetHeavyAttackCooldownFill01() * 100.0;
	}

	// ─── HP-palkin väri muuttuu punaiseksi kun alle 33% ──────────────────────

	private void UpdateHealthBar(int currentHealth, int maxHealth)
	{
		float pct = (float)currentHealth / maxHealth;
		_healthBar.Value = pct * 100f;

		// Alle 33% → vaihda fill punaiseksi
		if (_healthBar?.Theme == null) return;
		var fill = MakeStyleBox(pct <= 0.34f ? _hpRed : _hpGreen, _borderColor, cornerRadius: 4);
		_healthBar.Theme.SetStylebox("fill", "ProgressBar", fill);
	}
}
