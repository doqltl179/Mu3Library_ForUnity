namespace Mu3Library {
    public enum PlayState {
        Stop = 0, 
        Playing = 1, 
        Paused = 2, 
    }

    public enum Coordinate {
        Local = 0, 
        World = 1, 
    }

    [System.Flags]
    public enum Direction {
        None = 0, 

        F = 1 << 0, 
        B = 1 << 1, 
        R = 1 << 2, 
        L = 1 << 3, 
    }
}