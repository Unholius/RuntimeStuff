using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace System.MSTests.DTO.SQLite
{
    [Table("users")]
    public class User
    {
        [Key]
        public long Id { get; set; }

        public string? Name { get; set; }
        public UserProfile? UserProfile { get; set; }
        public Guid Guid { get; set; }
    }

    [Table("user_logs", Schema = "logs")]
    public class UserLogs
    {
        public DateTime Created { get; set; }
        public int UserId { get; set; }
        public string Message { get; set; }
    }
}