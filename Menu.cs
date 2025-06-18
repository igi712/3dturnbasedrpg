using Godot;
using System;

public partial class Menu : Control
{
	private void _on_BtnSelectStage_pressed()
	{
		GetTree().ChangeSceneToFile("res://select_stage.tscn");
	}
	private void _on_BtnGuide_pressed()
	{
		GetTree().ChangeSceneToFile("res://guide.tscn");
	}
	private void _on_BtnExit_pressed()
	{
		GetTree().Quit();
	}
}
