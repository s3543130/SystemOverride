using Godot;
using System;
using SystemOverride;

namespace SystemOverride
{
    public partial class EnemySpawner : Node2D
    {
        [Export]
        private PackedScene _enemySpaceship;

        [Export]
        private 飞船 _player;

        [Export]
        private float _spawnRadius = 100f; // default spawn radius is 100 units

        [Export]
        private AIController _controller;

        private RandomNumberGenerator _rng = new RandomNumberGenerator();

        public override void _Ready()
        {
            _rng.Randomize();
        }

        public override void _Process(double delta)
        {
            if (_controller.SpaceshipCount < 15)
            {
                SpawnEnemy();
            }
        }

        public void SpawnEnemy()
        {
            if (_player != null && _enemySpaceship != null)
            {
                // calculate a random angle on the circle
                float angle = (float)_rng.RandfRange(0, Mathf.Tau);

                // calculate the position of the enemy spaceship
                Vector2 offset = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * _spawnRadius;
                Vector2 enemyPos = _player.Position + offset;

                // spawn the enemy spaceship at the calculated position
                飞船 enemy = _enemySpaceship.Instantiate<飞船>();
                enemy.Position = enemyPos;
                _controller.AddSpaceship(enemy);
            }
        }
    }
}