using Godot;

// ═══════════════════════════════════════════════════════════════════════════════
// EnemyLevel2Spawner — Level 2:n “aaltojen” hallinta ilman käsin sijoiteltuja vihollisia
// ═══════════════════════════════════════════════════════════════════════════════
// Tarkoitus:
//   • Vihollisia ei ole esimerkitynä sceneen — tämä node luo EnemyLevel2-instansseja
//     dynaamisesti pelaajan edetessä X-akselilla (putki / luola).
//   • “Tasainen väli” = kiinteä etäisyys pelaajan X-koordinaatissa ennen seuraavaa spawnia
//     (SpawnEveryPlayerXMeters), ei reaaliaikainen ajastin (jolloin paikallaan seisoessa
//     tulisi silti vihollisia).
//   • Kapasiteetti: enintään MaxConcurrentEnemies elossa. Jos kentällä on jo täysi,
//     uutta ei synny — kynnys ei siirry eteenpäin, joten seuraava yritys vasta kun
//     joku vihollinen kuolee ja pelaaja on edelleen vähintään kynnyksellä.
//
// Riippuvuudet:
//   • EnemyLevel2 lisää itsensä ryhmään "enemy_level2" ja tarjoaa IsAliveForSpawner().
//   • Spawner sijoittaa instanssit kentän juureen (GetParent()), sama taso kuin pelaaja.
//
// Säädöt: kaikki [Export] — muokkaa level_2.tscn:n EnemyLevel2Spawner-nodea tai oletuksia koodissa.
// ═══════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Spawnaa EnemyLevel2-vihollisia pelaajan X-etenemisen perusteella, max N elossa kerrallaan.
/// </summary>
public partial class EnemyLevel2Spawner : Node3D
{
	// ─── Inspector: mikä scene syntyy ─────────────────────────────────────────────

	/// <summary>Viittaus esim. res://scenes/enemies/enemy_level_2.tscn — asetetaan level-scenessä.</summary>
	[Export] public PackedScene EnemyScene;

	// ─── Inspector: milloin ensimmäinen ja seuraavat spawnit (X-maailma) ────────

	/// <summary>
	/// Pelaajan maailman X josta lähtien ensimmäinen spawn yritetään (kun kapasiteetti riittää).
	/// Esimerkiksi -90: pelaaja alkaa noin -105 → ensimmäinen kynnys kun px ≥ -90.
	/// </summary>
	[Export] public float FirstSpawnAtPlayerX = -90f;

	/// <summary>
	/// Jokaisen onnistuneen spawnin jälkeen kynnys kasvaa tämän verran (metriä).
	/// Pienempi arvo = tiheämmin vihollisia, isompi = harvemmin.
	/// </summary>
	[Export] public float SpawnEveryPlayerXMeters = 12f;

	/// <summary>
	/// Enintään näin monta EnemyLevel2:ta voi olla elossa yhtä aikaa.
	/// Jos täynnä, spawner odottaa kuolemia — kynnys ei hyppää eteenpäin tyhjänä.
	/// </summary>
	[Export] public int MaxConcurrentEnemies = 3;

	/// <summary>Spawner ei tee mitään jos pelaajan X on tämän alapuolella (kentän alku).</summary>
	[Export] public float MinPlayerX = -108f;

	/// <summary>Spawner ei tee mitään jos pelaajan X on tämän yläpuolella (esim. bossin eteen).</summary>
	[Export] public float MaxPlayerX = 168f;

	// ─── Inspector: missä vihollinen ilmestyy suhteessa pelaajaan ────────────────

	/// <summary>
	/// Spawn-pisteen X = pelaajan X + tämä (plus satunnainen varianssi).
	/// Positiivinen = vihollinen syntyy pelaajan “edessä” oikealle (korkeampi X).
	/// </summary>
	[Export] public float SpawnAheadOfPlayer = 10f;

	/// <summary>Satunnainen lisä/vähennys metreinä SpawnAheadOfPlayer:iin (±).</summary>
	[Export] public float SpawnAheadVariance = 3.5f;

	/// <summary>
	/// Putken leveys Z-suunnassa: satunnainen z ∈ [-SpawnZHalfRange, +SpawnZHalfRange].
	/// Putki on noin ±4 m; 2.8 pitää spawnit käytännössä käytävällä.
	/// </summary>
	[Export] public float SpawnZHalfRange = 2.8f;

	/// <summary>Maailman Y-korkeus spawnille (sama kuin lattialla olevilla hahmoilla level_2:ssa).</summary>
	[Export] public float SpawnY = -0.07f;

	// ─── Tila ───────────────────────────────────────────────────────────────────

	private Node3D _player;

	/// <summary>
	/// Seuraava X-kynnys: kun pelaaja.GlobalPosition.X >= tämä (ja muut ehdot täyttyvät),
	/// yritetään yksi spawn ja kynnys += SpawnEveryPlayerXMeters.
	/// </summary>
	private float _nextSpawnThresholdX;

	public override void _Ready()
	{
		_nextSpawnThresholdX = FirstSpawnAtPlayerX;
		_player             = GetTree().GetFirstNodeInGroup("player") as Node3D;
	}

	/// <summary>
	/// Joka frame: päivitä pelaaja, tarkista X-väli, kapasiteetti ja kynnys; spawnaa tarvittaessa.
	/// </summary>
	public override void _Process(double delta)
	{
		if (EnemyScene == null) return;

		if (_player == null || !GodotObject.IsInstanceValid(_player) || !_player.IsInsideTree())
		{
			_player = GetTree().GetFirstNodeInGroup("player") as Node3D;
			return;
		}

		float px = _player.GlobalPosition.X;
		if (px < MinPlayerX || px > MaxPlayerX)
			return;

		// Ei ylimääräisiä spawneja jos jo täysi — kynnys ei kasva (katso silmukka alla)
		if (CountAliveEnemies() >= MaxConcurrentEnemies)
			return;

		// Pelaaja ei ole vielä edennyt seuraavaan “tasoihin” kiinnitettyyn kohtaan
		if (px < _nextSpawnThresholdX)
			return;

		SpawnOneEnemy();
		_nextSpawnThresholdX += SpawnEveryPlayerXMeters;
	}

	/// <summary>
	/// Luo yhden EnemyLevel2:n kentän juureen satunnaisella Z:llä ja X:llä pelaajan edessä.
	/// </summary>
	private void SpawnOneEnemy()
	{
		float px = _player.GlobalPosition.X;
		float spawnX = px + SpawnAheadOfPlayer + (float)GD.RandRange(-SpawnAheadVariance, SpawnAheadVariance);
		float spawnZ = (float)GD.RandRange(-SpawnZHalfRange, SpawnZHalfRange);

		var enemy = EnemyScene.Instantiate<Node3D>();
		GetParent().AddChild(enemy);
		enemy.GlobalPosition = new Vector3(spawnX, SpawnY, spawnZ);

		GD.Print($"EnemyLevel2Spawner: spawn x≈{spawnX:F0} z≈{spawnZ:F1} (pelaaja x={px:F0}).");
	}

	/// <summary>
	/// Laskee ryhmän "enemy_level2" elossa olevat instanssit (IsAliveForSpawner).
	/// </summary>
	private int CountAliveEnemies()
	{
		var tree = GetTree();
		if (tree == null) return 0;

		int c = 0;
		foreach (var n in tree.GetNodesInGroup("enemy_level2"))
		{
			if (!GodotObject.IsInstanceValid(n) || n.IsQueuedForDeletion()) continue;
			if (n is EnemyLevel2 e && e.IsAliveForSpawner())
				c++;
		}
		return c;
	}
}
