using System;

namespace Xamarin.Android.Prepare
{
	class RuntimeFile
	{
		/// <summary>
		///   Path of the source file (one in the Mono SDK output location). Either relative to Mono SDK output path or
		///   absolute.
		/// </summary>
		public Func<Runtime, string> Source      { get; }

		/// <summary>
		///   Destination of the file. Either relative to <see cref="Configurables.Paths.InstallMSBuildDir" /> or absolute.
		/// </summary>
		public Func<Runtime, string> Destination { get; }

		/// <summary>
		///   An optional check on whether or not the file should be installed for the particular runtime.
		/// </summary>
		public Func<Runtime, bool>   ShouldSkip  { get; }

		/// <summary>
		///   Whether or not to strip the binary of debugging symbols after installation.
		/// </summary>
		public bool                  Strip       { get; } = true;

		/// <summary>
		///   Type of the file. It's needed in order to determine what tools, if any, we can run on the file once it is
		///   installed, if any.
		/// </summary>
		public RuntimeFileType       Type        { get; } = RuntimeFileType.Other;

		/// <summary>
		///   If set to <c>true</c> then the file will be copied only once, not per runtime
		/// </summary>
		public bool Shared                       { get; }

		public bool AlreadyCopied                { get; set; }

		public RuntimeFile (Func<Runtime, string> sourceCreator, Func<Runtime, string> destinationCreator, Func<Runtime, bool> shouldSkip = null, bool strip = true, RuntimeFileType type = RuntimeFileType.Other, bool shared = false)
		{
			if (sourceCreator == null)
				throw new ArgumentNullException (nameof (sourceCreator));
			if (destinationCreator == null)
				throw new ArgumentNullException (nameof (destinationCreator));

			Source = sourceCreator;
			Destination = destinationCreator;
			ShouldSkip = shouldSkip;
			Strip = strip;
			Type = type;
			Shared = shared;
		}
	}
}
