using GTX.Models;
using Services;
using System;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Data;
using System.Data.SqlClient;
using System.Web;

namespace GTX {
    // One database-backed company profile, shared by the site and all roles.
    public sealed class SiteProfileStore : BaseService {
        private const string RequestCacheKey = "GTX.SiteProfile";

        // Reuse one read within a request; the next request always sees committed changes.
        public static SiteProfileModel Current {
            get {
                var context = HttpContext.Current;
                var profile = context?.Items[RequestCacheKey] as SiteProfileModel;
                if (profile == null) {
                    profile = new SiteProfileStore().Get();
                    if (context != null) context.Items[RequestCacheKey] = profile;
                }
                return profile;
            }
        }

        public SiteProfileModel Get() {
            using (var connection = new SqlConnection(connectionString))
            using (var command = new SqlCommand(@"SELECT CompanyName, Email, Phone, StreetAddress, AddressLine2, City, State, ZipCode,
ResearchRadiusMiles, Latitude, Longitude, DocumentaryFee FROM dbo.SiteProfile WHERE Id=1;
SELECT DayOfWeek, IsClosed, OpenTime, CloseTime FROM dbo.SiteProfileBusinessHours WHERE ProfileId=1 ORDER BY (DayOfWeek+6)%7;", connection)) {
                connection.Open();
                using (var reader = command.ExecuteReader()) {
                    if (!reader.Read()) throw new InvalidOperationException("The company profile has not been initialized. Run Scripts/Sql/CreateSiteProfile.sql against this database.");
                    var profile = new SiteProfileModel {
                        CompanyName = reader.GetString(0), Email = reader.GetString(1), Phone = reader.GetString(2),
                        StreetAddress = reader.GetString(3), AddressLine2 = reader.IsDBNull(4) ? null : reader.GetString(4),
                        City = reader.GetString(5), State = reader.GetString(6), ZipCode = reader.GetString(7),
                        ResearchRadiusMiles = reader.GetInt32(8),
                        Latitude = reader.IsDBNull(9) ? (decimal?)null : reader.GetDecimal(9),
                        Longitude = reader.IsDBNull(10) ? (decimal?)null : reader.GetDecimal(10),
                        DocumentaryFee = reader.GetInt32(11)
                    };
                    reader.NextResult();
                    while (reader.Read()) profile.BusinessHours.Add(new SiteBusinessHoursModel {
                        DayOfWeek = reader.GetByte(0), IsClosed = reader.GetBoolean(1),
                        OpenTime = reader.IsDBNull(2) ? null : reader.GetTimeSpan(2).ToString(@"hh\:mm", CultureInfo.InvariantCulture),
                        CloseTime = reader.IsDBNull(3) ? null : reader.GetTimeSpan(3).ToString(@"hh\:mm", CultureInfo.InvariantCulture)
                    });
                    if (profile.BusinessHours.Count != 7) throw new InvalidOperationException("Business hours have not been initialized. Run Scripts/Sql/CreateSiteProfile.sql against this database.");
                    return profile;
                }
            }
        }

        public void Save(SiteProfileModel profile) {
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            Validator.ValidateObject(profile, new ValidationContext(profile), validateAllProperties: true);
            using (var connection = new SqlConnection(connectionString)) {
                connection.Open();
                using (var transaction = connection.BeginTransaction())
                using (var command = new SqlCommand(@"UPDATE dbo.SiteProfile SET CompanyName=@company, Email=@email, Phone=@phone,
StreetAddress=@street, AddressLine2=@line2, City=@city, State=@state, ZipCode=@zip, ResearchRadiusMiles=@radius,
DocumentaryFee=@fee, UpdatedUtc=SYSUTCDATETIME() WHERE Id=1", connection, transaction)) {
                    command.Parameters.Add("@company", SqlDbType.NVarChar, 120).Value = profile.CompanyName.Trim();
                    command.Parameters.Add("@email", SqlDbType.NVarChar, 254).Value = profile.Email.Trim();
                    command.Parameters.Add("@phone", SqlDbType.NVarChar, 30).Value = profile.Phone.Trim();
                    command.Parameters.Add("@street", SqlDbType.NVarChar, 160).Value = profile.StreetAddress.Trim();
                    command.Parameters.Add("@line2", SqlDbType.NVarChar, 120).Value = string.IsNullOrWhiteSpace(profile.AddressLine2) ? (object)DBNull.Value : profile.AddressLine2.Trim();
                    command.Parameters.Add("@city", SqlDbType.NVarChar, 80).Value = profile.City.Trim();
                    command.Parameters.Add("@state", SqlDbType.NVarChar, 2).Value = profile.State.ToUpperInvariant();
                    command.Parameters.Add("@zip", SqlDbType.VarChar, 5).Value = profile.ZipCode;
                    command.Parameters.Add("@radius", SqlDbType.Int).Value = profile.ResearchRadiusMiles;
                    command.Parameters.Add("@fee", SqlDbType.Int).Value = profile.DocumentaryFee;
                    if (command.ExecuteNonQuery() != 1) throw new InvalidOperationException("The company profile has not been initialized.");
                    using (var hoursCommand = new SqlCommand(@"UPDATE dbo.SiteProfileBusinessHours SET IsClosed=@closed, OpenTime=@opens, CloseTime=@closes
WHERE ProfileId=1 AND DayOfWeek=@day", connection, transaction)) {
                        var closedParameter = hoursCommand.Parameters.Add("@closed", SqlDbType.Bit);
                        var dayParameter = hoursCommand.Parameters.Add("@day", SqlDbType.TinyInt);
                        var opensParameter = hoursCommand.Parameters.Add("@opens", SqlDbType.Time);
                        var closesParameter = hoursCommand.Parameters.Add("@closes", SqlDbType.Time);
                        foreach (var hours in profile.BusinessHours) {
                            TimeSpan opens, closes;
                            hours.TryTimes(out opens, out closes);
                            closedParameter.Value = hours.IsClosed;
                            dayParameter.Value = hours.DayOfWeek;
                            opensParameter.Value = hours.IsClosed ? (object)DBNull.Value : opens;
                            closesParameter.Value = hours.IsClosed ? (object)DBNull.Value : closes;
                            if (hoursCommand.ExecuteNonQuery() != 1) throw new InvalidOperationException("Business hours have not been initialized.");
                        }
                    }
                    transaction.Commit();
                }
            }
            HttpContext.Current?.Items.Remove(RequestCacheKey);
        }
    }
}
