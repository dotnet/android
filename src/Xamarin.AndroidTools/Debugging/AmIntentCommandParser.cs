//
// AmIntentCommandParser.cs
//
// Author:
//       Greg Munn <greg.munn@xamarin.com>
//
// Copyright (c) 2016 Xamarin Inc
//

using System;
using System.Collections.Generic;
using System.Linq;
using Mono.AndroidTools;
using Mono.AndroidTools.Util;

namespace Xamarin.AndroidTools.Debugging
{
	public static class AmIntentCommandParser
	{
		public static AmIntentCommand Parse(string command, string packageName) 
		{
			string[] args;
			if (!ProcessArgumentBuilder.TryParse(command, out args))
				throw new ArgumentException(string.Format("Command '{0}' could not be parsed", command));

			if (args.Length < 2)
				throw new ArgumentException(string.Format("Command '{0}' does not have the correct number of arguments", command));

			if (args[0] != "am")
				throw new ArgumentException(string.Format("Command '{0}' does not start with `am`", command));

			AmOptions options;
			switch (args[1]) {
				case "start":
					options = new AmStartOptions(packageName);

					break;
				case "startservice":
					options = new AmStartServiceOptions(packageName);

					break;
				case "broadcast":
					options = new AmBroadcastOptions(packageName);

					break;
				default:
					throw new NotSupportedException(string.Format("Unsupported `am {0}` command", args[1]));
			}

			var optSet = options.GetOptionSet();
			var optionArgs = args.Skip(2).ToArray();

			var remainingArgs = optSet.Parse(optionArgs);
			options.HandleRemainingArgs(remainingArgs);

			return options.Command;
		}
	
		public static ExtraDataUri ParseDataUri(string v)
		{
			return new ExtraDataUri(v);
		}

		public static ExtraComponentName ParseComponentName(string v)
		{
			return new ExtraComponentName(v);
		}

		public static long ParseLong(string v)
		{
			return long.Parse(v);
		}

		public static ExtraFloat ParseFloat(string v)
		{
			return new ExtraFloat(v);
		}

		public static int[] ParseIntArray(string v)
		{
			if (string.IsNullOrEmpty(v))
				return new int[0];

			var values = v.Split(new[] { "," }, StringSplitOptions.RemoveEmptyEntries);
			var result = new int[values.Length];
			for (int i = 0; i < values.Length; i++)
			{
				result[i] = int.Parse(values[i]);
			}

			return result;
		}

		public static long[] ParseLongArray(string v)
		{
			if (string.IsNullOrEmpty(v))
				return new long[0];

			var values = v.Split(new[] { "," }, StringSplitOptions.RemoveEmptyEntries);
			var result = new long[values.Length];
			for (int i = 0; i < values.Length; i++)
			{
				result[i] = ParseLong(values[i]);
			}

			return result;
		}

		public static ExtraFloat[] ParseFloatArray(string v)
		{
			if (string.IsNullOrEmpty(v))
				return new ExtraFloat[0];

			var values = v.Split(new[] { "," }, StringSplitOptions.RemoveEmptyEntries);
			var result = new ExtraFloat[values.Length];
			for (int i = 0; i < values.Length; i++)
			{
				result[i] = ParseFloat(values[i]);
			}

			return result;
		}

		public static bool ParseExtraBool(string v)
		{
			if (string.Equals(v, "true", StringComparison.OrdinalIgnoreCase))
			{
				return true;
			}

			if (string.Equals(v, "false", StringComparison.OrdinalIgnoreCase))
			{
				return false;
			}

			if (string.Equals(v, "1", StringComparison.OrdinalIgnoreCase))
			{
				return true;
			}

			if (string.Equals(v, "0", StringComparison.OrdinalIgnoreCase))
			{
				return false;
			}

			throw new FormatException("coud not parse boolean value");
		}

		abstract class AmOptions 
		{
			public AmIntentCommand Command { get; protected set; }
			public abstract Mono.Options.OptionSet GetOptionSet();
			public virtual void HandleRemainingArgs(List<string> args) 
			{
				if (args.Count > 0) {
					this.Command.Intent = args[0];
				}
			}

