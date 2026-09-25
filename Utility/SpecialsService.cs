using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using GTX.Models;
using Services;

namespace GTX
{
    public interface ISpecialsService
    {
        List<SpecialModel> GetAll(bool includeUnpublished = false);
        SpecialModel GetById(int id);
        bool Save(SpecialModel special);
        bool Delete(int id);
    }

    // Uses the same configured database as Blogs, but an independent table.
    public sealed class SpecialsService : BaseService, ISpecialsService
    {
        private const string Columns = "Id, Title, CardContent, IsPublished, CreatedAt";

        public List<SpecialModel> GetAll(bool includeUnpublished = false)
        {
            using (var connection = new SqlConnection(connectionString))
            using (var command = new SqlCommand("SELECT " + Columns + " FROM dbo.Specials WHERE (@all = 1 OR IsPublished = 1) ORDER BY CreatedAt DESC, Id DESC", connection))
            {
                command.Parameters.Add("@all", SqlDbType.Bit).Value = includeUnpublished;
                connection.Open();
                using (var reader = command.ExecuteReader())
                {
                    var result = new List<SpecialModel>();
                    while (reader.Read()) result.Add(Read(reader));
                    return result;
                }
            }
        }

        public SpecialModel GetById(int id)
        {
            using (var connection = new SqlConnection(connectionString))
            using (var command = new SqlCommand("SELECT " + Columns + " FROM dbo.Specials WHERE Id = @id", connection))
            {
                command.Parameters.Add("@id", SqlDbType.Int).Value = id;
                connection.Open();
                using (var reader = command.ExecuteReader()) return reader.Read() ? Read(reader) : null;
            }
        }

        public bool Save(SpecialModel special)
        {
            var sql = special.Id == 0
                ? "INSERT dbo.Specials (Title, CardContent, IsPublished) VALUES (@title, @card, @published)"
                : "UPDATE dbo.Specials SET Title=@title, CardContent=@card, IsPublished=@published WHERE Id=@id";
            using (var connection = new SqlConnection(connectionString))
            using (var command = new SqlCommand(sql, connection))
            {
                command.Parameters.Add("@id", SqlDbType.Int).Value = special.Id;
                command.Parameters.Add("@title", SqlDbType.NVarChar, 200).Value = special.Title;
                command.Parameters.Add("@card", SqlDbType.NVarChar, -1).Value = special.CardContent;
                command.Parameters.Add("@published", SqlDbType.Bit).Value = special.IsPublished;
                connection.Open();
                return command.ExecuteNonQuery() == 1;
            }
        }

        public bool Delete(int id)
        {
            using (var connection = new SqlConnection(connectionString))
            using (var command = new SqlCommand("DELETE dbo.Specials WHERE Id=@id", connection))
            {
                command.Parameters.Add("@id", SqlDbType.Int).Value = id;
                connection.Open();
                return command.ExecuteNonQuery() == 1;
            }
        }

        private static SpecialModel Read(SqlDataReader reader)
        {
            return new SpecialModel {
                Id = reader.GetInt32(0), Title = reader.GetString(1),
                CardContent = reader.GetString(2),
                IsPublished = reader.GetBoolean(3), CreatedAt = reader.GetDateTime(4)
            };
        }
    }
}
