using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using Monitoring.Infrastructure.Data.ScaffoldModels;

namespace Monitoring.Infrastructure.Data;

/// <summary>
/// Класс DbContext, сгенерированный через Scaffold-DBContext (Database First).
/// Хранит DbSet'ы, сопоставленные с таблицами.
/// 
/// ВАЖНО: строку подключения больше НЕ задаём внутри OnConfiguring,
/// а используем DI (через DbContextOptions, которые прокидывает AddDbContext).
/// </summary>
public partial class MyDbContext : DbContext
{
    // Конструктор без параметров Scaffold создаёт "старый" контекст
    // Но в реальной работе, если вы используете DI, обычно не вызываете его.
    public MyDbContext()
    {
    }

    // Основной конструктор, который будет вызван DI-контейнером
    public MyDbContext(DbContextOptions<MyDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<ContractFile> ContractFiles { get; set; }

    public virtual DbSet<Division> Divisions { get; set; }

    public virtual DbSet<Document> Documents { get; set; }

    public virtual DbSet<MessageView> MessageViews { get; set; }

    public virtual DbSet<Month> Months { get; set; }

    public virtual DbSet<OrderWorksTep> OrderWorksTeps { get; set; }

    public virtual DbSet<PrivatePenalty> PrivatePenalties { get; set; }

    public virtual DbSet<Request> Requests { get; set; }

    public virtual DbSet<SpPenalty> SpPenalties { get; set; }

    public virtual DbSet<TypeDoc> TypeDocs { get; set; }

    public virtual DbSet<TypeUser> TypeUsers { get; set; }

    public virtual DbSet<User> Users { get; set; }

    public virtual DbSet<UserAllowedDivision> UserAllowedDivisions { get; set; }

    public virtual DbSet<UserPrivacy> UserPrivacies { get; set; }

    public virtual DbSet<Work> Works { get; set; }

    public virtual DbSet<WorkUser> WorkUsers { get; set; }

    public virtual DbSet<WorkUserCheck> WorkUserChecks { get; set; }

    public virtual DbSet<WorkUserControl> WorkUserControls { get; set; }

    public virtual DbSet<WorksTep> WorksTeps { get; set; }

    /// <summary>
    /// Убираем вызов UseSqlServer(...), так как у нас DI.
    /// Если нужно, можем проверить IsConfigured и т.д.
    /// </summary>
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        // Если надо, можно проверить:
        // if (!optionsBuilder.IsConfigured)
        // {
        //     // Здесь можно при желании задать fallback-логику
        //     // Но обычно оставляют пустым
        // }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ContractFile>(entity =>
        {
            entity.HasKey(e => e.FileId).HasName("PK__Contract__6F0F989FA629DC72");

            entity.ToTable("ContractFile");

            entity.Property(e => e.FileId).HasColumnName("FileID");
            entity.Property(e => e.FileName).IsUnicode(false);
            entity.Property(e => e.FilePath).IsUnicode(false);
        });

        modelBuilder.Entity<Division>(entity =>
        {
            entity.HasKey(e => e.IdDivision);

            entity.Property(e => e.IdDivision).HasColumnName("idDivision");
            entity.Property(e => e.IdParentDivision).HasColumnName("idParentDivision");
            entity.Property(e => e.IdUserHead).HasColumnName("idUserHead");
            entity.Property(e => e.NameDivision).HasMaxLength(255);
            entity.Property(e => e.Position).HasColumnName("position");
            entity.Property(e => e.SmallNameDivision)
                .HasMaxLength(255)
                .HasColumnName("smallNameDivision");
        });

