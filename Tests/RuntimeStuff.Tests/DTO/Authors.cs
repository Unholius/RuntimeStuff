using System.ComponentModel.DataAnnotations;using System.ComponentModel.DataAnnotations.Schema;namespace RuntimeStuff.MSTests.DTO{    [Table("Authors", Schema = "dbo")]
    public class Authors    {        [Key]        [Column("AuthorID")]
        public int AuthorID { get; set; }        [Column("Name")]
        public string Name { get; set; }        [ForeignKey("AuthorID")]
        public IEnumerable<Books> Books { get; set; }    }}