// <copyright file="TreeList{T}.cs" company="Rudnev Sergey">
// Copyright (c) Rudnev Sergey. All rights reserved.
// </copyright>

namespace System.Collections
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Specialized;
    using System.ComponentModel;
    using System.Linq;
    using System.Text;

    /// <summary>
    /// Представляет узел древовидной структуры, который может содержать дочерние элементы.
    /// Поддерживает навигацию, вставку, удаление и обход дерева.
    /// </summary>
    /// <typeparam name="T">Тип значения узла.</typeparam>
    public class TreeList<T> : IEnumerable<T>
    {
        private readonly List<TreeList<T>> children = [];

        /// <summary>
        /// Инициализирует новый экземпляр узла дерева с указанным значением.
        /// </summary>
        public TreeList()
        {
            this.Root = this;
        }

        /// <summary>
        /// Инициализирует новый экземпляр узла дерева с указанным значением.
        /// </summary>
        /// <param name="item">Значение узла.</param>
        public TreeList(T item)
            : this()
        {
            this.Value = item;
        }

        /// <summary>
        /// Инициализирует новый экземпляр узла дерева с дочерними элементами.
        /// </summary>
        /// <param name="item">Значение узла.</param>
        /// <param name="children">Значения дочерних узлов.</param>
        public TreeList(T item, IEnumerable<T> children)
            : this(item)
        {
            this.AddRange(children);
        }

        /// <summary>
        /// Дочерние узлы текущего элемента.
        /// </summary>
        public IReadOnlyList<TreeList<T>> Children => this.children;

        /// <summary>
        /// Индекс текущего узла в коллекции дочерних элементов родителя.
        /// Для корневого узла возвращает -1.
        /// </summary>
        public int Index { get; private set; }

        /// <summary>
        /// Определяет, раскрыт ли узел (используется при обходе видимых элементов).
        /// </summary>
        public bool IsExpanded { get; set; } = true;

        /// <summary>
        /// Уровень вложенности узла.
        /// </summary>
        public int Level { get; private set; }

        /// <summary>
        /// Следующий узел в плоском обходе дерева.
        /// Возвращает null, если текущий узел является последним видимым узлом.
        /// </summary>
        public TreeList<T> Next { get; private set; }

        /// <summary>
        /// Родительский узел.
        /// </summary>
        public TreeList<T> Parent { get; private set; }

        /// <summary>
        /// Значение родительского узла.
        /// Для корневого элемента возвращает default(T).
        /// </summary>
        public T ParentValue => this.Parent != null ? this.Parent.Value : default;

        /// <summary>
        /// Предыдущий узел в плоском обходе дерева.
        /// Возвращает null, если текущий узел является первым видимым узлом.
        /// </summary>
        public TreeList<T> Prev { get; private set; }

        /// <summary>
        /// Корневой узел дерева.
        /// </summary>
        public TreeList<T> Root { get; private set; }

        /// <summary>
        /// Значение, хранимое в узле.
        /// </summary>
        public T Value { get; set; }

        /// <summary>
        /// Количество видимых узлов, начиная с текущего (с учётом IsExpanded).
        /// </summary>
        public int VisibleCount
        {
            get
            {
                int count = 1;

                if (!this.IsExpanded)
                {
                    return count;
                }

                foreach (var child in this.children)
                {
                    count += child.VisibleCount;
                }

                return count;
            }
        }

        /// <summary>
        /// Возвращает дочерний узел по указанному индексу.
        /// </summary>
        /// <param name="index">Индекс дочернего узла.</param>
        /// <returns>Дочерний узел по указанной позиции.</returns>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Выбрасывается, если <paramref name="index"/> выходит за пределы диапазона.
        /// </exception>
        /// <remarks>
        /// Индексация выполняется по непосредственным дочерним элементам текущего узла.
        /// </remarks>
        public TreeList<T> this[int index] => this.children[index];

        /// <summary>
        /// Возвращает путь между двумя узлами через их общего предка.
        /// </summary>
        /// <param name="fromNode">Начальный узел.</param>
        /// <param name="toNode">Конечный узел.</param>
        /// <returns>Путь.</returns>
        public static List<TreeList<T>> GetPath(TreeList<T> fromNode, TreeList<T> toNode)
        {
            if (fromNode == null)
            {
                throw new ArgumentNullException(nameof(fromNode));
            }

            if (toNode == null)
            {
                throw new ArgumentNullException(nameof(toNode));
            }

            var a = new List<TreeList<T>>();
            var b = new List<TreeList<T>>();

            for (var cur = fromNode; cur != null; cur = cur.Parent)
            {
                a.Add(cur);
            }

            for (var cur = toNode; cur != null; cur = cur.Parent)
            {
                b.Add(cur);
            }

            int i = a.Count - 1;
            int j = b.Count - 1;

            while (i >= 0 && j >= 0 && a[i] == b[j])
            {
                i--;
                j--;
            }

            var result = new List<TreeList<T>>();

            for (int k = 0; k <= i; k++)
            {
                result.Add(a[k]);
            }

            for (int k = j + 1; k >= 0; k--)
            {
                result.Add(b[k]);
            }

            return result;
        }

        /// <summary>
        /// Создаёт новый дочерний узел с указанным значением и добавляет его в текущее поддерево.
        /// </summary>
        /// <param name="item">Значение нового узла.</param>
        /// <returns>Созданный и добавленный дочерний узел.</returns>
        public TreeList<T> Add(T item)
        {
            var child = new TreeList<T>(item);
            return this.Add(child);
        }

        /// <summary>
        /// Добавляет существующий узел в качестве дочернего элемента текущего узла.
        /// </summary>
        /// <param name="child">Добавляемый узел.</param>
        /// <returns>Добавленный узел.</returns>
        /// <exception cref="ArgumentNullException">
        /// Выбрасывается, если <paramref name="child"/> равен <see langword="null"/>.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Может быть выброшено, если нарушается структура дерева (например, некорректная иерархия).
        /// </exception>
        public TreeList<T> Add(TreeList<T> child)
        {
            if (child is null)
            {
                throw new ArgumentNullException(nameof(child));
            }

            child.Parent = this;
            child.Root = this.Root;
            child.Level = this.Level + 1;
            child.Index = this.children.Count;
            this.children.Add(child);
            return child;
        }

        /// <summary>
        /// Добавляет последовательность значений как дочерние узлы.
        /// </summary>
        /// <param name="items">Последовательность добавляемых значений.</param>
        /// <returns>
        /// Последний добавленный узел или <see langword="null"/>, если последовательность пуста.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Выбрасывается, если <paramref name="items"/> равен <see langword="null"/>.
        /// </exception>
        public TreeList<T> AddRange(params T[] items)
        {
            return this.AddRange((IEnumerable<T>)items);
        }

        /// <summary>
        /// Добавляет последовательность значений как дочерние узлы.
        /// </summary>
        /// <param name="items">Последовательность добавляемых значений.</param>
        /// <returns>
        /// Последний добавленный узел или <see langword="null"/>, если последовательность пуста.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Выбрасывается, если <paramref name="items"/> равен <see langword="null"/>.
        /// </exception>
        public TreeList<T> AddRange(IEnumerable<T> items)
        {
            if (items == null)
            {
                throw new ArgumentNullException(nameof(items));
            }

            TreeList<T> last = null;

            foreach (var item in items)
            {
                last = this.Add(item);
            }

            return last;
        }

        /// <summary>
        /// Добавляет набор узлов как дочерние элементы текущего узла.
        /// </summary>
        /// <param name="items">Добавляемые узлы.</param>
        /// <returns>
        /// Последний добавленный узел или <see langword="null"/>, если вход пуст.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Выбрасывается, если <paramref name="items"/> равен <see langword="null"/>.
        /// </exception>
        public TreeList<T> AddRange(IEnumerable<TreeList<T>> items)
        {
            if (items is null)
            {
                throw new ArgumentNullException(nameof(items));
            }

            TreeList<T> lastItem = null;

            foreach (var item in items)
            {
                if (item is null)
                {
                    continue;
                }

                lastItem = this.Add(item);
            }

            return lastItem;
        }

        /// <summary>
        /// Перечисление узлов от <see cref="Root"/> до текущего узла.
        /// </summary>
        /// <param name="includeSelf">Включать ли в перечисление корневой узел.</param>
        /// <param name="includeRoot">Включать ли в перечисление текущий узел.</param>
        /// <returns>Узлы от корневого до текущего узла.</returns>
        public IEnumerable<TreeList<T>> Branch(bool includeSelf = true, bool includeRoot = true)
        {
            var path = GetPath(this.Root, this);
            for (int i = 0; i < path.Count; i++)
            {
                if ((i == 0 && !includeRoot) || (i == path.Count - 1 && !includeSelf))
                {
                    continue;
                }

                yield return path[i];
            }
        }

        /// <summary>
        /// Удаляет все дочерние узлы текущего элемента и разрывает связи с ними.
        /// </summary>
        /// <remarks>
        /// После вызова метода все дочерние узлы становятся не связанными с деревом.
        /// </remarks>
        public void Clear()
        {
            foreach (var child in this.children)
            {
                child.Parent = null;
            }

            this.children.Clear();
        }

        /// <summary>
        /// Выполняет полный обход поддерева, начиная с текущего узла.
        /// </summary>
        /// <returns>
        /// Последовательность всех узлов поддерева в порядке обхода.
        /// </returns>
        /// <remarks>
        /// Обход включает все узлы независимо от состояния <see cref="IsExpanded"/>.
        /// </remarks>
        public IEnumerable<TreeList<T>> DescendantsAndSelf()
        {
            yield return this;

            foreach (var child in this.children)
            {
                foreach (var node in child.DescendantsAndSelf())
                {
                    yield return node;
                }
            }
        }

        /// <summary>
        /// Отсоединяет текущий узел от родительского узла.
        /// </summary>
        /// <remarks>
        /// Удаляет текущий узел из коллекции дочерних элементов родителя.
        /// </remarks>
        public void Detach()
        {
            this.Parent?.Remove(this);
        }

        /// <summary>
        /// Возвращает перечислитель значений узлов в порядке обхода видимого поддерева.
        /// </summary>
        /// <returns>
        /// Перечислитель значений узлов (<typeparamref name="T"/>), начиная с текущего узла.
        /// </returns>
        /// <remarks>
        /// Обход выполняется только по видимым узлам (<see cref="VisibleDescendantsAndSelf"/>).
        /// </remarks>
        public IEnumerator<T> GetEnumerator()
        {
            foreach (var node in this.VisibleDescendantsAndSelf())
            {
                yield return node.Value;
            }
        }

        /// <inheritdoc />
        IEnumerator IEnumerable.GetEnumerator()
        {
            return this.GetEnumerator();
        }

        /// <summary>
        /// Возвращает индекс текущего узла в плоском обходе видимого дерева.
        /// </summary>
        /// <remarks>
        /// Индекс вычисляется относительно корневого узла и учитывает только видимые элементы.
        /// </remarks>
        /// <returns>
        /// Индекс узла в последовательности обхода или <c>-1</c>, если узел не найден.
        /// </returns>
        public int GetFlatIndex()
        {
            int index = 0;

            foreach (var node in this.Root.VisibleDescendantsAndSelf())
            {
                if (node == this)
                {
                    return index;
                }

                index++;
            }

            return index;
        }

        /// <summary>
        /// Возвращает узел по индексу в плоском обходе видимого дерева.
        /// </summary>
        /// <remarks>
        /// Обход выполняется в порядке <see cref="VisibleDescendantsAndSelf"/> начиная с текущего узла.
        /// Учитываются только видимые элементы.
        /// </remarks>
        /// <param name="targetIndex">Индекс узла в последовательности обхода.</param>
        /// <returns>
        /// Узел с указанным индексом или <see langword="null"/>, если индекс выходит за пределы диапазона.
        /// </returns>
        public TreeList<T> GetNodeByFlatIndex(int targetIndex)
        {
            int index = 0;

            foreach (var node in this.VisibleDescendantsAndSelf())
            {
                if (index == targetIndex)
                {
                    return node;
                }

                index++;
            }

            return null;
        }

        /// <summary>
        /// Вставляет новый дочерний узел по указанному индексу.
        /// </summary>
        /// <param name="index">Позиция вставки в коллекции дочерних элементов.</param>
        /// <param name="item">Значение нового узла.</param>
        /// <returns>Созданный и добавленный узел.</returns>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Выбрасывается, если <paramref name="index"/> выходит за пределы допустимого диапазона.
        /// </exception>
        public TreeList<T> Insert(int index, T item)
        {
            if (index < 0 || index > this.children.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            var child = new TreeList<T>(item);
            return this.Insert(index, child);
        }

        /// <summary>
        /// Вставляет существующий узел в коллекцию дочерних элементов по указанному индексу.
        /// </summary>
        /// <param name="index">Позиция вставки в коллекции дочерних элементов.</param>
        /// <param name="child">Вставляемый узел.</param>
        /// <returns>Вставленный узел.</returns>
        /// <exception cref="ArgumentNullException">
        /// Выбрасывается, если <paramref name="child"/> равен <see langword="null"/>.
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Выбрасывается, если <paramref name="index"/> выходит за пределы допустимого диапазона.
        /// </exception>
        public TreeList<T> Insert(int index, TreeList<T> child)
        {
            if (index < 0 || index > this.children.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            child.Parent = this;
            child.Index = index;
            this.children.Insert(index, child);
            this.UpdateIndexes(index + 1, 1);
            return child;
        }

        /// <summary>
        /// Определяет, является ли текущий узел предком указанного узла.
        /// </summary>
        /// <param name="node">Проверяемый узел.</param>
        /// <returns>
        /// <see langword="true"/>, если текущий узел является предком <paramref name="node"/>;
        /// иначе <see langword="false"/>.
        /// </returns>
        public bool IsAncestorOf(TreeList<T> node)
        {
            var current = node;

            while (current != null)
            {
                if (current == this)
                {
                    return true;
                }

                current = current.Parent;
            }

            return false;
        }

        /// <summary>
        /// Возвращает все листовые узлы дерева (узлы без дочерних элементов),
        /// включая текущий узел, если у него нет потомков.
        /// </summary>
        /// <returns>Последовательность листовых узлов.</returns>
        public IEnumerable<TreeList<T>> Leaves()
        {
            if (this.children.Count == 0)
            {
                yield return this;
                yield break;
            }

            foreach (var child in this.children)
            {
                foreach (var leaf in child.Leaves())
                {
                    yield return leaf;
                }
            }
        }

        /// <summary>
        /// Перемещает текущий узел в другой родительский узел.
        /// </summary>
        /// <param name="newParent">Новый родительский узел.</param>
        /// <param name="index">
        /// Позиция вставки в коллекции дочерних элементов нового родителя.
        /// Если значение не указано, узел добавляется в конец.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Выбрасывается, если <paramref name="newParent"/> равен <see langword="null"/>.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Выбрасывается, если попытка переместить узел внутрь собственного поддерева.
        /// </exception>
        public void MoveTo(TreeList<T> newParent, int? index = null)
        {
            if (newParent == null)
            {
                throw new ArgumentNullException(nameof(newParent));
            }

            if (this.IsAncestorOf(newParent))
            {
                throw new InvalidOperationException("Cannot move node into its own subtree.");
            }

            this.Parent?.Remove(this);

            if (index.HasValue)
            {
                newParent.Insert(index.Value, this);
            }
            else
            {
                newParent.Add(this);
            }
        }

        /// <summary>
        /// Удаляет указанный дочерний узел из текущего узла.
        /// </summary>
        /// <param name="node">Удаляемый дочерний узел.</param>
        /// <returns>
        /// <see langword="true"/>, если узел был найден и удалён; иначе <see langword="false"/>.
        /// </returns>
        /// <remarks>
        /// Удаление возможно только для непосредственных дочерних элементов текущего узла.
        /// </remarks>
        public bool Remove(TreeList<T> node)
        {
            if (node == null || node.Parent != this)
            {
                return false;
            }

            node.Parent = null;
            if (this.children.Remove(node))
            {
                this.UpdateIndexes(node.Index, -1);
                return true;
            }
            else
            {
                return false;
            }
        }

        /// <summary>
        /// Удаляет все дочерние узлы, значения которых удовлетворяют заданному условию.
        /// </summary>
        /// <param name="match">
        /// Условие для проверки значений узлов.
        /// </param>
        /// <returns>
        /// Количество удалённых узлов.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Выбрасывается, если <paramref name="match"/> равен <see langword="null"/>.
        /// </exception>
        /// <remarks>
        /// Обход выполняется в обратном порядке для безопасного удаления элементов.
        /// </remarks>
        public int RemoveAll(Predicate<T> match)
        {
            int removedCount = 0;

            for (int i = this.children.Count - 1; i >= 0; i--)
            {
                if (match(this.children[i].Value))
                {
                    this.RemoveAt(i);
                    removedCount++;
                }
            }

            return removedCount;
        }

        /// <summary>
        /// Удаляет дочерний узел по указанному индексу.
        /// </summary>
        /// <param name="index">Индекс удаляемого дочернего узла.</param>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Выбрасывается, если <paramref name="index"/> выходит за пределы допустимого диапазона.
        /// </exception>
        /// <remarks>
        /// После удаления происходит переиндексация оставшихся дочерних элементов.
        /// </remarks>
        /// <returns>Возвращает удаленный узел дерева.</returns>
        public TreeList<T> RemoveAt(int index)
        {
            if (index < 0 || index >= this.children.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            var removed = this.children[index];
            this.children.RemoveAt(index);
            removed.Parent = null;
            this.UpdateIndexes(index, -1);
            return removed;
        }

        /// <summary>
        /// Удаляет первый дочерний узел, содержащий указанное значение.
        /// </summary>
        /// <param name="value">Значение, по которому выполняется поиск узла для удаления.</param>
        /// <returns>
        /// <see langword="true"/>, если узел с указанным значением найден и удалён; иначе <see langword="false"/>.
        /// </returns>
        /// <remarks>
        /// Удаляется только первое вхождение значения среди непосредственных дочерних элементов.
        /// </remarks>
        public bool RemoveValue(T value)
        {
            for (int i = 0; i < this.children.Count; i++)
            {
                if (EqualityComparer<T>.Default.Equals(this.children[i].Value, value))
                {
                    this.RemoveAt(i);
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Возвращает строковое представление дерева с отступами по уровням вложенности.
        /// </summary>
        /// <returns>
        /// Строковое представление дерева, где каждый узел расположен на отдельной строке.
        /// </returns>
        /// <remarks>
        /// Отступ формируется символами табуляции в зависимости от уровня узла.
        /// Обход выполняется по всем узлам через <see cref="DescendantsAndSelf()"/>.
        /// </remarks>
        public override string ToString()
        {
            var sb = new StringBuilder();

            foreach (var node in this.DescendantsAndSelf())
            {
                sb.Append(new string('\t', node.Level));
                sb.Append(node.Value);
                sb.Append("\r\n");
            }

            sb.Remove(sb.Length - 2, 2);
            return sb.ToString();
        }

        /// <summary>
        /// Выполняет обход только видимых узлов поддерева.
        /// </summary>
        /// <returns>
        /// Последовательность узлов, доступных в текущем состоянии раскрытия.
        /// </returns>
        /// <remarks>
        /// Если <see cref="IsExpanded"/> равно <see langword="false"/>, дочерние узлы не обходятся.
        /// </remarks>
        public IEnumerable<TreeList<T>> VisibleDescendantsAndSelf()
        {
            yield return this;

            if (!this.IsExpanded)
            {
                yield break;
            }

            foreach (var child in this.children)
            {
                foreach (var node in child.VisibleDescendantsAndSelf())
                {
                    yield return node;
                }
            }
        }

        /// <summary>
        /// Выполняет полный обход дерева, начиная с текущего узла, с возможностью
        /// обработки событий входа в ветку, выхода из ветки и посещения каждого узла.
        /// </summary>
        /// <param name="onEachNode">
        /// Делегат, вызываемый для каждого посещаемого узла, включая текущий.
        /// Вызывается до возврата узла через <c>yield return</c>.
        /// </param>
        /// <param name="onEnterBranch">
        /// Делегат, вызываемый перед началом обхода дочернего узла (перед входом в ветку).
        /// </param>
        /// <param name="onLeaveBranch">
        /// Делегат, вызываемый после завершения обхода дочернего узла (после выхода из ветки).
        /// </param>
        /// <remarks>
        /// <para>
        /// Обход выполняется в глубину (DFS) с предварительной обработкой узла (pre-order).
        /// </para>
        /// <para>
        /// Порядок вызовов для каждого дочернего узла:
        /// <list type="number">
        /// <item><description>Вызов <c>onEnterBranch</c></description></item>
        /// <item><description>Рекурсивный обход дочернего узла</description></item>
        /// <item><description>Вызов <c>onLeaveBranch</c></description></item>
        /// </list>
        /// </para>
        /// <para>
        /// Все делегаты являются необязательными и могут быть равны <c>null</c>.
        /// </para>
        /// </remarks>
        public void VisitAll(
            Action<TreeList<T>> onEachNode,
            Action<TreeList<T>> onEnterBranch,
            Action<TreeList<T>> onLeaveBranch)
        {
            onEachNode?.Invoke(this);

            foreach (var child in this.children)
            {
                onEnterBranch?.Invoke(child);
                child.VisitAll(onEachNode, onEnterBranch, onLeaveBranch);
                onLeaveBranch?.Invoke(child);
            }
        }

        private void UpdateIndexes(int fromIndex, int step)
        {
            if (fromIndex < 0 || fromIndex >= this.children.Count)
            {
                return;
            }

            for (int i = fromIndex; i < this.children.Count; i++)
            {
                this.children[i].Index += step;
            }
        }
    }
}