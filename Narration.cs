using Godot;
using System;
using System.Threading.Tasks;

public partial class Narration : Control
{
	[Export] public float LineFadeDuration = 0.5f;
	[Export] public float LineDelay = 0.2f;
	[Export] public string[] Lines = new string[]
	{
		"Desa Waktosa, sebuah desa damai di tepi hutan.",
		"Suatu malam, iblis menyerang tanpa peringatan.",
		"Api dan kegelapan menyelimuti rumah-rumah.",
		"Para pawang, penjaga desa, bangkit melawan.",
		"Mereka harus melindungi Waktosa dari kehancuran.",
		"\n",
		"Pertempuran penentuan pun dimulai..."
	};

	private Label _hintLabel;
	private bool _allLinesShown = false;

	public override async void _Ready()
	{
		await ShowLinesAnimated();
		SetProcessUnhandledInput(true);
	}
	
	private void _on_BtnBack_pressed()
	{
		GetTree().ChangeSceneToFile("res://menu.tscn");
	}
	private async Task ShowLinesAnimated()
	{
		var font = GetThemeFont("font", "Label");
		float lineHeight = 60f;
		int lineCount = Lines.Length;
		int centerIndex = lineCount / 2;
		float centerY = 900f / 2;
		float yStart = centerY - (centerIndex * lineHeight);
		for (int i = 0; i < lineCount; i++)
		{
			var line = Lines[i];
			var label = new Label();
			label.Text = line;
			label.Modulate = new Color(1, 1, 1, 0);
			label.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
			label.HorizontalAlignment = HorizontalAlignment.Center;
			label.VerticalAlignment = VerticalAlignment.Center;
			label.Position = new Vector2(0, yStart + i * lineHeight + 50);
			label.AddThemeFontSizeOverride("font_size", 48);
			label.AddThemeColorOverride("font_outline_color", Colors.Black);
			label.AddThemeConstantOverride("outline_size", 2);
			// Add shadow effect
			label.AddThemeColorOverride("shadow_color", new Color(0, 0, 0, 0.7f));
			label.AddThemeConstantOverride("shadow_offset_x", 2);
			label.AddThemeConstantOverride("shadow_offset_y", 2);
			AddChild(label);
			await AnimateLabelFadeIn(label, yStart + i * lineHeight);
			await ToSignal(GetTree().CreateTimer(LineDelay), "timeout");
		}
		ShowHint();
		_allLinesShown = true;
		//wait 1 second to battle arena
		await ToSignal(GetTree().CreateTimer(1.0f), "timeout");
		GetTree().ChangeSceneToFile("res://battle_arena.tscn");
	}

	private void ShowHint()
	{
		_hintLabel = new Label();
		_hintLabel.Text = "Klik atau tekan tombol apa saja untuk lanjut";
		_hintLabel.Modulate = new Color(1, 1, 1, 0.85f);
		_hintLabel.AddThemeFontSizeOverride("font_size", 28);
		_hintLabel.AddThemeColorOverride("font_outline_color", Colors.Black);
		_hintLabel.AddThemeConstantOverride("outline_size", 2);
		_hintLabel.HorizontalAlignment = HorizontalAlignment.Left;
		_hintLabel.VerticalAlignment = VerticalAlignment.Bottom;
		_hintLabel.Position = new Vector2(40, 900f - 40);
		AddChild(_hintLabel);
	}

	private async Task AnimateLabelFadeIn(Label label, float targetY)
	{
		float duration = LineFadeDuration;
		float elapsed = 0f;
		float startY = targetY + 50f;
		float endY = targetY;
		while (elapsed < duration)
		{
			float t = elapsed / duration;
			label.Modulate = new Color(1, 1, 1, t);
			label.Position = new Vector2(label.Position.X, Mathf.Lerp(startY, endY, t));
			await ToSignal(GetTree().CreateTimer(0.016f), "timeout");
			elapsed += 0.016f;
		}
		label.Modulate = new Color(1, 1, 1, 1);
		label.Position = new Vector2(label.Position.X, endY);
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (@event is InputEventMouseButton mouseEvent && mouseEvent.Pressed)
		{
			GetTree().ChangeSceneToFile("res://battle_arena.tscn");
		}
		if (@event is InputEventKey keyEvent && keyEvent.Pressed)
		{
			GetTree().ChangeSceneToFile("res://battle_arena.tscn");
		}
	}
}
