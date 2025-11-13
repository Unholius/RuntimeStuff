using System.Collections.Generic;

namespace RuntimeStuff.UI.Core
{
    public sealed class BindingListViewNode<T>
    {
        internal BindingListViewNode(T value, IEnumerable<T> source = null, int? sourceListIndex = null)
        {
            this.Value = value;
            this.SourceListIndex = sourceListIndex ?? source?.IndexOf(value, 0) ?? -1;
        }
        public T Value { get; internal set; }
        public bool Visible { get; set; } = true;
        public int SourceListIndex { get; internal set; }
        public int VisibleIndex { get; internal set; }

        public override string ToString()
        {
            return $"[{SourceListIndex}] {(Value?.ToString() ?? base.ToString())}".Trim();
        }
    }
}

