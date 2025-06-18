using Godot;
using System;

public partial class Guide : Control
{
	private void _on_BtnBack_pressed()
	{
		GetTree().ChangeSceneToFile("res://menu.tscn");
	}
}
