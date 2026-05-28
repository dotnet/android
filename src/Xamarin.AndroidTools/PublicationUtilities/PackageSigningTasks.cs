using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Mono.AndroidTools.Util;
using System.Linq;

namespace Xamarin.AndroidTools.PublicationUtilities
{
	public static class PackageSigningTasks
	{
		// simply returns true, assuming the process exit code is zero, then the output is irrelevant
		private static Func<string, bool> defaultOutputParser = (s) => true;

		/// <summary>
		/// Queries the given apk and determines if it is signed or not
		/// </summary>
		public static Task<bool> QueryPackageSignatureAsync (string apkFile, CancellationToken token)
		{
			return QueryPackageSignatureAsync (apkFile, token, AndroidSdk.JarsignerExe);
		}

		/// <summary>
		/// Queries the given apk and determines if it is signed or not
		/// </summary>
		public static Task<bool> QueryPackageSignatureAsync (string apkFile, CancellationToken token, string jarsigner)
		{
			// we need to check the output to see if the package signature was verified or not
			Func<string, bool> outputParser = (output) => output.Contains ("verified");

			var args = new ProcessArgumentBuilder ();
			args.Add ("-verify");
			args.AddQuoted (apkFile);

			var toolTask = ProcessUtils.ExecuteToolAsync<bool> (jarsigner, args, outputParser, token);
			return toolTask;
		}

		/// <summary>
		/// Signs the .APK asynchronously
		/// </summary>
		public static Task<bool> SignPackageAsync (AndroidSigningOptions options, string unsignedApk, string signedApk, CancellationToken token)
		{
			var apkSigner = AndroidSdk.ApkSignerJar;
			if (apkSigner != null) {
				// use apk signer instead if it exists
				return SignPackageWithApkSignerAsync (options, unsignedApk, signedApk, token, apkSigner);
			}

			return SignPackageAsync (options, unsignedApk, signedApk, token, AndroidSdk.JarsignerExe);
		}

		/// <summary>
		/// Signs the .APK asynchronously
		/// </summary>
		public static Task<bool> SignPackageAsync (AndroidSigningOptions options, string unsignedApk, string signedApk, CancellationToken token, string jarsigner)
		{
			// Create our Argument list
			var args = new ProcessArgumentBuilder ();
			args.Add ("-keystore");
			args.AddQuoted (options.KeyStore);
			args.Add ("-storepass");
			args.AddQuoted (options.StorePass);
			args.Add ("-keypass");
			args.AddQuoted (options.KeyPass);

			if (!string.IsNullOrEmpty (options.TsaUrl)) {
				args.Add ("-tsa");
				args.AddQuoted (options.TsaUrl);
			}
			
			args.Add ("-digestalg");
			
			if(options.SigningAlgorithm == PackageSigningAlgorithm.SHA256withRSA)
				args.Add("SHA-256");
			else
				args.Add ("SHA1");
			
			args.Add ("-sigalg");

			switch (options.SigningAlgorithm) {
			case PackageSigningAlgorithm.RSA:
				args.Add ("md5withRSA");
				break;
			case PackageSigningAlgorithm.DSA:
				args.Add ("SHA1withDSA");
				break;
			case PackageSigningAlgorithm.SHA256withRSA:
				args.Add("SHA256withRSA");
				break;
			default:
				args.Add ("md5withRSA");
				break;
			}

			args.Add ("-signedjar");
			args.AddQuoted (signedApk, unsignedApk, options.KeyAlias);

			var toolTask = ProcessUtils.ExecuteToolAsync<bool> (jarsigner, args, defaultOutputParser, token);
			return toolTask;
		}

		/// <summary>
		/// Signs the .APK asynchronously
		/// </summary>
		public static Task<bool> SignPackageWithApkSignerAsync (AndroidSigningOptions options, string unsignedApk, string signedApk, CancellationToken token, string apksigner) =>
			SignPackageWithApkSignerAsync (options, unsignedApk, signedApk, token, apksigner, null);

