using Godot;

/// <summary>
/// Seuraa pelaajaa ja orbitoi oikean sauvan (tai hiiren) suuntaan — voi katsoa ylös (torni) ja sivuille.
/// </summary>
public partial class CameraFollow : Camera3D
{
	[Export] public NodePath PlayerPath;
	[Export] public float FollowSpeed = 14f;
	/// <summary>Alkuperäinen etäisyys/suunta ennen ensimmäistä sauvaa (lasketaan yaw/pitch/distance).</summary>
	[Export] public Vector3 Offset = new(2f, 3.5f, 12f);
	[ExportGroup("Orbit")]
	[Export] public float PivotHeight = 1.35f;
	[Export] public float LookSensitivity = 2.35f;
	[Export] public float MinPitchDeg = -58f;
	[Export] public float MaxPitchDeg = 78f;
	[ExportGroup("Maahan rajoitus")]
	[Export] public bool ClampCameraAboveGround = true;
	[Export] public float MinHeightAboveGround = 0.45f;
	[Export] public float GroundRaycastUp = 12f;
	[Export] public float GroundRaycastDown = 320f;
	[ExportGroup("Hiiri (PC)")]
	[Export] public bool MouseLookEnabled = true;
	[Export] public float MouseSensitivity = 0.0045f;
	[Export] public bool MouseLookRequiresRightButton = true;

	/// <summary>
	/// Kiinteä sivunäkymä (putki tms.): ei orbit-sauvaa/hiirtä, yaw/pitch pysyvät asetuksissa.
	/// Etäisyys = <see cref="Offset"/>.Length() (säädä Offset suuremmaksi zoomataksesi kauemmas).
	/// </summary>
	[ExportGroup("Side-scroller / kiinteä kulma")]
	[Export] public bool SideScrollerLock = false;
	[Export] public float SideScrollerYawDeg = 0f;
	[Export] public float SideScrollerPitchDeg = 14f;
	[Export] public bool SideScrollerDisableOrbitInput = true;

	/// <summary>
	/// Side-scroller: älä lyhennä kameraetäisyyttä seinä-raylla (sama etäisyys putkessa vs. avoin luola).
	/// Lisäksi täysi seuranta yhdellä framella (ei lerp + LookAt -värinää).
	/// </summary>
	[Export] public bool SideScrollerSkipWallRayAndSnap = true;

	/// <summary>
	/// Side-scroller: seinä-ray pakotettuna päälle — kamera ei mene seinän läpi (esim. putken +Z-reuna).
	/// Toimii yhdessä <see cref="SideScrollerSkipWallRayAndSnap"/>-kanssa: snap säilyy, vain ray otetaan käyttöön.
	/// </summary>
	[Export] public bool SideScrollerWallClamp = false;

	[ExportGroup("Level 1 boss — tanssikamera")]
	[Export] public bool EnableBossDanceCamera = true;
	/// <summary>
	/// Kun true, kamera pysyy olkapää-näkymässä koko bossitaistelun ajan (myös syöksy + potku). Estää vapaa-orbitin.
	/// </summary>
	[Export] public bool BossEncounterFramingWholeFight = true;
	/// <summary>True: kamera pelaajan takaa bossia kohti (stabiili). False: vanha bossin etupuoli kohti pelaajaa (lähelle zoom).</summary>
	[Export] public bool BossDanceCamFromPlayerShoulder = true;
	[Export] public float BossDanceLookAtYOffset = 1.35f;
	/// <summary>Tanssi: etäisyys kohteesta (olkapää: pelaajan pivotista taaksepäin); legacy: bossin etupuoli.</summary>
	[Export] public float BossDanceCamDistance = 2.15f;
	/// <summary>Lisäetäisyys kun pelaaja–bossi vaakaetäisyys kasvaa (bossi pysyy ruudussa syöksyssä).</summary>
	[Export] public float BossEncounterCamExtraPerMeterBeyond = 0.42f;
	[Export] public float BossEncounterCamExtraStartPlanarM = 4.25f;
	[Export] public float BossEncounterCamExtraMaxM = 9f;
	[Export] public float BossDanceCamFrontHeightM = 1.05f;
	[Export] public float BossDanceCamBlendInSeconds = 0.9f;
	[Export] public float BossDanceCamBlendOutSeconds = 0.75f;
	[Export] public float BossDanceCamFollowSpeed = 4.2f;
	[Export] public bool BossDanceCamIgnoreWallPull = true;
	/// <summary>Kuinka paljon LookAt painottuu bossiin (1 = täysin bossin keskipistettä kohti).</summary>
	[Export] public float BossEncounterLookAtBossWeight = 0.88f;

