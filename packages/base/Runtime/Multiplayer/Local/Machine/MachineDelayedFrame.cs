namespace Mu3Library.Multiplayer.Local.Machine
{
    /// <summary>
    /// A frame the simulated network holds back before it goes out on its connection. The buffer
    /// is borrowed from a pool and goes back once the frame is queued or dropped.
    /// </summary>
    internal struct MachineDelayedFrame
    {
        public MachineFrameKind Kind;
        public byte[] Buffer;
        public int Length;
    }
}
