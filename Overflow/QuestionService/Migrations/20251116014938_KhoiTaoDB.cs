using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace QuestionService.Migrations
{
    /// <inheritdoc />
    public partial class KhoiTaoDB : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Questions",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    Title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Content = table.Column<string>(type: "character varying(5000)", maxLength: 5000, nullable: false),
                    AskerId = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ViewCount = table.Column<int>(type: "integer", nullable: false),
                    TagSlugs = table.Column<List<string>>(type: "text[]", nullable: false),
                    HasAcceptedAnswer = table.Column<bool>(type: "boolean", nullable: false),
                    Votes = table.Column<int>(type: "integer", nullable: false),
                    AnswerCount = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Questions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Tags",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    Name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Slug = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    UsageCount = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tags", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "Tags",
                columns: new[] { "Id", "Description", "Name", "Slug", "UsageCount" },
                values: new object[,]
                {
                    { "aspire", "A composable, opinionated stack for building distributed apps with .NET. Provides dashboard, diagnostics, and simplified service orchestration.", "Aspire", "aspire", 0 },
                    { "dotnet", "A modern, cross-platform development platform for building cloud, web, mobile, desktop, and IoT apps using C# and F#.", ".NET", "dotnet", 0 },
                    { "ef-core", "A modern object-database mapper (ORM) for .NET that supports LINQ, change tracking, and migrations for working with relational databases.", "Entity Framework Core", "ef-core", 0 },
                    { "keycloak", "An open-source identity and access management solution for modern applications and services. Integrates easily with OAuth2, OIDC, and SSO.", "Keycloak", "keycloak", 0 },
                    { "microservices", "An architectural style that structures an application as a collection of loosely coupled services that can be independently deployed and scaled.", "Microservices", "microservices", 0 },
                    { "nextjs", "A React framework for building fast, full-stack web apps with server-side rendering, routing, and static site generation.", "Next.js", "nextjs", 0 },
                    { "postgresql", "A powerful, open-source object-relational database system known for reliability, feature richness, and standards compliance.", "PostgreSQL", "postgresql", 0 },
                    { "signalr", "A real-time communication library for ASP.NET that enables server-to-client messaging over WebSockets, long polling, and more.", "SignalR", "signalr", 0 },
                    { "typescript", "A statically typed superset of JavaScript that compiles to clean JavaScript. Helps build large-scale apps with tooling support.", "TypeScript", "typescript", 0 },
                    { "wolverine", "A high-performance messaging and command-handling framework for .NET with built-in support for Mediator, queues, retries, and durable messaging.", "Wolverine", "wolverine", 0 }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Questions");

            migrationBuilder.DropTable(
                name: "Tags");
        }
    }
}
