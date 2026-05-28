//
// KeyManagement.cs
//
// Author:
//       Greg Munn <greg.munn@xamarin.com>
//
// Copyright (c) 2014 Xamarin Inc
//
using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Text;
using Mono.AndroidTools;
using System.Xml.Linq;
using System.Threading;
using Xamarin.AndroidTools.Properties;

namespace Xamarin.AndroidTools.PublicationUtilities
{
	public static class KeyManagement
	{
		static string keystoreBaseDir;

		public enum Platform
		{
			Mac,
			Windows
		}

		static KeyManagement ()
		{
			if (Mono.AndroidTools.Util.Platform.IsMac) {
				Initialize (KeyManagement.Platform.Mac);	
			} else {
				Initialize (KeyManagement.Platform.Windows);	
			}
		}

		/// <summary>
		/// Used by test fixtures to set an arbitrary keystore folder
		/// </summary>
		internal static void OverrideKeystoreBaseDirectory (string folder)
		{
			keystoreBaseDir = folder;
		}

		static void Initialize (Platform platform)
		{
			switch (platform) {
			case Platform.Mac:
				var macHome = Environment.GetFolderPath (Environment.SpecialFolder.UserProfile);
				keystoreBaseDir = Path.Combine (macHome, "Library", "Developer", "Xamarin", "Keystore");
				break;
			case Platform.Windows:
				var winHome = Environment.GetFolderPath (Environment.SpecialFolder.LocalApplicationData);
				keystoreBaseDir = Path.Combine (winHome, "Xamarin", "Mono for Android", "Keystore");
				break;
			default:
				throw new InvalidOperationException (Resources.CreateKeyError_UnsupportedPlatform);
			}
		}

		public static void DeleteKey (KeystoreEntry key)
		{
			var dir = Path.GetDirectoryName (key.Keystore);
			Directory.Delete (dir, true);
		}

		public static Task<KeystoreEntry> GetKeyAsync(string keystore)
		{
			if (string.IsNullOrWhiteSpace(keystore) || !File.Exists(keystore))
				return Task.FromResult(default(KeystoreEntry));

			CheckInitialized();

			return Task.Run(() =>
			{
				var alias = Path.GetFileNameWithoutExtension(keystore);

				var aliasInfo = GetAliasInfo(keystore);
				if (string.IsNullOrEmpty(aliasInfo.Item2))
				{
					return new KeystoreEntry(keystore, alias, aliasInfo.Item1, string.Empty);
				}
				else
				{
					return new KeystoreEntry(keystore, alias, aliasInfo.Item1, aliasInfo.Item2);
				}
			});
		}

		public static Task<List<KeystoreEntry>> ListManagedKeysAsync ()
		{
			CheckInitialized ();

			// iterate over the .keystore files that are in the baedir
			var tcs = new TaskCompletionSource<List<KeystoreEntry>> ();
			var result = new List<KeystoreEntry> ();
			int missingInfoCount = 0;

			if (!Directory.Exists (keystoreBaseDir)) {
				tcs.SetResult (new List<KeystoreEntry> ());
				return tcs.Task;
			}

			var keystores = Directory.GetFiles (keystoreBaseDir, "*.keystore", SearchOption.AllDirectories);
			foreach (var key in keystores) {
				var alias = Path.GetFileNameWithoutExtension (key);

				var aliasInfo = GetAliasInfo (key);
				if (string.IsNullOrEmpty (aliasInfo.Item2)) {
					// we don't have it, get from keytool and update...
					// we lock on the result list to allow tasks to sync info
					lock (result) {
						missingInfoCount++;
					}

					ExtractAliasInfoAsync (key, aliasInfo.Item1).ContinueWith (lt => {
						// we have returned with a list of aliases in the key (we expect there should only be one and 
						// we can now update our creation date info

						lock (result) {
							missingInfoCount--;

							if (!lt.IsFaulted) {
								foreach (var entry in lt.Result) {
									result.Add (entry);
									WriteCreationDateInfo (entry.Keystore, entry.CreationDate, entry.ValidityInfo);
								}
							}

							if (missingInfoCount <= 0) {
								tcs.SetResult (result);
							}
						}
					});
				} else {
					lock (result)
						result.Add (new KeystoreEntry (key, alias, aliasInfo.Item1, aliasInfo.Item2));
				}
			}

			lock (result) {
				if (missingInfoCount <= 0) {
					tcs.SetResult (result);
				}
			}

			return tcs.Task;
		}

