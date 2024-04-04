namespace Mu3Library {
    public enum CharacterType {
        Player, 
        Monster, 
    }

    public enum CharacterState {
        None,
        Idle,
        Move,
        Dash, 
        Jump,
        Hit,
        Avoid, 
        Attack,
        KnockDown,
        Die,
        Victory,
    }

    public enum AttackType {
        NormalAttack, 
        HardAttack, 
    }

    public enum RayType {
        Sphere,
        Capsule,
    }

    public enum Coordinate {
        Local, 
        World, 
    }

    [System.Flags]
    public enum Direction {
        None = 0, 
        F = 1 << 0, 
        B = 1 << 1, 
        R = 1 << 2, 
        L = 1 << 3, 
    }

    public enum SceneType {
        None,
        Main,
        Game,
        Credits,
    }
}