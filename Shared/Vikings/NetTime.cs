using System;

namespace Kriona.Vikings
{
	/// <summary>
	/// The network time every client shares, which the times both mods save in ZDOs are measured in
	/// </summary>
	internal static class NetTime
	{
		/// <summary>
		/// The network time all clients share
		/// </summary>
		/// <returns>The time in ticks, or 0 outside a world</returns>
		public static long Now()
		{
			return((ZNet.instance != null) ? ZNet.instance.GetTime().Ticks : 0L);
		}

		/// <summary>
		/// Converts seconds to ticks
		/// </summary>
		/// <param name="seconds">The seconds</param>
		/// <returns>The ticks</returns>
		public static long Ticks(float seconds)
		{
			return((long)(seconds * TimeSpan.TicksPerSecond));
		}
	}
}