			protected void AddIntentOptions(Mono.Options.OptionSet options)
			{
				// intent options
				options.Add("n=", "Component", s => this.Command.Component = s);
				options.Add("a=", "Action", s => this.Command.Action = s);
				options.Add("d=", "Data Uri", s => this.Command.DataUri = s);
				options.Add("c=", "Category", s => this.Command.Categories.Add(s));
				options.Add("t=", "Mime Type", s => this.Command.MimeType = s);
				// strictly speaking we should also support -p <Package> here, but we don't because of conflicts with the use of 
				// packageName for fast dev property settings

				options.Add("selector", "Selector", s => this.Command.Selector = true);

			}

			protected void AddFlagsOptions(Mono.Options.OptionSet options)
			{
				options.Add("f=", (k) => { throw new NotSupportedException("-f is not supported, use individual flag arguments"); });

				options.Add("grant-read-uri-permission", (k) => this.Command.Flags = this.Command.Flags | IntentFlag.GrantReadUriPermission);
				options.Add("grant-write-uri-permission", (k) => this.Command.Flags = this.Command.Flags | IntentFlag.GrantWriteUriPermission);
				options.Add("debug-log-resolution", (k) => this.Command.Flags = this.Command.Flags | IntentFlag.DebugLogResolution);
				options.Add("exclude-stopped-packages", (k) => this.Command.Flags = this.Command.Flags | IntentFlag.ExcludeStoppedPackages);
				options.Add("include-stopped-packages", (k) => this.Command.Flags = this.Command.Flags | IntentFlag.IncludeStoppedPackages);
				options.Add("activity-brought-to-front", (k) => this.Command.Flags = this.Command.Flags | IntentFlag.ActivityBroughtToFront);
				options.Add("activity-clear-top", (k) => this.Command.Flags = this.Command.Flags | IntentFlag.ActivityClearTop);
				options.Add("activity-clear-when-task-reset", (k) => this.Command.Flags = this.Command.Flags | IntentFlag.ActivityClearWhenTaskReset);
				options.Add("activity-exclude-from-recents", (k) => this.Command.Flags = this.Command.Flags | IntentFlag.ActivityExcludeFromRecents);
				options.Add("activity-launched-from-history", (k) => this.Command.Flags = this.Command.Flags | IntentFlag.ActivityLaunchedFromHistory);
				options.Add("activity-multiple-task", (k) => this.Command.Flags = this.Command.Flags | IntentFlag.ActvityMultipleTask);
				options.Add("activity-no-animation", (k) => this.Command.Flags = this.Command.Flags | IntentFlag.ActivityNoAnimation);
				options.Add("activity-no-history", (k) => this.Command.Flags = this.Command.Flags | IntentFlag.ActivityNoHistory);
				options.Add("activity-no-user-action", (k) => this.Command.Flags = this.Command.Flags | IntentFlag.ActivityNoUserAction);
				options.Add("activity-previous-is-top", (k) => this.Command.Flags = this.Command.Flags | IntentFlag.ActivityPreviousIsTop);
				options.Add("activity-reorder-to-front", (k) => this.Command.Flags = this.Command.Flags | IntentFlag.ActivityReorderToFront);
				options.Add("activity-reset-task-if-needed", (k) => this.Command.Flags = this.Command.Flags | IntentFlag.ActivityResetTaskIfNeeded);
				options.Add("activity-single-top", (k) => this.Command.Flags = this.Command.Flags | IntentFlag.ActivitySingleTop);
				options.Add("activity-clear-task", (k) => this.Command.Flags = this.Command.Flags | IntentFlag.ActivityClearTask);
				options.Add("activity-task-on-home", (k) => this.Command.Flags = this.Command.Flags | IntentFlag.ActivityTaskOnHome);
				options.Add("receiver-registered-only", (k) => this.Command.Flags = this.Command.Flags | IntentFlag.ReceiverRegisteredOnly);
				options.Add("receiver-replace-pending", (k) => this.Command.Flags = this.Command.Flags | IntentFlag.ReceiverReplacePending);
			}

