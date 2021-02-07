using DarkUI.Controls;
using DarkUI.Docking;

namespace Example
{
    public partial class DockProject : DarkToolWindow
    {
        public DockProject()
        {
            this.InitializeComponent();

            // Build dummy nodes
            var childCount = 0;
            for (var i = 0; i < 20; i++)
            {
                var node = new DarkTreeNode($"Root node #{i}");

                for (var x = 0; x < 10; x++)
                {
                    var childNode = new DarkTreeNode($"Child node #{childCount}")
                    {
                    };
                    childCount++;
                    node.Nodes.Add(childNode);
                }

                this.treeProject.Nodes.Add(node);
            }
        }
    }
}