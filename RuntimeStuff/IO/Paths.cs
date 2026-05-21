// <copyright file="Paths.cs" company="Rudnev Sergey">
// Copyright (c) Rudnev Sergey. All rights reserved.
// </copyright>

namespace System.IO
{
    /// <summary>
    /// Содержит вспомогательные методы для работы с путями файловой системы.
    /// </summary>
    public static class Paths
    {
        /// <summary>
        /// Возвращает путь к каталогу данных текущего приложения
        /// в папке <see cref="Environment.SpecialFolder.ApplicationData"/>.
        /// </summary>
        /// <returns>
        /// Полный путь к каталогу приложения внутри <c>AppData\Roaming</c>.
        /// Имя каталога формируется на основе
        /// <see cref="AppDomain.FriendlyName"/> без расширения файла.
        /// </returns>
        /// <remarks>
        /// <para>
        /// Пример результата:
        /// <c>C:\Users\User\AppData\Roaming\MyApplication</c>.
        /// </para>
        /// <para>
        /// Если каталог отсутствует, он будет создан автоматически.
        /// </para>
        /// </remarks>
        /// <exception cref="UnauthorizedAccessException">
        /// Выбрасывается при отсутствии прав на создание каталога.
        /// </exception>
        /// <exception cref="IOException">
        /// Выбрасывается при ошибке ввода-вывода во время создания каталога.
        /// </exception>
        public static string GetAppDataDir()
        {
            var appName = AppDomain.CurrentDomain.FriendlyName;
            appName = Path.GetFileNameWithoutExtension(appName);
            var appDataPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                appName);

            if (!Directory.Exists(appDataPath))
            {
                Directory.CreateDirectory(appDataPath);
            }

            return appDataPath;
        }

        /// <summary>
        /// Возвращает путь к каталогу данных текущего приложения
        /// в папке <see cref="Environment.SpecialFolder.ApplicationData"/>.
        /// </summary>
        /// <returns>
        /// Полный путь к каталогу приложения внутри <c>AppData\Roaming</c>.
        /// Имя каталога формируется на основе
        /// <see cref="AppDomain.FriendlyName"/> без расширения файла.
        /// </returns>
        /// <remarks>
        /// <para>
        /// Пример результата:
        /// <c>C:\Users\User\AppData\Roaming\MyApplication</c>.
        /// </para>
        /// <para>
        /// Если каталог отсутствует, он будет создан автоматически.
        /// </para>
        /// </remarks>
        /// <exception cref="UnauthorizedAccessException">
        /// Выбрасывается при отсутствии прав на создание каталога.
        /// </exception>
        /// <exception cref="IOException">
        /// Выбрасывается при ошибке ввода-вывода во время создания каталога.
        /// </exception>
        public static DirectoryInfo GetAppDataDirInfo()
            => new DirectoryInfo(GetAppDataDir());
    }
}
