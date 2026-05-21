using Godot;

/// <summary>
/// Kenttäbonus: laatikko (laatikko.glb); miekalla osuessa laatikko katoaa ja ilmestyy kokis.glb.
/// Kokiksen kosketus antaa yhden elämän <see cref="HealthComponent.TryGrantExtraLife"/> jos elämiä on menetetty.
/// Käytössä esim. level_2 ja level_3 (sijainti asetetaan scenessä tien päälle).
/// </summary>
public partial class Level2CrateKokis : Node3D
{
	[Export] public string LaatikkoScenePath = "res://assets/materials/laatikko.glb";
	[Export] public string KokisScenePath = "res://assets/materials/kokis.glb";

	[Export] public float[] SwordHitProbeHeights = { 0.2f, 0.45f, 0.7f };
	[Export] public float SwordHitActivationTime = 0.06f;
	[Export] public float RaskasIskuAktivoitumisenLisäviive = 0.4f;
	[Export] public float RaskasIskuLäheisyysYlikirjoitus = 0.85f;
	[Export] public float RaskasIskuLisäKoetuskorkeus = 1.05f;
	[Export] public float KokisPickupRadius = 0.55f;
	[Export] public float KokisPopScaleTweenSec = 0.22f;

	private Node3D _laatikkoRoot;
	private Node3D _kokisRoot;
	private Area3D _kokisPickup;
	private PlayerController _player;

	private bool _broken;
	private bool _armedForNextSwing = true;

	public override void _Ready()
	{
		_player = GetTree().GetFirstNodeInGroup("player") as PlayerController;

		var laatikkoPacked = GD.Load<PackedScene>(LaatikkoScenePath);
		var kokisPacked = GD.Load<PackedScene>(KokisScenePath);
		if (laatikkoPacked == null)
		{
			GD.PrintErr($"Level2CrateKokis: laatikko puuttuu: {LaatikkoScenePath}");
			return;
		}

		if (kokisPacked == null)
		{
			GD.PrintErr($"Level2CrateKokis: kokis puuttuu: {KokisScenePath}");
			return;
		}

		_laatikkoRoot = EnsureNode3DRoot(laatikkoPacked.Instantiate<Node>(), "LaatikkoRoot");
		AddChild(_laatikkoRoot);

		_kokisRoot = EnsureNode3DRoot(kokisPacked.Instantiate<Node>(), "KokisRoot");
		AddChild(_kokisRoot);
		_kokisRoot.Visible = false;
		DisableCollisionRecursive(_kokisRoot);

		_kokisPickup = new Area3D
		{
			Name = "KokisPickup",
			CollisionLayer = 0,
			CollisionMask = 0xFFFFFFFFu,
			Monitoring = false,
		};
		var sphere = new CollisionShape3D
		{
			Shape = new SphereShape3D { Radius = KokisPickupRadius },
			Position = new Vector3(0f, KokisPickupRadius * 0.35f, 0f),
		};
		_kokisPickup.AddChild(sphere);
		_kokisRoot.AddChild(_kokisPickup);
		_kokisPickup.BodyEntered += OnKokisBodyEntered;

		try
		{
			MeshTangentFix.ApplyToSubtree(_laatikkoRoot);
			MeshTangentFix.ApplyToSubtree(_kokisRoot);
		}
		catch (System.Exception ex)
		{
			GD.PrintErr("Level2CrateKokis MeshTangentFix: " + ex.Message);
		}
	}

	private static Node3D EnsureNode3DRoot(Node inst, string wrapName)
	{
		if (inst is Node3D n3)
			return n3;
		var wrap = new Node3D { Name = wrapName };
		wrap.AddChild(inst);
		return wrap;
	}

