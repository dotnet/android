//
// AmCommand.cs
//
// Author:
//       Sandy Armstrong <sandy@xamarin.com>
//
// Copyright (c) 2014 Xamarin Inc.
//
using System;
using System.Collections.Generic;
using System.Linq;

using Mono.AndroidTools.Util;
using System.Net;
using System.Text;

namespace Mono.AndroidTools
{
	/// <summary>
	/// Represents an adb shell command.
	/// </summary>
	public abstract class AdbCommand
	{
		protected AdbCommand ()
		{
		}

		protected AdbCommand (AdbCommand copyCommand) : this ()
		{
		}

		/// <summary>
		/// Appends the arguments to the given argument builder
		/// </summary>
		protected abstract void AppendTo (ProcessArgumentBuilder pb);

		/// <summary>
		/// Returns the command as a string
		/// </summary>
		public override string ToString ()
		{
			var pb = new ProcessArgumentBuilder ();
			AppendTo (pb);
			return pb.ToString ();
		}
	}

	/// <summary>
	/// Represents a component name that can be specified as part of an extra in an IntentCommand
	/// </summary>
	public sealed class ExtraComponentName
	{
		public ExtraComponentName(string component)
		{
			this.Component = component;
		}

		public string Component { get; private set; }

		public override string ToString()
		{
			return this.Component;
		}
	}

	/// <summary>
	/// Represents a float value that can be specified as part of an extra in an IntentCommand
	/// </summary>
	/// <remarks>
	/// This keeps the original text as entered by the user so that we do not loose information
	/// when converting from string to float and back again
	/// </remarks>
	public sealed class ExtraFloat
	{
		public ExtraFloat(string val)
		{
			this.StringValue = val;
			this.FloatValue = float.Parse(val);
		}

		public string StringValue { get; private set; }

		public float FloatValue { get; private set; }

		public override string ToString()
		{
			return this.StringValue;
		}
	}

	/// <summary>
	/// Represents a data uri that can be specified as part of an extra in an IntentCommand
	/// </summary>
	public sealed class ExtraDataUri
	{
		public ExtraDataUri(string uri)
		{
			this.Uri = uri;
		}

		public string Uri { get; set; }

		public override string ToString()
		{
			return this.Uri;
		}
	}

	/// <summary>
	/// Represents an intent flag that can be specified in an IntentCommand
	/// </summary>
	[Flags]
	public enum IntentFlag
	{
		None = 0,
		GrantReadUriPermission = 1,
		GrantWriteUriPermission = 2,
		DebugLogResolution = 4,
		ExcludeStoppedPackages = 8,
		IncludeStoppedPackages = 16,
		ActivityBroughtToFront = 32,
		ActivityClearTop = 64,
		ActivityClearWhenTaskReset = 128,
		ActivityExcludeFromRecents = 256,
		ActivityLaunchedFromHistory = 512,
		ActvityMultipleTask = 1024,
		ActivityNoAnimation = 2048,
		ActivityNoHistory = 4096,
		ActivityNoUserAction = 8192,
		ActivityPreviousIsTop = 16384,
		ActivityReorderToFront = 32768,
		ActivityResetTaskIfNeeded = 65536,
		ActivitySingleTop = 131072,
		ActivityClearTask = 262144,
		ActivityTaskOnHome = 524288,
		ReceiverRegisteredOnly = 1048576,
		ReceiverReplacePending = 2097152
	}

