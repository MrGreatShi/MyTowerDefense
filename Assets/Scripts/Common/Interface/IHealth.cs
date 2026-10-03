public interface IHealth
{
    void TakeDamage(int damage, AttackType attackType, IAttack attacker);

    void Heal(int healAmount); 
    bool IsAlive();
}
