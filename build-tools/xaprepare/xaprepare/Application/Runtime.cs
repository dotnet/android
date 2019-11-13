using System;

namespace Xamarin.Android.Prepare
{
	abstract class Runtime : AppObject
	{
		Func<Context, bool> enabledCheck;

		/// <summary>
		///   Set to <c>true</c> if the current runtime is supported on the host OS.
		/// </summary>
		protected bool SupportedOnHostOS { get; set; } = true;

		protected Context Context => Context.Instance;
		public bool Enabled       => SupportedOnHostOS && enabledCheck (Context);
		public string ExeSuffix   { get; protected set; }

		/// <summary>
		///   Path relative to <see cref="Configurables.Paths.InstallMSBuildDir"/> where the runtime will be placed or
		///   <c>null</c> to install the runtime directly under <see cref="Configurables.Paths.InstallMSBuildDir"/>. In
		///   each case the runtime <see cref="Name"/> will be appended to create full path to the destination
		///   directory.
		/// </summary>
		public string InstallPath { get; protected set; }
		public string Name        { get; protected set; }

		/// <summary>
		///   Purely cosmetic thing - the kind of runtime (LLVM, JIT etc), for progress reporting.
		/// </summary>
		public abstract string Flavor { get; }

		/// <summary>
		///   Prefix to <see cref="Name"/> used by MonoSDKs to construct the runtime output directory.
		/// </summary>
		protected string MonoSdksPrefix { get; set; }

		/// <summary>
		///   Host runtimes need a prefix in order to match Mono SDKs output directory name for them. This property is
		///   defined in the base class to make code using the runtime definitions simpler.
		/// </summary>
		public string PrefixedName => String.IsNullOrEmpty (MonoSdksPrefix) ? Name : $"{MonoSdksPrefix}{Name}";

		public Runtime (string name, Func<Context, bool> enabledCheck)
		{
			if (String.IsNullOrEmpty (name))
				throw new ArgumentException ("must not be null or empty", nameof (name));

			if (enabledCheck == null)
				throw new ArgumentNullException (nameof (enabledCheck));

			this.enabledCheck = enabledCheck;
			Name = name;
		}

		public abstract void Init (Context context);
	}
}
