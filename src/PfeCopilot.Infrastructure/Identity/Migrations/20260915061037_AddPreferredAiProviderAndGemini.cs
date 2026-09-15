using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PfeCopilot.Infrastructure.Identity.Migrations
{
    /// <inheritdoc />
    public partial class AddPreferredAiProviderAndGemini : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PreferredAiProvider",
                table: "CandidateProfiles",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PreferredAiProvider",
                table: "CandidateProfiles");
        }
    }
}
