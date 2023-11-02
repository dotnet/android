namespace Xamarin.Android.Net
{
	/// <summary>
	/// Authentication schemes supported by <see cref="AndroidMessageHandler"/>
	/// </summary>
	public enum AuthenticationScheme
	{
		/// <summary>
		/// Default value used in <see cref="AuthenticationData.Scheme"/>
		/// </summary>
		None,

		/// <summary>
		/// <see cref="AndroidMessageHandler"/> doesn't support this scheme, the application must provide its own value. See <see cref="AuthenticationData.Scheme"/>
		/// </summary>
		Unsupported,

		/// <summary>
		/// The HTTP Basic authentication scheme
		/// </summary>
		Basic,

		/// <summary>
		/// The HTTP Digest authentication scheme
		/// </summary>
		Digest
	}
}
