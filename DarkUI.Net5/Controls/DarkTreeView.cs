using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using DarkUI.Collections;
using DarkUI.Config;
using DarkUI.Extensions;
using DarkUI.Forms;

namespace DarkUI.Controls
{
    public class DarkTreeView : DarkScrollView
    {
        private readonly int _expandAreaSize = 16;

        private readonly int _iconSize = 16;

        private readonly ObservableCollection<DarkTreeNode> _selectedNodes;

        private DarkTreeNode _anchoredNodeEnd;

        private DarkTreeNode _anchoredNodeStart;

        private bool _disposed;

        private List<DarkTreeNode> _dragNodes;

        private Point _dragPos;

        private DarkTreeNode _dropNode;

        private int _indent = 20;

        private int _itemHeight = 20;

        private Bitmap _nodeClosed;

        private Bitmap _nodeClosedHover;

        private Bitmap _nodeClosedHoverSelected;

        private Bitmap _nodeOpen;

        private Bitmap _nodeOpenHover;

        private Bitmap _nodeOpenHoverSelected;

        private ObservableList<DarkTreeNode> _nodes;

        private bool _provisionalDragging;

        private DarkTreeNode _provisionalNode;

        public DarkTreeView()
        {
            this.Nodes = new ObservableList<DarkTreeNode>();
            this._selectedNodes = new ObservableCollection<DarkTreeNode>();
            this._selectedNodes.CollectionChanged += this.SelectedNodes_CollectionChanged;

            this.MaxDragChange = this._itemHeight;

            this.LoadIcons();
        }

        public event EventHandler AfterNodeCollapse;

        public event EventHandler AfterNodeExpand;

        public event EventHandler SelectedNodesChanged;

        [Category("Behavior")]
        [Description("Determines whether nodes can be moved within this tree view.")]
        [DefaultValue(false)]
        public bool AllowMoveNodes { get; set; }

        [Category("Appearance")]
        [Description("Determines the amount of horizontal space given by parent node.")]
        [DefaultValue(20)]
        public int Indent
        {
            get => this._indent;

            set
            {
                this._indent = value;
                this.UpdateNodes();
            }
        }

        [Category("Appearance")]
        [Description("Determines the height of tree nodes.")]
        [DefaultValue(20)]
        public int ItemHeight
        {
            get => this._itemHeight;

            set
            {
                this._itemHeight = value;
                this.MaxDragChange = this._itemHeight;
                this.UpdateNodes();
            }
        }

        [Category("Behavior")]
        [Description("Determines whether multiple tree nodes can be selected at once.")]
        [DefaultValue(false)]
        public bool MultiSelect { get; set; }

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public ObservableList<DarkTreeNode> Nodes
        {
            get => this._nodes;

            set
            {
                if (this._nodes != null)
                {
                    this._nodes.ItemsAdded -= this.Nodes_ItemsAdded;
                    this._nodes.ItemsRemoved -= this.Nodes_ItemsRemoved;

                    foreach (DarkTreeNode node in this._nodes)
                    {
                        this.UnhookNodeEvents(node);
                    }
                }

                this._nodes = value;

                this._nodes.ItemsAdded += this.Nodes_ItemsAdded;
                this._nodes.ItemsRemoved += this.Nodes_ItemsRemoved;

                foreach (DarkTreeNode node in this._nodes)
                {
                    this.HookNodeEvents(node);
                }

                this.UpdateNodes();
            }
        }

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public ObservableCollection<DarkTreeNode> SelectedNodes => this._selectedNodes;

        [Category("Appearance")]
        [Description("Determines whether icons are rendered with the tree nodes.")]
        [DefaultValue(false)]
        public bool ShowIcons { get; set; }

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public IComparer<DarkTreeNode> TreeViewNodeSorter { get; set; }

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public int VisibleNodeCount { get; private set; }

        public void EnsureVisible()
        {
            if (this.SelectedNodes.Count == 0)
            {
                return;
            }

            foreach (DarkTreeNode node in this.SelectedNodes)
            {
                node.EnsureVisible();
            }

            var itemTop = -1;

            if (!this.MultiSelect)
            {
                itemTop = this.SelectedNodes[0].FullArea.Top;
            }
            else
            {
                itemTop = this._anchoredNodeEnd.FullArea.Top;
            }

            var itemBottom = itemTop + this.ItemHeight;

            if (itemTop < this.Viewport.Top)
            {
                this.VScrollTo(itemTop);
            }

            if (itemBottom > this.Viewport.Bottom)
            {
                this.VScrollTo((itemBottom - this.Viewport.Height));
            }
        }

