using System;
using System.Runtime.InteropServices;

using NUnit.Framework;

namespace AndroidRuntimeTests
{
	[TestFixture]
	public class PInvokeOverrideTests
	{
		[DllImport ("libmonosgen-2.0", CallingConvention = CallingConvention.Cdecl)]
		static extern bool mono_config_is_server_mode ();

		[Test]
		public void CheckThirdPartySharedLibrariesWork ()
		{
			// We need to test if a library that is neither a .NET for Android internal one nor a standard DotNet one is properly cached in the robin_map p/invoke
			// override cache. The only such library that we can count to always be present in the apk is `libmonosgen-2.0.so`. It doesn't matter which API we call,
			// what it does and what returns as long as we hit the .NET for Android code path which uses the robin_map cache. If something doesn't work, the runtime will
			// crash, thus we don't need any asserts here. The API is called twice, because the first call hits a slightly different code path than the subsequent ones.
			mono_config_is_server_mode ();
			mono_config_is_server_mode ();
		}
	}
}
