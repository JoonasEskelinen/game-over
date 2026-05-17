using Godot;

// ═══════════════════════════════════════════════════════════════════════════════
// EnemyLevel2Spawner — Level 2: PipeA / Cave / PipeB erillisinä osioina
// ═══════════════════════════════════════════════════════════════════════════════
// Tarkoitus:
//   • PipeA (ennen CaveReachedPlayerX): kiintiö PipeASpawnQuota, tasainen väli X:ssä
//     ((CaveReachedPlayerX − FirstSpawnAtPlayerX) / kiintiö).
//   • Cave (CaveSpawnTriggerMin…Max): kiintiö CaveSpawnTotal, useita kerralla
//     (CaveEnemiesPerBurst) kun pelaaja saavuttaa aaltokynnyksen.
//   • PipeB: kiintiö PipeBSpawnQuota, tasainen väli ((PipeBMaxX − PipeBMinX) / kiintiö).
//   • Kapasiteetti: osakohtainen max elossa (PreCave* / Cave* / PipeB*).
//   • Elämän menetys: kuuntelee HealthComponent.PlayerDied — poistaa vanhat liskot ja nollaa kiintiöt
//     (defer, jotta Respawn ehtii siirtää pelaajan ennen kynnysten laskua).
//   • Taaksepäin-hyppy: sama progress + despawn jos iso X-hyppy (AfterRespawnSpawnLeadMeters).
//
// Riippuvuudet:
//   • EnemyLevel2 lisää itsensä ryhmään "enemy_level2" ja tarjoaa IsAliveForSpawner().
//   • Spawner sijoittaa instanssit kentän juureen (GetParent()), sama taso kuin pelaaja.
//
// Säädöt: kaikki [Export] — muokkaa level_2.tscn:n EnemyLevel2Spawner-nodea tai oletuksia koodissa.
// ═══════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Spawnaa EnemyLevel2-vihollisia kentän osien (PipeA, luola, PipeB) mukaan.
/// </summary>
public partial class EnemyLevel2Spawner : Node3D
{
	[Export] public PackedScene EnemyScene;

	[Export] public float FirstSpawnAtPlayerX = -112f;

	[Export] public float MinPlayerX = -200f;
	[Export] public float MaxPlayerX = 168f;

	[Export] public float SpawnAheadOfPlayer = 10f;
	[Export] public float SpawnAheadVariance = 3.5f;
	[Export] public float SpawnZHalfRange = 2.8f;
	[Export] public float SpawnY = -0.07f;

	[Export] public float BacktrackRespawnMeters = 14f;
	[Export] public float AfterRespawnSpawnLeadMeters = 9f;

	// ─── PipeA: px < CaveReachedPlayerX ─────────────────────────────────────────

	[Export] public float CaveReachedPlayerX = 0f;
	[Export] public int PipeASpawnQuota = 5;
	[Export] public int PreCaveMaxConcurrentEnemies = 2;

	[Export] public float PipeAMinX = -118f;
	[Export] public float PipeAMaxX = -3f;

	// ─── Cave: pelaajan X aaltokynnyksissä, syntymä maailman X luolan lattialla ─

	[Export] public float CaveSpawnTriggerMinPlayerX = 0f;
	[Export] public float CaveSpawnTriggerMaxPlayerX = 61f;

	/// <summary>Ensimmäisen luola-aallon kynnys vähintään pelaajan X + tämä (ei “catch-up” respawnissa).</summary>
	[Export] public float CaveFirstBurstLeadFromPlayerMeters = 2.5f;

	[Export] public float CaveSpawnWorldMinX = 2f;
	[Export] public float CaveSpawnWorldMaxX = 57f;

	[Export] public int CaveSpawnTotal = 10;
	[Export] public int CaveEnemiesPerBurst = 3;
	[Export] public int CaveMaxConcurrentEnemies = 5;