	private Node3D _player;
	private float _yaw;
	private float _pitch;
	private float _distance;
	private float _minPitchRad;
	private float _maxPitchRad;

	private int _cinemaPhase = -1;
	private float _cinemaPhaseTime;
	private Vector3 _cinemaLookAt;
	private Vector3 _cinemaCamFrom;
	private Vector3 _cinemaCamHold;
	private float _cinemaBlendIn = 1f;
	private float _cinemaHold;
	private float _cinemaBlendOut = 1f;

	private float _bossCloseupBlend;

	/// <summary>Pidä lähestymistä hetken kun tanssi päättyy — vähentää blendin "nykäisyä".</summary>
	private float _bossCloseupRawWantHold;

	private bool _hadBossEncounterFraming;

	// Screen shake
	private float _shakeAmplitude;
	private float _shakeDecayDuration = 0.25f;
	private float _shakeTimer;

	/// <summary>Hetkellinen etäisyyden muutos (metriä) pivotista — negatiivinen = zoom lähemmäs. Level 1 joystick.</summary>
	private float _momentaryDistanceDelta;
	private float _momentaryDistanceTimer;

	/// <summary>
	/// Lyhyt “cinematic” zoom: siirtää kameraa lähemmäs tai kauemmas hetkeksi (esim. joystickin aktivointi).
	/// </summary>
	public void ApplyMomentaryDistanceOffset(float deltaMeters, float durationSeconds)
	{
		_momentaryDistanceDelta = deltaMeters;
		_momentaryDistanceTimer   = Mathf.Max(0.01f, durationSeconds);
	}

	/// <summary>Käynnistää ruututärinän — kutsutaan miekkaosumahetkellä.</summary>
	public void ShakeImpulse(float amplitude, float decaySeconds = 0.25f)
	{
		_shakeAmplitude = Mathf.Max(_shakeAmplitude, amplitude);
		_shakeDecayDuration = decaySeconds;
		_shakeTimer = Mathf.Max(_shakeTimer, decaySeconds);
	}

	private void ApplyScreenShake(float dt)
	{
		if (_shakeTimer <= 0f) return;
		_shakeTimer = Mathf.Max(0f, _shakeTimer - dt);
		float frac = _shakeDecayDuration > 0f ? _shakeTimer / _shakeDecayDuration : 0f;
		float amount = _shakeAmplitude * frac * frac; // square → sharp start, fast decay
		var right = GlobalTransform.Basis.X;
		var up = GlobalTransform.Basis.Y;
		GlobalPosition += right * (float)GD.RandRange(-amount, amount)
						+ up    * (float)GD.RandRange(-amount * 0.55f, amount * 0.55f);
	}

	public override void _Ready()
	{
		_player = GetNodeOrNull<Node3D>(PlayerPath);
		if (_player == null && GetParent() is Node3D)
			_player = GetParent().GetNodeOrNull<Node3D>("Player");
		_minPitchRad = Mathf.DegToRad(MinPitchDeg);
		_maxPitchRad = Mathf.DegToRad(MaxPitchDeg);
		var o = Offset;
		if (o.LengthSquared() < 0.01f)
			o = new Vector3(0f, 2f, 10f);
		if (SideScrollerLock)
		{
			_distance = o.Length();
			if (_distance < 0.01f)
				_distance = 16f;
			_yaw = Mathf.DegToRad(SideScrollerYawDeg);
			_pitch = Mathf.Clamp(Mathf.DegToRad(SideScrollerPitchDeg), _minPitchRad, _maxPitchRad);
		}
		else
		{
			_distance = o.Length();
			var nd = o.Normalized();
			_pitch = Mathf.Asin(Mathf.Clamp(nd.Y, -1f, 1f));
			_yaw = Mathf.Atan2(nd.X, nd.Z);
		}

		if (_player != null && SideScrollerLock)
			CallDeferred(nameof(DeferredSnapSideScrollerToPlayer));
	}

