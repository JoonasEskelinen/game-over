using Godot;
using Godot.Collections;

/// <summary>
/// Level 2 putki: hämähäkki laskeutuu katolta seittiä pitkin, pysähtyy lattian yläpuolelle ja horjuu.
/// Miekka tappaa heti. Kissan isku: ohjainpalaute heti + hämähäkki putoaa seittiä pitkin lattialle ja jää ruumiiksi.
/// </summary>
public partial class Level2CeilingSpider : CharacterBody3D
{
	[ExportGroup("Liike")]
	[Export] public float Painovoima = 24f;
	[Export] public float HorjutusTaajuus = 2.35f;
	[Export] public float HorjutusVaakaNopeus = 1.55f;

	/// <summary>Raycast lattiaan: hämähäkin juuren minimi-Y = osuma-Y + tämä (metriä). Ei laske lattiaan kiinni.</summary>
	[Export] public float MinKorkeusLattiasta = 0.95f;

	[ExportGroup("Seitti (visuaali)")]
	[Export] public bool SeittiNäkyvissä = true;
	[Export] public float SeittiSäde = 0.014f;
	[Export] public int SeittiSegmentit = 6;

	[ExportGroup("Terveys ja osumat")]
	[Export] public int MaxTerveys = 1;
	[Export] public float OsumaKeskusOffsetY = 0.14f;
	[Export] public int KosketusVahinko = 1;
	[Export] public float KosketusJäähdytys = 0.55f;
	[Export] public float MiekkaOsumaJäähdytys = 0.28f;
	[Export] public float KissaHyökkäysJäähdytys = 0.28f;

	[ExportGroup("Kissan tappo — ruumis + haptinen")]
	[Export(PropertyHint.Range, "0,1,0.05")] public float OhjainTärinäHeikkoKissaTappo = 0.52f;
	[Export(PropertyHint.Range, "0,1,0.05")] public float OhjainTärinäVahvaKissaTappo = 0.72f;
	[Export] public float OhjainTärinäSekuntiaKissaTappo = 0.18f;
	/// <summary>SpiderModel-kallistus (astetta) kun jää maahan kissan tappamana.</summary>
	[Export] public float RuumiinKallistusXAstetta = 88f;

	[ExportGroup("Poistuminen")]
	/// <summary>≤0 = ei aikarajaa seillä roikkumiselle (poistuu vain miekan osumasta tai pelaajakosketuksesta). &gt;0 = QueueFree tämän kuluttua.</summary>
	[Export] public float PoistuSeilläSekunteina = 0f;
	[Export] public float PoistuKunYAlle = -6f;

	[ExportGroup("Malli")]
	[Export] public float Visuaalikoko = 0.085f;

	private int _hp;
	private float _swayPhase;
	private float _meleeCd;
	private float _contactCd;
	private bool _damagedPlayer;
	private bool _hanging;
	private float _hangTimer;
	private float _minHangGlobalY;
	private Vector3 _webAnchorGlobal;
	private MeshInstance3D _silk;
	private CylinderMesh _silkMesh;
	private bool _spawnLayoutDone;

	private bool _catKillCorpse;
	private bool _catKillCorpseFalling;
	private Node3D _spiderModel;

	public override void _Ready()
	{
		_hp = MaxTerveys;
		AddToGroup("level2_ceiling_spider");
		CollisionLayer = 1;
		CollisionMask = 1;
		FloorSnapLength = 0f;
		FloorMaxAngle = Mathf.DegToRad(48f);

		_spiderModel = GetNodeOrNull<Node3D>("SpiderModel");
		if (_spiderModel != null && !Mathf.IsZeroApprox(Visuaalikoko))
			_spiderModel.Scale = Vector3.One * Visuaalikoko;

		if (SeittiNäkyvissä)
			SetupSilkMesh();
	}