	// ─── PipeB: px ≥ PipeBMinX ─────────────────────────────────────────────────

	[Export] public float PipeBMinX = 63f;
	[Export] public float PipeBMaxX = 168f;
	[Export] public int PipeBSpawnQuota = 5;
	[Export] public int PipeBMaxConcurrentEnemies = 2;

	/// <summary>Ensimmäinen PipeB-kynnys vähintään pelaajan X + tämä kun astutaan putkeen.</summary>
	[Export] public float PipeBFirstSpawnLeadFromPlayerMeters = 2f;

	/// <summary>Putken X-poiminta: luolan “aukko” jätetään väliin PipeA/B -satunnaisessa edessä-spawnissa.</summary>
	[Export] public float CaveNoSpawnMinX = -0.5f;
	[Export] public float CaveNoSpawnMaxX = 60.5f;

	private Node3D _player;
	private float _lastPlayerX;
	private bool _haveLastPlayerX;

	private float _nextPipeAThresholdX;
	private int _pipeASpawnsDone;

	private float _nextCaveBurstThresholdX;
	private int _caveSpawnsDone;
	private bool _caveBurstScheduleInitialized;

	private float _nextPipeBThresholdX;
	private int _pipeBSpawnsDone;
	private bool _pipeBThresholdInitialized;

	private HealthComponent _subscribedPlayerHealth;
	private bool _playerDeathSignalConnected;

	private float PipeAStepMeters =>
		Mathf.Max(0.5f, (CaveReachedPlayerX - FirstSpawnAtPlayerX) / Mathf.Max(1, PipeASpawnQuota));

	private float PipeBStepMeters =>
		Mathf.Max(0.5f, (PipeBMaxX - PipeBMinX) / Mathf.Max(1, PipeBSpawnQuota));

	private int CaveBurstWaveCount =>
		Mathf.Max(1, Mathf.CeilToInt(CaveSpawnTotal / (float)Mathf.Max(1, CaveEnemiesPerBurst)));

	private float CaveBurstStepMeters =>
		Mathf.Max(0.5f, (CaveSpawnTriggerMaxPlayerX - CaveSpawnTriggerMinPlayerX) / CaveBurstWaveCount);

	public override void _Ready()
	{
		ResetSpawnProgress();
		_player = GetTree().GetFirstNodeInGroup("player") as Node3D;
		if (_player != null)
		{
			_lastPlayerX = _player.GlobalPosition.X;
			_haveLastPlayerX = true;
		}
	}

	public override void _ExitTree()
	{
		DisconnectPlayerDeathSignal();
		base._ExitTree();
	}

	private void ResetSpawnProgress()
	{
		_nextPipeAThresholdX = FirstSpawnAtPlayerX;
		_pipeASpawnsDone = 0;

		_caveSpawnsDone = 0;
		_caveBurstScheduleInitialized = false;
		_nextCaveBurstThresholdX = CaveSpawnTriggerMinPlayerX;

		_pipeBSpawnsDone = 0;
		_pipeBThresholdInitialized = false;
		_nextPipeBThresholdX = PipeBMinX;
	}

	private bool IsPlayerInCaveTrigger(float px) =>
		px >= CaveSpawnTriggerMinPlayerX && px <= CaveSpawnTriggerMaxPlayerX;

	public override void _Process(double delta)
	{
		if (EnemyScene == null) return;

		if (_player == null || !GodotObject.IsInstanceValid(_player) || !_player.IsInsideTree())
		{
			_player = GetTree().GetFirstNodeInGroup("player") as Node3D;
			if (_player != null && GodotObject.IsInstanceValid(_player) && _player.IsInsideTree())
			{
				_lastPlayerX = _player.GlobalPosition.X;
				_haveLastPlayerX = true;
			}
			return;
		}

		TryConnectPlayerDeathSignal();

		float px = _player.GlobalPosition.X;
		if (px < MinPlayerX || px > MaxPlayerX)
		{
			_lastPlayerX = px;
			_haveLastPlayerX = true;
			return;
		}

		if (_haveLastPlayerX && px < _lastPlayerX - BacktrackRespawnMeters)
			ResyncThresholdAfterPlayerBacktrack(px);

		_lastPlayerX = px;
		_haveLastPlayerX = true;

		if (px < CaveReachedPlayerX)
			TickPipeA(px);
		else if (IsPlayerInCaveTrigger(px))
			TickCaveBursts(px);
		else if (px >= PipeBMinX)
			TickPipeB(px);
	}

