using Godot;
using System;
using System.Threading.Tasks;

public partial class Character : Node3D
{
	[Export] public string CharacterName { get; set; } = "Hero";
	[Export] public int MaxHp { get; set; } = 100;
	private int _currentHp;
	private Control _hpBarControl; // Store the root HPBar control
	private ProgressBar _hpBar;
	private PackedScene _hpBarScene;
	[Export] public int AttackPower { get; set; } = 10;
	[Export] public int DefensePower { get; set; } = 5;
	[Export] public int Speed { get; set; } = 10;
	[Export] public bool IsAlly { get; set; } = true;
	[Export] public int ActionValue { get; set; } = 0;
	// Tambahkan status lain jika perlu

	private bool _isPendingRemoval = false;

	public int CurrentHp
	{
		get => _currentHp;
		set
		{
			_currentHp = Mathf.Max(0, value);
			if (_hpBar != null)
				_hpBar.Value = _currentHp;
		}
	}

	public override void _Ready()
	{
		CurrentHp = MaxHp;
		// Instance HP bar from scene and add to HPBarsLayer
		_hpBarScene = GD.Load<PackedScene>("res://hp_bar.tscn");
		_hpBarControl = _hpBarScene.Instantiate<Control>();
		// Find the HPBarsLayer in the scene tree
		var hpBarsLayer = GetTree().Root.GetNodeOrNull<CanvasLayer>("BattleArena/HPBarsLayer");
		if (hpBarsLayer != null && _hpBarControl != null)
		{
			hpBarsLayer.AddChild(_hpBarControl);
			_hpBar = _hpBarControl.GetNodeOrNull<ProgressBar>("ProgressBar");
			if (_hpBar != null)
			{
				_hpBar.MaxValue = MaxHp;
				_hpBar.Value = CurrentHp;
			}
		}
		else
		{
			GD.PrintErr("HPBarsLayer or HPBarControl not found!");
		}

		// Set idle animation if AnimationTree exists
		var animTree = GetNodeOrNull<AnimationTree>("AnimationTree");
		if (animTree != null)
		{
			animTree.Active = true;
			var stateMachineObj = animTree.Get("parameters/playback");
			var stateMachine = stateMachineObj.AsGodotObject() as AnimationNodeStateMachinePlayback;
			if (stateMachine != null)
			{
				stateMachine.Travel("Idle");
			}
			else
			{
				GD.PrintErr($"AnimationTree found but no AnimationNodeStateMachinePlayback for {CharacterName}");
			}
		}
	}
	public override void _Process(double delta)
	{
		if (!IsInsideTree() || _isPendingRemoval)
			return;
		// Only update HP bar if the control is still valid and not disposed
		if (_hpBarControl == null || !_hpBarControl.IsInsideTree())
			return;
		var hpBarAnchor = GetNodeOrNull<Node3D>("HPBarAnchor");
		var camera = GetViewport().GetCamera3D();
		if (hpBarAnchor != null && camera != null)
		{
			Vector3 worldPos = hpBarAnchor.GlobalTransform.Origin;
			Vector2 screenPos = camera.UnprojectPosition(worldPos);
			_hpBarControl.GlobalPosition = screenPos;
		}
	}
	public void QueueHpBarFree()
	{
		if (_hpBarControl != null && _hpBarControl.IsInsideTree())
		{
			_hpBarControl.QueueFree();
		}
	}

	public void MarkForRemoval()
	{
		_isPendingRemoval = true;
		QueueHpBarFree();
		QueueFree();
	}

	public void TakeDamage(int damage)
	{
		int actualDamage = Mathf.Max(1, damage - DefensePower); // Minimal 1 damage
		CurrentHp -= actualDamage;
		GD.Print($"{CharacterName} takes {actualDamage} damage, HP: {CurrentHp}/{MaxHp}");
		if (CurrentHp <= 0 && !_isPendingRemoval)
		{
			GD.Print($"{CharacterName} has been defeated!");
			MarkForRemoval(); // Remove HP bar and character node when dead
			// Immediately notify BattleManager to remove from turn order and AllCharacters
			var battleManager = GetTree().Root.GetNodeOrNull<BattleManager>("BattleArena/BattleManager");
			battleManager?.OnCharacterDied(this);
		}
	}

