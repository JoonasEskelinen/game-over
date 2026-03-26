using Godot;

/// <summary>
/// Kiinnitä GLB-/mallijuureen: generoi normaalit + tangentit alipuun MeshInstance3D:lle (normal map -shaderit).
/// </summary>
public partial class MeshTangentFixOnReady : Node3D
{
	public override void _Ready()
	{
		MeshTangentFix.ApplyToSubtree(this);
	}
}
