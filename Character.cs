using Godot;
using System;

public partial class Character : Node3D
{
	[Export] public string CharacterName { get; set; } = "Hero";
	[Export] public int MaxHp { get; set; } = 100;
	private int _currentHp;
	[Export] public int AttackPower { get; set; } = 10;
	[Export] public int DefensePower { get; set; } = 5;
	[Export] public int Speed { get; set; } = 10;
	// Tambahkan status lain jika perlu

	public int CurrentHp
	{
		get => _currentHp;
		set => _currentHp = Mathf.Max(0, value); // Jangan sampai HP minus
	}

	public override void _Ready()
	{
		CurrentHp = MaxHp;
	}

	public void TakeDamage(int damage)
	{
		int actualDamage = Mathf.Max(1, damage - DefensePower); // Minimal 1 damage
		CurrentHp -= actualDamage;
		GD.Print($"{CharacterName} takes {actualDamage} damage, HP: {CurrentHp}/{MaxHp}");
		if (CurrentHp <= 0)
		{
			GD.Print($"{CharacterName} has been defeated!");
			// Handle kematian (animasi, hapus dari giliran, dll.)
		}
	}

	public void Attack(Character target)
	{
		GD.Print($"{CharacterName} attacks {target.CharacterName}!");
		target.TakeDamage(AttackPower);
		// Nanti di sini bisa trigger animasi serangan
	}
}