	public override void _PhysicsProcess(double delta)
	{
		if (_broken)
			return;

		if (_player == null || !GodotObject.IsInstanceValid(_player) || !_player.IsInsideTree())
			_player = GetTree().GetFirstNodeInGroup("player") as PlayerController;

		if (_player == null)
			return;

		if (!_player.IsMeleeAttackActive())
		{
			_armedForNextSwing = true;
			return;
		}

		if (!_armedForNextSwing)
			return;

		float animTime = _player.GetAttackAnimationTime();
		float hitFrom = _player.GetMeleeStrikeWindowStart() + SwordHitActivationTime;
		if (_player.IsHeavyMeleeAttackActive() && RaskasIskuAktivoitumisenLisäviive > 0f)
			hitFrom += RaskasIskuAktivoitumisenLisäviive;

		if (animTime < hitFrom)
			return;

		bool heavy = _player.IsHeavyMeleeAttackActive();
		var heights = SwordHitProbeHeights;
		if (heights == null || heights.Length == 0)
			heights = new[] { 0.35f };

		int extra = heavy && RaskasIskuLisäKoetuskorkeus > 0.01f ? 1 : 0;
		float proxOverride = heavy && RaskasIskuLäheisyysYlikirjoitus > 0f
			? RaskasIskuLäheisyysYlikirjoitus
			: -1f;

		for (int pi = 0; pi < heights.Length + extra; pi++)
		{
			float h = pi < heights.Length ? heights[pi] : RaskasIskuLisäKoetuskorkeus;
			Vector3 p = GlobalPosition + Vector3.Up * h;
			bool can = proxOverride >= 0f
				? _player.CanApplyMeleeHitAtWorldPoint(p, proxOverride)
				: _player.CanApplyMeleeHitAtWorldPoint(p);
			if (!can)
				continue;

			BreakCrate();
			_player.NotifyMeleeHitLanded();
			_armedForNextSwing = false;
			return;
		}
	}

	private void BreakCrate()
	{
		if (_broken)
			return;
		_broken = true;

		if (_laatikkoRoot != null && GodotObject.IsInstanceValid(_laatikkoRoot))
		{
			DisableCollisionRecursive(_laatikkoRoot);
			_laatikkoRoot.Visible = false;
		}

		if (_kokisRoot == null || !GodotObject.IsInstanceValid(_kokisRoot))
			return;

		_kokisRoot.Visible = true;
		_kokisRoot.Scale = Vector3.One * 0.01f;
		if (_kokisPickup != null)
			_kokisPickup.Monitoring = true;

		Callable.From(TryPickupIfPlayerAlreadyOverlapping).CallDeferred();

		var tw = CreateTween();
		tw.SetIgnoreTimeScale(true);
		tw.TweenProperty(_kokisRoot, "scale", Vector3.One, KokisPopScaleTweenSec)
			.SetTrans(Tween.TransitionType.Back)
			.SetEase(Tween.EaseType.Out);
	}

	private static void DisableCollisionRecursive(Node node)
	{
		if (node is CollisionObject3D co)
		{
			co.CollisionLayer = 0;
			co.CollisionMask = 0;
		}

		foreach (Node ch in node.GetChildren())
			DisableCollisionRecursive(ch);
	}

	private void TryPickupIfPlayerAlreadyOverlapping()
	{
		if (_kokisPickup == null || !GodotObject.IsInstanceValid(_kokisPickup))
			return;
		foreach (var obj in _kokisPickup.GetOverlappingBodies())
		{
			if (obj is PlayerController pc)
			{
				OnKokisBodyEntered(pc);
				break;
			}
		}
	}

	private void OnKokisBodyEntered(Node3D body)
	{
		if (body is not PlayerController pc)
			return;
		var hc = pc.GetNodeOrNull<HealthComponent>("HealthComponent");
		if (hc == null)
			return;
		if (!hc.TryGrantExtraLife())
		{
			GD.Print("Kokis: elämät jo täynnä — kerää myöhemmin tai jätä.");
			return;
		}

		QueueFree();
	}
}
