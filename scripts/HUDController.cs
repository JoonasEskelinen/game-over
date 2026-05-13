using Godot;

public partial class HUDController : CanvasLayer
{
	private ProgressBar _healthBar;
	private ProgressBar _heavyAttackBar;
	private ProgressBar _bossHealthBar;
	private Label _bossBarTitle;
	private Label _r1Label;
	private HBoxContainer _livesRow;
	private PlayerController _player;
	private int _prevLivesForPulse = -1;
	private BossLevel1 _boss1HudSubscribed;
	private int _boss1HudLastCur = int.MinValue;
	private int _boss1HudLastMax;

	// Värit
	private static readonly Color _hpGreen      = new(0.15f, 0.85f, 0.25f, 1f);
	private static readonly Color _hpRed        = new(0.85f, 0.12f, 0.12f, 1f);
	private static readonly Color _hpBg         = new(0.08f, 0.08f, 0.08f, 0.85f);
	private static readonly Color _heavyFill    = new(0.95f, 0.75f, 0.10f, 1f);
	private static readonly Color _heavyBg      = new(0.08f, 0.08f, 0.08f, 0.85f);
	private static readonly Color _bossFill     = new(0.80f, 0.10f, 0.10f, 1f);
	private static readonly Color _bossBg       = new(0.08f, 0.08f, 0.08f, 0.85f);
	private static readonly Color _bossGold     = new(0.95f, 0.70f, 0.05f, 1f);
	private static readonly Color _bossL1TitleCol  = new(0.82f, 0.70f, 0.42f, 1f);
	private static readonly Color _borderColor  = new(0.25f, 0.25f, 0.25f, 1f);
	private static readonly Color _heartFull    = new(0.95f, 0.18f, 0.22f, 1f);
	private static readonly Color _heartEmpty   = new(0.22f, 0.22f, 0.26f, 0.55f);

	public override void _Ready()
	{
		_healthBar      = GetNode<ProgressBar>("HealthBar");
		_heavyAttackBar = GetNodeOrNull<ProgressBar>("HeavyAttackCooldownBar");
		_bossHealthBar  = GetNodeOrNull<ProgressBar>("BossHealthBar");
		_livesRow       = GetNodeOrNull<HBoxContainer>("LivesRow");

		SetupLayout();
		StyleHealthBar();
		StyleHeavyBar();
		StyleBossBar();

		if (_bossHealthBar != null)
			_bossHealthBar.Visible = false;

		_player = GetTree().Root.FindChild("Player", true, false) as PlayerController;
		if (_player == null) { GD.PrintErr("HUDController: Pelaajaa ei löydy!"); return; }

		var hudJoy = new HudJoystickPreview();
		hudJoy.Name = "JoystickHudPreview";
		hudJoy.Initialize(_player);
		AddChild(hudJoy);

		var hc = _player.GetNode<HealthComponent>("HealthComponent");
		hc.HealthChanged += UpdateHealthBar;
		hc.LivesChanged += OnLivesChanged;
		UpdateHealthBar(hc.GetCurrentHealth(), hc.MaxHealth);
		UpdateLivesDisplay(hc.GetCurrentLives(), hc.MaxLives);
		_prevLivesForPulse = hc.GetCurrentLives();
	}

	private void OnLivesChanged(int currentLives, int maxLives)
	{
		bool lostLife = _prevLivesForPulse >= 0 && currentLives < _prevLivesForPulse;
		_prevLivesForPulse = currentLives;
		UpdateLivesDisplay(currentLives, maxLives);
		if (lostLife)
			PlayLifeLostHudPulse();
	}

