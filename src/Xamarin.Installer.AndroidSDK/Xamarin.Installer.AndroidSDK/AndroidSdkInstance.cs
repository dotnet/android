using System;
using System.Collections.Generic;
using Xamarin.Installer.AndroidSDK.Common;

namespace Xamarin.Installer.AndroidSDK
{
	/// <summary>
	/// Describes a single Android SDK instance on the user's machine
	/// </summary>
	public sealed class AndroidSdkInstance
	{
		ulong? downloadSize;
		IList<IAndroidComponent> components;

		/// <summary>
		/// Occurs when component status changes. <seealso cref="t:AndroidComponentStatus"/>
		/// </summary>
		public event EventHandler<AndroidComponentStatusChangeEventArgs> ComponentStatusChanged;

		/// <summary>
		/// List of all the components that can be or are installed in this SDK instance. 
		/// The caller needs to determine whether or not they desire all of them to be actually installed.
		/// </summary>
		/// <value>List of all SDK components for this instance</value>
		public IList<IAndroidComponent> Components { 
			get { return components; }
			internal set {
				components = value;
				HookComponentEvents (components);
			}
		}

		/// <summary>
		/// A convenience property to store only components selected for installation as opposed to
		/// <see cref="Components"/> which has all the components loaded from the manifest. This
		/// property is used by the unified installer.
		/// </summary>
		/// <value>The components for installation.</value>
		public IList<IAndroidComponent> ComponentsForInstallation { get; set; }

		/// <summary>
		/// Indicates whether this instance corresponds to the default Android SDK location
		/// </summary>
		/// <value><c>true</c> if this instance corresponds to the default Android SDK location; otherwise, <c>false</c>.</value>
		public bool IsDefault { get; internal set; }

		/// <summary>
		/// Indicates whether this instance corresponds to the official Android SDK location. Official location is, in fact, found
		/// only on Windows when the .exe Android SDK installer was used.
		/// </summary>
		/// <value><c>true</c> if this instance is official; otherwise, <c>false</c>.</value>
		public bool IsOfficial { get; internal set; }

		/// <summary>
		/// Indicates whether this instance corresponds to a location specified by the user in the Unified Installer.
		/// </summary>
		/// <value><c>true</c> if this instance is user location; otherwise, <c>false</c>.</value>
		public bool IsUserLocation { get; set; }

		/// <summary>
		/// Path to this instance's directory on disk. Note that the directory might not exist yet.
		/// </summary>
		/// <value>Instance directory path.</value>
		public string Path { get; internal set; }

		/// <summary>
		/// Indicates whether this instance has been selected as the one to install to. Used by the unified installer.
		/// </summary>
		/// <value><c>true</c> if selected; otherwise, <c>false</c>.</value>
		public bool Selected { get; set; }

		/// <summary>
		/// Android SDK version in string (unparsed) form. It may include additional information in additon to the actual
		/// revision/version number.
		/// </summary>
		/// <value>The version string.</value>
		public string VersionString { get; internal set; }

		/// <summary>
		/// Parsed Android SDK version. This is parsed from <see cref="VersionString"/> and includes only the numeric part.
		/// </summary>
		/// <value>The version.</value>
		public AndroidRevision Version { get; internal set; }

		/// <summary>
		/// Gets a value indicating whether this <see cref="T:Xamarin.Installer.AndroidSDK.AndroidSdkInstance"/> needs any updates.
		/// </summary>
		/// <value><c>true</c> if needs update; otherwise, <c>false</c>.</value>
		public bool NeedsUpdate => Components != null && Components.AnyOutdated ();

		internal AndroidSdkInstance ()
		{}

		/// <summary>
		/// Computes the total download size of all components
		/// passed in the <paramref name="componentsToInstall"/> parameter. If <paramref name="refresh"/> is <c>false</c>
		/// (the default) a cached value will be returned for all calls following the first one. If <paramref name="componentsToInstall"/>
		/// is null or empty, all the outdated components in this instance will be included in calculation.
		/// </summary>
		/// <returns>The total download size.</returns>
		/// <param name="componentsToInstall">Components to install (optional)</param>
		/// <param name="refresh">Use cached value of the download size if <c>true</c>.</param>
		public ulong GetDownloadSize (IList<IAndroidComponent> componentsToInstall = null, bool refresh = false)
		{
			if (!refresh && downloadSize.HasValue)
				return downloadSize.Value;

			if (componentsToInstall == null || componentsToInstall.Count == 0)
				componentsToInstall = Components.AllOutdated ();

			downloadSize = 0;
			if (componentsToInstall.Count == 0)
				return 0;

			ulong ret = 0;
			foreach (IAndroidComponent component in componentsToInstall) {
				if (component == null)
					continue;
				foreach (Archive archive in component.Archives) {
					if (archive == null)
						continue;
					ret += archive.Size;
				}
			};

			downloadSize = ret;
			return ret;
		}

		void HookComponentEvents (IList <IAndroidComponent> components)
		{
			if (components == null || components.Count == 0)
				return;

			foreach (IAndroidComponent c in components) {
				c.StatusChanged -= OnStatusChanged;
				c.StatusChanged += OnStatusChanged;
			}
		}

		void OnStatusChanged (object sender, AndroidComponentStatusChangeEventArgs args)
		{
			if (ComponentStatusChanged == null)
				return;

			ComponentStatusChanged (this, args);
		}
	}
}
