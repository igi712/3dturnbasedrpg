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
	private int _currentCharacterIndex = 0;

	public Character CurrentTurnCharacter => (_turnOrder != null && _turnOrder.Count > 0 && _currentCharacterIndex < _turnOrder.Count) ? _turnOrder[_currentCharacterIndex] : null;


	public override void _Ready()
	{
		// Pindahkan StartBattle ke sini jika ingin battle dimulai otomatis
		// atau panggil dari tempat lain jika battle tidak langsung mulai
		StartBattle();
	}

	public void StartBattle()
	{
		if (AllCharacters.Count == 0)
		{
			GD.PrintErr("No characters in battle!");
			return;
		}

		_turnOrder = AllCharacters.Where(c => c != null && c.CurrentHp > 0).OrderByDescending(c => c.Speed).ToList();
		_currentCharacterIndex = 0;
		PrintTurnOrder();
		
		if (_turnOrder.Count > 0)
		{
			StartNextTurn(); // Memulai giliran pertama
		}
		else
		{
			GD.Print("No characters left to start battle or in turn order.");
		}
	}

	private void PrintTurnOrder()
	{
		GD.Print("Turn order:");
		foreach (var character in _turnOrder)
		{
			GD.Print($"- {character.CharacterName} (Speed: {character.Speed})");
		}
	}

	public void StartNextTurn()
	{
		if (_turnOrder.Count == 0 || CheckBattleEnd()) return;

		GD.Print($"--- {CurrentTurnCharacter.CharacterName}'s Turn ---");
		
		// 2. Emit sinyal di sini, setelah CurrentTurnCharacter dipastikan ada dan sebelum logika spesifik giliran
		EmitSignal(SignalName.NewTurnStarted); // Menggunakan nameof lebih aman jika ada refactoring

		if (CurrentTurnCharacter.CharacterName.Contains("Enemy")) // Cara identifikasi musuh yang lebih baik diperlukan
		{
			// AI Sederhana untuk musuh
			Character playerTarget = _turnOrder.FirstOrDefault(c => !c.CharacterName.Contains("Enemy") && c.CurrentHp > 0);
			if (playerTarget != null)
			{
				CurrentTurnCharacter.Attack(playerTarget);
			}
			EndTurn();
		}
		else
		{
			GD.Print("Player's turn. Waiting for action...");
			// UI akan diaktifkan oleh sinyal NewTurnStarted
		}
	}

	public void PlayerPerformAttack(Character target)
	{
		if (CurrentTurnCharacter != null && !CurrentTurnCharacter.CharacterName.Contains("Enemy") && CurrentTurnCharacter.CurrentHp > 0)
		{
			CurrentTurnCharacter.Attack(target);
			EndTurn();
		}
	}

	public void EndTurn()
	{
		// Hapus karakter yang kalah dari _turnOrder
		_turnOrder.RemoveAll(c => c.CurrentHp <= 0);

		// Cek kondisi akhir pertempuran lagi setelah menghapus karakter
		if (CheckBattleEnd()) return;

		// Jika masih ada karakter dalam giliran
		if (_turnOrder.Count > 0)
		{
			_currentCharacterIndex = (_currentCharacterIndex + 1) % _turnOrder.Count;
			StartNextTurn();
		}
		else
		{
			// Ini bisa terjadi jika semua karakter dari satu pihak kalah bersamaan
			// dan CheckBattleEnd belum menangkapnya karena pemanggilan sebelumnya.
			GD.Print("No characters left in turn order after EndTurn cleanup.");
			CheckBattleEnd(); // Panggil lagi untuk memastikan status akhir yang benar
		}
	}
	
	public bool CheckBattleEnd()
	{
		// Pastikan _turnOrder tidak null sebelum melakukan query LINQ
		if (_turnOrder == null) return false;

		// Cek apakah semua pemain (non-musuh) kalah
		bool allPlayersDefeated = !_turnOrder.Any(c => c != null && !c.CharacterName.Contains("Enemy") && c.CurrentHp > 0);
		// Cek apakah semua musuh kalah
		bool allEnemiesDefeated = !_turnOrder.Any(c => c != null && c.CharacterName.Contains("Enemy") && c.CurrentHp > 0);

		if (allPlayersDefeated && _turnOrder.Any(c => c != null && c.CharacterName.Contains("Enemy"))) // Jika masih ada musuh tapi pemain habis
		{
			GD.Print("Game Over! Player defeated.");
			// GetTree().Quit(); atau tampilkan layar game over
			return true;
		}
		if (allEnemiesDefeated && _turnOrder.Any(c => c != null && !c.CharacterName.Contains("Enemy"))) // Jika masih ada pemain tapi musuh habis
		{
			GD.Print("Victory! All enemies defeated.");
			// Tampilkan layar kemenangan
			return true;
		}
		if (_turnOrder.Count(c => c != null && c.CurrentHp > 0) <= 1 && AllCharacters.Count > 1) {
			// Jika hanya satu tim yang tersisa atau tidak ada yang tersisa
			// Ini untuk menangani kasus di mana salah satu kondisi di atas mungkin tidak langsung true
			// Misalnya, jika kedua pihak kalah bersamaan, atau hanya 1 karakter tersisa secara keseluruhan.
			if (!_turnOrder.Any(c => c != null && !c.CharacterName.Contains("Enemy") && c.CurrentHp > 0) &&
				!_turnOrder.Any(c => c != null && c.CharacterName.Contains("Enemy") && c.CurrentHp > 0) &&
				AllCharacters.Count > 0) // Jika semua karakter (pemain dan musuh) kalah
			{
				GD.Print("Draw or all defeated!"); // Atau kondisi lain yang sesuai
				return true;
			}
			// Jika hanya 1 karakter yang hidup dari semua karakter awal, bisa jadi itu kemenangan/kekalahan tergantung siapa dia
			// Logika ini mungkin perlu disesuaikan lebih lanjut dengan aturan game Anda
		}


		return false;
	}
}
