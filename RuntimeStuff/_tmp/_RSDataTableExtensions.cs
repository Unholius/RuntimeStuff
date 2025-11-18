//using System;
//using System.Collections.Generic;
//using System.Data;
//using System.Linq;

//namespace RuntimeStuff.Extensions
//{
//    /// <summary>
//    /// Класс расширяющих методов для удобной работы с объектом DataTable.
//    /// Позволяет добавлять новые колонки, строки и импортировать данные из коллекций.
//    /// </summary>
//    public static class RSDataTableExtensions
//    {
//        /// <summary>
//        /// Добавляет новую колонку в DataTable.
//        /// Если колонка уже существует и флаг addCopyIfExists установлен в false, ничего не происходит.
//        /// </summary>
//        /// <param name="dt">Объект DataTable, к которому добавляется колонка.</param>
//        /// <param name="colName">Имя новой колонки.</param>
//        /// <param name="caption">Заголовок колонки (опционально).</param>
//        /// <param name="addCopyIfExists">
//        /// Флаг, указывающий, следует ли добавить копию колонки,
//        /// если она уже существует в таблице.</param>
//        /// <param name="colType">Тип данных колонки. Если не указан, по умолчанию используется строковый тип.</param>
//        /// <param name="isPrimaryKey">Флаг, указывающий, является ли колонка частью первичного ключа.</param>
//        /// <param name="isAutoIncrement">
//        /// Флаг, указывающий, следует ли автоматически инкрементировать значения в этой колонке.</param>
//        /// <returns>Тот же экземпляр DataTable для возможности цепочного вызова.</returns>
//        public static DataTable AddCol(
//            this DataTable dt,
//            string colName,
//            string caption = null,
//            bool addCopyIfExists = false,
//            Type colType = null,
//            bool isPrimaryKey = false,
//            bool isAutoIncrement = false)
//        {
//            if (dt.Columns.Contains(colName) && !addCopyIfExists)
//                return dt;

//            colName = colName.GetUniqueName(x => dt.Columns.Contains(x));
//            DataColumn col = dt.Columns.Add(colName, colType ?? typeof(string));
//            col.Caption = caption;
//            col.AutoIncrementSeed = 1;
//            col.AutoIncrementStep = 1;
//            col.AutoIncrement = isAutoIncrement;

//            if (isPrimaryKey)
//            {
//                col.AllowDBNull = false;
//                col.Unique = true;
//                dt.PrimaryKey = dt.PrimaryKey.Concat(new[] { col }).ToArray();
//            }

//            return dt;
//        }

//        /// <summary>
//        /// Добавляет новую строку в DataTable с указанными значениями в порядке колонок.
//        /// </summary>
//        /// <param name="dt">Объект DataTable, в который добавляется строка.</param>
//        /// <param name="values">Массив значений для ячеек новой строки.</param>
//        /// <returns>Тот же экземпляр DataTable для возможности цепочного вызова.</returns>
//        public static DataTable AddRow(this DataTable dt, params object[] values)
//        {
//            return AddRow(dt, out _, values);
//        }

//        /// <summary>
//        /// Добавляет новую строку в DataTable с указанными значениями в порядке колонок
//        /// и возвращает ссылку на добавленную строку.
//        /// </summary>
//        /// <param name="dt">Объект DataTable, в который добавляется строка.</param>
//        /// <param name="row">Выходной параметр, ссылка на добавленную строку.</param>
//        /// <param name="values">Массив значений для ячеек новой строки.</param>
//        /// <returns>Тот же экземпляр DataTable для возможности цепочного вызова.</returns>
//        public static DataTable AddRow(this DataTable dt, out DataRow row, params object[] values)
//        {
//            int valCount = Math.Min(dt.Columns.Count, values.Length);
//            row = dt.NewRow();
//            for (int i = 0; i < valCount; i++)
//            {
//                row[i] = values[i];
//            }

//            dt.Rows.Add(row);
//            return dt;
//        }

//        /// <summary>
//        /// Добавляет новую строку в DataTable, сопоставляя переданные значения заданным колонкам.
//        /// </summary>
//        /// <param name="dt">Объект DataTable, в который добавляется строка.</param>
//        /// <param name="columnNames">Массив имён колонок для соответствующих ячеек.</param>
//        /// <param name="values">Массив значений, сопоставляемых колонкам.</param>
//        /// <returns>Тот же экземпляр DataTable для возможности цепочного вызова.</returns>
//        public static DataTable AddRow(this DataTable dt, string[] columnNames, object[] values)
//        {
//            return AddRow(dt, columnNames, values, out _);
//        }

