using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LegacyVault.Infrastructure.Data.Migrations
{
    /// <summary>
    /// SRS v3.14.1: một Owner một kho (SETUP-12); tách trạng thái kho (OPEN/CLOSED) khỏi kế hoạch bàn giao
    /// (bảng HandoverPlans) và quyền dịch vụ (bảng VaultEntitlements); thêm Users.EmailVerifiedAt (AUTH-04).
    /// </summary>
    /// <remarks>
    /// Migration này được sửa tay sau khi EF sinh ra, để chuyển dữ liệu cũ:
    /// - Vaults.Status cũ (Draft/Active/.../Completed) → trạng thái kế hoạch; kho Completed → Vault CLOSED.
    /// - Vaults.ActivatedAt cũ → HandoverPlans.ActivatedAt (EF định đổi tên thành ClosedAt, sai nghĩa).
    /// - Mọi kho cũ nhận quyền dịch vụ FREE 20 MiB.
    /// - Tài khoản Active cũ coi như đã xác minh email (trước đây chưa có bước xác minh).
    /// SRS §14.7: nếu một Owner đang có nhiều kho, migration DỪNG để nhóm tự gộp, không tự xóa kho nào.
    /// </remarks>
    public partial class OneVaultPerOwnerWithPlanAndEntitlement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF EXISTS (SELECT OwnerId FROM Vaults GROUP BY OwnerId HAVING COUNT(*) > 1)
    THROW 50001, N'Có Owner đang sở hữu nhiều kho. SRS v3.14.1 §14.7: gộp dữ liệu thủ công trước khi áp dụng ràng buộc một kho mỗi Owner.', 1;");

            migrationBuilder.DropIndex(
                name: "UX_Vaults_OwnerId_Open",
                table: "Vaults");

            // ---- Users.EmailVerifiedAt ----
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "EmailVerifiedAt",
                table: "Users",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.Sql("UPDATE Users SET EmailVerifiedAt = CreatedAt WHERE Status = 'Active';");

            // ---- Bảng mới ----
            migrationBuilder.CreateTable(
                name: "HandoverPlans",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VaultId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    ConfigVersion = table.Column<int>(type: "int", nullable: false),
                    ActivatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HandoverPlans", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HandoverPlans_Vaults_VaultId",
                        column: x => x.VaultId,
                        principalTable: "Vaults",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "VaultEntitlements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VaultId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Tier = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    StorageQuotaBytes = table.Column<long>(type: "bigint", nullable: false),
                    IncludedBeneficiarySlots = table.Column<int>(type: "int", nullable: false),
                    ExtraBeneficiarySlots = table.Column<int>(type: "int", nullable: false),
                    ServiceExpiresAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VaultEntitlements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VaultEntitlements_Vaults_VaultId",
                        column: x => x.VaultId,
                        principalTable: "Vaults",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            // ---- Chuyển dữ liệu kho cũ (đọc Vaults.Status/ActivatedAt cũ TRƯỚC khi đổi/xóa) ----
            migrationBuilder.Sql(@"
INSERT INTO HandoverPlans (Id, VaultId, Status, ConfigVersion, ActivatedAt, CreatedAt)
SELECT NEWID(), Id,
       CASE Status
           WHEN 'Active' THEN 'Active'
           WHEN 'CheckInOverdue' THEN 'CheckinOverdue'
           WHEN 'PendingVerification' THEN 'AwaitingCheck'
           WHEN 'ReadyForHandover' THEN 'HandoverInProgress'
           WHEN 'PartiallyHandedOver' THEN 'HandoverInProgress'
           WHEN 'Completed' THEN 'Closed'
           WHEN 'Frozen' THEN 'Blocked'
           ELSE 'Draft'
       END,
       1, ActivatedAt, CreatedAt
FROM Vaults;");

            migrationBuilder.Sql(@"
INSERT INTO VaultEntitlements (Id, VaultId, Tier, Status, StorageQuotaBytes, IncludedBeneficiarySlots, ExtraBeneficiarySlots, CreatedAt)
SELECT NEWID(), Id, 'Free', 'Free', 20971520, 0, 0, CreatedAt
FROM Vaults;");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ClosedAt",
                table: "Vaults",
                type: "datetimeoffset",
                nullable: true);

            // Hai CASE cùng đọc giá trị Status CŨ (SQL Server tính mọi biểu thức trước khi ghi).
            migrationBuilder.Sql(@"
UPDATE Vaults
SET ClosedAt = CASE WHEN Status = 'Completed' THEN COALESCE(UpdatedAt, CreatedAt) ELSE NULL END,
    Status   = CASE WHEN Status = 'Completed' THEN 'Closed' ELSE 'Open' END;");

            migrationBuilder.DropColumn(
                name: "ActivatedAt",
                table: "Vaults");

            migrationBuilder.DropColumn(
                name: "ActivatedAt",
                table: "Assets");

            // ---- Index ----
            migrationBuilder.CreateIndex(
                name: "UX_Vaults_OwnerId",
                table: "Vaults",
                column: "OwnerId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HandoverPlans_VaultId",
                table: "HandoverPlans",
                column: "VaultId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_VaultEntitlements_VaultId",
                table: "VaultEntitlements",
                column: "VaultId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_Vaults_OwnerId",
                table: "Vaults");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ActivatedAt",
                table: "Assets",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ActivatedAt",
                table: "Vaults",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.Sql(@"
UPDATE v
SET v.ActivatedAt = p.ActivatedAt,
    v.Status = CASE
        WHEN v.Status = 'Closed' THEN 'Completed'
        WHEN p.Status = 'Active' THEN 'Active'
        WHEN p.Status = 'CheckinOverdue' THEN 'CheckInOverdue'
        WHEN p.Status = 'AwaitingCheck' THEN 'PendingVerification'
        WHEN p.Status = 'HandoverInProgress' THEN 'ReadyForHandover'
        WHEN p.Status = 'Blocked' THEN 'Frozen'
        ELSE 'Draft'
    END
FROM Vaults v
JOIN HandoverPlans p ON p.VaultId = v.Id;");

            migrationBuilder.DropColumn(
                name: "ClosedAt",
                table: "Vaults");

            migrationBuilder.DropTable(
                name: "HandoverPlans");

            migrationBuilder.DropTable(
                name: "VaultEntitlements");

            migrationBuilder.DropColumn(
                name: "EmailVerifiedAt",
                table: "Users");

            migrationBuilder.CreateIndex(
                name: "UX_Vaults_OwnerId_Open",
                table: "Vaults",
                column: "OwnerId",
                unique: true,
                filter: "[Status] <> 'Completed'");
        }
    }
}
