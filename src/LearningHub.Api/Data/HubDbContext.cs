using LearningHub.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace LearningHub.Api.Data;

public sealed class HubDbContext(DbContextOptions<HubDbContext> options) : DbContext(options)
{
    public DbSet<Skill> Skills => Set<Skill>();
    public DbSet<Lesson> Lessons => Set<Lesson>();
    public DbSet<Track> Tracks => Set<Track>();
    public DbSet<TimeBox> TimeBoxes => Set<TimeBox>();
    public DbSet<PlanSettings> PlanSettings => Set<PlanSettings>();
    public DbSet<PlanEntry> PlanEntries => Set<PlanEntry>();
    public DbSet<RestDay> RestDays => Set<RestDay>();
    public DbSet<LessonProgress> LessonProgress => Set<LessonProgress>();
    public DbSet<Question> Questions => Set<Question>();
    public DbSet<AnswerRecord> AnswerRecords => Set<AnswerRecord>();
    public DbSet<Mistake> Mistakes => Set<Mistake>();
    public DbSet<Note> Notes => Set<Note>();
    public DbSet<ConfidenceHistory> ConfidenceHistory => Set<ConfidenceHistory>();
    public DbSet<Resource> Resources => Set<Resource>();
    public DbSet<Reflection> Reflections => Set<Reflection>();
    public DbSet<ReflectionSuggestion> ReflectionSuggestions => Set<ReflectionSuggestion>();
    public DbSet<CompletedDsaTopic> CompletedDsaTopics => Set<CompletedDsaTopic>();
    public DbSet<GoogleToken> GoogleTokens => Set<GoogleToken>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Skill>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasMaxLength(80);
            e.Property(x => x.Name).HasMaxLength(160);
            e.Property(x => x.Area).HasMaxLength(80);
            e.Property(x => x.Description).HasColumnType("longtext");
        });

        modelBuilder.Entity<Lesson>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasMaxLength(120);
            e.Property(x => x.SkillId).HasMaxLength(80);
            e.Property(x => x.Topic).HasMaxLength(80);
            e.Property(x => x.ContentJson).HasColumnType("longtext");
            e.Property(x => x.FilePath).HasMaxLength(400);
            e.HasIndex(x => new { x.SkillId, x.Order });
        });

        modelBuilder.Entity<Track>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.SkillId).HasMaxLength(80);
            e.HasIndex(x => x.SkillId);
        });

        modelBuilder.Entity<TimeBox>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.SkillId).HasMaxLength(80);
            e.Property(x => x.Label).HasMaxLength(200);
            e.Property(x => x.PausedTrackIdsJson).HasColumnType("longtext");
            e.Property(x => x.StartDate).HasConversion(DateOnlyConverter);
            e.Property(x => x.EndDate).HasConversion(DateOnlyConverter);
        });

        modelBuilder.Entity<PlanSettings>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.RestWeekdaysCsv).HasMaxLength(120);
            e.Property(x => x.StartDate).HasConversion(DateOnlyConverter);
        });

        modelBuilder.Entity<PlanEntry>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.LessonId).HasMaxLength(120);
            e.Property(x => x.Date).HasConversion(DateOnlyConverter);
            e.HasIndex(x => new { x.Date, x.TrackId });
            e.HasIndex(x => x.LessonId);
        });

        modelBuilder.Entity<RestDay>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Date).HasConversion(DateOnlyConverter);
            e.HasIndex(x => x.Date).IsUnique();
        });

        modelBuilder.Entity<LessonProgress>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.LessonId).HasMaxLength(120);
            e.Property(x => x.LearnedNote).HasColumnType("longtext");
            e.Property(x => x.ExplainBack).HasColumnType("longtext");
            e.Property(x => x.ExplainGrade).HasColumnType("longtext");
            e.HasIndex(x => new { x.LessonId, x.TrackId }).IsUnique();
        });

        modelBuilder.Entity<Question>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.SourceKey).HasMaxLength(160);
            e.Property(x => x.LessonId).HasMaxLength(120);
            e.Property(x => x.SkillId).HasMaxLength(80);
            e.Property(x => x.Prompt).HasColumnType("longtext");
            e.Property(x => x.OptionsJson).HasColumnType("longtext");
            e.Property(x => x.Explanation).HasColumnType("longtext");
            e.Property(x => x.OptionExplanationsJson).HasColumnType("longtext");
            e.Property(x => x.Code).HasColumnType("longtext");
            e.Property(x => x.Answer).HasColumnType("longtext");
            e.HasIndex(x => x.SourceKey).IsUnique();
            e.HasIndex(x => x.SkillId);
        });

        modelBuilder.Entity<AnswerRecord>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.NextReviewDate).HasConversion(DateOnlyConverter);
            e.HasIndex(x => new { x.QuestionId, x.AnsweredAt });
            e.HasIndex(x => x.NextReviewDate);
        });

        modelBuilder.Entity<Mistake>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.SkillId).HasMaxLength(80);
            e.Property(x => x.LessonId).HasMaxLength(120);
            e.Property(x => x.ReasonTag).HasMaxLength(120);
            e.Property(x => x.Note).HasColumnType("longtext");
            e.HasIndex(x => x.SkillId);
        });

        modelBuilder.Entity<Note>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Title).HasMaxLength(200);
            e.Property(x => x.Body).HasColumnType("longtext");
            e.Property(x => x.SkillId).HasMaxLength(80);
            e.Property(x => x.LessonId).HasMaxLength(120);
        });

        modelBuilder.Entity<ConfidenceHistory>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.SkillId).HasMaxLength(80);
            e.Property(x => x.Date).HasConversion(DateOnlyConverter);
            e.HasIndex(x => new { x.SkillId, x.Date });
        });

        modelBuilder.Entity<Resource>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Title).HasMaxLength(240);
            e.Property(x => x.Url).HasColumnType("longtext");
            e.Property(x => x.Notes).HasColumnType("longtext");
            e.Property(x => x.SkillId).HasMaxLength(80);
            e.Property(x => x.LessonId).HasMaxLength(120);
        });

        modelBuilder.Entity<Reflection>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.WentWell).HasColumnType("longtext");
            e.Property(x => x.WasHard).HasColumnType("longtext");
            e.Property(x => x.ToChange).HasColumnType("longtext");
            e.Property(x => x.SummaryJson).HasColumnType("longtext");
            e.Property(x => x.WeekStart).HasConversion(DateOnlyConverter);
            e.HasIndex(x => x.WeekStart).IsUnique();
        });

        modelBuilder.Entity<ReflectionSuggestion>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Text).HasColumnType("longtext");
            e.Property(x => x.Rationale).HasColumnType("longtext");
        });

        modelBuilder.Entity<CompletedDsaTopic>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Topic).HasMaxLength(80);
            e.HasIndex(x => new { x.TrackId, x.Topic }).IsUnique();
        });

        modelBuilder.Entity<GoogleToken>(e => e.HasKey(x => x.Id));
    }

    private static readonly Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter<DateOnly, DateTime> DateOnlyConverter =
        new(date => date.ToDateTime(TimeOnly.MinValue), value => DateOnly.FromDateTime(value));
}
