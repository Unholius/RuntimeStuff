using System;
using System.ComponentModel;
using System.Drawing.Design;
using System.Windows.Forms;
using System.Windows.Forms.Design;

namespace QuickDialogs.WinForms.NET
{
    public class DateTimePickerInlineEditor : UITypeEditor
    {
        public override UITypeEditorEditStyle GetEditStyle(ITypeDescriptorContext context)
            => UITypeEditorEditStyle.DropDown;

        public override object EditValue(ITypeDescriptorContext context, IServiceProvider provider, object value)
        {
            var edSvc = (IWindowsFormsEditorService)provider.GetService(typeof(IWindowsFormsEditorService));
            if (edSvc == null)
                return value;

            var picker = new DateTimePicker
            {
                Format = DateTimePickerFormat.Custom,
                CustomFormat = "yyyy-MM-dd HH:mm:ss",

                // 👇 ВАЖНО: отключаем встроенный календарь DateTimePicker
                ShowUpDown = true,
            };

            if (value is DateTime dt)
                picker.Value = dt;

            // Закрываем dropdown когда пользователь перестал менять значение
            picker.Leave += (s, e) => edSvc.CloseDropDown();

            edSvc.DropDownControl(picker);

            return picker.Value;
        }
    }
    
}
