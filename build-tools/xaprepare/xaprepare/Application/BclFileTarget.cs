namespace Xamarin.Android.Prepare
{
	/// <summary>
	///   Installation target of the BCL file. <seealso cref="BclFile"/>
	/// </summary>
	enum BclFileTarget
	{
		/// <summary>
		///   Install for Android
		/// </summary>
		Android,

		/// <summary>
		///   Install for Android Designer on the current host operating system
		/// </summary>
		DesignerHost,

		/// <summary>
		///   Install for Android Designer on Windows
		/// </summary>
		DesignerWindows,
	}
}
