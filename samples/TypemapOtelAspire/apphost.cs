#:sdk Aspire.AppHost.Sdk@13.3.5

var builder = DistributedApplication.CreateBuilder(args);

var otlpHttpEndpoint = Environment.GetEnvironmentVariable("ASPIRE_DASHBOARD_OTLP_HTTP_ENDPOINT_URL") ?? "http://localhost:4318";
var configuration = Environment.GetEnvironmentVariable("HELLOWORLD_ANDROID_CONFIGURATION") ?? "Release";
var runtime = Environment.GetEnvironmentVariable("HELLOWORLD_ANDROID_RUNTIME") ?? "CoreCLR";
var typemap = Environment.GetEnvironmentVariable("HELLOWORLD_ANDROID_TYPEMAP") ?? "trimmable";

builder.AddExecutable(
	"helloworld-android",
	"bash",
	"../..",
	"samples/TypemapOtelAspire/run-helloworld-android.sh")
	.WithEnvironment("OTEL_EXPORTER_OTLP_ENDPOINT", otlpHttpEndpoint)
	.WithEnvironment("HELLOWORLD_ANDROID_CONFIGURATION", configuration)
	.WithEnvironment("HELLOWORLD_ANDROID_RUNTIME", runtime)
	.WithEnvironment("HELLOWORLD_ANDROID_TYPEMAP", typemap);

builder.Build().Run();
