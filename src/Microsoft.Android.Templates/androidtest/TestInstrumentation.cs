using Android.Runtime;
using Microsoft.Testing.Extensions;
using Microsoft.Testing.Platform.Builder;
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
    bool _listTests;
    string? _encodedTestUids;
    int _discovered;
    int _passed, _failed, _skipped;

    protected TestInstrumentation(IntPtr handle, JniHandleOwnership ownership)
        : base(handle, ownership) { }

    public override void OnCreate(Bundle? arguments)
    {
        base.OnCreate(arguments);
        // Set by 'dotnet test --list-tests'
        _listTests = arguments?.GetString("list-tests") == "true";
        _encodedTestUids = arguments?.GetString("test-uids");
        Start();
    }

    public override async void OnStart()
    {
        base.OnStart();

        var bundle = new Bundle();
        try
        {
            var externalFilesPath = Application.Context.GetExternalFilesDir(null)?.AbsolutePath;
            var writeablePath = externalFilesPath ?? Path.GetTempPath();
            var resultsPath = Path.Combine(writeablePath, "TestResults");
            string[]? testUids = null;
            if (_encodedTestUids is string encodedUids)
            {
                if (encodedUids == "file-location")
                {
                    if (externalFilesPath is null)
                        throw new InvalidOperationException("The app's external files directory is unavailable.");
                    bundle.PutString("test-uids-directory", externalFilesPath);
                    Finish(Result.Ok, bundle);
                    return;
                }
                if (encodedUids.StartsWith("file:", StringComparison.Ordinal))
                {
                    var token = encodedUids["file:".Length..];
                    if (!Guid.TryParseExact(token, "N", out _))
                        throw new FormatException("Invalid instrumentation filter file token.");
                    if (externalFilesPath is null)
                        throw new InvalidOperationException("The app's external files directory is unavailable.");
                    encodedUids = File.ReadAllText(Path.Combine(externalFilesPath, $"test-uids-{token}.txt"));
                }
                testUids = XElement.Parse(Encoding.UTF8.GetString(Convert.FromBase64String(encodedUids)))
                    .Elements("test").Select(test => (string?)test.Attribute("uid")
                        ?? throw new FormatException("Missing test UID in the instrumentation filter.")).ToArray();
                if (testUids.Length == 0)
                    throw new ArgumentException("The selected-test filter must not be empty.");
            }
            using var client = await MtpServerClient.LaunchInProcessAsync(async (serverArgs, cancellationToken) =>
            {
                var builder = await TestApplication.CreateBuilderAsync(_listTests
                    ? [.. serverArgs, "--results-directory", resultsPath]
                    : [.. serverArgs, "--results-directory", resultsPath, "--report-trx"]);
                builder.AddMSTest(() => [GetType().Assembly]);
                builder.AddTrxReportProvider();
                using ITestApplication app = await builder.BuildAsync();
                return await app.RunAsync();
            });
            Exception? reportingError = null;
            client.TestNodesUpdated += (_, e) =>
            {
                try
                {
                    foreach (var node in e.Changes)
                        ReportStatus(node);
                }
                catch (Exception ex)
                {
                    reportingError = ex;
                }
            };

            try
            {
                await client.InitializeAsync();
                if (_listTests)
                {
                    if (testUids is null)
                        await client.DiscoverTestsAsync();
                    else
                        await client.DiscoverTestsAsync(testUids);
                    bundle.PutInt("discovered", _discovered);
                }
                else
                {
                    var result = testUids is null ? await client.RunTestsAsync() : await client.RunTestsAsync(testUids);
                    bundle.PutInt("passed", _passed);
                    bundle.PutInt("failed", _failed);
                    bundle.PutInt("skipped", _skipped);
                    bundle.PutString("resultsPath", result.Artifacts.FirstOrDefault(a => a.Uri?.EndsWith(".trx") == true)?.Uri);
                }
                await client.ExitAsync();
            }
            finally
            {
                await client.ShutdownAsync();
            }
            if (_listTests && client.ServerExitCode is not 0)
                throw new InvalidOperationException($"Test discovery server exited with code {client.ServerExitCode}.");
            if (reportingError is not null)
                throw new InvalidOperationException("Failed to report test results.", reportingError);
            Finish(Result.Ok, bundle);
        }
        catch (Exception ex)
        {
            bundle.PutString("error", ex.ToString());
            Finish(Result.Canceled, bundle);
        }
    }

    void ReportStatus(MtpTestNodeUpdate node)
    {
        if (node.NodeType != "action")
            return;

        (string? eventName, string? outcome) = node.ExecutionState switch
        {
            "discovered" => ("discovered", null),
            "in-progress" => ("start", null),
            "passed" => ("finish", "passed"),
            "skipped" => ("finish", "skipped"),
            "failed" or "error" or "timed-out" or "canceled" => ("finish", "failed"),
            _ => (null, null),
        };
        if (eventName is null)
            return;

        if (eventName == "discovered")
            Interlocked.Increment(ref _discovered);
        else if (outcome == "passed")
            Interlocked.Increment(ref _passed);
        else if (outcome == "failed")
            Interlocked.Increment(ref _failed);
        else if (outcome == "skipped")
            Interlocked.Increment(ref _skipped);

        var b = new Bundle();
        b.PutString("event", eventName);
        b.PutString("test", node.Uid);
        b.PutString("name", node.DisplayName);
        WriteMethodIdentifier(b, GetMethodIdentifier(node));
        if (node.Node.GetValueOrDefault("location.type") is string className)
            b.PutString("class", className);
        if (outcome is not null)
            b.PutString("outcome", outcome);
        if (node.ErrorMessage is { } message)
            b.PutString("message-b64", Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(message)));
        if (node.ErrorStackTrace is { } stack)
            b.PutString("stack-b64", Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(stack)));
        SendStatus(0, b);
    }

    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "MSTest.SourceGeneration preserves the discovered test types and their members.")]
    [UnconditionalSuppressMessage("Trimming", "IL2075", Justification = "MSTest.SourceGeneration preserves the methods on each discovered test class.")]
    TestMethodIdentifierProperty? GetMethodIdentifier(MtpTestNodeUpdate node)
    {
        if (!node.Node.TryGetValue("location.type", out var typeValue) || typeValue is not string typeName ||
            !node.Node.TryGetValue("location.method", out var methodValue) || methodValue is not string signature)
            return null;

        var type = GetType().Assembly.GetType(typeName, throwOnError: true)
            ?? throw new InvalidOperationException($"Discovered test type '{typeName}' could not be resolved.");
        var arity = node.Node.TryGetValue("location.method-arity", out var arityValue)
            ? Convert.ToInt32(arityValue, System.Globalization.CultureInfo.InvariantCulture) : 0;
        var method = type.GetMethods(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Static
            | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic)
            .Single(candidate => ManagedTestName.Matches(candidate, signature, arity));
        return new TestMethodIdentifierProperty(
            type.Assembly.FullName ?? throw new InvalidOperationException("The test assembly has no full name."),
            type.Namespace ?? "", type.Namespace is null ? typeName : typeName[(type.Namespace.Length + 1)..], method.Name, arity,
            method.GetParameters().Select(parameter => ManagedTestName.GetTypeName(parameter.ParameterType)).ToArray(),
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
}
