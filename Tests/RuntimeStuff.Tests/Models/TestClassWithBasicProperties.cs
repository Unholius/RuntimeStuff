namespace RuntimeStuff.MSTests.Models
{
    public class TestClassWithBasicPropertiesWithNotifyPropertyChanged : System.ComponentModel.INotifyPropertyChanged
    {
        public TestClassWithBasicPropertiesWithNotifyPropertyChanged(int int32, string? str = null, bool? @bool = null, double? @double = null)
        {
            _int32 = int32;
            _str = str ?? int32.ToString();
            _bool = @bool ?? int32 % 2 == 0;
            _double = @double ?? int32 + int32 / 15.0;
        }

        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(propertyName));
        }
        private int _int32;
        public int Int32
        {
            get => _int32;
            set
            {
                if (_int32 != value)
                {
                    _int32 = value;
                    OnPropertyChanged(nameof(Int32));
                }
            }
        }
        private string? _str;
        public string? Str
        {
            get => _str;
            set
            {
                if (_str != value)
                {
                    _str = value;
                    OnPropertyChanged(nameof(Str));
                }
            }
        }
        private bool _bool;
        public bool Bool
        {
            get => _bool;
            set
            {
                if (_bool != value)
                {
                    _bool = value;
                    OnPropertyChanged(nameof(Bool));
                }
            }
        }
        private double _double;
        public double Double
        {
            get => _double;
            set
            {
                if (_double != value)
                {
                    _double = value;
                    OnPropertyChanged(nameof(Double));
                }
            }
        }
    }

    public class TestClassWithBasicProperties
    {
        public TestClassWithBasicProperties()
        {

        }

        public TestClassWithBasicProperties(int int32, string? str = null, bool? @bool = null, double? @double = null)
        {
            Int32 = int32;
            Str = str ?? int32.ToString();
            Bool = @bool ?? int32 % 2 == 0;
            Double = @double ?? int32 + int32 / 15.0;
        }

        public int Int32 { get; set; }
        public string? Str { get; set; }
        public bool Bool { get; set; }
        public double Double { get; set; }

        public override string ToString()
        {
            return $"{Int32}";
        }
    }
}
