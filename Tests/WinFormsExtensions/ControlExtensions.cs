namespace WinFormsExtensions
{
    using RuntimeStuff;
    using System;
    using System.Collections.Generic;
    using System.Windows.Forms;

    /// <summary>
    /// Статический класс-расширение для работы с формами Windows Forms.
    /// Позволяет назначать действия на определённые клавиши и реализует удобное закрытие формы по клавише.
    /// </summary>
    public static class ControlExtensions
    {
        // Словарь для хранения привязок клавиш для каждой формы
        private static readonly Dictionary<Control, Dictionary<Keys, Action>> _keyBindings = new Dictionary<Control, Dictionary<Keys, Action>>();

        /// <summary>
        /// Привязывает действие к нажатию клавиши на форме
        /// </summary>
        /// <param name="form">Форма, к которой привязывается клавиша</param>
        /// <param name="key">Клавиша или комбинация клавиш</param>
        /// <param name="action">Действие, которое выполнится при нажатии</param>
        public static void BindKey(this Control form, Keys key, Action action)
        {
            if (form == null)
                throw new ArgumentNullException(nameof(form));

            if (action == null)
                throw new ArgumentNullException(nameof(action));

            // Получаем или создаем словарь привязок для формы
            if (!_keyBindings.TryGetValue(form, out var formBindings))
            {
                formBindings = new Dictionary<Keys, Action>();
                _keyBindings[form] = formBindings;

                // Подписываемся на события формы при первой привязке
                Obj.Set(form, "KeyPreview", true);
                form.KeyDown += Form_KeyDown;
                form.Disposed += Form_Disposed;
            }

            // Добавляем или обновляем привязку
            formBindings[key] = action;
        }

        private static void Form_KeyDown(object sender, KeyEventArgs e)
        {
            if (!(sender is Control form))
                return;

            // Проверяем, есть ли привязка для нажатой клавиши
            if (_keyBindings.TryGetValue(form, out var formBindings) &&
                formBindings.TryGetValue(e.KeyData, out var action))
            {
                action?.Invoke();
                e.Handled = true;
                e.SuppressKeyPress = true; // Предотвращает дальнейшую обработку клавиши
            }
        }

        private static void Form_Disposed(object sender, EventArgs e)
        {
            // Очищаем привязки при закрытии формы
            if (sender is Form form)
            {
                _keyBindings.Remove(form);
            }
        }

        /// <summary>
        /// Удаляет привязку клавиши для формы
        /// </summary>
        public static void UnbindKey(this Control form, Keys key)
        {
            if (form == null)
                throw new ArgumentNullException(nameof(form));

            if (_keyBindings.TryGetValue(form, out var formBindings))
            {
                formBindings.Remove(key);

                // Если привязок больше нет, отписываемся от событий
                if (formBindings.Count == 0)
                {
                    _keyBindings.Remove(form);
                    form.KeyDown -= Form_KeyDown;
                    form.Disposed -= Form_Disposed;
                }
            }
        }

        /// <summary>
        /// Удаляет все привязки клавиш для формы
        /// </summary>
        public static void ClearKeyBindings(this Control form)
        {
            if (form == null)
                throw new ArgumentNullException(nameof(form));

            if (_keyBindings.Remove(form))
            {
                form.KeyDown -= Form_KeyDown;
                form.Disposed -= Form_Disposed;
            }
        }

        /// <summary>
        /// Назначает действие закрытия формы на указанную клавишу (по умолчанию Escape).
        /// </summary>
        /// <param name="form">Форма, для которой назначается действие закрытия.</param>
        /// <param name="key">Клавиша, по которой будет закрываться форма (по умолчанию Escape).</param>
        public static void BindCloseFormKey(this Form form, Keys key = Keys.Escape)
        {
            form.BindKey(key, form.Close);
        }

        public static void AutoCloseOnDeactivate(this Form form, Func<bool> allowClose = null)
        {
            var filter = new SmartCloseFilter(form, allowClose);

            form.FormClosed += (s, e) =>
            {
                Application.RemoveMessageFilter(filter);
            };

            Application.AddMessageFilter(filter);
        }

        private class SmartCloseFilter : IMessageFilter
        {
            private readonly Form _form;
            private readonly Func<bool> _allowClose;

            public SmartCloseFilter(Form form, Func<bool> allowClose)
            {
                _form = form;
                _allowClose = allowClose;
            }

            public bool PreFilterMessage(ref Message m)
            {
                if (_form.IsDisposed || !_form.Visible)
                    return false;

                var cursor = Cursor.Position;

                const int WM_LBUTTONDOWN = 0x0201;
                const int WM_RBUTTONDOWN = 0x0204;
                const int WM_MBUTTONDOWN = 0x0207;
                const int WM_ACTIVATE = 0x0006;

                // --- 1. Клик вне формы ---
                if (m.Msg == WM_LBUTTONDOWN ||
                    m.Msg == WM_RBUTTONDOWN ||
                    m.Msg == WM_MBUTTONDOWN)
                {
                    if (!_form.Bounds.Contains(cursor))
                    {
                        if (_allowClose == null || _allowClose())
                        {
                            _form.Close();
                            return false; // не блокируем клик
                        }
                    }
                }

                // --- 2. Потеря фокуса ---
                if (m.Msg == WM_ACTIVATE)
                {
                    int wParam = m.WParam.ToInt32() & 0xFFFF;
                    const int WA_INACTIVE = 0;

                    if (wParam == WA_INACTIVE)
                    {
                        if (_allowClose == null || _allowClose())
                        {
                            _form.Close();
                            return false;
                        }
                    }
                }

                return false;
            }
        }
    }

}
