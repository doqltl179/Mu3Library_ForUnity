using UnityEngine;

namespace Mu3Library.Audio
{
    public partial interface IAudioManager
    {
        /// <summary>
        /// Starts playing the given clips as a BGM playlist.
        /// Stops any currently playing BGM before starting.
        /// loopCount &lt;= 0 means infinite cycles. loopCount &gt; 0 plays that many full cycles.
        /// </summary>
        public void PlayBgmPlaylist(AudioClip[] clips);
        public void PlayBgmPlaylist(AudioClip[] clips, int loopCount);
        public void PlayBgmPlaylist(AudioClip[] clips, bool shuffle);
        public void PlayBgmPlaylist(AudioClip[] clips, float interval);
        public void PlayBgmPlaylist(AudioClip[] clips, int loopCount, bool shuffle);
        public void PlayBgmPlaylist(AudioClip[] clips, int loopCount, float interval);
        public void PlayBgmPlaylist(AudioClip[] clips, bool shuffle, float interval);
        public void PlayBgmPlaylist(AudioClip[] clips, int loopCount, bool shuffle, float interval);

        /// <summary>
        /// Stops the active BGM playlist and the current track.
        /// </summary>
        public void StopBgmPlaylist();
    }
}
