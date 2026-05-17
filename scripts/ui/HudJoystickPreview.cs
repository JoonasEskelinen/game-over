using System;
using Godot;

/// <summary>
/// HUD: 3D joystick.glb näytön yläosassa erikoisase-tilassa.
/// Näyttö: <see cref="TextureRect"/> + <see cref="ViewportTexture"/> (ei SubViewportContainer) — container voi näyttää vain harmaan joissain setupissa.
/// </summary>
public partial class HudJoystickPreview : Control
{
	private PlayerController _player;
	private SubViewport _viewport;
	private TextureRect _display;
	private Node3D _modelRoot;
	private Node3D _tiltTarget;
	private Camera3D _previewCam;

	[Export] public float ViewportWidth = 200f;
	[Export] public float ViewportHeight = 110f;
	[Export] public float MaxStickTiltDeg = 26f;
	[Export] public float ModelUniformScale = 0.62f;

	[Export] public bool HidePlaneMeshBackdrop = false;
	[Export] public bool ApplyMeshTangentFix = false;

	public void Initialize(PlayerController player) => _player = player;

	public override void _Ready()
	{
		MouseFilter = MouseFilterEnum.Ignore;
		Visible = false;

		AnchorLeft = AnchorRight = 0.5f;
		AnchorTop = 0f;
		AnchorBottom = 0f;
		OffsetLeft = -100f;
		OffsetRight = 100f;
		OffsetTop = 56f;
		OffsetBottom = 56f + ViewportHeight;
		GrowHorizontal = GrowDirection.Both;

		_viewport = new SubViewport
		{
			Name = "HudJoySubViewport",
			RenderTargetUpdateMode = SubViewport.UpdateMode.Always,
			// OwnWorld3D=true: oma erillinen 3D-maailma — ilman tätä viewport jakaa pelimaiseman World3D:n
			// ja kamera näyttää kentän (putken/luolan liitoskohta world-origon lähellä).
			OwnWorld3D = true,
			// TransparentBg=true: läpinäkyvä tausta — joystick kelluu HUD:issa ilman taustaruutua.
			TransparentBg = true,
			HandleInputLocally = false,
			Size = new Vector2I(Mathf.Max(32, (int)ViewportWidth), Mathf.Max(32, (int)ViewportHeight)),
			Msaa3D = Viewport.Msaa.Disabled,
		};
		AddChild(_viewport);

		// World3D / ympäristö vasta kun SubViewport on puussa — muuten World3D voi olla null (NRE).
		Callable.From(SetupSubViewportEnvironmentDeferred).CallDeferred();

		_modelRoot = new Node3D { Name = "HudJoystickModelRoot" };
		_viewport.AddChild(_modelRoot);

		var packed = GD.Load<PackedScene>("res://assets/models/joystick/joystick.glb");
		if (packed == null)
		{
			GD.PrintErr("HudJoystickPreview: joystick.glb puuttuu.");
			return;
		}

		var inst = packed.Instantiate<Node>();
		_modelRoot.AddChild(inst);
		_modelRoot.Scale = Vector3.One * ModelUniformScale;

		_tiltTarget = FindStickPivotNode(inst) ?? _modelRoot;

		if (HidePlaneMeshBackdrop)
		{
			var stickPivot = FindStickPivotNode(inst);
			if (stickPivot != null)
				HideBackdropPlaneMeshes(inst, stickPivot);
			else
				GD.PrintErr("HudJoystickPreview: StickPivot ei löytynyt — PlaneMesh-taustaa ei piiloteta.");
		}

		// Aina generoidaan tangentit GLB-meshille: normaalikartta vaatii ne, ilman niitä malli voi renderöityä mustana.
		try
		{
			MeshTangentFix.ApplyToSubtree(inst);
		}
		catch (System.Exception ex)
		{
			GD.PrintErr("HudJoystickPreview MeshTangentFix: " + ex.Message);
		}

		_previewCam = new Camera3D
		{
			Current = true,
			Fov = 40f,
			Near = 0.02f,
			Far = 50f,
			CullMask = (uint)((1 << 20) - 1),
		};
		_viewport.AddChild(_previewCam);

		var sun = new DirectionalLight3D
		{
			RotationDegrees = new Vector3(-52f, 32f, 8f),
			ShadowEnabled = false,
			LightEnergy = 1.25f,
		};
		_viewport.AddChild(sun);

		var fill = new OmniLight3D
		{
			Position = new Vector3(-0.25f, 0.35f, 0.15f),
			LightEnergy = 0.55f,
			ShadowEnabled = false,
		};
		_viewport.AddChild(fill);

		_display = new TextureRect
		{
			Name = "HudJoyDisplay",
			MouseFilter = MouseFilterEnum.Ignore,
			StretchMode = TextureRect.StretchModeEnum.Scale,
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			CustomMinimumSize = new Vector2(ViewportWidth, ViewportHeight),
		};
		_display.SetAnchorsPreset(LayoutPreset.FullRect);
		_display.OffsetLeft = _display.OffsetTop = _display.OffsetRight = _display.OffsetBottom = 0f;
		AddChild(_display);

		// ViewportTexture vaatii että molemmat ovat puussa; polku suhteessa TextureRectiin.
		Callable.From(BindViewportTextureDeferred).CallDeferred();

		_display.Resized += OnDisplayResized;
		Callable.From(SyncViewportSizeToDisplay).CallDeferred();

		Callable.From(DeferredFitCameraToModel).CallDeferred();
	}