	/// <summary>
	/// Ensimmäinen frame voi piirtää ennen ensimmäistä _PhysicsProcess-kutsua — snapataan sivunäkymä heti pelaajan kohdalle.
	/// </summary>
	private void DeferredSnapSideScrollerToPlayer()
	{
		if (_player == null || !_player.IsInsideTree() || !SideScrollerLock)
			return;
		var pivotFollow = _player.GlobalPosition + new Vector3(0f, PivotHeight, 0f);
		float cp = Mathf.Cos(_pitch);
		var dir = new Vector3(Mathf.Sin(_yaw) * cp, Mathf.Sin(_pitch), Mathf.Cos(_yaw) * cp);
		if (dir.LengthSquared() < 1e-6f)
			dir = Vector3.Back;
		else
			dir = dir.Normalized();
		GlobalPosition = pivotFollow + dir * _distance;
		if (SideScrollerWallClamp)
			GlobalPosition = ShortenCameraTargetAgainstWalls(pivotFollow, GlobalPosition, dir);
		if (pivotFollow.DistanceSquaredTo(GlobalPosition) > 1e-6f)
			LookAt(pivotFollow, Vector3.Up);
	}

	/// <summary>Raycast pivot→target: jos välissä on staattinen kollisio, kamera jää osuman sisäpuolelle.</summary>
	private Vector3 ShortenCameraTargetAgainstWalls(Vector3 pivotFollow, Vector3 targetPos, Vector3 dir)
	{
		var spaceState = GetWorld3D()?.DirectSpaceState;
		if (spaceState == null)
			return targetPos;
		var wallQuery = PhysicsRayQueryParameters3D.Create(pivotFollow, targetPos);
		wallQuery.CollideWithAreas = false;
		if (_player is CollisionObject3D playerCol)
			wallQuery.Exclude = new Godot.Collections.Array<Rid> { playerCol.GetRid() };
		var wallHit = spaceState.IntersectRay(wallQuery);
		if (wallHit.Count > 0 && wallHit.ContainsKey("position"))
			return (Vector3)wallHit["position"] + dir * -0.2f;
		return targetPos;
	}

	/// <summary>Lyhyt intro: siirtyy kohteeseen, pysähtyy, palaa seurantaan.</summary>
	public void PlayBossIntroShot(Vector3 lookAtWorld, Vector3 cameraEndWorld, float blendInSeconds, float holdSeconds, float blendOutSeconds)
	{
		if (!IsInsideTree())
		{
			var la = lookAtWorld;
			var ce = cameraEndWorld;
			float bi = blendInSeconds, h = holdSeconds, bo = blendOutSeconds;
			Callable.From(() => PlayBossIntroShot(la, ce, bi, h, bo)).CallDeferred();
			return;
		}

		_cinemaPhase = 0;
		_cinemaPhaseTime = 0f;
		_cinemaLookAt = lookAtWorld;
		_cinemaCamFrom = GlobalPosition;
		_cinemaCamHold = cameraEndWorld;
		_cinemaBlendIn = Mathf.Max(0.05f, blendInSeconds);
		_cinemaHold = Mathf.Max(0f, holdSeconds);
		_cinemaBlendOut = Mathf.Max(0.05f, blendOutSeconds);
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (!MouseLookEnabled || _player == null)
			return;
		if (SideScrollerLock && SideScrollerDisableOrbitInput)
			return;
		if (_cinemaPhase >= 0 && _cinemaPhase <= 1)
			return;
		if (IsBossDanceCameraBlockingInput())
			return;
		if (MouseLookRequiresRightButton && !Input.IsMouseButtonPressed(MouseButton.Right))
			return;
		if (@event is not InputEventMouseMotion mm)
			return;
		_yaw -= mm.Relative.X * MouseSensitivity;
		_pitch -= mm.Relative.Y * MouseSensitivity;
		_pitch = Mathf.Clamp(_pitch, _minPitchRad, _maxPitchRad);
	}

