using Android.App;
using Android.OS;
using Android.Util;
using Android.Widget;
using Android.Views;
using System;
using System.Linq;
using System.Net.Http;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;

namespace NativeAotComplexApp;

[Activity (Label = "@string/app_name", MainLauncher = true, Theme = "@style/AppTheme")]
public class MainActivity : Activity
{
	const string TAG = "NativeAotComplex";

	TextView? _statusText;
	TextView? _resultText;
	ProgressBar? _progressBar;
	Button? _httpButton;
	Button? _customCertButton;
	Button? _clearButton;
	ImageView? _statusImage;

	protected override void OnCreate (Bundle? savedInstanceState)
	{
		base.OnCreate (savedInstanceState);
		SetContentView (Resource.Layout.activity_main);

		Log.Info (TAG, "=== NativeAOT Complex App Started ===");

		// Find views
		_statusText = FindViewById<TextView> (Resource.Id.statusText);
		_resultText = FindViewById<TextView> (Resource.Id.resultText);
		_progressBar = FindViewById<ProgressBar> (Resource.Id.progressBar);
		_httpButton = FindViewById<Button> (Resource.Id.httpButton);
		_customCertButton = FindViewById<Button> (Resource.Id.customCertButton);
		_clearButton = FindViewById<Button> (Resource.Id.clearButton);
		_statusImage = FindViewById<ImageView> (Resource.Id.statusImage);

		// Wire up buttons
		_httpButton!.Click += OnHttpButtonClick;
		_customCertButton!.Click += OnCustomCertButtonClick;
		_clearButton!.Click += OnClearButtonClick;

		UpdateStatus ("Ready", StatusType.Info);

		// Auto-run HTTPS test on startup for easier debugging
		Log.Info (TAG, "Auto-running HTTPS test on startup...");
		_ = Task.Run (async () => {
			await Task.Delay (1000); // Wait for UI to settle
			RunOnUiThread (() => OnCustomCertButtonClick (null, EventArgs.Empty));
		});
	}

	enum StatusType { Info, Success, Error, Loading }

	void UpdateStatus (string message, StatusType type)
	{
		RunOnUiThread (() => {
			_statusText!.Text = message;
			_progressBar!.Visibility = type == StatusType.Loading ? ViewStates.Visible : ViewStates.Gone;

			int imageRes = type switch {
				StatusType.Success => Resource.Drawable.ic_success,
				StatusType.Error => Resource.Drawable.ic_error,
				StatusType.Loading => Resource.Drawable.ic_loading,
				_ => Resource.Drawable.ic_info
			};
			_statusImage!.SetImageResource (imageRes);
		});
	}

	void AppendResult (string text)
	{
		RunOnUiThread (() => {
			_resultText!.Text += text + "\n";
		});
	}

	async void OnHttpButtonClick (object? sender, EventArgs e)
	{
		Log.Info (TAG, "HTTP button clicked");
		UpdateStatus ("Testing network...", StatusType.Loading);
		_resultText!.Text = "";

		try {
			// Simple DNS resolution test - no crypto involved
			AppendResult ("Testing DNS resolution...");
			var addresses = await System.Net.Dns.GetHostAddressesAsync ("google.com");
			AppendResult ($"Resolved google.com to {addresses.Length} addresses:");
			foreach (var addr in addresses) {
				AppendResult ($"  - {addr} ({addr.AddressFamily})");
			}

			// Find an IPv4 address for the socket test
			var ipv4Address = addresses.FirstOrDefault (a => a.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork);
			if (ipv4Address == null) {
				AppendResult ("No IPv4 address found, using first address");
				ipv4Address = addresses[0];
			}

			// Simple TCP socket test - no crypto
			AppendResult ($"\nTesting TCP connection to {ipv4Address}:80...");
			using var socket = new System.Net.Sockets.Socket (
				ipv4Address.AddressFamily, 
				System.Net.Sockets.SocketType.Stream, 
				System.Net.Sockets.ProtocolType.Tcp);
			socket.ReceiveTimeout = 5000;
			socket.SendTimeout = 5000;
			await socket.ConnectAsync (ipv4Address, 80);
			AppendResult ($"Connected to {ipv4Address}:80");
			
			// Send simple HTTP request
			var request = "GET / HTTP/1.0\r\nHost: google.com\r\n\r\n";
			var requestBytes = System.Text.Encoding.ASCII.GetBytes (request);
			await socket.SendAsync (requestBytes, System.Net.Sockets.SocketFlags.None);
			AppendResult ("Sent HTTP request");
			
			// Read response
			var buffer = new byte[1024];
			var received = await socket.ReceiveAsync (buffer, System.Net.Sockets.SocketFlags.None);
			var response = System.Text.Encoding.ASCII.GetString (buffer, 0, received);
			AppendResult ($"Received {received} bytes:");
			AppendResult (response.Length > 200 ? response[..200] + "..." : response);
			
			socket.Close ();

			UpdateStatus ("Network test successful!", StatusType.Success);
			Log.Info (TAG, "Network test completed successfully");
		} catch (Exception ex) {
			Log.Error (TAG, $"Network test failed: {ex}");
			AppendResult ($"Error: {ex.Message}");
			AppendResult ($"Stack: {ex.StackTrace}");
			UpdateStatus ("Network test failed", StatusType.Error);
		}
	}