	private void SetupSubViewportEnvironmentDeferred()
	{
		if (!GodotObject.IsInstanceValid(_viewport) || !_viewport.IsInsideTree())
			return;
		var w3d = _viewport.World3D;
		if (w3d == null)
		{
			GD.PrintErr("HudJoystickPreview: SubViewport.World3D puuttuu — HUD-joystickin valaistus voi puuttua.");
			return;
		}

		// TransparentBg=true: taustaa ei tarvita. Asetetaan vain ambient-valo jotta mallin
		// varjopuolet eivät ole täysin mustia (läpinäkyvän taustan kanssa ne erottuisivat rumasti).
		if (w3d.Environment == null)
			w3d.Environment = new Godot.Environment();
		w3d.Environment.BackgroundMode = Godot.Environment.BGMode.Color;
		w3d.Environment.BackgroundColor = new Color(0f, 0f, 0f, 0f);
		w3d.Environment.AmbientLightSource = Godot.Environment.AmbientSource.Color;
		w3d.Environment.AmbientLightColor = new Color(0.6f, 0.6f, 0.65f, 1f);
		w3d.Environment.AmbientLightEnergy = 0.5f;
	}

	private void BindViewportTextureDeferred()
	{
		if (!GodotObject.IsInstanceValid(_display) || !GodotObject.IsInstanceValid(_viewport)
			|| !_display.IsInsideTree() || !_viewport.IsInsideTree())
			return;

		// GetTexture() on suora viite SubViewportin renderteksturiin — luotettavampi kuin
		// ViewportTexture + NodePath, joka vaatii polun suhteessa scene-juureen eikä _displaystä.
		_display.Texture = _viewport.GetTexture();
	}

	private void OnDisplayResized() => SyncViewportSizeToDisplay();

	private void SyncViewportSizeToDisplay()
	{
		if (!GodotObject.IsInstanceValid(_viewport) || !GodotObject.IsInstanceValid(_display))
			return;
		var sz = (Vector2I)(_display.Size.Round());
		sz.X = Mathf.Max(2, sz.X);
		sz.Y = Mathf.Max(2, sz.Y);
		_viewport.Size = sz;
	}

