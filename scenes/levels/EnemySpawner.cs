using Godot;
 
/// <summary>
/// EnemySpawner hallinnoi vihollisten spawnausta level_1:ssä.
/// 
/// TOIMINTALOGIIKKA:
/// 1. Spawner ei käynnisty automaattisesti — odottaa Activate()-kutsua
/// 2. Activate() kutsutaan LeverTrigger.cs:stä kun pelaaja aktivoi vivun
/// 3. Viholliset spawnataan yksi kerrallaan _spawnInterval-välein
/// 4. Kun kaikki viholliset on spawnattu ja tapettu, IsNormalEncounterComplete() palauttaa true
/// 5. Level1BossDirector kuuntelee tätä ja spawnaaa bossin
/// </summary>
public partial class EnemySpawner : Node3D
{
	// ─────────────────────────────────────────────
	// EXPORTATUT MUUTTUJAT — säädettävissä Inspectorissa
	// ─────────────────────────────────────────────
 
	/// <summary>Vihollisen scene-tiedosto (EnemyLevel1.tscn).</summary>
	[Export] public PackedScene EnemyScene;
 
	/// <summary>Vihollisten miniminopeus.</summary>
	[Export] public float MinSpeed = 1.65f;
 
	/// <summary>Vihollisten maksiminopeus.</summary>
	[Export] public float MaxSpeed = 3.75f;
 
	/// <summary>Kuinka monta normaalia vihollista spawnataan ennen bossia.</summary>
	[Export] public int TotalNormalEnemiesToSpawn = 20;

	/// <summary>Enintään näin monta EnemyLevel1:ää kerrallaan elossa — uusia ei luoda ennen kuin joku kuolee.</summary>
	[Export] public int MaxConcurrentEnemyLevel1 = 3;
 
	/// <summary>
	/// HUD-viesti joka näytetään kun pelaaja on vivun lähellä.
	/// Näkyy ruudulla ennen kuin pelaaja on aktivoinut vivun.
	/// </summary>
	[Export] public string LeverHintMessage = "Paina [Neliö] aktivoidaksesi";
 
	// ─────────────────────────────────────────────
	// PRIVAATIT MUUTTUJAT
	// ─────────────────────────────────────────────
 
	/// <summary>
	/// Kiinteät spawnipisteet areenan reunoilla.
	/// Viholliset ilmestyvät näihin pisteisiin satunnaisessa järjestyksessä.
	/// </summary>
	private Vector3[] _spawnPoints = new Vector3[]
	{
		new(13, 0, 0),
		new(-13, 0, 0),
		new(0, 0, 13),
		new(0, 0, -13),
		new(10, 0, 10),
		new(-10, 0, 10),
		new(10, 0, -10),
		new(-10, 0, -10),
	};
 
	/// <summary>Kuinka monta vihollista tässä aallossa spawnataan yhteensä.</summary>
	private int _enemiesInWave = 0;
 
	/// <summary>Kuinka monta vihollista on jo spawnattu.</summary>
	private int _spawned = 0;
 
	/// <summary>Ajastin seuraavaan spawniin.</summary>
	private float _timer = 0f;
 
	/// <summary>Aika sekunteina vihollisten välillä.</summary>
	private float _spawnInterval = 2.2f;
 
	/// <summary>True kun aalto on käynnissä (Activate() on kutsuttu).</summary>
	private bool _waveInProgress = false;
 
	/// <summary>
	/// True kun pelaaja on aktivoinut vivun.
	/// False = spawner odottaa eikä tee mitään.
	/// </summary>
	private bool _activated = false;
 
	// ─────────────────────────────────────────────
	// ALUSTUS
	// ─────────────────────────────────────────────
 
	public override void _Ready()
	{
		// Spawner ei käynnisty automaattisesti — odotetaan vivun aktivointia.
		// Activate() käynnistää spawnauksen kun pelaaja löytää vivun.
		GD.Print("EnemySpawner: odottaa vivun aktivointia.");
	}
 
	// ─────────────────────────────────────────────
	// JULKISET METODIT
	// ─────────────────────────────────────────────
 
	/// <summary>
	/// Kutsutaan LeverTrigger.cs:stä kun pelaaja aktivoi vivun.
	/// Käynnistää vihollisten spawnauksen.
	/// Voidaan kutsua vain kerran — myöhemmät kutsut ohitetaan.
	/// </summary>
	public void Activate()
	{
		// Estetään moninkertainen aktivointi
		if (_activated)
		{
			GD.Print("EnemySpawner: jo aktivoitu, ohitetaan.");
			return;
		}
 
		_activated = true;
		GD.Print("EnemySpawner: vipu aktivoitu — aloitetaan spawnaus!");
		StartLevelSpawns();
		// Ensimmäinen EnemyLevel1 heti aktivointihetkellä (aiemmin vasta ensimmäisen intervallin jälkeen).
		if (_spawned < _enemiesInWave)
		{
			SpawnEnemy();
			_timer = 0f;
		}
	}
 
