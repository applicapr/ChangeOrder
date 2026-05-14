namespace ChangeOrder.Presentation.DTOs.Responses;

/// <summary>
/// Response payload for <c>GET /version</c>. Identifies the running API
/// build so operators and smoke-test scripts can confirm which release
/// is deployed and against which environment.
/// </summary>
/// <param name="Name">Logical service name, e.g. <c>"ChangeOrder.Api"</c>.</param>
/// <param name="Version">
/// Semantic version of the build, sourced from
/// <c>AssemblyInformationalVersionAttribute</c> (any <c>+sha</c> suffix is stripped).
/// </param>
/// <param name="Environment">
/// Hosting environment name as exposed by <c>IHostEnvironment.EnvironmentName</c>
/// (<c>Development</c>, <c>Staging</c>, <c>Production</c>, ...).
/// </param>
public sealed record VersionResponse(string Name, string Version, string Environment);
