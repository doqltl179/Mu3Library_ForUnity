namespace Mu3Library.Multiplayer.Data
{
    public enum DeliveryMode
    {
        /// <summary>Retransmitted until it arrives; arrives in the order it was sent.</summary>
        Reliable = 0,
        /// <summary>Sent once; may be lost or arrive out of order.</summary>
        Unreliable = 1,
    }
}
