public interface IAttack
{
    void Attack(IHealth target, int damage, AttackType attackType, IAttack attacker);
}
