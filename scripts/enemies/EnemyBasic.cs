using Godot;

/// <summary>
/// Vihollinen: törmäys vahingoittaa pelaajaa. Kuolee vain miekka+kilpi -iskulla (osuma: miekan probe vs torso).
/// </summary>
public partial class EnemyBasic : CharacterBody3D
{
	[Export] public float MoveSpeed = 3f;
	[Export] public float Gravity = 24f;
	[Export] public int MaxHealth = 1;
	/// <summary>Vain miekka-tilassa, iskun aikana: etäisyys miekan osumapisteestä vihollisen osumakeskioon.</summary>
	[Export] public float SwordHitRange = 2.15f;
	/// <summary>CollisionShape3D Y-offset (torso); level_1 vihollisella 0.875.</summary>
	[Export] public float HitCenterYOffset = 0.875f;
	[Export] public float MeleeHitCooldown = 0.32f;
	[Export] public int ContactDamage = 1;
	[Export] public float ContactCooldown = 0.75f;
	/// <summary>Varmuus jos slide ei rekisteröi (XZ-keskiöetäisyys).</summary>
	[Export] public float ContactRadiusXZ = 1.15f;
	[Export] public float ContactMaxHeightDiff = 1.65f;

	[Signal] public delegate void DiedEventHandler();

	private int _hp;
	private float _meleeCd;
	private float _contactCd;

	public override void _Ready()
	{
		_hp = MaxHealth;
	}

	public override void _PhysicsProcess(double delta)
	{
		float dt = (float)delta;
		if (_meleeCd > 0f) _meleeCd -= dt;
		if (_contactCd > 0f) _contactCd -= dt;

		var player = GetTree().GetFirstNodeInGroup("player") as PlayerController;
		if (player == null)
			return;

		var v = Velocity;
		if (!IsOnFloor())
			v.Y -= Gravity * dt;
		else
			v.Y = 0f;

		var to = player.GlobalPosition - GlobalPosition;
		to.Y = 0f;
		if (to.LengthSquared() > 0.01f)
		{
			var dir = to.Normalized();
			v.X = dir.X * MoveSpeed;
			v.Z = dir.Z * MoveSpeed;
		}
		else
		{
			v.X = 0f;
			v.Z = 0f;
		}

		Velocity = v;
		MoveAndSlide();

		TryContactDamageFromCollision(player);
		TryContactDamageFromProximity(player);

		Vector3 enemyHitCenter = GlobalPosition + new Vector3(0f, HitCenterYOffset, 0f);
		float dist = enemyHitCenter.DistanceTo(player.GetMeleeHitProbeGlobalPosition());
		if (_meleeCd <= 0f
			&& player.IsSwordWeaponMode()
			&& player.IsMeleeAttackActive()
			&& dist < SwordHitRange)
		{
			TakeDamage(Mathf.Max(1, player.GetMeleeAttackDamage()));
			_meleeCd = MeleeHitCooldown;
		}
	}

	private void TryContactDamageFromCollision(PlayerController player)
	{
		if (_contactCd > 0f)
			return;

		for (int i = 0; i < GetSlideCollisionCount(); i++)
		{
			var col = GetSlideCollision(i);
			if (col.GetCollider() is PlayerController)
			{
				ApplyContactDamageTo(player);
				return;
			}
		}
	}

	private void TryContactDamageFromProximity(PlayerController player)
	{
		if (_contactCd > 0f)
			return;

		float dxz = new Vector2(
			player.GlobalPosition.X - GlobalPosition.X,
			player.GlobalPosition.Z - GlobalPosition.Z).Length();
		float dy = Mathf.Abs(player.GlobalPosition.Y - GlobalPosition.Y);
		if (dxz < ContactRadiusXZ && dy < ContactMaxHeightDiff)
			ApplyContactDamageTo(player);
	}

	private void ApplyContactDamageTo(PlayerController player)
	{
		var hc = player.GetNodeOrNull<HealthComponent>("HealthComponent");
		hc?.TakeDamage(ContactDamage);
		_contactCd = ContactCooldown;
	}

	public void TakeDamage(int amount)
	{
		_hp -= amount;
		if (_hp <= 0)
		{
			EmitSignal(SignalName.Died);
			QueueFree();
		}
	}
}
