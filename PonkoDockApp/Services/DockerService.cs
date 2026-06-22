using System.Text;
using Docker.DotNet;
using Docker.DotNet.Models;

namespace PonkoDockApp.Services
{
    public class DockerService : IDockerService
    {
        private readonly DockerClient client;

        public DockerService(IConfiguration configuration)
        {
            var dockerUri = configuration["Docker:Uri"];
            if (string.IsNullOrEmpty(dockerUri))
            {
                dockerUri = OperatingSystem.IsWindows() 
                    ? "npipe:////./pipe/docker_engine" 
                    : "unix:///var/run/docker.sock";
            }
            
            // Ensure we use the correct format for Unix sockets in Linux containers
            if (!OperatingSystem.IsWindows() && dockerUri.StartsWith("unix://") && !dockerUri.StartsWith("unix:///"))
            {
                dockerUri = "/" + dockerUri;
            }

            client = new DockerClientConfiguration(new Uri(dockerUri)).CreateClient();
        }

        public async Task<List<ContainerListResponse>> ListContainersAsync(bool all = true)
        {
            var containers = await client.Containers.ListContainersAsync(new ContainersListParameters { All = all });
            return containers.ToList();
        }

        public async Task StartContainerAsync(string id)
        {
            await client.Containers.StartContainerAsync(id, null);
        }

        public async Task StopContainerAsync(string id)
        {
            await client.Containers.StopContainerAsync(id, new ContainerStopParameters());
        }

        public async Task RestartContainerAsync(string id)
        {
            await client.Containers.RestartContainerAsync(id, new ContainerRestartParameters());
        }

        public async Task RemoveContainerAsync(string id, bool force = true)
        {
            await client.Containers.RemoveContainerAsync(id, new ContainerRemoveParameters { Force = force });
        }

        public async Task RecreateContainerAsync(string id)
        {
            var inspect = await InspectContainerAsync(id);
            dynamic inspectDyn = inspect;
            var name = inspectDyn.Name;
            var image = inspectDyn.Image;

            await StopContainerAsync(id);
            await RemoveContainerAsync(id);

            var createParams = new CreateContainerParameters
            {
                Image = image,
                Name = name.TrimStart('/'),
            };

            var response = await client.Containers.CreateContainerAsync(createParams);
            await StartContainerAsync(response.ID);
        }

        public async Task PullImageAsync(string image)
        {
            await client.Images.CreateImageAsync(
                new ImagesCreateParameters { FromImage = image },
                null,
                new Progress<JSONMessage>(m => Console.WriteLine(m.Status))
            );
        }

        public async Task<ContainerInspectResponse> InspectContainerAsync(string id)
        {
            return await client.Containers.InspectContainerAsync(id);
        }

        public async Task StreamLogsAsync(string id, Func<string, Task> onLogReceived, CancellationToken ct)
        {
            var stream = await client.Containers.GetContainerLogsAsync(id, true, new ContainerLogsParameters
            {
                ShowStdout = true,
                ShowStderr = true,
                Follow = true
            }, ct);

            try
            {
                await stream.CopyOutputToAsync(
                    Stream.Null,
                    new CallbackStream(onLogReceived, ""),
                    new CallbackStream(onLogReceived, "[ERR] "),
                    ct
                );
            }
            catch (OperationCanceledException) { }
            finally
            {
                stream.Dispose();
            }
        }

        private class CallbackStream(Func<string, Task> onLog, string prefix) : Stream
        {
            public override void Write(byte[] buffer, int offset, int count)
            {
                var message = Encoding.UTF8.GetString(buffer, offset, count);
                _ = onLog(prefix + message);
            }

            public override bool CanRead => false;
            public override bool CanSeek => false;
            public override bool CanWrite => true;
            public override long Length => 0;
            public override long Position { get; set; }
            public override void Flush() { }
            public override int Read(byte[] buffer, int offset, int count) => 0;
            public override long Seek(long offset, SeekOrigin origin) => 0;
            public override void SetLength(long value) { }
        }

    }
}