		/// <summary>
		/// Signs the .APK asynchronously with logging
		/// </summary>
		public static Task<bool> SignPackageWithApkSignerAsync (AndroidSigningOptions options, string unsignedApk, string signedApk, CancellationToken token, string apksigner, Action<string> logMessage)
		{
			apksigner = apksigner ?? AndroidSdk.ApkSignerJar;
			bool useJava = apksigner != null && apksigner.EndsWith (".jar", StringComparison.OrdinalIgnoreCase);

			// Create our Argument list
			var fileName = useJava ? AndroidSdk.JavaExe : apksigner;
			var args = new ProcessArgumentBuilder ();
			if (useJava) {
				args.Add ("-jar");
				args.AddQuoted (apksigner);
			}
			args.Add ("sign");
			args.Add ("--ks");
			args.AddQuoted (options.KeyStore);
			args.Add ("--ks-key-alias");
			args.AddQuoted (options.KeyAlias);
			args.Add ("--out");
			args.AddQuoted (signedApk);
			args.Add ("--ks-pass");
			args.AddQuoted ("pass:" + options.StorePass);
			args.Add ("--key-pass");
			args.AddQuoted ("pass:"+ options.KeyPass);

			if (options.MinSdkVersion != null) {
				args.Add ("--min-sdk-version");
				args.Add (options.MinSdkVersion.ToString ());
			}

			//if (!string.IsNullOrEmpty (options.TsaUrl))
			//{
			//	args.Add ("-tsa");
			//	args.AddQuoted (options.TsaUrl);
			//}

			//args.Add ("-digestalg");
			//args.Add ("SHA1");
			//args.Add ("-sigalg");

			//switch (options.SigningAlgorithm)
			//{
			//case PackageSigningAlgorithm.RSA:
			//	args.Add ("md5withRSA");
			//	break;
			//case PackageSigningAlgorithm.DSA:
			//	args.Add ("SHA1withDSA");
			//	break;
			//default:
			//	args.Add ("md5withRSA");
			//	break;
			//}

			args.AddQuoted (unsignedApk);

			logMessage?.Invoke ($"Executing: {fileName} {args}");
			return ProcessUtils.ExecuteToolAsync (fileName, args, result => {
				if (!string.IsNullOrEmpty (result))
					logMessage?.Invoke ($"apksigner: {result}");
				return true;
			}, token);
		}

		/// <summary>
		/// Attempts to determine the signing algorithm to use with the given keystore and alias
		/// </summary>
		public static Task<PackageSigningAlgorithm> DetermineSigningAlgorithm (string keystore, string alias, CancellationToken token)
		{
			return DetermineSigningAlgorithm (keystore, alias, token, AndroidSdk.KeyToolExe);
		}

		/// <summary>
		/// Attempts to determine the signing algorithm to use with the given keystore and alias
		/// </summary>
		public static Task<PackageSigningAlgorithm> DetermineSigningAlgorithm (string keystore, string alias, CancellationToken token, string keytool)
		{
			var args = new ProcessArgumentBuilder ();
			args.Add ("-list");
			args.Add ("-v");
			args.Add ("-keystore");
			args.AddQuoted (keystore);
			args.Add ("-alias");
			args.AddQuoted (alias);
			// make sure the output is in english
			args.Add ("-J\"-Duser.language=en-US\"");

			Action<Process> onStarted = (p) => {
				// press enter when prompted to enter password
				p.StandardInput.WriteLine ();
			};

			var aliasDetailsTask = ProcessUtils.ExecuteToolAsync<string> (keytool, args, (s) => s, token, onStarted);
			return aliasDetailsTask.ContinueWith<PackageSigningAlgorithm> (dt => {
				var lines = dt.Result.Split (new [] { "\n" }, StringSplitOptions.RemoveEmptyEntries);
				var algLine = lines.FirstOrDefault (l => l.Contains ("Signature algorithm name:"));

				if (!string.IsNullOrEmpty (algLine)) {
					var parts = algLine.Trim().Split (new [] { "Signature algorithm name:" }, StringSplitOptions.RemoveEmptyEntries);
					if (parts.Length > 0) {
						return FromString (parts [0]);
					}
				} else {
					// mmm, unexpected
					Mono.AndroidTools.AndroidLogger.LogWarning (string.Format ("Could not determine signing algorithm: {0}", dt.Result));
				}

				return PackageSigningAlgorithm.Unsupported;
			}, TaskContinuationOptions.NotOnFaulted | TaskContinuationOptions.NotOnCanceled );
		}

		static PackageSigningAlgorithm FromString (string alg)
		{
			Mono.AndroidTools.AndroidLogger.LogInfo (string.Format ("Converting signing algorithm from {0}", alg));

			if (!string.IsNullOrEmpty (alg)) {
				var parts = alg.Split (new [] { "with" }, StringSplitOptions.RemoveEmptyEntries);
				if (parts.Length == 2) {
					switch (parts [1]) {
					case "DSA":
						return PackageSigningAlgorithm.DSA;
					case "RSA":
						return PackageSigningAlgorithm.RSA;
					}
				}
			}

			return PackageSigningAlgorithm.Unsupported;
		}

