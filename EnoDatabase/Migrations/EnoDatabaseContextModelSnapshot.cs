﻿// <auto-generated />
using System;
using EnoDatabase;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

namespace EnoDatabase.Migrations
{
    [DbContext(typeof(EnoDatabaseContext))]
    partial class EnoDatabaseContextModelSnapshot : ModelSnapshot
    {
        protected override void BuildModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn)
                .HasAnnotation("ProductVersion", "3.0.0")
                .HasAnnotation("Relational:MaxIdentifierLength", 63);

            modelBuilder.Entity("EnoCore.Models.Database.CheckerTask", b =>
                {
                    b.Property<long>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("bigint")
                        .HasAnnotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);

                    b.Property<string>("Address")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<int>("CheckerResult")
                        .HasColumnType("integer");

                    b.Property<int>("CheckerTaskLaunchStatus")
                        .HasColumnType("integer");

                    b.Property<string>("CheckerUrl")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<long>("CurrentRoundId")
                        .HasColumnType("bigint");

                    b.Property<string>("ErrorMessage")
                        .HasColumnType("text");

                    b.Property<int>("MaxRunningTime")
                        .HasColumnType("integer");

                    b.Property<int>("Method")
                        .HasColumnType("integer");

                    b.Property<string>("Payload")
                        .HasColumnType("text");

                    b.Property<long>("RelatedRoundId")
                        .HasColumnType("bigint");

                    b.Property<long>("RoundLength")
                        .HasColumnType("bigint");

                    b.Property<long>("ServiceId")
                        .HasColumnType("bigint");

                    b.Property<string>("ServiceName")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<DateTime>("StartTime")
                        .HasColumnType("timestamp without time zone");

                    b.Property<long>("TaskIndex")
                        .HasColumnType("bigint");

                    b.Property<long>("TeamId")
                        .HasColumnType("bigint");

                    b.Property<string>("TeamName")
                        .IsRequired()
                        .HasColumnType("text");

                    b.HasKey("Id");

                    b.HasIndex("TeamId");

                    b.HasIndex("CheckerTaskLaunchStatus", "StartTime");

                    b.HasIndex("CurrentRoundId", "RelatedRoundId", "CheckerResult");

                    b.ToTable("CheckerTasks");
                });

            modelBuilder.Entity("EnoCore.Models.Database.Round", b =>
                {
                    b.Property<long>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("bigint")
                        .HasAnnotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);

                    b.Property<DateTime>("Begin")
                        .HasColumnType("timestamp without time zone");

                    b.Property<DateTime>("End")
                        .HasColumnType("timestamp without time zone");

                    b.Property<DateTime>("Quarter2")
                        .HasColumnType("timestamp without time zone");

                    b.Property<DateTime>("Quarter3")
                        .HasColumnType("timestamp without time zone");

                    b.Property<DateTime>("Quarter4")
                        .HasColumnType("timestamp without time zone");

                    b.HasKey("Id");

                    b.ToTable("Rounds");
                });

            modelBuilder.Entity("EnoCore.Models.Database.RoundTeamServiceState", b =>
                {
                    b.Property<long>("ServiceId")
                        .HasColumnType("bigint");

                    b.Property<long>("TeamId")
                        .HasColumnType("bigint");

                    b.Property<long>("GameRoundId")
                        .HasColumnType("bigint");

                    b.Property<string>("ErrorMessage")
                        .HasColumnType("text");

                    b.Property<int>("Status")
                        .HasColumnType("integer");

                    b.HasKey("ServiceId", "TeamId", "GameRoundId");

                    b.HasIndex("GameRoundId");

                    b.HasIndex("TeamId");

                    b.ToTable("RoundTeamServiceStates");
                });

            modelBuilder.Entity("EnoCore.Models.Database.Service", b =>
                {
                    b.Property<long>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("bigint")
                        .HasAnnotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);

                    b.Property<bool>("Active")
                        .HasColumnType("boolean");

                    b.Property<long>("FlagStores")
                        .HasColumnType("bigint");

                    b.Property<long>("FlagsPerRound")
                        .HasColumnType("bigint");

                    b.Property<long>("HavocsPerRound")
                        .HasColumnType("bigint");

                    b.Property<string>("Name")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<long>("NoisesPerRound")
                        .HasColumnType("bigint");

                    b.HasKey("Id");

                    b.ToTable("Services");
                });

            modelBuilder.Entity("EnoCore.Models.Database.ServiceStats", b =>
                {
                    b.Property<long>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("bigint")
                        .HasAnnotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);

                    b.Property<double>("AttackPoints")
                        .HasColumnType("double precision");

                    b.Property<string>("ErrorMessage")
                        .HasColumnType("text");

                    b.Property<double>("LostDefensePoints")
                        .HasColumnType("double precision");

                    b.Property<long>("ServiceId")
                        .HasColumnType("bigint");

                    b.Property<double>("ServiceLevelAgreementPoints")
                        .HasColumnType("double precision");

                    b.Property<int>("Status")
                        .HasColumnType("integer");

                    b.Property<long>("TeamId")
                        .HasColumnType("bigint");

                    b.HasKey("Id");

                    b.HasIndex("ServiceId");

                    b.HasIndex("TeamId");

                    b.ToTable("ServiceStats");
                });

            modelBuilder.Entity("EnoCore.Models.Database.ServiceStatsSnapshot", b =>
                {
                    b.Property<long>("ServiceId")
                        .HasColumnType("bigint");

                    b.Property<long>("RoundId")
                        .HasColumnType("bigint");

                    b.Property<long>("TeamId")
                        .HasColumnType("bigint");

                    b.Property<double>("AttackPoints")
                        .HasColumnType("double precision");

                    b.Property<double>("LostDefensePoints")
                        .HasColumnType("double precision");

                    b.Property<double>("ServiceLevelAgreementPoints")
                        .HasColumnType("double precision");

                    b.HasKey("ServiceId", "RoundId", "TeamId");

                    b.HasIndex("RoundId");

                    b.HasIndex("TeamId");

                    b.ToTable("ServiceStatsSnapshots");
                });

            modelBuilder.Entity("EnoCore.Models.Database.SubmittedFlag", b =>
                {
                    b.Property<long>("FlagServiceId")
                        .HasColumnType("bigint");

                    b.Property<long>("FlagRoundId")
                        .HasColumnType("bigint");

                    b.Property<long>("FlagOwnerId")
                        .HasColumnType("bigint");

                    b.Property<int>("FlagRoundOffset")
                        .HasColumnType("integer");

                    b.Property<long>("AttackerTeamId")
                        .HasColumnType("bigint");

                    b.Property<long>("RoundId")
                        .HasColumnType("bigint");

                    b.Property<long>("SubmissionsCount")
                        .HasColumnType("bigint");

                    b.Property<DateTime>("Timestamp")
                        .HasColumnType("timestamp without time zone");

                    b.HasKey("FlagServiceId", "FlagRoundId", "FlagOwnerId", "FlagRoundOffset", "AttackerTeamId");

                    b.HasIndex("AttackerTeamId");

                    b.HasIndex("RoundId");

                    b.HasIndex("FlagServiceId", "FlagRoundOffset", "Timestamp");

                    b.ToTable("SubmittedFlags");
                });

            modelBuilder.Entity("EnoCore.Models.Database.Team", b =>
                {
                    b.Property<long>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("bigint")
                        .HasAnnotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);

                    b.Property<bool>("Active")
                        .HasColumnType("boolean");

                    b.Property<string>("Address")
                        .HasColumnType("text");

                    b.Property<double>("AttackPoints")
                        .HasColumnType("double precision");

                    b.Property<double>("LostDefensePoints")
                        .HasColumnType("double precision");

                    b.Property<string>("Name")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<double>("ServiceLevelAgreementPoints")
                        .HasColumnType("double precision");

                    b.Property<long>("ServiceStatsId")
                        .HasColumnType("bigint");

                    b.Property<string>("TeamSubnet")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<double>("TotalPoints")
                        .HasColumnType("double precision");

                    b.HasKey("Id");

                    b.ToTable("Teams");
                });

            modelBuilder.Entity("EnoCore.Models.Database.CheckerTask", b =>
                {
                    b.HasOne("EnoCore.Models.Database.Team", null)
                        .WithMany("CheckerTasks")
                        .HasForeignKey("TeamId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();
                });

            modelBuilder.Entity("EnoCore.Models.Database.RoundTeamServiceState", b =>
                {
                    b.HasOne("EnoCore.Models.Database.Round", "GameRound")
                        .WithMany()
                        .HasForeignKey("GameRoundId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.HasOne("EnoCore.Models.Database.Service", "Service")
                        .WithMany()
                        .HasForeignKey("ServiceId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.HasOne("EnoCore.Models.Database.Team", "Team")
                        .WithMany("ServiceDetails")
                        .HasForeignKey("TeamId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();
                });

            modelBuilder.Entity("EnoCore.Models.Database.ServiceStats", b =>
                {
                    b.HasOne("EnoCore.Models.Database.Service", "Service")
                        .WithMany()
                        .HasForeignKey("ServiceId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.HasOne("EnoCore.Models.Database.Team", "Team")
                        .WithMany("ServiceStats")
                        .HasForeignKey("TeamId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();
                });

            modelBuilder.Entity("EnoCore.Models.Database.ServiceStatsSnapshot", b =>
                {
                    b.HasOne("EnoCore.Models.Database.Round", "Round")
                        .WithMany()
                        .HasForeignKey("RoundId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.HasOne("EnoCore.Models.Database.Service", "Service")
                        .WithMany()
                        .HasForeignKey("ServiceId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.HasOne("EnoCore.Models.Database.Team", "Team")
                        .WithMany()
                        .HasForeignKey("TeamId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();
                });

            modelBuilder.Entity("EnoCore.Models.Database.SubmittedFlag", b =>
                {
                    b.HasOne("EnoCore.Models.Database.Team", "AttackerTeam")
                        .WithMany()
                        .HasForeignKey("AttackerTeamId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.HasOne("EnoCore.Models.Database.Round", "Round")
                        .WithMany()
                        .HasForeignKey("RoundId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();
                });
#pragma warning restore 612, 618
        }
    }
}
