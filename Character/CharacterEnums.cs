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

        Skill_01,
        Skill_02,
        Skill_03,
        Skill_04,
        Skill_05,
        Skill_06,
        Skill_07,
        Skill_08,
        Skill_09,
        Skill_10, 

        /* Else */

    }

    public enum AttackType {
        NormalAttack,
        HardAttack,
    }

    public enum AttackPointType {
        HitAnything,
        HitOnlyOneObject, 
        HitEachCharacterOnce,
        HitEachCharacter,
        HitOnlyOneChatacter,
    }
}