        public DarkTreeNode FindNode(string path)
        {
            foreach (DarkTreeNode node in this.Nodes)
            {
                DarkTreeNode compNode = this.FindNode(node, path);
                if (compNode != null)
                {
                    return compNode;
                }
            }

            return null;
        }

        public Rectangle GetNodeFullRowArea(DarkTreeNode node)
        {
            if (node.ParentNode != null && !node.ParentNode.Expanded)
            {
                return new Rectangle(-1, -1, -1, -1);
            }

            var width = Math.Max(this.ContentSize.Width, this.Viewport.Width);
            var rect = new Rectangle(0, node.FullArea.Top, width, this.ItemHeight);
            return rect;
        }

        public void SelectNode(DarkTreeNode node)
        {
            this._selectedNodes.Clear();
            this._selectedNodes.Add(node);

            this._anchoredNodeStart = node;
            this._anchoredNodeEnd = node;

            this.Invalidate();
        }

        public void SelectNodes(DarkTreeNode startNode, DarkTreeNode endNode)
        {
            var nodes = new List<DarkTreeNode>();

            if (startNode == endNode)
            {
                nodes.Add(startNode);
            }

            if (startNode.VisibleIndex < endNode.VisibleIndex)
            {
                DarkTreeNode node = startNode;
                nodes.Add(node);
                while (node != endNode && node != null)
                {
                    node = node.NextVisibleNode;
                    nodes.Add(node);
                }
            }
            else if (startNode.VisibleIndex > endNode.VisibleIndex)
            {
                DarkTreeNode node = startNode;
                nodes.Add(node);
                while (node != endNode && node != null)
                {
                    node = node.PrevVisibleNode;
                    nodes.Add(node);
                }
            }

            this.SelectNodes(nodes, false);
        }

        public void SelectNodes(List<DarkTreeNode> nodes, bool updateAnchors = true)
        {
            this._selectedNodes.Clear();

            foreach (DarkTreeNode node in nodes)
            {
                this._selectedNodes.Add(node);
            }

            if (updateAnchors && this._selectedNodes.Count > 0)
            {
                this._anchoredNodeStart = this._selectedNodes[this._selectedNodes.Count - 1];
                this._anchoredNodeEnd = this._selectedNodes[this._selectedNodes.Count - 1];
            }

            this.Invalidate();
        }

        public void Sort()
        {
            if (this.TreeViewNodeSorter == null)
            {
                return;
            }

            this.Nodes.Sort(this.TreeViewNodeSorter);

            foreach (DarkTreeNode node in this.Nodes)
            {
                this.SortChildNodes(node);
            }
        }

        public void ToggleNode(DarkTreeNode node)
        {
            if (this._selectedNodes.Contains(node))
            {
                this._selectedNodes.Remove(node);

                // If we just removed both the anchor start AND end then reset them
                if (this._anchoredNodeStart == node && this._anchoredNodeEnd == node)
                {
                    if (this._selectedNodes.Count > 0)
                    {
                        this._anchoredNodeStart = this._selectedNodes[0];
                        this._anchoredNodeEnd = this._selectedNodes[0];
                    }
                    else
                    {
                        this._anchoredNodeStart = null;
                        this._anchoredNodeEnd = null;
                    }
                }

                // If we just removed the anchor start then update it accordingly
                if (this._anchoredNodeStart == node)
                {
                    if (this._anchoredNodeEnd.VisibleIndex < node.VisibleIndex)
                    {
                        this._anchoredNodeStart = node.PrevVisibleNode;
                    }
                    else if (this._anchoredNodeEnd.VisibleIndex > node.VisibleIndex)
                    {
                        this._anchoredNodeStart = node.NextVisibleNode;
                    }
                    else
                    {
                        this._anchoredNodeStart = this._anchoredNodeEnd;
                    }
                }

                // If we just removed the anchor end then update it accordingly
                if (this._anchoredNodeEnd == node)
                {
                    if (this._anchoredNodeStart.VisibleIndex < node.VisibleIndex)
                    {
                        this._anchoredNodeEnd = node.PrevVisibleNode;
                    }
                    else if (this._anchoredNodeStart.VisibleIndex > node.VisibleIndex)
                    {
                        this._anchoredNodeEnd = node.NextVisibleNode;
                    }
                    else
                    {
                        this._anchoredNodeEnd = this._anchoredNodeStart;
                    }
                }
            }
            else
            {
                this._selectedNodes.Add(node);

                this._anchoredNodeStart = node;
                this._anchoredNodeEnd = node;
            }

            this.Invalidate();
        }