	//<INTENT> specifications include these flags and arguments:
	//	[-a <ACTION>] [-d <DATA_URI>] [-t <MIME_TYPE>]
	//	[-c <CATEGORY> [-c <CATEGORY>] ...]
	//	[-e|--es <EXTRA_KEY> <EXTRA_STRING_VALUE> ...]
	//	[--esn <EXTRA_KEY> ...]
	//	[--ez <EXTRA_KEY> <EXTRA_BOOLEAN_VALUE> ...]
	//	[--ei <EXTRA_KEY> <EXTRA_INT_VALUE> ...]
	//	[--el <EXTRA_KEY> <EXTRA_LONG_VALUE> ...]
	//	[--ef <EXTRA_KEY> <EXTRA_FLOAT_VALUE> ...]
	//	[--eu <EXTRA_KEY> <EXTRA_URI_VALUE> ...]
	//	[--ecn <EXTRA_KEY> <EXTRA_COMPONENT_NAME_VALUE>]
	//	[--eia <EXTRA_KEY> <EXTRA_INT_VALUE>[,<EXTRA_INT_VALUE...]]
	//	[--ela <EXTRA_KEY> <EXTRA_LONG_VALUE>[,<EXTRA_LONG_VALUE...]]
	//	[--efa <EXTRA_KEY> <EXTRA_FLOAT_VALUE>[,<EXTRA_FLOAT_VALUE...]]
	//	[-n <COMPONENT>] [-f <FLAGS>]
	//	[--grant-read-uri-permission] [--grant-write-uri-permission]
	//	[--debug-log-resolution] [--exclude-stopped-packages]
	//	[--include-stopped-packages]
	//	[--activity-brought-to-front] [--activity-clear-top]
	//	[--activity-clear-when-task-reset] [--activity-exclude-from-recents]
	//	[--activity-launched-from-history] [--activity-multiple-task]
	//	[--activity-no-animation] [--activity-no-history]
	//	[--activity-no-user-action] [--activity-previous-is-top]
	//	[--activity-reorder-to-front] [--activity-reset-task-if-needed]
	//	[--activity-single-top] [--activity-clear-task]
	//	[--activity-task-on-home]
	//	[--receiver-registered-only] [--receiver-replace-pending]
	//	[--selector]
	//	[<URI> | <PACKAGE> | <COMPONENT>]
	public abstract class AmIntentCommand : AdbCommand
	{
		// [-a <ACTION>]
		public string Action { get; set; }

		// [-d <DATA_URI>]
		public string DataUri { get; set; }

		// [-c <CATEGORY> [-c <CATEGORY>] ...]
		public ICollection<string> Categories { get; set; }

		// [-t <MIME_TYPE>]
		public string MimeType { get; set; }

		// [-e|--es <EXTRA_KEY> <EXTRA_STRING_VALUE> ...]
		// [--esn <EXTRA_KEY> ...]
		// [--ez <EXTRA_KEY> <EXTRA_BOOLEAN_VALUE> ...]
		// [--ei <EXTRA_KEY> <EXTRA_INT_VALUE> ...]
		// [--el <EXTRA_KEY> <EXTRA_LONG_VALUE> ...]
		// [--ef <EXTRA_KEY> <EXTRA_FLOAT_VALUE> ...]
		// [--eu <EXTRA_KEY> <EXTRA_URI_VALUE> ...]
		// [--ecn <EXTRA_KEY> <EXTRA_COMPONENT_NAME_VALUE>]
		// [--eia <EXTRA_KEY> <EXTRA_INT_VALUE>[,<EXTRA_INT_VALUE...]]
		// [--ela <EXTRA_KEY> <EXTRA_LONG_VALUE>[,<EXTRA_LONG_VALUE...]]
		// [--efa <EXTRA_KEY> <EXTRA_FLOAT_VALUE>[,<EXTRA_FLOAT_VALUE...]]
		public IDictionary<string, object> Extras { get; set; }

		/// <summary>
		/// Specify the component name with package name prefix to create an explicit intent, such as "com.example.app/.ExampleActivity"
		/// </summary>
		// [-n <COMPONENT>]
		public string Component { get; set; }

		// [-p <PACKAGE>]
		/// <summary>
		/// Gets or sets the name of the package. This is not emitted as an option even though it's supported.
		/// </summary>
		/// <remarks>
		/// The problem here at the moment is that we use the PackageName to support FastDev and this conflicts with
		/// with having the PackageName argument. The other issue is that although `am start` reports this as a valid
		/// argument, we don't have any documentation on what it does - http://developer.android.com/tools/help/shell.html#IntentSpec
		/// contains no information about it.
		/// </remarks>
		public string PackageName { get; set; }

