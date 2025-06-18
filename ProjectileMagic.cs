using Godot;
using System;

public partial class ProjectileMagic : Node3D
{
	[Export] public float Speed = 25.0f;
	private Vector3 _startPos;
	private Vector3 _targetPos;
	private float _travelTime = 0.0f;
	private float _duration = 0.0f;
	private bool _isLaunched = false;
	private Character _targetCharacter;
	private int _damage;

	public void Launch(Vector3 targetPos, Character targetCharacter = null, int damage = 0)
	{
		_startPos = GlobalTransform.Origin;
		_targetPos = targetPos;
		float distance = _startPos.DistanceTo(_targetPos);
		_duration = distance / Speed;
		_travelTime = 0.0f;
		_isLaunched = true;
		_targetCharacter = targetCharacter;
		_damage = damage;
	}

	public override void _Process(double delta)
	{
		if (!_isLaunched)
			return;

		_travelTime += (float)delta;
		float t = Mathf.Clamp(_travelTime / _duration, 0, 1);

		// Straight line (no arc)
		Vector3 pos = _startPos.Lerp(_targetPos, t);
		GlobalTransform = new Transform3D(GlobalTransform.Basis, pos);

		if (t >= 1.0f)
		{
			// On hit: reduce HP and play hurt animation
			if (_targetCharacter != null && IsInstanceValid(_targetCharacter))
			{
				var characterNode = _targetCharacter;
				characterNode.OnProjectileHit(_targetCharacter, _damage);
			}
			QueueFree();
		}
	}
}