			protected void AddExtraOptions(Mono.Options.OptionSet options)
			{
				options.Add("esn=", "Extra Null", (k) => this.Command.Extras.Add(k, null));
				options.Add("e|es=", "Extra String", (k, v) => this.Command.Extras.Add(k, v));
				options.Add("ez=", "Extra Bool", (k, v) => this.Command.Extras.Add(k, ParseExtraBool(v)));
				options.Add("ei=", "Extra Int", (k, v) => this.Command.Extras.Add(k, int.Parse(v)));
				options.Add("el=", "Extra Long", (k, v) => this.Command.Extras.Add(k, ParseLong(v)));
				options.Add("ef=", "Extra Float", (k, v) => this.Command.Extras.Add(k, ParseFloat(v)));
				options.Add("eu=", "Extra Uri", (k, v) => this.Command.Extras.Add(k, ParseDataUri(v)));
				options.Add("ecn=", "Extra Component Name", (k, v) => this.Command.Extras.Add(k, ParseComponentName(v)));

				// extras that take an array
				options.Add("eia=", "Extra Int Array", (k, v) => this.Command.Extras.Add(k, ParseIntArray(v)));
				options.Add("ela=", "Extra Long Array", (k, v) => this.Command.Extras.Add(k, ParseLongArray(v)));
				options.Add("efa=", "Extra Float Array", (k, v) => this.Command.Extras.Add(k, ParseFloatArray(v)));
			}
		}

		class AmStartOptions : AmOptions
		{
			readonly AmStartCommand command;

			public AmStartOptions(string packageName)
			{
				this.Command = this.command = new AmStartCommand();
				this.command.PackageName = packageName;
				this.command.Extras = new Dictionary<string, object>();
				this.command.Categories = new List<string>();
			}

			public override Mono.Options.OptionSet GetOptionSet()
			{
				var options = new Mono.Options.OptionSet {
					// start options
					{ "D", "Enable Debugging", s => this.command.EnableDebugging = true },
					{ "W", "Wait For Launch", s => this.command.Wait = true },
					{ "start-profiler=", "Profiler Output", s => this.command.ProfilerOutputPath = s },
					{ "R=", "Repeat Launch", s => this.command.Repeat = int.Parse(s) },
					{ "S", "Force Stop", s => this.command.ForceStop = true },
					{ "opengl-trace", "Open GL Trace", s => this.command.EnableOpenGLTracing = true },
					{ "user=", "User Id", s => this.command.User = s },
				};

				AddIntentOptions(options);
				AddExtraOptions(options);
				AddFlagsOptions(options);
				return options;
			}
		}

		class AmStartServiceOptions : AmOptions
		{
			readonly AmStartServiceCommand command;

			public AmStartServiceOptions(string packageName)
			{
				this.Command = this.command = new AmStartServiceCommand();
				this.command.PackageName = packageName;
				this.command.Extras = new Dictionary<string, object>();
				this.command.Categories = new List<string>();
			}

			public override Mono.Options.OptionSet GetOptionSet()
			{
				var options = new Mono.Options.OptionSet {
					{ "user=", "User Id", s => this.command.User = s },
				};

				AddIntentOptions(options);
				AddExtraOptions(options);
				AddFlagsOptions(options);
				return options;
			}
		}

		class AmBroadcastOptions : AmOptions
		{
			readonly AmBroadcastCommand command;

			public AmBroadcastOptions(string packageName)
			{
				this.Command = this.command = new AmBroadcastCommand();
				this.command.PackageName = packageName;
				this.command.Extras = new Dictionary<string, object>();
				this.command.Categories = new List<string>();
			}

			public override Mono.Options.OptionSet GetOptionSet()
			{
				var options = new Mono.Options.OptionSet {
					{ "user=", "User Id", s => this.command.User = s },
				};

				AddIntentOptions(options);
				AddExtraOptions(options);
				AddFlagsOptions(options);
				return options;
			}
		}
	}
}