	public override void _PhysicsProcess(double delta)
	{
		if (!IsInsideTree())
			return;
		if (_player == null || !_player.IsInsideTree())
			return;

		float dt = (float)delta;
		bool lockOrbit = (_cinemaPhase >= 0 && _cinemaPhase <= 1) || IsBossDanceCameraBlockingInput();

		if (SideScrollerLock)
		{
			_yaw = Mathf.DegToRad(SideScrollerYawDeg);
			_pitch = Mathf.Clamp(Mathf.DegToRad(SideScrollerPitchDeg), _minPitchRad, _maxPitchRad);
		}
		else if (!lockOrbit)
		{
			float ax = Input.GetAxis("cam_look_left", "cam_look_right");
			float ay = Input.GetAxis("cam_look_up", "cam_look_down");
			_yaw -= ax * LookSensitivity * dt;
			_pitch -= ay * LookSensitivity * dt;
			_pitch = Mathf.Clamp(_pitch, _minPitchRad, _maxPitchRad);
		}

		var pivotFollow = _player.GlobalPosition + new Vector3(0f, PivotHeight, 0f);

		if (BossEncounterFramingWholeFight && !SideScrollerLock)
		{
			bool enc = ShouldBossEncounterFramingCamera();
			if (_hadBossEncounterFraming && !enc)
				SyncOrbitFromWorldCamera(pivotFollow);
			_hadBossEncounterFraming = enc;
		}

		if (ClampCameraAboveGround && !SideScrollerLock)
			ApplyGroundPitchClamp(pivotFollow);

		float cp = Mathf.Cos(_pitch);
		var dir = new Vector3(Mathf.Sin(_yaw) * cp, Mathf.Sin(_pitch), Mathf.Cos(_yaw) * cp);
		if (dir.LengthSquared() < 1e-6f)
			dir = Vector3.Back;
		else
			dir = dir.Normalized();

		float effectiveDist = _distance;
		if (_momentaryDistanceTimer > 0f)
		{
			effectiveDist += _momentaryDistanceDelta;
			_momentaryDistanceTimer -= dt;
			if (_momentaryDistanceTimer <= 0f)
			{
				_momentaryDistanceTimer   = 0f;
				_momentaryDistanceDelta     = 0f;
			}
		}

		Vector3 targetPos = pivotFollow + dir * effectiveDist;

		var world = GetWorld3D();
		var spaceState = world?.DirectSpaceState;
		bool skipWallRay = SideScrollerLock && SideScrollerSkipWallRayAndSnap && !SideScrollerWallClamp;
		if (spaceState != null && !skipWallRay)
			targetPos = ShortenCameraTargetAgainstWalls(pivotFollow, targetPos, dir);

		if (_cinemaPhase >= 0)
		{
			_cinemaPhaseTime += dt;
			if (_cinemaPhase == 0)
			{
				float u = Mathf.Clamp(_cinemaPhaseTime / _cinemaBlendIn, 0f, 1f);
				GlobalPosition = _cinemaCamFrom.Lerp(_cinemaCamHold, u);
				LookAt(_cinemaLookAt, Vector3.Up);
				if (u >= 1f)
				{
					_cinemaPhase = 1;
					_cinemaPhaseTime = 0f;
				}
				return;
			}
			if (_cinemaPhase == 1)
			{
				GlobalPosition = _cinemaCamHold;
				LookAt(_cinemaLookAt, Vector3.Up);
				if (_cinemaPhaseTime >= _cinemaHold)
				{
					_cinemaPhase = 2;
					_cinemaPhaseTime = 0f;
				}
				return;
			}
			float uo = Mathf.Clamp(_cinemaPhaseTime / _cinemaBlendOut, 0f, 1f);
			GlobalPosition = _cinemaCamHold.Lerp(targetPos, uo);
			LookAt(pivotFollow, Vector3.Up);
			if (uo >= 1f)
				_cinemaPhase = -1;
			return;
		}

		bool rawWantBossCloseup = BossEncounterFramingWholeFight
			? ShouldBossEncounterFramingCamera()
			: ShouldBossCloseupDanceCamera();
		if (rawWantBossCloseup)
			_bossCloseupRawWantHold = 0.22f;
		else if (_bossCloseupRawWantHold > 0f)
			_bossCloseupRawWantHold = Mathf.Max(0f, _bossCloseupRawWantHold - dt);

		var bossForCloseup = GetTree().GetFirstNodeInGroup("level1_boss") as BossLevel1;
		if (bossForCloseup != null && GodotObject.IsInstanceValid(bossForCloseup) && bossForCloseup.IsBossDead)
			_bossCloseupRawWantHold = 0f;

		bool wantBossCloseup = rawWantBossCloseup || _bossCloseupRawWantHold > 0f;
		float blendStep = dt / Mathf.Max(0.05f, wantBossCloseup ? BossDanceCamBlendInSeconds : BossDanceCamBlendOutSeconds);
		_bossCloseupBlend = Mathf.MoveToward(_bossCloseupBlend, wantBossCloseup ? 1f : 0f, blendStep);
		float bossBlendSmooth = _bossCloseupBlend * _bossCloseupBlend * (3f - 2f * _bossCloseupBlend);

		if (_bossCloseupBlend > 0.001f)
		{
			var boss = GetTree().GetFirstNodeInGroup("level1_boss") as BossLevel1;
			if (boss != null && GodotObject.IsInstanceValid(boss) && boss.IsInsideTree() && !boss.IsBossDead)
			{
				Vector3 lookAtBoss = boss.GlobalPosition + Vector3.Up * BossDanceLookAtYOffset;
				Vector3 bossRoot = boss.GlobalPosition;

				Vector3 danceTarget;
				Vector3 blendedLook;

				if (BossDanceCamFromPlayerShoulder)
				{
					Vector3 towardBoss = bossRoot - pivotFollow;
					towardBoss.Y = 0f;
					float planarSep = towardBoss.Length();
					if (planarSep < 1e-4f)
						towardBoss = Vector3.Forward;
					else
						towardBoss /= planarSep;

					float extraBack = Mathf.Clamp(
						(planarSep - BossEncounterCamExtraStartPlanarM) * BossEncounterCamExtraPerMeterBeyond,
						0f,
						BossEncounterCamExtraMaxM);
					float camDist = BossDanceCamDistance + extraBack;

					danceTarget = pivotFollow - towardBoss * camDist + Vector3.Up * BossDanceCamFrontHeightM;
					float lookW = Mathf.Clamp(BossEncounterLookAtBossWeight, 0.35f, 1f);
					blendedLook = pivotFollow.Lerp(lookAtBoss, Mathf.Clamp(bossBlendSmooth * lookW, 0f, 1f));
				}
				else
				{
					Vector3 toPlayer = _player.GlobalPosition - bossRoot;
					toPlayer.Y = 0f;
					if (toPlayer.LengthSquared() < 1e-5f)
					{
						var bz = boss.GlobalTransform.Basis.Z;
						toPlayer = new Vector3(-bz.X, 0f, -bz.Z);
						if (toPlayer.LengthSquared() < 1e-5f)
							toPlayer = Vector3.Forward;
					}
					toPlayer = toPlayer.Normalized();

					danceTarget = bossRoot + toPlayer * BossDanceCamDistance + Vector3.Up * BossDanceCamFrontHeightM;
					blendedLook = pivotFollow.Lerp(lookAtBoss, bossBlendSmooth);
				}

				if (!BossDanceCamIgnoreWallPull && spaceState != null)
				{
					var wallQuery = PhysicsRayQueryParameters3D.Create(lookAtBoss, danceTarget);
					wallQuery.CollideWithAreas = false;
					var ex = new Godot.Collections.Array<Rid>();
					if (_player is CollisionObject3D pc)
						ex.Add(pc.GetRid());
					ex.Add(boss.GetRid());
					wallQuery.Exclude = ex;
					var wallHit = spaceState.IntersectRay(wallQuery);
					if (wallHit.Count > 0 && wallHit.ContainsKey("position"))
						danceTarget = (Vector3)wallHit["position"] + (danceTarget - lookAtBoss).Normalized() * -0.2f;
				}

				Vector3 blendedPos = targetPos.Lerp(danceTarget, bossBlendSmooth);
				float spd = Mathf.Lerp(FollowSpeed, BossDanceCamFollowSpeed, bossBlendSmooth);
				float td = Mathf.Clamp(spd * dt, 0f, 1f);
				GlobalPosition = GlobalPosition.Lerp(blendedPos, td);
				ApplyScreenShake(dt);
				if (blendedLook.DistanceSquaredTo(GlobalPosition) > 1e-6f)
					LookAt(blendedLook, Vector3.Up);
				return;
			}
		}

		bool sideScrollerSnap = SideScrollerLock && SideScrollerSkipWallRayAndSnap;
		float t = sideScrollerSnap
			? 1f
			: Mathf.Clamp(FollowSpeed * dt, 0f, 1f);
		GlobalPosition = GlobalPosition.Lerp(targetPos, t);
		ApplyScreenShake(dt);
		LookAt(pivotFollow, Vector3.Up);
	}

