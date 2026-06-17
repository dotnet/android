using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace Xamarin.Installer.Common
{
	/// <summary>
	/// Interface to be implemented by the client code to help the installer perform certain tasks that
	/// may be implemented differently in different environments (e.g. Unified Installer vs IDE vs MSBuild
	/// targets in Xamarin.Android)
	/// </summary>
	public interface IHelpers
	{
		/// <summary>
		/// Returns <c>true</c> if the app is running on ARM64 machine
		/// </summary>
		bool IsArm64 { get; }

		/// <summary>
		/// Returns <c>true</c> if the current OS is a 64-bit one
		/// </summary>
		bool Is64BitOS { get; }

		/// <summary>
		/// Gets the home directory of the current user.
		/// </summary>
		/// <value>Home directory of the current user</value>
		string HomeDirectory { get; }

		/// <summary>
		/// Gets the name of the current user.
		/// </summary>
		/// <value>The name of the current user.</value>
		string UserName { get; }

		/// <summary>
		/// Gets a value indicating the current OS has case-sensitive (or case-aware) file systems
		/// </summary>
		/// <value><c>true</c> if the current OS has case sensitive file systems; otherwise, <c>false</c>.</value>
		bool IsCaseSensitiveFileSystem { get; }

		/// <summary>
		/// Determine whether the file at <paramref name="filePath"/> exists and is a special file
		/// (e.g. a paging file on Windows or a character device on Unix)
		/// </summary>
		/// <param name="filePath">Path to the file</param>
		/// <returns><c>true</c> if file is a special one, <c>false</c> otherwise</returns>
		bool IsSpecialFile (string filePath);

		/// <summary>
		/// Copy a special file (such as Windows paging file or Unix character device) from
		/// <paramref name="source"/> to <paramref name="target"/>
		/// </summary>
		/// <param name="source">Source file</param>
		/// <param name="target">Destination file</param>
		void CopySpecialFile (string source, string target);

		/// <summary>
		/// Attempt to determine the content length of the resource at the <paramref name="url"/> URL.
		/// </summary>
		/// <param name="url">URL to obtain content length of</param>
		/// <returns>The resource's content length. 0 should be returned if an error accessing the URL occurs.</returns>
		ulong GetUrlContentLength (Uri url);

		/// <summary>
		/// Unzip the archive found at the path pointed to by <paramref name="archivePath"/> placing its contents in the
		/// directory indicated by <paramref name="baseDirectory"/>. If <paramref name="fileOwnerName"/> is specified, the
		/// extracted files will be owned by that user.
		/// </summary>
		/// <remarks>The <paramref name="archivePath"/> archive is will contain a single top-level directory which should be located by
		/// the unzipper and used, in combination with <paramref name="baseDirectory"/>, as the return value of this method.</remarks>
		/// <param name="baseDirectory">Top-level directory in which to unpack the archive</param>
		/// <param name="archivePath">Path to the compressed archive</param>
		/// <param name="fileOwnerName">Owner of the unpacked content</param>
		/// <returns>Path to the root directory of the *unpacked* content</returns>
		string Unzip (string baseDirectory, string archivePath, string fileOwnerName = null);

		/// <summary>
		/// Unzip the archive found at the path pointed to by <paramref name="archivePath"/> placing its contents in the
		/// directory indicated by <paramref name="baseDirectory"/>. If <paramref name="fileOwnerName"/> is specified, the
		/// extracted files will be owned by that user.
		/// </summary>
		/// <remarks>The <paramref name="archivePath"/> archive is will contain a single top-level directory which should be located by
		/// the unzipper and used, in combination with <paramref name="baseDirectory"/>, as the return value of this method.</remarks>
		/// <param name="baseDirectory">Top-level directory in which to unpack the archive</param>
		/// <param name="archivePath">Path to the compressed archive</param>
		/// <param name="fileOwnerName">Owner of the unpacked content</param>
		/// <param name="progressCallback"></param>
		/// <returns>Path to the root directory of the *unpacked* content</returns>
		string Unzip (string baseDirectory, string archivePath, string fileOwnerName = null, InstallationProgressEventArgs.InstallationProgressActionDelegate progressCallback = null);

		/// <summary>
		/// Download contents of the given <paramref name="url"/> and place it in the <paramref name="output"/> string
		/// </summary>
		/// <returns><c>true</c>, if download was successful, <c>false</c> otherwise.</returns>
		/// <param name="url">URL to download from</param>
		/// <param name="output">Output string</param>
		bool DownloadToString (Uri url, out string output);

		/// <summary>
		/// Checks whether web resource pointed to by <paramref name="url"/> exists.
		/// </summary>
		/// <returns><c>true</c>, if URL exists, <c>false</c> otherwise.</returns>
		/// <param name="url">URL to check the existence of</param>
		bool URLExists (Uri url);

		/// <summary>
		/// Gets the registry key value. On platforms other than Windows it should return an empty string.
		/// </summary>
		/// <returns>The registry key value or empty string on platforms other than Windows.</returns>
		/// <param name="subKeyPath">Sub key path.</param>
		/// <param name="keyName">Key name.</param>
		/// <param name="check64Node">If set to <c>true</c> check the 32-bit registry hive on 64-bit windows.</param>
		string GetRegistryKeyValue (string subKeyPath, string keyName, bool check64Node);

		/// <summary>
		/// Look up translation of the provided string.
		/// </summary>
		/// <returns>Translated string</returns>
		/// <param name="s">String to look up translation of</param>
		string GetString (string s);

		/// <summary>
		/// Look up translation of the provided string, for strings which include numerals.
		/// </summary>
		/// <returns>Translated string</returns>
		/// <param name="s">String to look up translation of, singular form</param>
		/// <param name="p">String to look up translation of, plural form</param>
		/// <param name="n">integer used to select plural or singular form of the string</param>
		string GetPluralString (string s, string p, int n);

		/// <summary>
		/// Gets existing property value by its key
		/// </summary>
		/// <param name="key">Property key</param>
		/// <param name="defaultValue">Default value</param>
		/// <returns>Property value or <c>defaultValue</c></returns>
		string GetProperty (string key, string defaultValue = "");

		/// <summary>
		/// Sets a new property or updates an existing one
		/// </summary>
		/// <param name="key">Property key</param>
		/// <param name="value">Property value</param>
		void SetProperty (string key, string value);

		Task<bool> CheckIfNetworkIsAvailableAsync ();
	}
}
