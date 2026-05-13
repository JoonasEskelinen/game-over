using Godot;

/// <summary>
/// Level 2 putki: putoava hämähäkki katolta, horjuva XZ-liike. Osuessaan pelaajaan vahingoittaa kerran ja katoaa.
/// Miekka+kilpi -isku tappaa (1 HP).
/// </summary>
public partial class Level2CeilingSpider : CharacterBody3D
{
	[ExportGroup("Liike")]
	[Export] public float Painovoima = 24f;
	[Export] public float HorjutusTaajuus = 2.35f;
	[Export] public float HorjutusVaakaNopeus = 1.55f;

	[ExportGroup("Terveys ja osumat")]
	[Export] public int MaxTerveys = 1;
	[Export] public float OsumaKeskusOffsetY = 0.14f;
	[Export] public int KosketusVahinko = 1;
	[Export] public float KosketusJäähdytys = 0.55f;
	[Export] public float MiekkaOsumaJäähdytys = 0.28f;

	[ExportGroup("Poistuminen")]
	[Export] public float PoistuMaassaSekunteina = 6f;
	[Export] public float PoistuKunYAlle = -6f;

	[ExportGroup("Malli")]
	[Export] public float Visuaalikoko = 0.085f;

	private int _hp;
	private float _swayPhase;
	private float _meleeCd;
	private float _contactCd;
	private bool _damagedPlayer;
	private bool _landed;
	private float _landedTimer;

	public override void _Ready()
	{
		_hp = MaxTerveys;
		AddToGroup("level2_ceiling_spider");
		CollisionLayer = 1;
		CollisionMask = 1;
		FloorSnapLength = 0.12f;
		FloorMaxAngle = Mathf.DegToRad(48f);

		var holder = GetNodeOrNull<Node3D>("SpiderModel");
		if (holder != null && !Mathf.IsZeroApprox(Visuaalikoko))
			holder.Scale = Vector3.One * Visuaalikoko;
	}

	public override void _PhysicsProcess(double delta)
	{
		if (!IsInsideTree())
			return;

		float dt = (float)delta;
		if (_meleeCd > 0f) _meleeCd -= dt;
		if (_contactCd > 0f) _contactCd -= dt;

		var player = GetTree().GetFirstNodeInGroup("player") as PlayerController;
		if (player != null && GodotObject.IsInstanceValid(player) && player.IsInsideTree())
			TryMeleeFromPlayer(player);

		_swayPhase += dt;
		float tx = Mathf.Sin(_swayPhase * HorjutusTaajuus) * HorjutusVaakaNopeus;
		float tz = Mathf.Cos(_swayPhase * HorjutusTaajuus * 0.74f) * HorjutusVaakaNopeus * 0.62f;

		Vector3 v = Velocity;
		v.Y -= Painovoima * dt;
		if (!IsOnFloor())
		{
			v.X = tx;
			v.Z = tz;
		}
		else
		{
			v.X = 0f;
			v.Z = 0f;
			if (v.Y < 0f)
				v.Y = 0f;
		}
		Velocity = v;
		MoveAndSlide();

		if (player != null && GodotObject.IsInstanceValid(player) && player.IsInsideTree())
		{
			TryContactDamageFromCollision(player);
			TryContactDamageFromProximity(player);
		}

		if (IsOnFloor() && !_landed)
		{
			_landed = true;
			_landedTimer = 0f;
		}

		if (_landed)
		{
			_landedTimer += dt;
			if (_landedTimer >= PoistuMaassaSekunteina)
				QueueFree();
		}

		if (GlobalPosition.Y < PoistuKunYAlle)
			QueueFree();
	}

	private void TryMeleeFromPlayer(PlayerController player)
	{
		if (_meleeCd > 0f || _hp <= 0)
			return;
		if (!player.IsSwordWeaponMode() || !player.IsMeleeAttackActive())
			return;

		Vector3 hitCenter = GlobalPosition + Vector3.Up * OsumaKeskusOffsetY;
		if (!player.CanApplyMeleeHitAtWorldPoint(hitCenter))
			return;

		TakeDamage(Mathf.Max(1, player.GetMeleeAttackDamage()));
		player.NotifyMeleeHitLanded();
		_meleeCd = MiekkaOsumaJäähdytys;
	}

	private void TryContactDamageFromCollision(PlayerController player)
	{
		if (_damagedPlayer || _contactCd > 0f)
			return;

		for (int i = 0; i < GetSlideCollisionCount(); i++)
		{
			if (GetSlideCollision(i).GetCollider() is PlayerController)
			{
				ApplyContactDamage(player);
				return;
			}
		}
	}

	private void TryContactDamageFromProximity(PlayerController player)
	{
		if (_damagedPlayer || _contactCd > 0f)
			return;

		float dxz = new Vector2(
			player.GlobalPosition.X - GlobalPosition.X,
			player.GlobalPosition.Z - GlobalPosition.Z).Length();
		float dy = Mathf.Abs(player.GlobalPosition.Y - GlobalPosition.Y);
		if (dxz < 0.52f && dy < 1.35f)
			ApplyContactDamage(player);
	}

	private void ApplyContactDamage(PlayerController player)
	{
		var hc = player.GetNodeOrNull<HealthComponent>("HealthComponent");
		if (hc == null)
			return;
		hc.TakeDamage(Mathf.Max(1, KosketusVahinko));
		player.NotifyEnemyEnergyDrainHit();
		_damagedPlayer = true;
		_contactCd = KosketusJäähdytys;
		QueueFree();
	}

	public void TakeDamage(int amount)
	{
		_hp -= amount;
		if (_hp <= 0)
			QueueFree();
	}
}
