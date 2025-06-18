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
		// Do not QueueFree here; handled after death animation
	}

	public async Task PlayDeathAnimationAndRemove()
	{
		var animTree = GetNodeOrNull<AnimationTree>("AnimationTree");
		if (animTree != null)
		{
			animTree.Active = true;
			var stateMachineObj = animTree.Get("parameters/playback");
			var stateMachine = stateMachineObj.AsGodotObject() as AnimationNodeStateMachinePlayback;
			if (stateMachine != null)
			{
				stateMachine.Travel("Death_A");
				await ToSignal(GetTree().CreateTimer(1.0f), "timeout"); // Adjust duration as needed
			}
		}
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
			MarkForRemoval(); // Remove HP bar and mark for removal
			// Notify BattleManager to play death animation and remove after
			var battleManager = GetTree().Root.GetNodeOrNull<BattleManager>("BattleArena/BattleManager");
			battleManager?.OnCharacterDied(this);
		}
	}

	public async Task Attack(Character target)
	{
		GD.Print($"{CharacterName} attacks {target.CharacterName}!");
		var animTree = GetNodeOrNull<AnimationTree>("AnimationTree");
		var battleManager = GetTree().Root.GetNodeOrNull<BattleManager>("BattleArena/BattleManager");
		var camera = GetTree().Root.GetNodeOrNull<Camera3D>("BattleArena/Camera3D");
		Vector3 originalPosition = GlobalTransform.Origin;
		Vector3 originalCamPos = camera != null ? camera.Position : Vector3.Zero;
		Vector3 originalCamRot = camera != null ? camera.RotationDegrees : Vector3.Zero;

		if ((CharacterName == "Knight" || CharacterName == "Rogue") && animTree != null && camera != null)
		{
			if (battleManager != null) battleManager.SetActionInProgress(true);
			animTree.Active = true;
			var stateMachineObj = animTree.Get("parameters/playback");
			var stateMachine = stateMachineObj.AsGodotObject() as AnimationNodeStateMachinePlayback;
			if (stateMachine != null)
			{
				// 1. Move camera to left of character, look right
				camera.Position = GlobalTransform.Origin + new Vector3(-4, 2, 0);
				camera.RotationDegrees = new Vector3(0, -90, 0);

				// 2. Start Jump_Forward animation
				stateMachine.Travel("Jump_Forward");
				await ToSignal(GetTree().CreateTimer(0.3f), "timeout");

				// 3. Teleport character in front of enemy
				if (target != null)
				{
					Vector3 targetPos = target.GlobalTransform.Origin;
					Vector3 forward = target.GlobalTransform.Basis.Z.Normalized();
					float distance = 1.5f;
					GlobalTransform = new Transform3D(GlobalTransform.Basis, targetPos + forward * distance);
				}

				// 4. Move camera to diagonal POV
				camera.Position = GlobalTransform.Origin + new Vector3(-2.5f, 1.5f, -1.5f);
				camera.RotationDegrees = new Vector3(-10, -135, 0);

				// 5. Jump_Forward_Idle
				stateMachine.Travel("Jump_Forward_Idle");
				await ToSignal(GetTree().CreateTimer(0.3f), "timeout");

				// 6. Jump_Forward_Land
				stateMachine.Travel("Jump_Forward_Land");
				await ToSignal(GetTree().CreateTimer(0.6f), "timeout");

				// 7. Attack animation (stab)
				stateMachine.Travel("Basic_Attack");
				await ToSignal(GetTree().CreateTimer(0.25f), "timeout");

				// Play enemy hit or death animation and reduce HP after attack animation
				if (target != null)
				{
					var targetAnimTree = target.GetNodeOrNull<AnimationTree>("AnimationTree");
					target.TakeDamage(AttackPower);
					target.PlayHurtEffect(this);
					if (targetAnimTree != null)
					{
						targetAnimTree.Active = true;
						var targetStateMachineObj = targetAnimTree.Get("parameters/playback");
						var targetStateMachine = targetStateMachineObj.AsGodotObject() as AnimationNodeStateMachinePlayback;
						if (targetStateMachine != null)
						{
							// If target is dead, play Death_A immediately (override Hit_A)
							if (target.CurrentHp <= 0)
							{
								targetStateMachine.Travel("Death_A");
								await ToSignal(GetTree().CreateTimer(1.0f), "timeout");
							}
							else
							{
								targetStateMachine.Travel("Hit_A");
								await ToSignal(GetTree().CreateTimer(0.6f), "timeout");
								targetStateMachine.Travel("Idle");
							}
						}
					}
				}
				//await ToSignal(GetTree().CreateTimer(0.55f), "timeout");

				// 8. Play Jump_Forward_Land in reverse (simulate jump back)
				stateMachine.Travel("Jump_Forward_Land_Reverse");
				await ToSignal(GetTree().CreateTimer(0.3f), "timeout");
				// 9. Play Jump_Forward_Idle in reverse
				stateMachine.Travel("Jump_Forward_Idle_Reverse");
				await ToSignal(GetTree().CreateTimer(0.3f), "timeout");

				// Return to Idle
				stateMachine.Travel("Idle");
				GlobalTransform = new Transform3D(GlobalTransform.Basis, originalPosition);
				camera.Position = originalCamPos;
				camera.RotationDegrees = originalCamRot;
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
					// 1. Move camera to cinematic position relative to enemy BEFORE teleport
					if (camera != null)
					{
						camera.Position = GlobalTransform.Origin + new Vector3(-2.5f, 1.5f, -1.5f);
						camera.RotationDegrees = new Vector3(-10, -135, 0);
					}


					stateMachine.Travel("Basic_Attack");
					await ToSignal(GetTree().CreateTimer(0.3f), "timeout");
					if (target != null)
					{
						// 2. Teleport enemy in front of target
						Vector3 targetPos = target.GlobalTransform.Origin;
						Vector3 forward = target.GlobalTransform.Basis.Z.Normalized();
						float distance = 1.5f;
						GlobalTransform = new Transform3D(GlobalTransform.Basis, targetPos + forward * distance);
						// Move camera to cinematic position relative to ally target
						if (camera != null)
						{
							camera.Position = target.GlobalTransform.Origin + new Vector3(-2.5f, 1.5f, -1.5f);
							camera.RotationDegrees = new Vector3(-10, -135, 0);
						}
						// Play hurt animation and particle on the target
						var targetAnimTree = target.GetNodeOrNull<AnimationTree>("AnimationTree");
						target.TakeDamage(AttackPower);
						target.PlayHurtEffect(this);
						if (targetAnimTree != null)
						{
							targetAnimTree.Active = true;
							var targetStateMachineObj = targetAnimTree.Get("parameters/playback");
							var targetStateMachine = targetStateMachineObj.AsGodotObject() as AnimationNodeStateMachinePlayback;
							if (targetStateMachine != null)
							{
								if (target.CurrentHp <= 0)
								{
									targetStateMachine.Travel("Death_A");
									await ToSignal(GetTree().CreateTimer(1.0f), "timeout");
								}
								else
								{
									targetStateMachine.Travel("Hit_A");
									await ToSignal(GetTree().CreateTimer(0.6f), "timeout");
									targetStateMachine.Travel("Idle");
								}
							}
						}
					}
					await ToSignal(GetTree().CreateTimer(0.4f), "timeout");
					if (camera != null)
					{
						camera.Position = originalCamPos;
						camera.RotationDegrees = originalCamRot;
					}
					stateMachine.Travel("Idle");
					GlobalTransform = new Transform3D(GlobalTransform.Basis, originalPosition);
				}
			}
			if (battleManager != null) battleManager.SetActionInProgress(false);
		}
		else if (CharacterName == "Mage" && animTree != null && camera != null)
		{
			if (battleManager != null) battleManager.SetActionInProgress(true);
			animTree.Active = true;
			var stateMachineObj = animTree.Get("parameters/playback");
			var stateMachine = stateMachineObj.AsGodotObject() as AnimationNodeStateMachinePlayback;
			if (stateMachine != null)
			{
				// Move camera to cinematic mage attack position
				camera.Position = GlobalTransform.Origin + new Vector3(-3f, 1.5f, -1.0f);
				camera.RotationDegrees = new Vector3(0, -165, 0);

				stateMachine.Travel("Basic_Attack");
				await ToSignal(GetTree().CreateTimer(0.3f), "timeout");

				// Spawn projectile at staff gem and shoot toward target (after 0.3s)
				if (target != null)
				{
					// Use the Stone mesh as the projectile spawn point for more accurate VFX
					var staffGem = GetNodeOrNull<Node3D>("Rig/Skeleton3D/2H_Staff/2H_Staff/Stone");
					if (staffGem == null)
					{
						// Fallback to staff if Stone mesh is missing
						staffGem = GetNodeOrNull<Node3D>("Rig/Skeleton3D/2H_Staff/2H_Staff");
					}
					if (staffGem != null)
					{
						var projectileScene = GD.Load<PackedScene>("res://projectile_magic.tscn");
						var projectile = projectileScene.Instantiate<Node3D>();
						projectile.GlobalTransform = new Transform3D(staffGem.GlobalTransform.Basis, staffGem.GlobalTransform.Origin + new Vector3(0, 0.1f, 0));
						GetTree().CurrentScene.AddChild(projectile);
						// Set target position, target character, and damage for projectile
						if (projectile.HasMethod("Launch"))
						{
							// Raise the target position's Y for better visual impact
							Vector3 targetPos = target.GlobalTransform.Origin + new Vector3(0, 1.0f, 0);
							projectile.Call("Launch", targetPos, target, AttackPower);
						}
					}
				}

				await ToSignal(GetTree().CreateTimer(0.4f), "timeout"); // Remaining anim time (0.7s total)
				stateMachine.Travel("Idle");
				// Restore camera
				await ToSignal(GetTree().CreateTimer(1f), "timeout");
				camera.Position = originalCamPos;
				camera.RotationDegrees = originalCamRot;
			}
			if (battleManager != null) battleManager.SetActionInProgress(false);
		}
		else if (CharacterName == "Boss" && animTree != null && camera != null)
		{
			if (battleManager != null) battleManager.SetActionInProgress(true);
			animTree.Active = true;
			var stateMachineObj = animTree.Get("parameters/playback");
			var stateMachine = stateMachineObj.AsGodotObject() as AnimationNodeStateMachinePlayback;
			if (stateMachine != null)
			{
				// 1. Move camera to boss cinematic position (like other enemies) BEFORE teleport
				camera.Position = GlobalTransform.Origin + new Vector3(-2.5f, 1.5f, -1.5f);
				camera.RotationDegrees = new Vector3(-10, -135, 0);
				await ToSignal(GetTree().CreateTimer(0.5f), "timeout");

				stateMachine.Travel("Basic_Attack");
				await ToSignal(GetTree().CreateTimer(0.3f), "timeout");

				// 2. Teleport boss to center front of allies
				var battleMgr = GetTree().Root.GetNodeOrNull<BattleManager>("BattleArena/BattleManager");
				if (battleMgr != null)
				{
					var alliesArr = battleMgr.AllCharacters;
					var allies = new System.Collections.Generic.List<Character>();
					foreach (var c in alliesArr)
					{
						if (c != null && c.IsAlly && c.CurrentHp > 0) allies.Add(c);
					}
					if (allies.Count > 0)
					{
						float avgX = 0f;
						foreach (var a in allies) avgX += a.GlobalTransform.Origin.X;
						avgX /= allies.Count;
						float z = allies[0].GlobalTransform.Origin.Z + 2.0f; // In front of allies
						GlobalTransform = new Transform3D(GlobalTransform.Basis, new Vector3(avgX, 0, z));
						// 3. Move camera to neutral position for the attack itself using BattleManager's exported properties
						if (battleMgr != null)
						{
							camera.Position = battleMgr.EnemyCameraPosition;
							camera.RotationDegrees = battleMgr.EnemyCameraRotation;
						}
					}
					// Damage all living allies
					foreach (var ally in allies)
					{
						ally.TakeDamage(AttackPower);
						ally.PlayHurtEffect(this);
						var allyAnimTree = ally.GetNodeOrNull<AnimationTree>("AnimationTree");
						if (allyAnimTree != null)
						{
							allyAnimTree.Active = true;
							var allyStateMachineObj = allyAnimTree.Get("parameters/playback");
							var allyStateMachine = allyStateMachineObj.AsGodotObject() as AnimationNodeStateMachinePlayback;
							if (allyStateMachine != null)
							{
								if (ally.CurrentHp <= 0)
								{
									allyStateMachine.Travel("Death_A");
									await ToSignal(GetTree().CreateTimer(1.0f), "timeout");
								}
								else
								{
									allyStateMachine.Travel("Hit_A");
									await ToSignal(GetTree().CreateTimer(0.2f), "timeout");
									allyStateMachine.Travel("Idle");
								}
							}
						}
					}
				}
				await ToSignal(GetTree().CreateTimer(0.4f), "timeout");
				camera.Position = originalCamPos;
				camera.RotationDegrees = originalCamRot;
				stateMachine.Travel("Idle");
				GlobalTransform = new Transform3D(GlobalTransform.Basis, originalPosition);
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
		return;
	}

	public async void OnProjectileHit(Character target, int damage)
	{
		if (target == null || !IsInstanceValid(target))
			return;

		target.TakeDamage(damage);
		target.PlayHurtEffect(this);
		var targetAnimTree = target.GetNodeOrNull<AnimationTree>("AnimationTree");
		if (targetAnimTree != null)
		{
			targetAnimTree.Active = true;
			var targetStateMachineObj = targetAnimTree.Get("parameters/playback");
			var targetStateMachine = targetStateMachineObj.AsGodotObject() as AnimationNodeStateMachinePlayback;
			if (targetStateMachine != null)
			{
				// If target is dead, play Death_A immediately (override Hit_A)
				if (target.CurrentHp <= 0)
				{
					targetStateMachine.Travel("Death_A");
					await ToSignal(GetTree().CreateTimer(1.0f), "timeout"); // Match death anim duration
				}
				else
				{
					targetStateMachine.Travel("Hit_A");
					await ToSignal(GetTree().CreateTimer(0.6f), "timeout");
					targetStateMachine.Travel("Idle");
				}
			}
		}
		// Set action in progress to false only after animation ends
		var battleManager = GetTree().Root.GetNodeOrNull<BattleManager>("BattleArena/BattleManager");
		if (battleManager != null)
			battleManager.SetActionInProgress(false);
	}

	public void SpawnHurtParticle(Color color)
	{
		var anchor = GetNodeOrNull<Node3D>("HurtEffectAnchor");
		if (anchor == null) anchor = this; // fallback to root
		var particleScene = GD.Load<PackedScene>("res://hurt_particle.tscn");
		var particle = particleScene.Instantiate();
		anchor.AddChild(particle);
		// Set color via process material if possible
		var processMaterialProp = particle.GetType().GetProperty("ProcessMaterial");
		if (processMaterialProp != null)
		{
			var processMaterial = processMaterialProp.GetValue(particle, null);
			if (processMaterial != null && processMaterial is ParticleProcessMaterial)
			{
				((ParticleProcessMaterial)processMaterial).Color = color;
			}
		}
		// Try to set Emitting, OneShot, and Finished
		var emittingProp = particle.GetType().GetProperty("Emitting");
		if (emittingProp != null) emittingProp.SetValue(particle, true, null);
		var oneShotProp = particle.GetType().GetProperty("OneShot");
		if (oneShotProp != null) oneShotProp.SetValue(particle, true, null);
		var finishedEvent = particle.GetType().GetEvent("Finished");
		if (finishedEvent != null)
		{
			System.Action handler = () => { ((Node)particle).QueueFree(); };
			finishedEvent.AddEventHandler(particle, handler);
		}
	}

	public void PlayHurtEffect(Character attacker)
	{
		Color color = new Color(1,1,1); // default white
		if (attacker != null)
		{
			if (attacker.CharacterName == "Rogue") color = new Color(0,1,0);
			else if (attacker.CharacterName == "Knight") color = new Color(1,1,0);
			else if (attacker.CharacterName == "Mage") color = new Color(0,1,1);
			else if (attacker.CharacterName.StartsWith("Enemy")) color = new Color(1,1,1);
		}
		SpawnHurtParticle(color);
	}

	private void MoveCrosshairToHurtAnchor()
	{
		var battleUI = GetTree().Root.GetNodeOrNull<BattleUI>("/root/BattleUI");
		if (battleUI == null) return;
		var anchor = GetNodeOrNull<Node3D>("HurtEffectAnchor");
		if (anchor == null) anchor = this;
		var camera = GetViewport().GetCamera3D();
		if (camera != null)
		{
			Vector2 screenPos = camera.UnprojectPosition(anchor.GlobalTransform.Origin);
			battleUI.ShowCrosshair(screenPos);
		}
	}
}
