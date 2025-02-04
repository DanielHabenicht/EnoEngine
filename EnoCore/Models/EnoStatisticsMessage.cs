﻿namespace EnoCore.Models
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    public record EnoStatisticsMessage(
        string MessageType,
        string Timestamp);

    public record SubmissionBatchMessage(
        long FlagsProcessed,
        long OkFlags,
        long DuplicateFlags,
        long OldFlags,
        long Duration)
        : EnoStatisticsMessage(nameof(SubmissionBatchMessage), EnoCoreUtil.GetCurrentTimestamp());

    public record CheckerTaskLaunchMessage(
        long RoundId,
        string ServiceName,
        string Method,
        long TaskIndex)
        : EnoStatisticsMessage(nameof(CheckerTaskLaunchMessage), EnoCoreUtil.GetCurrentTimestamp())
    {
        public static CheckerTaskLaunchMessage FromCheckerTask(CheckerTask task)
        {
            return new(
                task.CurrentRoundId,
                task.ServiceName,
                task.Method.ToString(),
                task.UniqueVariantId);
        }
    }

    public record CheckerTaskFinishedMessage(
        long RoundId,
        string ServiceName,
        string Metho,
        long TaskIndex,
        double Duration,
        string Result)
        : EnoStatisticsMessage(nameof(CheckerTaskFinishedMessage), EnoCoreUtil.GetCurrentTimestamp())
    {
        public static CheckerTaskFinishedMessage FromCheckerTask(CheckerTask task)
        {
            return new(
                task.CurrentRoundId,
                task.ServiceName,
                task.Method.ToString(),
                task.UniqueVariantId,
                (DateTime.UtcNow - task.StartTime).TotalSeconds,
                task.CheckerResult.ToString());
        }
    }

    public record CheckerTaskAggregateMessage(
        long RoundId,
        long Time)
        : EnoStatisticsMessage(nameof(CheckerTaskAggregateMessage), EnoCoreUtil.GetCurrentTimestamp());

    public record TeamFlagSubmissionStatisticMessage(
        string TeamName,
        long TeamId,
        long OkFlags,
        long DuplicateFlags,
        long OldFlags,
        long InvalidFlags,
        long OwnFlags)
        : EnoStatisticsMessage(nameof(TeamFlagSubmissionStatisticMessage), EnoCoreUtil.GetCurrentTimestamp());
}