		// [<URI> | <PACKAGE> | <COMPONENT>]
		// passing a package name here will automatically add Starting: Intent { act=android.intent.action.MAIN cat=[android.intent.category.LAUNCHER] cmp=package/name }
		// action and category to the command, passing just -n will not add the action or categories
		public string Intent { get; set; }

		//	[--grant-read-uri-permission] [--grant-write-uri-permission]
		//	[--debug-log-resolution] [--exclude-stopped-packages]
		//	[--include-stopped-packages]
		//	[--activity-brought-to-front] [--activity-clear-top]
		//	[--activity-clear-when-task-reset] [--activity-exclude-from-recents]
		//	[--activity-launched-from-history] [--activity-multiple-task]
		//	[--activity-no-animation] [--activity-no-history]
		//	[--activity-no-user-action] [--activity-previous-is-top]
		//	[--activity-reorder-to-front] [--activity-reset-task-if-needed]
		//	[--activity-single-top] [--activity-clear-task]
		//	[--activity-task-on-home]
		//	[--receiver-registered-only] [--receiver-replace-pending]
		public IntentFlag Flags { get; set; }

		// --selector
		public bool Selector { get; set; }

		protected AmIntentCommand ()
		{
		}

		protected AmIntentCommand (AmIntentCommand copyCommand) : base (copyCommand)
		{
			Action = copyCommand.Action;
			Categories = copyCommand.Categories == null ? null : copyCommand.Categories.ToArray ();
			Extras = copyCommand.Extras == null ? null : new Dictionary<string, object> (copyCommand.Extras);
			Component = copyCommand.Component;
			Intent = copyCommand.Intent;

			this.DataUri = copyCommand.DataUri;
			this.Flags = copyCommand.Flags;
			this.MimeType = copyCommand.MimeType;
			this.PackageName = copyCommand.PackageName;
			this.Selector = copyCommand.Selector;
		}

