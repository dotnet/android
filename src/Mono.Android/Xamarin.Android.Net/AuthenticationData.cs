namespace Xamarin.Android.Net
{
	/// <summary>
	/// Contains all the information required to perform pre-authentication of HTTP requests. See <see cref="AndroidMessageHandler"/>.
	/// </summary>
	public class AuthenticationData
	{
		/// <summary>
		/// Gets the authentication scheme. If instance of AuthenticationData comes from the <see cref="AndroidMessageHandler.RequestedAuthentication"/>
		/// collection it will have this property set to the type of authentication as requested by the server, or to <c>AuthenticationScheme.Unsupported</c>/>. 
		/// In the latter case the application is required to provide the authentication module in <see cref="AuthModule"/>.
		/// </summary>
		/// <value>The authentication scheme.</value>
		public AuthenticationScheme Scheme { get; set; } = AuthenticationScheme.None;

		/// <summary>
		/// Contains the full authentication challenge (full value of the WWW-Authenticate HTTP header). This information can be used by the custom
		/// authentication module (<see cref="AuthModule"/>)
		/// </summary>
		/// <value>The challenge.</value>
		public string? Challenge { get; internal set; }

		/// <summary>
		/// Indicates whether authentication performed using data in this instance should be done for the end server or a proxy. If instance of 
		/// AuthenticationData comes from the <see cref="AndroidMessageHandler.RequestedAuthentication"/> collection it will have this property set to
		/// <c>true</c> if authentication request came from a proxy, <c>false</c> otherwise.
		/// </summary>
		/// <value><c>true</c> to use proxy authentication.</value>
		public bool UseProxyAuthentication { get; set; }

		/// <summary>
		/// If the <see cref="Scheme"/> property is set to <c>AuthenticationScheme.Unsupported</c>, this property must be set to an instance of
		/// a class that implements the <see cref="IAndroidAuthenticationModule"/> interface and which understands the authentication challenge contained
		/// in the <see cref="Challenge"/> property.
		/// </summary>
		/// <value>The auth module.</value>
		public IAndroidAuthenticationModule? AuthModule { get; set; }
	}
}