	private void EnsureSpawnLayout()
	{
		if (_spawnLayoutDone)
			return;
		_spawnLayoutDone = true;
		_webAnchorGlobal = GlobalPosition;
		_minHangGlobalY = ComputeMinHangY();
	}

	private float ComputeMinHangY()
	{
		var space = GetWorld3D()?.DirectSpaceState;
		if (space == null)
			return GlobalPosition.Y - 2f;

		var from = GlobalPosition + Vector3.Up * 0.35f;
		var to = GlobalPosition + Vector3.Down * 80f;
		var query = PhysicsRayQueryParameters3D.Create(from, to);
		query.CollisionMask = CollisionMask;
		var exclude = new Array<Rid> { GetRid() };
		query.Exclude = exclude;

		var hit = space.IntersectRay(query);
		if (hit.TryGetValue("position", out Variant posVar))
		{
			float floorY = posVar.AsVector3().Y;
			return floorY + MinKorkeusLattiasta;
		}

		return GlobalPosition.Y - 2f;
	}

	private void SetupSilkMesh()
	{
		_silkMesh = new CylinderMesh
		{
			TopRadius = SeittiSäde,
			BottomRadius = SeittiSäde,
			Height = 1f,
			RadialSegments = Mathf.Clamp(SeittiSegmentit, 3, 12),
			Rings = 1,
		};
		_silk = new MeshInstance3D
		{
			Name = "Seitti",
			Mesh = _silkMesh,
			CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
		};
		var mat = new StandardMaterial3D
		{
			AlbedoColor = new Color(0.12f, 0.12f, 0.13f),
			Roughness = 1f,
			Metallic = 0f,
		};
		_silk.MaterialOverride = mat;
		AddChild(_silk);
	}

	public override void _PhysicsProcess(double delta)
	{
		if (!IsInsideTree())
			return;

		EnsureSpawnLayout();

		float dt = (float)delta;

		if (_catKillCorpse)
		{
			ProcessCatKillCorpse(dt);
			return;
		}

		if (_meleeCd > 0f) _meleeCd -= dt;
		if (_contactCd > 0f) _contactCd -= dt;

		var player = GetTree().GetFirstNodeInGroup("player") as PlayerController;
		if (player != null && GodotObject.IsInstanceValid(player) && player.IsInsideTree())
			TryMeleeFromPlayer(player);

		TryMeleeFromSpecialCat();

		_swayPhase += dt;
		float tx = Mathf.Sin(_swayPhase * HorjutusTaajuus) * HorjutusVaakaNopeus;
		float tz = Mathf.Cos(_swayPhase * HorjutusTaajuus * 0.74f) * HorjutusVaakaNopeus * 0.62f;

		Vector3 v = Velocity;
		if (!_hanging)
			v.Y -= Painovoima * dt;
		else
			v.Y = 0f;

		v.X = tx;
		v.Z = tz;
		Velocity = v;
		MoveAndSlide();

		if (!_hanging && GlobalPosition.Y <= _minHangGlobalY)
		{
			GlobalPosition = new Vector3(GlobalPosition.X, _minHangGlobalY, GlobalPosition.Z);
			Velocity = new Vector3(Velocity.X, 0f, Velocity.Z);
			_hanging = true;
			_hangTimer = 0f;
		}

		if (player != null && GodotObject.IsInstanceValid(player) && player.IsInsideTree())
		{
			TryContactDamageFromCollision(player);
			TryContactDamageFromProximity(player);
		}

		if (_hanging && PoistuSeilläSekunteina > 0.001f)
		{
			_hangTimer += dt;
			if (_hangTimer >= PoistuSeilläSekunteina)
				QueueFree();
		}

		if (GlobalPosition.Y < PoistuKunYAlle)
			QueueFree();

		UpdateSilkVisual();
	}

