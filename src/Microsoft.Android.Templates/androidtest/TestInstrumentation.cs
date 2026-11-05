using Android.Runtime;
using Microsoft.Testing.Extensions;
using Microsoft.Testing.Platform.Builder;
using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.ServerMode.Client;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Xml.Linq;

[assembly: Parallelize(Scope = ExecutionScope.MethodLevel)]

namespace AndroidTest1;

[Instrumentation(Name = "com.companyname.AndroidTest1.TestInstrumentation")]
public class TestInstrumentation : Instrumentation
{
    Bundle? instrumentationArguments;

    protected TestInstrumentation(IntPtr handle, JniHandleOwnership ownership)
        : base(handle, ownership) { }

    public override void OnCreate(Bundle? arguments)
    {
        instrumentationArguments = arguments;
        base.OnCreate(arguments);
        Start();
    }

    public override async void OnStart()
    {
        base.OnStart();

        var consumer = new ResultConsumer(this);
        var bundle = new Bundle();
        try
        {
            var writeablePath = Application.Context.GetExternalFilesDir(null)?.AbsolutePath ?? Path.GetTempPath();
            var resultsPath = Path.Combine(writeablePath, "TestResults");
            string[]? testUids = null;
            if (instrumentationArguments?.GetString("test-uids") is string encodedUids)
            {
                testUids = XElement.Parse(Encoding.UTF8.GetString(Convert.FromBase64String(encodedUids)))
                    .Elements("test").Select(test => (string?)test.Attribute("uid")
                        ?? throw new FormatException("Missing test UID in the instrumentation filter.")).ToArray();
                if (testUids.Length == 0)
                    throw new ArgumentException("The selected-test filter must not be empty.");
            }
            bool discover = instrumentationArguments?.GetString("list-tests") == "true";
            if (discover)
            {
                bundle.PutInt("discovered", await DiscoverTestsAsync(resultsPath, testUids));
                Finish(Result.Ok, bundle);
                return;
            }
            var args = new List<string> {
                "--results-directory", resultsPath,
                "--report-trx"
            };
            if (testUids is not null)
            {
                args.Add("--filter-uid");
                args.AddRange(testUids);
            }
            var builder = await TestApplication.CreateBuilderAsync(args.ToArray());
            builder.AddMSTest(() => [GetType().Assembly]);
            builder.AddTrxReportProvider();
            builder.TestHost.AddDataConsumer(_ => consumer);

            using ITestApplication app = await builder.BuildAsync();
            await app.RunAsync();

            bundle.PutInt("passed", consumer.Passed);
            bundle.PutInt("failed", consumer.Failed);
            bundle.PutInt("skipped", consumer.Skipped);
            bundle.PutString("resultsPath", consumer.TrxReportPath);
            Finish(Result.Ok, bundle);
        }
        catch (Exception ex)
        {
            bundle.PutString("error", ex.ToString());
            Finish(Result.Canceled, bundle);
        }
    }

