#:sdk Aspire.AppHost.Sdk@13.3.5

var builder = DistributedApplication.CreateBuilder(args);

var otlpHttpEndpoint = Environment.GetEnvironmentVariable("ASPIRE_DASHBOARD_OTLP_HTTP_ENDPOINT_URL") ?? "http://localhost:4318";

builder.AddExecutable(
	"helloworld-android",
	"bash",
	"../..",
	"samples/TypemapOtelAspire/run-helloworld-android.sh")
	.WithEnvironment("OTEL_EXPORTER_OTLP_ENDPOINT", otlpHttpEndpoint)
	.WithEnvironment("HELLOWORLD_ANDROID_CONFIGURATION", "Release")
	.WithEnvironment("HELLOWORLD_ANDROID_RUNTIME", "CoreCLR")
	.WithEnvironment("HELLOWORLD_ANDROID_TYPEMAP", "trimmable");

builder.Build().Run();
