using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RuntimeStuff.MSTests.DTO.SQLite
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
}