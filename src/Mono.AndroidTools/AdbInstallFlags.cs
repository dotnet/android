using System;

namespace Mono.AndroidTools
{
	/// <summary>
	/// Flags for the `adb pm install` command
	/// </summary>
	[Flags]
	public enum AdbInstallFlags {
		None           = 0,
		/// <summary>
		/// pm install -r
		/// </summary>
		Reinstall      = 1 << 0,
		/// <summary>
		/// pm install -s
		/// </summary>
		External       = 1 << 1,
		/// <summary>
		/// pm install -d
		/// </summary>
		AllowDowngrade = 1 << 2,
		/// <summary>
		/// pm install -t
		/// </summary>
		TestOnly = 1 << 3,
	}
}
