using Android.Runtime;
using Microsoft.Testing.Extensions;
using Microsoft.Testing.Platform.Builder;
using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Extensions.Messages;

namespace JniReferenceLeakTests;

[Instrumentation (Name = "net.dot.jni.referenceleaktests.TestInstrumentation")]
public class TestInstrumentation : Instrumentation
{
	protected TestInstrumentation (IntPtr handle, JniHandleOwnership ownership)
		: base (handle, ownership)
	{
	}

	public override void OnCreate (Bundle? arguments)
	{
		base.OnCreate (arguments);
		Start ();
	}

	public override async void OnStart ()
	{
		base.OnStart ();

		var consumer = new ResultConsumer (this);
		var bundle = new Bundle ();
		try {
			var writeablePath = Application.Context.GetExternalFilesDir (null)?.AbsolutePath ?? Path.GetTempPath ();
			var resultsPath = Path.Combine (writeablePath, "TestResults");
			var builder = await TestApplication.CreateBuilderAsync ([
				"--results-directory", resultsPath,
				"--report-trx",
			]);
			builder.AddMSTest (() => [GetType ().Assembly]);
			builder.AddTrxReportProvider ();
			builder.TestHost.AddDataConsumer (_ => consumer);

			using ITestApplication app = await builder.BuildAsync ();
			await app.RunAsync ();

			bundle.PutInt ("passed", consumer.Passed);
			bundle.PutInt ("failed", consumer.Failed);
			bundle.PutInt ("skipped", consumer.Skipped);
			bundle.PutString ("resultsPath", consumer.TrxReportPath);
			Finish (Result.Ok, bundle);
		} catch (Exception ex) {
			bundle.PutString ("error", ex.ToString ());
			Finish (Result.Canceled, bundle);
		}
	}

	class ResultConsumer (Instrumentation instrumentation) : IDataConsumer
	{
		int passed;
		int failed;
		int skipped;

		public int Passed => passed;
		public int Failed => failed;
		public int Skipped => skipped;
		public string? TrxReportPath { get; private set; }

		public string Uid => nameof (ResultConsumer);
		public string DisplayName => nameof (ResultConsumer);
		public string Description => "";
		public string Version => "1.0";

		public Type [] DataTypesConsumed => [typeof (TestNodeUpdateMessage), typeof (SessionFileArtifact)];

		public Task<bool> IsEnabledAsync () => Task.FromResult (true);

		public Task ConsumeAsync (IDataProducer dataProducer, IData value, CancellationToken cancellationToken)
		{
			if (value is SessionFileArtifact artifact) {
				TrxReportPath = artifact.FileInfo.FullName;
				return Task.CompletedTask;
			}

			if (value is not TestNodeUpdateMessage { TestNode: var node }) {
				return Task.CompletedTask;
			}

			var state = node.Properties.SingleOrDefault<TestNodeStateProperty> ();
			string? outcome = state switch {
				PassedTestNodeStateProperty => "passed",
				FailedTestNodeStateProperty or ErrorTestNodeStateProperty or TimeoutTestNodeStateProperty => "failed",
				SkippedTestNodeStateProperty => "skipped",
				_ => null,
			};
			if (outcome is null) {
				return Task.CompletedTask;
			}

			_ = outcome switch {
				"passed" => Interlocked.Increment (ref passed),
				"failed" => Interlocked.Increment (ref failed),
				_ => Interlocked.Increment (ref skipped),
			};

			var id = node.Properties.SingleOrDefault<TestMethodIdentifierProperty> ();
			var bundle = new Bundle ();
			bundle.PutString ("test", id is not null ? $"{id.Namespace}.{id.TypeName}.{id.MethodName}" : node.DisplayName);
			bundle.PutString ("outcome", outcome);
			instrumentation.SendStatus (0, bundle);
			return Task.CompletedTask;
		}
	}
}
