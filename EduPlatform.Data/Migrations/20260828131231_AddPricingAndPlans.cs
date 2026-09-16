using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EduPlatform.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPricingAndPlans : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "AmountPaid",
                table: "Enrollments",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "PaymentStatus",
                table: "Enrollments",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Plan",
                table: "Enrollments",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "Price",
                table: "Courses",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AmountPaid",
                table: "Enrollments");

            migrationBuilder.DropColumn(
                name: "PaymentStatus",
                table: "Enrollments");

            migrationBuilder.DropColumn(
                name: "Plan",
                table: "Enrollments");

            migrationBuilder.DropColumn(
                name: "Price",
                table: "Courses");
        }
    }
}
