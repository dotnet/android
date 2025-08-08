using System;

namespace Xamarin.ProjectTools
{
	/// <summary>
	/// Specifies the Android linker mode for IL trimming in Xamarin.Android projects.
	/// Determines how aggressively the linker removes unused code to reduce application size.
	/// </summary>
	/// <remarks>
	/// The Android linker analyzes IL code and removes unused methods, types, and assemblies
	/// to reduce the final application size. Different modes provide different levels of
	/// optimization with varying degrees of safety.
	/// </remarks>
	/// <seealso cref="TrimMode"/>
	public enum AndroidLinkMode
	{
		/// <summary>
		/// No linking is performed. All code is preserved in the final application.
		/// </summary>
		None,
		
		/// <summary>
		/// Only Android SDK and BCL assemblies are linked. User assemblies are preserved.
		/// </summary>
		SdkOnly,
		
		/// <summary>
		/// All assemblies including user assemblies are linked for maximum size reduction.
		/// </summary>
		Full,
	}

	/// <summary>
	/// Specifies the trim mode for .NET trimming in modern .NET Android projects.
	/// Determines the aggressiveness of IL trimming for size optimization.
	/// </summary>
	/// <remarks>
	/// Trim modes are used in .NET 6+ Android projects to control how the .NET trimmer
	/// removes unused code. This is similar to but distinct from the traditional
	/// Xamarin.Android linker modes.
	/// </remarks>
	/// <seealso cref="AndroidLinkMode"/>
	public enum TrimMode
	{
		/// <summary>
		/// Partial trimming removes some unused code while preserving reflection safety.
		/// </summary>
		Partial,
		
		/// <summary>
		/// Full trimming aggressively removes unused code for maximum size reduction.
		/// </summary>
		Full,
	}
}
