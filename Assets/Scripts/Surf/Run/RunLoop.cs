using UnityEngine;
using VContainer.Unity;

namespace Game.Surf
{
    /// <summary>
    /// Steps the board systems in a fixed order. Runs before MonoBehaviour updates,
    /// so views always read this frame's state.
    /// </summary>
    public sealed class RunLoop : IStartable, ITickable
    {
        private readonly BoardMovement _movement;
        private readonly BoardBody _body;
        private readonly BoardCollision _collision;
        private readonly PickupCollector _pickups;
        private readonly PowerUps _powerUps;
        private readonly ScoreCounter _score;

        public RunLoop(BoardMovement movement, BoardBody body, BoardCollision collision, PickupCollector pickups,
            PowerUps powerUps, ScoreCounter score)
        {
            _movement = movement;
            _body = body;
            _collision = collision;
            _pickups = pickups;
            _powerUps = powerUps;
            _score = score;
        }

        public void Start()
        {
            _movement.Initialize();
            _body.Initialize();
        }

        public void Tick()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f)
            {
                return;
            }

            _movement.Tick(dt);
            _body.Tick(dt);
            _collision.Tick();
            _pickups.Tick(dt);
            _powerUps.Tick(dt);
            _score.Tick();
        }
    }
}