	private void DeferredFitCameraToModel()
	{
		if (!GodotObject.IsInstanceValid(_modelRoot) || !GodotObject.IsInstanceValid(_previewCam))
			return;

		_modelRoot.ForceUpdateTransform();
		if (_previewCam.IsInsideTree())
			_previewCam.MakeCurrent();

		if (!TryUnionVisualAabb(_modelRoot, out Aabb aabb))
		{
			GD.PrintErr("HudJoystickPreview: ei GeometryInstance3D AABB:ä — käytetään oletuskameraa.");
			_previewCam.Position = new Vector3(0f, 0.11f, 0.52f);
			_previewCam.LookAt(new Vector3(0f, 0.035f, 0f), Vector3.Up);
			_previewCam.MakeCurrent();
			return;
		}

		Vector3 c = aabb.GetCenter();
		float extent = aabb.Size.Length() * 0.5f;
		float dist = Mathf.Clamp(extent * 2.6f, 0.18f, 3.5f);
		_previewCam.Position = c + new Vector3(0f, extent * 0.25f, dist);
		_previewCam.LookAt(c, Vector3.Up);
		_previewCam.MakeCurrent();
	}

	private static bool TryUnionVisualAabb(Node root, out Aabb worldAabb)
	{
		worldAabb = default;
		bool has = false;
		AccumulateGeometryAabb(root, ref worldAabb, ref has);
		return has;
	}

	private static void AccumulateGeometryAabb(Node node, ref Aabb acc, ref bool hasAny)
	{
		foreach (Node ch in node.GetChildren())
			AccumulateGeometryAabb(ch, ref acc, ref hasAny);

		if (node is not GeometryInstance3D geo || !geo.Visible)
			return;

		var local = geo.GetAabb();
		if (local.Size.LengthSquared() < 1e-14f)
			return;

		var xf = geo.GlobalTransform;
		for (int i = 0; i < 8; i++)
		{
			var corner = local.Position + new Vector3(
				(i & 1) != 0 ? local.Size.X : 0f,
				(i & 2) != 0 ? local.Size.Y : 0f,
				(i & 4) != 0 ? local.Size.Z : 0f);
			var w = xf.Basis * corner + xf.Origin;
			if (!hasAny)
			{
				acc = new Aabb(w, Vector3.Zero);
				hasAny = true;
			}
			else
				acc = acc.Expand(w);
		}
	}

	private static void HideBackdropPlaneMeshes(Node node, Node stickPivot)
	{
		foreach (Node child in node.GetChildren())
		{
			if (child is MeshInstance3D mi && mi.Mesh is PlaneMesh)
			{
				bool underStickPivot = stickPivot != null && stickPivot.IsAncestorOf(mi);
				if (!underStickPivot)
					mi.Visible = false;
			}

			HideBackdropPlaneMeshes(child, stickPivot);
		}
	}

	private static Node3D FindStickPivotNode(Node node)
	{
		if (node == null) return null;
		if (node is Node3D n3 && NameContains(node.Name, "StickPivot"))
			return n3;
		foreach (Node ch in node.GetChildren())
		{
			var found = FindStickPivotNode(ch);
			if (found != null)
				return found;
		}
		return null;
	}

	private static bool NameContains(StringName name, string part)
		=> name.ToString().IndexOf(part, StringComparison.OrdinalIgnoreCase) >= 0;

	public override void _Process(double delta)
	{
		if (_player == null || !GodotObject.IsInstanceValid(_player) || !_player.IsInsideTree())
		{
			Visible = false;
			return;
		}

		bool on = _player.IsSpecialWeaponJoystickContextActive();
		Visible = on;
		if (!on || _tiltTarget == null)
			return;

		Vector2 stick = _player.GetSpecialWeaponStickVector();
		float rx = -stick.X * MaxStickTiltDeg;
		float ry = stick.Y * MaxStickTiltDeg;
		_tiltTarget.RotationDegrees = new Vector3(ry, 0f, rx);
	}
}
