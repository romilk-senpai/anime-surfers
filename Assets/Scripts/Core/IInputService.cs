namespace Game.Core
{
    /// <summary>
    /// Gameplay input polled once per frame. Everything reads false/0 while disabled.
    /// </summary>
    public interface IInputService
    {
        bool IsEnabled { get; }

        /// <summary>-1 or 1 on the frame a lane change was requested, 0 otherwise.</summary>
        int LaneChange { get; }

        bool JumpPressed { get; }
        bool SlidePressed { get; }

        void Enable();
        void Disable();
    }
}