		/// <summary>
		/// Appends the arguments to the given argument builder
		/// </summary>
		protected override void AppendTo (ProcessArgumentBuilder pb)
		{
			if (!String.IsNullOrEmpty (Action)) {
				pb.Add ("-a");
				pb.AddQuoted (Action);
			}

			if (Categories != null) {
				foreach (var category in Categories) {
					pb.Add ("-c");
					pb.AddQuoted (category);
				}
			}

			if (!String.IsNullOrEmpty(DataUri))
			{
				pb.Add("-d");
				pb.AddQuoted(DataUri);
			}

			if (!String.IsNullOrEmpty(MimeType))
			{
				pb.Add("-t");
				pb.AddQuoted(MimeType);
			}

			if (Extras != null) {
				foreach (var e in Extras) {
					if (e.Value == null) {
						pb.Add ("--esn");
						pb.AddQuoted (e.Key);
					} else if (e.Value is string) {
						var val = (string)e.Value;
						pb.Add (string.IsNullOrEmpty (val) ? "--esn" : "-e");
						pb.AddQuoted (e.Key);
						if (!string.IsNullOrEmpty (val))
							pb.AddQuoted (val);
					} else if (e.Value is ExtraComponentName) {
						var val = (ExtraComponentName)e.Value;
						pb.Add ("--ecn");
						pb.AddQuoted (e.Key);
						pb.AddQuoted (val.Component);
					} else if (e.Value is ExtraDataUri) {
						var val = (ExtraDataUri)e.Value;
						pb.Add ("--eu");
						pb.AddQuoted (e.Key);
						pb.AddQuoted (val.Uri);
					} else if (e.Value is double) {
						var val = (double)e.Value;
						pb.Add ("--ef");
						pb.AddQuoted (e.Key);
						pb.AddQuotedFormat ("{0}", val);
					} else if (e.Value is ExtraFloat) {
						var val = (ExtraFloat)e.Value;
						pb.Add ("--ef");
						pb.AddQuoted (e.Key);
						pb.AddQuoted (val.StringValue);
					} else if (e.Value is long) {
						var val = (long)e.Value;
						pb.Add ("--el");
						pb.AddQuoted (e.Key);
						pb.AddQuotedFormat ("{0}", val);
					} else if (e.Value is int) {
						var val = (int)e.Value;
						pb.Add ("--ei");
						pb.AddQuoted (e.Key);
						pb.AddQuotedFormat ("{0}", val);
					} else if (e.Value is float) {
						var val = (float)e.Value;
						pb.Add ("--ef");
						pb.AddQuoted (e.Key);
						pb.AddQuotedFormat ("{0}", val);
					} else if (e.Value is bool) {
						var val = (bool)e.Value;
						pb.Add ("--ez");
						pb.AddQuoted (e.Key);
						pb.AddQuoted (val ? "true" : "false");
					} else if (e.Value is int[]) {
						var val = (int[])e.Value;
						AppendIntArray(pb, e.Key, val);
					} else if (e.Value is long[]) {
						var val = (long[])e.Value;
						AppendLongArray(pb, e.Key, val);
					} else if (e.Value is float[]) {
						var val = (float[])e.Value;
						AppendFloatArray(pb, e.Key, val);
					} else if (e.Value is ExtraFloat[]) {
						var val = (ExtraFloat[])e.Value;
						AppendFloatArray(pb, e.Key, val);
					} else {
						throw new ArgumentException (
							String.Format ("Extra activity arguments of type {0} are not yet supported", e.Value.GetType ()),
							"Extras");
					}
				}
			}

			if (!String.IsNullOrEmpty (Component)) {
				pb.Add ("-n");
				pb.AddQuoted (Component);
			}

			// DON'T output this, see comment for the property
			//if (!String.IsNullOrEmpty(PackageName))
			//{
			//	pb.Add("-p");
			//	pb.AddQuoted(PackageName);
			//}

			AppendFlagsTo(pb);

			if (Selector)
			{
				pb.Add("--selector");
			}

			// This goes last
			if (!String.IsNullOrEmpty (Intent))
				pb.AddQuoted (Intent);
		}

		void AppendIntArray(ProcessArgumentBuilder pb, string key, int[] args)
		{
			pb.Add("--eia");
			pb.AddQuoted(key);

			var arg = new StringBuilder();

			for (int i = 0; i < args.Length; i++)
			{
				if (i > 0)
					arg.Append(", ");
				arg.Append(args[i].ToString());
			}

			pb.AddQuoted(arg.ToString());
		}

		void AppendLongArray(ProcessArgumentBuilder pb, string key, long[] args)
		{
			pb.Add("--ela");
			pb.AddQuoted(key);

			var arg = new StringBuilder();

			for (int i = 0; i < args.Length; i++)
			{
				if (i > 0)
					arg.Append(", ");
				arg.Append(args[i].ToString());
			}

			pb.AddQuoted(arg.ToString());
		}

		void AppendFloatArray(ProcessArgumentBuilder pb, string key, float[] args)
		{
			pb.Add("--efa");
			pb.AddQuoted(key);

			var arg = new StringBuilder();

			for (int i = 0; i < args.Length; i++)
			{
				if (i > 0)
					arg.Append(", ");
				arg.Append(args[i].ToString());
			}

			pb.AddQuoted(arg.ToString());
		}

		void AppendFloatArray(ProcessArgumentBuilder pb, string key, ExtraFloat[] args)
		{
			pb.Add("--efa");
			pb.AddQuoted(key);

			var arg = new StringBuilder();

			for (int i = 0; i < args.Length; i++)
			{
				if (i > 0)
					arg.Append(", ");
				arg.Append(args[i].StringValue);
			}

			pb.AddQuoted(arg.ToString());
		}

