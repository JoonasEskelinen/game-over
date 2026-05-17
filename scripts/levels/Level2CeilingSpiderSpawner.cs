using Godot;

/// <summary>
/// Synnyttää putoavia hämähäkkejä level_2 putkiin: yksi kerrallaan per putki (max-katto),
/// Z putken keskilinjaa ja pelaajan kulkureittiä kohti (+pieni bias kameraan päin).
/// </summary>
public partial class Level2CeilingSpiderSpawner : Node3D
{
	[ExportGroup("Viittaukset")]
	[Export] public PackedScene SpiderScene;
	[Export] public NodePath PlayerPath = new("../Player");

	[ExportGroup("Synnytys")]
	[Export] public float SynnytysVäliMinSekuntia = 4.2f;
	[Export] public float SynnytysVäliMaxSekuntia = 5.5f;
	/// <summary>Lisätään vain ensimmäiseen ajastimeen (kentän alku / putkeen tulo) — ei ensimmäistä hämähäkkiä heti pelaajan niskaan.</summary>
	[Export] public float EnsimmäisenSpiderinLisäAlkuviiveSekunteina = 5f;
	/// <summary>Putoamis-X vähintään näin monta metriä pelaajan X:n oikealle (+X). Estää spawnin suoraan aloituspaikan ylle.</summary>
	[Export] public float VähintäänMetriXPelaajanEteen = 8.5f;
	[Export] public float SynnytysXSatunnainenMetri = 16f;
	/// <summary>0 = vain pelaajan X:n ympärillä. 1 = usein putken pituuden keskipisteen lähellä (± <see cref="SynnytysXPutkenKeskelleHajonta"/>).</summary>
	[Export] public float SynnytysXPutkenKeskelleSekoitus = 0.28f;
	[Export] public float SynnytysXPutkenKeskelleHajonta = 20f;

	/// <summary>0 = putken Z-keskilinja, 1 = täysi pelaajan Z (kulkukäytävä keskellä putkea).</summary>
	[Export] public float SynnytysZTavoitePelaajanZSekoitus = 0.72f;
	/// <summary>Satunnainen Z ± tämän ympärillä tavoite-Z:tä (kapea kaista, ei seiniin).</summary>
	[Export] public float SynnytysKulkukäytäväZSatunnainenPuolileveys = 0.48f;
	/// <summary>Lisätään Z:hen (+Z ≈ lähemmäs kameraa kun CameraFollow.Offset.z &gt; 0).</summary>
	[Export] public float SynnytysZKohtiKameraaOffsetMetri = 0.32f;

	[ExportGroup("Putki (maailmakoordinaatit)")]
	[Export] public float PutkiZPuolileveys = 3.35f;
	[Export] public float PutkiMinX = -119f;
	[Export] public float PutkiMaxX = -0.5f;
	/// <summary>Toinen putki (PipeB). Jos Max ≤ Min, toista aluetta ei käytetä.</summary>
	[Export] public float Putki2MinX = 57f;
	[Export] public float Putki2MaxX = 180f;
	[Export] public float KattoKorkeusY = 3.48f;
	[Export] public float UlkonaPutkessaYritäUudelleenSekuntia = 2.5f;
	/// <summary>Kuinka monta hämähäkkiä sallitaan samassa putkessa (X-väli + marginaali). Ei globaalia — muuten A:n roikkujat estävät B:n spawnit.</summary>
	[Export] public int MaxHämähäkkejäPutkeaKohti = 1;
	/// <summary>Metriä X-suunnassa segmentin ulkopuolelle: lasketaan mukaan putkeen kuuluvaksi (laskeutuva / horjunut).</summary>
	[Export] public float SpideriLaskentaXMarginaali = 6f;

	private float _nextSpawnIn;
	private PlayerController _player;

	public override void _Ready()
	{
		if (SpiderScene == null)
			SpiderScene = GD.Load<PackedScene>("res://scenes/hazards/level2_ceiling_spider.tscn");
		_nextSpawnIn = (float)GD.RandRange(SynnytysVäliMinSekuntia, SynnytysVäliMaxSekuntia)
			+ EnsimmäisenSpiderinLisäAlkuviiveSekunteina;
		TryCachePlayer();
	}