		/// <summary>
		/// Aligns the .APK asynchronously
		/// </summary>
		public static Task<bool> AlignPackageAsync (string srcApk, string destApk, CancellationToken token)
		{
			return AlignPackageAsync (srcApk, destApk, token, AndroidSdk.ZipAlignExe);
		}

		/// <summary>
		/// Aligns the .APK asynchronously
		/// </summary>
		public static Task<bool> AlignPackageAsync (string srcApk, string destApk, CancellationToken token, string zipAlignExe)
		{
			// Create our Argument list
			var args = new ProcessArgumentBuilder ();
			args.Add ("-f");
			args.Add ("4");
			args.AddQuoted (srcApk, destApk);

			var toolTask = ProcessUtils.ExecuteToolAsync<bool> (zipAlignExe, args, defaultOutputParser, token);
			return toolTask;
		}

		/// <summary>
		/// Verifies the alignment of the .APK asynchronously
		/// </summary>
		public static Task<bool> VerifyPackageAlignmentAsync (string apkfileName, CancellationToken token)
		{
			return VerifyPackageAlignmentAsync (apkfileName, token, AndroidSdk.ZipAlignExe);
		}

		/// <summary>
		/// Verifies the alignment of the .APK asynchronously
		/// </summary>
		public static Task<bool> VerifyPackageAlignmentAsync (string apkfileName, CancellationToken token, string zipAlignExe)
		{
			// Create our Argument list
			var args = new ProcessArgumentBuilder ();
			args.Add ("-c");
			args.Add ("4");
			args.AddQuoted (apkfileName);

			var toolTask = ProcessUtils.ExecuteToolAsync<bool> (zipAlignExe, args, defaultOutputParser, token);
			return toolTask;
		}

		/// <summary>
		/// Generates a key-pair asynchronously
		/// </summary>
		public static Task<bool> GenerateKeyPairAsync (AndroidSigningOptions options, string dname, int validity, CancellationToken token)
		{
			return GenerateKeyPairAsync (options, dname, validity, token, AndroidSdk.KeyToolExe);	
		}

		/// <summary>
		/// Generates a key-pair asynchronously
		/// </summary>
		public static Task<bool> GenerateKeyPairAsync (AndroidSigningOptions options, string dname, int validity, CancellationToken token, string keytool)
		{
			// For compatibility with JDK > 1.8, which errors out on an empty `-dname` value
			if (string.IsNullOrEmpty (dname))
				dname = "CN=";

			var dnameParameter = ProcessArgumentBuilder.Quote(dname);
			
			if (OS.IsWindows)
				dnameParameter = dnameParameter.Replace(",", @"\,");

			var args = new ProcessArgumentBuilder ();
			args.Add ("-genkeypair");
			args.Add ("-alias");
			args.AddQuoted (options.KeyAlias);
			args.Add ("-dname");
			args.Add (dnameParameter);
			args.Add ("-storepass");
			args.AddQuoted (options.StorePass);
			args.Add ("-keypass");
			args.AddQuoted (options.KeyPass);
			args.Add ("-keystore");
			args.AddQuoted (options.KeyStore);
			args.Add ("-keysize");
			args.Add ("2048");
			args.Add ("-keyalg");
			args.Add ("RSA");
			if (validity > 0) {
				args.Add ("-validity");
				args.AddQuoted (validity.ToString ());
			}

			var toolTask = ProcessUtils.ExecuteToolAsync<bool> (keytool, args, defaultOutputParser, token);
			return toolTask;
		}

		/// <summary>
		/// Verifies a key-pair asynchronously
		/// </summary>
		public static Task<bool> VerifyKeyPairAsync (AndroidSigningOptions options, CancellationToken token)
		{
			return VerifyKeyPairAsync (options, token, AndroidSdk.KeyToolExe);
		}

		/// <summary>
		/// Verifies a key-pair asynchronously. Note: options.AliasPass is not used, only the other values
		/// </summary>
		public static Task<bool> VerifyKeyPairAsync (AndroidSigningOptions options, CancellationToken token, string keytool)
		{
			var args = new ProcessArgumentBuilder ();
			args.Add ("-list");
			args.AddQuoted ("-keystore", options.KeyStore);
			args.AddQuoted ("-storepass", options.StorePass);
			args.AddQuoted ("-alias", options.KeyAlias);

			var toolTask = ProcessUtils.ExecuteToolAsync<bool> (keytool, args, defaultOutputParser, token);
			return toolTask;
		}