	private void ResyncThresholdAfterPlayerBacktrack(float px)
	{
		DespawnAllEnemyLevel2();
		ResetSpawnProgress();
		_nextPipeAThresholdX = Mathf.Max(FirstSpawnAtPlayerX, px + AfterRespawnSpawnLeadMeters);
		GD.Print($"EnemyLevel2Spawner: taaksepäin — progress reset, PipeA kynnys ≥{_nextPipeAThresholdX:F1}.");
	}

	private void TryConnectPlayerDeathSignal()
	{
		if (_playerDeathSignalConnected) return;
		if (_player is not PlayerController pc) return;
		var hc = pc.GetNodeOrNull<HealthComponent>("HealthComponent");
		if (hc == null) return;
		hc.PlayerDied += OnPlayerLifeLostForSpawner;
		_subscribedPlayerHealth = hc;
		_playerDeathSignalConnected = true;
	}

	private void DisconnectPlayerDeathSignal()
	{
		if (!_playerDeathSignalConnected || _subscribedPlayerHealth == null) return;
		if (GodotObject.IsInstanceValid(_subscribedPlayerHealth))
			_subscribedPlayerHealth.PlayerDied -= OnPlayerLifeLostForSpawner;
		_subscribedPlayerHealth = null;
		_playerDeathSignalConnected = false;
	}

	private void OnPlayerLifeLostForSpawner()
	{
		// Defer: HealthComponent emitoi ennen PlayerController.Respawn — odotetaan että sijainti on checkpoint.
		Callable.From(PlayerRespawnResetSpawnState).CallDeferred();
	}

	private void PlayerRespawnResetSpawnState()
	{
		if (!IsInsideTree() || EnemyScene == null) return;

		_player = GetTree().GetFirstNodeInGroup("player") as Node3D;
		DespawnAllEnemyLevel2();
		ResetSpawnProgress();

		if (_player != null && GodotObject.IsInstanceValid(_player) && _player.IsInsideTree())
		{
			float px = _player.GlobalPosition.X;
			_nextPipeAThresholdX = Mathf.Max(FirstSpawnAtPlayerX, px + AfterRespawnSpawnLeadMeters);
			_lastPlayerX = px;
			_haveLastPlayerX = true;
		}

		GD.Print("EnemyLevel2Spawner: elämä meni — liskot poistettu, spawn-kiintiöt nollattu.");
	}

	private void DespawnAllEnemyLevel2()
	{
		var tree = GetTree();
		if (tree == null) return;
		foreach (var n in tree.GetNodesInGroup("enemy_level2"))
		{
			if (!GodotObject.IsInstanceValid(n) || n.IsQueuedForDeletion()) continue;
			n.QueueFree();
		}
	}

	private void EnsureCaveBurstSchedule(float px)
	{
		if (_caveBurstScheduleInitialized) return;
		float step = CaveBurstStepMeters;
		float defaultFirst = CaveSpawnTriggerMinPlayerX + step * 0.35f;
		_nextCaveBurstThresholdX = Mathf.Max(defaultFirst, px + CaveFirstBurstLeadFromPlayerMeters);
		_caveBurstScheduleInitialized = true;
	}

