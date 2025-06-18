// BattleUI.cs
using Godot;
using System;
using System.Linq; // Diperlukan untuk FirstOrDefault

public partial class BattleUI : Control
{
	private VBoxContainer _turnOrderBar;
	private BattleManager _battleManager;

	// Target yang dipilih untuk diserang
	private Character _selectedTarget;

	[Export] public PackedScene CrosshairScene { get; set; }
	private Control _crosshairInstance;

	public override void _Ready()
	{
		// No AttackButton node anymore
		_turnOrderBar = GetNode<VBoxContainer>("TurnOrderBar");

		if (_turnOrderBar == null) GD.PrintErr("TurnOrderBar not found in BattleUI!");

		_battleManager = GetTree().Root.GetNode<BattleManager>("BattleArena/BattleManager");
		if (_battleManager != null)
		{
			var enemies = _battleManager.GetAliveEnemies();
			if (enemies.Count > 0)
			{
				_selectedTarget = enemies[0];
			}
			_battleManager.Connect(BattleManager.SignalName.NewTurnStarted, Callable.From(OnNewTurn));
			GD.Print("BattleUI connected to BattleManager's NewTurnStarted signal.");
			if (_battleManager.CurrentTurnCharacter != null)
			{
				UpdateTurnInfo();
			}
		}
		else
		{
			GD.PrintErr("BattleManager not found at path 'BattleArena/BattleManager' for BattleUI connection!");
		}
	}