	private void ProcessCatKillCorpse(float dt)
	{
		if (_catKillCorpseFalling)
		{
			Vector3 v = Velocity;
			v.Y -= Painovoima * dt;
			v.X = 0f;
			v.Z = 0f;
			Velocity = v;
			MoveAndSlide();

			if (GlobalPosition.Y <= _minHangGlobalY)
			{
				GlobalPosition = new Vector3(GlobalPosition.X, _minHangGlobalY, GlobalPosition.Z);
				Velocity = Vector3.Zero;
				_catKillCorpseFalling = false;
				_hanging = true;
			}
		}
		else
			Velocity = Vector3.Zero;

		UpdateSilkVisual();
	}

	private void UpdateSilkVisual()
	{
		if (_silk == null || _silkMesh == null || !SeittiNäkyvissä)
			return;

		var spiderPos = GlobalPosition;
		var anchor = _webAnchorGlobal;
		var dir = anchor - spiderPos;
		float len = dir.Length();
		if (len < 0.04f)
		{
			_silk.Visible = false;
			return;
		}

		_silk.Visible = true;
		dir /= len;
		_silkMesh.Height = len;

		var mid = (anchor + spiderPos) * 0.5f;
		Vector3 upRef = Vector3.Up;
		if (Mathf.Abs(dir.Dot(upRef)) > 0.92f)
			upRef = Vector3.Forward;
		Vector3 side = dir.Cross(upRef).Normalized();
		Vector3 binormal = side.Cross(dir).Normalized();
		var basis = new Basis(side, dir, binormal);
		_silk.GlobalTransform = new Transform3D(basis, mid);
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

	private void TryMeleeFromSpecialCat()
	{
		if (_meleeCd > 0f || _hp <= 0)
			return;

		var cat = GetTree().GetFirstNodeInGroup("level2_special_cat") as Level2SpecialCat;
		if (cat == null || !GodotObject.IsInstanceValid(cat) || !cat.IsInsideTree())
			return;
		if (!cat.IsCatMeleeHitWindowActive())
			return;

		Vector3 hitCenter = GlobalPosition + Vector3.Up * OsumaKeskusOffsetY;
		if (!cat.CanCatMeleeHitPoint(hitCenter) && !cat.CanCatMeleeHitCeilingSpiderAt(hitCenter))
			return;
		if (!cat.TryClaimOneCeilingSpiderHitThisAttack())
			return;

		int dmg = Mathf.Max(1, MaxTerveys);
		bool willKill = _hp <= dmg;
		if (willKill)
			BeginCatKillCorpse();
		else
			TakeDamage(dmg);
		_meleeCd = KissaHyökkäysJäähdytys;
	}

	private void BeginCatKillCorpse()
	{
		_hp = 0;
		_catKillCorpse = true;
		RemoveFromGroup("level2_ceiling_spider");

		if (OhjainTärinäSekuntiaKissaTappo > 1e-4f)
			Input.StartJoyVibration(0, OhjainTärinäHeikkoKissaTappo, OhjainTärinäVahvaKissaTappo, OhjainTärinäSekuntiaKissaTappo);

		CollisionLayer = 0;
		CollisionMask = 0;

		if (_spiderModel != null)
			_spiderModel.RotationDegrees = new Vector3(RuumiinKallistusXAstetta, _spiderModel.RotationDegrees.Y, _spiderModel.RotationDegrees.Z);

		if (_hanging || GlobalPosition.Y <= _minHangGlobalY + 0.1f)
		{
			GlobalPosition = new Vector3(GlobalPosition.X, _minHangGlobalY, GlobalPosition.Z);
			Velocity = Vector3.Zero;
			_catKillCorpseFalling = false;
			_hanging = true;
		}
		else
		{
			_hanging = false;
			_catKillCorpseFalling = true;
		}
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
		float reach = Mathf.Max(1f, Visuaalikoko / 0.2f);
		if (dxz < 0.52f * reach && dy < 1.35f * reach)
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
		if (_catKillCorpse)
			return;
		_hp -= amount;
		if (_hp <= 0)
			QueueFree();
	}
}
