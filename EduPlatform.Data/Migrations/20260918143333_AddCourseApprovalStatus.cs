using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EduPlatform.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCourseApprovalStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "RejectionReason",
                table: "Courses",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "Courses",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "Draft");

            migrationBuilder.Sql(
                "UPDATE Courses SET Status = 'Approved' WHERE IsPublished = 1;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RejectionReason",
                table: "Courses");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "Courses");
        }
    }
}
