using Android.Runtime;
using Microsoft.Testing.Extensions;
using Microsoft.Testing.Platform.Builder;
using Microsoft.Testing.Platform.ServerMode.Client;

[assembly: Parallelize(Scope = ExecutionScope.MethodLevel)]

namespace AndroidTest1;

[Instrumentation(Name = "com.companyname.AndroidTest1.TestInstrumentation")]
public class TestInstrumentation : Instrumentation
{
    bool _listTests;
    int _passed, _failed, _skipped;

    protected TestInstrumentation(IntPtr handle, JniHandleOwnership ownership)
        : base(handle, ownership) { }

    public override void OnCreate(Bundle? arguments)
    {
        base.OnCreate(arguments);
        // Set by 'dotnet test --list-tests'
        _listTests = arguments?.GetString("list-tests") == "true";
        Start();
    }

    public override async void OnStart()
    {
        base.OnStart();

        var bundle = new Bundle();
        try
        {
            var writeablePath = Application.Context.GetExternalFilesDir(null)?.AbsolutePath ?? Path.GetTempPath();
            var resultsPath = Path.Combine(writeablePath, "TestResults");
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
            client.TestNodesUpdated += (_, e) =>
            {
                foreach (var node in e.Changes)
                    ReportStatus(node);
            };

            await client.InitializeAsync();
            if (_listTests)
            {
                await client.DiscoverTestsAsync();
            }
            else
            {
                var result = await client.RunTestsAsync();
                bundle.PutInt("passed", _passed);
                bundle.PutInt("failed", _failed);
                bundle.PutInt("skipped", _skipped);
                bundle.PutString("resultsPath", result.Artifacts.FirstOrDefault(a => a.Uri?.EndsWith(".trx") == true)?.Uri);
            }
            await client.ExitAsync();
            await client.ShutdownAsync();
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
        if (node.NodeType != "action" || node.Uid is null)
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

        if (outcome == "passed")
            Interlocked.Increment(ref _passed);
        else if (outcome == "failed")
            Interlocked.Increment(ref _failed);
        else if (outcome == "skipped")
            Interlocked.Increment(ref _skipped);

        var b = new Bundle();
        b.PutString("event", eventName);
        b.PutString("test", node.Uid);
        b.PutString("name", node.DisplayName ?? node.Uid);
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
}