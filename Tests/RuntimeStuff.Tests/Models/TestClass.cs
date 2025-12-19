using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RuntimeStuff.MSTests.Models;

[Table("TestTable")]
public class DtoTestClass
{
    // Числовые типы
    [Key] public int IdInt { get; set; } = -1;
    public long? ColBigInt { get; set; }
    public short? ColSmallInt { get; set; }
    public byte? ColTinyInt { get; set; }
    public bool? ColBit { get; set; }

    public decimal? ColDecimal { get; set; }
    public decimal? ColNumeric { get; set; }
    public decimal? ColMoney { get; set; }
    public decimal? ColSmallMoney { get; set; }
    public double? ColFloat { get; set; }
    public float? ColReal { get; set; }

    // Дата и время
    public DateTime? ColDate { get; set; }
    public TimeSpan? ColTime { get; set; }
    public DateTime? ColDateTime { get; set; }
    public DateTime? ColDateTime2 { get; set; }
    public DateTime? ColSmallDateTime { get; set; }
    public DateTimeOffset? ColDateTimeOffset { get; set; }

    // Строки
    public string? ColChar { get; set; }
    public string? ColVarChar { get; set; }
    public string? ColVarCharMax { get; set; }

    public string? ColNChar { get; set; }
    public string? ColNVarChar { get; set; }
    public string? ColNVarCharMax { get; set; }

    // Бинарные
    public byte[]? ColBinary { get; set; }
    public byte[]? ColVarBinary { get; set; }
    public byte[]? ColVarBinaryMax { get; set; }

    // Специальные
    public Guid ColUniqueIdentifier { get; set; }
    public byte[] ColRowVersion { get; set; } = Array.Empty<byte>();

    public string? ColXml { get; set; }
    public string? ColJson { get; set; }

    public object? ColSqlVariant { get; set; }

    // Computed column (только чтение)
    public int? ColComputed { get; private set; }

    // Nullable пример
    public int? ColNullableInt { get; set; }
}
