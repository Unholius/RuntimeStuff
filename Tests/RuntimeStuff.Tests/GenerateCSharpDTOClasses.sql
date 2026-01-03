DECLARE @namespace NVARCHAR(100) = 'RuntimeStuff.MSTests.DTO';
WITH cols AS (
    SELECT 
        TABLE_SCHEMA,
        TABLE_NAME,
        COLUMN_NAME,
        DATA_TYPE,
        MAX_LENGTH,
        IS_NULLABLE,
        IS_PK,
        IS_FK,
        REFERENCED_TABLE_NAME,
        REFERENCED_COLUMN_NAME,
        COLUMN_DESCRIPTION,
        ORDINAL_POSITION,
		CASE 
            WHEN dbo.fn_ToPascalCase(COLUMN_NAME) = dbo.fn_ToPascalCase(TABLE_NAME)
                THEN dbo.fn_ToPascalCase(COLUMN_NAME) + 'Prop'
            ELSE dbo.fn_ToPascalCase(COLUMN_NAME)
        END AS COLUMN_NAME_PASCAL,
		dbo.fn_ToPascalCase(TABLE_NAME) AS TABLE_NAME_PASCAL
    FROM v_TableColumnInfo
	WHERE 
        IS_VIEW = 0
        AND TABLE_NAME NOT LIKE '%[_]Deleted'
),
tables AS (
    SELECT DISTINCT TABLE_SCHEMA, TABLE_NAME, TABLE_NAME_PASCAL
    FROM cols
)
SELECT 
	t.TABLE_NAME + '.cs' AS [FILE_NAME],
    'using System;' + CHAR(13) +
    'using System.ComponentModel.DataAnnotations;' + CHAR(13) +
    'using System.ComponentModel.DataAnnotations.Schema;' + CHAR(13) +
    CHAR(13) +
    'namespace '+ @namespace + CHAR(13) +
    '{' + CHAR(13) +
    '    [Table("' + t.TABLE_NAME + '", Schema = "' + t.TABLE_SCHEMA + '")]' + CHAR(13) +
    '    public class ' + t.TABLE_NAME_PASCAL + CHAR(13) +
    '    {' + CHAR(13) +
    STRING_AGG(

        CASE WHEN c.COLUMN_DESCRIPTION IS NOT NULL 
            THEN '        /// <summary>' + CHAR(13) +
                 '        /// ' + REPLACE(c.COLUMN_DESCRIPTION, CHAR(13), ' ') + CHAR(13) +
                 '        /// </summary>' + CHAR(13)
            ELSE '' END +

        CASE WHEN c.IS_PK = 1 
            THEN '        [Key]' + CHAR(13)
            ELSE '' END +

        CASE WHEN c.IS_FK = 1 
            THEN '        [ForeignKey("' + c.REFERENCED_TABLE_NAME + '")]' + CHAR(13)
            ELSE '' END +

        '        [Column("' + c.COLUMN_NAME + '")]' + CHAR(13) +

        '        public ' +
            CASE 
                WHEN c.DATA_TYPE IN ('varchar','nvarchar','char','nchar','text','ntext') THEN 'string'
                WHEN c.DATA_TYPE = 'tinyint' THEN 
                    CASE WHEN c.IS_NULLABLE = 1 THEN 'byte?' ELSE 'byte' END
                WHEN c.DATA_TYPE = 'smallint' THEN 
                    CASE WHEN c.IS_NULLABLE = 1 THEN 'short?' ELSE 'short' END
                WHEN c.DATA_TYPE IN ('int') THEN 
                    CASE WHEN c.IS_NULLABLE = 1 THEN 'int?' ELSE 'int' END
                WHEN c.DATA_TYPE = 'bigint' THEN 
                    CASE WHEN c.IS_NULLABLE = 1 THEN 'long?' ELSE 'long' END
                WHEN c.DATA_TYPE IN ('decimal','numeric','money','smallmoney') THEN 
                    CASE WHEN c.IS_NULLABLE = 1 THEN 'decimal?' ELSE 'decimal' END
                WHEN c.DATA_TYPE = 'float' THEN 
                    CASE WHEN c.IS_NULLABLE = 1 THEN 'double?' ELSE 'double' END
                WHEN c.DATA_TYPE = 'real' THEN 
                    CASE WHEN c.IS_NULLABLE = 1 THEN 'float?' ELSE 'float' END
                WHEN c.DATA_TYPE IN ('bit') THEN 
                    CASE WHEN c.IS_NULLABLE = 1 THEN 'bool?' ELSE 'bool' END
                WHEN c.DATA_TYPE IN ('datetime','datetime2','smalldatetime','date','time') THEN
                    CASE WHEN c.IS_NULLABLE = 1 THEN 'DateTime?' ELSE 'DateTime' END
                WHEN c.DATA_TYPE = 'uniqueidentifier' THEN 
                    CASE WHEN c.IS_NULLABLE = 1 THEN 'Guid?' ELSE 'Guid' END
                WHEN c.DATA_TYPE IN ('binary','varbinary') THEN 'byte[]'
                ELSE 'string'
            END +
        ' ' + c.COLUMN_NAME_PASCAL + ' { get; set; }' + CHAR(13),

        CHAR(13)
    ) WITHIN GROUP (
    	ORDER BY 
    	CASE WHEN c.IS_PK = 1 THEN 0 
             WHEN c.IS_FK = 1 THEN 1
             ELSE 2 END,
        c.COLUMN_NAME_PASCAL
    ) +
    '    }' + CHAR(13) +
    '}' AS FILE_CONTENT
FROM tables t
JOIN cols c 
    ON c.TABLE_SCHEMA = t.TABLE_SCHEMA
   AND c.TABLE_NAME = t.TABLE_NAME
GROUP BY t.TABLE_SCHEMA, t.TABLE_NAME, t.TABLE_NAME_PASCAL
ORDER BY t.TABLE_SCHEMA, t.TABLE_NAME;