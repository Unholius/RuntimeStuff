using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RuntimeStuff.MSTests.DTO.SQLite
{
    [Table("users")]
    public class User
    {
        [Key]
        public long Id { get; set; }
        public string Name { get; set; }
        public UserProfile UserProfile { get; set; }
    }
}
