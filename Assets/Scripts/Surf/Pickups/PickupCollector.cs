using UnityEngine;

namespace Game.Surf
{
    /// <summary>
    /// Collects pickups the rider's body touches and pulls coins in while the magnet is active.
    /// Water-following pickups are tested against the water height under the board, which is accurate enough
    /// this close to it.
    /// </summary>
    public sealed class PickupCollector
    {
        private readonly PickupSettings _settings;
        private readonly BoardSettings _boardSettings;
        private readonly BoardState _board;
        private readonly PickupField _field;
        private readonly PowerUps _powerUps;
        private readonly RunState _run;

        public PickupCollector(PickupSettings settings, BoardSettings boardSettings, BoardState board,
            PickupField field, PowerUps powerUps, RunState run)
        {
            _settings = settings;
            _boardSettings = boardSettings;
            _board = board;
            _field = field;
            _powerUps = powerUps;
            _run = run;
        }

        public void Tick(float dt)
        {
            if (_board.IsCrashed)
            {
                return;
            }

            float radius = _settings.PickupRadius;
            float halfLength = _boardSettings.BodyHalfLength + radius;
            float halfWidth = _boardSettings.BoardWidth * 0.5f + radius;
            float bodyHeight = _board.IsSliding ? _boardSettings.SlideBodyHeight : _boardSettings.BodyHeight;
            float bottom = _board.Height - radius;
            float top = _board.Height + bodyHeight + radius;
            bool magnet = _powerUps.IsMagnetActive;

            foreach (Pickup pickup in _field.Pickups)
            {
                if (pickup.Collected)
                {
                    continue;
                }

                float ahead = pickup.Distance - _board.Distance;

                if (magnet && pickup.Kind == PickupKind.Coin && !pickup.Attracted && ahead > -halfLength &&
                    ahead < _settings.MagnetRange)
                {
                    Attract(pickup);
                }

                if (pickup.Attracted)
                {
                    Pull(pickup, bodyHeight * 0.5f, dt);
                    ahead = pickup.Distance - _board.Distance;
                }

                float height = pickup.FollowsWater ? _board.WaterHeight + pickup.Height : pickup.Height;

                if (Mathf.Abs(ahead) < halfLength && Mathf.Abs(pickup.Lateral - _board.Lateral) < halfWidth &&
                    height > bottom && height < top)
                {
                    Collect(pickup);
                }
            }
        }

        private void Attract(Pickup pickup)
        {
            if (pickup.FollowsWater)
            {
                pickup.Height += _board.WaterHeight;
                pickup.FollowsWater = false;
            }

            pickup.Attracted = true;
        }

        private void Pull(Pickup pickup, float targetHeight, float dt)
        {
            var offset = new Vector3(_board.Distance - pickup.Distance, _board.Lateral - pickup.Lateral,
                _board.Height + targetHeight - pickup.Height);
            float step = (_settings.MagnetPullSpeed + _board.Speed) * dt;
            Vector3 move = Vector3.ClampMagnitude(offset, step);

            pickup.Distance += move.x;
            pickup.Lateral += move.y;
            pickup.Height += move.z;
        }

        private void Collect(Pickup pickup)
        {
            pickup.Collected = true;

            if (pickup.Kind == PickupKind.Coin)
            {
                _run.AddCoins(1);
            }
            else
            {
                _powerUps.Activate(pickup.Kind);
            }
        }
    }
}