		void AppendFlagsTo(ProcessArgumentBuilder pb)
		{
			if (this.Flags.HasFlag(IntentFlag.GrantReadUriPermission))
				pb.Add("--grant-read-uri-permission");

			if (this.Flags.HasFlag(IntentFlag.GrantWriteUriPermission))
				pb.Add("--grant-write-uri-permission");

			if (this.Flags.HasFlag(IntentFlag.DebugLogResolution))
				pb.Add("--debug-log-resolution");

			if (this.Flags.HasFlag(IntentFlag.ExcludeStoppedPackages))
				pb.Add("--exclude-stopped-packages");

			if (this.Flags.HasFlag(IntentFlag.IncludeStoppedPackages))
				pb.Add("--include-stopped-packages");

			if (this.Flags.HasFlag(IntentFlag.ActivityBroughtToFront))
				pb.Add("--activity-brought-to-front");

			if (this.Flags.HasFlag(IntentFlag.ActivityClearTop))
				pb.Add("--activity-clear-top");

			if (this.Flags.HasFlag(IntentFlag.ActivityClearWhenTaskReset))
				pb.Add("--activity-clear-when-task-reset");

			if (this.Flags.HasFlag(IntentFlag.ActivityExcludeFromRecents))
				pb.Add("--activity-exclude-from-recents");

			if (this.Flags.HasFlag(IntentFlag.ActivityLaunchedFromHistory))
				pb.Add("--activity-launched-from-history");

			if (this.Flags.HasFlag(IntentFlag.ActvityMultipleTask))
				pb.Add("--activity-multiple-task");

			if (this.Flags.HasFlag(IntentFlag.ActivityNoAnimation))
				pb.Add("--activity-no-animation");

			if (this.Flags.HasFlag(IntentFlag.ActivityNoHistory))
				pb.Add("--activity-no-history");

			if (this.Flags.HasFlag(IntentFlag.ActivityNoUserAction))
				pb.Add("--activity-no-user-action");

			if (this.Flags.HasFlag(IntentFlag.ActivityPreviousIsTop))
				pb.Add("--activity-previous-is-top");

			if (this.Flags.HasFlag(IntentFlag.ActivityReorderToFront))
				pb.Add("--activity-reorder-to-front");

			if (this.Flags.HasFlag(IntentFlag.ActivityResetTaskIfNeeded))
				pb.Add("--activity-reset-task-if-needed");

			if (this.Flags.HasFlag(IntentFlag.ActivitySingleTop))
				pb.Add("--activity-single-top");

			if (this.Flags.HasFlag(IntentFlag.ActivityClearTask))
				pb.Add("--activity-clear-task");

			if (this.Flags.HasFlag(IntentFlag.ActivityTaskOnHome))
				pb.Add("--activity-task-on-home");

			if (this.Flags.HasFlag(IntentFlag.ReceiverRegisteredOnly))
				pb.Add("--receiver-registered-only");

			if (this.Flags.HasFlag(IntentFlag.ReceiverReplacePending))
				pb.Add("--receiver-replace-pending");
		}
	}

	// usage: am start [-D] [-W] [-P <FILE>] [--start-profiler <FILE>]
	//	[--R COUNT] [-S] [--opengl-trace]
	//	[--user <USER_ID> | current] <INTENT>
	//
	// am start: start an Activity.  Options are:
	//	-D: enable debugging
	//	-W: wait for launch to complete
	//	--start-profiler <FILE>: start profiler and send results to <FILE>
	//	-P <FILE>: like above, but profiling stops when app goes idle
	//	-R: repeat the activity launch <COUNT> times.  Prior to each repeat,
	//	    the top activity will be finished.
	//	-S: force stop the target app before starting the activity
	//	--opengl-trace: enable tracing of OpenGL functions
	//	--user <USER_ID> | current: Specify which user to run as; if not
	//	    specified then run as the current user.
	public class AmStartCommand : AmIntentCommand
	{
		// -D: enable debugging
		public bool EnableDebugging { get; set; }