    async Task<int> DiscoverTestsAsync(string resultsPath, string[]? testUids)
    {
        // MTP does not activate ordinary data consumers during discovery.
        // Use its embedded server client to receive discovery notifications.
        using var client = await MtpServerClient.LaunchInProcessAsync(async (serverArgs, cancellationToken) =>
        {
            var builder = await TestApplication.CreateBuilderAsync([
                .. serverArgs, "--results-directory", resultsPath
            ]);
            builder.AddMSTest(() => [GetType().Assembly]);
            using var app = await builder.BuildAsync();
            return await app.RunAsync();
        });
        int discovered = 0;
        Exception? discoveryError = null;
        client.TestNodesUpdated += (_, update) =>
        {
            // Notification-handler exceptions are logged, not propagated by the client.
            try
            {
                foreach (var node in update.Changes)
                {
                    if (node.ExecutionState != "discovered")
                        continue;
                    if (string.IsNullOrEmpty(node.Uid))
                        throw new InvalidOperationException("A discovered test has no UID.");
                    var status = new Bundle();
                    status.PutString("event", "discover");
                    status.PutString("test", node.Uid);
                    status.PutString("name", node.DisplayName ?? node.Uid);
                    WriteMethodIdentifier(status, GetDiscoveryMethodIdentifier(node));
                    SendStatus(0, status);
                    Interlocked.Increment(ref discovered);
                }
            }
            catch (Exception ex)
            {
                discoveryError = ex;
            }
        };
        try
        {
            await client.InitializeAsync();
            if (testUids is not null)
            {
                await client.DiscoverTestsAsync(testUids);
            }
            else
            {
                await client.DiscoverTestsAsync();
            }
            await client.ExitAsync();
        }
        finally
        {
            await client.ShutdownAsync();
        }
        if (client.ServerExitCode is not 0)
            throw new InvalidOperationException($"Test discovery server exited with code {client.ServerExitCode}.");
        if (discoveryError is not null)
            throw new InvalidOperationException("Failed to report discovered tests.", discoveryError);
        return discovered;
    }

    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "The project roots the complete test assembly with TrimmerRootAssembly.")]
    [UnconditionalSuppressMessage("Trimming", "IL2075", Justification = "The project roots all methods in the test assembly with TrimmerRootAssembly.")]
    TestMethodIdentifierProperty? GetDiscoveryMethodIdentifier(MtpTestNodeUpdate node)
    {
        if (!node.Node.TryGetValue("location.type", out var typeValue) || typeValue is not string typeName ||
            !node.Node.TryGetValue("location.method", out var methodValue) || methodValue is not string signature)
            return null;

        // Server notifications omit the assembly, return type, and separate parameter types.
        // Recover the complete identifier from the rooted test assembly, without changing its UID.
        var type = GetType().Assembly.GetType(typeName, throwOnError: true)
            ?? throw new InvalidOperationException($"Discovered test type '{typeName}' could not be resolved.");
        var arity = node.Node.TryGetValue("location.method-arity", out var arityValue)
            ? Convert.ToInt32(arityValue, System.Globalization.CultureInfo.InvariantCulture) : 0;
        var method = type.GetMethods(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Static
            | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic).Single(candidate =>
        {
            var parameters = candidate.GetParameters();
            var candidateSignature = parameters.Length == 0 ? candidate.Name
                : $"{candidate.Name}({string.Join(",", parameters.Select(parameter => parameter.ParameterType.FullName ?? parameter.ParameterType.Name))})";
            return candidateSignature == signature && candidate.GetGenericArguments().Length == arity;
        });
        return new TestMethodIdentifierProperty(
            type.Assembly.FullName ?? throw new InvalidOperationException("The test assembly has no full name."),
            type.Namespace ?? "", type.Namespace is null ? typeName : typeName[(type.Namespace.Length + 1)..], method.Name, arity,
            method.GetParameters().Select(parameter => parameter.ParameterType.FullName ?? parameter.ParameterType.Name).ToArray(),
            method.ReturnType.FullName ?? method.ReturnType.Name);
    }

    static void WriteMethodIdentifier(Bundle status, TestMethodIdentifierProperty? id)
    {
        if (id is null)
            return;
        status.PutString("class", string.IsNullOrEmpty(id.Namespace) ? id.TypeName : $"{id.Namespace}.{id.TypeName}");
        status.PutString("assembly", id.AssemblyFullName);
        status.PutString("namespace", id.Namespace);
        status.PutString("type", id.TypeName);
        status.PutString("method", id.MethodName);
        status.PutString("method-arity", id.MethodArity.ToString(System.Globalization.CultureInfo.InvariantCulture));
        status.PutString("parameter-types", Convert.ToBase64String(Encoding.UTF8.GetBytes(
            new XElement("tests", id.ParameterTypeFullNames.Select(type => new XElement("test", new XAttribute("uid", type)))).ToString(SaveOptions.DisableFormatting))));
        status.PutString("return-type", id.ReturnTypeFullName);
    }

    class ResultConsumer(Instrumentation instrumentation) : IDataConsumer
    {
        int _passed, _failed, _skipped;
        public int Passed => _passed;
        public int Failed => _failed;
        public int Skipped => _skipped;
        public string? TrxReportPath;

        public string Uid => nameof(ResultConsumer);
        public string DisplayName => nameof(ResultConsumer);
        public string Description => "";
        public string Version => "1.0";
        public Task<bool> IsEnabledAsync() => Task.FromResult(true);

        public Type[] DataTypesConsumed => [typeof(TestNodeUpdateMessage), typeof(SessionFileArtifact)];

        public Task ConsumeAsync(IDataProducer dataProducer, IData value, CancellationToken cancellationToken)
        {
            if (value is SessionFileArtifact artifact)
            {
                TrxReportPath = artifact.FileInfo.FullName;
            }
            else if (value is TestNodeUpdateMessage { TestNode: var node })
            {
                var state = node.Properties.SingleOrDefault<TestNodeStateProperty>();
                string? eventKind = state switch
                {
                    InProgressTestNodeStateProperty => "start",
                    PassedTestNodeStateProperty or FailedTestNodeStateProperty
                        or ErrorTestNodeStateProperty or TimeoutTestNodeStateProperty
                        or SkippedTestNodeStateProperty => "finish",
                    _ => null
                };
                if (eventKind is null)
                    return Task.CompletedTask;

                string? outcome = state switch
                {
                    PassedTestNodeStateProperty => "passed",
                    FailedTestNodeStateProperty or ErrorTestNodeStateProperty
                        or TimeoutTestNodeStateProperty => "failed",
                    SkippedTestNodeStateProperty => "skipped",
                    _ => null
                };
                if (outcome is not null)
                    _ = outcome switch { "passed" => Interlocked.Increment(ref _passed), "failed" => Interlocked.Increment(ref _failed), _ => Interlocked.Increment(ref _skipped) };

                var id = node.Properties.SingleOrDefault<TestMethodIdentifierProperty>();
                var b = new Bundle();
                b.PutString("event", eventKind);
                b.PutString("test", node.Uid.Value);
                b.PutString("name", node.DisplayName);
                if (outcome is not null)
                    b.PutString("outcome", outcome);
                WriteMethodIdentifier(b, id);
                var exception = state switch
                {
                    FailedTestNodeStateProperty failed => failed.Exception,
                    ErrorTestNodeStateProperty error => error.Exception,
                    TimeoutTestNodeStateProperty timeout => timeout.Exception,
                    _ => null
                };
                var message = state?.Explanation ?? exception?.Message;
                if (message is not null)
                    b.PutString("message-b64", Convert.ToBase64String(Encoding.UTF8.GetBytes(message)));
                if (exception?.StackTrace is string stackTrace)
                    b.PutString("stack-b64", Convert.ToBase64String(Encoding.UTF8.GetBytes(stackTrace)));
                instrumentation.SendStatus(0, b);
            }
            return Task.CompletedTask;
        }
    }
}
