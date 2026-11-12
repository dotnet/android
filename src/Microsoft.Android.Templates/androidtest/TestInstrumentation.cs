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
    int _passed, _failed, _skipped, _discovered;

    protected TestInstrumentation(IntPtr handle, JniHandleOwnership ownership)
        : base(handle, ownership) { }

    public override void OnCreate(Bundle? arguments)
    {
        base.OnCreate(arguments);
        // 'dotnet test --list-tests' passes '-e list-tests true' to only discover tests.
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

            // Host MSTest in-process in server mode, and drive it with the server-mode client.
            // This reports discovered tests (for 'dotnet test --list-tests') as well as test results.
            using var client = await MtpServerClient.LaunchInProcessAsync(async (serverArgs, cancellationToken) =>
            {
                string[] args = _listTests
                    ? [.. serverArgs, "--results-directory", resultsPath]
                    : [.. serverArgs, "--results-directory", resultsPath, "--report-trx"];
                var builder = await TestApplication.CreateBuilderAsync(args);
                builder.AddMSTest(() => [GetType().Assembly]);
                builder.AddTrxReportProvider();
                using ITestApplication app = await builder.BuildAsync();
                return await app.RunAsync();
            });
            client.TestNodesUpdated += (_, e) =>
            {
                foreach (var change in e.Changes)
                    ReportStatus(change);
            };

            await client.InitializeAsync();
            if (_listTests)
            {
                await client.DiscoverTestsAsync();
                bundle.PutInt("discovered", _discovered);
            }
            else
            {
                var result = await client.RunTestsAsync();
                bundle.PutInt("passed", _passed);
                bundle.PutInt("failed", _failed);
                bundle.PutInt("skipped", _skipped);
                bundle.PutString("resultsPath", GetTrxPath(result));
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

    void ReportStatus(MtpTestNodeUpdate change)
    {
        if (change.NodeType != "action" || change.Uid is null)
            return;

        string eventName = change.ExecutionState switch
        {
            "discovered" => "discovered",
            "in-progress" => "start",
            _ => "finish",
        };
        string? outcome = change.ExecutionState switch
        {
            "passed" => "passed",
            "skipped" => "skipped",
            "failed" or "error" or "timed-out" or "canceled" => "failed",
            _ => null,
        };
        if (eventName == "finish" && outcome is null)
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
        b.PutString("test", change.Uid);
        b.PutString("name", change.DisplayName ?? change.Uid);
        if (change.Node.TryGetValue("location.type", out var type) && type is string className)
            b.PutString("class", className);
        if (outcome is not null)
            b.PutString("outcome", outcome);
        if (change.ErrorMessage is { } message)
            b.PutString("message-b64", Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(message)));
        if (change.ErrorStackTrace is { } stack)
            b.PutString("stack-b64", Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(stack)));
        SendStatus(0, b);
    }

    static string? GetTrxPath(MtpRunResult result)
    {
        foreach (var artifact in result.Artifacts)
        {
            if (artifact.Uri is not { } uri || !uri.EndsWith(".trx", StringComparison.OrdinalIgnoreCase))
                continue;
            return Uri.TryCreate(uri, UriKind.Absolute, out var parsed) && parsed.IsFile ? parsed.LocalPath : uri;
        }
        return null;
    }
}