	private bool ShouldBossCloseupDanceCamera()
	{
		if (!EnableBossDanceCamera || !IsInsideTree())
			return false;
		var boss = GetTree().GetFirstNodeInGroup("level1_boss") as BossLevel1;
		return boss != null && GodotObject.IsInstanceValid(boss) && boss.IsInsideTree() && boss.IsBossCloseupDanceCameraActive;
	}

	/// <summary>Level 1 -bossi elossa → kehystyskamera (olkapää kohti bossia) koko taistelun.</summary>
	private bool ShouldBossEncounterFramingCamera()
	{
		if (!EnableBossDanceCamera || !IsInsideTree())
			return false;
		var boss = GetTree().GetFirstNodeInGroup("level1_boss") as BossLevel1;
		return boss != null && GodotObject.IsInstanceValid(boss) && boss.IsInsideTree() && !boss.IsBossDead;
	}

	private bool IsBossDanceCameraBlockingInput()
	{
		var boss = GetTree()?.GetFirstNodeInGroup("level1_boss") as BossLevel1;
		if (BossEncounterFramingWholeFight
			&& boss != null && GodotObject.IsInstanceValid(boss) && boss.IsInsideTree() && !boss.IsBossDead)
			return true;
		// Vanha tila: koko taistelun kehystä ei — vapaa orbit kunnes tanssi-lähikuva (blend) pyytää lukkoa.
		if (!BossEncounterFramingWholeFight
			&& boss != null && GodotObject.IsInstanceValid(boss) && boss.IsInsideTree() && !boss.IsBossDead)
			return false;
		return _bossCloseupBlend > 0.04f || ShouldBossCloseupDanceCamera();
	}

