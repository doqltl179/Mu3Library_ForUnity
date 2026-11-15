using UnityEngine;

namespace Mu3Library.Utility
{
    /// <summary>
    /// Which clock a timed transition advances on, such as an MVP view animation or an audio fade.
    /// </summary>
    public enum TimeMode
    {
        /// <summary>
        /// Real time, <see cref="Time.unscaledDeltaTime"/>. The transition keeps running while
        /// <see cref="Time.timeScale"/> is 0, as under a pause menu.
        /// </summary>
        Unscaled = 0,

        /// <summary>
        /// Game time, <see cref="Time.deltaTime"/>. The transition follows <see cref="Time.timeScale"/>
        /// and stops while it is 0.
        /// </summary>
        Scaled = 1,
    }

    public static class TimeModeExtensions
    {
        /// <summary>
        /// The time the last frame took on the clock <paramref name="timeMode"/> names.
        /// </summary>
        public static float GetDeltaTime(this TimeMode timeMode)
            => timeMode == TimeMode.Scaled ? Time.deltaTime : Time.unscaledDeltaTime;
    }
}
