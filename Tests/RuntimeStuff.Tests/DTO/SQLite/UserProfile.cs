using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RuntimeStuff.MSTests.DTO.SQLite
{
    [Table("user_profiles")]
    public class UserProfile
    {
        [Key]
        [ForeignKey(nameof(User))]
        [Column("user_id")]
        public long UserId { get; set; }

        [Column("bio")]
        public string Bio { get; set; }

        [Column("avatar_url")]
        public Uri AvatarUrl { get; set; }

        public User User { get; set; }
    }
}