namespace Godot.Scene;

using Godot;
using System;
public partial class Welcome: Control
{

	private void _on_BtnKarya1_pressed()
	{
		GetTree().ChangeSceneToFile("res://Scenes/Karya1.tscn");
	}

	private void _on_BtnKarya2_pressed()
	{
		GetTree().ChangeSceneToFile("res://Scenes/Karya2.tscn");
	}

	private void _on_BtnKarya3_pressed()
	{
		GetTree().ChangeSceneToFile("res://Scenes/Karya3.tscn");
	}

	private void _on_BtnKarya4_pressed()
	{
		GetTree().ChangeSceneToFile("res://Scenes/Karya4.tscn");
	}

	private void _on_BtnAbout_pressed()
	{
		GetTree().ChangeSceneToFile("res://Scenes/About.tscn");
	}

	private void _on_BtnGuide_pressed()
	{
		GetTree().ChangeSceneToFile("res://Scenes/Guide.tscn");
	}

	private void _on_BtnExit_pressed()
	{
		GetTree().Quit();
	}
}
