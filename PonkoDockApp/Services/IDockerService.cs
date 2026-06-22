using Docker.DotNet.Models;

namespace PonkoDockApp.Services;

public interface IDockerService
{
    Task<List<ContainerListResponse>> ListContainersAsync(bool all = true);
    Task StartContainerAsync(string id);
    Task StopContainerAsync(string id);
    Task RestartContainerAsync(string id);
    Task RecreateContainerAsync(string id);
    Task PullImageAsync(string image);
    Task<ContainerInspectResponse> InspectContainerAsync(string id);
    Task StreamLogsAsync(string id, Func<string, Task> onLogReceived, CancellationToken ct);
}