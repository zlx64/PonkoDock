using System.Threading.Channels;
using System.Text;
using Docker.DotNet;
using Docker.DotNet.Models;
using Microsoft.Extensions.Logging;

namespace PonkoDockApp.Services
{
    public class DockerService : IDockerService
    {
        private readonly DockerClient client;
        private readonly ILogger<DockerService> logger;

        public DockerService(IConfiguration configuration, ILogger<DockerService> logger)
        {
            this.logger = logger;
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
            logger.LogInformation("Listing containers (all: {All})", all);
            var containers = await client.Containers.ListContainersAsync(new ContainersListParameters { All = all });
            return containers.ToList();
        }
        
        public async Task StartContainerAsync(string id)
        {
            logger.LogInformation("Starting container {Id}", id);
            await client.Containers.StartContainerAsync(id, null);
        }
        
        public async Task StopContainerAsync(string id)
        {
            logger.LogInformation("Stopping container {Id}", id);
            await client.Containers.StopContainerAsync(id, new ContainerStopParameters());
        }
        
        public async Task RestartContainerAsync(string id)
        {
            logger.LogInformation("Restarting container {Id}", id);
            await client.Containers.RestartContainerAsync(id, new ContainerRestartParameters());
        }
        
        public async Task RemoveContainerAsync(string id, bool force = true)
        {
            logger.LogInformation("Removing container {Id} (force: {Force})", id, force);
            await client.Containers.RemoveContainerAsync(id, new ContainerRemoveParameters { Force = force });
        }

        public async Task RecreateContainerAsync(string id)
        {
            logger.LogInformation("Recreating container {Id}", id);
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
            logger.LogInformation("Pulling image {Image}", image);
            await client.Images.CreateImageAsync(
                new ImagesCreateParameters { FromImage = image },
                null,
                new Progress<JSONMessage>(m => Console.WriteLine(m.Status))
            );
        }
        
        public async Task<ContainerInspectResponse> InspectContainerAsync(string id)
        {
            logger.LogInformation("Inspecting container {Id}", id);
            return await client.Containers.InspectContainerAsync(id);
        }
        
        public async IAsyncEnumerable<LogEntry> GetContainerLogsAsync(string id, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
        {
            logger.LogInformation("Streaming logs for container {Id}", id);
            var channel = Channel.CreateUnbounded<LogEntry>();
            
            var stream = await client.Containers.GetContainerLogsAsync(id, false, new ContainerLogsParameters
            {
                ShowStdout = true,
                ShowStderr = true,
                Follow = true,
                Tail = "50"
            }, ct);

            _ = Task.Run(async () =>
            {
                try
                {
                    await stream.CopyOutputToAsync(
                        Stream.Null,
                        new CallbackStream(channel.Writer, "", false),
                        new CallbackStream(channel.Writer, "", true),
                        ct
                    );
                }
                catch (OperationCanceledException) { }
                catch (Exception ex) { Console.WriteLine($"Log stream error: {ex.Message}"); }
                finally
                {
                    stream.Dispose();
                    channel.Writer.Complete();
                }
            }, ct);

            await foreach (var log in channel.Reader.ReadAllAsync(ct))
            {
                yield return log;
            }
        }

        private class CallbackStream(ChannelWriter<LogEntry> writer, string prefix, bool isError) : Stream
        {
            public override void Write(byte[] buffer, int offset, int count)
            {
                var message = Encoding.UTF8.GetString(buffer, offset, count);
                _ = writer.WriteAsync(new LogEntry(prefix + message, isError));
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