		// -W: wait for launch to complete
		public bool Wait { get; set; }

		// --start-profiler <FILE>: start profiler and send results to <FILE>
		public string ProfilerOutputPath { get; set; }

		// -R: repeat the activity launch <COUNT> times.  Prior to each repeat,
		//     the top activity will be finished.
		public int Repeat { get; set; }

		// -S: force stop the target app before starting the activity
		public bool ForceStop { get; set; }

		// --opengl-trace: enable tracing of OpenGL functions
		public bool EnableOpenGLTracing { get; set; }

		// --user <USER_ID> | current: Specify which user to run as; if not
		//     specified then run as the current user.
		public string User { get; set; }

		public string Activity { get; set; }

		public AmStartCommand ()
		{
		}

		public AmStartCommand (string package, string activity) : this ()
		{
			PackageName = package;
			Activity = activity;
			Component = String.Format ("{0}/{1}", package, activity);
		}

		public AmStartCommand (AmStartCommand copyCommand) : base (copyCommand)
		{
			EnableDebugging = copyCommand.EnableDebugging;
			Wait = copyCommand.Wait;
			ProfilerOutputPath = copyCommand.ProfilerOutputPath;
			Repeat = copyCommand.Repeat;
			ForceStop = copyCommand.ForceStop;
			EnableOpenGLTracing = copyCommand.EnableOpenGLTracing;
			User = copyCommand.User;
			Activity = copyCommand.Activity;
		}

		protected override void AppendTo (ProcessArgumentBuilder pb)
		{
			pb.Add ("am", "start");

			if (EnableDebugging)
				pb.Add ("-D");
			if (Wait)
				pb.Add ("-W");
			if (!String.IsNullOrEmpty (ProfilerOutputPath)) {
				pb.Add ("--start-profiler");
				pb.AddQuoted (ProfilerOutputPath);
			}
			if (Repeat > 0)
				pb.Add ("-R", Repeat.ToString ());
			if (ForceStop)
				pb.Add ("-S");
			if (EnableOpenGLTracing)
				pb.Add ("--opengl-trace");
			if (!String.IsNullOrEmpty (User)) {
				pb.Add ("--user");
				pb.AddQuoted (User);
			}

			// <INTENT>
			base.AppendTo (pb);
		}
	}

	// usage: am startservice [--user <USER_ID> | current] <INTENT>
	public class AmStartServiceCommand : AmIntentCommand
	{
		public AmStartServiceCommand()
		{
		}

		public AmStartServiceCommand(string packageName, string serviceName)
		{
			this.PackageName = packageName;
			this.Component = string.Format("{0}/{1}", packageName, serviceName);
		}

		public AmStartServiceCommand(AmStartCommand copyCommand) : base(copyCommand)
		{
			User = copyCommand.User;
		}

		// --user <USER_ID> | current: Specify which user to run as; if not
		//     specified then run as the current user.
		public string User { get; set; }

		protected override void AppendTo(ProcessArgumentBuilder pb)
		{
			pb.Add("am", "startservice");
			if (!String.IsNullOrEmpty(User))
			{
				pb.Add("--user");
				pb.AddQuoted(User);
			}

			// <INTENT>
			base.AppendTo(pb);
		}
	}

	// usage: am broadcast [--user <USER_ID> | all | current] <INTENT>
	//
	// am broadcast: send a broadcast Intent.  Options are:
	//	--user <USER_ID> | all | current: Specify which user to send to; if not
	//		specified then send to all users.
	//	--receiver-permission <PERMISSION>: Require receiver to hold permission.
	public class AmBroadcastCommand : AmIntentCommand
	{
		// --user <USER_ID> | current: Specify which user to run as; if not
		//     specified then run as the current user.
		public string User { get; set; }

		public AmBroadcastCommand ()
		{
		}