//        /// <summary>
//        /// Добавляет новую строку в DataTable, сопоставляя переданные значения заданным колонкам,
//        /// и возвращает ссылку на добавленную строку.
//        /// </summary>
//        /// <param name="dt">Объект DataTable, в который добавляется строка.</param>
//        /// <param name="columnNames">Массив имён колонок для соответствующих ячеек.</param>
//        /// <param name="values">Массив значений, сопоставляемых колонкам.</param>
//        /// <param name="row">Выходной параметр, ссылка на добавленную строку.</param>
//        /// <returns>Тот же экземпляр DataTable для возможности цепочного вызова.</returns>
//        public static DataTable AddRow(
//            this DataTable dt,
//            string[] columnNames,
//            object[] values,
//            out DataRow row)
//        {
//            row = dt.NewRow();
//            for (int i = 0; i < columnNames.Length; i++)
//            {
//                row[columnNames[i]] = values[i];
//            }

//            dt.Rows.Add(row);
//            return dt;
//        }

//        /// <summary>
//        /// Импортирует данные из коллекции объектов в DataTable.
//        /// Для каждого свойства объекта, которое является базовым типом и публичным,
//        /// добавляется соответствующая колонка в таблицу, затем заполняются строки.
//        /// </summary>
//        /// <typeparam name="T">Тип элементов коллекции.</typeparam>
//        /// <param name="dt">Объект DataTable, в который импортируются данные.</param>
//        /// <param name="items">Коллекция объектов для импорта.</param>
//        public static void ImportData<T>(this DataTable dt, IEnumerable<T> items) where T : class
//        {
//            var typeInfo = typeof(T).GetMemberInfoEx();
//            dt.BeginLoadData();

//            // Добавляем колонки на основе свойств типа T
//            foreach (var p in typeInfo.Members.Where(x => x.IsProperty && x.IsPublic && x.IsBasic))
//            {
//                dt.AddCol(p.Name, p.DisplayName ?? p.Description, false, p.Type, p.IsPrimaryKey);
//            }

//            // Добавляем строки с данными
//            foreach (T item in items)
//            {
//                if (item == null)
//                    continue;
//                dt.AddRow(Obj.GetValues(item));
//            }

//            dt.EndLoadData();
//            dt.AcceptChanges();
//        }

//        /// <summary>
//        /// Преобразует DataTable в список объектов указанного типа T.
//        /// </summary>
//        /// <typeparam name="T">Тип элемента в списке</typeparam>
//        /// <param name="dt">Таблица</param>
//        /// <param name="mapper">Сопоставление имен колонок в таблице к именам свойств объекта. Можно указывать путь до вложенных свойств через точку. Дочерние экземпляры объектов будут созданы автоматически.</param>
//        /// <param name="fastSet">Использовать ли быстрый способ установки значений без вложенных свойств и конвертации типов</param>
//        /// <returns></returns>
//        public static List<T> ToList<T>(this DataTable dt, IDictionary<string, string> mapper = null,
//            bool fastSet = false) where T : class, new()
//        {
//            if (dt == null)
//                throw new ArgumentNullException(nameof(dt));

//            if (mapper == null)
//            {
//                mapper = new Dictionary<string, string>();
//                foreach (DataColumn col in dt.Columns)
//                {
//                    mapper[col.ColumnName] = col.ColumnName;
//                }
//            }

//            var columns = new List<(DataColumn, MemberInfoEx)>();
//            foreach (var kvp in mapper)
//            {
//                var m = Obj.GetMember<T>(kvp.Item);
//                if (m == null)
//                    continue;
//                columns.Add((dt.Columns[kvp.Key], m));
//            }

//            var list = new List<T>(dt.Rows.Count);
//            //Action<object, MemberInfoEx, object> action = fastSet ? (Action<object, MemberInfoEx, object>)(object o, MemberInfoEx mi, object value) => mi.Setter(o, value) : (object o, MemberInfoEx mi, object value) => Obj.Set(o, mapper[mi.ColumnName], value);
//            if (fastSet)
//            {
//                foreach (DataRow row in dt.Rows)
//                {
//                    var item = new T();
//                    foreach (var col in columns)
//                    {
//                        col.Item2.Setter(item, row[col.Item1]);
//                    }

//                    list.Add(item);
//                }
//            }
//            else
//            {
//                foreach (DataRow row in dt.Rows)
//                {
//                    var item = new T();
//                    foreach (var col in columns)
//                    {
//                        Obj.Set(item, mapper[col.Item1.ColumnName], row[col.Item1]);
//                    }

//                    list.Add(item);
//                }
//            }

//            return list;
//        }
//    }
//}
