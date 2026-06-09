//
//  Authors:
//    Marek Habersack <marhab@microsoft.com>
//
//  Copyright (c) 2017, Microsoft, Inc
//
//  All rights reserved.
//
using System;
using System.Collections.Generic;

namespace Xamarin.Installer.AndroidSDK.Common
{
	/// <summary>
	/// Interface fully describing a single Android component
	/// </summary>
	public interface IAndroidComponent
	{
		/// <summary>
		/// Occurs when status changes. <seealso cref="t:AndroidComponentStatus"/>
		/// </summary>
		event EventHandler<AndroidComponentStatusChangeEventArgs> StatusChanged;

		/// <summary>
		/// <c>true</c> if this component is required for an instance of Android SDK to work properly
		/// </summary>
		bool IsEssential { get; }

		/// <summary>
		/// Gets a value indicating whether this component ignores installation failures.
		/// </summary>
		/// <value><c>true</c> if installation failure is ignored; otherwise, <c>false</c>.</value>
		bool IgnoreFailure { get; }

		/// <summary>
		/// Gets or sets a value indicating whether the component is present in the given Android SDK instance on disk.
		/// </summary>
		/// <value><c>true</c> if present; otherwise, <c>false</c>.</value>
		bool Present { get; }

		/// <summary>
		/// Gets or sets a value indicating whether this component has to be
		/// updated. It will be set to <c>true</c> also of <see cref="Present"/> is <c>false</c>
		/// </summary>
		/// <value><c>true</c> if needs update; otherwise, <c>false</c>.</value>
		bool NeedsUpdate { get; }

		/// <summary>
		/// Type of the component. This is a map from the textual type name in the repository manifest into an
		/// enumeration for easier searching and component identification
		/// </summary>
		/// <value>The type of the component.</value>
		AndroidComponentType ComponentType { get; }

		/// <summary>
		/// Gets the unique identifier of this particular instance. The ID is not persistent, it is valid only within
		/// a single installer session and used to retrieve download information from the hosting application.
		/// <seealso cref="F:Xamarin.Installer.Common.IHelpers.GetPathForDownloadId(Guid id)"/>
		/// </summary>
		/// <value>The unique identifier.</value>
		Guid UniqueID { get; }

		/// <summary>
		/// Gets package path as found in the SDK repository manifest. Note that paths aren't necessarily unique, there
		/// may exist several packages with the same path but different version.
		/// </summary>
		/// <value>The manifest path.</value>
		string Path { get; }

		/// <summary>
		/// Gets the file system path where the package is (or will be) found, relative to the given Android SDK root
		/// directory. This is, essentially, value of the <see cref="Path"/> property converted to the proper path
		/// representation for the current operating system.
		/// </summary>
		/// <value>The file system path.</value>
		string FileSystemPath { get;  }

		/// <summary>
		/// Gets a value indicating whether this <see cref="T:Xamarin.Installer.AndroidSDK.Common.IAndroidComponent"/> is obsolete.
		/// </summary>
		/// <value><c>true</c> if obsolete; otherwise, <c>false</c>.</value>
		bool Obsolete { get; }

		/// <summary>
		/// Type-specific information about this component
		/// </summary>
		/// <value>Component type-specific information</value>
		AndroidComponentInfo Info { get; }

		/// <summary>
		/// Gets the available component revision. <seealso cref="InstalledRevision"/>
		/// </summary>
		/// <value>Component revision.</value>
		AndroidRevision Revision { get; }

		/// <summary>
		/// Gets the revision of the component as found in the given Android SDK root, if any. <seealso cref="Revision"/>
		/// </summary>
		/// <value>Revision of the installed component</value>
		AndroidRevision InstalledRevision { get; }

		/// <summary>
		/// Gets the display name of the component
		/// </summary>
		/// <value>Component display name</value>
		string DisplayName { get; }

		/// <summary>
		/// Gets the detailed description of the component
		/// </summary>
		/// <value>Detailed description of the component</value>
		string DetailedDescription { get; }

		/// <summary>
		/// Gets the component dependencies, if any.
		/// </summary>
		/// <value>Component dependencies</value>
		IList<Dependency> Dependencies { get; }

		/// <summary>
		/// Gets the component channel.
		/// </summary>
		/// <value>Component channel</value>
		Channel Channel { get; }

		/// <summary>
		/// Gets the component archives. Only archives valid for the current operating system will be found here.
		/// </summary>
		/// <value>Component archives</value>
		IList<Archive> Archives { get; }

		/// <summary>
		/// Gets the component license.
		/// </summary>
		/// <value>Component license</value>
		License License { get; }

		/// <summary>
		/// Gets the URL of the manifest from which the component description was loaded.
		/// </summary>
		/// <value>The manifest URL.</value>
		Uri ManifestURL { get; }

		/// <summary>
		/// Hosting application can set this to <c>true</c> if it wants the installer to ignore value of the <see cref="NeedsUpdate"/>
		/// property and force (re)installation of the component. This property must be set before the component is passed to the
		/// <see cref="AndroidSDKInstaller.Install"/> method.
		/// </summary>
		/// <value><c>true</c> to force installation; otherwise, <c>false</c>.</value>
		bool ForceInstallation { get; set; }

		/// <summary>
		/// ID of the license the component uses
		/// </summary>
		string LicenseID { get; set; }

		/// <summary>
		/// Compares properties related to Path and Revision,
		/// and returns True, if they are same for the matching components.
		/// </summary>
		/// <param name="other">Component to compare with</param>
		/// <returns></returns>
		bool MatchesTo (IAndroidComponent other);
	}
}
