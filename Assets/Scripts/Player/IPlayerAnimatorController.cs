namespace Player
{
    public interface IPlayerAnimatorController
    {
        void SetSpeed(float speed);
        void JumpAnimation();
        void PlayDeath();
    }
}