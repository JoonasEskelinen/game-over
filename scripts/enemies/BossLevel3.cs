using Godot;

/// <summary>
/// Level 3 tornibossi: visuaali GLB:nä, HP ja <see cref="HurtArea"/> dronen pommeille (kerros 14).
/// </summary>
public partial class BossLevel3 : Node3D
{
	public const uint DroneBombPhysicsLayer = 1u << 13;

	[Export] public int BossTerveysAlussa = 28;
	[Export] public int PommiVahinkoPerOsuma = 7;
	[Export] public string BossVahinkoÄäniPolku = "res://assets/audio/sfx/enemybosshit.mp3";

	private int _hp;
	private bool _dead;
	private Area3D _hurt;
	private Node3D _visual;

	public bool IsBossDead => _dead;

	public int GetBossMaxHealth() => Mathf.Max(1, BossTerveysAlussa);
	public int GetBossCurrentHealth() => Mathf.Clamp(_hp, 0, BossTerveysAlussa);

	public override void _Ready()
	{
		AddToGroup("level3_boss");
		_hp = BossTerveysAlussa;
		_hurt = GetNodeOrNull<Area3D>("HurtArea");
		_visual = GetNodeOrNull<Node3D>("Visual");
		if (_hurt != null)
			_hurt.BodyEntered += OnHurtBodyEntered;
	}

	private void OnHurtBodyEntered(Node3D body)
	{
		if (_dead || body == null)
			return;
		if (body is not Level3DroneBomb bomb)
			return;
		if (!bomb.TryConsumeBossHit())
			return;
		ApplyDamage(Mathf.Max(1, bomb.BossDamage > 0 ? bomb.BossDamage : PommiVahinkoPerOsuma));
	}

	private void ApplyDamage(int amount)
	{
		if (_dead)
			return;
		_hp -= amount;
		PlayHitSound();
		GD.Print($"BossLevel3 HP: {_hp} / {BossTerveysAlussa}");
		if (_hp <= 0)
			Die();
	}

	private void PlayHitSound()
	{
		if (string.IsNullOrEmpty(BossVahinkoÄäniPolku))
			return;
		var p = new AudioStreamPlayer3D
		{
			Stream = GD.Load<AudioStream>(BossVahinkoÄäniPolku),
			MaxDistance = 80f,
			UnitSize = 4f,
		};
		AddChild(p);
		p.Finished += () => p.QueueFree();
		p.Play();
	}

	private void Die()
	{
		if (_dead)
			return;
		_dead = true;
		if (_hurt != null)
		{
			_hurt.BodyEntered -= OnHurtBodyEntered;
			// Ei suoraan Monitoring=false BodyEntered-ketjussa / signaalin aikana (Godot-varoitus).
			_hurt.SetDeferred(Area3D.PropertyName.Monitoring, false);
		}

		if (_visual != null)
			_visual.Visible = false;

		var thrower = GetParent() as Level3TowerBossRockThrower;
		thrower?.StopThrowing();

		GD.Print("BossLevel3: kuollut — heitot pysähtyvät.");
	}
}