	/// <summary>
	/// Boss-kehyksen jälkeen yaw/pitch/distance vastaavat nykyistä kameraa — vapaa orbit ei hyppää väärään kulmaan.
	/// </summary>
	private void SyncOrbitFromWorldCamera(Vector3 pivotFollow)
	{
		if (SideScrollerLock)
			return;
		Vector3 off = GlobalPosition - pivotFollow;
		float len = off.Length();
		if (len < 0.08f)
			return;
		Vector3 nd = off / len;
		_pitch = Mathf.Asin(Mathf.Clamp(nd.Y, -1f, 1f));
		_yaw = Mathf.Atan2(nd.X, nd.Z);
		_distance = len;
		_pitch = Mathf.Clamp(_pitch, _minPitchRad, _maxPitchRad);
	}

	/// <summary>
	/// Estää kameran uppoamisen pelattavan pinnan alle: vaatii riittävän pitchin suhteessa maahan pivotin alla.
	/// </summary>
	private void ApplyGroundPitchClamp(Vector3 pivot)
	{
		if (!IsInsideTree())
			return;
		var space = GetWorld3D()?.DirectSpaceState;
		if (space == null)
			return;
		var from = pivot + Vector3.Up * GroundRaycastUp;
		var to = pivot + Vector3.Down * GroundRaycastDown;
		var query = PhysicsRayQueryParameters3D.Create(from, to);
		query.CollideWithAreas = false;
		if (_player is CollisionObject3D co)
			query.Exclude = new Godot.Collections.Array<Rid> { co.GetRid() };
		var hit = space.IntersectRay(query);
		if (hit.Count == 0 || !hit.ContainsKey("position"))
			return;
		var groundY = ((Vector3)hit["position"]).Y;
		float minCamY = groundY + MinHeightAboveGround;
		float k = (minCamY - pivot.Y) / Mathf.Max(_distance, 0.1f);
		k = Mathf.Clamp(k, -1f, 1f);
		float floorMinPitch = Mathf.Asin(k);
		// Alaraja ei saa ylittää ylärajaa (.NET Clamp heittää ArgumentException).
		float minBound = Mathf.Max(_minPitchRad, floorMinPitch);
		minBound = Mathf.Min(minBound, _maxPitchRad);
		_pitch = Mathf.Clamp(_pitch, minBound, _maxPitchRad);
	}
}