		/// <summary>
		/// Lists the aliases that are stored in the keystore, returns the raw output from keytool
		/// </summary>
		public static Task<string> ListKeyStoreAliasesAsync (string keystore, string storePassword, CancellationToken token)
		{
			return ListKeyStoreAliasesAsync (keystore, storePassword, token, AndroidSdk.KeyToolExe);	
		}

		/// <summary>
		/// Lists the aliases that are stored in the keystore, returns the raw output from keytool
		/// </summary>
		public static Task<string> ListKeyStoreAliasesAsync (string keystore, string storePassword, CancellationToken token, string keytool)
		{
			var args = new ProcessArgumentBuilder ();
			args.Add ("-list");
			args.Add ("-v");
			if (!string.IsNullOrEmpty (storePassword)) {
				args.Add ("-storepass");
				args.AddQuoted (storePassword);
			}
			args.Add ("-keystore");
			args.AddQuoted (keystore);

			Action<Process> onStarted = (p) => {
				// press enter when prompted to enter password
				if (string.IsNullOrEmpty (storePassword))
					p.StandardInput.WriteLine ();
			};

			var toolTask = ProcessUtils.ExecuteToolAsync<string> (keytool, args, (s) => s, token, onStarted);
			return toolTask;
		}

		/// <summary>
		/// Performs a list of a specific alias within a keystore asynchronously.
		/// Used to get information about a specific alias. 
		/// </summary>
		public static Task<string> ListKeyStoreAliasAsync (string keystore, string alias, string storePassword, CancellationToken token)
		{
			return ListKeyStoreAliasAsync (keystore, alias, storePassword, token, AndroidSdk.KeyToolExe);
		}

		/// <summary>
		/// Performs a list of a specific alias within a keystore asynchronously.
		/// Used to get information about a specific alias
		/// </summary>
		public static Task<string> ListKeyStoreAliasAsync (string keystore, string alias, string storePassword, CancellationToken token, string keytool)
		{
			var args = new ProcessArgumentBuilder ();
			args.Add ("-list");
			args.Add ("-v");
			if (!string.IsNullOrEmpty (storePassword)) {
				args.Add ("-storepass");
				args.AddQuoted (storePassword);
			}
			args.Add ("-keystore");
			args.AddQuoted (keystore);
			args.Add ("-alias");
			args.AddQuoted (alias);

			Action<Process> onStarted = (p) => {
				// press enter when prompted to enter password
				if (string.IsNullOrEmpty (storePassword))
					p.StandardInput.WriteLine ();
			};

			var toolTask = ProcessUtils.ExecuteToolAsync<string> (keytool, args, (s) => s, token, onStarted);
			return toolTask;
		}

		/// <summary>
		/// Imports the key from sourceKeystore to destKeystore but uses the same password for the new store
		/// </summary>
		public static Task<bool> ImportKeyAsync (string sourceKeystore, string sourceStorePassword, string alias, string aliasPassword, string destKeystore, CancellationToken token)
		{
			return ImportKeyAsync (sourceKeystore, sourceStorePassword, alias, aliasPassword, destKeystore, token, AndroidSdk.KeyToolExe);
		}

		/// <summary>
		/// Imports the key from sourceKeystore to destKeystore but uses the same password for the new store
		/// </summary>
		public static Task<bool> ImportKeyAsync (string sourceKeystore, string sourceStorePassword, string alias, string aliasPassword, string destKeystore, CancellationToken token, string keytool)
		{
			var args = new ProcessArgumentBuilder ();
			args.Add ("-importkeystore");
			args.Add ("-srckeystore");
			args.AddQuoted (sourceKeystore);
			args.Add ("-destkeystore");
			args.AddQuoted (destKeystore);

			args.Add ("-srcstorepass");
			args.AddQuoted (sourceStorePassword);
			args.Add ("-srcalias");
			args.AddQuoted (alias);
			args.Add ("-srckeypass");
			args.AddQuoted (aliasPassword);

			args.Add ("-deststorepass");
			args.AddQuoted (aliasPassword);
			args.Add ("-destkeypass");
			args.AddQuoted (aliasPassword);

			var toolTask = ProcessUtils.ExecuteToolAsync<bool> (keytool, args, defaultOutputParser, token);
			return toolTask;
		}
	}
}
