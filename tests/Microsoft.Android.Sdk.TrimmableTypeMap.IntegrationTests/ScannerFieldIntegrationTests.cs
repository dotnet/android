using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace Microsoft.Android.Sdk.TrimmableTypeMap.IntegrationTests;

public partial class ScannerComparisonTests
{
	static readonly Lazy<List<JavaPeerInfo>> MonoAndroidPeers = new (() => ScanPeers (AllAssemblyPaths));
	static readonly Lazy<List<JavaPeerInfo>> UserFixturePeers = new (() => {
		var paths = AllUserTypesAssemblyPaths
			?? throw new InvalidOperationException ("UserTypesFixture.dll not found.");
		return ScanPeers (paths);
	});

	[Fact]
	public void IsUnconditional_ComponentTypes ()
	{
		Assert.True (FindUserPeerByManagedName ("UserApp.MainActivity").IsUnconditional);
		Assert.True (FindUserPeerByManagedName ("UserApp.Services.MyBackgroundService").IsUnconditional);
		Assert.True (FindUserPeerByManagedName ("UserApp.Receivers.BootReceiver").IsUnconditional);
		Assert.True (FindUserPeerByManagedName ("UserApp.Providers.SettingsProvider").IsUnconditional);

		Assert.False (FindUserPeerByManagedName ("UserApp.PlainActivity").IsUnconditional);
		Assert.False (FindUserPeerByManagedName ("UserApp.Listeners.MyClickListener").IsUnconditional);

		Assert.True (FindUserPeerByManagedName ("UserApp.MyBackupAgent").IsUnconditional);
	}

	[Fact]
	public void IsUnconditional_MonoAndroid ()
	{
		var peers = ScanMonoAndroidPeers ();

		Assert.NotEmpty (peers);
		Assert.Equal (0, peers.Count (p => p.IsUnconditional));
		Assert.All (peers.Where (p => p.DoNotGenerateAcw), peer =>
			Assert.False (peer.IsUnconditional, $"{peer.ManagedTypeName} should not be unconditional."));
	}

	[Fact]
	public void InvokerTypeName_InterfacesAndAbstractTypes_MonoAndroid ()
	{
		var peers = ScanMonoAndroidPeers ();
		var managedNames = peers.Select (p => p.ManagedTypeName).ToHashSet (StringComparer.Ordinal);
		var interfaces = peers.Where (p => p.IsInterface).ToList ();

		Assert.NotEmpty (interfaces);
		Assert.All (interfaces, peer => {
			var invokerTypeName = peer.InvokerTypeName;
			Assert.False (string.IsNullOrEmpty (invokerTypeName), $"{peer.ManagedTypeName} should have an invoker.");
			Assert.Contains (invokerTypeName, managedNames);
		});

		var clickListener = interfaces.Single (p => p.JavaName == "android/view/View$OnClickListener");
		Assert.Equal ("Android.Views.View+IOnClickListenerInvoker", clickListener.InvokerTypeName);
		Assert.Contains ("Android.Views.View+IOnClickListenerInvoker", managedNames);

		var absListView = FindMonoAndroidPeerByManagedName ("Android.Widget.AbsListView");
		Assert.Equal ("Android.Widget.AbsListViewInvoker", absListView.InvokerTypeName);
		Assert.Contains ("Android.Widget.AbsListViewInvoker", managedNames);
	}

	[Fact]
	public void InvokerTypeName_UserTypes ()
	{
		Assert.Null (FindUserPeerByManagedName ("UserApp.MainActivity").InvokerTypeName);
		Assert.Null (FindUserPeerByManagedName ("UserApp.Listeners.MyClickListener").InvokerTypeName);

		Assert.Equal ("UserApp.Interfaces.IWidgetListenerInvoker",
			FindUserPeerByManagedName ("UserApp.Interfaces.IWidgetListener").InvokerTypeName);
		Assert.Equal ("UserApp.AbstractWidgets.AbstractWidgetInvoker",
			FindUserPeerByManagedName ("UserApp.AbstractWidgets.AbstractWidget").InvokerTypeName);
	}

	[Fact]
	public void CompatJniName_UserTypes ()
	{
		var userModel = FindUserPeerByManagedName ("UserApp.Models.UserModel");
		Assert.StartsWith ("crc64", userModel.JavaName);
		Assert.Equal ("userapp.models/UserModel", userModel.CompatJniName);

		var dataManager = FindUserPeerByManagedName ("UserApp.Models.DataManager");
		Assert.StartsWith ("crc64", dataManager.JavaName);
		Assert.Equal ("userapp.models/DataManager", dataManager.CompatJniName);
	}

	[Fact]
	public void ManagedMethodName_MarshalMethods ()
	{
		var peers = ScanMonoAndroidPeers ();
		var marshalMethods = peers.SelectMany (p => p.MarshalMethods).ToList ();

		Assert.NotEmpty (marshalMethods);
		Assert.All (marshalMethods, method => Assert.False (string.IsNullOrWhiteSpace (method.ManagedMethodName)));

		var activity = FindMonoAndroidPeerByJavaName ("android/app/Activity");
		Assert.Contains (activity.MarshalMethods, method =>
			method.ManagedMethodName == "OnCreate" && method.JniName == "onCreate");
	}

	static List<JavaPeerInfo> ScanMonoAndroidPeers () => MonoAndroidPeers.Value;

	static List<JavaPeerInfo> ScanUserFixturePeers () => UserFixturePeers.Value;

	static List<JavaPeerInfo> ScanPeers (string[] assemblyPaths)
	{
		var primaryAssemblyName = Path.GetFileNameWithoutExtension (assemblyPaths [0]);
		using var scanner = new JavaPeerScanner ();
		return scanner.Scan (assemblyPaths)
			.Where (p => p.AssemblyName == primaryAssemblyName)
			.ToList ();
	}

	static JavaPeerInfo FindMonoAndroidPeerByJavaName (string javaName)
		=> FindPeerByJavaName (ScanMonoAndroidPeers (), javaName);

	static JavaPeerInfo FindMonoAndroidPeerByManagedName (string managedTypeName)
		=> FindPeerByManagedName (ScanMonoAndroidPeers (), managedTypeName);

	static JavaPeerInfo FindUserPeerByManagedName (string managedTypeName)
		=> FindPeerByManagedName (ScanUserFixturePeers (), managedTypeName);

	static JavaPeerInfo FindPeerByJavaName (IEnumerable<JavaPeerInfo> peers, string javaName)
	{
		var peer = peers.FirstOrDefault (p => p.JavaName == javaName);
		return peer ?? throw new InvalidOperationException ($"Peer with Java name '{javaName}' not found.");
	}

	static JavaPeerInfo FindPeerByManagedName (IEnumerable<JavaPeerInfo> peers, string managedTypeName)
	{
		var peer = peers.FirstOrDefault (p => p.ManagedTypeName == managedTypeName);
		return peer ?? throw new InvalidOperationException ($"Peer with managed name '{managedTypeName}' not found.");
	}
}
