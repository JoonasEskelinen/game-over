using Godot;

/// <summary>
/// Yksi <see cref="AudioStreamPlayer"/> kenttämusiikille. Level 1: voi pysäyttää kun ryhmässä ilmestyy node (boss).
/// </summary>
public partial class LevelMusicPlayer : Node
{
	[Export] public string MusicResourcePath = "";

	[Export] public float VolumeDb = -10f;

	/// <summary>Kun true: <see cref="_Process"/> pysäyttää musiikin kun <see cref="BossStopGroup"/> löytyy puusta.</summary>
	[Export] public bool StopWhenBossGroupAppears = false;

	[Export] public string BossStopGroup = "level1_boss";

	private AudioStreamPlayer _player;
	private bool _stoppedForBoss;

	public override void _Ready()
	{
		if (!StopWhenBossGroupAppears)
			SetProcess(false);

		if (string.IsNullOrWhiteSpace(MusicResourcePath) || !ResourceLoader.Exists(MusicResourcePath))
			return;

		var stream = GD.Load<AudioStream>(MusicResourcePath);
		if (stream == null)
		{
			GD.PrintErr($"LevelMusicPlayer: ei voitu ladata: {MusicResourcePath}");
			return;
		}

		ApplyLoop(stream);

		_player = new AudioStreamPlayer { Name = "LevelMusic" };
		AddChild(_player);
		_player.Stream = stream;
		_player.VolumeDb = VolumeDb;
		_player.Play();
	}

	public override void _Process(double delta)
	{
		if (!StopWhenBossGroupAppears || _stoppedForBoss || _player == null)
			return;
		if (GetTree().GetFirstNodeInGroup(BossStopGroup) == null)
			return;

		_stoppedForBoss = true;
		if (_player.Playing)
			_player.Stop();
		_player.QueueFree();
		_player = null;
	}

	private static void ApplyLoop(AudioStream stream)
	{
		switch (stream)
		{
			case AudioStreamMP3 mp3:
				mp3.Loop = true;
				break;
			case AudioStreamOggVorbis ogg:
				ogg.Loop = true;
				break;
		}
	}
}
