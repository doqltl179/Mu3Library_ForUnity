using Mu3Library.Multiplayer.Data;

namespace Mu3Library.Multiplayer
{
    /// <summary>
    /// Answers, on the host, whether a message that <paramref name="sender"/> sent reaches
    /// <paramref name="recipient"/>, who the message's target and policy already let it reach
    /// (<see cref="IMultiplayerMessenger.SetRelayFilter"/>). <paramref name="sender"/> is the
    /// player the connection proved, as <see cref="MessageContext.Sender"/> is.
    /// </summary>
    public delegate bool RelayFilter(PlayerId sender, PlayerId recipient);
}