		/// <summary>
		/// Lists the aliases that are in the given keystore. Throws if the password is incorrect.
		/// </summary>
		public static Task<List<KeystoreEntry>> ListKeystoreAliasesAsync (string store, string storePassword)
		{
			var listTask = PackageSigningTasks.ListKeyStoreAliasesAsync (store, storePassword, CancellationToken.None);

			return listTask.ContinueWith<List<KeystoreEntry>> (t => {
				if (t.IsFaulted) {
					// observe the error
					AndroidLogger.LogError ("keytool", "ListKeytoreAliases - {0}", t.Exception);

					var toolEx = t.Exception.InnerException as AndroidSdkToolException;
					if (toolEx != null) {
						throw new InvalidOperationException (toolEx.ToolErrorMessage);
					}

					throw t.Exception.InnerException;
				}

				var aliasInfo = ExtractAliasInfo (t.Result);
				return aliasInfo.Select (x => new KeystoreEntry (store, x.Item1, x.Item2, x.Item3)).ToList ();
			});
		}

		/// <summary>
		/// Special version of ListKeystoreAliasesAsync that assumes that there will only be one alias in the keystore because it is a managed keystore.
		/// This is to ensure that no matter what happens when we go to get additional information from the keystore that we always return a KeyStoreEntry
		/// </summary>
		static  Task<List<KeystoreEntry>> ExtractAliasInfoAsync (string store, DateTime currentCreationTimestamp)
		{
			var listTask = PackageSigningTasks.ListKeyStoreAliasesAsync (store, null, CancellationToken.None);

			return listTask.ContinueWith<List<KeystoreEntry>> (t => {
				if (t.IsFaulted) {
					// observe the error
					AndroidLogger.LogError ("keytool", "ListKeytoreAliases - {0}", t.Exception);

					var aliasName = Path.GetFileNameWithoutExtension (store);
					var entry = new KeystoreEntry (store, aliasName, currentCreationTimestamp, string.Empty);
					return new [] { entry }.ToList ();
				}

				var aliasInfo = ExtractAliasInfo (t.Result);

				if (aliasInfo.Count > 0) {
					return aliasInfo.Select (x => new KeystoreEntry (store, x.Item1, x.Item2, x.Item3)).ToList ();
				}
				else {
					var aliasName = Path.GetFileNameWithoutExtension (store);
					var entry = new KeystoreEntry (store, aliasName, currentCreationTimestamp, string.Empty);
					return new [] { entry }.ToList ();
				}
			});
		}

		/// <summary>
		/// Gets the information about the alias. Returns the detail as is from keytool
		/// </summary>
		public static Task<string> GetAliasDetailAsync (string store, string alias, string storePassword)
		{
			// we could just return the task you know.....
			var listTask = PackageSigningTasks.ListKeyStoreAliasAsync (store, alias, storePassword, CancellationToken.None);

			return listTask.ContinueWith<string> (t => {
				if (t.IsFaulted) {
					// observe the error
					AndroidLogger.LogError ("keytool", "ListKeyStoreAlias - {0}", t.Exception);

					var toolEx = t.Exception.InnerException as AndroidSdkToolException;
					if (toolEx != null) {
						throw new InvalidOperationException (toolEx.ToolErrorMessage);
					}

					throw t.Exception.InnerException;
				}

				return t.Result;
			});
		}

		/// <summary>
		/// Creates a key with the given alias and returns the store in which the key was created.
		/// </summary>
		public static Task<string> CreateKeyAsync (string alias, string password, string dname, int validity)
		{
			if (string.IsNullOrEmpty (alias))
				throw new ArgumentNullException ("alias");
			if (string.IsNullOrEmpty (password))
				throw new ArgumentNullException ("password");
			if (string.IsNullOrEmpty (dname))
				throw new ArgumentNullException ("dname");
			if (validity < 1)
				throw new ArgumentException (Resources.CreateKeyError_MinimumOneYearValidity, "validity");
			if (Path.GetInvalidFileNameChars ().Intersect (alias).Any ())
				throw new InvalidDataException (Resources.CreateKeyError_AliasContainsInvalidCharacters);

			var options = new AndroidSigningOptions {
				KeyAlias = alias,
				KeyPass = password,
				KeyStore = CreateStoreFilename (alias),
				StorePass = password,
			};

			var keyTask = PackageSigningTasks.GenerateKeyPairAsync (options, dname, validity, CancellationToken.None);

			return keyTask.ContinueWith<string> ((t) => {
				if (t.IsFaulted) {
					// observe the error
					AndroidLogger.LogError ("keytool", "CreateKey - {0}", t.Exception);

					var toolEx = t.Exception.InnerException as AndroidSdkToolException;
					if (toolEx != null) {
						throw new InvalidOperationException (toolEx.ToolErrorMessage);
					}

					throw t.Exception.InnerException;
				}

				if (!t.Result) {
					// observe the error
					AndroidLogger.LogError ("keytool", "CreateKey failed");
					throw new InvalidOperationException (Resources.CreateKeyError_GenericError);
				}

				WriteCreationDateInfo (options.KeyStore, DateTime.Today, string.Empty);
				return options.KeyStore;
			});
		}