	public async Task Attack(Character target)
	{
		GD.Print($"{CharacterName} attacks {target.CharacterName}!");
		var animTree = GetNodeOrNull<AnimationTree>("AnimationTree");
		var battleManager = GetTree().Root.GetNodeOrNull<BattleManager>("BattleArena/BattleManager");
		Vector3 originalPosition = GlobalTransform.Origin;

		if (CharacterName == "Knight" || CharacterName == "Rogue")
		{
			if (battleManager != null) battleManager.SetActionInProgress(true);
			if (animTree != null)
			{
				animTree.Active = true;
				var stateMachineObj = animTree.Get("parameters/playback");
				var stateMachine = stateMachineObj.AsGodotObject() as AnimationNodeStateMachinePlayback;
				if (stateMachine != null)
				{
					stateMachine.Travel("1H_Melee_Attack_Stab");
					await ToSignal(GetTree().CreateTimer(0.3f), "timeout");
					if (target != null)
					{
						Vector3 targetPos = target.GlobalTransform.Origin;
						Vector3 forward = target.GlobalTransform.Basis.Z.Normalized();
						float distance = 1.5f;
						GlobalTransform = new Transform3D(GlobalTransform.Basis, targetPos + forward * distance);
					}
					await ToSignal(GetTree().CreateTimer(0.4f), "timeout");
					stateMachine.Travel("Idle");
					GlobalTransform = new Transform3D(GlobalTransform.Basis, originalPosition);
				}
			}
			if (battleManager != null) battleManager.SetActionInProgress(false);
		}
		else if (CharacterName.StartsWith("Enemy")) // Skeleton
		{
			if (battleManager != null) battleManager.SetActionInProgress(true);
			if (animTree != null)
			{
				animTree.Active = true;
				var stateMachineObj = animTree.Get("parameters/playback");
				var stateMachine = stateMachineObj.AsGodotObject() as AnimationNodeStateMachinePlayback;
				if (stateMachine != null)
				{
					stateMachine.Travel("Unarmed_Melee_Attack_Punch_B");
					await ToSignal(GetTree().CreateTimer(0.3f), "timeout");
					if (target != null)
					{
						Vector3 targetPos = target.GlobalTransform.Origin;
						Vector3 forward = target.GlobalTransform.Basis.Z.Normalized();
						float distance = 1.5f;
						GlobalTransform = new Transform3D(GlobalTransform.Basis, targetPos + forward * distance);
					}
					await ToSignal(GetTree().CreateTimer(0.4f), "timeout");
					stateMachine.Travel("Idle");
					GlobalTransform = new Transform3D(GlobalTransform.Basis, originalPosition);
				}
			}
			if (battleManager != null) battleManager.SetActionInProgress(false);
		}
		else if (CharacterName == "Mage")
		{
			if (battleManager != null) battleManager.SetActionInProgress(true);
			if (animTree != null)
			{
				animTree.Active = true;
				var stateMachineObj = animTree.Get("parameters/playback");
				var stateMachine = stateMachineObj.AsGodotObject() as AnimationNodeStateMachinePlayback;
				if (stateMachine != null)
				{
					stateMachine.Travel("Spellcast_Shoot");
					await ToSignal(GetTree().CreateTimer(0.7f), "timeout");
					stateMachine.Travel("Idle");
				}
			}
			if (battleManager != null) battleManager.SetActionInProgress(false);
		}
		else if (animTree != null)
		{
			if (battleManager != null) battleManager.SetActionInProgress(true);
			animTree.Active = true;
			var stateMachineObj = animTree.Get("parameters/playback");
			var stateMachine = stateMachineObj.AsGodotObject() as AnimationNodeStateMachinePlayback;
			if (stateMachine != null)
			{
				stateMachine.Travel("BasicAttack");
				await ToSignal(GetTree().CreateTimer(0.7f), "timeout");
				stateMachine.Travel("Idle");
			}
			if (battleManager != null) battleManager.SetActionInProgress(false);
		}
		target.TakeDamage(AttackPower);
		return;
	}
}