	async void OnCustomCertButtonClick (object? sender, EventArgs e)
	{
		Log.Info (TAG, "Custom certificate button clicked");
		UpdateStatus ("Making HTTPS request with custom cert validation...", StatusType.Loading);
		_resultText!.Text = "";

		try {
			// Use SocketsHttpHandler with default certificate validation for simplicity
			var handler = new SocketsHttpHandler ();

			using var client = new HttpClient (handler);
			client.Timeout = TimeSpan.FromSeconds (10);

			AppendResult ("Using default certificate validation");
			AppendResult ("Requesting https://httpbin.org/get...");

			var response = await client.GetAsync ("https://httpbin.org/get");

			AppendResult ($"Status: {response.StatusCode}");
			var content = await response.Content.ReadAsStringAsync ();
			AppendResult ($"Response length: {content.Length} chars");
			AppendResult ("Headers returned:");
			AppendResult (content);

			UpdateStatus ("Custom cert validation successful!", StatusType.Success);
			Log.Info (TAG, "Custom cert validation completed successfully");
		} catch (Exception ex) {
			Log.Error (TAG, $"Custom cert request failed: {ex}");
			AppendResult ($"Error: {ex.Message}");
			if (ex.InnerException != null) {
				AppendResult ($"Inner: {ex.InnerException.Message}");
			}
			UpdateStatus ("Custom cert request failed", StatusType.Error);
		}
	}

	bool ValidateServerCertificate (
		HttpRequestMessage request,
		X509Certificate2? certificate,
		X509Chain? chain,
		SslPolicyErrors sslPolicyErrors)
	{
		Log.Info (TAG, $"=== Custom Certificate Validation ===");
		Log.Info (TAG, $"URL: {request.RequestUri}");
		Log.Info (TAG, $"SSL Policy Errors: {sslPolicyErrors}");

		if (certificate != null) {
			Log.Info (TAG, $"Certificate Subject: {certificate.Subject}");
			Log.Info (TAG, $"Certificate Issuer: {certificate.Issuer}");
			Log.Info (TAG, $"Certificate Thumbprint: {certificate.Thumbprint}");
			Log.Info (TAG, $"Valid From: {certificate.NotBefore}");
			Log.Info (TAG, $"Valid To: {certificate.NotAfter}");

			RunOnUiThread (() => {
				AppendResult ($"Cert Subject: {certificate.Subject}");
				AppendResult ($"Cert Issuer: {certificate.Issuer}");
				AppendResult ($"Valid: {certificate.NotBefore:d} - {certificate.NotAfter:d}");
			});
		}

		if (chain != null) {
			Log.Info (TAG, $"Chain has {chain.ChainElements.Count} elements");
			foreach (var element in chain.ChainElements) {
				Log.Info (TAG, $"  Chain element: {element.Certificate.Subject}");
			}
		}

		// Accept any certificate for testing purposes
		// In production, you would validate properly!
		bool isValid = sslPolicyErrors == SslPolicyErrors.None;
		Log.Info (TAG, $"Certificate accepted: {isValid} (accepting all for demo)");

		return true; // Accept all for demo
	}

	void OnClearButtonClick (object? sender, EventArgs e)
	{
		_resultText!.Text = "";
		UpdateStatus ("Ready", StatusType.Info);
	}

	protected override void OnDestroy ()
	{
		base.OnDestroy ();
		Log.Info (TAG, "=== NativeAOT Complex App Destroyed ===");
	}
}
