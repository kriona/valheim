namespace Kriona.Shared
{
	/// <summary>
	/// Display options Configuration Manager reads from a config entry's tags
	/// </summary>
	/// <remarks>
	/// Configuration Manager finds this class by its name and reads its fields by theirs, so a plugin doesn't need a
	/// reference to it and works without it installed. Fields left null keep Configuration Manager's defaults
	/// </remarks>
	internal sealed class ConfigurationManagerAttributes
	{
		/// <summary>
		/// Position of the entry in its category, higher first
		/// </summary>
		public int? Order;

		/// <summary>
		/// Name shown in place of the entry's key
		/// </summary>
		public string DispName;
	}
}
