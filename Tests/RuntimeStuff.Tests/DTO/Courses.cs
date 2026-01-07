using System.ComponentModel.DataAnnotations;using System.ComponentModel.DataAnnotations.Schema;namespace RuntimeStuff.MSTests.DTO{    [Table("Courses", Schema = "dbo")]
    public class Courses    {        [Key]        [Column("CourseID")]
        public int CourseID { get; set; }        [Column("CourseName")]
        public string CourseName { get; set; }    }}