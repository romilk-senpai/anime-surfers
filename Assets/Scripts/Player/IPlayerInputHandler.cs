namespace Player
{
    public interface IPlayerInputHandler
    {
        void ProcessLeft();
        void ProcessRight();
        void ProcessJump();
    }
}