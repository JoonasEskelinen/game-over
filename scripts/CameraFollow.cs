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

	[ExportGroup("Level 1 boss — tanssikamera")]
	[Export] public bool EnableBossDanceCamera = true;
	[Export] public float BossDanceLookAtYOffset = 1.35f;
	/// <summary>Etäisyys bossin ja pelaajan välillä XZ: bossin puolelta (näet kasvot).</summary>
	[Export] public float BossDanceCamDistance = 2.15f;
	[Export] public float BossDanceCamFrontHeightM = 1.05f;
	[Export] public float BossDanceCamBlendInSeconds = 0.9f;
	[Export] public float BossDanceCamBlendOutSeconds = 0.75f;
	[Export] public float BossDanceCamFollowSpeed = 4.2f;
	[Export] public bool BossDanceCamIgnoreWallPull = true;

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
		bool skipWallForSide = SideScrollerLock && SideScrollerSkipWallRayAndSnap;
		if (spaceState != null && !skipWallForSide)
		{
			var wallQuery = PhysicsRayQueryParameters3D.Create(pivotFollow, targetPos);
			wallQuery.CollideWithAreas = false;
			if (_player is CollisionObject3D playerCol)
				wallQuery.Exclude = new Godot.Collections.Array<Rid> { playerCol.GetRid() };
			var wallHit = spaceState.IntersectRay(wallQuery);
			if (wallHit.Count > 0 && wallHit.ContainsKey("position"))
				targetPos = (Vector3)wallHit["position"] + dir * -0.2f;
		}

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

		bool wantBossCloseup = ShouldBossCloseupDanceCamera();
		float blendStep = dt / Mathf.Max(0.05f, wantBossCloseup ? BossDanceCamBlendInSeconds : BossDanceCamBlendOutSeconds);
		_bossCloseupBlend = Mathf.MoveToward(_bossCloseupBlend, wantBossCloseup ? 1f : 0f, blendStep);
		float bossBlendSmooth = _bossCloseupBlend * _bossCloseupBlend * (3f - 2f * _bossCloseupBlend);

		if (_bossCloseupBlend > 0.001f)
		{
			var boss = GetTree().GetFirstNodeInGroup("level1_boss") as BossLevel1;
			if (boss != null && GodotObject.IsInstanceValid(boss) && boss.IsInsideTree())
			{
				Vector3 lookAtBoss = boss.GlobalPosition + Vector3.Up * BossDanceLookAtYOffset;
				Vector3 bossRoot = boss.GlobalPosition;
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

				Vector3 danceTarget = bossRoot + toPlayer * BossDanceCamDistance + Vector3.Up * BossDanceCamFrontHeightM;

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
				Vector3 blendedLook = pivotFollow.Lerp(lookAtBoss, bossBlendSmooth);
				float spd = Mathf.Lerp(FollowSpeed, BossDanceCamFollowSpeed, bossBlendSmooth);
				float td = Mathf.Clamp(spd * dt, 0f, 1f);
				GlobalPosition = GlobalPosition.Lerp(blendedPos, td);
				ApplyScreenShake(dt);
				if (blendedLook.DistanceSquaredTo(GlobalPosition) > 1e-6f)
					LookAt(blendedLook, Vector3.Up);
				return;
			}
		}

		float t = skipWallForSide
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

	private bool IsBossDanceCameraBlockingInput()
		=> _bossCloseupBlend > 0.04f || ShouldBossCloseupDanceCamera();

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
