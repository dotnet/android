using System;

namespace Xamarin.Android.Prepare
{
	abstract class MonoRuntime : Runtime
	{
		public bool   CanStripNativeLibrary         { get; set; } = true;
		public string CrossMonoName                 { get; set; } = String.Empty;
		public string ExePrefix                     { get; set; } = String.Empty;
		public bool   IsCrossRuntime                { get; set; }
		public string NativeLibraryExtension        { get; set; } = String.Empty;

		/// <summary>
		///   Optional directory prefix for native library source. This should be a path relative to runtime's library
		///   output dir and it exists because MinGW builds will put the runtime .dll in the bin directory instead of in
		///   the lib one.
		/// </summary>
		public string NativeLibraryDirPrefix        { get; set; } = String.Empty;
		public string OutputAotProfilerFilename     { get; set; } = String.Empty;
		public string OutputMonoBtlsFilename        { get; set; } = String.Empty;
		public string OutputMonoPosixHelperFilename { get; set; } = String.Empty;
		public string OutputProfilerFilename        { get; set; } = String.Empty;
		public string OutputRuntimeFilename         { get; set; } = Configurables.Defaults.MonoRuntimeOutputFileName;
		public string Strip                         { get; set; } = String.Empty;
		public string StripFlags                    { get; set; } = "--strip-debug";

		protected MonoRuntime (string name, Func<Context, bool> enabledCheck)
			: base (name, enabledCheck)
		{}
	}
}
