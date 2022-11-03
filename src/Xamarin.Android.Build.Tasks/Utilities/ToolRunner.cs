using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;

using Microsoft.Build.Utilities;

using TPLTask = System.Threading.Tasks.Task;

namespace Xamarin.Android.Tasks
{
	abstract class ToolRunner
	{
		protected abstract class ToolOutputSink : TextWriter
		{
			TaskLoggingHelper log;

			public override Encoding Encoding => Encoding.Default;

			protected ToolOutputSink (TaskLoggingHelper logger)
			{
				log = logger;
			}

			public override void WriteLine (string? value)
			{
				log.LogMessage (value ?? String.Empty);
			}
		}

		static readonly TimeSpan DefaultProcessTimeout = TimeSpan.FromMinutes (15);

		protected TaskLoggingHelper Logger { get; }

		public string ToolPath                 { get; }
		public bool EchoCmdAndArguments        { get; set; } = true;
		public bool EchoStandardError          { get; set; } = true;
		public bool EchoStandardOutput         { get; set; }
		public virtual TimeSpan ProcessTimeout { get; set; } = DefaultProcessTimeout;

		protected ToolRunner (TaskLoggingHelper logger, string toolPath)
		{
			if (String.IsNullOrEmpty (toolPath)) {
				throw new ArgumentException ("must not be null or empty", nameof (toolPath));
			}

			Logger = logger;
			ToolPath = toolPath;
		}

                protected virtual ProcessRunner CreateProcessRunner (params string[] initialParams)
                {
	                var runner = new ProcessRunner (Logger, ToolPath) {
                                ProcessTimeout = ProcessTimeout,
                                EchoCmdAndArguments = EchoCmdAndArguments,
                                EchoStandardError = EchoStandardError,
                                EchoStandardOutput = EchoStandardOutput,
                        };

                        runner.AddArguments (initialParams);
                        return runner;
                }

		protected virtual async Task<bool> RunTool (Func<bool> runner)
		{
			return await TPLTask.Run (runner);
		}

		protected TextWriter SetupOutputSink (ProcessRunner runner, bool ignoreStderr = false)
                {
                        TextWriter ret = CreateLogSink (Logger);

                        if (!ignoreStderr) {
                                runner.AddStandardErrorSink (ret);
                        }
                        runner.AddStandardOutputSink (ret);

                        return ret;
                }

		protected virtual TextWriter CreateLogSink (TaskLoggingHelper logger)
		{
			throw new NotSupportedException ("Child class must implement this method if it uses output sinks");
		}
	}
}
