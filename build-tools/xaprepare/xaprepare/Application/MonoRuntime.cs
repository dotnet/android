using System;

namespace Xamarin.Android.Prepare
{
	abstract class MonoRuntime : Runtime
	{
		string nativeLibraryDirPrefix;

		public bool   CanStripNativeLibrary         { get; set; } = true;
		public string CrossMonoName                 { get; set; }
		public string ExePrefix                     { get; set; }
		public bool   IsCrossRuntime                { get; set; }
		public string NativeLibraryExtension        { get; set; }

		/// <summary>
		///   Optional directory prefix for native library source. This should be a path relative to runtime's library
		///   output dir and it exists because MinGW builds will put the runtime .dll in the bin directory instead of in
		///   the lib one.
		/// </summary>
		public string NativeLibraryDirPrefix        {
			get => nativeLibraryDirPrefix ?? String.Empty;
			set => nativeLibraryDirPrefix = value;
		}

		public string OutputAotProfilerFilename     { get; set; }
		public string OutputMonoBtlsFilename        { get; set; }
		public string OutputMonoPosixHelperFilename { get; set; }
		public string OutputProfilerFilename        { get; set; }
		public string OutputRuntimeFilename         { get; set; } = Configurables.Defaults.MonoRuntimeOutputFileName;
		public string Strip                         { get; set; }
		public string StripFlags                    { get; set; }

		protected MonoRuntime (string name, Func<Context, bool> enabledCheck)
			: base (name, enabledCheck)
		{}
	}
}
