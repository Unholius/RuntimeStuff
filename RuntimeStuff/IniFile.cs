// ***********************************************************************
// Assembly         : RuntimeStuff
// Author           : RS
// Created          : 01-06-2026
//
// Last Modified By : RS
// Last Modified On : 01-07-2026
// ***********************************************************************
// <copyright file="IniFile.cs" company="Rudnev Sergey">
// Copyright (c) Rudnev Sergey. All rights reserved.
// </copyright>
// <summary></summary>
// ***********************************************************************
namespace RuntimeStuff
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Text;
    using System.Text.RegularExpressions;

    /// <summary>
    /// Класс для работы с INI-файлами (конфигурационные файлы в формате Windows).
    /// Предоставляет методы для чтения, записи и управления секциями, ключами и значениями.
    /// Поддерживает автоматическое определение кодировки, управление типами данных,
    /// экранирование символов и интеграцию со свойствами классов через рефлексию.
    /// </summary>
    /// <remarks>
    /// Пример использования:
    /// <code>
    /// // Загрузка файла
    /// var ini = IniFile.Load("config.ini");
    ///
    /// // Чтение значения
    /// string value = ini.ReadString("Section1", "Key1", "Default");
    ///
    /// // Запись значения
    /// ini.WriteString("Section1", "Key2", "NewValue");
    ///
    /// // Сохранение
    /// ini.Save("config_modified.ini");
    /// </code>
    /// </remarks>
    public sealed class IniFile
    {
        private static readonly char[] InvalidPathChars = Path.GetInvalidPathChars();

        private static readonly Regex Regex = new Regex(
                    @"(?=\S)(?<text>(?<comment>(?<open>[#;]+)(?:[^\S\r\n]*)(?<value>.+))|" +
            @"(?<section>(?<open>\[)(?:\s*)(?<value>[^\]]*\S+)(?:[^\S\r\n]*)(?<close>\]))|" +
            @"(?<entry>(?<key>[^=\r\n\[\]]*\S)(?:[^\S\r\n]*)(?<delimiter>:|=)(?:[^\S\r\n]*)(?<value>[^#;\r\n]*))|" +
            @"(?<undefined>.+))(?<=\S)|" +
            @"(?<linebreaker>\r\n|\n)|" +
            @"(?<whitespace>[^\S\r\n]+)", RegexOptions.Compiled);

        private readonly bool allowEscapeChars;
        private readonly StringComparison comparison;
        private readonly string fileName;
        private readonly string lineBreaker;
        private bool cacheDirty = true;
        private string content;
        private Dictionary<string, HashSet<string>> keyCache;
        private HashSet<string> sectionCache;
        private Dictionary<string, Dictionary<string, List<string>>> valueCache;
        private Dictionary<string, Dictionary<string, List<(int index, int length)>>> writeIndex;

        /// <summary>
        /// Initializes a new instance of the <see cref="IniFile"/> class.
        /// Основной приватный конструктор. Инициализирует объект с содержимым INI-файла.
        /// </summary>
        /// <param name="content">Содержимое ini.</param>
        /// <param name="comparison">Правила сравнения строк (регистрозависимость).</param>
        /// <param name="allowEscChars">Разрешить экранирование символов в значениях.</param>
        public IniFile(
            string content,
            StringComparison comparison = StringComparison.InvariantCultureIgnoreCase,
            bool allowEscChars = false)
        {
            this.content = content;
            this.comparison = comparison;
            this.allowEscapeChars = allowEscChars;
            this.lineBreaker = AutoDetectLineBreaker(this.content);
            this.fileName = Assembly.GetExecutingAssembly().GetName().Name + ".ini";
        }

        /// <summary>
        /// Gets or sets содержимое INI-файла в виде строки.
        /// При установке значения null преобразуется в пустую строку.
        /// </summary>
        public string Content
        {
            get => this.content ?? (this.content = string.Empty);

            set
            {
                this.content = value ?? string.Empty;
                this.cacheDirty = true;
            }
        }

        /// <summary>
        /// Индексатор для быстрого чтения/записи строковых значений.
        /// </summary>
        /// <param name="section">Имя секции. Может быть null для глобальных ключей.</param>
        /// <param name="key">Имя ключа.</param>
        /// <returns>Значение ключа или пустая строка, если ключ не найден.</returns>
        public string this[string section, string key]
        {
            get => this.GetValue(section, key, string.Empty);
            set => this.SetValue(section, key, value);
        }

        /// <summary>
        /// Индексатор только для чтения с указанием значения по умолчанию.
        /// </summary>
        /// <param name="section">Имя секции. Может быть null для глобальных ключей.</param>
        /// <param name="key">Имя ключа.</param>
        /// <param name="defaultValue">Значение по умолчанию, если ключ не найден.</param>
        /// <returns>Значение ключа или defaultValue, если ключ не найден.</returns>
        public string this[string section, string key, string defaultValue] => this.GetValue(section, key, defaultValue);

        /// <summary>
        /// Загружает INI-файл из TextReader.
        /// </summary>
        /// <param name="reader">TextReader для чтения содержимого.</param>
        /// <param name="comparison">Правила сравнения строк.</param>
        /// <param name="allowEscChars">Разрешить экранирование символов.</param>
        /// <returns>Новый экземпляр IniFile с загруженным содержимым.</returns>
        public static IniFile Load(
            TextReader reader,
            StringComparison comparison = StringComparison.InvariantCultureIgnoreCase,
            bool allowEscChars = false)
        {
            return new IniFile(reader.ReadToEnd(), comparison, allowEscChars);
        }

        /// <summary>
        /// Загружает INI-файл из потока (Stream).
        /// </summary>
        /// <param name="stream">Поток для чтения.</param>
        /// <param name="encoding">Кодировка файла. Если null, определяется автоматически или используется UTF-8.</param>
        /// <param name="comparison">Правила сравнения строк.</param>
        /// <param name="allowEscChars">Разрешить экранирование символов.</param>
        /// <returns>Новый экземпляр IniFile с загруженным содержимым.</returns>
        /// <exception cref="ArgumentNullException">Если stream равен null.</exception>
        public static IniFile Load(Stream stream, Encoding encoding = null, StringComparison comparison = StringComparison.InvariantCultureIgnoreCase, bool allowEscChars = false)
        {
            using (var reader = new StreamReader(stream ?? throw new ArgumentNullException(nameof(stream)), encoding ?? Encoding.UTF8))
            {
                return new IniFile(reader.ReadToEnd(), comparison, allowEscChars);
            }
        }

        /// <summary>
        /// Загружает INI-файл с диска по указанному имени файла.
        /// </summary>
        /// <param name="fileName">Путь к файлу.</param>
        /// <param name="encoding">Кодировка файла. Если null, определяется автоматически.</param>
        /// <param name="comparison">Правила сравнения строк.</param>
        /// <param name="allowEscChars">Разрешить экранирование символов.</param>
        /// <returns>Новый экземпляр IniFile с загруженным содержимым.</returns>
        /// <exception cref="ArgumentException">Если fileName некорректен.</exception>
        /// <exception cref="FileNotFoundException">Если файл не существует.</exception>
        public static IniFile Load(
            string fileName,
            Encoding encoding,
            StringComparison comparison = StringComparison.InvariantCultureIgnoreCase,
            bool allowEscChars = false)
        {
            var filePath = GetFullPath(fileName, true);
            return new IniFile(
                File.ReadAllText(filePath, encoding ?? AutoDetectEncoding(fileName)),
                comparison,
                allowEscChars);
        }

        /// <summary>
        /// Загружает INI-файл с диска по указанному имени файла с автоматическим определением кодировки.
        /// </summary>
        /// <param name="fileName">Путь к файлу.</param>
        /// <param name="comparison">Правила сравнения строк.</param>
        /// <param name="allowEscChars">Разрешить экранирование символов.</param>
        /// <returns>Новый экземпляр IniFile с загруженным содержимым.</returns>
        /// <exception cref="ArgumentException">Если fileName некорректен.</exception>
        /// <exception cref="FileNotFoundException">Если файл не существует.</exception>
        public static IniFile Load(
            string fileName,
            StringComparison comparison = StringComparison.InvariantCultureIgnoreCase,
            bool allowEscChars = false)
        {
            var filePath = GetFullPath(fileName, true);

            return new IniFile(
                File.ReadAllText(filePath),
                comparison,
                allowEscChars);
        }

        /// <summary>
        /// Загружает INI-файл, если он существует, или создает новый пустой объект.
        /// </summary>
        /// <param name="fileName">Путь к файлу.</param>
        /// <param name="encoding">Кодировка файла. Если null, определяется автоматически.</param>
        /// <param name="comparison">Правила сравнения строк.</param>
        /// <param name="allowEscChars">Разрешить экранирование символов.</param>
        /// <returns>Новый экземпляр IniFile. Если файл не существует, возвращается пустой объект.</returns>
        /// <exception cref="ArgumentException">Если fileName некорректен.</exception>
        public static IniFile LoadOrCreate(string fileName, Encoding encoding, StringComparison comparison = StringComparison.InvariantCultureIgnoreCase, bool allowEscChars = false)
        {
            var filePath = GetFullPath(fileName);

            return new IniFile(
                File.Exists(filePath)
                    ? File.ReadAllText(filePath, encoding ?? AutoDetectEncoding(filePath, Encoding.UTF8))
                    : string.Empty,
                comparison,
                allowEscChars);
        }

        /// <summary>
        /// Загружает INI-файл, если он существует, или создает новый файл и пустой объект.
        /// </summary>
        /// <param name="fileName">Путь к файлу.</param>
        /// <param name="comparison">Правила сравнения строк.</param>
        /// <param name="allowEscChars">Разрешить экранирование символов.</param>
        /// <returns>Новый экземпляр IniFile. Если файл не существовал, он будет создан.</returns>
        /// <exception cref="ArgumentException">Если fileName некорректен.</exception>
        public static IniFile LoadOrCreate(
            string fileName,
            StringComparison comparison = StringComparison.InvariantCultureIgnoreCase,
            bool allowEscChars = false)
        {
            var filePath = GetFullPath(fileName);

            if (!File.Exists(filePath))
            {
                File.WriteAllText(filePath, string.Empty);
            }

            return new IniFile(
                File.ReadAllText(filePath),
                comparison,
                allowEscChars);
        }

        /// <summary>
        /// Возвращает перечисление ключей в указанной секции.
        /// </summary>
        /// <param name="section">Имя секции. Если null, возвращаются ключи глобальной секции.</param>
        /// <returns>Перечисление имен ключей.</returns>
        public IEnumerable<string> GetKeys(string section)
        {
            this.EnsureCache();

            if (section == null)
            {
                section = string.Empty;
            }

            return this.keyCache.TryGetValue(section, out var keys) ? keys : Enumerable.Empty<string>();
        }

        /// <summary>
        /// Возвращает перечисление всех секций в файле.
        /// </summary>
        /// <returns>Перечисление имен секций.</returns>
        public IEnumerable<string> GetSections()
        {
            this.EnsureCache();

            foreach (var section in this.sectionCache)
            {
                // пропускаем пустую секцию, если в ней нет ключей
                if (section.Length == 0 && (!this.keyCache.TryGetValue(section, out var keys) || keys.Count == 0))
                {
                    continue;
                }

                yield return section;
            }
        }

        /// <summary>
        /// Получает строковое значение ключа.
        /// </summary>
        /// <param name="section">Имя секции. Может быть null для глобальных ключей.</param>
        /// <param name="key">Имя ключа.</param>
        /// <param name="defaultValue">Значение по умолчанию.</param>
        /// <returns>Значение ключа или defaultValue.</returns>
        public string GetValue(string section, string key, string defaultValue = null)
        {
            if (key == null)
            {
                throw new ArgumentNullException(nameof(key));
            }

            this.EnsureCache();

            if (section == null)
            {
                section = string.Empty;
            }

            if (this.valueCache.TryGetValue(section, out var keys) &&
                keys.TryGetValue(key, out var values) &&
                values.Count > 0)
            {
                return values[values.Count - 1]; // последний wins
            }

            return defaultValue;
        }

        /// <summary>
        /// Сохраняет содержимое INI-файла в файл на диске.
        /// </summary>
        /// <param name="encoding">Кодировка для записи. Если null, используется UTF-8.</param>
        /// <exception cref="ArgumentException">Если fileName некорректен.</exception>
        public void Save(Encoding encoding = null)
        {
            var fullPath = GetFullPath(this.fileName);
            File.WriteAllText(fullPath, this.Content, encoding ?? Encoding.UTF8);
        }

        /// <summary>
        /// Сохраняет содержимое INI-файла в TextWriter.
        /// </summary>
        /// <param name="writer">TextWriter для записи.</param>
        public void SaveAs(TextWriter writer)
        {
            writer.Write(this.Content);
        }

        /// <summary>
        /// Сохраняет содержимое INI-файла в поток (Stream).
        /// </summary>
        /// <param name="stream">Поток для записи.</param>
        /// <param name="encoding">Кодировка для записи. Если null, используется UTF-8.</param>
        public void SaveAs(Stream stream, Encoding encoding = null)
        {
            using (var writer = new StreamWriter(stream, encoding ?? Encoding.UTF8))
            {
                writer.Write(this.Content);
            }
        }

        /// <summary>
        /// Сохраняет содержимое INI-файла в файл на диске.
        /// </summary>
        /// <param name="newFileName">Путь к файлу.</param>
        /// <param name="encoding">Кодировка для записи. Если null, используется UTF-8.</param>
        /// <exception cref="ArgumentException">Если fileName некорректен.</exception>
        public void SaveAs(string newFileName, Encoding encoding = null)
        {
            var fullPath = GetFullPath(newFileName);
            File.WriteAllText(fullPath, this.Content, encoding ?? Encoding.UTF8);
        }

        /// <summary>
        /// Устанавливает строковое значение ключа.
        /// </summary>
        /// <param name="section">Имя секции. Может быть null для глобальных ключей.</param>
        /// <param name="key">Имя ключа.</param>
        /// <param name="value">Значение ключа.</param>
        public void SetValue(string section, string key, string value)
        {
            this.EnsureCache();

            if (section == null)
            {
                section = string.Empty;
            }

            if (this.allowEscapeChars && value != null)
            {
                value = ToEscape(value);
            }

            if (this.writeIndex.TryGetValue(section, out var keys) &&
                keys.TryGetValue(key, out var positions) &&
                positions.Count > 0)
            {
                // заменяем последнее значение
                var (index, length) = positions[positions.Count - 1];

                var sb = new StringBuilder(this.Content);
                sb.Remove(index, length);
                sb.Insert(index, value ?? string.Empty);

                this.Content = sb.ToString(); // инвалидирует кэш
                return;
            }

            // если ключа нет — fallback
            this.SetValueSlow(section, key, value);
        }

        /// <summary>
        /// Возвращает содержимое INI-файла в виде строки.
        /// </summary>
        /// <returns>Строковое представление содержимого INI-файла.</returns>
        public override string ToString()
        {
            return this.Content;
        }

        /// <summary>
        /// Определяет, является ли символ символом новой строки.
        /// </summary>
        /// <param name="c">Проверяемый символ.</param>
        /// <returns>true, если символ является '\n' или '\r'.</returns>
        internal static bool IsNewLine(char c)
        {
            return c == '\n' || c == '\r';
        }

        /// <summary>
        /// Автоматически определяет кодировку файла по BOM (Byte Order Mark).
        /// </summary>
        /// <param name="fileName">Путь к файлу.</param>
        /// <param name="defaultEncoding">Кодировка по умолчанию, если BOM не обнаружен.</param>
        /// <returns>Определенная кодировка или defaultEncoding.</returns>
        private static Encoding AutoDetectEncoding(string fileName, Encoding defaultEncoding = null)
        {
            var bom = new byte[4];

            using (var fs = File.OpenRead(fileName))
            {
                var count = fs.Read(bom, 0, 4);

                if (count > 2)
                {
                    if (bom[0] == 0x2b && bom[1] == 0x2f && bom[2] == 0x76)
                    {
                        return Encoding.UTF7;
                    }

                    if (bom[0] == 0xef && bom[1] == 0xbb && bom[2] == 0xbf)
                    {
                        return Encoding.UTF8;
                    }

                    if (bom[0] == 0 && bom[1] == 0 && bom[2] == 0xfe && bom[3] == 0xff)
                    {
                        return Encoding.UTF32;
                    }
                }
                else if (count > 1)
                {
                    if (bom[0] == 0xff && bom[1] == 0xfe)
                    {
                        return Encoding.Unicode;
                    }

                    if (bom[0] == 0xfe && bom[1] == 0xff)
                    {
                        return Encoding.BigEndianUnicode;
                    }
                }
            }

            return defaultEncoding ?? Encoding.Default;
        }

        /// <summary>
        /// Автоматически определяет символ(ы) разрыва строки в тексте (\n, \r\n, \r).
        /// </summary>
        /// <param name="text">Текст для анализа.</param>
        /// <returns>Обнаруженный разделитель строк или Environment.NewLine по умолчанию.</returns>
        private static string AutoDetectLineBreaker(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return Environment.NewLine;
            }

            bool r = false, n = false;

            for (var index = 0; index < text.Length; index++)
            {
                var c = text[index];
                if (c == '\r')
                {
                    r = true;
                }

                if (c == '\n')
                {
                    n = true;
                }

                if (r && n)
                {
                    return "\r\n";
                }
            }

            if (!r && !n)
            {
                return Environment.NewLine;
            }

            return n ? "\n" : "\r";
        }

        /// <summary>
        /// Возвращает StringComparer на основе указанного StringComparison.
        /// </summary>
        /// <param name="comparison">Правила сравнения строк.</param>
        /// <returns>Соответствующий StringComparer.</returns>
        private static StringComparer GetComparer(StringComparison comparison)
        {
            switch (comparison)
            {
                case StringComparison.CurrentCulture:
                    return StringComparer.CurrentCulture;

                case StringComparison.CurrentCultureIgnoreCase:
                    return StringComparer.CurrentCultureIgnoreCase;

                case StringComparison.InvariantCulture:
                    return StringComparer.InvariantCulture;

                case StringComparison.InvariantCultureIgnoreCase:
                    return StringComparer.InvariantCultureIgnoreCase;

                case StringComparison.Ordinal:
                    return StringComparer.Ordinal;

                case StringComparison.OrdinalIgnoreCase:
                    return StringComparer.OrdinalIgnoreCase;

                default:
                    return StringComparer.InvariantCultureIgnoreCase;
            }
        }

        /// <summary>
        /// Возвращает полный путь к файлу после проверки его корректности.
        /// </summary>
        /// <param name="fileName">Имя файла.</param>
        /// <param name="checkExists">Проверять существование файла.</param>
        /// <returns>Полный путь к файлу.</returns>
        /// <exception cref="ArgumentNullException">Если fileName равен null.</exception>
        /// <exception cref="ArgumentException">Если fileName пуст или содержит недопустимые символы.</exception>
        /// <exception cref="FileNotFoundException">Если checkExists=true и файл не существует.</exception>
        private static string GetFullPath(string fileName, bool checkExists = false)
        {
            if (ValidateFileName(fileName, checkExists) is Exception exception)
            {
                throw exception;
            }

            return Path.GetFullPath(fileName);
        }

        /// <summary>
        /// Вставляет строку в StringBuilder на новую строку после указанной позиции.
        /// </summary>
        /// <param name="sb">StringBuilder для модификации.</param>
        /// <param name="index">Позиция для вставки (будет скорректирована до конца текущей строки).</param>
        /// <param name="newLine">Разделитель строк.</param>
        /// <param name="text">Текст для вставки.</param>
        /// <exception cref="ArgumentNullException">Если sb или text равны null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Если index меньше 0.</exception>
        private static void InsertLine(StringBuilder sb, ref int index, string newLine, string text)
        {
            if (sb == null)
            {
                throw new ArgumentNullException(nameof(sb));
            }

            if (text == null)
            {
                throw new ArgumentNullException(nameof(text));
            }

            if (index < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            sb = MoveIndexToEndOfLinePosition(sb, ref index);

            sb = sb.Insert(index, text);
            index += text.Length;

            sb.Insert(index, newLine);
            index += newLine.Length - 1;
        }

        /// <summary>
        /// Проверяет, является ли символ недопустимым в пути.
        /// </summary>
        /// <param name="c">Проверяемый символ.</param>
        /// <returns>true, если символ недопустим.</returns>
        private static bool InvalidPathChar(char c)
        {
            return InvalidPathChars.Contains(c);
        }

        /// <summary>
        /// Проверяет, содержит ли строка недопустимые символы пути.
        /// </summary>
        /// <param name="fileName">Строка для проверки.</param>
        /// <returns>true, если строка содержит недопустимые символы.</returns>
        private static bool IsInvalidPath(string fileName)
        {
            return fileName.Any(InvalidPathChar);
        }

        /// <summary>
        /// Перемещает индекс в конец текущей строки в StringBuilder.
        /// </summary>
        /// <param name="sb">StringBuilder для анализа.</param>
        /// <param name="index">Текущая позиция индекса (будет скорректирована).</param>
        /// <returns>Тот же StringBuilder.</returns>
        private static StringBuilder MoveIndexToEndOfLinePosition(StringBuilder sb, ref int index)
        {
            var length = sb.Length;

            if (index < 0)
            {
                index = 0;
            }
            else if (index >= length)
            {
                index = length;
            }
            else if (index > 0)
            {
                while (index < length && !IsNewLine(sb[index]))
                {
                    index++;
                }

                while (index < length && IsNewLine(sb[index]))
                {
                    index++;
                }
            }

            return sb;
        }

        /// <summary>
        /// Экранирует специальные символы в строке (например, \n, \t) для записи в INI-файл.
        /// </summary>
        /// <param name="text">Исходная строка.</param>
        /// <returns>Экранированная строка.</returns>
        private static string ToEscape(string text)
        {
            var pos = 0;
            var inputLength = text.Length;

            if (inputLength == 0)
            {
                return text;
            }

            var sb = new StringBuilder(inputLength * 2);
            do
            {
                var c = text[pos++];

                switch (c)
                {
                    case '\\':
                        sb.Append(@"\\");
                        break;

                    case '\0':
                        sb.Append(@"\0");
                        break;

                    case '\a':
                        sb.Append(@"\a");
                        break;

                    case '\b':
                        sb.Append(@"\b");
                        break;

                    case '\n':
                        sb.Append(@"\n");
                        break;

                    case '\r':
                        sb.Append(@"\r");
                        break;

                    case '\f':
                        sb.Append(@"\f");
                        break;

                    case '\t':
                        sb.Append(@"\t");
                        break;

                    case '\v':
                        sb.Append(@"\v");
                        break;

                    default:
                        sb.Append(c);
                        break;
                }
            }
            while (pos < inputLength);

            return sb.ToString();
        }

        /// <summary>
        /// Убирает экранирование символов в строке (преобразует \n, \t обратно в управляющие символы).
        /// </summary>
        /// <param name="text">Экранированная строка.</param>
        /// <returns>Неэкранированная строка.</returns>
        private static string UnEscape(string text)
        {
            var pos = -1;
            var inputLength = text.Length;

            if (inputLength == 0)
            {
                return text;
            }

            for (var i = 0; i < inputLength; ++i)
            {
                if (text[i] == '\\')
                {
                    pos = i;
                    break;
                }
            }

            if (pos < 0)
            {
                return text;
            }

            var sb = new StringBuilder(text.Substring(0, pos));

            do
            {
                var c = text[pos++];
                if (c == '\\')
                {
                    c = pos < inputLength ? text[pos] : '\\';
                    switch (c)
                    {
                        case '\\':
                            c = '\\';
                            break;

                        case '0':
                            c = '\0';
                            break;

                        case 'a':
                            c = '\a';
                            break;

                        case 'b':
                            c = '\b';
                            break;

                        case 'n':
                            c = '\n';
                            break;

                        case 'r':
                            c = '\r';
                            break;

                        case 'f':
                            c = '\f';
                            break;

                        case 't':
                            c = '\t';
                            break;

                        case 'v':
                            c = '\v';
                            break;

                        case 'u' when pos < inputLength - 3:
                            c = UnHex(text.Substring(++pos, 4));
                            pos += 3;
                            break;

                        case 'x' when pos < inputLength - 1:
                            c = UnHex(text.Substring(++pos, 2));
                            pos++;
                            break;

                        case 'c' when pos < inputLength:
                            c = text[++pos];
                            if (c >= 'a' && c <= 'z')
                            {
                                c -= ' ';
                            }

                            if ((c = (char)(c - 0x40U)) >= ' ')
                            {
                                c = '?';
                            }

                            break;

                        default:
                            sb.Append("\\" + c);
                            pos++;
                            continue;
                    }

                    pos++;
                }

                sb.Append(c);
            }
            while (pos < inputLength);

            return sb.ToString();
        }

        /// <summary>
        /// Преобразует шестнадцатеричную строку в символ.
        /// </summary>
        /// <param name="hex">Шестнадцатеричная строка (например, "1A3F").</param>
        /// <returns>Символ, соответствующий шестнадцатеричному значению, или '?' в случае ошибки.</returns>
        private static char UnHex(string hex)
        {
            var c = 0;
            for (var i = 0; i < hex.Length; i++)
            {
                int r = hex[i];
                if (r > 0x2F && r < 0x3A)
                {
                    r -= 0x30;
                }
                else if (r > 0x40 && r < 0x47)
                {
                    r -= 0x37;
                }
                else if (r > 0x60 && r < 0x67)
                {
                    r -= 0x57;
                }
                else
                {
                    return '?';
                }

                c = (c << 4) + r;
            }

            return (char)c;
        }

        /// <summary>
        /// Проверяет корректность имени файла.
        /// </summary>
        /// <param name="fileName">Имя файла для проверки.</param>
        /// <param name="checkExists">Проверять существование файла.</param>
        /// <returns>Исключение, если проверка не пройдена, иначе null.</returns>
        private static Exception ValidateFileName(string fileName, bool checkExists = false)
        {
            if (fileName == null)
            {
                return new ArgumentNullException(nameof(fileName));
            }

            if (string.IsNullOrEmpty(fileName) || fileName.All(char.IsWhiteSpace) || IsInvalidPath(fileName))
            {
                return new ArgumentException(null, nameof(fileName));
            }

            if (checkExists && !File.Exists(fileName))
            {
                return new FileNotFoundException(null, fileName);
            }

            return null;
        }

        private void EnsureCache()
        {
            if (!this.cacheDirty && this.valueCache != null)
            {
                return;
            }

            var comparer = GetComparer(this.comparison);

            this.valueCache = new Dictionary<string, Dictionary<string, List<string>>>(comparer);
            this.keyCache = new Dictionary<string, HashSet<string>>(comparer);
            this.sectionCache = new HashSet<string>(comparer);
            this.writeIndex = new Dictionary<string, Dictionary<string, List<(int, int)>>>(comparer);

            var currentSection = string.Empty;
            this.sectionCache.Add(string.Empty);

            for (var m = IniFile.Regex.Match(this.Content); m.Success; m = m.NextMatch())
            {
                // ---------- SECTION ----------
                if (m.Groups["section"].Success)
                {
                    currentSection = m.Groups["value"].Value;
                    this.sectionCache.Add(currentSection);
                    continue;
                }

                // ---------- ENTRY ----------
                if (!m.Groups["entry"].Success)
                {
                    continue;
                }

                var key = m.Groups["key"].Value;
                var value = m.Groups["value"].Value;

                if (this.allowEscapeChars)
                {
                    value = UnEscape(value);
                }

                // ---- valueCache ----
                if (!this.valueCache.TryGetValue(currentSection, out var keyDict))
                {
                    keyDict = new Dictionary<string, List<string>>(comparer);
                    this.valueCache[currentSection] = keyDict;
                }

                if (!keyDict.TryGetValue(key, out var values))
                {
                    values = new List<string>();
                    keyDict[key] = values;
                }

                values.Add(value);

                // ---- keyCache ----
                if (!this.keyCache.TryGetValue(currentSection, out var keySet))
                {
                    keySet = new HashSet<string>(comparer);
                    this.keyCache[currentSection] = keySet;
                }

                keySet.Add(key);

                // ---- writeIndex ----
                if (!this.writeIndex.TryGetValue(currentSection, out var writeKeys))
                {
                    writeKeys = new Dictionary<string, List<(int, int)>>(comparer);
                    this.writeIndex[currentSection] = writeKeys;
                }

                if (!writeKeys.TryGetValue(key, out var positions))
                {
                    positions = new List<(int, int)>();
                    writeKeys[key] = positions;
                }

                positions.Add((
                    m.Groups["value"].Index,
                    m.Groups["value"].Length));
            }

            this.cacheDirty = false;
        }

        /// <summary>
        /// Устанавливает значение ключа. Если ключ существует, обновляет его значение. Если нет — добавляет новый ключ.
        /// </summary>
        /// <param name="section">Имя секции. Может быть null для глобальных ключей.</param>
        /// <param name="key">Имя ключа.</param>
        /// <param name="value">Значение для установки. Если null или пустая строка, ключ будет удален.</param>
        private void SetValueSlow(string section, string key, string value)
        {
            var emptySection = string.IsNullOrEmpty(section);
            var expectedValue = !string.IsNullOrEmpty(value);
            var inSection = emptySection;
            Match lastMatch = null;
            var sb = new StringBuilder(this.content);

            if (this.allowEscapeChars && expectedValue)
            {
                value = ToEscape(value);
            }

            for (var match = IniFile.Regex.Match(this.Content); match.Success; match = match.NextMatch())
            {
                if (match.Groups["section"].Success)
                {
                    inSection = match.Groups["value"].Value.Equals(section, this.comparison);
                    if (emptySection)
                    {
                        break;
                    }

                    continue;
                }

                if (inSection && match.Groups["entry"].Success)
                {
                    lastMatch = match;

                    if (!match.Groups["key"].Value.Equals(key, this.comparison))
                    {
                        continue;
                    }

                    var group = match.Groups["value"];

                    var index = group.Index;
                    var length = group.Length;

                    sb.Remove(index, length);

                    if (expectedValue)
                    {
                        sb.Insert(index, value);
                    }

                    expectedValue = false;
                    break;
                }
            }

            if (expectedValue)
            {
                var index = 0;

                if (lastMatch != null)
                {
                    index = lastMatch.Index + lastMatch.Length;
                }
                else if (!emptySection)
                {
                    sb.Append(this.lineBreaker);
                    sb.Append($"[{section}]{this.lineBreaker}");
                    index = sb.Length;
                }

                var line = $"{key}={value}";
                InsertLine(sb, ref index, this.lineBreaker, line);
            }

            this.Content = sb.ToString();
        }
    }
}