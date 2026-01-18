using System.ComponentModel.DataAnnotations;using System.ComponentModel.DataAnnotations.Schema;namespace RuntimeStuff.MSTests.DTO{    [Table("Profiles", Schema = "dbo")]
    public class Profiles    {        [Key]        [Column("ProfileID")]
        public int ProfileID { get; set; }        [ForeignKey("Users")]        [Column("UserID")]
        public int? UserID { get; set; }        [Column("Bio")]
        public string? Bio { get; set; }    }}