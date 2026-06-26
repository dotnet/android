using System;

namespace Xamarin.Installer.AndroidSDK
{
	/// <summary>
	/// Android SDK System Image ABI
	/// </summary>
	public enum AndroidSystemImageAbi
	{
		/// <summary>
		/// System Image for the x86 architecture
		/// </summary>
		X86,

		/// <summary>
		/// System Image for the x86_64 architecture
		/// </summary>
		X86_64,

		/// <summary>
		/// System Image for the MIPS architecture
		/// </summary>
		Mips,

		/// <summary>
		/// System Image for the arm-v7a architecture
		/// </summary>
		ARMV7a,

		/// <summary>
		/// System Image for the arm64-v8a architecture
		/// </summary>
		ARM64V8a,

		/// <summary>
		/// Represents any ABI
		/// </summary>
		Any = 1000
	}
}

