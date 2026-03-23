using System;
using System.ComponentModel;
using System.Windows.Forms;
using RuntimeStuff.Builders;
using RuntimeStuff.Extensions;
using RuntimeStuff.Helpers;

namespace WinFormsExtensions
{
    public partial class ValueFilterTemplate : UserControl, INotifyPropertyChanged
    {
        public ValueFilterTemplate()
        {
            InitializeComponent();
            Presets.DataSource = presetsList;
        }

        private StringFilterBuilder filterBuilder = new StringFilterBuilder();

        public Type ValueType { get; set; }

        private void OnValueTypeChanged()
        {
            switch (ValueType.FullName)
            {
                case "System.DateTime":
                    AddPreset($"Сегодня ({DateTime.Now:dd ddd MMM})", () => GetFilterTextBetween(DateTime.Now.BeginDay(), DateTime.Now.EndDay()));
                    AddPreset($"Вчера ({DateTime.Now.BeginDay(-1):dd ddd MMM})", () => GetFilterTextBetween(DateTime.Now.BeginDay(-1), DateTime.Now.EndDay(-1)));
                    AddPreset($"Неделя ({DateTime.Now.BeginDay(-6):dd ddd MMM} - {DateTime.Now.EndDay():dd ddd MMM})", () => GetFilterTextBetween(DateTime.Now.BeginDay(-6), DateTime.Now.EndDay()));
                    AddPreset($"Две недели ({DateTime.Now.BeginDay(-13):dd ddd MMM} - {DateTime.Now.EndDay():dd ddd MMM})", () => GetFilterTextBetween(DateTime.Now.BeginDay(-13), DateTime.Now.EndDay()));
                    AddPreset($"Месяц (30 дней)", () => GetFilterTextBetween(DateTime.Now.BeginDay(-30), DateTime.Now.EndDay()));
                    for (int i = 1; i <= 12; i++)
                    {
                        var range = DateTimeHelper.MonthRange(i);
                        AddPreset($"{range.From:MMMM}".Capitalize(), () => GetFilterTextBetween(range));
                    }
                    AddPreset($"Год ({DateTime.Now.Year})", () => GetFilterTextBetween(DateTime.Now.BeginYear(), DateTime.Now.EndYear()));
                    break;
                case "System.Int32":
                case "System.Double":
                case "System.Decimal":
                {
                    // 0
                    AddPreset("Равно 0", () => GetFilterTextEquals(0));

                    // Положительные
                    AddPreset("Больше 0", () => GetFilterTextGreater(0));
                    AddPreset("Меньше 0", () => GetFilterTextLess(0));

                    // Малые диапазоны
                    AddPreset("1–10", () => GetFilterTextBetween(1, 10));
                    AddPreset("11–100", () => GetFilterTextBetween(11, 100));
                    AddPreset("101–1000", () => GetFilterTextBetween(101, 1000));

                    // Чётные / Нечётные
                    AddPreset("Чётные", () => GetFilterTextModulo(2, 0));    // value % 2 == 0
                    AddPreset("Нечётные", () => GetFilterTextModulo(2, 1));  // value % 2 == 1

                    // Положительные / Отрицательные
                    AddPreset("Положительные", () => GetFilterTextGreater(0));
                    AddPreset("Отрицательные", () => GetFilterTextLess(0));

                    // Можно добавить «Top N» или «Bottom N»
                    AddPreset("Top 10", () => GetFilterTextTop(10));
                    AddPreset("Bottom 10", () => GetFilterTextBottom(10));

                    break;
                }

                case "System.String":
                {
                    // Пустые / не пустые
                    AddPreset("Пустые", () => GetFilterTextEquals(""));
                    AddPreset("Не пустые", () => GetFilterTextNotEquals(""));

                    // Содержит текст
                    AddPreset("Содержит 'test'", () => GetFilterTextContains("test")); // пример, можно менять

                    // Начинается с
                    AddPreset("Начинается с 'A'", () => GetFilterTextStartsWith("A"));

                    // Заканчивается на
                    AddPreset("Заканчивается на 'Z'", () => GetFilterTextEndsWith("Z"));

                    // По длине
                    AddPreset("Длина = 0", () => GetFilterTextLengthEquals(0));
                    AddPreset("Длина > 10", () => GetFilterTextLengthGreater(10));

                    break;
                }
            }
        }

        private string GetFilterTextBetween(DateTimeHelper.DateRange range)
        {
            return GetFilterTextBetween(range.From, range.To);
        }

        private string GetFilterTextBetween(DateTime dateFrom, DateTime dateTo)
        {
            filterBuilder.Clear();
            filterBuilder
                .Property(FieldName)
                .GreaterOrEqual(dateFrom)
                .And()
                .Property(FieldName)
                .LowerOrEqual(dateTo);
            return filterBuilder.ToString();
        }


        public string FieldName { get; set; }

        public string SelectedPresetName => (Presets.CurrentRow?.DataBoundItem as PresetItem)?.PresetName;   
        public Func<string> SelectedFilterFunc => (Presets.CurrentRow?.DataBoundItem as PresetItem)?.FilterText;
        public DataGridView Grid => Presets;

        public void AddPreset(string presetName, Func<string> filterText)
        {
            presetsList.Add(new PresetItem { PresetName = presetName, FilterText = filterText });
        }

        public void ClearPresets()
        {
            presetsList.Clear();
        }

        private BindingList<PresetItem> presetsList = new BindingList<PresetItem>();

        public event PropertyChangedEventHandler PropertyChanged;

        internal class PresetItem
        {
            public string PresetName { get; set; }
            public Func<string> FilterText { get; set; }
        }
    }
}
