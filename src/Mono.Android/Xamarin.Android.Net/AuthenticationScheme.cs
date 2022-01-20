namespace Xamarin.Android.Net
{
	/// <summary>
#if MONOANDROID1_0
	/// Authentication schemes supported by <see cref="AndroidClientHandler"/>
#else
	/// Authentication schemes supported by <see cref="AndroidMessageHandler"/>
#endif
	/// </summary>
	public enum AuthenticationScheme
	{
		/// <summary>
		/// Default value used in <see cref="AuthenticationData.Scheme"/>
		/// </summary>
		None,

		/// <summary>
#if MONOANDROID1_0
		/// <see cref="AndroidClientHandler"/> doesn't support this scheme, the application must provide its own value. See <see cref="AuthenticationData.Scheme"/>
#else
		/// <see cref="AndroidMessageHandler"/> doesn't support this scheme, the application must provide its own value. See <see cref="AuthenticationData.Scheme"/>
#endif
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
