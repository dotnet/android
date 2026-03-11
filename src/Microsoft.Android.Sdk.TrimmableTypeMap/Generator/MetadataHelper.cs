using System;
using System.Security.Cryptography;
using System.Text;

namespace Microsoft.Android.Sdk.TrimmableTypeMap;

static class MetadataHelper
{
	/// <summary>
	/// Produces a deterministic MVID from the module name so that identical inputs produce identical assemblies.
	/// </summary>
	public static Guid DeterministicMvid (string moduleName)
	{
		using var sha = SHA256.Create ();
		byte [] hash = sha.ComputeHash (Encoding.UTF8.GetBytes (moduleName));
		byte [] guidBytes = new byte [16];
		Array.Copy (hash, guidBytes, 16);
		return new Guid (guidBytes);
	}
}