	/// <summary>
	/// Palauttaa true kun kaikki normaaliviholliset on spawnattu ja tapettu.
	/// Level1BossDirector kutsuu tätä joka framella odottaessaan bossin spawnausta.
	/// </summary>
	public bool IsNormalEncounterComplete()
	{
		// Ei aktivoitu vielä — ei voida olla valmiita
		if (!_activated)
			return false;
 
		// Aalto ei ole käynnissä — ei voida olla valmiita
		if (!_waveInProgress)
			return false;
 
		// Tarkistetaan montako vihollista on vielä hengissä ryhmässä "enemy"
		// Huom: bossi ei ole tässä ryhmässä, joten se ei häiritse laskuria
		int alive = GetTree().GetNodesInGroup("enemy").Count;
 
		// Valmis kun kaikki on spawnattu ja kaikki on tapettu
		return _spawned >= _enemiesInWave && alive == 0;
	}
 
	// ─────────────────────────────────────────────
	// PÄÄSILMUKKA
	// ─────────────────────────────────────────────
 
	public override void _Process(double delta)
	{
		// Ei tehdä mitään ennen aktivointia
		if (!_activated)
			return;
 
		// Spawnataan vihollisia tasaisin väliajoin kunnes aalto on valmis
		if (_spawned < _enemiesInWave)
		{
			_timer += (float)delta;
			if (_timer >= _spawnInterval)
			{
				SpawnEnemy();
				_timer = 0f;
			}
		}
	}
 
	// ─────────────────────────────────────────────
	// PRIVAATIT METODIT
	// ─────────────────────────────────────────────
 
	/// <summary>
	/// Alustaa aallon arvot ja merkitsee aallon käynnissä olevaksi.
	/// Kutsutaan Activate():sta.
	/// </summary>
	private void StartLevelSpawns()
	{
		_spawned = 0;
		_waveInProgress = true;
		_enemiesInWave = Mathf.Max(1, TotalNormalEnemiesToSpawn);
 
		// Spawn-intervalli skaalautuu vihollisten määrän mukaan:
		// vähän vihollisia = pidempi väli, paljon = lyhyempi väli
		_spawnInterval = Mathf.Max(1.2f, 5.0f - _enemiesInWave * 0.12f);
 
		GD.Print($"EnemySpawner: aalto alkaa — {_enemiesInWave} vihollista, intervalli {_spawnInterval:F1}s");
	}
 
	/// <summary>
	/// Luo yhden vihollisen satunnaiseen spawnipisteeseen.
	/// Antaa 20% todennäköisyydellä nopean vihollisen.
	/// </summary>
	private void SpawnEnemy()
	{
		if (EnemyScene == null)
		{
			GD.PrintErr("EnemySpawner: EnemyScene puuttuu Inspectorista!");
			return;
		}

		int cap = Mathf.Max(1, MaxConcurrentEnemyLevel1);
		if (CountAliveEnemyLevel1() >= cap)
			return;
 
		var enemy = EnemyScene.Instantiate() as CharacterBody3D;
		if (enemy == null)
		{
			GD.PrintErr("EnemySpawner: vihollisen instantiointi epäonnistui.");
			return;
		}
 
		// Valitaan satunnainen spawnipiste areenan reunalta
		var point = _spawnPoints[GD.RandRange(0, _spawnPoints.Length - 1)];
 
		// Asetetaan vihollisen nopeus — 20% todennäköisyydellä nopea vihollinen
		if (enemy is EnemyLevel1 script)
		{
			bool fastEnemy = GD.RandRange(0, 4) == 0;
			script.Juoksunopeus = fastEnemy
				? (float)GD.RandRange(MaxSpeed, MaxSpeed * 1.5f)
				: (float)GD.RandRange(MinSpeed, MaxSpeed);

			if (fastEnemy)
				GD.Print($"EnemySpawner: nopea vihollinen! Nopeus: {script.Juoksunopeus:F1}");
		}
 
		// Lisätään ryhmään "enemy" — IsNormalEncounterComplete() laskee tätä ryhmää
		enemy.AddToGroup("enemy");
 
		// GlobalPosition vaatii että node on scene-puussa ensin
		GetParent().AddChild(enemy);
		enemy.GlobalPosition = point;
 
		_spawned++;
		GD.Print($"EnemySpawner: spawnattu {_spawned}/{_enemiesInWave}");
	}

	private int CountAliveEnemyLevel1()
	{
		int n = 0;
		foreach (Node node in GetTree().GetNodesInGroup("enemy"))
		{
			if (node is EnemyLevel1 && GodotObject.IsInstanceValid(node) && node.IsInsideTree())
				n++;
		}
		return n;
	}
}
