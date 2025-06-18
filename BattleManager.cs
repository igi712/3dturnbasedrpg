// BattleManager: Manages turn order, character actions, and battle flow for the 3D turn-based RPG system.
// Handles dynamic turn order, AV normalization, character instancing, camera logic, and robust cleanup.
// Inspired by Honkai Star Rail turn system.
//
// Key exported properties:
// - AllySpacing, EnemySpacing, AllyZ, EnemyZ: Layout for character positions
// - AllyCameraOffset, AllyCameraRotation, EnemyCameraPosition, EnemyCameraRotation: Camera settings
//
// Main methods:
// - StartBattle: Initializes and starts the battle
// - StartNextTurn: Advances to the next character's turn
// - PlayerPerformAttack: Handles player attack input and validation
// - EndTurn: Cleans up and advances turn order
// - OnCharacterDied: Robustly removes dead characters and updates UI
//
// See GDD for design details and expansion points.

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

	private const int AV_BASE = 10000;

	public bool IsActionInProgress { get; private set; } = true;

	private Camera3D _battleCamera;

	// Exported fields for spacing, Z positions, and camera offsets/rotations
	[Export] public float AllySpacing { get; set; } = 5.0f;
	[Export] public float EnemySpacing { get; set; } = 5.0f;
	[Export] public float AllyZ { get; set; } = -7f;
	[Export] public float EnemyZ { get; set; } = 7f;
	[Export] public Vector3 AllyCameraOffset { get; set; } = new Vector3(-3f, 2f, -2f);
	[Export] public Vector3 AllyCameraRotation { get; set; } = new Vector3(-10, -165, 0);
	[Export] public Vector3 EnemyCameraPosition { get; set; } = new Vector3(0, 4, -14);
	[Export] public Vector3 EnemyCameraRotation { get; set; } = new Vector3(0, -180, 0);

	private Label _endOverlayLabel;
	private bool _battleEnded = false;

	public override void _Ready()
	{
		AllCharacters.Clear();
		// Find the battle camera in the scene
		_battleCamera = GetTree().Root.GetNodeOrNull<Camera3D>("BattleArena/Camera3D");
		// Initial spawn (positions will be set dynamically)
		SpawnCharacter("Mage", true, Vector3.Zero, 100, 15, 8, 120);
		SpawnCharacter("Rogue", true, Vector3.Zero, 100, 18, 5, 125);
		SpawnCharacter("Knight", true, Vector3.Zero, 100, 12, 12, 85);
		SpawnCharacter("Enemy1", false, Vector3.Zero, 30, 10, 4, 100);
		SpawnCharacter("Enemy2", false, Vector3.Zero, 30, 13, 6, 90);
		SpawnCharacter("Boss", false, Vector3.Zero, 100, 20, 10, 80); // Example for a boss
		SpawnCharacter("Enemy3", false, Vector3.Zero, 30, 12, 5, 95);
		SpawnCharacter("Enemy4", false, Vector3.Zero, 30, 14, 7, 105);
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
			case "Boss": return "res://enemy_skeleton_warrior.tscn";
			case "Enemy3": return "res://enemy_skeleton_minion.tscn";
			case "Enemy4": return "res://enemy_skeleton_minion.tscn";
			default: return "res://character.tscn";
		}
	}

	public void SpawnCharacter(string name, bool isAlly, Vector3 position, int maxHp = 100, int attack = 10, int defense = 5, int speed = 10)
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

	private void NormalizeActionValues(IEnumerable<Character> characters)
	{
		if (characters == null) return;
		var list = characters.Where(c => c != null).ToList();
		if (list.Count == 0) return;
		int minAV = list.Min(c => c.ActionValue);
		if (minAV != 0)
		{
			foreach (var c in list)
			{
				c.ActionValue -= minAV;
			}
		}
	}

	private void UpdateActionValuesAfterTurn(Character justActed)
	{
		// Set acting character's AV to their Base AV
		justActed.ActionValue = (int)Math.Round((double)AV_BASE / justActed.Speed);

		// Find the minimum AV among all alive characters and normalize
		var alive = AllCharacters.Where(c => c.CurrentHp > 0).ToList();
		NormalizeActionValues(alive);
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
		NormalizeActionValues(_turnOrder);
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

	private List<Character> GetAllies()
	{
		return AllCharacters.Where(c => c != null && c.IsAlly).ToList();
	}

	private List<Character> GetEnemies()
	{
		return AllCharacters.Where(c => c != null && !c.IsAlly).ToList();
	}

	public void SetCameraForTurn()
	{
		if (_battleCamera == null) return;
		if (CurrentTurnCharacter == null)
			return;

		// Always reset character positions to layout default before any translation
		UpdateCharacterPositions();

		if (CurrentTurnCharacter.IsAlly)
		{
			// Tidak perlu menggeser posisi allies, cukup offset kamera ke acting character
			_battleCamera.Position = CurrentTurnCharacter.Position + AllyCameraOffset;
			_battleCamera.RotationDegrees = AllyCameraRotation;
		}
		else if (CurrentTurnCharacter.CharacterName == "Boss")
		{
			// Neutral camera for Boss
			_battleCamera.Position = new Vector3(0, 2, -2.5f);
			_battleCamera.RotationDegrees = new Vector3(-10, 0, 0);
		}
		else
		{
			// Neutral/enemy turn camera
			_battleCamera.Position = EnemyCameraPosition;
			_battleCamera.RotationDegrees = EnemyCameraRotation;
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
			NormalizeActionValues(_turnOrder);
			// Update turn order after normalization
			_turnOrder = GetSortedTurnOrder();
		}
		PrintTurnOrder();
		GD.Print($"--- {CurrentTurnCharacter.CharacterName}'s Turn (AV: {CurrentTurnCharacter.ActionValue}) ---");
		SetCameraForTurn(); // Set camera position/rotation for this turn
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

	private Character GetValidAttackTarget(Character target)
	{
		if (target != null && target.IsInsideTree() && target.CurrentHp > 0)
			return target;
		var enemies = GetAliveEnemies();
		if (enemies.Count > 0)
		{
			GD.Print($"[PlayerPerformAttack] Target was invalid. Retargeting to {enemies[0].CharacterName}");
			return enemies[0];
		}
		GD.Print("[PlayerPerformAttack] No valid targets to attack.");
		return null;
	}

	public async void PlayerPerformAttack(Character target)
	{
		if (CurrentTurnCharacter != null && CurrentTurnCharacter.IsAlly && CurrentTurnCharacter.CurrentHp > 0 && !IsActionInProgress)
		{
			target = GetValidAttackTarget(target);
			if (target == null)
				return;
			IsActionInProgress = true;
			await CurrentTurnCharacter.Attack(target);
			EndTurn();
		}
	}

	public void UpdateCharacterPositions()
	{
		// Allies
		var livingAllies = AllCharacters.Where(c => c != null && c.IsAlly && c.CurrentHp > 0).ToList();
		float allyZ = AllyZ;
		float allySpacing = AllySpacing;
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
		float enemyZ = EnemyZ;
		float enemySpacing = EnemySpacing;
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
		// Remove from turn order as well as AllCharacters, using OnCharacterDied for consistency
		var deadCharacters = AllCharacters.Where(c => c != null && c.CurrentHp <= 0).ToList();
		foreach (var c in deadCharacters)
		{
			OnCharacterDied(c);
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

		if (allAlliesDefeated)
		{
			ShowEndOverlay("KALAH! Semua pawang gugur.\nTekan tombol apa saja untuk kembali ke menu.");
			return true;
		}
		if (allEnemiesDefeated)
		{
			ShowEndOverlay("MENANG! Desa Waktosa selamat.\nTekan tombol apa saja untuk kembali ke menu.");
			return true;
		}
		if (_turnOrder.Count(c => c != null && c.CurrentHp > 0) <= 1 && AllCharacters.Count > 1) {
			if (!_turnOrder.Any(c => c != null && c.IsAlly && c.CurrentHp > 0) &&
				!_turnOrder.Any(c => c != null && !c.IsAlly && c.CurrentHp > 0) &&
				AllCharacters.Count > 0) {
				ShowEndOverlay("Seri! Semua gugur.\nTekan tombol apa saja untuk kembali ke menu.");
				return true;
			}
		}
		return false;
	}

	private void ShowEndOverlay(string text)
	{
		if (_battleEnded) return;
		_battleEnded = true;
		_endOverlayLabel = new Label();
		_endOverlayLabel.Text = text;
		_endOverlayLabel.Modulate = new Color(0.1f, 0.1f, 0.1f, 0.85f);
		_endOverlayLabel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		_endOverlayLabel.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
		_endOverlayLabel.HorizontalAlignment = HorizontalAlignment.Center;
		_endOverlayLabel.VerticalAlignment = VerticalAlignment.Center;
		_endOverlayLabel.AddThemeFontSizeOverride("font_size", 54);
		_endOverlayLabel.AddThemeColorOverride("font_outline_color", Colors.Black);
		_endOverlayLabel.AddThemeConstantOverride("outline_size", 3);
		_endOverlayLabel.AddThemeColorOverride("font_color", Colors.White);
		_endOverlayLabel.Position = new Vector2(300, 300);
		AddChild(_endOverlayLabel);
		SetProcessUnhandledInput(true);
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (!_battleEnded) return;
		if ((@event is InputEventMouseButton mouseEvent && mouseEvent.Pressed) ||
			(@event is InputEventKey keyEvent && keyEvent.Pressed))
		{
			GetTree().ChangeSceneToFile("res://menu.tscn");
		}
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

	public async void OnCharacterDied(Character c)
	{
		if (c == null) return;
		if (c.IsAlly)
		{
			GD.Print($"Ally {c.CharacterName} has died.");
			// Optionally, play a death animation or sound here
		}
		else
		{
			GD.Print($"Enemy {c.CharacterName} has died.");
		}
		// Remove the character from the battle
		c.QueueFree();
		AllCharacters.Remove(c);
		RemoveDeadCharacters(); // Ensure dead characters are removed from turn order
		_turnOrder = GetSortedTurnOrder();
		_turnOrder.RemoveAll(c => c.CurrentHp <= 0);
		UpdateCharacterPositions();
		CheckBattleEnd(); // Check if the battle has ended after a character dies
	}
}
