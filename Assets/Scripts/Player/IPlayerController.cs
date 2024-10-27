namespace Player
{
    public interface IPlayerController
    {
        float MoveSpeed { get; }
        float JumpHeight { get; }

        void StartRunning();
        void ProcessLeft();
        void ProcessRight();
        void ProcessJump();
        void ProcessHit(HitSide hitSide);
        void ProcessDeath();
    }
}