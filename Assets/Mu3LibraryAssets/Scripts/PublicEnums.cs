namespace Mu3Library {
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
}