        modelBuilder.Entity<Document>(entity =>
        {
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.IdAuthor).HasColumnName("idAuthor");
            entity.Property(e => e.IdTypeDoc).HasColumnName("idTypeDoc");
            entity.Property(e => e.IdUtverUser).HasColumnName("idUtverUser");
            entity.Property(e => e.IsActive).HasColumnName("isActive");
            entity.Property(e => e.IsInWork).HasColumnName("isInWork");
            entity.Property(e => e.Name).IsUnicode(false);
            entity.Property(e => e.Notes).IsUnicode(false);
            entity.Property(e => e.NumDog)
                .IsUnicode(false)
                .HasColumnName("numDog");
            entity.Property(e => e.Number).IsUnicode(false);
        });

        modelBuilder.Entity<MessageView>(entity =>
        {
            entity.ToTable("messageView");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.DateSetInSystem).HasColumnName("dateSetInSystem");
            entity.Property(e => e.IdDocument)
                .HasMaxLength(10)
                .IsFixedLength()
                .HasColumnName("idDocument");
            entity.Property(e => e.IdUser).HasColumnName("idUser");
            entity.Property(e => e.IsActive).HasColumnName("isActive");
            entity.Property(e => e.Name)
                .IsUnicode(false)
                .HasColumnName("name");
        });

        modelBuilder.Entity<Month>(entity =>
        {
            entity.ToTable("Month");

            entity.Property(e => e.Id).HasColumnName("id");
        });

        modelBuilder.Entity<OrderWorksTep>(entity =>
        {
            entity.ToTable("OrderWorksTep");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.IdDiv).HasColumnName("idDiv");
            entity.Property(e => e.NamePokaz)
                .IsUnicode(false)
                .HasColumnName("namePokaz");
            entity.Property(e => e.Pp).HasColumnName("pp");
            entity.Property(e => e.Razdel)
                .IsUnicode(false)
                .HasColumnName("razdel");
            entity.Property(e => e.TypePokaz)
                .IsUnicode(false)
                .HasColumnName("typePokaz");
            entity.Property(e => e.Year).HasColumnName("year");
        });

        modelBuilder.Entity<PrivatePenalty>(entity =>
        {
            entity.HasKey(e => e.IdPrivatePenalty).HasName("PK_PrivatePenalty_1");

            entity.ToTable("PrivatePenalty");

            entity.Property(e => e.IdPrivatePenalty).HasColumnName("Id_PrivatePenalty");
            entity.Property(e => e.FkIdPenalty).HasColumnName("Fk_IdPenalty");
            entity.Property(e => e.FkIdUser).HasColumnName("FK_idUser");
            entity.Property(e => e.NotePrPenalty).IsUnicode(false);
        });

        modelBuilder.Entity<Request>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Requests__3214EC07E0C31DB8");

            entity.Property(e => e.Controller).HasMaxLength(100);
            entity.Property(e => e.DocumentName).HasMaxLength(500);
            entity.Property(e => e.Executor).HasMaxLength(100);
            entity.Property(e => e.Korrect1).HasColumnType("datetime");
            entity.Property(e => e.Korrect2).HasColumnType("datetime");
            entity.Property(e => e.Korrect3).HasColumnType("datetime");
            entity.Property(e => e.PlanDate).HasColumnType("datetime");
            entity.Property(e => e.ProposedDate).HasColumnType("datetime");
            entity.Property(e => e.Receiver).HasMaxLength(100);
            entity.Property(e => e.RequestDate).HasColumnType("datetime");
            entity.Property(e => e.RequestType).HasMaxLength(50);
            entity.Property(e => e.Sender).HasMaxLength(100);
            entity.Property(e => e.Status).HasMaxLength(20);
            entity.Property(e => e.WorkDocumentNumber).HasMaxLength(200);
            entity.Property(e => e.WorkName).HasMaxLength(500);
        });

        modelBuilder.Entity<SpPenalty>(entity =>
        {
            entity.HasKey(e => e.IdIdPenalty).HasName("PK_Sp_Penalty_1");

            entity.ToTable("Sp_Penalty");

            entity.Property(e => e.IdIdPenalty).HasColumnName("Id_IdPenalty");
            entity.Property(e => e.NameP).IsUnicode(false);
            entity.Property(e => e.SummPChief).HasColumnName("SummP_Chief");
            entity.Property(e => e.SummPDiv).HasColumnName("SummP_Div");
            entity.Property(e => e.SummPWorker).HasColumnName("SummP_Worker");
        });

        modelBuilder.Entity<TypeDoc>(entity =>
        {
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Name).IsUnicode(false);
        });

        modelBuilder.Entity<TypeUser>(entity =>
        {
            entity.HasNoKey();

            entity.Property(e => e.Id)
                .ValueGeneratedOnAdd()
                .HasColumnName("id");
            entity.Property(e => e.Type).HasMaxLength(50);
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.IdUser);

            entity.Property(e => e.IdUser).HasColumnName("idUser");
            entity.Property(e => e.IdDivision).HasColumnName("idDivision");
            entity.Property(e => e.IdTypeUser).HasColumnName("idTypeUser");
            entity.Property(e => e.Isvalid).HasDefaultValue(true);
            entity.Property(e => e.Name)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.Password)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.SmallName)
                .HasMaxLength(255)
                .IsUnicode(false)
                .HasColumnName("smallName");
        });

        modelBuilder.Entity<UserAllowedDivision>(entity =>
        {
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.IdDivision).HasColumnName("idDivision");
            entity.Property(e => e.IdUser).HasColumnName("idUser");

            entity.HasOne(d => d.IdDivisionNavigation).WithMany(p => p.UserAllowedDivisions)
                .HasForeignKey(d => d.IdDivision)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_UserAllowedDivisions_Divisions");

            entity.HasOne(d => d.IdUserNavigation).WithMany(p => p.UserAllowedDivisions)
                .HasForeignKey(d => d.IdUser)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_UserAllowedDivisions_Users");
        });

        modelBuilder.Entity<UserPrivacy>(entity =>
        {
            entity.HasKey(e => e.IdUser);

            entity.ToTable("UserPrivacy");

            entity.Property(e => e.IdUser)
                .ValueGeneratedNever()
                .HasColumnName("idUser");

            entity.HasOne(d => d.IdUserNavigation).WithOne(p => p.UserPrivacy)
                .HasForeignKey<UserPrivacy>(d => d.IdUser)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_UserPrivacy_Users");
        });

        modelBuilder.Entity<Work>(entity =>
        {
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.CurrNum)
                .HasMaxLength(10)
                .IsFixedLength();
            entity.Property(e => e.DaysRefresh)
                .HasMaxLength(10)
                .IsFixedLength()
                .HasColumnName("daysRefresh");
            entity.Property(e => e.IdDocuments).HasColumnName("idDocuments");
            entity.Property(e => e.Name).IsUnicode(false);
            entity.Property(e => e.Notes).IsUnicode(false);
            entity.Property(e => e.Razdel).IsUnicode(false);
            entity.Property(e => e.Rezult).HasMaxLength(300);
        });

        modelBuilder.Entity<WorkUser>(entity =>
        {
            entity.ToTable("WorkUser");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.DateFact).HasColumnName("dateFact");
            entity.Property(e => e.DateKorrect1).HasColumnName("dateKorrect1");
            entity.Property(e => e.DateKorrect2).HasColumnName("dateKorrect2");
            entity.Property(e => e.DateKorrect3).HasColumnName("dateKorrect3");
            entity.Property(e => e.IdUser).HasColumnName("idUser");
            entity.Property(e => e.IdWork).HasColumnName("idWork");
            entity.Property(e => e.IsSendToUser).HasColumnName("isSendToUser");
        });

        modelBuilder.Entity<WorkUserCheck>(entity =>
        {
            entity.ToTable("WorkUserCheck");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.IdUser).HasColumnName("idUser");
            entity.Property(e => e.IdWork).HasColumnName("idWork");
            entity.Property(e => e.IsSendToUser).HasColumnName("isSendToUser");
        });

        modelBuilder.Entity<WorkUserControl>(entity =>
        {
            entity.ToTable("WorkUserControl");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.IdUser).HasColumnName("idUser");
            entity.Property(e => e.IdWork).HasColumnName("idWork");
        });

        modelBuilder.Entity<WorksTep>(entity =>
        {
            entity.ToTable("WorksTep");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.DateFact).HasColumnName("dateFact");
            entity.Property(e => e.DatePlan).HasColumnName("datePlan");
            entity.Property(e => e.IdOrderWorksTep).HasColumnName("idOrderWorksTep");
            entity.Property(e => e.KolFact).HasColumnName("kolFact");
            entity.Property(e => e.KolPlan).HasColumnName("kolPlan");
            entity.Property(e => e.Month).HasColumnName("month");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
