using System.ComponentModel.DataAnnotations;using System.ComponentModel.DataAnnotations.Schema;namespace RuntimeStuff.MSTests.DTO{    [Table("Books", Schema = "dbo")]
    public class Books    {        [Key]        [Column("BookID")]
        public int BookID { get; set; }        [ForeignKey("Authors")]        [Column("AuthorID")]
        public int? AuthorID { get; set; }        [Column("Title")]
        public string? Title { get; set; }        public Authors? Author { get; set; }    }}