using Godot;
using System;
using System.Collections.Generic;

namespace SystemOverride
{
    public partial class AI控制器 : Node2D
    {
        /// <summary>
        /// AI will try to target this point.
        /// </summary>
        [Export]
        Node2D _target;

        /// <summary>
        /// All spaceships that are children of this node will be controled by this AI.
        /// </summary>
        [Export]
        Node2D _spaceshipsRoot;

        [Export]
        子弹时间 _slowMotion;

        [Export]
        AudioStreamPlayer _spaceshipDestroyedStreamPlayer;

        List<飞船> _spaceships = new List<飞船>();

        public int SpaceshipCount { get {  return _spaceships.Count; } }

        [Signal]
        public delegate void AISpaceshipDestroyedEventHandler();

        public override void _Ready()
        {
            int spaceshipCount = _spaceshipsRoot.GetChildCount();

            for (int i = 0; i < spaceshipCount; i++)
            {
                var spaceship = _spaceshipsRoot.GetChild<飞船>(i);
                _spaceships.Add(spaceship);
                spaceship.SwitchToCollisionLayer(2);
            }
        }

        /// <summary>
        /// Overrides the _Process method to update and control all spaceships.
        /// Removes invalid spaceships from the list.
        /// </summary>
        /// <param name="delta">The time elapsed since the last frame update.</param>
        public override void _Process(double delta)
        {

            RemoveInvalidSpaceships();

            foreach (var spaceship in _spaceships)
            {
                var direction = GetDirection(spaceship);
                var facingDirectionOffset = GetFacingDirectionOffset(spaceship, direction);
                UpdateSpaceship(spaceship, facingDirectionOffset);
                FireIfPossible(spaceship, facingDirectionOffset);
            }
        }

        /// <summary>
        /// Removes invalid and destroyed spaceships from the _spaceships list.
        /// </summary>
        private void RemoveInvalidSpaceships()
        {
            for (int i = _spaceships.Count - 1; i >= 0; i--)
            {
                if (!IsInstanceValid(_spaceships[i]))
                {
                    RemoveSpaceshipAtIndex(i);
                }

                if (_spaceships[i].IsDestroyed)
                {
                    RemoveSpaceshipAtIndex(i);
                }
            }
        }

        private void RemoveSpaceshipAtIndex(int i)
        {
            _spaceships.RemoveAt(i);
            _spaceshipDestroyedStreamPlayer.PitchScale = (float)GD.RandRange(0.8, 1.3);
            _spaceshipDestroyedStreamPlayer.Play();
            _slowMotion.TriggerSlowMotion();
            EmitSignal(SignalName.AISpaceshipDestroyed);
        }

        /// <summary>
        /// Gets the normalized direction from a given spaceship to the target.
        /// </summary>
        /// <param name="spaceship">The spaceship to calculate the direction from.</param>
        /// <returns>The normalized direction from the spaceship to the target.</returns>
        private Vector2 GetDirection(飞船 spaceship)
        {
            if (IsInstanceValid(_target))
            {
                return (_target.GlobalPosition - spaceship.GlobalPosition).Normalized();
            }

            return new Vector2(GD.Randf(), GD.Randf()).Normalized();
        }

        /// <summary>
        /// Calculates the offset of the facing direction of the spaceship relative to the target direction,
        /// while adding some random deviation to the target direction to create a more natural movement.
        /// </summary>
        /// <param name="spaceship">The spaceship to calculate the offset for.</param>
        /// <param name="direction">The direction of the target relative to the spaceship.</param>
        /// <returns>The offset of the facing direction of the spaceship relative to the target direction.</returns>

        private float GetFacingDirectionOffset(飞船 spaceship, Vector2 direction)
        {
            const float deviationFactor = 0.2f;
            var randomDeviation = new Vector2((float)GD.RandRange(-1.0, 1.0), (float)GD.RandRange(-1.0, 1.0)) * deviationFactor;
            direction += randomDeviation;

            return -spaceship.ToLocal(direction + spaceship.GlobalPosition).X;
        }

        /// <summary>
        /// Updates the spaceship's turn process offset and target movement effort.
        /// </summary>
        /// <param name="spaceship">The spaceship to update.</param>
        /// <param name="facingDirectionOffset">The offset of the facing direction.</param>
        private void UpdateSpaceship(飞船 spaceship, float facingDirectionOffset)
        {
            spaceship.TurnProcessOffset = facingDirectionOffset;
            spaceship.TargetMovementEffort = Vector2.Up;
        }

        /// <summary>
        /// Fires the weapons of the spaceship if the facing direction offset is less than a maximum threshold and the target is within range.
        /// </summary>
        /// <param name="spaceship">The spaceship to check and fire weapons for.</param>
        /// <param name="facingDirectionOffset">The offset of the facing direction of the spaceship from the target.</param>

        private void FireIfPossible(飞船 spaceship, float facingDirectionOffset)
        {
            if (IsInstanceValid(_target))
            {
                const float maxFacingDirectionOffsetToFire = 0.1f;
                if (facingDirectionOffset <= maxFacingDirectionOffsetToFire && spaceship.GlobalPosition.DistanceSquaredTo(_target.GlobalPosition) < 40000000.0f)
                {
                    spaceship.FireWeapons(0);
                }
            }
        }

        public void AddSpaceship(飞船 spaceship)
        {
            AddChild(spaceship);
            _spaceships.Add(spaceship);
            spaceship.SwitchToCollisionLayer(2);
        }
    }
}