using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace banzai_server.Models;

public partial class BanzaiDbContext : DbContext
{
    public BanzaiDbContext(DbContextOptions<BanzaiDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<Cache> Caches { get; set; }

    public virtual DbSet<CacheLock> CacheLocks { get; set; }

    public virtual DbSet<ChatChannel> ChatChannels { get; set; }

    public virtual DbSet<ChatMessage> ChatMessages { get; set; }

    public virtual DbSet<FailedJob> FailedJobs { get; set; }

    public virtual DbSet<Job> Jobs { get; set; }

    public virtual DbSet<JobBatch> JobBatches { get; set; }

    public virtual DbSet<Migration> Migrations { get; set; }

    public virtual DbSet<PasswordResetToken> PasswordResetTokens { get; set; }

    public virtual DbSet<Session> Sessions { get; set; }

    public virtual DbSet<User> Users { get; set; }

    public virtual DbSet<UserStat> UserStats { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Cache>(entity =>
        {
            entity.HasKey(e => e.Key).HasName("cache_pkey");
        });

        modelBuilder.Entity<CacheLock>(entity =>
        {
            entity.HasKey(e => e.Key).HasName("cache_locks_pkey");
        });

        modelBuilder.Entity<ChatChannel>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("chat_channels_pkey");

            entity.Property(e => e.Description).HasDefaultValueSql("''::character varying");
        });

        modelBuilder.Entity<ChatMessage>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("chat_messages_pkey");

            entity.HasOne(d => d.From).WithMany()
                .HasForeignKey(d => d.FromId)
                .HasConstraintName("chat_messages_from_id_foreign");

            entity.HasOne(d => d.Channel).WithMany()
                .HasForeignKey(d => d.ChannelId)
                .HasConstraintName("chat_messages_channel_id_foreign");
        });

        modelBuilder.Entity<FailedJob>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("failed_jobs_pkey");

            entity.Property(e => e.FailedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
        });

        modelBuilder.Entity<Job>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("jobs_pkey");
        });

        modelBuilder.Entity<JobBatch>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("job_batches_pkey");
        });

        modelBuilder.Entity<Migration>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("migrations_pkey");
        });

        modelBuilder.Entity<PasswordResetToken>(entity =>
        {
            entity.HasKey(e => e.Email).HasName("password_reset_tokens_pkey");
        });

        modelBuilder.Entity<Session>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("sessions_pkey");
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("users_pkey");

            entity.Property(e => e.Country)
                .HasDefaultValueSql("'XX'::bpchar")
                .IsFixedLength();
            entity.Property(e => e.Privileges).HasDefaultValue(1);
        });

        modelBuilder.Entity<UserStat>(entity =>
        {
            entity.HasKey(e => new { e.UserId, e.Mode }).HasName("user_stats_pkey");

            entity.Property(e => e.Mode).HasDefaultValueSql("'0'::smallint");
            entity.Property(e => e.Accuracy).HasDefaultValueSql("'0'::double precision");
            entity.Property(e => e.RankedScore).HasDefaultValueSql("'0'::bigint");
            entity.Property(e => e.TotalScore).HasDefaultValueSql("'0'::bigint");

            entity.HasOne(d => d.User).WithMany(p => p.UserStats).HasConstraintName("user_stats_user_id_foreign");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