		/// <summary>
		/// Creates a key with the given alias and returns the store in which the key was created.
		/// </summary>
		public static Task<string> CreateKeyAsync (string alias, string password, string commonName, string organizationUnit, string organization, string locality, string state, string country, int validity)
		{
			var dname = GetDNameFromValues (commonName, organizationUnit, organization, locality, state, country);

			return CreateKeyAsync (alias, password, dname, validity);
		}

		/// <summary>
		/// Imports a given key from a keystore and returns the path to the new store to which it was imported.
		/// A new store is guaranteed to be created for the imported key.
		/// </summary>
		public static Task<string> ImportKeyAsync (string keystore, string alias, string storePassword, string aliasPassword, DateTime creationDate)
		{
			var storeFilename = CreateStoreFilename (alias);

			if (!File.Exists (keystore))
				throw new FileNotFoundException (Resources.CreateKeyError_KeyStoreNotFound, "keystore");

			var listTask = PackageSigningTasks.ImportKeyAsync (keystore, storePassword, alias, aliasPassword, storeFilename, CancellationToken.None);

			return listTask.ContinueWith<string> ((t) => {
				if (t.IsFaulted) {
					// observe the error
					AndroidLogger.LogError ("keytool", "ImportKey - {0}", t.Exception);

					var toolEx = t.Exception.InnerException as AndroidSdkToolException;
					if (toolEx != null) {
						throw new InvalidOperationException (toolEx.ToolErrorMessage);
					}

					throw t.Exception.InnerException;
				}

				if (!t.Result) {
					// observe the error
					AndroidLogger.LogError ("keytool", "ImportKey failed");
					throw new InvalidOperationException (Resources.CreateKeyError_ErrorImportingKey);
				}

				WriteCreationDateInfo (storeFilename, creationDate, string.Empty);
				return storeFilename;
			});
		}

		public static string GetDNameFromValues (string [] values)
		{
			var sb = new StringBuilder ();

			for (int i = 0; i < values.Length; i++) {
				string value = values [i];
				if (string.IsNullOrEmpty (value))
					continue;

				if (sb.Length > 0)
					sb.Append (", ");

				switch (i) {
				case 0: sb.Append ("CN=");
					break;
				case 1: sb.Append ("OU=");
					break;
				case 2: sb.Append ("O=");
					break;
				case 3: sb.Append ("L=");
					break;
				case 4: sb.Append ("S=");
					break;
				case 5: sb.Append ("C=");
					break;
				}
				sb.Append (GetEscapedDnameValue (value));
			}

			return sb.ToString ();
		}

		public static string GetDNameFromValues (string commonName, string organizationUnit, string organization, string locality, string state, string country)
		{
			return GetDNameFromValues (new [] { commonName, organizationUnit, organization, locality, state, country });
		}

		public static bool IsValidAlias (string alias)
		{
			if (string.IsNullOrEmpty (alias))
				return false;

			if (Path.GetInvalidFileNameChars ().Intersect (alias).Any ())
				return false;

			return true;
		}

		public static string GetErrorText (AggregateException ex)
		{
			if (ex.InnerExceptions.Count == 1) {
				var message = ex.InnerExceptions [0].Message;

				message = message.Replace ("keytool error: java.io.IOException:", string.Empty).Trim ();

				return message;
			}

			return ex.Message;
		}

		static string GetEscapedDnameValue (string value)
		{
			return value.Replace (@",", @"\,");
		}

		static string CreateStoreFilename (string alias) 
		{
			CheckInitialized ();

			var aliasDir = CreateStoreDirectory (keystoreBaseDir, alias);

			return Path.Combine (aliasDir, alias + ".keystore");
		}

		static string CreateStoreDirectory (string baseDir, string alias)
		{
			string aliasDir;
			int unique = 1;
			string name;

			do {
				if (unique > 1)
					name = string.Format ("{0} - {1}", alias, unique);
				else
					name = alias;

				aliasDir = Path.Combine (baseDir, name);
				unique++;
			} while (Directory.Exists (aliasDir));

			Directory.CreateDirectory (aliasDir);
			return aliasDir;
		}

