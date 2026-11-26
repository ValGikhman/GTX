using GTX.Common;
using GTX.Models;
using Services;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Web.Helpers;

namespace GTX {
    // No credential cache: password changes, deletion and permission edits take effect on the next request.
    public sealed class SiteRoleStore : BaseService {
        private static readonly object InitializeLock = new object();
        private static readonly HashSet<string> Initialized = new HashSet<string>();
        private void Initialize() {
            lock (InitializeLock) {
                if (Initialized.Contains(connectionString)) return;
                using (var db = new SqlConnection(connectionString)) {
                    db.Open();
                    using (var tx = db.BeginTransaction(IsolationLevel.Serializable)) {
                        using (var cmd = new SqlCommand(@"IF OBJECT_ID('dbo.SiteRole','U') IS NULL
CREATE TABLE dbo.SiteRole(Id int IDENTITY(100,1) PRIMARY KEY, Name nvarchar(40) NOT NULL UNIQUE, PasswordHash nvarchar(256) NOT NULL, Revision nvarchar(32) NOT NULL, Permissions nvarchar(1000) NOT NULL);
IF OBJECT_ID('dbo.SiteRoleSetup','U') IS NULL CREATE TABLE dbo.SiteRoleSetup(Id int PRIMARY KEY);", db, tx)) cmd.ExecuteNonQuery();
                        bool seed;
                        using (var cmd = new SqlCommand("SELECT COUNT(*) FROM dbo.SiteRoleSetup WITH (UPDLOCK,HOLDLOCK) WHERE Id=1", db, tx)) seed = (int)cmd.ExecuteScalar() == 0;
                        if (seed) {
                            if (!ConfigurationManager.AppSettings.AllKeys.Any(k => k.StartsWith("sitePassword:", StringComparison.OrdinalIgnoreCase)))
                                throw new InvalidOperationException("Site roles have not been initialized in this database. Import the existing hashed role records before using this environment.");
                            foreach (var key in ConfigurationManager.AppSettings.AllKeys.Where(k => k.StartsWith("sitePassword:", StringComparison.OrdinalIgnoreCase))) {
                                var name = key.Substring(13);
                                if (MajordomeAccount.IsReservedName(name)) continue;
                                if (name.Equals("Blog", StringComparison.OrdinalIgnoreCase)) name = "Blogger";
                                var standard = Enum.GetNames(typeof(CommonUnit.Roles)).FirstOrDefault(n => n.Equals(name, StringComparison.OrdinalIgnoreCase));
                                name = standard ?? name;
                                using (var cmd = new SqlCommand("INSERT dbo.SiteRole(Name,PasswordHash,Revision,Permissions) VALUES(@name,@hash,@revision,@permissions)", db, tx)) {
                                    cmd.Parameters.AddWithValue("@name", name);
                                    cmd.Parameters.AddWithValue("@hash", Crypto.HashPassword(ConfigurationManager.AppSettings[key]));
                                    cmd.Parameters.AddWithValue("@revision", Guid.NewGuid().ToString("N"));
                                    cmd.Parameters.AddWithValue("@permissions", string.Join(",", MajordomeMenu.Defaults(name)));
                                    cmd.ExecuteNonQuery();
                                }
                            }
                            using (var cmd = new SqlCommand("INSERT dbo.SiteRoleSetup(Id) VALUES(1)", db, tx)) cmd.ExecuteNonQuery();
                        }
                        // Upgrade existing roles once; later edits must retain their chosen grants.
                        using (var cmd = new SqlCommand(@"IF NOT EXISTS (SELECT 1 FROM dbo.SiteRoleSetup WITH (UPDLOCK,HOLDLOCK) WHERE Id=2)
BEGIN
    UPDATE dbo.SiteRole SET Permissions=@permissions;
    INSERT dbo.SiteRoleSetup(Id) VALUES(2);
END", db, tx)) {
                            cmd.Parameters.AddWithValue("@permissions", string.Join(",", MajordomeMenu.Defaults("")));
                            cmd.ExecuteNonQuery();
                        }
                        // Keep existing Dashboard access intact without changing any other grants.
                        using (var cmd = new SqlCommand(@"IF NOT EXISTS (SELECT 1 FROM dbo.SiteRoleSetup WITH (UPDLOCK,HOLDLOCK) WHERE Id=3)
BEGIN
    UPDATE dbo.SiteRole SET Permissions=Permissions + N',' + @section
    WHERE CHARINDEX(N',' + @dashboard + N',', (N',' + Permissions + N',') COLLATE Latin1_General_100_BIN2) > 0
      AND CHARINDEX(N',' + @section + N',', (N',' + Permissions + N',') COLLATE Latin1_General_100_BIN2) = 0;
    INSERT dbo.SiteRoleSetup(Id) VALUES(3);
END", db, tx)) {
                            cmd.Parameters.AddWithValue("@dashboard", MajordomeItem.InventoryDashboard.ToString());
                            cmd.Parameters.AddWithValue("@section", MajordomeMenu.DashboardSectionKey(DashboardPermission.LocationCode));
                            cmd.ExecuteNonQuery();
                        }
                        // These legacy tokens did not confer access. Give each token its own delimiters, even if duplicated.
                        using (var cmd = new SqlCommand(@"IF NOT EXISTS (SELECT 1 FROM dbo.SiteRoleSetup WITH (UPDLOCK,HOLDLOCK) WHERE Id=4)
BEGIN
    UPDATE r SET Permissions=CASE WHEN LEN(clean.Value) <= 1 THEN N'' ELSE SUBSTRING(clean.Value, 2, LEN(clean.Value) - 2) END
    FROM dbo.SiteRole AS r
    CROSS APPLY (VALUES (N',' + REPLACE(r.Permissions, N',', N',,') + N',')) AS wrapped(Value)
    CROSS APPLY (VALUES (REPLACE(REPLACE(REPLACE(wrapped.Value, N',Roles,', N''), N',Users,', N''), N',,', N','))) AS clean(Value)
    WHERE CHARINDEX(N',Roles,', wrapped.Value) > 0 OR CHARINDEX(N',Users,', wrapped.Value) > 0;
    INSERT dbo.SiteRoleSetup(Id) VALUES(4);
END", db, tx)) cmd.ExecuteNonQuery();
                        tx.Commit();
                    }
                }
                Initialized.Add(connectionString);
            }
        }
        public List<SiteRoleModel> All() {
            Initialize();
            var result = new List<SiteRoleModel>();
            using (var db = new SqlConnection(connectionString))
            using (var cmd = new SqlCommand("SELECT Id,Name,PasswordHash,Revision,Permissions FROM dbo.SiteRole ORDER BY Name", db)) {
                db.Open();
                using (var r = cmd.ExecuteReader()) while (r.Read()) result.Add(new SiteRoleModel { Id=r.GetInt32(0), Name=r.GetString(1), PasswordHash=r.GetString(2), Revision=r.GetString(3), Permissions=ParsePermissions(r.GetString(4)) });
            }
            return result.Where(r => !MajordomeAccount.IsReservedName(r.Name)).ToList();
        }
        public SiteRoleModel Authenticate(string password) {
            var administrator = MajordomeAccount.Current();
            if (administrator != null && Crypto.VerifyHashedPassword(administrator.PasswordHash, password ?? "")) return administrator;
            return All().FirstOrDefault(r => Crypto.VerifyHashedPassword(r.PasswordHash, password ?? ""));
        }
        public void Save(SiteRoleModel model, SiteRoleModel actor) {
            if (model == null) throw new ArgumentNullException(nameof(model));
            if (model.Id < 0 || MajordomeAccount.IsReservedName(model.Name)) throw new InvalidOperationException("The built-in administrator cannot be edited.");
            model.Name = model.Name?.Trim();
            if (string.IsNullOrEmpty(model.Name)) throw new InvalidOperationException("A role name is required.");
            if (model.Name.Equals("User", StringComparison.OrdinalIgnoreCase) || model.Name.Equals("Blog", StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("That role name is reserved.");
            var permissions = (model.Permissions ?? new string[0]).Distinct().ToArray();
            var dashboardSections = MajordomeMenu.DashboardSections.Keys.Select(MajordomeMenu.DashboardSectionKey).ToArray();
            if (permissions.Any(p => !MajordomeMenu.Items.Any(i => MajordomeMenu.PermissionKey(i.Item) == p) && !dashboardSections.Contains(p))) throw new InvalidOperationException("Unknown permission.");
            if (permissions.Contains(nameof(MajordomeItem.Services))) throw new InvalidOperationException("Services is reserved for the built-in administrator.");
            if (!permissions.Contains(MajordomeItem.InventoryDashboard.ToString())) permissions = permissions.Except(dashboardSections).ToArray();
            var administrator = MajordomeAccount.Current();
            if (!string.IsNullOrEmpty(model.Password) && administrator != null && Crypto.VerifyHashedPassword(administrator.PasswordHash, model.Password))
                throw new InvalidOperationException("That password is already assigned to another role.");
            Initialize();
            using (var db = new SqlConnection(connectionString)) {
                db.Open();
                using (var tx = db.BeginTransaction(IsolationLevel.Serializable)) {
                    actor = ReadAuthorizedManager(db, tx, actor);
                    var target = model.Id == 0 ? null : ReadRoleForUpdate(db, tx, model.Id);
                    if (model.Id != 0 && target == null) throw new InvalidOperationException("Role no longer exists.");
                    if (MajordomeAccount.IsReservedName(target?.Name)) throw new InvalidOperationException("The built-in administrator cannot be edited.");
                    if (!SiteAccess.CanManageRole(actor, target) || permissions.Except(SiteAccess.GrantableRolePermissions(actor)).Any())
                        throw new UnauthorizedAccessException("You do not have access to manage this role or its permissions.");
                    if (model.Id == 0 && string.IsNullOrWhiteSpace(model.Password)) throw new InvalidOperationException("A password is required for a new role.");
                    if (!string.IsNullOrEmpty(model.Password)) {
                        using (var cmd = new SqlCommand("SELECT PasswordHash FROM dbo.SiteRole WHERE Id<>@id", db, tx)) {
                            cmd.Parameters.AddWithValue("@id", model.Id);
                            using (var r = cmd.ExecuteReader()) while (r.Read()) if (Crypto.VerifyHashedPassword(r.GetString(0), model.Password)) throw new InvalidOperationException("That password is already assigned to another role.");
                        }
                    }
                    using (var cmd = new SqlCommand(model.Id == 0 ? "INSERT dbo.SiteRole(Name,PasswordHash,Revision,Permissions) VALUES(@name,@hash,@revision,@permissions)" : "UPDATE dbo.SiteRole SET Name=@name,PasswordHash=COALESCE(@hash,PasswordHash),Revision=CASE WHEN @hash IS NULL THEN Revision ELSE @revision END,Permissions=@permissions WHERE Id=@id", db, tx)) {
                        cmd.Parameters.AddWithValue("@id", model.Id); cmd.Parameters.AddWithValue("@name", model.Name.Trim());
                        cmd.Parameters.Add("@hash", SqlDbType.NVarChar, 256).Value = string.IsNullOrEmpty(model.Password) ? (object)DBNull.Value : Crypto.HashPassword(model.Password);
                        cmd.Parameters.AddWithValue("@revision", Guid.NewGuid().ToString("N"));
                        cmd.Parameters.AddWithValue("@permissions", string.Join(",", permissions)); cmd.ExecuteNonQuery();
                    }
                    tx.Commit();
                }
            }
        }
        public void Delete(int id, SiteRoleModel actor) {
            if (id <= 0) throw new InvalidOperationException("The built-in administrator cannot be deleted.");
            Initialize();
            using (var db = new SqlConnection(connectionString)) {
                db.Open();
                using (var tx = db.BeginTransaction(IsolationLevel.Serializable)) {
                    actor = ReadAuthorizedManager(db, tx, actor);
                    var target = ReadRoleForUpdate(db, tx, id);
                    if (target == null || MajordomeAccount.IsReservedName(target.Name)) throw new InvalidOperationException("The role no longer exists or is protected.");
                    if (!SiteAccess.CanManageRole(actor, target)) throw new UnauthorizedAccessException("You do not have access to manage this role.");
                    using (var cmd = new SqlCommand("DELETE dbo.SiteRole WHERE Id=@id", db, tx)) {
                        cmd.Parameters.AddWithValue("@id", id);
                        cmd.ExecuteNonQuery();
                    }
                    tx.Commit();
                }
            }
        }

        private static string[] ParsePermissions(string value) {
            return value.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
        }

        private static SiteRoleModel ReadAuthorizedManager(SqlConnection db, SqlTransaction tx, SiteRoleModel actor) {
            if (SiteAccess.IsMajordome(actor)) return actor;
            var current = actor != null && actor.Id > 0 ? ReadRoleForUpdate(db, tx, actor.Id) : null;
            if (!SiteAccess.Can(current, MajordomeItem.Roles) || current.Revision != actor.Revision)
                throw new UnauthorizedAccessException("Role management access is required.");
            return current;
        }

        private static SiteRoleModel ReadRoleForUpdate(SqlConnection db, SqlTransaction tx, int id) {
            using (var cmd = new SqlCommand("SELECT Id,Name,Revision,Permissions FROM dbo.SiteRole WITH (UPDLOCK,HOLDLOCK) WHERE Id=@id", db, tx)) {
                cmd.Parameters.AddWithValue("@id", id);
                using (var r = cmd.ExecuteReader()) {
                    return r.Read() ? new SiteRoleModel {
                        Id = r.GetInt32(0), Name = r.GetString(1), Revision = r.GetString(2), Permissions = ParsePermissions(r.GetString(3))
                    } : null;
                }
            }
        }
    }
}
