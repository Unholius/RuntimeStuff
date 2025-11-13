namespace RuntimeStuff.MSTests.Models
{
    public class TestClassWithBasicProperties
    {
        public TestClassWithBasicProperties()
        {

        }

        public TestClassWithBasicProperties(int intProperty, string? stringProperty = null, bool boolProperty = true, double doubleProperty = 0.0)
        {
            IntProperty = intProperty;
            StringProperty = stringProperty;
            BoolProperty = boolProperty;
            DoubleProperty = doubleProperty;
        }

        public int IntProperty { get; set; }
        public string? StringProperty { get; set; }
        public bool BoolProperty { get; set; }
        public double DoubleProperty { get; set; }

        public override string ToString()
        {
            return $"{IntProperty}";
        }
    }
}
