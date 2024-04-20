namespace Mu3Library.Character {
    public enum CharacterType {
        PlayerCharacter = 0,
        OtherCharacter = 1,
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

        PickupL,
        PickupR,
        PickupLR, 

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
}