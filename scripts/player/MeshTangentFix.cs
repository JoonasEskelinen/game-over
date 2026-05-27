using System;
using Godot;

/// <summary>
/// GLB:t voivat tulla ilman normaaleja/tangentteja; StandardMaterial3D + normal map vaatii molemmat.
/// Järjestys: GenerateNormals → GenerateTangents (muuten Godot valittaa ARRAY_FORMAT_NORMAL / tangents).
/// </summary>
public static class MeshTangentFix
{
	public static void ApplyToSubtree(Node root, Func<Node, bool> skipNode = null)
	{
		if (root == null) return;
		FixRecursive(root, skipNode);
	}

	private static void FixRecursive(Node node, Func<Node, bool> skipNode)
	{
		if (skipNode != null && skipNode(node))
			return;

		if (node is MeshInstance3D mi && mi.Mesh != null)
		{
			if (mi.Skeleton != null || mi.GetSkin() != null)
			{
				foreach (Node c in node.GetChildren())
					FixRecursive(c, skipNode);
				return;
			}

			try
			{
				mi.Mesh = EnsureMeshAttributes(mi.Mesh);
			}
			catch (Exception ex)
			{
				GD.PrintErr($"MeshTangentFix ({mi.GetPath()}): {ex.Message}");
			}
		}

		foreach (Node c in node.GetChildren())
			FixRecursive(c, skipNode);
	}

	public static Mesh EnsureMeshAttributes(Mesh mesh)
	{
		if (mesh is not ArrayMesh am || am.GetSurfaceCount() == 0)
			return mesh;

		var result = new ArrayMesh();
		for (int i = 0; i < am.GetSurfaceCount(); i++)
		{
			var st = new SurfaceTool();
			st.CreateFrom(am, i);
			st.GenerateNormals();
			st.GenerateTangents();
			st.Commit(result);
		}

		return result;
	}
}
