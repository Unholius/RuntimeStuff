using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RuntimeStuff.MSTests.Models;

[Table("TestTable")]
public class DtoTestClass
{
    public DtoTestClass()
    {
    }

    public DtoTestClass(int seed)
    {
        ColBigInt = seed * 1_000_000;
        ColSmallInt = (short)(seed % short.MaxValue);
        ColTinyInt = (byte)(seed % byte.MaxValue);
        ColBit = (seed % 2) == 0;
        ColDecimal = Convert.ToDecimal(seed) + 0.1m;
        ColNumeric = Convert.ToDecimal(seed) + 0.2m;
        ColMoney = Convert.ToDecimal(seed) + 0.3m;
        ColSmallMoney = Convert.ToDecimal(seed) + 0.4m;
        ColFloat = Convert.ToDouble(seed) + 0.5;
        ColReal = Convert.ToSingle(seed) + 0.6f;
        ColDate = new DateTime(2000 + seed % 30, 1 + seed % 12, 1 + seed % 28);
        ColTime = new TimeSpan(0, seed % 24, seed % 60, seed % 60);
        ColSmallDateTime = ColDate = ColDateTime2 = ColDateTime = DateTime.Now;
        ColDecimal = Convert.ToDecimal(seed);
        ColDateTimeOffset = new DateTimeOffset(ColDateTime.Value);
        ColChar = $"C{seed}";
        ColVarChar = $"VarChar{seed}";
        ColVarCharMax = new string('X', seed % 4000);
        ColNChar = $"NChar{seed}";
        ColNVarChar = $"NVarChar{seed}";
        ColNVarCharMax = new string('Y', seed % 4000);
        ColBinary = [(byte)(seed % 256), (byte)((seed + 1) % 256)];
        ColVarBinary = [(byte)(seed % 256), (byte)((seed + 1) % 256), (byte)((seed + 2) % 256)];
        ColVarBinaryMax = new byte[seed % 8000];
        ColUniqueIdentifier = Guid.NewGuid();
        ColXml = $"<root><value>{seed}</value></root>";
        ColJson = $"{{ \"value\": {seed} }}";
        ColSqlVariant = seed % 2 == 0 ? seed : $"Str{seed}";
        ColNullableInt = seed % 3 == 0 ? null : seed;
    }

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

    public byte[] ColRowVersion { get; set; } = [];

    public string? ColXml { get; set; }
    public string? ColJson { get; set; }

    public object? ColSqlVariant { get; set; }

    // Computed column (только чтение)
    public int? ColComputed { get; private set; }

    // Nullable пример
    public int? ColNullableInt { get; set; }
}