        protected virtual bool CanMoveNodes(List<DarkTreeNode> dragNodes, DarkTreeNode dropNode, bool isMoving = false)
        {
            if (dropNode == null)
            {
                return false;
            }

            foreach (DarkTreeNode node in dragNodes)
            {
                if (node == dropNode)
                {
                    if (isMoving)
                    {
                        DarkMessageBox.ShowError($"Cannot move {node.Text}. The destination folder is the same as the source folder.", Application.ProductName);
                    }

                    return false;
                }

                if (node.ParentNode != null && node.ParentNode == dropNode)
                {
                    if (isMoving)
                    {
                        DarkMessageBox.ShowError($"Cannot move {node.Text}. The destination folder is the same as the source folder.", Application.ProductName);
                    }

                    return false;
                }

                DarkTreeNode parentNode = dropNode.ParentNode;
                while (parentNode != null)
                {
                    if (node == parentNode)
                    {
                        if (isMoving)
                        {
                            DarkMessageBox.ShowError($"Cannot move {node.Text}. The destination folder is a subfolder of the source folder.", Application.ProductName);
                        }

                        return false;
                    }

                    parentNode = parentNode.ParentNode;
                }
            }

            return true;
        }

        protected override void Dispose(bool disposing)
        {
            if (!this._disposed)
            {
                this.DisposeIcons();

                if (SelectedNodesChanged != null)
                {
                    SelectedNodesChanged = null;
                }

                if (AfterNodeExpand != null)
                {
                    AfterNodeExpand = null;
                }

                if (AfterNodeCollapse != null)
                {
                    AfterNodeExpand = null;
                }

                if (this._nodes != null)
                {
                    this._nodes.Dispose();
                }

                if (this._selectedNodes != null)
                {
                    this._selectedNodes.CollectionChanged -= this.SelectedNodes_CollectionChanged;
                }

                this._disposed = true;
            }

            base.Dispose(disposing);
        }

        protected virtual bool ForceDropToParent(DarkTreeNode node)
        {
            return false;
        }

        protected virtual void MoveNodes(List<DarkTreeNode> dragNodes, DarkTreeNode dropNode)
        { }

        protected virtual void NodesMoved(List<DarkTreeNode> nodesMoved)
        { }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);

            if (this.IsDragging)
            {
                return;
            }

            if (this.Nodes.Count == 0)
            {
                return;
            }

            if (e.KeyCode != Keys.Down && e.KeyCode != Keys.Up && e.KeyCode != Keys.Left && e.KeyCode != Keys.Right)
            {
                return;
            }

            if (this._anchoredNodeEnd == null)
            {
                if (this.Nodes.Count > 0)
                {
                    this.SelectNode(this.Nodes[0]);
                }

                return;
            }

            if (e.KeyCode == Keys.Down || e.KeyCode == Keys.Up)
            {
                if (this.MultiSelect && ModifierKeys == Keys.Shift)
                {
                    if (e.KeyCode == Keys.Up)
                    {
                        if (this._anchoredNodeEnd.PrevVisibleNode != null)
                        {
                            this.SelectAnchoredRange(this._anchoredNodeEnd.PrevVisibleNode);
                            this.EnsureVisible();
                        }
                    }
                    else if (e.KeyCode == Keys.Down)
                    {
                        if (this._anchoredNodeEnd.NextVisibleNode != null)
                        {
                            this.SelectAnchoredRange(this._anchoredNodeEnd.NextVisibleNode);
                            this.EnsureVisible();
                        }
                    }
                }
                else
                {
                    if (e.KeyCode == Keys.Up)
                    {
                        if (this._anchoredNodeEnd.PrevVisibleNode != null)
                        {
                            this.SelectNode(this._anchoredNodeEnd.PrevVisibleNode);
                            this.EnsureVisible();
                        }
                    }
                    else if (e.KeyCode == Keys.Down)
                    {
                        if (this._anchoredNodeEnd.NextVisibleNode != null)
                        {
                            this.SelectNode(this._anchoredNodeEnd.NextVisibleNode);
                            this.EnsureVisible();
                        }
                    }
                }
            }

