using Godot;
using System;
using System.Collections.Generic;

namespace SystemOverride
{
    public partial class 飞船 : RigidBody2D
    {
        /// <summary>
        /// 飞船的最大生命值，默认为100。
        /// 当生命值耗尽时，飞船爆炸。
        /// </summary>
        [Export] int _maxHealth = 100;

        /// <summary>
        /// 飞船消失前的时间，默认为7秒。
        /// </summary>
        [Export] float _despawnTime = 7.0f;

        /// <summary>
        /// 碰撞音效播放器。
        /// </summary>
        [Export] AudioStreamPlayer2D _impactSfx;

        /// <summary>
        /// 飞船爆炸视觉效果。
        /// </summary>
        [Export] PackedScene _spaceshipExplositionFX;

        /// <summary>
        /// 飞船爆炸音效。
        /// </summary>
        [Export] PackedScene _spaceshipExplostionSfx;

        public int MaxHealth
        {
            get { return _maxHealth; }
        }

        public int Health { get; private set; }

        public bool IsDestroyed { get; private set; } = false;

        [Signal]
        public delegate void ScreenShakeRequestedEventHandler(float screenShakeFactor);

        [Signal]
        public delegate void DamageTakenEventHandler();

        Vector2 _targetMovementEffort = Vector2.Zero;

        /// <summary>
        /// 目标推力方向。每个轴的值范围为 <-1, 1>。
        /// 负Y轴表示向前，X轴表示向右。
        /// 使用零向量表示无移动。
        /// </summary>
        public Vector2 TargetMovementEffort
        {
            get { return _targetMovementEffort; }

            set
            {
                调试.Assert(value.X <= 1.0f && value.X >= -1.0f, "value.X <= 1.0f && value.X >= -1.0f");
                调试.Assert(value.Y <= 1.0f && value.Y >= -1.0f, "value.Y <= 1.0f && value.Y >= -1.0f");

                _targetMovementEffort = value;
            }
        }

        public float TurnProcessOffset { get; set; }

        const float _turnProporcionalGain = 10.0f;
        const float _turnIntegralGain = 15.0f;
        const float _turnDerivativeGain = 5.0f;

        PID控制器 _turnPidController;

        List<引擎> _engines = new List<引擎>();
        List<武器> _weapons = new List<武器>();

        RandomNumberGenerator _rng = new RandomNumberGenerator();

        Gravity _gravity;

        Node2D _enginesRoot;
        Node2D _weaponsRoot;
        Node2D _spaceshipSprite;

        Timer _despawnTimer;

        public override void _Ready()
        {
            // 检查导出变量
            调试.Assert(_impactSfx != null, "_impactSfx != null");

            // 初始化引用
            _turnPidController = new PID控制器(_turnProporcionalGain, _turnIntegralGain, _turnDerivativeGain, 1.0f, -1.0f);

            _gravity = GetNode<Gravity>("/root/Gravity");
            _enginesRoot = GetNode<Node2D>("Engines");
            _weaponsRoot = GetNode<Node2D>("Weapons");
            _spaceshipSprite = GetNode<Node2D>("Sprite");

            _despawnTimer = new Timer();
            _despawnTimer.Timeout += OnDespawn;
            AddChild(_despawnTimer);
            _despawnTimer.WaitTime = _despawnTime;

            // 其他初始化操作
            _rng.Randomize();

            Health = _maxHealth;

            int engineCount = _enginesRoot.GetChildCount();

            // 至少需要一些引擎，否则不是真正的飞船
            调试.Assert(engineCount > 0, "engineCount > 0");

            for (int i = 0; i < engineCount; i++)
            {
                var engine = _enginesRoot.GetChild<引擎>(i);
                _engines.Add(engine);
            }

            int weaponCount = _weaponsRoot.GetChildCount();

            // 可能没有武器
            for (int i = 0; i < weaponCount; i++)
            {
                var weapon = _weaponsRoot.GetChild<武器>(i);
                weapon.ScreenShakeRequested += OnScreenShakeRequestedFromWeapons;
                _weapons.Add(weapon);
            }
        }

