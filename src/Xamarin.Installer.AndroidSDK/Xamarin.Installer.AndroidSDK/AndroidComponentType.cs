using System;

namespace Xamarin.Installer.AndroidSDK
{
	/// <summary>
	/// Android component type. This serves as an easier way to find what type an instance of
	/// <see cref="T:Xamarin.Installer.AndroidSDK.Common.IAndroidComponent"/> represents.
	/// </summary>
	public enum AndroidComponentType
	{
		/// <summary>
		/// Component is an addon
		/// </summary>
		Addon,

		/// <summary>
		/// Component is an extra addition to the SDK
		/// </summary>
		Extra,

		/// <summary>
		/// A generic component
		/// </summary>
		Generic,

		/// <summary>
		/// A Maven component
		/// </summary>
		Maven,

		/// <summary>
		/// An API level (platform) component
		/// </summary>
		Platform,

		/// <summary>
		/// A source component
		/// </summary>
		Source,

		/// <summary>
		/// A system image component
		/// </summary>
		SystemImage,

		/// <summary>
		/// Unknown type of the component
		/// </summary>
		Unknown = 1000
	}
}
