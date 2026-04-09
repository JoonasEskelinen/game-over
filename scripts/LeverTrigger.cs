using Godot;
 
/// <summary>
/// LeverTrigger on Area3D-node kentässä.
/// Kun pelaaja astuu alueelle ja painaa "grab" (neliö),
/// EnemySpawner aktivoituu ja viholliset alkavat ilmestyä.
/// </summary>
public partial class LeverTrigger : Area3D
{
	// ─────────────────────────────────────────────
	// EXPORTATUT MUUTTUJAT — säädettävissä Inspectorissa
	// ─────────────────────────────────────────────
 
	/// <summary>Polku EnemySpawner-nodeen. Aseta Inspectorissa.</summary>
	[Export] public NodePath EnemySpawnerPath;
 
	/// <summary>Viesti joka tulostetaan kun pelaaja astuu alueelle.</summary>
	[Export] public string HintMessage = "Paina [Neliö] aktivoidaksesi vipu";
 
	// ─────────────────────────────────────────────
	// PRIVAATIT MUUTTUJAT
	// ─────────────────────────────────────────────
 
	/// <summary>True kun pelaaja on alueen sisällä.</summary>
	private bool _playerInside = false;
 
	/// <summary>True kun vipu on jo aktivoitu — estetään uudelleenaktivointia.</summary>
	private bool _activated = false;
 
	/// <summary>Viittaus EnemySpawneriin.</summary>
	private EnemySpawner _spawner;
 
	/// <summary>Visuaali — vaihdetaan väriä aktivoinnin merkiksi.</summary>
	private MeshInstance3D _mesh;
 
	// ─────────────────────────────────────────────
	// ALUSTUS
	// ─────────────────────────────────────────────
 
	public override void _Ready()
	{
		// Haetaan EnemySpawner
		_spawner = GetNodeOrNull<EnemySpawner>(EnemySpawnerPath);
		if (_spawner == null)
			GD.PrintErr("LeverTrigger: EnemySpawner ei löydy — aseta EnemySpawnerPath Inspectorissa!");
 
		// Haetaan BoxMesh visuaali värinvaihtoa varten
		_mesh = GetNodeOrNull<MeshInstance3D>("MeshInstance3D");
 
		// Kytketään signaalit pelaajan tunnistukseen
		BodyEntered += OnBodyEntered;
		BodyExited  += OnBodyExited;
	}
 
	// ─────────────────────────────────────────────
	// PÄÄSILMUKKA
	// ─────────────────────────────────────────────
 
	public override void _Process(double delta)
	{
		// Ei tehdä mitään jos vipu on jo aktivoitu tai pelaaja ei ole alueella
		if (_activated || !_playerInside)
			return;
 
		// Tarkistetaan painaako pelaaja "grab"-nappia (neliö / E)
		if (Input.IsActionJustPressed("grab"))
		{
			Activate();
		}
	}
 
	// ─────────────────────────────────────────────
	// PRIVAATIT METODIT
	// ─────────────────────────────────────────────
 
	/// <summary>Aktivoi vivun ja käynnistää spawnauksen.</summary>
	private void Activate()
	{
		_activated = true;
 
		// Käynnistetään viholliset
		_spawner?.Activate();
 
		// Vaihdetaan boxin väri vihreäksi merkiksi aktivoinnista
		if (_mesh != null)
		{
			var mat = new StandardMaterial3D();
			mat.AlbedoColor = new Color(0f, 1f, 0f); // vihreä = aktivoitu
			_mesh.MaterialOverride = mat;
		}
 
		GD.Print("LeverTrigger: aktivoitu!");
	}
 
	/// <summary>Kutsutaan kun jokin body astuu alueelle.</summary>
	private void OnBodyEntered(Node3D body)
	{
		if (body is PlayerController)
		{
			_playerInside = true;
			if (!_activated)
				GD.Print(HintMessage);
		}
	}
 
	/// <summary>Kutsutaan kun jokin body poistuu alueelta.</summary>
	private void OnBodyExited(Node3D body)
	{
		if (body is PlayerController)
			_playerInside = false;
	}
}
