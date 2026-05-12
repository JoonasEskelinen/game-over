using Godot;

/// <summary>
/// Joystick GLB voi tulla meshinä ilman tangenteja; normaalikartta + forward+-shader vaativat tangentin.
/// Poistetaan normaalikartta materiaalista (visuaali lähes sama, ei varoitusta).
/// </summary>
public partial class JoystickTangentSafeMaterials : Node
{
	public override void _Ready()
	{
		ApplyDownTree(this);
	}

	private static void ApplyDownTree(Node n)
	{
		foreach (Node c in n.GetChildren())
			ApplyDownTree(c);

		if (n is not MeshInstance3D mi || mi.Mesh == null)
			return;

		int surfaces = mi.Mesh.GetSurfaceCount();
		for (int i = 0; i < surfaces; i++)
		{
			var active = mi.GetActiveMaterial(i);
			if (active is BaseMaterial3D bm && bm.NormalEnabled && bm.NormalTexture != null)
			{
				var dup = (BaseMaterial3D)bm.Duplicate();
				dup.NormalEnabled = false;
				dup.NormalTexture = null;
				mi.SetSurfaceOverrideMaterial(i, dup);
			}
		}
	}
}
