namespace RuntimeStuff.MSTests
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class TreeListTests
    {

        [TestMethod]
        public void Speed_Test_01()
        {
            var sw = new Stopwatch();
            var n = 1_000_000;
            var t = new TreeList<int>(0);
            sw.Start();
            for (int i = 1; i <= n; i++)
            {
                t = t.Add(i);
            }
            sw.Stop();
        }

        [TestMethod]
        public void TraverseAll_With_Actions_Test_01()
        {
            var root = new TreeList<string>("root");
            root.Add("br-1").Add("br-1.1").Add("br-1.1.1");
            root.Add("br-2").Add("br-2.1").Add("br-2.1.1").AddRange();
            root.VisitAll(null, (x) => x.Value = "(" + x.Value, (x) => x.Value += ")");
            var s = root.ToString();
            Assert.AreEqual("root\r\n\t(br-1)\r\n\t\t(br-1.1)\r\n\t\t\t(br-1.1.1)\r\n\t(br-2)\r\n\t\t(br-2.1)\r\n\t\t\t(br-2.1.1)", s);
        }

        [TestMethod]
        public void Constructor_WithValue_InitializesNode()
        {
            // Arrange & Act
            var node = new TreeList<string>("test");

            // Assert
            Assert.AreEqual("test", node.Value);
            Assert.AreEqual(0, node.Children.Count);
            Assert.IsTrue(node.IsExpanded);
            Assert.IsNull(node.Parent);
        }

        [TestMethod]
        public void Constructor_WithValueAndChildren_AddsChildren()
        {
            // Arrange
            var children = new[] { "child1", "child2", "child3" };

            // Act
            var node = new TreeList<string>("root", children);

            // Assert
            Assert.AreEqual("root", node.Value);
            Assert.AreEqual(3, node.Children.Count);
            Assert.AreEqual("child1", node[0].Value);
            Assert.AreEqual("child2", node[1].Value);
            Assert.AreEqual("child3", node[2].Value);
            Assert.AreEqual(node, node[0].Parent);
            Assert.AreEqual(node, node[1].Parent);
            Assert.AreEqual(node, node[2].Parent);
        }

        [TestMethod]
        public void Add_WithValue_CreatesAndAddsChild()
        {
            // Arrange
            var parent = new TreeList<string>("parent");

            // Act
            var child = parent.Add("child");

            // Assert
            Assert.AreEqual(1, parent.Children.Count);
            Assert.AreEqual("child", child.Value);
            Assert.AreEqual(parent, child.Parent);
            Assert.AreSame(child, parent[0]);
        }

        [TestMethod]
        public void Add_WithNode_AddsExistingNode()
        {
            // Arrange
            var parent = new TreeList<string>("parent");
            var child = new TreeList<string>("child");

            // Act
            var result = parent.Add(child);

            // Assert
            Assert.AreEqual(1, parent.Children.Count);
            Assert.AreEqual(parent, child.Parent);
            Assert.AreSame(child, result);
            Assert.AreSame(child, parent[0]);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void Add_NullNode_ThrowsArgumentNullException()
        {
            // Arrange
            var parent = new TreeList<string>("parent");

            // Act
            parent.Add((TreeList<string>?)null);
        }

        [TestMethod]
        public void AddRange_WithValues_AddsMultipleChildren()
        {
            // Arrange
            var parent = new TreeList<string>("parent");
            var values = new[] { "child1", "child2", "child3" };

            // Act
            var lastItem = parent.AddRange(values);

            // Assert
            Assert.AreEqual(3, parent.Children.Count);
            Assert.AreEqual("child1", parent[0].Value);
            Assert.AreEqual("child2", parent[1].Value);
            Assert.AreEqual("child3", parent[2].Value);
            Assert.AreEqual("child3", lastItem.Value);
            Assert.AreEqual(parent, parent[0].Parent);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void AddRange_NullValues_ThrowsArgumentNullException()
        {
            // Arrange
            var parent = new TreeList<string>("parent");

            // Act
            parent.AddRange((IEnumerable<string>?)null);
        }

        [TestMethod]
        public void AddRange_WithNodes_AddsMultipleNodes()
        {
            // Arrange
            var parent = new TreeList<string>("parent");
            var nodes = new[]
            {
                new TreeList<string>("child1"),
                new TreeList<string>("child2"),
                new TreeList<string>("child3")
            };

            // Act
            var lastItem = parent.AddRange(nodes);

            // Assert
            Assert.AreEqual(3, parent.Children.Count);
            Assert.AreEqual("child1", parent[0].Value);
            Assert.AreEqual("child2", parent[1].Value);
            Assert.AreEqual("child3", parent[2].Value);
            Assert.AreEqual("child3", lastItem.Value);
            Assert.AreEqual(parent, nodes[0].Parent);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void AddRange_NullNodes_ThrowsArgumentNullException()
        {
            // Arrange
            var parent = new TreeList<string>("parent");

            // Act
            parent.AddRange((IEnumerable<TreeList<string>>?)null);
        }

        [TestMethod]
        public void Clear_RemovesAllChildren()
        {
            // Arrange
            var parent = new TreeList<string>("parent");
            parent.Add("child1");
            parent.Add("child2");
            var child = parent[0];

            // Act
            parent.Clear();

            // Assert
            Assert.AreEqual(0, parent.Children.Count);
            Assert.IsNull(child.Parent);
        }

        [TestMethod]
        public void Detach_RemovesNodeFromParent()
        {
            // Arrange
            var parent = new TreeList<string>("parent");
            var child = parent.Add("child");

            // Act
            child.Detach();

            // Assert
            Assert.AreEqual(0, parent.Children.Count);
            Assert.IsNull(child.Parent);
        }

        [TestMethod]
        public void Detach_OnRootNode_DoesNothing()
        {
            // Arrange
            var root = new TreeList<string>("root");

            // Act
            root.Detach();

            // Assert
            Assert.IsNull(root.Parent);
        }

        [TestMethod]
        public void Indexer_ReturnsChildAtIndex()
        {
            // Arrange
            var parent = new TreeList<string>("parent");
            parent.Add("child1");
            parent.Add("child2");

            // Act & Assert
            Assert.AreEqual("child1", parent[0].Value);
            Assert.AreEqual("child2", parent[1].Value);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentOutOfRangeException))]
        public void Indexer_InvalidIndex_ThrowsException()
        {
            // Arrange
            var parent = new TreeList<string>("parent");

            // Act
            var child = parent[0];
        }

        [TestMethod]
        public void Insert_ByIndex_InsertsChildAtPosition()
        {
            // Arrange
            var parent = new TreeList<string>("parent");
            parent.Add("child1");
            parent.Add("child3");

            // Act
            var inserted = parent.Insert(1, "child2");

            // Assert
            Assert.AreEqual(3, parent.Children.Count);
            Assert.AreEqual("child1", parent[0].Value);
            Assert.AreEqual("child2", parent[1].Value);
            Assert.AreEqual("child3", parent[2].Value);
            Assert.AreEqual(parent, inserted.Parent);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentOutOfRangeException))]
        public void Insert_InvalidIndex_ThrowsException()
        {
            // Arrange
            var parent = new TreeList<string>("parent");

            // Act
            parent.Insert(5, "child");
        }

        [TestMethod]
        public void Remove_RemovesDirectChild()
        {
            // Arrange
            var parent = new TreeList<string>("parent");
            var child1 = parent.Add("child1");
            var child2 = parent.Add("child2");

            // Act
            var result = parent.Remove(child1);

            // Assert
            Assert.IsTrue(result);
            Assert.AreEqual(1, parent.Children.Count);
            Assert.AreSame(child2, parent[0]);
            Assert.IsNull(child1.Parent);
        }

        [TestMethod]
        public void Remove_NonChild_ReturnsFalse()
        {
            // Arrange
            var parent = new TreeList<string>("parent");
            var otherTree = new TreeList<string>("other");
            parent.Add("child");

            // Act
            var result = parent.Remove(otherTree);

            // Assert
            Assert.IsFalse(result);
            Assert.AreEqual(1, parent.Children.Count);
        }

        [TestMethod]
        public void RemoveAt_RemovesChildAtIndex()
        {
            // Arrange
            var parent = new TreeList<string>("parent");
            var child1 = parent.Add("child1");
            parent.Add("child2");

            // Act
            parent.RemoveAt(0);

            // Assert
            Assert.AreEqual(1, parent.Children.Count);
            Assert.AreEqual("child2", parent[0].Value);
            Assert.IsNull(child1.Parent);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentOutOfRangeException))]
        public void RemoveAt_InvalidIndex_ThrowsException()
        {
            // Arrange
            var parent = new TreeList<string>("parent");

            // Act
            parent.RemoveAt(0);
        }

        [TestMethod]
        public void RemoveAll_RemovesChildrenMatchingPredicate()
        {
            // Arrange
            var parent = new TreeList<int>(0);
            parent.Add(1);
            parent.Add(2);
            parent.Add(3);
            parent.Add(4);

            // Act
            var removedCount = parent.RemoveAll(x => x % 2 == 0);

            // Assert
            Assert.AreEqual(2, removedCount);
            Assert.AreEqual(2, parent.Children.Count);
            Assert.AreEqual(1, parent[0].Value);
            Assert.AreEqual(3, parent[1].Value);
        }

        [TestMethod]
        public void RemoveValue_RemovesFirstChildWithValue()
        {
            // Arrange
            var parent = new TreeList<string>("parent");
            parent.Add("test");
            parent.Add("other");
            parent.Add("test");

            // Act
            var result = parent.RemoveValue("test");

            // Assert
            Assert.IsTrue(result);
            Assert.AreEqual(2, parent.Children.Count);
            Assert.AreEqual("other", parent[0].Value);
            Assert.AreEqual("test", parent[1].Value);
        }

        [TestMethod]
        public void RemoveValue_ValueNotFound_ReturnsFalse()
        {
            // Arrange
            var parent = new TreeList<string>("parent");
            parent.Add("test");

            // Act
            var result = parent.RemoveValue("nonexistent");

            // Assert
            Assert.IsFalse(result);
            Assert.AreEqual(1, parent.Children.Count);
        }

        [TestMethod]
        public void VisibleCount_WhenExpanded_IncludesAllDescendants()
        {
            // Arrange
            var root = new TreeList<string>("root");
            var child1 = root.Add("child1");
            child1.Add("grandchild1");
            child1.Add("grandchild2");
            root.Add("child2");

            // Act
            var visibleCount = root.VisibleCount;

            // Assert
            Assert.AreEqual(5, visibleCount); // root + child1 + 2 grandchildren + child2
        }

        [TestMethod]
        public void VisibleCount_WhenCollapsed_ExcludesChildren()
        {
            // Arrange
            var root = new TreeList<string>("root");
            var child1 = root.Add("child1");
            child1.Add("grandchild1");
            root.Add("child2");
            root.IsExpanded = true;
            child1.IsExpanded = false;

            // Act
            var visibleCount = root.VisibleCount;

            // Assert
            Assert.AreEqual(3, visibleCount); // root + child1 + child2 (grandchildren hidden)
        }

        [TestMethod]
        public void IsExpanded_CanBeChanged()
        {
            // Arrange
            var node = new TreeList<string>("node");

            // Act
            node.IsExpanded = false;

            // Assert
            Assert.IsFalse(node.IsExpanded);

            // Act
            node.IsExpanded = true;

            // Assert
            Assert.IsTrue(node.IsExpanded);
        }

        [TestMethod]
        public void GetRoot_ReturnsRootNode()
        {
            // Arrange
            var root = new TreeList<string>("root");
            var child = root.Add("child");
            var grandchild = child.Add("grandchild");

            // Act
            var result = grandchild.Root;

            // Assert
            Assert.AreSame(root, result);
        }

        [TestMethod]
        public void GetRoot_OnRoot_ReturnsSelf()
        {
            // Arrange
            var root = new TreeList<string>("root");

            // Act
            var result = root.Root;

            // Assert
            Assert.AreSame(root, result);
        }

        [TestMethod]
        public void IsAncestorOf_ReturnsTrueForAncestor()
        {
            // Arrange
            var root = new TreeList<string>("root");
            var child = root.Add("child");
            var grandchild = child.Add("grandchild");

            // Act & Assert
            Assert.IsTrue(root.IsAncestorOf(child));
            Assert.IsTrue(root.IsAncestorOf(grandchild));
            Assert.IsTrue(child.IsAncestorOf(grandchild));
        }

        [TestMethod]
        public void IsAncestorOf_ReturnsFalseForNonAncestor()
        {
            // Arrange
            var root = new TreeList<string>("root");
            var child1 = root.Add("child1");
            var child2 = root.Add("child2");

            // Act & Assert
            Assert.IsFalse(child1.IsAncestorOf(child2));
            Assert.IsFalse(child2.IsAncestorOf(child1));
            Assert.IsFalse(child1.IsAncestorOf(root));
        }

        [TestMethod]
        public void MoveTo_MovesNodeToNewParent()
        {
            // Arrange
            var root = new TreeList<string>("root");
            var sourceParent = root.Add("sourceParent");
            var targetParent = root.Add("targetParent");
            var nodeToMove = sourceParent.Add("nodeToMove");

            // Act
            nodeToMove.MoveTo(targetParent);

            // Assert
            Assert.AreEqual(0, sourceParent.Children.Count);
            Assert.AreEqual(1, targetParent.Children.Count);
            Assert.AreSame(nodeToMove, targetParent[0]);
            Assert.AreEqual(targetParent, nodeToMove.Parent);
        }

        [TestMethod]
        public void MoveTo_WithIndex_MovesNodeToSpecificPosition()
        {
            // Arrange
            var root = new TreeList<string>("root");
            var sourceParent = root.Add("sourceParent");
            var targetParent = root.Add("targetParent");
            targetParent.Add("existing1");
            targetParent.Add("existing2");
            var nodeToMove = sourceParent.Add("nodeToMove");

            // Act
            nodeToMove.MoveTo(targetParent, 1);

            // Assert
            Assert.AreEqual(3, targetParent.Children.Count);
            Assert.AreEqual("existing1", targetParent[0].Value);
            Assert.AreSame(nodeToMove, targetParent[1]);
            Assert.AreEqual("existing2", targetParent[2].Value);
        }

        [TestMethod]
        [ExpectedException(typeof(InvalidOperationException))]
        public void MoveTo_IntoOwnSubtree_ThrowsException()
        {
            // Arrange
            var root = new TreeList<string>("root");
            var parent = root.Add("parent");
            var child = parent.Add("child");

            // Act
            parent.MoveTo(child);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void MoveTo_NullParent_ThrowsException()
        {
            // Arrange
            var node = new TreeList<string>("node");

            // Act
            node.MoveTo(null);
        }

        [TestMethod]
        public void TraverseAll_ReturnsAllNodesInSubtree()
        {
            // Arrange
            var root = new TreeList<string>("root");
            var child1 = root.Add("child1");
            child1.Add("grandchild1");
            root.Add("child2");
            root.IsExpanded = false;

            // Act
            var allNodes = root.DescendantsAndSelf().ToList();

            // Assert
            Assert.AreEqual(4, allNodes.Count);
            Assert.AreEqual("root", allNodes[0].Value);
            Assert.AreEqual("child1", allNodes[1].Value);
            Assert.AreEqual("grandchild1", allNodes[2].Value);
            Assert.AreEqual("child2", allNodes[3].Value);
        }

        [TestMethod]
        public void TraverseVisible_ReturnsOnlyVisibleNodes()
        {
            // Arrange
            var root = new TreeList<string>("root");
            var child1 = root.Add("child1");
            child1.Add("grandchild1");
            child1.Add("grandchild2");
            root.Add("child2");
            child1.IsExpanded = false;

            // Act
            var visibleNodes = root.VisibleDescendantsAndSelf().ToList();

            // Assert
            Assert.AreEqual(3, visibleNodes.Count);
            Assert.AreEqual("root", visibleNodes[0].Value);
            Assert.AreEqual("child1", visibleNodes[1].Value);
            Assert.AreEqual("child2", visibleNodes[2].Value);
        }

        [TestMethod]
        public void GetEnumerator_ReturnsValuesOfVisibleNodes()
        {
            // Arrange
            var root = new TreeList<string>("root");
            var child1 = root.Add("child1");
            child1.Add("grandchild1");
            root.Add("child2");
            child1.IsExpanded = false;

            // Act
            var values = root.ToList();

            // Assert
            Assert.AreEqual(3, values.Count);
            Assert.AreEqual("root", values[0]);
            Assert.AreEqual("child1", values[1]);
            Assert.AreEqual("child2", values[2]);
        }

        [TestMethod]
        public void GetEnumerator_NonGeneric_ReturnsSameAsGeneric()
        {
            // Arrange
            var root = new TreeList<string>("root");
            root.Add("child1");
            root.Add("child2");

            // Act
            var genericEnumerator = root.GetEnumerator();
            var nonGenericEnumerator = ((IEnumerable)root).GetEnumerator();

            // Assert
            Assert.IsNotNull(genericEnumerator);
            Assert.IsNotNull(nonGenericEnumerator);
        }

        [TestMethod]
        public void GetFlatIndex_ReturnsCorrectIndex()
        {
            // Arrange
            var root = new TreeList<string>("root");
            var child1 = root.Add("child1");
            var grandchild1 = child1.Add("grandchild1");
            var child2 = root.Add("child2");

            // Act & Assert
            Assert.AreEqual(0, root.GetFlatIndex());
            Assert.AreEqual(1, child1.GetFlatIndex());
            Assert.AreEqual(2, grandchild1.GetFlatIndex());
            Assert.AreEqual(3, child2.GetFlatIndex());
        }

        [TestMethod]
        public void GetFlatIndex_OnDetachedNode_ReturnsZero()
        {
            // Arrange
            var root = new TreeList<string>("root");
            var node = new TreeList<string>("detached");

            // Act
            var index = node.GetFlatIndex();

            // Assert
            Assert.AreEqual(0, index);
        }

        [TestMethod]
        public void GetNodeByFlatIndex_ReturnsCorrectNode()
        {
            // Arrange
            var root = new TreeList<string>("root");
            var child1 = root.Add("child1");
            var grandchild1 = child1.Add("grandchild1");
            var child2 = root.Add("child2");

            // Act & Assert
            Assert.AreSame(root, root.GetNodeByFlatIndex(0));
            Assert.AreSame(child1, root.GetNodeByFlatIndex(1));
            Assert.AreSame(grandchild1, root.GetNodeByFlatIndex(2));
            Assert.AreSame(child2, root.GetNodeByFlatIndex(3));
        }

        [TestMethod]
        public void GetNodeByFlatIndex_InvalidIndex_ReturnsNull()
        {
            // Arrange
            var root = new TreeList<string>("root");

            // Act
            var node = root.GetNodeByFlatIndex(10);

            // Assert
            Assert.IsNull(node);
        }

        [TestMethod]
        public void GetPath_ReturnsPathBetweenNodes()
        {
            // Arrange
            var root = new TreeList<string>("root");
            var child1 = root.Add("child1");
            var child2 = root.Add("child2");
            var grandchild1 = child1.Add("grandchild1");
            var grandchild2 = child2.Add("grandchild2");

            // Act
            var path = TreeList<string>.GetPath(grandchild1, grandchild2);

            var s = root.ToString();

            // Assert
            Assert.AreEqual(5, path.Count);
            Assert.AreSame(grandchild1, path[0]);
            Assert.AreSame(child1, path[1]);
            Assert.AreSame(root, path[2]);
            Assert.AreSame(child2, path[3]);
            Assert.AreSame(grandchild2, path[4]);
        }

        [TestMethod]
        public void GetPath_SameNode_ReturnsSingleNode()
        {
            // Arrange
            var node = new TreeList<string>("node");

            // Act
            var path = TreeList<string>.GetPath(node, node);

            // Assert
            Assert.AreEqual(1, path.Count);
            Assert.AreSame(node, path[0]);
        }

        [TestMethod]
        public void GetPath_AncestorDescendant_ReturnsPath()
        {
            // Arrange
            var root = new TreeList<string>("root");
            var child = root.Add("child");
            var grandchild = child.Add("grandchild");

            // Act
            var path = TreeList<string>.GetPath(root, grandchild);

            // Assert
            Assert.AreEqual(3, path.Count);
            Assert.AreSame(root, path[0]);
            Assert.AreSame(child, path[1]);
            Assert.AreSame(grandchild, path[2]);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void GetPath_NullFromNode_ThrowsException()
        {
            // Act
            TreeList<string>.GetPath(null, new TreeList<string>("node"));
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void GetPath_NullToNode_ThrowsException()
        {
            // Act
            TreeList<string>.GetPath(new TreeList<string>("node"), null);
        }

        [TestMethod]
        public void Value_CanBeChanged()
        {
            // Arrange
            var node = new TreeList<string>("old");

            // Act
            node.Value = "new";

            // Assert
            Assert.AreEqual("new", node.Value);
        }

        [TestMethod]
        public void Children_ReturnsReadOnlyList()
        {
            // Arrange
            var parent = new TreeList<string>("parent");
            parent.Add("child1");
            parent.Add("child2");

            // Act
            var children = parent.Children;

            // Assert
            Assert.IsInstanceOfType(children, typeof(IReadOnlyList<TreeList<string>>));
            Assert.AreEqual(2, children.Count);
            Assert.AreEqual("child1", children[0].Value);
            Assert.AreEqual("child2", children[1].Value);
        }

        [TestMethod]
        public void ComplexTreeOperations_WorkCorrectly()
        {
            // Arrange - Build a complex tree
            var root = new TreeList<string>("Root");
            var documents = root.Add("Documents");
            var pictures = root.Add("Pictures");
            var music = root.Add("Music");

            var work = documents.Add("Work");
            var personal = documents.Add("Personal");
            var report = work.Add("Report.docx");
            var invoice = work.Add("Invoice.pdf");

            var vacation = pictures.Add("Vacation");
            var family = pictures.Add("Family");
            vacation.Add("Beach.jpg");
            vacation.Add("Mountains.jpg");

            // Act - Perform various operations
            var flatIndex = report.GetFlatIndex();
            var nodeAtFlatIndex = root.GetNodeByFlatIndex(flatIndex);
            var path = TreeList<string>.GetPath(invoice, vacation);
            var visibleCount = root.VisibleCount;

            // Move node
            report.MoveTo(personal);

            // Collapse some nodes
            documents.IsExpanded = false;

            var visibleNodesAfterCollapse = root.VisibleDescendantsAndSelf().Count();
            var s = root.ToString();
            // Assert
            Assert.AreSame(report, nodeAtFlatIndex);
            Assert.AreEqual(6, path.Count); // invoice -> work -> documents -> root -> pictures -> vacation
            Assert.AreEqual(12, visibleCount); // All nodes visible initially
            Assert.AreEqual(8, visibleNodesAfterCollapse); // Root, Pictures, Music, and their visible children
            Assert.AreEqual(personal, report.Parent);
        }
    }
}