		static Tuple<DateTime, string> GetAliasInfo (string keystoreFile)
		{
			var keystoreInfoFile = Path.ChangeExtension (keystoreFile, ".keyInfo");

			if (File.Exists (keystoreInfoFile)) {
				try {
					var doc = XDocument.Load (keystoreInfoFile);
					var creationDate = (DateTime)doc.Root.Element ("CreationDate");
					var validityInfo = (string)doc.Root.Element ("ValidityInfo");
					return new Tuple<DateTime, string> (creationDate, validityInfo);
				}
				catch {
					return new Tuple<DateTime, string> (DateTime.MinValue, string.Empty);
				}
			}

			return new Tuple<DateTime, string> (DateTime.MinValue, string.Empty);
		}

		static void WriteCreationDateInfo (string keystoreFile, DateTime creationDate, string validityInfo)
		{
			try {
				var keystoreInfoFile = Path.ChangeExtension (keystoreFile, ".keyInfo");

				var doc = new XDocument ();
				doc.Add (new XElement ("KeyStore"));
				doc.Root.Add (new XElement ("CreationDate", creationDate));
				doc.Root.Add (new XElement ("ValidityInfo", validityInfo));

				doc.Save (keystoreInfoFile);
			}
			catch (Exception ex) {
				AndroidLogger.LogError ("KeyStoreManagement - WriteCreationInfo", ex);
			}
		}

		/// <summary>
		/// Extracts the alias names, creation dates and validity from the keytool -list -v command output
		/// </summary>
		public static List<Tuple<string, DateTime, string>> ExtractAliasInfo (string listOutput)
		{
			/* list output looks a little like this
			Keystore type: JKS
			Keystore provider: SUN

			Your keystore contains 2 entries

			Alias name: Alias 1
			Creation date: Aug 14, 2014
			Entry type: PrivateKeyEntry
			Certificate chain length: 1
			Certificate[1]:
			Owner: CN=Me
			Issuer: CN=Me
			Serial number: 53ecc39d
			Valid from: Thu Aug 14 10:11:41 EDT 2014 until: Sat Sep 13 10:11:41 EDT 2014
			Certificate fingerprints:
				 MD5:  48:21:C7:99:74:FC:43:41:9B:52:EA:98:78:86:49:AD
				 SHA1: 3C:7E:3F:FE:26:9B:7B:76:1E:CF:84:AF:0F:92:69:B5:07:B9:24:03
				 Signature algorithm name: SHA1withRSA
				 Version: 3


			*******************************************
			*******************************************

			and then repeats

			because of localisatin from the output, we will have to rely on non-localisable output to find the information we want
			this will be the ": PrivateKeyEntry" and the "*******************************************"'s
			*/

			const string privateKeyEntryTag = ": PrivateKeyEntry";
			const string starsTag = "*******************************************";

			var result = new List<Tuple<string, DateTime, string>> ();

			var lines = listOutput.Split (new [] { "\n" }, StringSplitOptions.RemoveEmptyEntries);

			string alias = null;
			string creationDateText = null;
			string validityText = null;
			DateTime creationDate = DateTime.MinValue;

			for (int lineNumber = 0; lineNumber < lines.Length; lineNumber++) {
				var line = lines [lineNumber];

				if (line.IndexOf (privateKeyEntryTag, StringComparison.InvariantCultureIgnoreCase) != -1) {
					// we are on a line containing the PrivateKeyEntry tag
					// look behind to the alias name and creation date
					if (lineNumber > 2) {
						var aliasLine = lines [lineNumber - 2];
						var creationInfoLine = lines [lineNumber - 1];

						var ix = aliasLine.IndexOf (":", StringComparison.InvariantCultureIgnoreCase);
						alias = aliasLine.Substring (ix + 1).Trim ();

						ix = creationInfoLine.IndexOf (":", StringComparison.InvariantCultureIgnoreCase);
						creationDateText = creationInfoLine.Substring (ix + 1).Trim ();
					}

					if (lineNumber < lines.Length - 6) {
						var validityLine = lines [lineNumber + 6];

						var ix = validityLine.IndexOf (":", StringComparison.InvariantCultureIgnoreCase);
						validityText = validityLine.Substring (ix + 1).Trim ();
					}

					if (alias != null) {
						if (!DateTime.TryParse (creationDateText, out creationDate))
							creationDate = DateTime.MinValue;
						result.Add (new Tuple<string, DateTime, string> (alias, creationDate, validityText));
					}
				}

				if (line.IndexOf (starsTag, StringComparison.InvariantCultureIgnoreCase) != -1) {
					alias = null;
					creationDateText = null;
					validityText = null;
					creationDate = DateTime.MinValue;
				}
			}

			return result;
		}

		static void CheckInitialized ()
		{
			if (keystoreBaseDir == null)
				throw new InvalidOperationException (Resources.CreateKeyError_NotInitialized);
		}
	}
}