	private void TickPipeA(float px)
	{
		if (_pipeASpawnsDone >= PipeASpawnQuota) return;
		int cap = Mathf.Max(1, PreCaveMaxConcurrentEnemies);
		if (CountAliveEnemies() >= cap) return;
		if (px < _nextPipeAThresholdX) return;

		SpawnPipeEnemy(px, pipeA: true);
		_pipeASpawnsDone++;
		_nextPipeAThresholdX = px + PipeAStepMeters;
	}

	private void TickCaveBursts(float px)
	{
		EnsureCaveBurstSchedule(px);
		if (_caveSpawnsDone >= CaveSpawnTotal) return;

		int cap = Mathf.Max(1, CaveMaxConcurrentEnemies);
		float step = CaveBurstStepMeters;

		if (px < _nextCaveBurstThresholdX) return;

		int remaining = CaveSpawnTotal - _caveSpawnsDone;
		int wantBurst = Mathf.Min(CaveEnemiesPerBurst, remaining);
		int spawnedThisWave = 0;
		for (int i = 0; i < wantBurst; i++)
		{
			if (CountAliveEnemies() >= cap) break;
			SpawnCaveEnemy();
			_caveSpawnsDone++;
			spawnedThisWave++;
		}

		if (spawnedThisWave > 0)
			_nextCaveBurstThresholdX += step;
	}

	private void TickPipeB(float px)
	{
		if (!_pipeBThresholdInitialized)
		{
			float anchor = PipeBMinX + PipeBStepMeters * 0.4f;
			_nextPipeBThresholdX = Mathf.Max(anchor, px + PipeBFirstSpawnLeadFromPlayerMeters);
			_pipeBThresholdInitialized = true;
		}

		if (_pipeBSpawnsDone >= PipeBSpawnQuota) return;
		int cap = Mathf.Max(1, PipeBMaxConcurrentEnemies);
		if (CountAliveEnemies() >= cap) return;
		if (px < _nextPipeBThresholdX) return;

		SpawnPipeEnemy(px, pipeA: false);
		_pipeBSpawnsDone++;
		_nextPipeBThresholdX = px + PipeBStepMeters;
	}

	private void SpawnPipeEnemy(float px, bool pipeA)
	{
		float spawnX = pipeA ? PickSpawnWorldXPipeA(px) : PickSpawnWorldXPipeB(px);
		float spawnZ = (float)GD.RandRange(-SpawnZHalfRange, SpawnZHalfRange);
		InstantiateAt(spawnX, spawnZ, pipeA ? "PipeA" : "PipeB");
	}

	private void SpawnCaveEnemy()
	{
		float xMin = Mathf.Min(CaveSpawnWorldMinX, CaveSpawnWorldMaxX);
		float xMax = Mathf.Max(CaveSpawnWorldMinX, CaveSpawnWorldMaxX);
		float spawnX = (float)GD.RandRange(xMin, xMax);
		float spawnZ = (float)GD.RandRange(-SpawnZHalfRange, SpawnZHalfRange);
		InstantiateAt(spawnX, spawnZ, "Cave");
	}

	private void InstantiateAt(float spawnX, float spawnZ, string tag)
	{
		var enemy = EnemyScene.Instantiate<Node3D>();
		GetParent().AddChild(enemy);
		enemy.GlobalPosition = new Vector3(spawnX, SpawnY, spawnZ);
		GD.Print($"EnemyLevel2Spawner: [{tag}] spawn x≈{spawnX:F0} z≈{spawnZ:F1}.");
	}

	private float PickSpawnWorldXPipeA(float px)
	{
		const float minLead = 2.5f;
		float aheadLo = SpawnAheadOfPlayer - SpawnAheadVariance;
		float aheadHi = SpawnAheadOfPlayer + SpawnAheadVariance;
		float caveMin = Mathf.Min(CaveNoSpawnMinX, CaveNoSpawnMaxX);
		float caveMax = Mathf.Max(CaveNoSpawnMinX, CaveNoSpawnMaxX);
		if (TrySampleSpawnXInPipe(px, PipeAMinX, PipeAMaxX, aheadLo, aheadHi, minLead, caveMin, caveMax, out float ax))
			return ax;
		return Mathf.Clamp(px + SpawnAheadOfPlayer, PipeAMinX, PipeAMaxX);
	}

