using System;
using System.Collections.Generic;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Xamarin.Android.Tasks;

class EnvironmentBuilder
{
	static readonly string[] defaultLogLevel = {"MONO_LOG_LEVEL", "info"};
	static readonly string[] defaultMonoDebug = {"MONO_DEBUG", "gen-compact-seq-points"};
	static readonly string defaultHttpMessageHandler = "System.Net.Http.HttpClientHandler, System.Net.Http";

	readonly EnvironmentFilesParser environmentParser;
	readonly Dictionary<string, string> environmentVariables;
	readonly Dictionary<string, string> systemProperties;
	readonly TaskLoggingHelper log;
	readonly SequencePointsMode sequencePointsMode;

	public IDictionary<string, string> EnvironmentVariables => environmentVariables;
	public IDictionary<string, string> SystemProperties => systemProperties;
	public EnvironmentFilesParser Parser => environmentParser;

	public EnvironmentBuilder (
		TaskLoggingHelper log,
		bool usesAssemblyPreload = false,
		SequencePointsMode sequencePointsMode = SequencePointsMode.None,
		bool brokenExceptionTransitions = false)
	{
		this.log = log;
		this.sequencePointsMode = sequencePointsMode;

		environmentVariables = new Dictionary<string, string> (StringComparer.Ordinal);
		systemProperties = new Dictionary<string, string> (StringComparer.Ordinal);

		environmentParser = new EnvironmentFilesParser {
			BrokenExceptionTransitions = brokenExceptionTransitions,
			UsesAssemblyPreload = usesAssemblyPreload,
		};
	}

	public void Read (ITaskItem[]? envItems)
	{
		environmentParser.Parse (envItems, sequencePointsMode, log);

		foreach (string line in environmentParser.EnvironmentVariableLines) {
			AddEnvironmentVariableLine (line);
		}
	}

	public void AddEnvironmentVariable (string name, string value)
	{
		if (Char.IsUpper(name [0]) || !Char.IsLetter(name [0])) {
			environmentVariables [ValidAssemblerString (name)] = ValidAssemblerString (value);
		} else {
			systemProperties [ValidAssemblerString (name)] = ValidAssemblerString (value);
		}
	}

	public void AddEnvironmentVariableLine (string l)
	{
		string? line = l?.Trim ();
		if (line.IsNullOrEmpty () || line! [0] == '#') {
			return;
		}

		string[] nv = line.Split (new char[]{'='}, 2);
		AddEnvironmentVariable (nv[0].Trim (), nv.Length < 2 ? String.Empty : nv[1].Trim ());
	}

	public void AddDefaultDebugBuildLogLevel ()
	{
		if (environmentParser.HaveLogLevel) {
			return;
		}

		AddEnvironmentVariable (defaultLogLevel[0], defaultLogLevel[1]);
	}

	public void AddDefaultMonoDebug ()
	{
		if (sequencePointsMode == SequencePointsMode.None || environmentParser.HaveMonoDebug) {
			return;
		}

		AddEnvironmentVariable (defaultMonoDebug[0], defaultMonoDebug[1]);
	}

	public void AddHttpClientHandlerType (string? handlerType)
	{
		if (environmentParser.HaveHttpMessageHandler) {
			return;
		}

		if (String.IsNullOrEmpty (handlerType)) {
			handlerType = defaultHttpMessageHandler;
		}

		AddEnvironmentVariable ("XA_HTTP_CLIENT_HANDLER_TYPE", handlerType!.Trim ());
	}

	public void AddMonoGcParams (bool enableSgenConcurrent)
	{
		if (environmentParser.HaveMonoGCParams) {
			return;
		}

		AddEnvironmentVariable ("MONO_GC_PARAMS", enableSgenConcurrent ? "major=marksweep-conc" : "major=marksweep");
	}

	static string ValidAssemblerString (string s) => s.Replace ("\\", "\\\\").Replace ("\"", "\\\"");
}
