// BattleUI.cs
using Godot;
using System;
using System.Linq; // Diperlukan untuk FirstOrDefault

public partial class BattleUI : Control
{
	private Button _attackButton;
	private Label _turnInfoLabel;
	private BattleManager _battleManager;

	public override void _Ready()
	{
		_attackButton = GetNode<Button>("AttackButton"); // Pastikan nama node ini "AttackButton" di scene UI Anda
		_turnInfoLabel = GetNode<Label>("TurnInfoLabel"); // Pastikan nama node ini "TurnInfoLabel" di scene UI Anda
		
		if (_attackButton == null) GD.PrintErr("AttackButton not found in BattleUI!");
		if (_turnInfoLabel == null) GD.PrintErr("TurnInfoLabel not found in BattleUI!");

		_attackButton.Pressed += _on_attack_button_pressed; // Hubungkan sinyal tombol ke metode

		// Dapatkan referensi ke BattleManager
		// Pastikan path ini benar sesuai struktur scene Anda
		_battleManager = GetTree().Root.GetNode<BattleManager>("BattleArena/BattleManager"); 
		
		if (_battleManager != null)
		{
			// 3. Hubungkan sinyal dari _battleManager ke metode OnNewTurn di instance BattleUI ini
			_battleManager.Connect(BattleManager.SignalName.NewTurnStarted, Callable.From(OnNewTurn));
			GD.Print("BattleUI connected to BattleManager's NewTurnStarted signal.");
			
			// Panggil UpdateTurnInfo sekali di awal untuk setup UI awal jika battle sudah dimulai
			// Ini penting jika BattleManager memulai battle di _Ready() sebelum UI siap sepenuhnya.
			// Atau, BattleManager bisa emit sinyal setelah battle benar-benar siap.
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
		if (_battleManager?.CurrentTurnCharacter != null && _battleManager.CurrentTurnCharacter.CurrentHp > 0)
		{
			_turnInfoLabel.Text = $"{_battleManager.CurrentTurnCharacter.CharacterName}'s Turn. HP: {_battleManager.CurrentTurnCharacter.CurrentHp}/{_battleManager.CurrentTurnCharacter.MaxHp}";
			_attackButton.Disabled = _battleManager.CurrentTurnCharacter.CharacterName.Contains("Enemy"); // Identifikasi lebih baik
		}
		else if (_battleManager?.CurrentTurnCharacter != null && _battleManager.CurrentTurnCharacter.CurrentHp <= 0)
		{
			 _turnInfoLabel.Text = $"{_battleManager.CurrentTurnCharacter.CharacterName} is defeated.";
			 _attackButton.Disabled = true;
		}
		else
		{
			// Cek kondisi battle end dari BattleManager
			if (_battleManager != null && _battleManager.CheckBattleEnd()) { // Tambahkan metode CheckBattleEnd() yang bisa diakses publik jika perlu
				 _turnInfoLabel.Text = "Battle Ended!"; // Atau pesan kemenangan/kekalahan spesifik
			} else {
				 _turnInfoLabel.Text = "Waiting for battle to start or character data...";
			}
			_attackButton.Disabled = true;
		}
	}

	// Metode ini akan dipanggil oleh sinyal
	public void OnNewTurn()
	{
		GD.Print("BattleUI.OnNewTurn() called by signal.");
		UpdateTurnInfo();
	}

	private void _on_attack_button_pressed()
	{
		if (_battleManager != null && 
			_battleManager.CurrentTurnCharacter != null && 
			!_battleManager.CurrentTurnCharacter.CharacterName.Contains("Enemy")) // Identifikasi lebih baik
		{
			// Untuk MVP, asumsikan pemain menyerang musuh pertama yang hidup
			// Pastikan _battleManager.AllCharacters sudah terisi dengan benar
			Character enemyTarget = _battleManager.AllCharacters.FirstOrDefault(
				c => c != null && c.CharacterName.Contains("Enemy") && c.CurrentHp > 0
			); 

			if (enemyTarget != null)
			{
				_battleManager.PlayerPerformAttack(enemyTarget);
				// UpdateTurnInfo() akan dipanggil oleh sinyal NewTurnStarted saat giliran berikutnya dimulai
			}
			else
			{
				GD.Print("No enemy target found or all enemies defeated.");
				// Jika tidak ada musuh lagi, mungkin battle seharusnya sudah berakhir.
				// BattleManager.CheckBattleEnd() akan menangani ini.
			}
		}
	}
}