	public void UpdateTurnInfo()
	{
		if (_battleManager == null || _battleManager.CurrentTurnCharacter == null)
			return;

		for (int i = _turnOrderBar.GetChildCount() - 1; i >= 0; i--)
		{
			_turnOrderBar.GetChild(i).QueueFree();
		}

		// Ambil urutan giliran dari BattleManager (akses internal _turnOrder via refleksi jika perlu)
		var turnOrder = typeof(BattleManager)
			.GetMethod("GetSortedTurnOrder", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
			.Invoke(_battleManager, null) as System.Collections.Generic.List<Character>;

		if (turnOrder == null) return;

		for (int i = 0; i < turnOrder.Count; i++)
		{
			var character = turnOrder[i];
			var label = new Label();
			if (character.ActionValue == 0)
			{
				label.Text = $"{character.CharacterName}";
				label.AddThemeColorOverride("font_color", new Color(1, 1, 0)); // Highlight kuning
				label.AddThemeFontSizeOverride("font_size", 22);
				label.Text = $"> {label.Text} <";
			}
			else
			{
				label.Text = $"{character.CharacterName} {character.ActionValue}";
			}
			_turnOrderBar.AddChild(label);
		}

		// Remove EnsureCrosshairVisibleIfPlayerTurn from here
	}

	// Metode ini akan dipanggil oleh sinyal
	public void OnNewTurn()
	{
		GD.Print("BattleUI.OnNewTurn() called by signal.");
		UpdateTurnInfo();
		GD.Print("Current turn character: " + _battleManager.CurrentTurnCharacter?.CharacterName);
		GD.Print("Is action in progress: " + _battleManager.IsActionInProgress);
		if (_battleManager.CurrentTurnCharacter != null && _battleManager.CurrentTurnCharacter.IsAlly && !_battleManager.IsActionInProgress)
		{
			// Always reset target selection at the start of player's turn
			ResetTargetSelection();
			GD.Print("Selected target: " + _selectedTarget?.CharacterName);
			if (_selectedTarget != null)
			{
				UpdateTargetCrosshair();
			}
			else
			{
				HideCrosshair();
			}
		}
		else
		{
			// For enemy turns, always hide crosshair and do not print or update _selectedTarget
			HideCrosshair();
		}
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (_battleManager == null || !_battleManager.CurrentTurnCharacter.IsAlly || _battleManager.IsActionInProgress)
			return;

		// Ensure _selectedTarget is always valid on player's turn
		if (_selectedTarget == null)
		{
			var enemies = _battleManager.GetAliveEnemies();
			if (enemies.Count > 0)
			{
				_selectedTarget = enemies[0];
				UpdateTargetCrosshair();
			}
		}

		if (@event is InputEventKey keyEvent && keyEvent.Pressed)
		{
			if (Input.IsActionJustPressed("basic_attack"))
			{
				if (_selectedTarget != null)
				{
					HideCrosshair(); // Hide crosshair immediately when action is ordered
					_battleManager.PlayerPerformAttack(_selectedTarget);
				}
			}
			else if (Input.IsActionJustPressed("target_left"))
			{
				SelectNextTarget(-1);
			}
			else if (Input.IsActionJustPressed("target_right"))
			{
				SelectNextTarget(1);
			}
			// For future: skill, ultimate_1, ultimate_2, ultimate_3
		}
	}

	private void SelectNextTarget(int direction)
	{
		var enemies = _battleManager.GetAliveEnemies();
		if (enemies.Count == 0) {
			HideCrosshair();
			return;
		}
		int idx = _selectedTarget != null ? enemies.IndexOf(_selectedTarget) : -1;
		if (idx == -1) idx = enemies.Count - 1; // Default to rightmost if no target
		// Reverse direction so left means leftmost visually
		idx = (idx - direction + enemies.Count) % enemies.Count;
		_selectedTarget = enemies[idx];
		UpdateTargetCrosshair();
	}

	private void UpdateTargetCrosshair()
	{
		// Only show crosshair if it's player's turn and not waiting for input
		bool isPlayerTurn = _battleManager.CurrentTurnCharacter != null && _battleManager.CurrentTurnCharacter.IsAlly && !_battleManager.IsActionInProgress;
		if (_selectedTarget != null && isPlayerTurn)
		{
			// Move crosshair to HurtEffectAnchor if available
			Node3D anchor = _selectedTarget.GetNodeOrNull<Node3D>("HurtEffectAnchor");
			if (anchor == null)
			{
				// Fallback to Skeleton, MeshInstance3D, or root
				anchor = _selectedTarget.GetNodeOrNull<Node3D>("Skeleton");
				if (anchor == null) anchor = _selectedTarget.GetNodeOrNull<Node3D>("MeshInstance3D");
				if (anchor == null) anchor = _selectedTarget;
			}
			var camera = GetViewport().GetCamera3D();
			if (anchor != null && camera != null)
			{
				Vector3 worldPos = anchor.GlobalTransform.Origin;
				Vector2 screenPos = camera.UnprojectPosition(worldPos);
				ShowCrosshair(screenPos);
				GD.Print($"Crosshair updated for target {_selectedTarget.CharacterName} at {screenPos}");
				return;
			}
		}
		HideCrosshair();
		GD.Print("Crosshair hidden because no valid target or not player's turn.");
	}

	public void ShowCrosshair(Vector2 screenPos)
	{
		if (_crosshairInstance == null)
		{
			if (CrosshairScene == null)
			{
				GD.PrintErr("CrosshairScene not assigned in the inspector!");
				return;
			}
			_crosshairInstance = CrosshairScene.Instantiate<Control>();
			var hpBarsLayer = GetTree().Root.GetNodeOrNull<CanvasLayer>("BattleArena/HPBarsLayer");
			if (hpBarsLayer != null)
				hpBarsLayer.AddChild(_crosshairInstance);
		}
		// Only show if player's turn and not waiting
		bool isPlayerTurn = _battleManager != null && _battleManager.CurrentTurnCharacter != null && _battleManager.CurrentTurnCharacter.IsAlly && !_battleManager.IsActionInProgress;
		_crosshairInstance.Visible = isPlayerTurn;
		_crosshairInstance.GlobalPosition = screenPos;
		GD.Print($"Crosshair shown at {screenPos} for target {_selectedTarget?.CharacterName}");
	}
	public void HideCrosshair()
	{
		if (_crosshairInstance != null)
			_crosshairInstance.Visible = false;
		GD.Print("Crosshair hidden.");
	}

	// Call this to reset the selected target after an enemy dies
	public void ResetTargetSelection()
	{
		if (_battleManager == null) return;

		var enemies = _battleManager.GetAliveEnemies();

		if (enemies.Count == 0)
		{
			_selectedTarget = null;
			HideCrosshair();
			GD.Print("No alive enemies left, target set to null.");
			return;
		}

		// Check if the current target is invalid (has been freed) or is dead.
		// IsInstanceValid is crucial for checking objects that might have been QueueFree'd.
		if (!IsInstanceValid(_selectedTarget) || _selectedTarget.CurrentHp <= 0)
		{
			// If the target is invalid, select the first living enemy as the new default.
			_selectedTarget = enemies[0];
			GD.Print($"Previous target was invalid. Resetting to first available enemy: {_selectedTarget.CharacterName}");
		}
		// If the target is still valid and alive, we do nothing, preserving the player's last selection.

		// Finally, ensure the crosshair is visible and correctly positioned.
		UpdateTargetCrosshair();
	}
}
