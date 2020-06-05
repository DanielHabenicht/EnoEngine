﻿using EnoCore;
using EnoCore.Models.Database;
using EnoCore.Models.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using EnoCore.Models;
using System.Linq;
using EnoDatabase;

namespace FlagShooter
{
    class Program
    {
        private static readonly CancellationTokenSource LauncherCancelSource = new CancellationTokenSource();
        private readonly ServiceProvider ServiceProvider;
        private readonly Dictionary<long, (TcpClient, StreamReader reader, StreamWriter writer)[]> TeamSockets =
            new Dictionary<long, (TcpClient, StreamReader reader, StreamWriter writer)[]>();
        private readonly long AttackingTeams = 50;
        private readonly int SubmissionConnectionsPerTeam = 100;

        public Program(ServiceProvider serviceProvider)
        {
            ServiceProvider = serviceProvider;
            using (var scope = ServiceProvider.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<IEnoDatabase>();
                db.Migrate();
            }
            for (int i = 0; i < AttackingTeams; i++)
            {
                TeamSockets[i+1] = new (TcpClient, StreamReader reader, StreamWriter writer)[SubmissionConnectionsPerTeam];
                for (int j = 0; j < SubmissionConnectionsPerTeam; j++)
                {
                    var tcpClient = new TcpClient();
                    tcpClient.Connect("localhost", 1338);
                    (TcpClient tcpClient, StreamReader, StreamWriter writer) client = (tcpClient, new StreamReader(tcpClient.GetStream()), new StreamWriter(tcpClient.GetStream()));
                    client.writer.AutoFlush = true;
                    client.writer.Write($"{i + 200}\n");
                    TeamSockets[i + 1][j] = client;
                }
            }
        }

        public void Start()
        {
            FlagRunnerLoop().Wait();
        }

        public async Task FlagRunnerLoop()
        {
            using (var scope = ServiceProvider.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<IEnoDatabase>();
                db.Migrate();
            }

            Console.WriteLine("$FlagRunnerLoop starting");

            var flagcount = 10000;
            while (!LauncherCancelSource.IsCancellationRequested)
            {
                try
                {
                    using var scope = ServiceProvider.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<IEnoDatabase>();
                    var flags = await db.RetrieveFlags(flagcount);
                    var tasks = new Task[AttackingTeams];
                    if (flags.Length > 0)
                    {
                        Console.WriteLine($"Sending {flags.Length} flags");
                    }
                    for (int i = 0; i < AttackingTeams; i++)
                    {
                        var ti = i;
                        tasks[ti] = Task.Run(async () => await SendFlagsTask(flags.Select(f => f.ToString()).ToArray(), ti + 1));
                    }
                    await Task.WhenAll(tasks);
                    await Task.Delay(1000, LauncherCancelSource.Token);
                }
                catch (Exception e)
                {
                    Console.WriteLine($"FlagRunnerLoop retrying because: {EnoDatabaseUtils.FormatException(e)}");
                }
            }
        }

        private async Task SendFlagsTask(string[] flags, long teamId)
        {
            try
            {
                var connections = TeamSockets[teamId];
                var tasks = new List<Task>(SubmissionConnectionsPerTeam);
                for (int i = 0; i < flags.Length; i+= SubmissionConnectionsPerTeam)
                {
                    for (int j = 0; j < SubmissionConnectionsPerTeam; j++)
                    {
                        if (i + j < flags.Length)
                        {
                            var con = connections[j];
                            int ti = i;
                            int tj = j;
                            tasks.Add(Task.Run(async () =>
                            {
                                await con.writer.WriteAsync($"{flags[ti + tj]}\n");
                                await con.reader.ReadLineAsync();
                            }));
                        }
                    }
                    await Task.WhenAll(tasks);
                    tasks.Clear();
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"SendFlagTask failed because: {EnoDatabaseUtils.FormatException(e)}");
            }
        }


        static void Main(string[] args)
        {
            try
            {
                Console.WriteLine($"FlagShooter starting");
                var serviceProvider = new ServiceCollection()
                    .AddDbContextPool<EnoDatabaseContext>(options => {
                        options.UseNpgsql(
                            EnoDatabaseUtils.PostgresConnectionString,
                            pgoptions => pgoptions.EnableRetryOnFailure());
                    }, 2)
                    .AddScoped<IEnoDatabase, EnoDatabase.EnoDatabase>()
                    .AddLogging(logging => logging.AddConsole())
                    .BuildServiceProvider(validateScopes: true);
                new Program(serviceProvider).Start();
            }
            catch (Exception e)
            {
                Console.WriteLine($"FlagShooter failed: {EnoDatabaseUtils.FormatException(e)}");
            }
        }
    }
}