	public override void _Process(double delta)
	{
		if (SpiderScene == null)
			return;

		TryCachePlayer();
		if (_player == null || !GodotObject.IsInstanceValid(_player) || !_player.IsInsideTree())
			return;

		float px = _player.GlobalPosition.X;
		if (!TryGetActivePipeSegment(px, out float segMinX, out float segMaxX))
		{
			_nextSpawnIn = UlkonaPutkessaYritäUudelleenSekuntia;
			return;
		}

		_nextSpawnIn -= (float)delta;
		if (_nextSpawnIn > 0f)
			return;

		if (CountLiveSpidersInPipeSegment(segMinX, segMaxX) >= MaxHämähäkkejäPutkeaKohti)
		{
			_nextSpawnIn = 0.42f;
			return;
		}

		float pipeMidX = (segMinX + segMaxX) * 0.5f;
		float xNearPlayer = px + (float)GD.RandRange(-SynnytysXSatunnainenMetri, SynnytysXSatunnainenMetri);
		float xNearMid = pipeMidX + (float)GD.RandRange(-SynnytysXPutkenKeskelleHajonta, SynnytysXPutkenKeskelleHajonta);
		float xMix = Mathf.Clamp(SynnytysXPutkenKeskelleSekoitus, 0f, 1f);
		float spawnX = Mathf.Lerp(xNearPlayer, xNearMid, (float)GD.Randf() * xMix);
		spawnX = Mathf.Clamp(spawnX, segMinX, segMaxX);
		// Aina vähintään edessä päin (+X), jotta ensimmäinenkään ei tipu aloituspaikan suoraan ylle.
		spawnX = Mathf.Max(spawnX, px + VähintäänMetriXPelaajanEteen);
		spawnX = Mathf.Clamp(spawnX, segMinX, segMaxX);

		float pz = _player.GlobalPosition.Z;
		float zTarget = Mathf.Lerp(0f, pz, Mathf.Clamp(SynnytysZTavoitePelaajanZSekoitus, 0f, 1f));
		float zHalf = Mathf.Clamp(SynnytysKulkukäytäväZSatunnainenPuolileveys, 0.1f, PutkiZPuolileveys - 0.25f);
		float zCap = PutkiZPuolileveys - 0.2f;
		float spawnZ = Mathf.Clamp(
			zTarget + SynnytysZKohtiKameraaOffsetMetri + (float)GD.RandRange(-zHalf, zHalf),
			-zCap,
			zCap);

		var spider = SpiderScene.Instantiate<Node3D>();
		GetParent().AddChild(spider);
		spider.GlobalPosition = new Vector3(spawnX, KattoKorkeusY, spawnZ);

		_nextSpawnIn = (float)GD.RandRange(SynnytysVäliMinSekuntia, SynnytysVäliMaxSekuntia);
	}

	/// <summary>
	/// Palauttaa true jos pelaaja on jommassakummassa putkessa; asettaa aktiivisen X-välin maailmakoordinaateissa.
	/// </summary>
	private bool TryGetActivePipeSegment(float playerX, out float segMinX, out float segMaxX)
	{
		segMinX = PutkiMinX;
		segMaxX = PutkiMaxX;
		bool in1 = playerX >= PutkiMinX && playerX <= PutkiMaxX;
		bool in2 = Putki2MaxX > Putki2MinX + 0.01f && playerX >= Putki2MinX && playerX <= Putki2MaxX;
		if (in1)
			return true;
		if (in2)
		{
			segMinX = Putki2MinX;
			segMaxX = Putki2MaxX;
			return true;
		}
		return false;
	}

	private void TryCachePlayer()
	{
		if (_player != null && GodotObject.IsInstanceValid(_player) && _player.IsInsideTree())
			return;
		if (PlayerPath != default && GetNodeOrNull(PlayerPath) is PlayerController pc)
			_player = pc;
		else
			_player = GetTree().GetFirstNodeInGroup("player") as PlayerController;
	}

	/// <summary>Hämähäkit, joiden X on tämän putki-segmentin sisällä (± marginaali) — ei globaalia, jotta Pipe A:n roikkujat eivät lukitse Pipe B:tä.</summary>
	private int CountLiveSpidersInPipeSegment(float segMinX, float segMaxX)
	{
		var tree = GetTree();
		if (tree == null)
			return 0;
		float a = segMinX - SpideriLaskentaXMarginaali;
		float b = segMaxX + SpideriLaskentaXMarginaali;
		int c = 0;
		foreach (var n in tree.GetNodesInGroup("level2_ceiling_spider"))
		{
			if (!GodotObject.IsInstanceValid(n) || n.IsQueuedForDeletion())
				continue;
			if (n is not Node3D nd)
				continue;
			float x = nd.GlobalPosition.X;
			if (x < a || x > b)
				continue;
			c++;
		}
		return c;
	}
}