		public AmBroadcastCommand (AmBroadcastCommand copyCommand) : base (copyCommand)
		{
			User = copyCommand.User;
		}

		protected override void AppendTo (ProcessArgumentBuilder pb)
		{
			pb.Add ("am", "broadcast");

			if (!String.IsNullOrEmpty (User)) {
				pb.Add ("--user");
				pb.AddQuoted (User);
			}

			// <INTENT>
			base.AppendTo (pb);
		}
	}

	/// <summary>
	///
	/// </summary>
	public class PmListPackagesCommand : AdbCommand
	{
		public bool RequireVersions { get; set; }

		public int ApiLevel { get; set; }

		// --user <USER_ID> | current: Specify which user to run as; if not
		//     specified then run as the current user.
		public string User { get; set; }

		public PmListPackagesCommand ()
		{

		}

		public PmListPackagesCommand (PmListPackagesCommand copyCommand) : base (copyCommand)
		{
			RequireVersions = copyCommand.RequireVersions;
			ApiLevel = copyCommand.ApiLevel;
			User = copyCommand.User;
		}

		protected override void AppendTo (ProcessArgumentBuilder pb)
		{
			pb.Add ("pm", "list", "packages");

			pb.Add ("-f");
			//NOTE: --show-versioncode is only available since API 26
			if (RequireVersions && ApiLevel > 26)
				pb.Add ("--show-versioncode");

			if (!String.IsNullOrEmpty (User)) {
				pb.Add ("--user");
				pb.AddQuoted (User);
			} else {
				pb.Add ("--user");
				pb.Add ("0");
			}
		}
	}

	/// <summary>
	///
	/// </summary>
	public class PmUninstallCommand : AdbCommand
	{
		public string PackageName { get; set; }

		public bool PreserveData { get; set; }

		// --user <USER_ID> | current: Specify which user to run as; if not
		//     specified then run as the current user.
		public string User { get; set; }

		public PmUninstallCommand ()
		{

		}

		public PmUninstallCommand (PmUninstallCommand copyCommand) : base (copyCommand)
		{
			PackageName = copyCommand.PackageName;
			PreserveData = copyCommand.PreserveData;
			User = copyCommand.User;
		}

		protected override void AppendTo (ProcessArgumentBuilder pb)
		{
			pb.Add ("pm", "uninstall");

			if (PreserveData)
				pb.Add ("-k");

			if (!String.IsNullOrEmpty (User)) {
				pb.Add ("--user");
				pb.AddQuoted (User);
			}
			pb.Add (PackageName);
		}
	}

	/// <summary>
	///
	/// </summary>
	public class PmInstallCommand : AdbCommand
	{
		/// <summary>
		///
		/// </summary>
		public string RemoteApkFile { get; set; }

		// --user <USER_ID> | current: Specify which user to run as; if not
		//     specified then run as the current user.
		public string User { get; set; }

		/// <summary>
		///
		/// </summary>
		public AdbInstallFlags Flags { get; set; }

		public PmInstallCommand ()
		{
		}

		public PmInstallCommand (PmInstallCommand copyCommand) : base (copyCommand)
		{
			RemoteApkFile = copyCommand.RemoteApkFile;
			Flags = copyCommand.Flags;
			User = copyCommand.User;
		}

		protected override void AppendTo (ProcessArgumentBuilder pb)
		{
			pb.Add ("pm", "install");

			if ((Flags & AdbInstallFlags.Reinstall) != 0)
				pb.Add ("-r");
			if ((Flags & AdbInstallFlags.External) != 0)
				pb.Add ("-s");
			if ((Flags & AdbInstallFlags.AllowDowngrade) != 0)
				pb.Add ("-d");
			if ((Flags & AdbInstallFlags.TestOnly) != 0)
				pb.Add ("-t");

			if (!String.IsNullOrEmpty (User)) {
				pb.Add ("--user");
				pb.AddQuoted (User);
			}
			pb.AddQuoted (RemoteApkFile);
		}
	}
}

