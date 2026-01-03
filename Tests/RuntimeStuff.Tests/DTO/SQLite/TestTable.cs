using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RuntimeStuff.MSTests.DTO.SQLite
{
    public sealed class TestTable
    {
        public long Id { get; set; }
        public long IntValue { get; set; }
        public double RealValue { get; set; }
        public decimal NumericValue { get; set; }
        public string TextValue { get; set; } = null!;
        public byte[] BlobValue { get; set; } = null!;
        public bool BooleanValue { get; set; }
        public DateTime DateValue { get; set; }
        public DateTime DateTimeValue { get; set; }
        public DateTime TimeValue { get; set; }
        public decimal DecimalValue { get; set; }
        public string JsonValue { get; set; } = null!;
        public string? NullableValue { get; set; }
        public string NotNullValue { get; set; } = null!;
        public DateTime CreatedAt { get; set; }
    }

}
