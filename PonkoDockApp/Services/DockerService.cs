using Docker.DotNet;
using Docker.DotNet.Models;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using Microsoft.Extensions.Configuration;
using System.IO;
using System.Text;
using System.Collections.ObjectModel;
using System.Threading;

namespace PonkoDockApp.Services
{
    public interface IDockerService
    {
        Task<List<ContainerListResponse>> ListContainersAsync(bool all = true);
        Task StartContainerAsync(string id);
        Task StopContainerAsync(string id);
        Task RestartContainerAsync(string id);
        Task RemoveContainerAsync(string id, bool force = true);
        Task RecreateContainerAsync(string id);
        Task PullImageAsync(string image);
        Task<ContainerInspectResponse> InspectContainerAsync(string id);
        Task<ContainerStatsResponse> GetContainerStatsAsync(string id);
        Task<string> GetContainerLogsAsync(string id);
        Task StreamLogsAsync(string id, Func<string, Task> onLogReceived, CancellationToken ct);
    }

    public class DockerService : IDockerService
    {
        private readonly DockerClient _client;

        public DockerService(IConfiguration configuration)
        {
            var dockerUri = configuration["Docker:Uri"];
            if (string.IsNullOrEmpty(dockerUri))
            {
                dockerUri = System.OperatingSystem.IsWindows() 
                    ? "npipe:////./pipe/docker_engine" 
                    : "unix:///var/run/docker.sock";
            }
            
            // Ensure we use the correct format for Unix sockets in Linux containers
            if (!System.OperatingSystem.IsWindows() && dockerUri.StartsWith("unix://") && !dockerUri.StartsWith("unix:///"))
            {
                dockerUri = "/" + dockerUri;
            }

            _client = new DockerClientConfiguration(new Uri(dockerUri)).CreateClient();
        }

        public async Task<List<ContainerListResponse>> ListContainersAsync(bool all = true)
        {
            var containers = await _client.Containers.ListContainersAsync(new ContainersListParameters { All = all });
            return containers.ToList();
        }

        public async Task StartContainerAsync(string id)
        {
            await _client.Containers.StartContainerAsync(id, null);
        }

        public async Task StopContainerAsync(string id)
        {
            await _client.Containers.StopContainerAsync(id, new ContainerStopParameters());
        }

        public async Task RestartContainerAsync(string id)
        {
            await _client.Containers.RestartContainerAsync(id, new ContainerRestartParameters());
        }

        public async Task RemoveContainerAsync(string id, bool force = true)
        {
            await _client.Containers.RemoveContainerAsync(id, new ContainerRemoveParameters { Force = force });
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

            var response = await _client.Containers.CreateContainerAsync(createParams);
            await StartContainerAsync(response.ID);
        }

        public async Task PullImageAsync(string image)
        {
            await _client.Images.CreateImageAsync(
                new ImagesCreateParameters { FromImage = image },
                null,
                new Progress<JSONMessage>(m => Console.WriteLine(m.Status))
            );
        }

        public async Task<ContainerInspectResponse> InspectContainerAsync(string id)
        {
            return await _client.Containers.InspectContainerAsync(id);
        }

        public async Task<ContainerStatsResponse> GetContainerStatsAsync(string id)
        {
            using var stream = await _client.Containers.GetContainerStatsAsync(id, new ContainerStatsParameters { Stream = false }, CancellationToken.None);
            using var reader = new StreamReader(stream);
            var json = await reader.ReadToEndAsync();
            return System.Text.Json.JsonSerializer.Deserialize<ContainerStatsResponse>(json);
        }

        public async Task<string> GetContainerLogsAsync(string id)
        {
            var stream = await _client.Containers.GetContainerLogsAsync(id, true, new ContainerLogsParameters
            {
                ShowStdout = true,
                ShowStderr = true,
                Tail = "100"
            });
            
            using var stdoutMs = new MemoryStream();
            using var stderrMs = new MemoryStream();
            
            await stream.CopyOutputToAsync(Stream.Null, stdoutMs, stderrMs, CancellationToken.None);
            
            string stdout = Encoding.UTF8.GetString(stdoutMs.ToArray());
            string stderr = Encoding.UTF8.GetString(stderrMs.ToArray());
            
            return stdout + stderr;
        }

        public async Task StreamLogsAsync(string id, Func<string, Task> onLogReceived, CancellationToken ct)
        {
            var stream = await _client.Containers.GetContainerLogsAsync(id, true, new ContainerLogsParameters
            {
                ShowStdout = true,
                ShowStderr = true,
                Follow = true
            });

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

        private class CallbackStream : Stream
        {
            private readonly Func<string, Task> _onLog;
            private readonly string _prefix;

            public CallbackStream(Func<string, Task> onLog, string prefix)
            {
                _onLog = onLog;
                _prefix = prefix;
            }

            public override void Write(byte[] buffer, int offset, int count)
            {
                var message = Encoding.UTF8.GetString(buffer, offset, count);
                _ = _onLog(_prefix + message);
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