	private float PickSpawnWorldXPipeB(float px)
	{
		const float minLead = 2.5f;
		float aheadLo = SpawnAheadOfPlayer - SpawnAheadVariance;
		float aheadHi = SpawnAheadOfPlayer + SpawnAheadVariance;
		float caveMin = Mathf.Min(CaveNoSpawnMinX, CaveNoSpawnMaxX);
		float caveMax = Mathf.Max(CaveNoSpawnMinX, CaveNoSpawnMaxX);
		if (TrySampleSpawnXInPipe(px, PipeBMinX, PipeBMaxX, aheadLo, aheadHi, minLead, caveMin, caveMax, out float bx))
			return bx;
		return Mathf.Clamp(px + SpawnAheadOfPlayer, PipeBMinX, PipeBMaxX);
	}

	private static bool TrySampleSpawnXInPipe(float px, float pipeMin, float pipeMax, float aheadLo, float aheadHi,
		float minLead, float caveMin, float caveMax, out float spawnX)
	{
		spawnX = 0f;
		float wantLo = px + Mathf.Min(aheadLo, aheadHi);
		float wantHi = px + Mathf.Max(aheadLo, aheadHi);
		float lo = Mathf.Max(pipeMin, Mathf.Max(wantLo, px + minLead));
		float hi = Mathf.Min(pipeMax, wantHi);
		if (lo > hi)
		{
			lo = Mathf.Max(pipeMin, px + minLead);
			hi = pipeMax;
		}
		if (lo > hi) return false;

		float cLo = Mathf.Min(caveMin, caveMax);
		float cHi = Mathf.Max(caveMin, caveMax);
		return TryRandomXExcludingCave(lo, hi, cLo, cHi, out spawnX);
	}

	private static bool TryRandomXExcludingCave(float lo, float hi, float caveMin, float caveMax, out float spawnX)
	{
		spawnX = 0f;
		const float caveEps = 0.05f;
		if (hi < caveMin || lo > caveMax)
		{
			spawnX = (float)GD.RandRange(lo, hi);
			return true;
		}
		if (lo >= caveMin && hi <= caveMax)
			return false;

		float s1Lo, s1Hi, s2Lo, s2Hi;
		if (lo < caveMin && hi > caveMax)
		{
			s1Lo = lo;
			s1Hi = Mathf.Min(hi, caveMin - caveEps);
			s2Lo = Mathf.Max(lo, caveMax + caveEps);
			s2Hi = hi;
		}
		else if (lo < caveMin)
		{
			s1Lo = lo;
			s1Hi = Mathf.Min(hi, caveMin - caveEps);
			s2Lo = 1f;
			s2Hi = 0f;
		}
		else
		{
			s1Lo = Mathf.Max(lo, caveMax + caveEps);
			s1Hi = hi;
			s2Lo = 1f;
			s2Hi = 0f;
		}

		float w1 = s1Hi >= s1Lo ? s1Hi - s1Lo : -1f;
		float w2 = s2Hi >= s2Lo ? s2Hi - s2Lo : -1f;
		if (w1 < 0f && w2 < 0f) return false;
		if (w2 < 0f)
		{
			spawnX = (float)GD.RandRange(s1Lo, s1Hi);
			return true;
		}
		if (w1 < 0f)
		{
			spawnX = (float)GD.RandRange(s2Lo, s2Hi);
			return true;
		}
		if (GD.Randf() < w1 / (w1 + w2))
			spawnX = (float)GD.RandRange(s1Lo, s1Hi);
		else
			spawnX = (float)GD.RandRange(s2Lo, s2Hi);
		return true;
	}

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
