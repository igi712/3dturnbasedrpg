using Godot;
using System;

public partial class SelectStage : Control
{
	public static int StageIndex = 1; // 1 or 2
	private void _on_BtnStage1_pressed()
	{
		StageIndex = 1;
		GetTree().ChangeSceneToFile("res://narration.tscn");
	}
	private void _on_BtnStage2_pressed()
	{
		StageIndex = 2;
		GetTree().ChangeSceneToFile("res://narration.tscn");
	}
	private void _on_BtnBack_pressed()
	{
		GetTree().ChangeSceneToFile("res://menu.tscn");
	}
}
