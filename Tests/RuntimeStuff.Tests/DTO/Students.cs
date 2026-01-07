using System.ComponentModel.DataAnnotations;using System.ComponentModel.DataAnnotations.Schema;namespace RuntimeStuff.MSTests.DTO{    [Table("Students", Schema = "dbo")]
    public class Students    {        [Key]        [Column("StudentID")]
        public int StudentID { get; set; }        [Column("Name")]
        public string Name { get; set; }    }}