            if (e.KeyCode == Keys.Left || e.KeyCode == Keys.Right)
            {
                if (e.KeyCode == Keys.Left)
                {
                    if (this._anchoredNodeEnd.Expanded && this._anchoredNodeEnd.Nodes.Count > 0)
                    {
                        this._anchoredNodeEnd.Expanded = false;
                    }
                    else
                    {
                        if (this._anchoredNodeEnd.ParentNode != null)
                        {
                            this.SelectNode(this._anchoredNodeEnd.ParentNode);
                            this.EnsureVisible();
                        }
                    }
                }
                else if (e.KeyCode == Keys.Right)
                {
                    if (!this._anchoredNodeEnd.Expanded)
                    {
                        this._anchoredNodeEnd.Expanded = true;
                    }
                    else
                    {
                        if (this._anchoredNodeEnd.Nodes.Count > 0)
                        {
                            this.SelectNode(this._anchoredNodeEnd.Nodes[0]);
                            this.EnsureVisible();
                        }
                    }
                }
            }
        }

        protected override void OnMouseDoubleClick(MouseEventArgs e)
        {
            if (ModifierKeys == Keys.Control)
            {
                return;
            }

            if (e.Button == MouseButtons.Left)
            {
                foreach (DarkTreeNode node in this.Nodes)
                {
                    this.CheckNodeDoubleClick(node, this.OffsetMousePosition);
                }
            }

            base.OnMouseDoubleClick(e);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left || e.Button == MouseButtons.Right)
            {
                foreach (DarkTreeNode node in this.Nodes)
                {
                    this.CheckNodeClick(node, this.OffsetMousePosition, e.Button);
                }
            }

            base.OnMouseDown(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);

            foreach (DarkTreeNode node in this.Nodes)
            {
                this.NodeMouseLeave(node);
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (this._provisionalDragging)
            {
                if (this.OffsetMousePosition != this._dragPos)
                {
                    this.StartDrag();
                    this.HandleDrag();
                    return;
                }
            }

            if (this.IsDragging)
            {
                if (this._dropNode != null)
                {
                    Rectangle rect = this.GetNodeFullRowArea(this._dropNode);
                    if (!rect.Contains(this.OffsetMousePosition))
                    {
                        this._dropNode = null;
                        this.Invalidate();
                    }
                }
            }

            this.CheckHover();

            if (this.IsDragging)
            {
                this.HandleDrag();
            }

            base.OnMouseMove(e);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            if (this.IsDragging)
            {
                this.HandleDrop();
            }

            if (this._provisionalDragging)
            {
                if (this._provisionalNode != null)
                {
                    Point pos = this._dragPos;
                    if (this.OffsetMousePosition == pos)
                    {
                        this.SelectNode(this._provisionalNode);
                    }
                }

                this._provisionalDragging = false;
            }

            base.OnMouseUp(e);
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            this.CheckHover();

            base.OnMouseWheel(e);
        }

        protected override void PaintContent(Graphics g)
        {
            foreach (DarkTreeNode node in this.Nodes)
            {
                this.DrawNode(node, g);
            }
        }

        protected override void StartDrag()
        {
            if (!this.AllowMoveNodes)
            {
                this._provisionalDragging = false;
                return;
            }

            // Create initial list of nodes to drag
            this._dragNodes = new List<DarkTreeNode>();
            foreach (DarkTreeNode node in this.SelectedNodes)
            {
                this._dragNodes.Add(node);
            }

            // Clear out any nodes with a parent that is being dragged
            foreach (DarkTreeNode node in this._dragNodes.ToList())
            {
                if (node.ParentNode == null)
                {
                    continue;
                }

                if (this._dragNodes.Contains(node.ParentNode))
                {
                    this._dragNodes.Remove(node);
                }
            }

            this._provisionalDragging = false;

            this.Cursor = Cursors.SizeAll;

            base.StartDrag();
        }

        protected override void StopDrag()
        {
            this._dragNodes = null;
            this._dropNode = null;

            this.Cursor = Cursors.Default;

            this.Invalidate();

            base.StopDrag();
        }

        private void CheckHover()
        {
            if (!this.ClientRectangle.Contains(this.PointToClient(MousePosition)))
            {
                if (this.IsDragging)
                {
                    if (this._dropNode != null)
                    {
                        this._dropNode = null;
                        this.Invalidate();
                    }
                }

                return;
            }

            foreach (DarkTreeNode node in this.Nodes)
            {
                this.CheckNodeHover(node, this.OffsetMousePosition);
            }
        }

        private void CheckNodeClick(DarkTreeNode node, Point location, MouseButtons button)
        {
            Rectangle rect = this.GetNodeFullRowArea(node);
            if (rect.Contains(location))
            {
                if (node.ExpandArea.Contains(location))
                {
                    if (button == MouseButtons.Left)
                    {
                        node.Expanded = !node.Expanded;
                    }
                }
                else
                {
                    if (button == MouseButtons.Left)
                    {
                        if (this.MultiSelect && ModifierKeys == Keys.Shift)
                        {
                            this.SelectAnchoredRange(node);
                        }
                        else if (this.MultiSelect && ModifierKeys == Keys.Control)
                        {
                            this.ToggleNode(node);
                        }
                        else
                        {
                            if (!this.SelectedNodes.Contains(node))
                            {
                                this.SelectNode(node);
                            }

                            this._dragPos = this.OffsetMousePosition;
                            this._provisionalDragging = true;
                            this._provisionalNode = node;
                        }

                        return;
                    }
                    else if (button == MouseButtons.Right)
                    {
                        if (this.MultiSelect && ModifierKeys == Keys.Shift)
                        {
                            return;
                        }

                        if (this.MultiSelect && ModifierKeys == Keys.Control)
                        {
                            return;
                        }

                        if (!this.SelectedNodes.Contains(node))
                        {
                            this.SelectNode(node);
                        }

                        return;
                    }
                }
            }

            if (node.Expanded)
            {
                foreach (DarkTreeNode childNode in node.Nodes)
                {
                    this.CheckNodeClick(childNode, location, button);
                }
            }
        }

        private void CheckNodeDoubleClick(DarkTreeNode node, Point location)
        {
            Rectangle rect = this.GetNodeFullRowArea(node);
            if (rect.Contains(location))
            {
                if (!node.ExpandArea.Contains(location))
                {
                    node.Expanded = !node.Expanded;
                }

                return;
            }

            if (node.Expanded)
            {
                foreach (DarkTreeNode childNode in node.Nodes)
                {
                    this.CheckNodeDoubleClick(childNode, location);
                }
            }
        }

        private void CheckNodeHover(DarkTreeNode node, Point location)
        {
            if (this.IsDragging)
            {
                Rectangle rect = this.GetNodeFullRowArea(node);
                if (rect.Contains(this.OffsetMousePosition))
                {
                    DarkTreeNode newDropNode = this._dragNodes.Contains(node) ? null : node;

                    if (this._dropNode != newDropNode)
                    {
                        this._dropNode = newDropNode;
                        this.Invalidate();
                    }
                }
            }
            else
            {
                var hot = node.ExpandArea.Contains(location);
                if (node.ExpandAreaHot != hot)
                {
                    node.ExpandAreaHot = hot;
                    this.Invalidate();
                }
            }

            foreach (DarkTreeNode childNode in node.Nodes)
            {
                this.CheckNodeHover(childNode, location);
            }
        }

        private void ChildNodes_ItemsAdded(object sender, ObservableListModified<DarkTreeNode> e)
        {
            foreach (DarkTreeNode node in e.Items)
            {
                this.HookNodeEvents(node);
            }

            this.UpdateNodes();
        }

        private void ChildNodes_ItemsRemoved(object sender, ObservableListModified<DarkTreeNode> e)
        {
            foreach (DarkTreeNode node in e.Items)
            {
                if (this.SelectedNodes.Contains(node))
                {
                    this.SelectedNodes.Remove(node);
                }

                this.UnhookNodeEvents(node);
            }

            this.UpdateNodes();
        }

        private void DisposeIcons()
        {
            if (this._nodeClosed != null)
            {
                this._nodeClosed.Dispose();
            }

            if (this._nodeClosedHover != null)
            {
                this._nodeClosedHover.Dispose();
            }

            if (this._nodeClosedHoverSelected != null)
            {
                this._nodeClosedHoverSelected.Dispose();
            }

            if (this._nodeOpen != null)
            {
                this._nodeOpen.Dispose();
            }

            if (this._nodeOpenHover != null)
            {
                this._nodeOpenHover.Dispose();
            }

            if (this._nodeOpenHoverSelected != null)
            {
                this._nodeOpenHoverSelected.Dispose();
            }
        }

        private void DragTimer_Tick(object sender, EventArgs e)
        {
            if (!this.IsDragging)
            {
                this.StopDrag();
                return;
            }

            if (MouseButtons != MouseButtons.Left)
            {
                this.StopDrag();
                return;
            }

            Point pos = this.PointToClient(MousePosition);

            if (this._vScrollBar.Visible)
            {
                // Scroll up
                if (pos.Y < this.ClientRectangle.Top)
                {
                    var difference = (pos.Y - this.ClientRectangle.Top) * -1;

                    if (difference > this.ItemHeight)
                    {
                        difference = this.ItemHeight;
                    }

                    this._vScrollBar.Value = this._vScrollBar.Value - difference;
                }

                // Scroll down
                if (pos.Y > this.ClientRectangle.Bottom)
                {
                    var difference = pos.Y - this.ClientRectangle.Bottom;

                    if (difference > this.ItemHeight)
                    {
                        difference = this.ItemHeight;
                    }

                    this._vScrollBar.Value = this._vScrollBar.Value + difference;
                }
            }

            if (this._hScrollBar.Visible)
            {
                // Scroll left
                if (pos.X < this.ClientRectangle.Left)
                {
                    var difference = (pos.X - this.ClientRectangle.Left) * -1;

                    if (difference > this.ItemHeight)
                    {
                        difference = this.ItemHeight;
                    }

                    this._hScrollBar.Value = this._hScrollBar.Value - difference;
                }

                // Scroll right
                if (pos.X > this.ClientRectangle.Right)
                {
                    var difference = pos.X - this.ClientRectangle.Right;

                    if (difference > this.ItemHeight)
                    {
                        difference = this.ItemHeight;
                    }

                    this._hScrollBar.Value = this._hScrollBar.Value + difference;
                }
            }
        }

        private void DrawNode(DarkTreeNode node, Graphics g)
        {
            Rectangle rect = this.GetNodeFullRowArea(node);

            // 1. Draw background
            Color bgColor = node.Odd ? Colors.HeaderBackground : Colors.GreyBackground;

            if (this.SelectedNodes.Count > 0 && this.SelectedNodes.Contains(node))
            {
                bgColor = this.Focused ? Colors.BlueSelection : Colors.GreySelection;
            }

            if (this.IsDragging && this._dropNode == node)
            {
                bgColor = this.Focused ? Colors.BlueSelection : Colors.GreySelection;
            }

            using (var b = new SolidBrush(bgColor))
            {
                g.FillRectangle(b, rect);
            }

            // 2. Draw plus/minus icon
            if (node.Nodes.Count > 0)
            {
                var pos = new Point(node.ExpandArea.Location.X - 1, node.ExpandArea.Location.Y - 1);

                Bitmap icon = this._nodeOpen;

                if (node.Expanded && !node.ExpandAreaHot)
                {
                    icon = this._nodeOpen;
                }
                else if (node.Expanded && node.ExpandAreaHot && !this.SelectedNodes.Contains(node))
                {
                    icon = this._nodeOpenHover;
                }
                else if (node.Expanded && node.ExpandAreaHot && this.SelectedNodes.Contains(node))
                {
                    icon = this._nodeOpenHoverSelected;
                }
                else if (!node.Expanded && !node.ExpandAreaHot)
                {
                    icon = this._nodeClosed;
                }
                else if (!node.Expanded && node.ExpandAreaHot && !this.SelectedNodes.Contains(node))
                {
                    icon = this._nodeClosedHover;
                }
                else if (!node.Expanded && node.ExpandAreaHot && this.SelectedNodes.Contains(node))
                {
                    icon = this._nodeClosedHoverSelected;
                }

                g.DrawImageUnscaled(icon, pos);
            }

            // 3. Draw icon
            if (this.ShowIcons && node.Icon != null)
            {
                if (node.Expanded && node.ExpandedIcon != null)
                {
                    g.DrawImageUnscaled(node.ExpandedIcon, node.IconArea.Location);
                }
                else
                {
                    g.DrawImageUnscaled(node.Icon, node.IconArea.Location);
                }
            }

            // 4. Draw text
            using (var b = new SolidBrush(Colors.LightText))
            {
                var stringFormat = new StringFormat
                {
                    Alignment = StringAlignment.Near,
                    LineAlignment = StringAlignment.Center
                };

                g.DrawString(node.Text, this.Font, b, node.TextArea, stringFormat);
            }

            // 5. Draw child nodes
            if (node.Expanded)
            {
                foreach (DarkTreeNode childNode in node.Nodes)
                {
                    this.DrawNode(childNode, g);
                }
            }
        }

        private DarkTreeNode FindNode(DarkTreeNode parentNode, string path, bool recursive = true)
        {
            if (parentNode.FullPath == path)
            {
                return parentNode;
            }

            foreach (DarkTreeNode node in parentNode.Nodes)
            {
                if (node.FullPath == path)
                {
                    return node;
                }

                if (recursive)
                {
                    DarkTreeNode compNode = this.FindNode(node, path);
                    if (compNode != null)
                    {
                        return compNode;
                    }
                }
            }

            return null;
        }

        private void HandleDrag()
        {
            if (!this.AllowMoveNodes)
            {
                return;
            }

            DarkTreeNode dropNode = this._dropNode;

            if (dropNode == null)
            {
                if (this.Cursor != Cursors.No)
                {
                    this.Cursor = Cursors.No;
                }

                return;
            }

            if (this.ForceDropToParent(dropNode))
            {
                dropNode = dropNode.ParentNode;
            }

            if (!this.CanMoveNodes(this._dragNodes, dropNode))
            {
                if (this.Cursor != Cursors.No)
                {
                    this.Cursor = Cursors.No;
                }

                return;
            }

            if (this.Cursor != Cursors.SizeAll)
            {
                this.Cursor = Cursors.SizeAll;
            }
        }

        private void HandleDrop()
        {
            if (!this.AllowMoveNodes)
            {
                return;
            }

            DarkTreeNode dropNode = this._dropNode;

            if (dropNode == null)
            {
                this.StopDrag();
                return;
            }

            if (this.ForceDropToParent(dropNode))
            {
                dropNode = dropNode.ParentNode;
            }

            if (this.CanMoveNodes(this._dragNodes, dropNode, true))
            {
                var cachedSelectedNodes = this.SelectedNodes.ToList();

                this.MoveNodes(this._dragNodes, dropNode);

                foreach (DarkTreeNode node in this._dragNodes)
                {
                    if (node.ParentNode == null)
                    {
                        this.Nodes.Remove(node);
                    }
                    else
                    {
                        node.ParentNode.Nodes.Remove(node);
                    }

                    dropNode.Nodes.Add(node);
                }

                if (this.TreeViewNodeSorter != null)
                {
                    dropNode.Nodes.Sort(this.TreeViewNodeSorter);
                }

                dropNode.Expanded = true;

                this.NodesMoved(this._dragNodes);

                foreach (DarkTreeNode node in cachedSelectedNodes)
                {
                    this._selectedNodes.Add(node);
                }
            }

            this.StopDrag();
            this.UpdateNodes();
        }

        private void HookNodeEvents(DarkTreeNode node)
        {
            node.Nodes.ItemsAdded += this.ChildNodes_ItemsAdded;
            node.Nodes.ItemsRemoved += this.ChildNodes_ItemsRemoved;

            node.TextChanged += this.Nodes_TextChanged;
            node.NodeExpanded += this.Nodes_NodeExpanded;
            node.NodeCollapsed += this.Nodes_NodeCollapsed;

            foreach (DarkTreeNode childNode in node.Nodes)
            {
                this.HookNodeEvents(childNode);
            }
        }

        private void LoadIcons()
        {
            this.DisposeIcons();

            this._nodeClosed = TreeViewIcons.node_closed_empty.SetColor(Colors.LightText);
            this._nodeClosedHover = TreeViewIcons.node_closed_empty.SetColor(Colors.BlueHighlight);
            this._nodeClosedHoverSelected = TreeViewIcons.node_closed_full.SetColor(Colors.LightText);
            this._nodeOpen = TreeViewIcons.node_open.SetColor(Colors.LightText);
            this._nodeOpenHover = TreeViewIcons.node_open.SetColor(Colors.BlueHighlight);
            this._nodeOpenHoverSelected = TreeViewIcons.node_open_empty.SetColor(Colors.LightText);
        }

        private void NodeMouseLeave(DarkTreeNode node)
        {
            node.ExpandAreaHot = false;

            foreach (DarkTreeNode childNode in node.Nodes)
            {
                this.NodeMouseLeave(childNode);
            }

            this.Invalidate();
        }

        private void Nodes_ItemsAdded(object sender, ObservableListModified<DarkTreeNode> e)
        {
            foreach (DarkTreeNode node in e.Items)
            {
                node.ParentTree = this;
                node.IsRoot = true;

                this.HookNodeEvents(node);
            }

            if (this.TreeViewNodeSorter != null)
            {
                this.Nodes.Sort(this.TreeViewNodeSorter);
            }

            this.UpdateNodes();
        }

        private void Nodes_ItemsRemoved(object sender, ObservableListModified<DarkTreeNode> e)
        {
            foreach (DarkTreeNode node in e.Items)
            {
                node.ParentTree = this;
                node.IsRoot = true;

                this.HookNodeEvents(node);
            }

            this.UpdateNodes();
        }

        private void Nodes_NodeCollapsed(object sender, EventArgs e)
        {
            this.UpdateNodes();

            if (AfterNodeCollapse != null)
            {
                AfterNodeCollapse(this, null);
            }
        }

        private void Nodes_NodeExpanded(object sender, EventArgs e)
        {
            this.UpdateNodes();

            if (AfterNodeExpand != null)
            {
                AfterNodeExpand(this, null);
            }
        }

        private void Nodes_TextChanged(object sender, EventArgs e)
        {
            this.UpdateNodes();
        }

        private void SelectAnchoredRange(DarkTreeNode node)
        {
            this._anchoredNodeEnd = node;
            this.SelectNodes(this._anchoredNodeStart, this._anchoredNodeEnd);
        }

        private void SelectedNodes_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (SelectedNodesChanged != null)
            {
                SelectedNodesChanged(this, null);
            }
        }

        private void SortChildNodes(DarkTreeNode node)
        {
            node.Nodes.Sort(this.TreeViewNodeSorter);

            foreach (DarkTreeNode childNode in node.Nodes)
            {
                this.SortChildNodes(childNode);
            }
        }

        private void UnhookNodeEvents(DarkTreeNode node)
        {
            node.Nodes.ItemsAdded -= this.ChildNodes_ItemsAdded;
            node.Nodes.ItemsRemoved -= this.ChildNodes_ItemsRemoved;

            node.TextChanged -= this.Nodes_TextChanged;
            node.NodeExpanded -= this.Nodes_NodeExpanded;
            node.NodeCollapsed -= this.Nodes_NodeCollapsed;

            foreach (DarkTreeNode childNode in node.Nodes)
            {
                this.UnhookNodeEvents(childNode);
            }
        }

        private void UpdateNode(DarkTreeNode node, ref DarkTreeNode prevNode, int indent, ref int yOffset,
                                ref bool isOdd, ref int index)
        {
            this.UpdateNodeBounds(node, yOffset, indent);

            yOffset += this.ItemHeight;

            node.Odd = isOdd;
            isOdd = !isOdd;

            node.VisibleIndex = index;
            index++;

            node.PrevVisibleNode = prevNode;

            if (prevNode != null)
            {
                prevNode.NextVisibleNode = node;
            }

            prevNode = node;

            if (node.Expanded)
            {
                foreach (DarkTreeNode childNode in node.Nodes)
                {
                    this.UpdateNode(childNode, ref prevNode, indent + this.Indent, ref yOffset, ref isOdd, ref index);
                }
            }
        }

        private void UpdateNodeBounds(DarkTreeNode node, int yOffset, int indent)
        {
            var expandTop = yOffset + (this.ItemHeight / 2) - (this._expandAreaSize / 2);
            node.ExpandArea = new Rectangle(indent + 3, expandTop, this._expandAreaSize, this._expandAreaSize);

            var iconTop = yOffset + (this.ItemHeight / 2) - (this._iconSize / 2);

            if (this.ShowIcons)
            {
                node.IconArea = new Rectangle(node.ExpandArea.Right + 2, iconTop, this._iconSize, this._iconSize);
            }
            else
            {
                node.IconArea = new Rectangle(node.ExpandArea.Right, iconTop, 0, 0);
            }

            using (Graphics g = this.CreateGraphics())
            {
                var textSize = (int)(g.MeasureString(node.Text, this.Font).Width);
                node.TextArea = new Rectangle(node.IconArea.Right + 2, yOffset, textSize + 1, this.ItemHeight);
            }

            node.FullArea = new Rectangle(indent, yOffset, (node.TextArea.Right - indent), this.ItemHeight);

            if (this.ContentSize.Width < node.TextArea.Right + 2)
            {
                this.ContentSize = new Size(node.TextArea.Right + 2, this.ContentSize.Height);
            }
        }

        private void UpdateNodes()
        {
            if (this.IsDragging)
            {
                return;
            }

            this.ContentSize = new Size(0, 0);

            if (this.Nodes.Count == 0)
            {
                return;
            }

            var yOffset = 0;
            var isOdd = false;
            var index = 0;
            DarkTreeNode prevNode = null;

            for (var i = 0; i <= this.Nodes.Count - 1; i++)
            {
                DarkTreeNode node = this.Nodes[i];
                this.UpdateNode(node, ref prevNode, 0, ref yOffset, ref isOdd, ref index);
            }

            this.ContentSize = new Size(this.ContentSize.Width, yOffset);

            this.VisibleNodeCount = index;

            this.Invalidate();
        }
    }
}