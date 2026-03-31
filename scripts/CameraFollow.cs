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

	public override void _Ready()
	{
		_player = GetNodeOrNull<Node3D>(PlayerPath);
		_minPitchRad = Mathf.DegToRad(MinPitchDeg);
		_maxPitchRad = Mathf.DegToRad(MaxPitchDeg);
		var o = Offset;
		if (o.LengthSquared() < 0.01f)
			o = new Vector3(0f, 2f, 10f);
		_distance = o.Length();
		var nd = o.Normalized();
		_pitch = Mathf.Asin(Mathf.Clamp(nd.Y, -1f, 1f));
		_yaw = Mathf.Atan2(nd.X, nd.Z);
	}

	/// <summary>Lyhyt intro: siirtyy kohteeseen, pysähtyy, palaa seurantaan.</summary>
	public void PlayBossIntroShot(Vector3 lookAtWorld, Vector3 cameraEndWorld, float blendInSeconds, float holdSeconds, float blendOutSeconds)
	{
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
		if (_cinemaPhase >= 0 && _cinemaPhase <= 1)
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
		bool lockOrbit = _cinemaPhase >= 0 && _cinemaPhase <= 1;

		if (!lockOrbit)
		{
			float ax = Input.GetAxis("cam_look_left", "cam_look_right");
			float ay = Input.GetAxis("cam_look_up", "cam_look_down");
			_yaw -= ax * LookSensitivity * dt;
			_pitch -= ay * LookSensitivity * dt;
			_pitch = Mathf.Clamp(_pitch, _minPitchRad, _maxPitchRad);
		}

		var pivotFollow = _player.GlobalPosition + new Vector3(0f, PivotHeight, 0f);
		if (ClampCameraAboveGround)
			ApplyGroundPitchClamp(pivotFollow);

		float cp = Mathf.Cos(_pitch);
		var dir = new Vector3(Mathf.Sin(_yaw) * cp, Mathf.Sin(_pitch), Mathf.Cos(_yaw) * cp);
		if (dir.LengthSquared() < 1e-6f)
			dir = Vector3.Back;
		else
			dir = dir.Normalized();

		Vector3 targetPos = pivotFollow + dir * _distance;

		var world = GetWorld3D();
		var spaceState = world?.DirectSpaceState;
		if (spaceState != null)
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

		float t = Mathf.Clamp(FollowSpeed * dt, 0f, 1f);
		GlobalPosition = GlobalPosition.Lerp(targetPos, t);
		LookAt(pivotFollow, Vector3.Up);
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
		_pitch = Mathf.Clamp(_pitch, Mathf.Max(_minPitchRad, floorMinPitch), _maxPitchRad);
	}
}
