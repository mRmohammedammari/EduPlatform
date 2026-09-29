using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EduPlatform.Data.Migrations
{
    /// <summary>
    /// Cree reellement la table <c>CourseReviewReports</c>.
    /// </summary>
    /// <remarks>
    /// Correctif. La migration <c>20260921203940_AddCourseReviewReports</c> a ete generee
    /// vide (Up et Down sans instruction) puis enregistree comme appliquee. La table n'a
    /// donc jamais existe, alors que le code s'en sert : <c>GET /api/admin/reviews</c>
    /// repondait 500 et le signalement d'un avis echouait, sur toute base reelle.
    /// Les tests unitaires ne l'avaient pas vu car EF InMemory construit le schema depuis
    /// le modele, sans jouer les migrations.
    ///
    /// Migration ecrite a la main : le snapshot contient deja l'entite, donc
    /// <c>dotnet ef migrations add</c> produit un Up vide.
    ///
    /// Le garde <c>IF OBJECT_ID</c> rend l'operation sans effet sur une base ou la table
    /// aurait ete creee manuellement entre-temps.
    /// </remarks>
    public partial class CreateCourseReviewReportsTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF OBJECT_ID(N'[CourseReviewReports]', N'U') IS NULL
                BEGIN
                    CREATE TABLE [CourseReviewReports] (
                        [Id] uniqueidentifier NOT NULL,
                        [ReviewId] uniqueidentifier NOT NULL,
                        [ReporterId] uniqueidentifier NOT NULL,
                        [Reason] nvarchar(1000) NOT NULL,
                        [CreatedAt] datetime2 NOT NULL,
                        CONSTRAINT [PK_CourseReviewReports] PRIMARY KEY ([Id]),
                        CONSTRAINT [FK_CourseReviewReports_CourseReviews_ReviewId]
                            FOREIGN KEY ([ReviewId]) REFERENCES [CourseReviews] ([Id]) ON DELETE CASCADE,
                        CONSTRAINT [FK_CourseReviewReports_Users_ReporterId]
                            FOREIGN KEY ([ReporterId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
                    );

                    CREATE INDEX [IX_CourseReviewReports_ReporterId]
                        ON [CourseReviewReports] ([ReporterId]);

                    CREATE UNIQUE INDEX [IX_CourseReviewReports_ReviewId_ReporterId]
                        ON [CourseReviewReports] ([ReviewId], [ReporterId]);
                END
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF OBJECT_ID(N'[CourseReviewReports]', N'U') IS NOT NULL
                    DROP TABLE [CourseReviewReports];
                """);
        }
    }
}
