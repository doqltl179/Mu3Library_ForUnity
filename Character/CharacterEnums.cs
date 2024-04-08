namespace Mu3Library.Character {
    public enum CharacterType {
        TwoHandSword = 100,

        TreantMinion = 1000,
    }

    public enum CharacterStateType {
        /* Normal */
        None,
        Idle,
        Move,
        Jump,
        Dash,
        DashAttack,
        NormalAttack,
        Hit,



        /* Else */
        Roll,
    }

    public enum AttackType {
        NormalAttack,
        HardAttack,
    }
}