	/// <summary>Lyhyt “sydämet”-pulssi kun pelaaja menettää elämän.</summary>
	private void PlayLifeLostHudPulse()
	{
		if (_livesRow == null || !IsInsideTree())
			return;

		_livesRow.PivotOffset = _livesRow.Size * 0.5f;
		Vector2 baseScale = _livesRow.Scale;
		if (Mathf.IsZeroApprox(baseScale.X))
			baseScale = Vector2.One;

		var t = CreateTween();
		t.TweenProperty(_livesRow, "scale", baseScale * 1.42f, 0.09f)
			.SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
		t.TweenProperty(_livesRow, "scale", baseScale * 0.88f, 0.07f)
			.SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.In);
		t.TweenProperty(_livesRow, "scale", baseScale, 0.14f)
			.SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
	}

	/// <summary>
	/// Näyttää elämät sydäminä (♥). Myöhemmin voi korvata TextureRect-kuvilla.
	/// </summary>
	private void UpdateLivesDisplay(int currentLives, int maxLives)
	{
		if (_livesRow == null) return;

		while (_livesRow.GetChildCount() > 0)
		{
			Node c = _livesRow.GetChild(0);
			_livesRow.RemoveChild(c);
			c.Free();
		}

		maxLives = Mathf.Max(1, maxLives);
		currentLives = Mathf.Clamp(currentLives, 0, maxLives);

		for (int i = 0; i < maxLives; i++)
		{
			var heart = new Label
			{
				Text = "♥",
			};
			heart.AddThemeFontSizeOverride("font_size", 24);
			heart.AddThemeColorOverride("font_color", i < currentLives ? _heartFull : _heartEmpty);
			_livesRow.AddChild(heart);
		}
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

		// Boss HP — oikea yläkulma: käytä offsetteja (Anchor 1,1 + Position/Size voi antaa 0-leveyden Godot 4:ssa).
		if (_bossHealthBar != null)
		{
			_bossHealthBar.SetAnchorsPreset(Control.LayoutPreset.TopRight);
			_bossHealthBar.AnchorLeft = 1f;
			_bossHealthBar.AnchorRight = 1f;
			_bossHealthBar.AnchorTop = 0f;
			_bossHealthBar.AnchorBottom = 0f;
			_bossHealthBar.GrowHorizontal = Control.GrowDirection.Begin;
			_bossHealthBar.GrowVertical = Control.GrowDirection.Begin;
			_bossHealthBar.OffsetLeft = -240;
			_bossHealthBar.OffsetRight = -20;
			_bossHealthBar.OffsetTop = 22;
			_bossHealthBar.OffsetBottom = 44;
			_bossHealthBar.CustomMinimumSize = new Vector2(220, 22);
			_bossHealthBar.ShowPercentage = false;
			_bossHealthBar.ZIndex = 8;
		}

		_bossBarTitle = new Label
		{
			Text = "BOSS",
			Visible = false,
		};
		_bossBarTitle.SetAnchorsPreset(Control.LayoutPreset.TopRight);
		_bossBarTitle.AnchorLeft = 1f;
		_bossBarTitle.AnchorRight = 1f;
		_bossBarTitle.AnchorTop = 0f;
		_bossBarTitle.AnchorBottom = 0f;
		_bossBarTitle.GrowHorizontal = Control.GrowDirection.Begin;
		_bossBarTitle.GrowVertical = Control.GrowDirection.Begin;
		_bossBarTitle.OffsetLeft = -240;
		_bossBarTitle.OffsetRight = -20;
		_bossBarTitle.OffsetTop = 4;
		_bossBarTitle.OffsetBottom = 20;
		_bossBarTitle.ZIndex = 9;
		_bossBarTitle.HorizontalAlignment = HorizontalAlignment.Right;
		_bossBarTitle.AddThemeFontSizeOverride("font_size", 11);
		_bossBarTitle.AddThemeColorOverride("font_color", _bossL1TitleCol);
		AddChild(_bossBarTitle);
	}

	private void RestoreDefaultBossBarTheme()
	{
		StyleBossBar();
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

		var boss1 = FindActiveBossLevel1(GetTree());
		var boss2 = GetTree().GetFirstNodeInGroup("level2_boss") as BossLevel2;
		var boss3 = GetTree().GetFirstNodeInGroup("level3_boss") as BossLevel3;

		if (boss1 != null && GodotObject.IsInstanceValid(boss1)
			&& boss1.IsInsideTree() && !boss1.IsBossDead)
		{
			_bossHealthBar.Visible = true;
			if (_bossBarTitle != null)
			{
				_bossBarTitle.Text = "Shadow Fang";
				_bossBarTitle.Visible = true;
			}

			EnsureBossLevel1HudSubscription(boss1);
			int cur = boss1.GetBossCurrentHealth();
			int mx = boss1.GetBossMaxHealth();
			if (cur != _boss1HudLastCur || mx != _boss1HudLastMax)
			{
				_boss1HudLastCur = cur;
				_boss1HudLastMax = mx;
				OnBossLevel1HealthChanged(cur, mx);
			}
		}
		else if (boss2 != null && GodotObject.IsInstanceValid(boss2)
			&& boss2.IsInsideTree() && !boss2.IsBossDead)
		{
			DisconnectBossLevel1HudSubscription();
			_boss1HudLastCur = int.MinValue;
			RestoreDefaultBossBarTheme();

			_bossHealthBar.Visible = true;
			if (_bossBarTitle != null)
			{
				_bossBarTitle.Text = "Boss — Level 2";
				_bossBarTitle.Visible = true;
			}
			int maxHp = Mathf.Max(1, boss2.GetBossMaxHealth());
			_bossHealthBar.MaxValue = maxHp;
			_bossHealthBar.Value = Mathf.Clamp(boss2.GetBossCurrentHealth(), 0, maxHp);
		}
		else if (boss3 != null && GodotObject.IsInstanceValid(boss3)
			&& boss3.IsInsideTree() && !boss3.IsBossDead)
		{
			DisconnectBossLevel1HudSubscription();
			_boss1HudLastCur = int.MinValue;
			RestoreDefaultBossBarTheme();

			_bossHealthBar.Visible = true;
			if (_bossBarTitle != null)
			{
				_bossBarTitle.Text = "Boss — Level 3";
				_bossBarTitle.Visible = true;
			}
			int maxHp = Mathf.Max(1, boss3.GetBossMaxHealth());
			_bossHealthBar.MaxValue = maxHp;
			_bossHealthBar.Value = Mathf.Clamp(boss3.GetBossCurrentHealth(), 0, maxHp);
		}
		else
		{
			DisconnectBossLevel1HudSubscription();
			_boss1HudLastCur = int.MinValue;
			_bossHealthBar.Visible = false;
			if (_bossBarTitle != null)
				_bossBarTitle.Visible = false;
		}
	}

	private static BossLevel1 FindActiveBossLevel1(SceneTree tree)
	{
		if (tree == null) return null;
		BossLevel1 found = null;
		foreach (var node in tree.GetNodesInGroup("level1_boss"))
		{
			if (node is BossLevel1 b && GodotObject.IsInstanceValid(b) && b.IsInsideTree()
				&& !b.IsBossDead && !b.IsQueuedForDeletion())
				found = b;
		}

		return found;
	}

	private void EnsureBossLevel1HudSubscription(BossLevel1 boss1)
	{
		if (_boss1HudSubscribed == boss1)
			return;
		DisconnectBossLevel1HudSubscription();
		_boss1HudSubscribed = boss1;
		_boss1HudSubscribed.BossHealthChanged += OnBossLevel1HealthChanged;
		_boss1HudLastCur = int.MinValue;
	}

	private void DisconnectBossLevel1HudSubscription()
	{
		if (_boss1HudSubscribed != null && GodotObject.IsInstanceValid(_boss1HudSubscribed))
			_boss1HudSubscribed.BossHealthChanged -= OnBossLevel1HealthChanged;
		_boss1HudSubscribed = null;
	}

	private void OnBossLevel1HealthChanged(int currentHealth, int maxHealth)
	{
		if (_bossHealthBar == null) return;
		maxHealth = Mathf.Max(1, maxHealth);
		int curHp = Mathf.Clamp(currentHealth, 0, maxHealth);
		_boss1HudLastCur = curHp;
		_boss1HudLastMax = maxHealth;
		ApplyBossLevel1HealthBarLikePlayer(curHp, maxHealth);
	}

	/// <summary>Shadow Fang — sama täyttölogiikka kuin pelaajan HP (0–100, vihreä → punainen ≤33%).</summary>
	private void ApplyBossLevel1HealthBarLikePlayer(int currentHealth, int maxHealth)
	{
		if (_bossHealthBar == null)
			return;
		maxHealth = Mathf.Max(1, maxHealth);
		float pct = (float)currentHealth / maxHealth;
		_bossHealthBar.MaxValue = 100.0;
		_bossHealthBar.MinValue = 0.0;
		_bossHealthBar.Value = pct * 100.0;

		var fill = MakeStyleBox(pct <= 0.34f ? _hpRed : _hpGreen, _borderColor, cornerRadius: 4);
		var bg = MakeStyleBox(_hpBg, _borderColor, cornerRadius: 4);
		var theme = _bossHealthBar.Theme ?? new Theme();
		theme.SetStylebox("fill", "ProgressBar", fill);
		theme.SetStylebox("background", "ProgressBar", bg);
		_bossHealthBar.Theme = theme;
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
