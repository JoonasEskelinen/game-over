using Godot;

/// <summary>
/// Näytetään päävalikosta — lataa <see cref="GameState.PendingLoadScenePath"/> säikeessä
/// ja vaihtaa kenttään kun valmis (spinner + edistymispalkki pyörivät latauksen ajan).
/// </summary>
public partial class LoadingScreen : Control
{
	private string _scenePath;
	private ProgressBar _progress;
	private TextureRect _spinner;
	private Label _statusLabel;
	private bool _loadFinished;
	private bool _failureHandled;

	public override void _Ready()
	{
		_progress     = GetNodeOrNull<ProgressBar>("Overlay/Center/Column/ProgressBar");
		_spinner      = GetNodeOrNull<TextureRect>("Overlay/Center/Column/Spinner");
		_statusLabel = GetNodeOrNull<Label>("Overlay/Center/Column/StatusLabel");

		_scenePath = GameState.Instance.PendingLoadScenePath;
		GameState.Instance.PendingLoadScenePath = "";

		if (string.IsNullOrEmpty(_scenePath))
		{
			GD.PrintErr("LoadingScreen: PendingLoadScenePath puuttuu — palataan valikkoon.");
			GetTree().ChangeSceneToFile("res://scenes/ui/main_menu.tscn");
			return;
		}

		Error err = ResourceLoader.LoadThreadedRequest(_scenePath);
		if (err != Error.Ok)
		{
			GD.PrintErr($"LoadingScreen: LoadThreadedRequest epäonnistui ({err}) — synkroninen fallback.");
			var packed = GD.Load<PackedScene>(_scenePath);
			if (packed != null)
				GetTree().ChangeSceneToPacked(packed);
			else
				GetTree().ChangeSceneToFile("res://scenes/ui/main_menu.tscn");
			return;
		}

		if (_progress != null)
		{
			_progress.Value = 0;
			_progress.MaxValue = 100;
		}
	}

	public override void _Process(double delta)
	{
		if (_loadFinished || string.IsNullOrEmpty(_scenePath))
			return;

		if (_spinner != null)
			_spinner.Rotation += (float)(delta * 3.5);

		var progressArr = new Godot.Collections.Array();
		ResourceLoader.ThreadLoadStatus status =
			ResourceLoader.LoadThreadedGetStatus(_scenePath, progressArr);

		switch (status)
		{
			case ResourceLoader.ThreadLoadStatus.InProgress:
				if (_progress != null && progressArr.Count > 0)
				{
					float p = progressArr[0].AsSingle();
					_progress.Value = Mathf.Clamp(p * 100.0, 0, 100);
				}
				break;

			case ResourceLoader.ThreadLoadStatus.Loaded:
				_loadFinished = true;
				if (_progress != null)
					_progress.Value = 100;
				var packed = ResourceLoader.LoadThreadedGet(_scenePath) as PackedScene;
				if (packed != null)
					GetTree().ChangeSceneToPacked(packed);
				else
				{
					GD.PrintErr("LoadingScreen: PackedScene puuttuu.");
					GetTree().ChangeSceneToFile("res://scenes/ui/main_menu.tscn");
				}
				break;

			case ResourceLoader.ThreadLoadStatus.Failed:
			case ResourceLoader.ThreadLoadStatus.InvalidResource:
				if (_failureHandled)
					break;
				_failureHandled = true;
				_loadFinished = true;
				SetProcess(false);
				GD.PrintErr($"LoadingScreen: Lataus epäonnistui ({status}) polulle: {_scenePath}");
				if (_statusLabel != null)
					_statusLabel.Text = "Lataus epäonnistui — palataan valikkoon.";
				GetTree().CreateTimer(1.2).Timeout += () =>
					GetTree().ChangeSceneToFile("res://scenes/ui/main_menu.tscn");
				break;
		}
	}
}
