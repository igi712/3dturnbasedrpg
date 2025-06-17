// BattleManager.cs
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class BattleManager : Node
{
	// 1. Deklarasikan sinyal
	// Nama delegate bisa apa saja, tapi konvensinya diakhiri dengan EventHandler
	// Sinyal ini tidak membawa data tambahan, jadi tidak ada parameter di delegate.
	[Signal]
	public delegate void NewTurnStartedEventHandler();

	[Export] public Godot.Collections.Array<Character> AllCharacters { get; set; } = new Godot.Collections.Array<Character>();
	private List<Character> _turnOrder;

	public Character CurrentTurnCharacter => (_turnOrder != null && _turnOrder.Count > 0) ? _turnOrder[0] : null;

	private List<Character> Allies => AllCharacters.Where(c => c != null && c.IsAlly).ToList();
	private List<Character> Enemies => AllCharacters.Where(c => c != null && !c.IsAlly).ToList();

	private const int AV_BASE = 10000;

	public bool IsActionInProgress { get; private set; } = true;

	public override void _Ready()
	{
		AllCharacters.Clear();
		// Initial spawn (positions will be set dynamically)
		SpawnCharacter("Mage", true, Vector3.Zero, 120, 15, 8, 120);
		SpawnCharacter("Rogue", true, Vector3.Zero, 100, 18, 5, 125);
		SpawnCharacter("Knight", true, Vector3.Zero, 150, 12, 12, 80);
		SpawnCharacter("Enemy1", false, Vector3.Zero, 10, 10, 4, 100);
		SpawnCharacter("Enemy2", false, Vector3.Zero, 110, 13, 6, 90);
		SpawnCharacter("Enemy3", false, Vector3.Zero, 100, 12, 5, 95);
		SpawnCharacter("Enemy4", false, Vector3.Zero, 120, 14, 7, 105);
		InitializeActionValues();
		UpdateCharacterPositions(); // Initial positioning
		StartBattle();
	}

	private string GetCharacterScenePath(string name)
	{
		switch (name)
		{
			case "Rogue": return "res://player_rogue.tscn";
			case "Mage": return "res://player_mage.tscn";
			case "Knight": return "res://player_knight.tscn";
			case "Enemy1": return "res://enemy_skeleton_minion.tscn";
			case "Enemy2": return "res://enemy_skeleton_minion.tscn";
			case "Enemy3": return "res://enemy_skeleton_minion.tscn";
			case "Enemy4": return "res://enemy_skeleton_minion.tscn";
			default: return "res://character.tscn";
		}
	}

	private void SpawnCharacter(string name, bool isAlly, Vector3 position, int maxHp = 100, int attack = 10, int defense = 5, int speed = 10)
	{
		var scenePath = GetCharacterScenePath(name);
		var packedScene = GD.Load<PackedScene>(scenePath);
		var instance = packedScene.Instantiate<Character>();
		instance.CharacterName = name;
		instance.IsAlly = isAlly;
		instance.MaxHp = maxHp;
		instance.AttackPower = attack;
		instance.DefensePower = defense;
		instance.Speed = speed;
		instance.Position = position;
		// Make enemies face the allies (rotate 180 degrees on Y)
		if (!isAlly)
		{
			instance.Rotation = new Vector3(0, Mathf.Pi, 0);
		}
		AddChild(instance); // Atau ke node khusus jika ada
		AllCharacters.Add(instance);
	}

	private void InitializeActionValues()
	{
		foreach (var c in AllCharacters)
		{
			c.ActionValue = (int)Math.Round((double)AV_BASE / c.Speed);
		}
	}

	private void UpdateActionValuesAfterTurn(Character justActed)
	{
		// Set acting character's AV to their Base AV
		justActed.ActionValue = (int)Math.Round((double)AV_BASE / justActed.Speed);

		// Find the minimum AV among all alive characters
		var alive = AllCharacters.Where(c => c.CurrentHp > 0).ToList();
		if (alive.Count > 0)
		{
			int minAV = alive.Min(c => c.ActionValue);
			// Subtract minAV from all alive characters' AVs
			foreach (var c in alive)
			{
				c.ActionValue -= minAV;
			}
		}
	}

	private List<Character> GetSortedTurnOrder()
	{
		return AllCharacters.Where(c => c.CurrentHp > 0).OrderBy(c => c.ActionValue).ThenByDescending(c => c.Speed).ToList();
	}

	public void StartBattle()
	{
		if (AllCharacters.Count == 0)
		{
			GD.PrintErr("No characters in battle!");
			return;
		}
		_turnOrder = GetSortedTurnOrder();
		// Normalize AV so first turn character has AV=0
		if (_turnOrder.Count > 0)
		{
			int minAV = _turnOrder.Min(c => c.ActionValue);
			if (minAV != 0)
			{
				foreach (var c in _turnOrder)
				{
					c.ActionValue -= minAV;
				}
			}
		}
		PrintTurnOrder();
		if (_turnOrder.Count > 0)
		{
			StartNextTurn();
		}
		else
		{
			GD.Print("No characters left to start battle or in turn order.");
		}
	}

	private void PrintTurnOrder()
	{
		GD.Print("Turn order:");
		foreach (var character in GetSortedTurnOrder())
		{
			GD.Print($"- {character.CharacterName} (Speed: {character.Speed}) AV: {character.ActionValue}");
		}
	}

	public async void StartNextTurn()
	{
		IsActionInProgress = false;
		_turnOrder = GetSortedTurnOrder();
		if (_turnOrder.Count == 0 || CheckBattleEnd()) return;
		// Normalize AV so current turn character always has AV=0
		if (CurrentTurnCharacter != null && CurrentTurnCharacter.ActionValue != 0)
		{
			int offset = CurrentTurnCharacter.ActionValue;
			foreach (var c in _turnOrder)
			{
				c.ActionValue -= offset;
			}
			// Update turn order after normalization
			_turnOrder = GetSortedTurnOrder();
		}
		PrintTurnOrder();
		GD.Print($"--- {CurrentTurnCharacter.CharacterName}'s Turn (AV: {CurrentTurnCharacter.ActionValue}) ---");
		EmitSignal(SignalName.NewTurnStarted);

		if (!CurrentTurnCharacter.IsAlly)
		{
			IsActionInProgress = true;
			Character allyTarget = _turnOrder.FirstOrDefault(c => c.IsAlly && c.CurrentHp > 0);
			if (allyTarget != null)
			{
				await CurrentTurnCharacter.Attack(allyTarget);
			}
			EndTurn();
		}
		else
		{
			GD.Print("Player's turn. Waiting for action...");
		}
	}

	public async void PlayerPerformAttack(Character target)
	{
		if (CurrentTurnCharacter != null && CurrentTurnCharacter.IsAlly && CurrentTurnCharacter.CurrentHp > 0 && !IsActionInProgress)
		{
			// Defensive: ensure target is alive and valid
			if (target == null || !target.IsInsideTree() || target.CurrentHp <= 0)
			{
				// Try to pick a new valid target
				var enemies = GetAliveEnemies();
				if (enemies.Count > 0)
				{
					GD.Print($"[PlayerPerformAttack] Target was invalid. Retargeting to {enemies[0].CharacterName}");
					target = enemies[0];
				}
				else
				{
					GD.Print("[PlayerPerformAttack] No valid targets to attack.");
					return;
				}
			}
			IsActionInProgress = true;
			await CurrentTurnCharacter.Attack(target);
			EndTurn();
		}
	}

	private void UpdateCharacterPositions()
	{
		// Allies
		var livingAllies = AllCharacters.Where(c => c != null && c.IsAlly && c.CurrentHp > 0).ToList();
		float allyZ = -7f;
		float allySpacing = 5.0f; // Adjust for desired spread
		int allyCount = livingAllies.Count;
		if (allyCount > 0)
		{
			float allyStartX = -(allySpacing * (allyCount - 1)) / 2f;
			for (int i = 0; i < allyCount; i++)
			{
				var c = livingAllies[i];
				c.Position = new Vector3(allyStartX + i * allySpacing, 0, allyZ);
			}
		}

		// Enemies
		var livingEnemies = AllCharacters.Where(c => c != null && !c.IsAlly && c.CurrentHp > 0).ToList();
		float enemyZ = 7f;
		float enemySpacing = 5.0f; // Adjust for desired spread
		int enemyCount = livingEnemies.Count;
		if (enemyCount > 0)
		{
			float enemyStartX = -(enemySpacing * (enemyCount - 1)) / 2f;
			for (int i = 0; i < enemyCount; i++)
			{
				var c = livingEnemies[i];
				c.Position = new Vector3(enemyStartX + i * enemySpacing, 0, enemyZ);
			}
		}
	}

	private void RemoveDeadCharacters()
	{
		// Remove from turn order as well as AllCharacters
		var deadCharacters = AllCharacters.Where(c => c != null && c.CurrentHp <= 0).ToList();
		bool enemyDied = false;
		foreach (var c in deadCharacters)
		{
			if (c == null) continue;
			if (!c.IsQueuedForDeletion())
			{
				if (!c.IsAlly) enemyDied = true;
				c.QueueFree();
			}
			_turnOrder?.Remove(c); // Remove from turn order to prevent softlock
		}
		// Remove after loop to avoid modifying collection while iterating
		foreach (var c in deadCharacters)
		{
			AllCharacters.Remove(c);
		}
	}

	public void EndTurn()
	{
		RemoveDeadCharacters(); // Remove dead characters before updating positions/turn order
		UpdateActionValuesAfterTurn(CurrentTurnCharacter);
		_turnOrder = GetSortedTurnOrder();
		_turnOrder.RemoveAll(c => c.CurrentHp <= 0);
		UpdateCharacterPositions();
		if (CheckBattleEnd()) return;
		if (_turnOrder.Count > 0)
		{
			StartNextTurn();
		}
		else
		{
			GD.Print("No characters left in turn order after EndTurn cleanup.");
			CheckBattleEnd();
		}
	}
	
	public bool CheckBattleEnd()
	{
		if (_turnOrder == null) return false;

		bool allAlliesDefeated = !_turnOrder.Any(c => c != null && c.IsAlly && c.CurrentHp > 0);
		bool allEnemiesDefeated = !_turnOrder.Any(c => c != null && !c.IsAlly && c.CurrentHp > 0);

		if (allAlliesDefeated && _turnOrder.Any(c => c != null && !c.IsAlly))
		{
			GD.Print("Game Over! All allies defeated.");
			return true;
		}
		if (allEnemiesDefeated && _turnOrder.Any(c => c != null && c.IsAlly))
		{
			GD.Print("Victory! All enemies defeated.");
			return true;
		}
		if (_turnOrder.Count(c => c != null && c.CurrentHp > 0) <= 1 && AllCharacters.Count > 1) {
			if (!_turnOrder.Any(c => c != null && c.IsAlly && c.CurrentHp > 0) &&
				!_turnOrder.Any(c => c != null && !c.IsAlly && c.CurrentHp > 0) &&
				AllCharacters.Count > 0) {
				GD.Print("Draw or all defeated!");
				return true;
			}
		}
		return false;
	}

	// Returns a list of alive enemy characters
	public System.Collections.Generic.List<Character> GetAliveEnemies()
	{
		return AllCharacters.Where(c => c != null && !c.IsAlly && c.CurrentHp > 0).ToList();
	}

	// Add a public method to set IsActionInProgress
	public void SetActionInProgress(bool value)
	{
		IsActionInProgress = value;
	}

	public void OnCharacterDied(Character c)
	{
		if (c == null) return;
		if (!c.IsQueuedForDeletion())
		{
			c.QueueFree();
		}
		_turnOrder?.Remove(c);
		AllCharacters.Remove(c);
		UpdateCharacterPositions();
		if (!c.IsAlly)
		{
			var ui = GetNodeOrNull<BattleUI>("/root/BattleUI");
			ui?.ResetTargetSelection();
		}
	}
}

// Camera setup suggestion (not code, but for your scene):
// Place a Camera3D at e.g. (0, 6, -12), rotation looking at (0, 0, 4) or (0, 0, 0)
// This will frame the allies in the foreground and enemies in the background, both centered.
