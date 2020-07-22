﻿using EnoCore;
using EnoCore.Logging;
using EnoCore.Models;
using EnoCore.Models.Database;
using EnoCore.Models.Json;
using EnoDatabase;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using EnoCore.Utils;
using System.IO;
using System.IO.Pipelines;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace EnoEngine.FlagSubmission
{
    class FlagSubmissionEndpoint
    {
        private readonly Dictionary<long, Channel<(Flag Flag, TaskCompletionSource<FlagSubmissionResult> FeedbackSource)>> Channels =
            new Dictionary<long, Channel<(Flag, TaskCompletionSource<FlagSubmissionResult>)>>();
        private readonly Dictionary<long, TeamFlagSubmissionStatistic> SubmissionStatistics = new Dictionary<long, TeamFlagSubmissionStatistic>();
        private const int MAX_LIME_LENGTH = 200;
        private const int SUBMISSION_BATCH_SIZE = 500;
        private const int SUBMISSION_BATCH_PARALLELIZATION = 4;
        private readonly TcpListener ProductionListener = new TcpListener(IPAddress.IPv6Any, 1337);
        private readonly TcpListener DebugListener = new TcpListener(IPAddress.IPv6Any, 1338);
        private readonly ILogger Logger;
        private readonly JsonConfiguration Configuration;
        private readonly IServiceProvider ServiceProvider;
        private readonly EnoStatistics EnoStatistics;

        public FlagSubmissionEndpoint(IServiceProvider serviceProvider, ILogger<FlagSubmissionEndpoint> logger, JsonConfiguration configuration, EnoStatistics statistics)
        {
            Logger = logger;
            Configuration = configuration;
            EnoStatistics = statistics;
            ProductionListener.Server.SetSocketOption(SocketOptionLevel.IPv6, SocketOptionName.IPv6Only, false);
            DebugListener.Server.SetSocketOption(SocketOptionLevel.IPv6, SocketOptionName.IPv6Only, false);
            ServiceProvider = serviceProvider;
            foreach (var team in configuration.Teams)
            {
                Channels[team.Id] = Channel.CreateBounded<(Flag, TaskCompletionSource<FlagSubmissionResult>)>(new BoundedChannelOptions(100) { SingleReader = false, SingleWriter = false });
                SubmissionStatistics[team.Id] = new TeamFlagSubmissionStatistic(team.Id);
            }
        }

        public async Task LogSubmissionStatistics(long teamId, string teamName, CancellationToken token)
        {
            var statistic = SubmissionStatistics[teamId];
            while (!token.IsCancellationRequested)
            {
                var okFlags = Interlocked.Exchange(ref statistic.OkFlags, 0);
                var oldFlags = Interlocked.Exchange(ref statistic.OldFlags, 0);
                var ownFlags = Interlocked.Exchange(ref statistic.OwnFlags, 0);
                var duplicateFlags = Interlocked.Exchange(ref statistic.DuplicateFlags, 0);
                var invalidFlags = Interlocked.Exchange(ref statistic.InvalidFlags, 0);
                EnoStatistics.FlagSubmissionStatisticsMessage(teamName, teamId, okFlags, duplicateFlags, oldFlags, invalidFlags, ownFlags);
                await Task.Delay(5000);
            }
        }

        public async Task Start(CancellationToken token, JsonConfiguration config)
        {
            token.Register(() => ProductionListener.Stop());
            token.Register(() => DebugListener.Stop());
            foreach (var team in SubmissionStatistics)
            {
                var _ = Task.Run(async () => await LogSubmissionStatistics(team.Key, config.Teams.Where(t => t.Id == team.Key).First().Name, token));
            }
            var tasks = new List<Task>();
            for (int i=0;i< SUBMISSION_BATCH_PARALLELIZATION; i++) tasks.Add(await Task.Factory.StartNew(async () => await InsertSubmissionsLoop(i, token), token, TaskCreationOptions.RunContinuationsAsynchronously, TaskScheduler.Default));
            tasks.Add(await Task.Factory.StartNew(async () => await RunProductionEndpoint(config, token), token, TaskCreationOptions.RunContinuationsAsynchronously, TaskScheduler.Default));
            tasks.Add(await Task.Factory.StartNew(async () => await RunDebugEndpoint(config, token), token, TaskCreationOptions.RunContinuationsAsynchronously, TaskScheduler.Default));
            await Task.WhenAny(tasks);
        }

        async Task ProcessLinesAsync(Socket socket, long? teamId, JsonConfiguration config, CancellationToken token)
        {
            var pipe = new Pipe();
            Channel<Task<FlagSubmissionResult>> feedbackChannel = Channel.CreateUnbounded<Task<FlagSubmissionResult>>(new UnboundedChannelOptions() { SingleReader = true, SingleWriter = false });
            Task writing = FillPipeAsync(socket, pipe.Writer, token);
            Task reading = ReadPipeAsync(pipe.Reader, feedbackChannel.Writer, teamId, config, token);
            Task responding = RespondAsync(socket, teamId, feedbackChannel.Reader, token);
            await Task.WhenAll(reading, writing, responding);
            socket.Close();
        }

        async Task FillPipeAsync(Socket socket, PipeWriter writer, CancellationToken token)
        {
            const int minimumBufferSize = 512;
            while (true)
            {
                // Allocate at least 512 bytes from the PipeWriter.
                Memory<byte> memory = writer.GetMemory(minimumBufferSize);
                try
                {
                    int bytesRead = await socket.ReceiveAsync(memory, SocketFlags.None, token);
                    if (bytesRead == 0)
                    {
                        break;
                    }
                    // Tell the PipeWriter how much was read from the Socket.
                    writer.Advance(bytesRead);
                }
                catch (Exception ex)
                {
                    Logger.LogDebug($"FillPipeAsync failed: {ex.Message}\n{ex.StackTrace}");
                    break;
                }

                // Make the data available to the PipeReader.
                FlushResult result = await writer.FlushAsync(token);

                if (result.IsCompleted)
                {
                    break;
                }
            }

            // By completing PipeWriter, tell the PipeReader that there's no more data coming.
            await writer.CompleteAsync();
        }

        async Task ReadPipeAsync(PipeReader reader, ChannelWriter<Task<FlagSubmissionResult>> feedbackWriter, long? teamId, JsonConfiguration config, CancellationToken token)
        {
            while (true)
            {
                ReadResult result = await reader.ReadAsync(token);
                ReadOnlySequence<byte> buffer = result.Buffer;

                while (TryReadLine(ref buffer, out ReadOnlySequence<byte> line))
                {
                    // Process the line.
                    if (teamId is long _teamId)
                    {
                        var flag = Flag.Parse(line, Encoding.ASCII.GetBytes(config.FlagSigningKey), config.Encoding, Logger);
                        var tcs = new TaskCompletionSource<FlagSubmissionResult>();
                        if (flag == null)
                        {
                            await feedbackWriter.WriteAsync(Task.FromResult(FlagSubmissionResult.Invalid));
                        }
                        else if (flag.OwnerId == _teamId)
                        {
                            await feedbackWriter.WriteAsync(Task.FromResult(FlagSubmissionResult.Own));
                        }
                        else
                        {
                            var channel = Channels[_teamId];
                            await channel.Writer.WriteAsync((flag, tcs));
                            await feedbackWriter.WriteAsync(tcs.Task);
                        }
                    }
                    else
                    {
                        teamId = int.Parse(Encoding.ASCII.GetString(line.ToArray()));
                    }
                }

                // Tell the PipeReader how much of the buffer has been consumed.
                reader.AdvanceTo(buffer.Start, buffer.End);

                // Stop reading if there's no more data coming.
                if (result.IsCompleted)
                {
                    break;
                }

                // TryReadLine has returned false, so the remaining buffer does not contain a \n.
                // If the length is longer than a flag, somebody is sending bullshit!
                if (buffer.Length > MAX_LIME_LENGTH)
                {
                    await feedbackWriter.WriteAsync(Task.FromResult(FlagSubmissionResult.SpamError));
                    break;
                }
            }
            // Mark the PipeReader as complete.
            await reader.CompleteAsync();
            // Mark the Channel as complete
            feedbackWriter.Complete();
        }

        private async Task RespondAsync(Socket socket, long? teamId, ChannelReader<Task<FlagSubmissionResult>> feedbackReader, CancellationToken token)
        {
            TeamFlagSubmissionStatistic? statistic = null;
            if (teamId != null)
                statistic = SubmissionStatistics[teamId.Value];
            while (await feedbackReader.WaitToReadAsync(token))
            {
                while (feedbackReader.TryRead(out var itemTask))
                {
                    var item = await itemTask;
                    if (statistic != null)
                    {
                        switch (item)
                        {
                            case FlagSubmissionResult.Ok:
                                Interlocked.Increment(ref statistic.OkFlags);
                                break;
                            case FlagSubmissionResult.Duplicate:
                                Interlocked.Increment(ref statistic.DuplicateFlags);
                                break;
                            case FlagSubmissionResult.Invalid:
                                Interlocked.Increment(ref statistic.InvalidFlags);
                                break;
                            case FlagSubmissionResult.Old:
                                Interlocked.Increment(ref statistic.OldFlags);
                                break;
                            case FlagSubmissionResult.Own:
                                Interlocked.Increment(ref statistic.OwnFlags);
                                break;
                        }
                    }
                    var itemBytes = Encoding.ASCII.GetBytes(FormatSubmissionResult(item)); //TODO don't serialize every time
                    await socket.SendAsync(itemBytes, SocketFlags.None, token);  //TODO enforce batching
                    if (item == FlagSubmissionResult.SpamError)
                    {
                        // https://blog.netherlabs.nl/articles/2009/01/18/the-ultimate-so_linger-page-or-why-is-my-tcp-not-reliable
                        await Task.Delay(1000);
                        socket.Close();
                        break;
                    }
                }
            }
        }

        bool TryReadLine(ref ReadOnlySequence<byte> buffer, out ReadOnlySequence<byte> line)
        {
            // Look for a EOL in the buffer.
            SequencePosition? position = buffer.PositionOf((byte)'\n');

            if (position == null)
            {
                line = default;
                return false;
            }

            // Skip the line + the \n.
            line = buffer.Slice(0, position.Value);
            buffer = buffer.Slice(buffer.GetPosition(1, position.Value));
            return true;
        }

        public async Task RunDebugEndpoint(JsonConfiguration config, CancellationToken token)
        {
            Logger.LogInformation($"{nameof(RunDebugEndpoint)} started");
            try
            {
                DebugListener.Start();
                while (!token.IsCancellationRequested)
                {
                    var client = await DebugListener.AcceptTcpClientAsync();
                    var _ = ProcessLinesAsync(client.Client, null, config, token);
                }
            }
            catch (ObjectDisposedException) { }
            catch (TaskCanceledException) { }
            catch (Exception e)
            {
                Logger.LogCritical($"RunDebugEndpoint failed: {EnoDatabaseUtils.FormatException(e)}");
            }
            Logger.LogInformation("RunDebugEndpoint finished");
        }

        public async Task RunProductionEndpoint(JsonConfiguration config, CancellationToken token)
        {
            Logger.LogInformation($"{nameof(RunProductionEndpoint)} started");
            try
            {
                ProductionListener.Start();
                while (!token.IsCancellationRequested)
                {
                    try
                    {
                        var client = await ProductionListener.AcceptTcpClientAsync();
                        var t = Task.Run(async () =>
                        {
                            var attackerAddress = ((IPEndPoint)client.Client.RemoteEndPoint).Address.GetAddressBytes();
                            var attackerPrefix = new byte[Configuration.TeamSubnetBytesLength];
                            Array.Copy(attackerAddress, attackerPrefix, Configuration.TeamSubnetBytesLength);
                            var attackerPrefixString = BitConverter.ToString(attackerPrefix);
                            Team? team;
                            using (var scope = ServiceProvider.CreateScope()) // limited scope to ensure the db context isn't held indefinitely during ProcessLinesAsync
                            {
                                var db = scope.ServiceProvider.GetRequiredService<IEnoDatabase>();
                                team = await db.GetTeamIdByPrefix(attackerPrefixString);
                            }
                            if (team != null)
                            {
                                await ProcessLinesAsync(client.Client, team.Id, config, token);
                            }
                            else
                            {
                                var itemBytes = Encoding.ASCII.GetBytes(FormatSubmissionResult(FlagSubmissionResult.InvalidSenderError)); //TODO don't serialize every time
                                await client.Client.SendAsync(itemBytes, SocketFlags.None, token);
                                client.Close();
                            }
                        });
                    }
                    catch (TaskCanceledException)
                    {
                        throw;
                    }
                    catch (Exception e)
                    {
                        Logger.LogWarning($"RunProductionEndpoint failed to accept connection: {EnoDatabaseUtils.FormatException(e)}");
                    }
                }
            }
            catch (ObjectDisposedException) { }
            catch (TaskCanceledException) { }
            catch (Exception e)
            {
                Logger.LogCritical($"RunProductionEndpoint failed: {EnoDatabaseUtils.FormatException(e)}");
            }
            Logger.LogInformation("RunProductionEndpoint finished");
        }

        private static string FormatSubmissionResult(FlagSubmissionResult result)
        {
            return result switch
            {
                FlagSubmissionResult.Ok => Misc.SubmissionResultOk,
                FlagSubmissionResult.Invalid => Misc.SubmissionResultInvalid,
                FlagSubmissionResult.Duplicate => Misc.SubmissionResultDuplicate,
                FlagSubmissionResult.Own => Misc.SubmissionResultOwn,
                FlagSubmissionResult.Old => Misc.SubmissionResultOld,
                FlagSubmissionResult.UnknownError => Misc.SubmissionResultUnknownError,
                FlagSubmissionResult.InvalidSenderError => Misc.SubmissionResultInvalidSenderError,
                FlagSubmissionResult.SpamError => Misc.SubmissionResultSpamError,
                _ => Misc.SubmissionResultReallyUnknownError,
            };
        }

        async Task InsertSubmissionsLoop(int number, CancellationToken token)
        {
            Logger.LogInformation($"{nameof(InsertSubmissionsLoop)} {number} started");
            try
            {
                while (!token.IsCancellationRequested)
                {
                    bool empty = true;
                    List<(Flag flag, long attackerTeamId, TaskCompletionSource<FlagSubmissionResult> result)> submissions = new List<(Flag flag, long attackerTeamId, TaskCompletionSource<FlagSubmissionResult>)>();
                    foreach (var (teamid, channel) in Channels)
                    {
                        int SubmissionsPerTeam = 0;
                        var reader = channel.Reader;
                        while (SubmissionsPerTeam < 100 && reader.TryRead(out var item))
                        {
                            empty = false;
                            SubmissionsPerTeam++;
                            submissions.Add((item.Flag, teamid, item.FeedbackSource));
                            if (submissions.Count> SUBMISSION_BATCH_SIZE)
                            {
                                try
                                {
                                    await EnoDatabaseUtils.RetryDatabaseAction(async () =>
                                    {
                                        using var scope = ServiceProvider.CreateScope();
                                        var db = scope.ServiceProvider.GetRequiredService<IEnoDatabase>();
                                        await db.ProcessSubmissionsBatch(submissions, Configuration.FlagValidityInRounds, EnoStatistics);
                                    });
                                }
                                catch (Exception e)
                                {
                                    Logger.LogError($"InsertSubmissionsLoop dropping batch because: {EnoDatabaseUtils.FormatException(e)}");
                                    foreach (var (flag, attackerTeamId, tcs) in submissions)
                                    {
                                        tcs.SetResult(FlagSubmissionResult.UnknownError);
                                    }
                                }
                                submissions.Clear();
                            }
                        }
                    }
                    if (empty)
                    {
                        await Task.Delay(10);
                    }
                    else if (submissions.Count!=0)
                    {
                        try
                        {
                            await EnoDatabaseUtils.RetryDatabaseAction(async () =>
                            {
                                using var scope = ServiceProvider.CreateScope();
                                var db = scope.ServiceProvider.GetRequiredService<IEnoDatabase>();
                                await db.ProcessSubmissionsBatch(submissions, Configuration.FlagValidityInRounds, EnoStatistics);
                            });
                        }
                        catch (Exception e)
                        {
                            Logger.LogError($"InsertSubmissionsLoop dropping batch because: {EnoDatabaseUtils.FormatException(e)}");
                            foreach (var (flag, attackerTeamId, tcs) in submissions)
                            {
                                tcs.SetResult(FlagSubmissionResult.UnknownError);
                            }
                        }
                    }
                }
            }
            catch (TaskCanceledException) { }
            catch (Exception e)
            {
                Logger.LogCritical($"InsertSubmissionsLoop failed: {EnoDatabaseUtils.FormatException(e)}");
            }
        }
    }
}