using Godot;

/// <summary>
/// Lataa <see cref="GameState.PendingLoadScenePath"/> taustasäikeessä ja vaihtaa kenttään kun valmis.
/// Spinner + edistymispalkki ovat autoload- <see cref="LoadingOverlay"/> -kerroksella (pyöivät scene-vaihtojen yli).
/// </summary>
public partial class LoadingScreen : Control
{
	private string _scenePath;
	private bool _failureHandled;

	public override void _Ready()
	{
		LoadingOverlay.Instance?.ShowOverlay();

		_scenePath = GameState.Instance.PendingLoadScenePath;
		GameState.Instance.PendingLoadScenePath = "";

		if (string.IsNullOrEmpty(_scenePath))
		{
			GD.PrintErr("LoadingScreen: PendingLoadScenePath puuttuu — palataan valikkoon.");
			LoadingOverlay.Instance?.HideOverlay();
			GetTree().ChangeSceneToFile("res://scenes/ui/main_menu.tscn");
			return;
		}

		Error err = ResourceLoader.LoadThreadedRequest(_scenePath);
		if (err != Error.Ok)
		{
			GD.PrintErr($"LoadingScreen: LoadThreadedRequest epäonnistui ({err}) — synkroninen fallback.");
			var packed = GD.Load<PackedScene>(_scenePath);
			LoadingOverlay.Instance?.ScheduleHideAfterFrames(4);
			if (packed != null)
				GetTree().ChangeSceneToPacked(packed);
			else
			{
				LoadingOverlay.Instance?.HideOverlay();
				GetTree().ChangeSceneToFile("res://scenes/ui/main_menu.tscn");
			}
			return;
		}

		RunLoadAsync();
	}

	private async void RunLoadAsync()
	{
		while (IsInsideTree() && !_failureHandled)
		{
			await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

			if (string.IsNullOrEmpty(_scenePath))
				return;

			var progressArr = new Godot.Collections.Array();
			ResourceLoader.ThreadLoadStatus status =
				ResourceLoader.LoadThreadedGetStatus(_scenePath, progressArr);

			switch (status)
			{
				case ResourceLoader.ThreadLoadStatus.InProgress:
					if (progressArr.Count > 0)
					{
						float p = progressArr[0].AsSingle();
						LoadingOverlay.Instance?.SetProgress(p * 100f);
					}
					break;

				case ResourceLoader.ThreadLoadStatus.Loaded:
					await FinishLoadedAsync();
					return;

				case ResourceLoader.ThreadLoadStatus.Failed:
				case ResourceLoader.ThreadLoadStatus.InvalidResource:
					HandleLoadFailure(status);
					return;
			}
		}
	}

	private async System.Threading.Tasks.Task FinishLoadedAsync()
	{
		LoadingOverlay.Instance?.SetProgress(100f);

		var packed = ResourceLoader.LoadThreadedGet(_scenePath) as PackedScene;
		_scenePath = "";

		if (packed == null)
		{
			GD.PrintErr("LoadingScreen: PackedScene puuttuu.");
			LoadingOverlay.Instance?.HideOverlay();
			GetTree().ChangeSceneToFile("res://scenes/ui/main_menu.tscn");
			return;
		}

		for (int i = 0; i < 2; i++)
			await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

		LoadingOverlay.Instance?.ScheduleHideAfterFrames(4);
		GetTree().ChangeSceneToPacked(packed);
	}

	private void HandleLoadFailure(ResourceLoader.ThreadLoadStatus status)
	{
		if (_failureHandled)
			return;

		_failureHandled = true;
		_scenePath = "";
		GD.PrintErr($"LoadingScreen: Lataus epäonnistui ({status}).");
		LoadingOverlay.Instance?.SetStatus("Lataus epäonnistui — palataan valikkoon.");
		GetTree().CreateTimer(1.2).Timeout += () =>
		{
			LoadingOverlay.Instance?.HideOverlay();
			GetTree().ChangeSceneToFile("res://scenes/ui/main_menu.tscn");
		};
	}
}