        public override void _PhysicsProcess(double delta)
        {
            _turnPidController.ProcessVariable = TurnProcessOffset;

            float target_turn_effort = (float)_turnPidController.ControlVariable(TimeSpan.FromSeconds(delta));

            foreach (var engine in _engines)
            {
                // 如果飞船已被摧毁，不控制引擎
                if (!IsDestroyed)
                {
                    bool usedForTurning = false;

                    bool enableEngine = engine.MoveDirectionGroup switch
                    {
                        EngineMoveDirectionGroup.Forward => TargetMovementEffort.Y < 0.0f,
                        EngineMoveDirectionGroup.Backward => TargetMovementEffort.Y > 0.0f,
                        EngineMoveDirectionGroup.Left => TargetMovementEffort.X < 0.0f,
                        EngineMoveDirectionGroup.Right => TargetMovementEffort.X > 0.0f,
                        _ => false
                    };

                    switch (engine.TurnDirectionGroup)
                    {
                        case EngineTurnDirectionGroup.Left when target_turn_effort < 0.0f:
                            enableEngine = true;
                            usedForTurning = true;
                            break;
                        case EngineTurnDirectionGroup.Right when target_turn_effort > 0.0f:
                            enableEngine = true;
                            usedForTurning = true;
                            break;
                    }

                    engine.EngineEnabled = enableEngine;

                    float forceFactor = usedForTurning ? Mathf.Abs(target_turn_effort) : 1.0f;

                    engine.ApplyForceToOwner(this, forceFactor);
                }
                else
                {
                    // 如果飞船已摧毁且引擎仍激活，则保持最大推力
                    engine.ApplyForceToOwner(this, 1.0f);
                }
            }

            ApplyCentralForce(_gravity.CalculateGravityForce(GlobalPosition));
        }

        private void OnScreenShakeRequestedFromWeapons(float screenShakeFactor)
        {
            EmitSignal(SignalName.ScreenShakeRequested, screenShakeFactor);
        }

        /// <summary>
        /// 射击武器（仅在武器准备就绪时）。
        /// </summary>
        /// <param name="weaponGroupIndex">可能有多个相同类型的武器。
        /// 每种类型有一个索引，无效值会被忽略。</param>
        public void FireWeapons(int weaponGroupIndex)
        {
            if (IsDestroyed)
            {
                return;
            }

            foreach (var weapon in _weapons)
            {
                if (weapon.WeaponGroup == WeaponGroup.Primary)
                {
                    weapon.Fire();
                }
            }
        }

        public void SwitchToCollisionLayer(int layer)
        {
            CollisionLayer = 1u << (layer - 1);
            CollisionMask = 0xFFFF;

            foreach (var weapon in _weapons)
            {
                weapon.SwitchToCollisionLayer(layer);
            }
        }

        public void TakeDamage(int damage)
        {
            if (IsDestroyed)
            {
                _impactSfx.PitchScale = Mathf.Lerp(0.9f, 1.2f, _rng.Randf());
                _impactSfx.Play();
                return;
            }

            Health -= damage;

            EmitSignal(SignalName.ScreenShakeRequested, 14.0f);

            _impactSfx.PitchScale = Mathf.Lerp(0.9f, 1.2f, _rng.Randf());
            _impactSfx.Play();

            if (Health <= 0)
            {
                DestroySpaceship();
            }

            EmitSignal(SignalName.DamageTaken);
        }

        public void DestroySpaceship()
        {
            var spaceshipExplosionSfx = _spaceshipExplostionSfx.Instantiate<自动释放音效播放器>();
            spaceshipExplosionSfx.GlobalPosition = GlobalPosition;
            GetTree().Root.AddChild(spaceshipExplosionSfx);

            /* TODO: 队列化释放粒子效果 * /
            // 创建并实例化飞船爆炸粒子效果*/
            GpuParticles2D fx = _spaceshipExplositionFX.Instantiate<GpuParticles2D>();
            AddChild(fx);

            fx.GlobalPosition = GlobalPosition;
            fx.Emitting = true;

            // 使飞船变暗以显示已摧毁状态
            _spaceshipSprite.Modulate = new Color(0.2f, 0.2f, 0.2f);

            IsDestroyed = true;

            // 禁用所有引擎
            foreach (var engine in _engines)
            {
                engine.EngineEnabled = false;
            }

            _despawnTimer.Start();
        }

        /// <summary>
        /// 当飞船消失时触发的方法。
        /// </summary>
        private void OnDespawn()
        {
            // 创建并实例化飞船爆炸音效
            var spaceshipExplosionSfx = _spaceshipExplostionSfx.Instantiate<自动释放音效播放器>();
            spaceshipExplosionSfx.GlobalPosition = GlobalPosition;
            GetTree().Root.AddChild(spaceshipExplosionSfx);

            /* TODO: 队列化释放粒子效果 */
            // 创建并实例化飞船爆炸粒子效果
            GpuParticles2D fx = _spaceshipExplositionFX.Instantiate<GpuParticles2D>();
            fx.GlobalPosition = GlobalPosition;
            GetTree().Root.AddChild(fx);
            fx.Emitting = true;

            // 将当前对象加入队列以待释放
            QueueFree();
        }
    }
}