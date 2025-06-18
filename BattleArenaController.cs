using Godot;
using System;

public partial class BattleArenaController : Node
{
	public override void _Ready()
	{
		// Check which stage is selected
		int stage = 1;
		// Use static property directly
		stage = SelectStage.StageIndex;

		if (stage == 2)
		{
			// Adjust fog to black and reduce DirectionalLight3D intensity
			var worldEnv = GetNodeOrNull<WorldEnvironment>("/root/BattleArena/WorldEnvironment");
			if (worldEnv != null)
			{
				var env = worldEnv.Environment;
				if (env != null)
				{
					env.FogLightColor = new Color(0,0,0);
					env.FogDensity = 1.5f;
				}
			}
			var dirLight = GetNodeOrNull<DirectionalLight3D>("/root/BattleArena/DirectionalLight3D");
			if (dirLight != null)
			{
				dirLight.LightEnergy = 0.2f;
			}
			/*
			// Spawn 5 bosses
			var battleManager = GetNodeOrNull<BattleManager>("/root/BattleArena/BattleManager");
			if (battleManager != null)
			{
				for (int i = 0; i < 5; i++)
				{
					battleManager.SpawnCharacter($"Boss{i+1}", false, Vector3.Zero, 200, 20, 10, 80);
				}
				battleManager.UpdateCharacterPositions();
			}
			*/
		}
	}
}
