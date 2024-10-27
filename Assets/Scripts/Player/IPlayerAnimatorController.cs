namespace Player
{
    public interface IPlayerAnimatorController
    {
        void SetSpeed(float speed);
        void PlayJump();
        void PlayDeath();
        void PlayHit(HitSide hitSide);
        void PlayDodgeLeft();
        void PlayDodgeRight();
    }
}